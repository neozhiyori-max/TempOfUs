namespace TempMod.Core;

/// <summary>
/// tempMODのホスト専用ゲームルールエンジン。クライアントはAbilityRequestを送るだけで、
/// 能力の可否、死亡、勝利といったゲーム状態はこのクラスを介してのみ確定する。
/// </summary>
public sealed class RoleEngine
{
    private readonly IRoleGameGateway _gateway;
    private readonly RoleOptions _options;
    private readonly Dictionary<byte, PlayerState> _players = new();
    private readonly Dictionary<byte, BodyState> _bodies = new();
    private readonly List<Footprint> _footprints = new();
    private readonly Dictionary<byte, byte> _lovers = new();
    private readonly Dictionary<byte, float> _activeTrackUntil = new();
    private readonly Dictionary<byte, byte> _trackTargets = new();
    private readonly Dictionary<byte, float> _activeVitalsUntil = new();
    private readonly Dictionary<byte, byte> _witchLinks = new();
    private float _meetingStartedAt = -1;

    public RoleEngine(IRoleGameGateway gateway, RoleOptions? options = null)
    {
        _gateway = gateway;
        _options = options ?? new RoleOptions();
    }

    public IReadOnlyDictionary<byte, PlayerState> Players => _players;
    public IReadOnlyDictionary<byte, BodyState> Bodies => _bodies;
    public IReadOnlyList<Footprint> Footprints => _footprints;
    /// <summary>HUDの点灯判定と能力実行で共有する、対象指定能力の有効射程。</summary>
    public float TargetRange => _options.KillDistance;
    public float UndertakerSpeedMultiplier => _options.UndertakerSpeedMultiplier;
    public float CleanerDuration => _options.CleanerDuration;
    public bool IsMeetingActive { get; private set; }

    /// <summary>
    /// SuperNewRolesのExPlayerControl.IsJackalTeam相当。ジャッカルと、そのジャッカルが作成した
    /// サイドキックを同一チームとして一貫して判定する。
    /// </summary>
    public bool IsJackalTeamMember(byte playerId)
        => _players.TryGetValue(playerId, out var player) && IsJackalTeamRole(player.PrimaryRole);

    /// <summary>
    /// ホストから受信した確定状態を参加者側の表示・入力判定用へ反映する。
    /// ルール判定そのものは常にホストだけが行う。
    /// </summary>
    public void ApplyReplicatedState(IEnumerable<ReplicatedPlayerState> players, IEnumerable<BodyState> bodies)
    {
        foreach (var snapshot in players)
        {
            if (!_players.TryGetValue(snapshot.PlayerId, out var player))
            {
                player = new PlayerState { PlayerId = snapshot.PlayerId, PlayerName = snapshot.PlayerName };
                _players[snapshot.PlayerId] = player;
            }

            player.PrimaryRole = snapshot.PrimaryRole;
            player.IsAlive = snapshot.IsAlive;
            player.Position = snapshot.Position;
            player.HasBarrier = snapshot.HasBarrier;
            player.IsCursed = snapshot.IsCursed;
            player.CurseExpiresAt = snapshot.CurseExpiresAt;
            player.BiteExpiresAt = snapshot.BiteExpiresAt;
            player.PuppetControllerId = snapshot.PuppetControllerId;
            player.PuppetExpiresAt = snapshot.PuppetExpiresAt;
            player.CarriedBodyOwnerId = snapshot.CarriedBodyOwnerId;
            player.RoleErasedOnDeath = snapshot.RoleErasedOnDeath;
            player.SheriffKillsRemaining = snapshot.SheriffKillsRemaining;
            player.MadGuesserShotsThisMeeting = snapshot.MadGuesserShotsThisMeeting;
            player.AbilityCooldowns.Clear();
            foreach (var cooldown in snapshot.AbilityCooldowns)
                player.AbilityCooldowns[cooldown.Key] = cooldown.Value;
            player.EffectExpiresAt.Clear();
            foreach (var effect in snapshot.EffectExpiresAt)
                player.EffectExpiresAt[effect.Key] = effect.Value;
            player.EffectTargets.Clear();
            foreach (var target in snapshot.EffectTargets)
                player.EffectTargets[target.Key] = target.Value;
            player.EffectCounts.Clear();
            foreach (var count in snapshot.EffectCounts)
                player.EffectCounts[count.Key] = count.Value;
            player.SecondaryEffectTargetId = snapshot.SecondaryEffectTargetId;
            player.ImmobilizedUntil = snapshot.ImmobilizedUntil;
        }

        _bodies.Clear();
        foreach (var body in bodies)
            _bodies[body.OwnerId] = body;
    }

    public void RegisterPlayer(byte id, string name)
    {
        if (_players.ContainsKey(id))
            throw new InvalidOperationException($"Player {id} is already registered.");

        _players[id] = new PlayerState { PlayerId = id, PlayerName = name };
    }

    public void AssignRole(byte playerId, RoleId role)
    {
        var player = GetPlayer(playerId);
        player.PrimaryRole = role;
        if (role == RoleId.Sheriff)
            player.SheriffKillsRemaining = Math.Max(0, _options.SheriffKillLimit);
    }

    public void AddModifier(byte playerId, ModifierId modifier)
    {
        GetPlayer(playerId).Modifiers.Add(modifier);
    }

    public void PairLovers(byte firstId, byte secondId, float now)
    {
        if (firstId == secondId)
            throw new InvalidOperationException("Lovers must be two distinct players.");

        var first = GetPlayer(firstId);
        var second = GetPlayer(secondId);
        first.Modifiers.Add(ModifierId.Lovers);
        second.Modifiers.Add(ModifierId.Lovers);
        _lovers[firstId] = secondId;
        _lovers[secondId] = firstId;
        _gateway.Emit(new GameEvent(GameEventKind.LoversPaired, now, firstId, secondId));
    }

    public void UpdatePosition(byte playerId, Position position, float now)
    {
        var player = GetPlayer(playerId);
        if (!player.IsAlive || IsMeetingActive)
            return;

        if (player.ImmobilizedUntil > now)
            return;
        player.Position = position;
        while (player.PositionHistory.Count > 0 && now - player.PositionHistory.Peek().Time > _options.TimeTravelerSeconds + 2f)
            player.PositionHistory.Dequeue();

        var newest = player.PositionHistory.Count > 0 ? player.PositionHistory.Last() : default(PositionSample?);
        if (newest is null || now - newest.Value.Time >= _options.PositionHistoryInterval)
            player.PositionHistory.Enqueue(new PositionSample(now, position));

        if (newest is null || now - newest.Value.Time >= _options.FootprintInterval)
            _footprints.Add(new Footprint(playerId, position, now));
    }

    public void Tick(float now)
    {
        if (IsMeetingActive)
            return;

        _footprints.RemoveAll(step => now - step.CreatedAt > _options.InvestigatorTrailLifetime);
        ExpireTransientEffects(now);
        ResolveAdditionalEffects(now);
        ResolveExpiredBites(now);
        ResolveCurses(now);
        EvaluateVictory(now);
    }

    public void StartMeeting(float now)
    {
        if (IsMeetingActive)
            return;

        IsMeetingActive = true;
        _meetingStartedAt = now;
        _activeTrackUntil.Clear();
        _trackTargets.Clear();
        _activeVitalsUntil.Clear();
        // TownOfUsのUndertakerと同様に、会議へ持ち込まれた死体は会議開始位置で即座に配置する。
        // 会議中にBodyStateが運搬状態のまま残ると、会議後の通報・表示同期が壊れるためである。
        foreach (var carrier in _players.Values.Where(player => player.CarriedBodyOwnerId is not null).ToArray())
            DropCarriedBodyAtCarrier(carrier, now, "会議開始");
        foreach (var player in _players.Values)
        {
            player.PuppetControllerId = null;
            player.PuppetExpiresAt = 0;
            // SuperNewRolesのEvilGuesserShotsPerMeetingと同様に、会議ごとに推測残弾を回復する。
            player.MadGuesserShotsThisMeeting = 0;
        }
    }

    /// <summary>投票で追放された役職の処理。ジェスターは最優先で単独勝利する。</summary>
    public VictoryResult EndMeeting(byte? exiledPlayerId, float now, bool vanillaExileAlreadyApplied = false, bool evaluateVictory = true)
    {
        // RpcCloseは再送されることがあるため、会議が既に終了している場合は安全に無視する。
        if (!IsMeetingActive)
            return VictoryResult.None;

        if (_options.VampireTimerPausesDuringMeeting && _meetingStartedAt >= 0)
        {
            var pausedFor = now - _meetingStartedAt;
            foreach (var player in _players.Values)
            {
                if (player.BiteExpiresAt > 0)
                    player.BiteExpiresAt += pausedFor;
                if (player.CurseExpiresAt > 0)
                    player.CurseExpiresAt += pausedFor;
                if (player.PuppetExpiresAt > 0)
                    player.PuppetExpiresAt += pausedFor;
            }
        }

        IsMeetingActive = false;
        _meetingStartedAt = -1;
        if (exiledPlayerId is byte playerId)
        {
            var exiled = GetPlayer(playerId);
                        if (exiled.IsAlive)
            {
                if (vanillaExileAlreadyApplied)
                    exiled.IsAlive = false;
                else
                    KillPlayer(playerId, null, now, "追放", silent: false, erased: false);
            }
            if (exiled.PrimaryRole == RoleId.Jester)
            {
                var result = new VictoryResult(VictoryKind.Jester, new[] { playerId });
                if (evaluateVictory)
                    EmitVictory(result, now);
                return result;
            }
        }
        return evaluateVictory ? EvaluateVictory(now) : VictoryResult.None;
    }

