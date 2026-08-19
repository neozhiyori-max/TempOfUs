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
    private float _meetingStartedAt = -1;

    public RoleEngine(IRoleGameGateway gateway, RoleOptions? options = null)
    {
        _gateway = gateway;
        _options = options ?? new RoleOptions();
    }

    public IReadOnlyDictionary<byte, PlayerState> Players => _players;
    public IReadOnlyDictionary<byte, BodyState> Bodies => _bodies;
    public IReadOnlyList<Footprint> Footprints => _footprints;
    public bool IsMeetingActive { get; private set; }

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
            player.AbilityCooldowns.Clear();
            foreach (var cooldown in snapshot.AbilityCooldowns)
                player.AbilityCooldowns[cooldown.Key] = cooldown.Value;
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
        foreach (var player in _players.Values)
        {
            player.PuppetControllerId = null;
            player.PuppetExpiresAt = 0;
        }
    }

    /// <summary>投票で追放された役職の処理。ジェスターは最優先で単独勝利する。</summary>
    public VictoryResult EndMeeting(byte? exiledPlayerId, float now)
    {
        if (!IsMeetingActive)
            throw new InvalidOperationException("No meeting is active.");

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
                KillPlayer(playerId, null, now, "追放", silent: false, erased: false);

            if (exiled.PrimaryRole == RoleId.Jester)
            {
                var result = new VictoryResult(VictoryKind.Jester, new[] { playerId });
                EmitVictory(result, now);
                return result;
            }
        }

        return EvaluateVictory(now);
    }

    public int GetVoteWeight(byte playerId)
    {
        var player = GetPlayer(playerId);
        return player.IsAlive && player.PrimaryRole == RoleId.Mayor ? 2 : 1;
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
        if (IsMeetingActive)
            return Reject(request, now, "会議中は能力を使えません。");

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
            _ => Reject(request, now, "未対応の能力です。"),
        };
    }

    private bool TryDirectKill(PlayerState actor, byte? targetId, float now, bool erased)
    {
        if (actor.PrimaryRole == RoleId.Mafia)
            return Reject(actor, AbilityId.Kill, now, "マフィアは直接キルできません。");
        if (!RoleCatalog.Get(actor.PrimaryRole).CanDirectKill)
            return Reject(actor, AbilityId.Kill, now, "この役職はキル能力を持ちません。");
        if (!CanUse(actor, AbilityId.Kill, now))
            return Reject(actor, AbilityId.Kill, now, "キルのクールダウン中です。");
        if (!TryGetLivingTarget(actor, targetId, now, out var target))
            return false;

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
            RoleId.Jackal => _options.JackalKillCooldown,
            RoleId.Vampire => _options.VampireCooldown,
            _ => _options.StandardKillCooldown,
        };
        SetCooldown(actor, AbilityId.Kill, now, cooldown);
        KillPlayer(target.PlayerId, actor.PlayerId, now, "直接キル", actor.PrimaryRole == RoleId.Ninja, erased);
        Accept(actor, AbilityId.Kill, now);
        EvaluateVictory(now);
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
        _gateway.Emit(new GameEvent(GameEventKind.AbilityAccepted, now, actor.PlayerId, Detail: "ノーコストサボタージュ"));
        return true;
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
        if (!_gateway.IsWalkable(position) || !_bodies.TryGetValue(ownerId, out var body))
            return Reject(actor, AbilityId.DropBody, now, "死体を置けない位置です。");

        _bodies[ownerId] = body with { Position = position, IsCarried = false };
        actor.CarriedBodyOwnerId = null;
        _gateway.Emit(new GameEvent(GameEventKind.BodyDropped, now, actor.PlayerId, ownerId, Position: position));
        return Accept(actor, AbilityId.DropBody, now);
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

        target.IsAlive = false;
        target.IsCursed = false;
        target.BiteExpiresAt = 0;
        target.PuppetControllerId = null;
        target.PuppetExpiresAt = 0;
        target.RoleErasedOnDeath = erased;
        _bodies[targetId] = new BodyState(targetId, target.Position, now, false, erased);
        _gateway.Emit(new GameEvent(GameEventKind.PlayerDied, now, killerId, targetId, detail, target.Position, silent));
        if (erased)
            _gateway.Emit(new GameEvent(GameEventKind.RoleErased, now, killerId, targetId));

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

        var jackals = alive.Where(x => x.PrimaryRole == RoleId.Jackal).ToArray();
        if (jackals.Length == 1 && alive.Length == 1)
        {
            var result = new VictoryResult(VictoryKind.Jackal, new[] { jackals[0].PlayerId });
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

        var impostors = alive.Where(x => RoleCatalog.GetFaction(x.PrimaryRole) == Faction.Impostor).ToArray();
        var nonImpostors = alive.Length - impostors.Length;
        if (impostors.Length > 0 && impostors.Length >= nonImpostors)
        {
            var result = new VictoryResult(VictoryKind.Impostors, impostors.Select(x => x.PlayerId).ToArray());
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