    /// <summary>会議UI用のマッドゲッサー残弾判定。ホスト側のTryMeetingGuessでも同じ条件を再検証する。</summary>
    public bool CanMadGuesserShoot(byte playerId)
        => IsMeetingActive
            && _players.TryGetValue(playerId, out var player)
            && player.IsAlive
            && player.PrimaryRole == RoleId.MadGuesser
            && player.MadGuesserShotsThisMeeting < _options.MadGuesserShotsPerMeeting;

    public int GetMadGuesserShotsRemaining(byte playerId)
        => _players.TryGetValue(playerId, out var player)
            ? Math.Max(0, _options.MadGuesserShotsPerMeeting - player.MadGuesserShotsThisMeeting)
            : 0;

    public int GetVoteWeight(byte playerId)
    {
        var player = GetPlayer(playerId);
        if (!player.IsAlive)
            return 0;

        var advocate = _players.Values.FirstOrDefault(candidate =>
            candidate.IsAlive &&
            candidate.PrimaryRole == RoleId.Advocate &&
            candidate.EffectTargets.TryGetValue(AbilityId.Bribe, out var bribedId) &&
            bribedId == playerId);
        if (advocate != null)
            return 0;

        return player.PrimaryRole == RoleId.Mayor || (player.PrimaryRole == RoleId.Advocate && player.EffectTargets.ContainsKey(AbilityId.Bribe)) ? 2 : 1;
    }

    public IReadOnlyList<Footprint> GetVisibleFootprints(byte viewerId, float now)
    {
        var viewer = GetPlayer(viewerId);
        if (!viewer.IsAlive || viewer.PrimaryRole != RoleId.Investigator)
            return Array.Empty<Footprint>();

        return _footprints
            .Where(step => step.OwnerId != viewerId && now - step.CreatedAt <= _options.InvestigatorTrailLifetime)
            .ToArray();
    }

    public bool CanSeeNormallyDuringLightsOut(byte playerId)
        => GetPlayer(playerId).PrimaryRole == RoleId.LightWorker;

    public bool IsRemoteVitalsOpen(byte playerId, float now)
        => _activeVitalsUntil.TryGetValue(playerId, out var endsAt) && now < endsAt;

    public bool TryGetTrackedTarget(byte playerId, float now, out byte targetId)
    {
        targetId = default;
        if (!_activeTrackUntil.TryGetValue(playerId, out var endsAt) || now >= endsAt)
            return false;
        return _trackTargets.TryGetValue(playerId, out targetId);
    }

    public bool TryGetDeathAgeForDoctor(byte doctorId, byte bodyOwnerId, float now, out float secondsSinceDeath)
    {
        secondsSinceDeath = 0;
        if (GetPlayer(doctorId).PrimaryRole != RoleId.Doctor || !_bodies.TryGetValue(bodyOwnerId, out var body))
            return false;
        secondsSinceDeath = Math.Max(0, now - body.DiedAt);
        return true;
    }

    public float GetMovementSpeedMultiplier(byte playerId)
    {
        var player = GetPlayer(playerId);
        return player.PrimaryRole == RoleId.Undertaker && player.CarriedBodyOwnerId is not null
            ? _options.UndertakerSpeedMultiplier
            : 1f;
    }

    public bool CanStartSeance(byte seerId, byte deadPlayerId, float now)
    {
        var seer = GetPlayer(seerId);
        return seer.PrimaryRole == RoleId.Seer
            && seer.IsAlive
            && CanUse(seer, AbilityId.SpeakWithDead, now)
            && _players.TryGetValue(deadPlayerId, out var deadPlayer)
            && !deadPlayer.IsAlive;
    }

    public bool TryHandleAbility(AbilityRequest request, float now)
    {
        if (IsMeetingActive && request.Ability is not (AbilityId.GuessRole or AbilityId.Bribe or AbilityId.DeceiveVote))
            return Reject(request, now, "会議中はこの能力を使えません。");

        if (!_players.TryGetValue(request.SenderId, out var actor) || !actor.IsAlive)
            return Reject(request, now, "発動者が生存していません。");

        return request.Ability switch
        {
            AbilityId.Kill => TryDirectKill(actor, request.TargetId, now, erased: false),
            AbilityId.EraseKill => TryEraseKill(actor, request.TargetId, now),
            AbilityId.OpenVitals => TryOpenVitals(actor, now),
            AbilityId.Track => TryTrack(actor, request.TargetId, now),
            AbilityId.TimeWarp => TryTimeWarp(actor, now),
            AbilityId.SpeakWithDead => TrySpeakWithDead(actor, request.TargetId, now),
            AbilityId.GrantBarrier => TryGrantBarrier(actor, request.TargetId, now),
            AbilityId.Curse => TryCurse(actor, request.TargetId, now),
            AbilityId.Sabotage => TrySabotage(actor, now),
            AbilityId.Puppet => TryPuppet(actor, request.TargetId, now),
            AbilityId.CarryBody => TryCarryBody(actor, request.TargetId, now),
            AbilityId.DropBody => TryDropBody(actor, request.RequestedPosition, now),
            AbilityId.Bite => TryBite(actor, request.TargetId, now),
            AbilityId.Clean => TryClean(actor, request.TargetId, now),
            AbilityId.CollectDna => TryCollectDna(actor, request.TargetId, now),
            AbilityId.Morph => TryMorph(actor, now),
            AbilityId.PlantBomb => TryPlantBomb(actor, request.TargetId, now),
            AbilityId.SetTrap => TrySetTrap(actor, request.RequestedPosition, now),
            AbilityId.Blackout => TryTimedEffect(actor, RoleId.Blackout, AbilityId.Blackout, now, _options.BlackoutDuration, "目隠し", GameEventKind.BlackoutStarted),
            AbilityId.Phase => TryTimedEffect(actor, RoleId.Phantom, AbilityId.Phase, now, _options.PhantomDuration, "幽体化", GameEventKind.PhaseStarted),
            AbilityId.Silence => TrySilence(actor, request.TargetId, now),
            AbilityId.Devour => TryDevour(actor, request.TargetId, now),
            AbilityId.AnimateBody => TryAnimateBody(actor, request.TargetId, request.RequestedPosition, now),
            AbilityId.LinkCurse => TryLinkCurse(actor, request.TargetId, now),
            AbilityId.CheckBounty => TryCheckBounty(actor, now),
            AbilityId.Omniscience => TryOmniscience(actor, now),
            AbilityId.RecruitSidekick => TryRecruitSidekick(actor, request.TargetId, now),
            AbilityId.InfectKill => TryInfect(actor, request.TargetId, now),
            AbilityId.AbandonTasks => TryApathy(actor, now),
            AbilityId.ConfusionGas => TryConfusionGas(actor, request.TargetId, now),
            AbilityId.SelfDestruct => TrySelfDestruct(actor, now),
            AbilityId.CollectBody => TryCollectBody(actor, request.TargetId, now),
            AbilityId.AbsoluteDefense => TryAbsoluteDefense(actor, request.TargetId, now),
            AbilityId.FanaticWorship => TryFanaticWorship(actor, request.TargetId, now),
            AbilityId.Spectate => TryTimedEffect(actor, RoleId.Spectator, AbilityId.Spectate, now, _options.PhantomDuration, "観戦モード", GameEventKind.AbilityAccepted),
            AbilityId.Assassinate => TryAssassinate(actor, request.TargetId, now),
            AbilityId.MarionetteKill => TryDirectKill(actor, request.TargetId, now, erased: true),
            AbilityId.Wiretap => TryTimedEffect(actor, RoleId.Spy, AbilityId.Wiretap, now, 12f, "盗聴", GameEventKind.AbilityAccepted),
            AbilityId.Hack => TryTimedEffect(actor, RoleId.Hacker, AbilityId.Hack, now, 12f, "偽装工作", GameEventKind.AbilityAccepted),
            AbilityId.CreateIllusion => TryTimedEffect(actor, RoleId.Illusionist, AbilityId.CreateIllusion, now, 15f, "分身生成", GameEventKind.AbilityAccepted),
            AbilityId.StealTime => TryRoleAction(actor, RoleId.TimeThief, AbilityId.StealTime, now),
            AbilityId.DeceiveVote => TryRoleAction(actor, RoleId.Deceptor, AbilityId.DeceiveVote, now),
            AbilityId.AlchemyStealth => TryAlchemyStealth(actor, request.TargetId, now),
            AbilityId.GuessRole => TryMeetingGuess(actor, request.TargetId, request.RequestedPosition, now),
            AbilityId.Bribe => TryBribe(actor, request.TargetId, now),
            AbilityId.Douse => TryDouse(actor, request.TargetId, now),
            AbilityId.Ignite => TryIgnite(actor, now),
            AbilityId.AlignFaction => Reject(actor, AbilityId.AlignFaction, now, "シュレディンガーの猫は攻撃された時に自動で陣営同調します。"),
            AbilityId.StealItem => TryStealItem(actor, request.TargetId, now),
            AbilityId.ForceEject => TryRoleAction(actor, RoleId.Bouncer, AbilityId.ForceEject, now),
            AbilityId.CaptureGhost => TryCaptureGhost(actor, request.TargetId, now),
            AbilityId.StealSkin => TryStealSkin(actor, request.TargetId, now),
            _ => Reject(request, now, "未対応の能力です。"),
        };
    }

    private bool TryMeetingGuess(PlayerState actor, byte? targetId, Position? requestedPosition, float now)
    {
        if (!CanMadGuesserShoot(actor.PlayerId))
            return Reject(actor, AbilityId.GuessRole, now, "会議中のマッドゲッサーだけが、会議ごとの残弾の範囲で推測できます。");
        if (targetId is not byte id || !_players.TryGetValue(id, out var target) || !target.IsAlive || id == actor.PlayerId)
            return Reject(actor, AbilityId.GuessRole, now, "推測対象を選んでください。");
        if (requestedPosition is not Position guessPayload || guessPayload.X < byte.MinValue || guessPayload.X > byte.MaxValue || !Enum.IsDefined(typeof(RoleId), (byte)guessPayload.X))
            return Reject(actor, AbilityId.GuessRole, now, "推測する役職を選んでください。");

        var guessedRole = (RoleId)(byte)guessPayload.X;
        if (RoleCatalog.GetFaction(guessedRole) != Faction.Crew)
            return Reject(actor, AbilityId.GuessRole, now, "クルー役職だけを推測できます。");
        // SuperNewRolesのGuesserAbilityと同様に、推測の成否を問わず会議内の残弾を1つ消費する。
        actor.MadGuesserShotsThisMeeting++;
        if (target.PrimaryRole == guessedRole)
            KillPlayer(target.PlayerId, actor.PlayerId, now, $"推測成功: {RoleCatalog.Get(guessedRole).DisplayName}", silent: false, erased: false);
        else
            KillPlayer(actor.PlayerId, actor.PlayerId, now, $"推測失敗: {RoleCatalog.Get(guessedRole).DisplayName}", silent: false, erased: false);
        return Accept(actor, AbilityId.GuessRole, now);
    }

    private bool TryBribe(PlayerState actor, byte? targetId, float now)
    {
        if (!IsMeetingActive || actor.PrimaryRole != RoleId.Advocate || !CanUse(actor, AbilityId.Bribe, now))
            return Reject(actor, AbilityId.Bribe, now, "会議中のアドボケイトだけが買収できます。");
        if (targetId is not byte id || !_players.TryGetValue(id, out var target) || !target.IsAlive || id == actor.PlayerId)
            return Reject(actor, AbilityId.Bribe, now, "買収対象を選んでください。");
        actor.EffectTargets[AbilityId.Bribe] = id;
        actor.EffectExpiresAt[AbilityId.Bribe] = float.MaxValue;
        SetCooldown(actor, AbilityId.Bribe, now, float.MaxValue / 4f);
        return Accept(actor, AbilityId.Bribe, now);
    }

    private bool TryDouse(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Arsonist || !CanUse(actor, AbilityId.Douse, now) || !TryGetLivingTarget(actor, targetId, now, out var target))
            return Reject(actor, AbilityId.Douse, now, "ガソリンをかける対象に近づいてください。");
        if (target.EffectTargets.TryGetValue(AbilityId.Douse, out var ownerId) && ownerId == actor.PlayerId)
            return Reject(actor, AbilityId.Douse, now, "その対象には既にガソリンをかけています。");
        target.EffectTargets[AbilityId.Douse] = actor.PlayerId;
        SetCooldown(actor, AbilityId.Douse, now, _options.SpecialAbilityCooldown);
        return Accept(actor, AbilityId.Douse, now);
    }

    private bool TryIgnite(PlayerState actor, float now)
    {
        if (actor.PrimaryRole != RoleId.Arsonist || !CanUse(actor, AbilityId.Ignite, now))
            return Reject(actor, AbilityId.Ignite, now, "点火できません。");
        var victims = _players.Values.Where(player => player.IsAlive && player.PlayerId != actor.PlayerId).ToArray();
        if (victims.Length == 0 || victims.Any(player => !player.EffectTargets.TryGetValue(AbilityId.Douse, out var ownerId) || ownerId != actor.PlayerId))
            return Reject(actor, AbilityId.Ignite, now, "生存者全員にガソリンをかける必要があります。");
        foreach (var victim in victims)
            KillPlayer(victim.PlayerId, actor.PlayerId, now, "点火", silent: false, erased: false);
        EmitVictory(new VictoryResult(VictoryKind.Arsonist, new[] { actor.PlayerId }), now);
        return Accept(actor, AbilityId.Ignite, now);
    }

    private bool TryRoleAction(PlayerState actor, RoleId role, AbilityId ability, float now)
    {
        if (actor.PrimaryRole != role || !CanUse(actor, ability, now))
            return Reject(actor, ability, now, $"{RoleCatalog.Get(role).DisplayName}の能力は使用できません。");
        actor.EffectExpiresAt[ability] = now + _options.SpecialAbilityCooldown;
        SetCooldown(actor, ability, now, _options.SpecialAbilityCooldown);
        return Accept(actor, ability, now);
    }

    private bool TryAlchemyStealth(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Alchemist || !CanUse(actor, AbilityId.AlchemyStealth, now) || !TryGetLivingTarget(actor, targetId, now, out var target))
            return Reject(actor, AbilityId.AlchemyStealth, now, "錬金ステルス対象に近づいてください。");
        KillPlayer(target.PlayerId, actor.PlayerId, now, "錬金ステルス", silent: false, erased: false);
        if (_bodies.TryGetValue(target.PlayerId, out var body))
            _bodies[target.PlayerId] = body with { InvisibleUntil = now + _options.AlchemyBodyStealthDuration };
        SetCooldown(actor, AbilityId.AlchemyStealth, now, _options.StandardKillCooldown);
        _gateway.Emit(new GameEvent(GameEventKind.BodyHidden, now, actor.PlayerId, target.PlayerId, _options.AlchemyBodyStealthDuration.ToString("0.0")));
        return Accept(actor, AbilityId.AlchemyStealth, now);
    }

    private bool TryStealItem(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Collector || !CanUse(actor, AbilityId.StealItem, now) || !TryGetLivingTarget(actor, targetId, now, out _))
            return Reject(actor, AbilityId.StealItem, now, "アイテムを奪う対象に近づいてください。");
        actor.EffectCounts[AbilityId.StealItem] = (actor.EffectCounts.TryGetValue(AbilityId.StealItem, out var count) ? count : 0) + 1;
        SetCooldown(actor, AbilityId.StealItem, now, _options.SpecialAbilityCooldown);
        return Accept(actor, AbilityId.StealItem, now);
    }

    private bool TryCaptureGhost(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.GhostHunter || !CanUse(actor, AbilityId.CaptureGhost, now) || targetId is not byte ghostId || !_players.TryGetValue(ghostId, out var ghost) || ghost.IsAlive)
            return Reject(actor, AbilityId.CaptureGhost, now, "捕獲できるゴーストを選んでください。");
        actor.EffectTargets[AbilityId.CaptureGhost] = ghostId;
        actor.EffectCounts[AbilityId.CaptureGhost] = (actor.EffectCounts.TryGetValue(AbilityId.CaptureGhost, out var count) ? count : 0) + 1;
        SetCooldown(actor, AbilityId.CaptureGhost, now, _options.SpecialAbilityCooldown);
        return Accept(actor, AbilityId.CaptureGhost, now);
    }

    private bool TryStealSkin(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Thief || !CanUse(actor, AbilityId.StealSkin, now) || !TryGetLivingTarget(actor, targetId, now, out var target))
            return Reject(actor, AbilityId.StealSkin, now, "スキンを奪う対象に近づいてください。");
        KillPlayer(target.PlayerId, actor.PlayerId, now, "スキン強奪", silent: false, erased: false);
        actor.EffectTargets[AbilityId.StealSkin] = target.PlayerId;
        actor.EffectExpiresAt[AbilityId.StealSkin] = now + 20f;
        SetCooldown(actor, AbilityId.StealSkin, now, _options.StandardKillCooldown);
        return Accept(actor, AbilityId.StealSkin, now);
    }

    private bool TryOmniscience(PlayerState actor, float now)
    {
        if (actor.PrimaryRole != RoleId.God || !CanUse(actor, AbilityId.Omniscience, now))
            return Reject(actor, AbilityId.Omniscience, now, "全知は使用できません。");
        actor.EffectExpiresAt[AbilityId.Omniscience] = float.MaxValue;
        _gateway.Emit(new GameEvent(GameEventKind.AbilityAccepted, now, actor.PlayerId, Detail: "全役職を公開"));
        return Accept(actor, AbilityId.Omniscience, now);
    }

    private bool TryRecruitSidekick(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Jackal || !CanUse(actor, AbilityId.RecruitSidekick, now) || !TryGetLivingTarget(actor, targetId, now, out var target))
            return Reject(actor, AbilityId.RecruitSidekick, now, "勧誘できるクルーに近づいてください。");
        if (RoleCatalog.GetFaction(target.PrimaryRole) != Faction.Crew)
            return Reject(actor, AbilityId.RecruitSidekick, now, "クルーだけをサイドキックにできます。");
        if (_players.Values.Any(player => player.PrimaryRole == RoleId.Sidekick))
            return Reject(actor, AbilityId.RecruitSidekick, now, "すでにサイドキックがいるため、追加で勧誘できません。");
        target.PrimaryRole = RoleId.Sidekick;
        target.EffectTargets[AbilityId.RecruitSidekick] = actor.PlayerId;
        // SuperNewRolesのJackalAbility/CustomSidekickButtonAbilityと同様に、勧誘はキルとは別の専用クールダウンで管理する。
        SetCooldown(actor, AbilityId.RecruitSidekick, now, _options.JackalSidekickCooldown);
        _gateway.Emit(new GameEvent(GameEventKind.RoleChanged, now, actor.PlayerId, target.PlayerId, RoleId.Sidekick.ToString()));
        return Accept(actor, AbilityId.RecruitSidekick, now);
    }

    private bool TryInfect(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Zombie || !CanUse(actor, AbilityId.InfectKill, now) || !TryGetLivingTarget(actor, targetId, now, out var target))
            return Reject(actor, AbilityId.InfectKill, now, "感染できるクルーに近づいてください。");
        if (RoleCatalog.GetFaction(target.PrimaryRole) != Faction.Crew)
            return Reject(actor, AbilityId.InfectKill, now, "クルーだけを感染できます。");
        target.PrimaryRole = RoleId.ChildZombie;
        target.EffectTargets[AbilityId.InfectKill] = actor.PlayerId;
        SetCooldown(actor, AbilityId.InfectKill, now, _options.SpecialAbilityCooldown);
        return Accept(actor, AbilityId.InfectKill, now);
    }

    private bool TryApathy(PlayerState actor, float now)
    {
        if (actor.PrimaryRole != RoleId.Apathy)
            return Reject(actor, AbilityId.AbandonTasks, now, "アパシー専用能力です。");
        actor.EffectExpiresAt[AbilityId.AbandonTasks] = float.MaxValue;
        return Accept(actor, AbilityId.AbandonTasks, now);
    }

    private bool TryConfusionGas(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Clown || !CanUse(actor, AbilityId.ConfusionGas, now) || !TryGetLivingTarget(actor, targetId, now, out var target))
            return Reject(actor, AbilityId.ConfusionGas, now, "錯乱させる対象に近づいてください。");
        target.EffectExpiresAt[AbilityId.ConfusionGas] = now + _options.PhantomDuration;
        SetCooldown(actor, AbilityId.ConfusionGas, now, _options.SpecialAbilityCooldown);
        return Accept(actor, AbilityId.ConfusionGas, now);
    }

    private bool TrySelfDestruct(PlayerState actor, float now)
    {
        if (actor.PrimaryRole != RoleId.Terrorist || !CanUse(actor, AbilityId.SelfDestruct, now))
            return Reject(actor, AbilityId.SelfDestruct, now, "自爆できません。");
        var victims = _players.Values.Where(player => player.IsAlive && player.PlayerId != actor.PlayerId && player.Position.DistanceTo(actor.Position) <= _options.BombRadius).ToArray();
        var won = victims.Any(player => player.PrimaryRole is RoleId.Jackal or RoleId.Sidekick || RoleCatalog.GetFaction(player.PrimaryRole) == Faction.Impostor);
        foreach (var victim in victims)
            KillPlayer(victim.PlayerId, actor.PlayerId, now, "自爆", silent: false, erased: false);
        KillPlayer(actor.PlayerId, actor.PlayerId, now, "自爆", silent: false, erased: false);
        if (won)
            EmitVictory(new VictoryResult(VictoryKind.Terrorist, new[] { actor.PlayerId }), now);
        return Accept(actor, AbilityId.SelfDestruct, now);
    }

    private bool TryCollectBody(PlayerState actor, byte? bodyOwnerId, float now)
    {
        if (actor.PrimaryRole != RoleId.Vulture || !CanUse(actor, AbilityId.CollectBody, now) || !TryGetBodyTarget(actor, bodyOwnerId, out var body))
            return Reject(actor, AbilityId.CollectBody, now, "回収できる死体に近づいてください。");
        _bodies.Remove(body.OwnerId);
        var collected = (actor.EffectCounts.TryGetValue(AbilityId.CollectBody, out var count) ? count : 0) + 1;
        actor.EffectCounts[AbilityId.CollectBody] = collected;
        // SNR VultureのEatDeadBodyAbilityと同じく、ハゲタカ専用クールダウンを使用する。
        SetCooldown(actor, AbilityId.CollectBody, now, _options.VultureCooldown);
        var accepted = Accept(actor, AbilityId.CollectBody, now);
        // SNRでは必要数に達した回収そのものが即座に単独勝利を発火する。
        if (collected >= _options.VultureRequiredBodies)
            EmitVictory(new VictoryResult(VictoryKind.Vulture, new[] { actor.PlayerId }), now);
        return accepted;
    }

    private bool TryAbsoluteDefense(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Guardian || !TryGetLivingTarget(actor, targetId, now, out var target))
            return Reject(actor, AbilityId.AbsoluteDefense, now, "守るクルーに近づいてください。");
        if (RoleCatalog.GetFaction(target.PrimaryRole) != Faction.Crew)
            return Reject(actor, AbilityId.AbsoluteDefense, now, "クルーだけを守れます。");
        actor.EffectTargets[AbilityId.AbsoluteDefense] = target.PlayerId;
        target.HasBarrier = true;
        return Accept(actor, AbilityId.AbsoluteDefense, now);
    }

    private bool TryFanaticWorship(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Fanatic || !TryGetLivingTarget(actor, targetId, now, out var target))
            return Reject(actor, AbilityId.FanaticWorship, now, "崇拝するインポスターに近づいてください。");
        if (RoleCatalog.GetFaction(target.PrimaryRole) != Faction.Impostor)
            return Reject(actor, AbilityId.FanaticWorship, now, "インポスターだけを崇拝できます。");
        actor.EffectTargets[AbilityId.FanaticWorship] = target.PlayerId;
        target.NextKillAt = Math.Min(target.NextKillAt, now + _options.StandardKillCooldown * .5f);
        return Accept(actor, AbilityId.FanaticWorship, now);
    }

    private bool TryAssassinate(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Assassin || !CanUse(actor, AbilityId.Assassinate, now) || !TryGetLivingTarget(actor, targetId, now, out var target))
            return Reject(actor, AbilityId.Assassinate, now, "暗殺対象に近づいてください。");
        KillPlayer(target.PlayerId, actor.PlayerId, now, "暗殺", silent: false, erased: false);
        SetCooldown(actor, AbilityId.Assassinate, now, float.MaxValue / 4f);
        _gateway.Emit(new GameEvent(GameEventKind.AbilityAccepted, now, actor.PlayerId, target.PlayerId, $"暗殺位置: {actor.Position.X:0.0},{actor.Position.Y:0.0}", actor.Position));
        return Accept(actor, AbilityId.Assassinate, now);
    }

    private bool TryClean(PlayerState actor, byte? bodyOwnerId, float now)
    {
        if (actor.PrimaryRole != RoleId.Cleaner)
            return Reject(actor, AbilityId.Clean, now, "クリーナー専用能力です。");
        if (!CanUse(actor, AbilityId.Clean, now) || !TryGetBodyTarget(actor, bodyOwnerId, out var body))
            return Reject(actor, AbilityId.Clean, now, "清掃できる死体に近づいてください。");

        actor.EffectTargets[AbilityId.Clean] = body.OwnerId;
        actor.EffectExpiresAt[AbilityId.Clean] = now + _options.CleanerDuration;
        actor.ImmobilizedUntil = now + _options.CleanerDuration;
        SetCooldown(actor, AbilityId.Clean, now, _options.SpecialAbilityCooldown);
        return Accept(actor, AbilityId.Clean, now);
    }

    private bool TryDevour(PlayerState actor, byte? bodyOwnerId, float now)
    {
        if (actor.PrimaryRole != RoleId.Gluttony)
            return Reject(actor, AbilityId.Devour, now, "グラトニー専用能力です。");
        if (!CanUse(actor, AbilityId.Devour, now) || !TryGetBodyTarget(actor, bodyOwnerId, out var body))
            return Reject(actor, AbilityId.Devour, now, "捕食できる死体に近づいてください。");

        _bodies.Remove(body.OwnerId);
        SetCooldown(actor, AbilityId.Devour, now, _options.SpecialAbilityCooldown);
        _gateway.Emit(new GameEvent(GameEventKind.BodyDevoured, now, actor.PlayerId, body.OwnerId));
        return Accept(actor, AbilityId.Devour, now);
    }

    private bool TryCollectDna(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Morphing)
            return Reject(actor, AbilityId.CollectDna, now, "モーフィング専用能力です。");
        if (!CanUse(actor, AbilityId.CollectDna, now) || !TryGetLivingTarget(actor, targetId, now, out var target))
            return Reject(actor, AbilityId.CollectDna, now, "DNAを採取する対象に近づいてください。");

        actor.EffectTargets[AbilityId.CollectDna] = target.PlayerId;
        SetCooldown(actor, AbilityId.CollectDna, now, _options.SpecialAbilityCooldown);
        _gateway.Emit(new GameEvent(GameEventKind.DnaCollected, now, actor.PlayerId, target.PlayerId));
        return Accept(actor, AbilityId.CollectDna, now);
    }

    private bool TryMorph(PlayerState actor, float now)
    {
        if (actor.PrimaryRole != RoleId.Morphing || !actor.EffectTargets.TryGetValue(AbilityId.CollectDna, out var targetId))
            return Reject(actor, AbilityId.Morph, now, "先にDNAを採取してください。");
        if (!CanUse(actor, AbilityId.Morph, now))
            return Reject(actor, AbilityId.Morph, now, "変身のクールダウン中です。");

        actor.EffectTargets[AbilityId.Morph] = targetId;
        actor.EffectExpiresAt[AbilityId.Morph] = now + _options.MorphDuration;
        SetCooldown(actor, AbilityId.Morph, now, _options.SpecialAbilityCooldown);
        _gateway.Emit(new GameEvent(GameEventKind.MorphStarted, now, actor.PlayerId, targetId, _options.MorphDuration.ToString("0.0")));
        return Accept(actor, AbilityId.Morph, now);
    }

    private bool TryPlantBomb(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Bomber)
            return Reject(actor, AbilityId.PlantBomb, now, "ボマー専用能力です。");
        if (!CanUse(actor, AbilityId.PlantBomb, now) || !TryGetLivingTarget(actor, targetId, now, out var target))
            return Reject(actor, AbilityId.PlantBomb, now, "爆弾を設置する対象に近づいてください。");

        actor.EffectTargets[AbilityId.PlantBomb] = target.PlayerId;
        actor.EffectExpiresAt[AbilityId.PlantBomb] = now + _options.BombDelay;
        SetCooldown(actor, AbilityId.PlantBomb, now, _options.SpecialAbilityCooldown);
        _gateway.Emit(new GameEvent(GameEventKind.BombPlanted, now, actor.PlayerId, target.PlayerId, _options.BombDelay.ToString("0.0")));
        return Accept(actor, AbilityId.PlantBomb, now);
    }

    private bool TrySetTrap(PlayerState actor, Position? requestedPosition, float now)
    {
        if (actor.PrimaryRole != RoleId.Trapper)
            return Reject(actor, AbilityId.SetTrap, now, "トラッパー専用能力です。");
        if (!CanUse(actor, AbilityId.SetTrap, now))
            return Reject(actor, AbilityId.SetTrap, now, "罠のクールダウン中です。");

        actor.EffectExpiresAt[AbilityId.SetTrap] = now + _options.TrapDuration;
        SetCooldown(actor, AbilityId.SetTrap, now, _options.SpecialAbilityCooldown);
        _gateway.Emit(new GameEvent(GameEventKind.TrapPlaced, now, actor.PlayerId, Position: requestedPosition ?? actor.Position));
        return Accept(actor, AbilityId.SetTrap, now);
    }

    private bool TryTimedEffect(PlayerState actor, RoleId role, AbilityId ability, float now, float duration, string detail, GameEventKind eventKind)
    {
        if (actor.PrimaryRole != role)
            return Reject(actor, ability, now, $"{RoleCatalog.Get(role).DisplayName}専用能力です。");
        if (!CanUse(actor, ability, now))
            return Reject(actor, ability, now, $"{detail}のクールダウン中です。");

        actor.EffectExpiresAt[ability] = now + duration;
        SetCooldown(actor, ability, now, _options.SpecialAbilityCooldown);
        _gateway.Emit(new GameEvent(eventKind, now, actor.PlayerId, Detail: duration.ToString("0.0")));
        return Accept(actor, ability, now);
    }

    private bool TrySilence(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Silencer)
            return Reject(actor, AbilityId.Silence, now, "サイレンサー専用能力です。");
        if (!CanUse(actor, AbilityId.Silence, now) || !TryGetLivingTarget(actor, targetId, now, out var target))
            return Reject(actor, AbilityId.Silence, now, "口封じ対象に近づいてください。");
        if (actor.EffectTargets.TryGetValue(AbilityId.Silence, out var previous) && previous == target.PlayerId)
            return Reject(actor, AbilityId.Silence, now, "同じ対象を連続して口封じできません。");

        actor.EffectTargets[AbilityId.Silence] = target.PlayerId;
        target.EffectExpiresAt[AbilityId.Silence] = now + _options.SilenceDuration;
        SetCooldown(actor, AbilityId.Silence, now, _options.SpecialAbilityCooldown);
        _gateway.Emit(new GameEvent(GameEventKind.SilenceApplied, now, actor.PlayerId, target.PlayerId));
        return Accept(actor, AbilityId.Silence, now);
    }

    private bool TryAnimateBody(PlayerState actor, byte? bodyOwnerId, Position? position, float now)
    {
        if (actor.PrimaryRole != RoleId.Necromancer)
            return Reject(actor, AbilityId.AnimateBody, now, "ネクロマンサー専用能力です。");
        if (!CanUse(actor, AbilityId.AnimateBody, now) || !TryGetBodyTarget(actor, bodyOwnerId, out var body))
            return Reject(actor, AbilityId.AnimateBody, now, "操縦できる死体に近づいてください。");

        var destination = position ?? actor.Position;
        _bodies[body.OwnerId] = body with { Position = destination };
        SetCooldown(actor, AbilityId.AnimateBody, now, _options.SpecialAbilityCooldown);
        _gateway.Emit(new GameEvent(GameEventKind.BodyAnimated, now, actor.PlayerId, body.OwnerId, Position: destination));
        return Accept(actor, AbilityId.AnimateBody, now);
    }

    private bool TryLinkCurse(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Witch)
            return Reject(actor, AbilityId.LinkCurse, now, "ウィッチ専用能力です。");
        if (!TryGetLivingTarget(actor, targetId, now, out var target))
            return Reject(actor, AbilityId.LinkCurse, now, "リンク対象に近づいてください。");

        if (actor.EffectTargets.TryGetValue(AbilityId.LinkCurse, out var firstTargetId))
        {
            if (firstTargetId == target.PlayerId)
                return Reject(actor, AbilityId.LinkCurse, now, "別のプレイヤーを選んでください。");
            actor.EffectTargets.Remove(AbilityId.LinkCurse);
            actor.SecondaryEffectTargetId = null;
            _witchLinks[firstTargetId] = target.PlayerId;
            _witchLinks[target.PlayerId] = firstTargetId;
            SetCooldown(actor, AbilityId.LinkCurse, now, _options.SpecialAbilityCooldown);
            _gateway.Emit(new GameEvent(GameEventKind.WitchLinked, now, actor.PlayerId, target.PlayerId, firstTargetId.ToString()));
            return Accept(actor, AbilityId.LinkCurse, now);
        }

        actor.EffectTargets[AbilityId.LinkCurse] = target.PlayerId;
        actor.SecondaryEffectTargetId = target.PlayerId;
        return Accept(actor, AbilityId.LinkCurse, now);
    }

    private bool TryCheckBounty(PlayerState actor, float now)
    {
        if (actor.PrimaryRole != RoleId.BountyHunter)
            return Reject(actor, AbilityId.CheckBounty, now, "バウンティハンター専用能力です。");
        var target = _players.Values.FirstOrDefault(player => player.IsAlive && player.PlayerId != actor.PlayerId && RoleCatalog.GetFaction(player.PrimaryRole) != Faction.Impostor);
        if (target == null)
            return Reject(actor, AbilityId.CheckBounty, now, "有効なターゲットがいません。");
        actor.EffectTargets[AbilityId.CheckBounty] = target.PlayerId;
        _gateway.Emit(new GameEvent(GameEventKind.BountyAssigned, now, actor.PlayerId, target.PlayerId));
        return Accept(actor, AbilityId.CheckBounty, now);
    }

    private bool TryGetBodyTarget(PlayerState actor, byte? bodyOwnerId, out BodyState body)
    {
        body = default;
        if (bodyOwnerId is not byte ownerId || !_bodies.TryGetValue(ownerId, out body) || body.IsCarried)
            return false;
        return actor.Position.DistanceTo(body.Position) <= _options.KillDistance;
    }

    private bool TryDirectKill(PlayerState actor, byte? targetId, float now, bool erased)
    {
        // SuperNewRolesのMafia.IsKillFlagと同じく、生存中の他インポスターがいる間はマフィアはキルできない。
        // 他の生存インポスターが全員マフィアになった後だけ、通常キルを解放する。
        if (actor.PrimaryRole == RoleId.Mafia && _players.Values.Any(player =>
                player.IsAlive &&
                player.PlayerId != actor.PlayerId &&
                RoleCatalog.GetFaction(player.PrimaryRole) == Faction.Impostor &&
                player.PrimaryRole != RoleId.Mafia))
            return Reject(actor, AbilityId.Kill, now, "他のインポスターが生存している間、マフィアはキルできません。");
        if (!RoleCatalog.Get(actor.PrimaryRole).CanDirectKill)
            return Reject(actor, AbilityId.Kill, now, "この役職はキル能力を持ちません。");
        if (!CanUse(actor, AbilityId.Kill, now))
            return Reject(actor, AbilityId.Kill, now, "キルのクールダウン中です。");
        if (!TryGetLivingTarget(actor, targetId, now, out var target))
            return false;
        if (AreJackalAllies(actor, target))
            return Reject(actor, AbilityId.Kill, now, "ジャッカル陣営の味方はキルできません。");

        // SNR SchrodingersCatAbility.TryMurderと同じく、未同調の猫を最初にキルしようとした時は
        // キルを無効化して攻撃者の陣営へ自動同調させる。手動能力や任意の陣営選択は行わない。
        if (TryConvertSchrodingerCatOnAttack(actor, target, now))
        {
            SetCooldown(actor, AbilityId.Kill, now, _options.StandardKillCooldown);
            return Accept(actor, AbilityId.Kill, now);
        }

        if (actor.PrimaryRole == RoleId.Sheriff)
        {
            if (actor.SheriffKillsRemaining <= 0)
                return Reject(actor, AbilityId.Kill, now, "シェリフのキル回数を使い切っています。");

            var targetFaction = RoleCatalog.GetFaction(target.PrimaryRole);
            if (targetFaction == Faction.Crew || (targetFaction == Faction.Neutral && !_options.SheriffCanKillNeutrals))
            {
                actor.SheriffKillsRemaining--;
                SetCooldown(actor, AbilityId.Kill, now, _options.StandardKillCooldown);
                KillPlayer(actor.PlayerId, actor.PlayerId, now, "シェリフ誤射", silent: false, erased: false);
                return Accept(actor, AbilityId.Kill, now);
            }
            actor.SheriffKillsRemaining--;
        }
        else if (actor.PrimaryRole == RoleId.Jackal || actor.PrimaryRole == RoleId.Vampire)
        {
            // 第三陣営キラーは全陣営を標的にできる。
        }
        else if (RoleCatalog.GetFaction(actor.PrimaryRole) == Faction.Impostor && RoleCatalog.GetFaction(target.PrimaryRole) == Faction.Impostor)
        {
            return Reject(actor, AbilityId.Kill, now, "インポスター同士はキルできません。");
        }
        else if (RoleCatalog.GetFaction(actor.PrimaryRole) == Faction.Impostor && target.PrimaryRole is RoleId.Jackal or RoleId.Vampire)
        {
            // 敵対する第三陣営キラーはキル可能。
        }

        var cooldown = actor.PrimaryRole switch
        {
            RoleId.Ninja => _options.NinjaKillCooldown,
            RoleId.Jackal or RoleId.Sidekick => _options.JackalKillCooldown,
            RoleId.Vampire => _options.VampireCooldown,
            _ => _options.StandardKillCooldown,
        };
        SetCooldown(actor, AbilityId.Kill, now, cooldown);
        KillPlayer(target.PlayerId, actor.PlayerId, now, "直接キル", actor.PrimaryRole == RoleId.Ninja, erased);
        Accept(actor, AbilityId.Kill, now);
        EvaluateVictory(now);
        return true;
    }

    /// <summary>
    /// SNR SchrodingersCatAbilityの被キル時同調を、ホスト確定のRoleId変更へ適合する。
    /// trueの場合はキルを取消し、猫は生存したまま攻撃者側へ移る。
    /// </summary>
    private bool TryConvertSchrodingerCatOnAttack(PlayerState attacker, PlayerState target, float now)
    {
        if (target.PrimaryRole != RoleId.SchrodingerCat)
            return false;

        RoleId? alignedRole = RoleCatalog.GetFaction(attacker.PrimaryRole) switch
        {
            Faction.Impostor => RoleId.Impostor,
            Faction.Crew => RoleId.Crewmate,
            _ when IsJackalTeamRole(attacker.PrimaryRole) => RoleId.Sidekick,
            _ when _options.SchrodingerCatCrewOnKillByNonSpecific => RoleId.Crewmate,
            _ => null,
        };
        if (alignedRole is not RoleId newRole)
            return false;

        target.PrimaryRole = newRole;
        target.AbilityCooldowns.Clear();
        target.EffectExpiresAt.Clear();
        target.EffectTargets.Clear();
        target.EffectCounts.Clear();
        target.SecondaryEffectTargetId = null;
        if (_options.SchrodingerCatHasKillAbility && newRole is RoleId.Impostor or RoleId.Sidekick)
            target.AbilityCooldowns[AbilityId.Kill] = now + _options.SchrodingerCatKillCooldown;

        _gateway.Emit(new GameEvent(
            GameEventKind.RoleChanged,
            now,
            attacker.PlayerId,
            target.PlayerId,
            $"SchrodingerCatAligned:{newRole}"));
        return true;
    }

    private bool TryEraseKill(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Eraser)
            return Reject(actor, AbilityId.EraseKill, now, "イレイザー専用能力です。");
        return TryDirectKill(actor, targetId, now, erased: true);
    }

    private bool TryOpenVitals(PlayerState actor, float now)
    {
        if (actor.PrimaryRole != RoleId.MadScientist)
            return Reject(actor, AbilityId.OpenVitals, now, "マッドサイエンティスト専用能力です。");
        if (!CanUse(actor, AbilityId.OpenVitals, now))
            return Reject(actor, AbilityId.OpenVitals, now, "バイタル能力のクールダウン中です。");

        SetCooldown(actor, AbilityId.OpenVitals, now, _options.MadScientistCooldown);
        _activeVitalsUntil[actor.PlayerId] = now + _options.MadScientistDuration;
        _gateway.Emit(new GameEvent(GameEventKind.VitalsOpened, now, actor.PlayerId, Detail: _options.MadScientistDuration.ToString("0.0")));
        return Accept(actor, AbilityId.OpenVitals, now);
    }

    private bool TryTrack(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Tracker)
            return Reject(actor, AbilityId.Track, now, "トラッカー専用能力です。");
        if (!CanUse(actor, AbilityId.Track, now))
            return Reject(actor, AbilityId.Track, now, "トラッカー能力のクールダウン中です。");
        if (!TryGetLivingTarget(actor, targetId, now, out var target, needsRange: false))
            return false;

        SetCooldown(actor, AbilityId.Track, now, _options.TrackerCooldown);
        _trackTargets[actor.PlayerId] = target.PlayerId;
        _activeTrackUntil[actor.PlayerId] = now + _options.TrackerDuration;
        _gateway.Emit(new GameEvent(GameEventKind.TrackingStarted, now, actor.PlayerId, target.PlayerId, _options.TrackerDuration.ToString("0.0")));
        return Accept(actor, AbilityId.Track, now);
    }

    private bool TryTimeWarp(PlayerState actor, float now)
    {
        if (actor.PrimaryRole != RoleId.TimeTraveler)
            return Reject(actor, AbilityId.TimeWarp, now, "タイムトラベラー専用能力です。");
        if (!CanUse(actor, AbilityId.TimeWarp, now))
            return Reject(actor, AbilityId.TimeWarp, now, "タイムワープのクールダウン中です。");

        var desiredTime = now - _options.TimeTravelerSeconds;
        var candidates = actor.PositionHistory.Where(x => x.Time <= desiredTime).ToArray();
        if (candidates.Length == 0)
            return Reject(actor, AbilityId.TimeWarp, now, "安全な巻き戻し先がありません。");
        var sample = candidates[^1];
        if (!_gateway.IsWalkable(sample.Position))
            return Reject(actor, AbilityId.TimeWarp, now, "安全な巻き戻し先がありません。");

        actor.Position = sample.Position;
        SetCooldown(actor, AbilityId.TimeWarp, now, _options.TimeTravelerCooldown);
        _gateway.Emit(new GameEvent(GameEventKind.TimeWarped, now, actor.PlayerId, Position: sample.Position));
        return Accept(actor, AbilityId.TimeWarp, now);
    }

    private bool TrySpeakWithDead(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Seer)
            return Reject(actor, AbilityId.SpeakWithDead, now, "シーア専用能力です。");
        if (!CanUse(actor, AbilityId.SpeakWithDead, now))
            return Reject(actor, AbilityId.SpeakWithDead, now, "霊魂会話のクールダウン中です。");
        if (targetId is not byte targetPlayerId || !_players.TryGetValue(targetPlayerId, out var target) || target.IsAlive)
            return Reject(actor, AbilityId.SpeakWithDead, now, "死亡しているプレイヤーを選んでください。");

        SetCooldown(actor, AbilityId.SpeakWithDead, now, _options.SeerCooldown);
        _gateway.Emit(new GameEvent(GameEventKind.AbilityAccepted, now, actor.PlayerId, targetPlayerId, $"霊魂会話 {_options.SeerDuration:0.0}s"));
        return true;
    }

    private bool TryGrantBarrier(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.BarrierNic)
            return Reject(actor, AbilityId.GrantBarrier, now, "バリアニック専用能力です。");
        if (!CanUse(actor, AbilityId.GrantBarrier, now))
            return Reject(actor, AbilityId.GrantBarrier, now, "バリア付与のクールダウン中です。");
        if (!TryGetLivingTarget(actor, targetId, now, out var target, needsRange: false))
            return false;

        target.HasBarrier = true;
        SetCooldown(actor, AbilityId.GrantBarrier, now, _options.BarrierCooldown);
        _gateway.Emit(new GameEvent(GameEventKind.BarrierGranted, now, actor.PlayerId, target.PlayerId));
        return Accept(actor, AbilityId.GrantBarrier, now);
    }

    private bool TryCurse(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Warlock)
            return Reject(actor, AbilityId.Curse, now, "ウォーロック専用能力です。");
        if (!CanUse(actor, AbilityId.Curse, now))
            return Reject(actor, AbilityId.Curse, now, "呪いのクールダウン中です。");
        if (!TryGetLivingTarget(actor, targetId, now, out var target))
            return false;

        target.IsCursed = true;
        target.CurseExpiresAt = now + _options.WarlockDuration;
        SetCooldown(actor, AbilityId.Curse, now, _options.WarlockCooldown);
        _gateway.Emit(new GameEvent(GameEventKind.CurseApplied, now, actor.PlayerId, target.PlayerId, _options.WarlockDuration.ToString("0.0")));
        return Accept(actor, AbilityId.Curse, now);
    }

    private bool TrySabotage(PlayerState actor, float now)
    {
        if (actor.PrimaryRole != RoleId.Mafia)
            return Reject(actor, AbilityId.Sabotage, now, "マフィア専用能力です。");
        // SNRのマフィアは専用の連続サボタージュ能力を持たず、キル解放条件で個性を表す。
        return Reject(actor, AbilityId.Sabotage, now, "SNR移植版マフィアは専用サボタージュを使用しません。");
    }

    private bool TryPuppet(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Puppeteer)
            return Reject(actor, AbilityId.Puppet, now, "パペッティア専用能力です。");
        if (!CanUse(actor, AbilityId.Puppet, now))
            return Reject(actor, AbilityId.Puppet, now, "操作支配のクールダウン中です。");
        if (!TryGetLivingTarget(actor, targetId, now, out var target, needsRange: false))
            return false;

        target.PuppetControllerId = actor.PlayerId;
        target.PuppetExpiresAt = now + _options.PuppeteerDuration;
        SetCooldown(actor, AbilityId.Puppet, now, _options.PuppeteerCooldown);
        _gateway.Emit(new GameEvent(GameEventKind.AbilityAccepted, now, actor.PlayerId, target.PlayerId, $"支配 {_options.PuppeteerDuration:0.0}s"));
        return true;
    }

    /// <summary>
    /// 牽引中の死体を運搬者の現在位置へ安全に配置する。会議開始・死亡・ベント進入時の共通後始末に使う。
    /// </summary>
    public bool ForceDropCarriedBody(byte carrierId, Position position, float now, string detail)
    {
        if (!_players.TryGetValue(carrierId, out var carrier) || carrier.CarriedBodyOwnerId is not byte ownerId || !_bodies.TryGetValue(ownerId, out var body))
            return false;
        _bodies[ownerId] = body with { Position = position, IsCarried = false };
        carrier.CarriedBodyOwnerId = null;
        _gateway.Emit(new GameEvent(GameEventKind.BodyDropped, now, carrierId, ownerId, detail, position));
        return true;
    }

    private void DropCarriedBodyAtCarrier(PlayerState carrier, float now, string detail)
        => ForceDropCarriedBody(carrier.PlayerId, carrier.Position, now, detail);

    public bool IsUndertakerCarrying(byte playerId)
        => _players.TryGetValue(playerId, out var player) && player.PrimaryRole == RoleId.Undertaker && player.CarriedBodyOwnerId is not null;

    private bool TryCarryBody(PlayerState actor, byte? bodyOwnerId, float now)
    {
        if (actor.PrimaryRole != RoleId.Undertaker)
            return Reject(actor, AbilityId.CarryBody, now, "アンダーテイカー専用能力です。");
        if (actor.CarriedBodyOwnerId is not null)
            return Reject(actor, AbilityId.CarryBody, now, "すでに死体を運搬中です。");
        if (bodyOwnerId is not byte ownerId || !_bodies.TryGetValue(ownerId, out var body) || body.IsCarried)
            return Reject(actor, AbilityId.CarryBody, now, "運搬できる死体がありません。");
        if (actor.Position.DistanceTo(body.Position) > _options.KillDistance)
            return Reject(actor, AbilityId.CarryBody, now, "死体に近づいてください。");

        actor.CarriedBodyOwnerId = ownerId;
        _bodies[ownerId] = body with { IsCarried = true };
        _gateway.Emit(new GameEvent(GameEventKind.BodyCarried, now, actor.PlayerId, ownerId));
        return Accept(actor, AbilityId.CarryBody, now);
    }

    private bool TryDropBody(PlayerState actor, Position? requestedPosition, float now)
    {
        if (actor.PrimaryRole != RoleId.Undertaker || actor.CarriedBodyOwnerId is not byte ownerId)
            return Reject(actor, AbilityId.DropBody, now, "運搬中の死体がありません。");

        var position = requestedPosition ?? actor.Position;
        if (!_gateway.IsWalkable(position) || !_bodies.ContainsKey(ownerId))
            return Reject(actor, AbilityId.DropBody, now, "死体を置けない位置です。");

        return ForceDropCarriedBody(actor.PlayerId, position, now, "手動配置")
            ? Accept(actor, AbilityId.DropBody, now)
            : Reject(actor, AbilityId.DropBody, now, "運搬中の死体を配置できませんでした。");
    }

    private bool TryBite(PlayerState actor, byte? targetId, float now)
    {
        if (actor.PrimaryRole != RoleId.Vampire)
            return Reject(actor, AbilityId.Bite, now, "ヴァンパイア専用能力です。");
        if (!CanUse(actor, AbilityId.Bite, now))
            return Reject(actor, AbilityId.Bite, now, "噛みつきのクールダウン中です。");
        if (!TryGetLivingTarget(actor, targetId, now, out var target))
            return false;

        target.BiteExpiresAt = now + _options.VampireDelay;
        SetCooldown(actor, AbilityId.Bite, now, _options.VampireCooldown);
        _gateway.Emit(new GameEvent(GameEventKind.BiteApplied, now, actor.PlayerId, target.PlayerId, _options.VampireDelay.ToString("0.0")));
        return Accept(actor, AbilityId.Bite, now);
    }

    private void ResolveAdditionalEffects(float now)
    {
        foreach (var cleaner in _players.Values.Where(player => player.EffectExpiresAt.TryGetValue(AbilityId.Clean, out var cleanEndsAt) && cleanEndsAt <= now).ToArray())
        {
            cleaner.EffectExpiresAt.Remove(AbilityId.Clean);
            cleaner.ImmobilizedUntil = 0;
            if (cleaner.EffectTargets.Remove(AbilityId.Clean, out var bodyOwnerId) && _bodies.Remove(bodyOwnerId))
                _gateway.Emit(new GameEvent(GameEventKind.BodyCleaned, now, cleaner.PlayerId, bodyOwnerId));
        }

        foreach (var bomber in _players.Values.Where(player => player.EffectExpiresAt.TryGetValue(AbilityId.PlantBomb, out var bombEndsAt) && bombEndsAt <= now).ToArray())
        {
            bomber.EffectExpiresAt.Remove(AbilityId.PlantBomb);
            if (!bomber.EffectTargets.Remove(AbilityId.PlantBomb, out var targetId) || !_players.TryGetValue(targetId, out var target) || !target.IsAlive)
                continue;
            var victims = _players.Values.Where(player => player.IsAlive && player.PlayerId != bomber.PlayerId && player.Position.DistanceTo(target.Position) <= _options.BombRadius).Select(player => player.PlayerId).ToArray();
            foreach (var victimId in victims)
                KillPlayer(victimId, bomber.PlayerId, now, "時限爆弾", silent: false, erased: false);
            _gateway.Emit(new GameEvent(GameEventKind.BombExploded, now, bomber.PlayerId, targetId));
        }

        foreach (var player in _players.Values)
        {
            foreach (var ability in player.EffectExpiresAt.Where(effect => effect.Value <= now).Select(effect => effect.Key).ToArray())
            {
                player.EffectExpiresAt.Remove(ability);
                if (ability == AbilityId.Morph)
                    player.ImmobilizedUntil = now + 1f;
            }
        }
    }

    private void ResolveExpiredBites(float now)
    {
        foreach (var target in _players.Values.Where(x => x.IsAlive && x.BiteExpiresAt > 0 && x.BiteExpiresAt <= now).ToArray())
        {
            target.BiteExpiresAt = 0;
            var vampire = _players.Values.FirstOrDefault(x => x.IsAlive && x.PrimaryRole == RoleId.Vampire);
            KillPlayer(target.PlayerId, vampire?.PlayerId, now, "噛みつき", silent: false, erased: false);
        }
    }

    private void ResolveCurses(float now)
    {
        foreach (var cursed in _players.Values.Where(x => x.IsAlive && x.IsCursed).ToArray())
        {
            if (cursed.CurseExpiresAt <= now)
            {
                cursed.IsCursed = false;
                cursed.CurseExpiresAt = 0;
                continue;
            }

            var victim = _players.Values.FirstOrDefault(x => x.IsAlive && x.PlayerId != cursed.PlayerId && x.Position.DistanceTo(cursed.Position) <= _options.CurseDistance);
            if (victim is null)
                continue;

            cursed.IsCursed = false;
            cursed.CurseExpiresAt = 0;
            KillPlayer(victim.PlayerId, cursed.PlayerId, now, "呪いによるすれ違いキル", silent: false, erased: false);
            _gateway.Emit(new GameEvent(GameEventKind.CurseTriggered, now, cursed.PlayerId, victim.PlayerId));
        }
    }

    private void ExpireTransientEffects(float now)
    {
        foreach (var player in _players.Values)
        {
            if (player.PuppetExpiresAt > 0 && player.PuppetExpiresAt <= now)
            {
                player.PuppetExpiresAt = 0;
                player.PuppetControllerId = null;
            }
        }

        foreach (var trackerId in _activeTrackUntil.Where(x => x.Value <= now).Select(x => x.Key).ToArray())
        {
            _activeTrackUntil.Remove(trackerId);
            _trackTargets.Remove(trackerId);
        }
        foreach (var scientistId in _activeVitalsUntil.Where(x => x.Value <= now).Select(x => x.Key).ToArray())
            _activeVitalsUntil.Remove(scientistId);
    }

    private void KillPlayer(byte targetId, byte? killerId, float now, string detail, bool silent, bool erased)
    {
        var target = GetPlayer(targetId);
        if (!target.IsAlive)
            return;

        if (target.HasBarrier)
        {
            target.HasBarrier = false;
            _gateway.Emit(new GameEvent(GameEventKind.BarrierConsumed, now, killerId, targetId));
            return;
        }

        // TownOfUsのUndertakerは死亡時に牽引を解除する。運搬死体を消失状態のまま残さない。
        if (target.CarriedBodyOwnerId is not null)
            DropCarriedBodyAtCarrier(target, now, "運搬者死亡");
        target.IsAlive = false;
        target.BiteExpiresAt = 0;
        target.PuppetControllerId = null;
        target.PuppetExpiresAt = 0;
        target.RoleErasedOnDeath = erased;
        _bodies[targetId] = new BodyState(targetId, target.Position, now, false, erased);
        _gateway.Emit(new GameEvent(GameEventKind.PlayerDied, now, killerId, targetId, detail, target.Position, silent));
        PromoteSidekickAfterJackalDeath(target, now);
        if (erased)
            _gateway.Emit(new GameEvent(GameEventKind.RoleErased, now, killerId, targetId));

        if (_witchLinks.Remove(targetId, out var linkedId) && _players.TryGetValue(linkedId, out var linked) && linked.IsAlive)
        {
            _witchLinks.Remove(linkedId);
            KillPlayer(linkedId, killerId, now, "呪詛リンク", silent: false, erased: false);
        }

        if (_lovers.TryGetValue(targetId, out var partnerId) && _players.TryGetValue(partnerId, out var partner) && partner.IsAlive)
        {
            _gateway.Emit(new GameEvent(GameEventKind.LoversTriggered, now, targetId, partnerId));
            KillPlayer(partnerId, null, now, "ラバーズ後追い", silent: false, erased: false);
        }
    }

    private VictoryResult EvaluateVictory(float now)
    {
        var alive = _players.Values.Where(x => x.IsAlive).ToArray();
        if (alive.Length == 0)
            return VictoryResult.None;

        var livingLovers = alive.Where(x => x.HasModifier(ModifierId.Lovers)).Select(x => x.PlayerId).ToArray();
        if (alive.Length == 2 && livingLovers.Length == 2 && _lovers.TryGetValue(livingLovers[0], out var partner) && partner == livingLovers[1])
        {
            var result = new VictoryResult(VictoryKind.Lovers, livingLovers);
            EmitVictory(result, now);
            return result;
        }

        // SuperNewRolesのCheckEndGame.IsKillerWinに合わせ、人数優勢だけでなく他のキラー陣営が残っていないことも確認する。
        // これにより、ヴァンパイアなど別第三陣営キラーを残したままジャッカルが誤勝利することを防ぐ。
        var jackalTeam = alive.Where(x => IsJackalTeamRole(x.PrimaryRole)).ToArray();
        var livingImpostors = alive.Where(x => RoleCatalog.GetFaction(x.PrimaryRole) == Faction.Impostor).ToArray();
        var totalKillerCount = alive.Count(x => RoleCatalog.GetFaction(x.PrimaryRole) == Faction.Impostor || RoleCatalog.IsKillerNeutral(x.PrimaryRole));
        var jackalCanWin = jackalTeam.Length > 0
            && jackalTeam.Length >= alive.Length - jackalTeam.Length
            && totalKillerCount <= jackalTeam.Length;
        if (jackalCanWin)
        {
            var result = new VictoryResult(VictoryKind.Jackal, jackalTeam.Select(x => x.PlayerId).ToArray());
            EmitVictory(result, now);
            return result;
        }

        var zombieTeam = alive.Where(x => x.PrimaryRole is RoleId.Zombie or RoleId.ChildZombie).ToArray();
        if (zombieTeam.Length > 0 && zombieTeam.Length == alive.Length)
        {
            var result = new VictoryResult(VictoryKind.Zombie, zombieTeam.Select(x => x.PlayerId).ToArray());
            EmitVictory(result, now);
            return result;
        }

        var vultures = alive.Where(x => x.PrimaryRole == RoleId.Vulture && x.EffectCounts.TryGetValue(AbilityId.CollectBody, out var collected) && collected >= _options.VultureRequiredBodies).ToArray();
        if (vultures.Length > 0)
        {
            var result = new VictoryResult(VictoryKind.Vulture, vultures.Select(x => x.PlayerId).ToArray());
            EmitVictory(result, now);
            return result;
        }

        var vampires = alive.Where(x => x.PrimaryRole == RoleId.Vampire).ToArray();
        if (vampires.Length == 1 && alive.Length == 1)
        {
            var result = new VictoryResult(VictoryKind.Vampire, new[] { vampires[0].PlayerId });
            EmitVictory(result, now);
            return result;
        }

        var impostors = livingImpostors;
        var nonImpostors = alive.Length - impostors.Length;
        if (impostors.Length > 0 && impostors.Length >= nonImpostors)
        {
            var winners = impostors.Select(x => x.PlayerId)
                .Concat(alive.Where(x => x.PrimaryRole is RoleId.Advocate or RoleId.Clown or RoleId.Fanatic).Select(x => x.PlayerId))
                .Distinct().ToArray();
            var result = new VictoryResult(VictoryKind.Impostors, winners);
            EmitVictory(result, now);
            return result;
        }

        var hostileNeutrals = alive.Any(x => RoleCatalog.IsKillerNeutral(x.PrimaryRole));
        if (impostors.Length == 0 && !hostileNeutrals)
        {
            var winners = alive.Where(x => RoleCatalog.GetFaction(x.PrimaryRole) == Faction.Crew).Select(x => x.PlayerId).ToArray();
            if (winners.Length > 0)
            {
                var result = new VictoryResult(VictoryKind.Crewmates, winners);
                EmitVictory(result, now);
                return result;
            }
        }

        return VictoryResult.None;
    }

    private void EmitVictory(VictoryResult result, float now)
    {
        if (result.Kind != VictoryKind.None)
            _gateway.Emit(new GameEvent(GameEventKind.Victory, now, Detail: result.Kind.ToString(), ParticipantIds: result.WinnerIds));
    }

    /// <summary>
    /// SuperNewRolesのJackalAbility/JSidekickAbilityが持つ昇格規則を、tempMODのホスト確定状態へ適合する。
    /// 親ジャッカルが死亡した時だけ、その親が作成した存命サイドキックをジャッカルへ昇格する。
    /// </summary>
    private void PromoteSidekickAfterJackalDeath(PlayerState deadPlayer, float now)
    {
        if (!_options.JackalSidekickPromotesOnJackalDeath || deadPlayer.PrimaryRole != RoleId.Jackal)
            return;

        foreach (var sidekick in _players.Values
                     .Where(player => player.IsAlive
                         && player.PrimaryRole == RoleId.Sidekick
                         && player.EffectTargets.TryGetValue(AbilityId.RecruitSidekick, out var ownerId)
                         && ownerId == deadPlayer.PlayerId)
                     .ToArray())
        {
            sidekick.PrimaryRole = RoleId.Jackal;
            sidekick.EffectTargets.Remove(AbilityId.RecruitSidekick);
            sidekick.AbilityCooldowns.Remove(AbilityId.RecruitSidekick);
            _gateway.Emit(new GameEvent(GameEventKind.RoleChanged, now, deadPlayer.PlayerId, sidekick.PlayerId, "SidekickPromotedToJackal"));
        }
    }

    private static bool IsJackalTeamRole(RoleId role)
        => role is RoleId.Jackal or RoleId.Sidekick;

    private static bool AreJackalAllies(PlayerState first, PlayerState second)
        => IsJackalTeamRole(first.PrimaryRole) && IsJackalTeamRole(second.PrimaryRole);

    private bool TryGetLivingTarget(PlayerState actor, byte? targetId, float now, out PlayerState target, bool needsRange = true)
    {
        target = null!;
        if (targetId is not byte id || !_players.TryGetValue(id, out var candidate) || !candidate.IsAlive || id == actor.PlayerId)
            return Reject(actor, AbilityId.Kill, now, "有効な対象を選んでください。");
        if (needsRange && actor.Position.DistanceTo(candidate.Position) > _options.KillDistance)
            return Reject(actor, AbilityId.Kill, now, "対象が遠すぎます。");

        target = candidate;
        return true;
    }

    private bool CanUse(PlayerState actor, AbilityId ability, float now)
        => !actor.AbilityCooldowns.TryGetValue(ability, out var nextAt) || now >= nextAt;

    private void SetCooldown(PlayerState actor, AbilityId ability, float now, float duration)
        => actor.AbilityCooldowns[ability] = now + duration;

    private bool Accept(PlayerState actor, AbilityId ability, float now)
    {
        _gateway.Emit(new GameEvent(GameEventKind.AbilityAccepted, now, actor.PlayerId, Detail: ability.ToString()));
        return true;
    }

    private bool Reject(AbilityRequest request, float now, string detail)
    {
        _gateway.Emit(new GameEvent(GameEventKind.AbilityRejected, now, request.SenderId, request.TargetId, detail));
        return false;
    }

    private bool Reject(PlayerState actor, AbilityId ability, float now, string detail)
    {
        _gateway.Emit(new GameEvent(GameEventKind.AbilityRejected, now, actor.PlayerId, Detail: $"{ability}: {detail}"));
        return false;
    }

    private PlayerState GetPlayer(byte playerId)
        => _players.TryGetValue(playerId, out var player)
            ? player
            : throw new KeyNotFoundException($"Unknown player {playerId}.");
}
