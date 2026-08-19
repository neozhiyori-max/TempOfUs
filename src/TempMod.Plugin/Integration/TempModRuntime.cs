using AmongUs.GameOptions;
using BepInEx.Logging;
using Hazel;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.InteropTypes;
using TempMod.Core;
using TMPro;
using UnityEngine;

namespace TempMod.Plugin.Integration;

internal enum TempModRpc : byte
{
    AssignRoles = 250,
    AbilityRequest = 251,
    SyncState = 252,
}

/// <summary>
/// Among Usのライフサイクルと、ゲーム非依存のRoleEngineを接続する層。
/// 役職割当はホストだけが生成し、専用RPCで全クライアントに同一状態を配布する。
/// </summary>
internal sealed class TempModRuntime : IRoleGameGateway
{
    private readonly ManualLogSource _log;
    private readonly TempModSettings _settings;
    private RoleEngine _engine;
    private bool _assignmentReceived;
    private bool _introRoleLogged;
    private readonly HashSet<byte> _nativeRolesApplied = new();
    private readonly List<ResultLine> _resultLines = new();
    private string? _victoryLabel;
    private bool _resultSentToChat;

    internal TempModRuntime(ManualLogSource log, TempModSettings settings)
    {
        _log = log;
        _settings = settings;
        _engine = new RoleEngine(this, _settings.CreateRoleOptions());
    }

    internal RoleEngine Engine => _engine;

    private sealed record ResultLine(string PlayerName, string ActionText, string RoleName);

    internal void OnGameStarted()
    {
        // OnGameStartは開始演出の後に呼ばれる版がある。抽選済みの状態をここで初期化すると
        // タスク表示・キル判定から役職が消えるため、未割当時だけ初期化する。
        if (_assignmentReceived)
        {
            _log.LogInfo("tempMOD: PlayerControl.OnGameStart を受信しましたが、役職割当済みのため状態を保持します。");
            return;
        }
        _engine = new RoleEngine(this, _settings.CreateRoleOptions());
        _introRoleLogged = false;
        _log.LogInfo("tempMOD: PlayerControl.OnGameStart を受信しました。開始演出で役職を確定します。");
    }

    internal void OnIntroStarted()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            return;
        if (_assignmentReceived)
            return;

        _introRoleLogged = false;
        _log.LogInfo("tempMOD: IntroCutscene.CoBegin でホスト役職抽選を開始します。");
        AssignAndBroadcastRoles();
    }

    internal void ApplyRoleIntro(IntroCutscene intro)
    {
        if (!_assignmentReceived || PlayerControl.LocalPlayer == null)
            return;
        if (!_engine.Players.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var state))
            return;

        var definition = RoleCatalog.Get(state.PrimaryRole);
        var color = definition.Faction switch
        {
            Faction.Crew => new Color(0.40f, 0.85f, 1.00f),
            Faction.Impostor => new Color(1.00f, 0.36f, 0.36f),
            _ => new Color(0.85f, 0.55f, 1.00f),
        };
        var factionName = definition.Faction switch
        {
            Faction.Crew => "クルー",
            Faction.Impostor => "インポスター",
            _ => "第三陣営",
        };
        intro.YouAreText.text = "あなたのロールは";
        intro.YouAreText.color = color;
        intro.RoleText.text = definition.DisplayName;
        intro.RoleText.color = color;
        intro.RoleBlurbText.text = GetRoleIntroDescription(state.PrimaryRole);
        intro.RoleBlurbText.color = color;
        intro.TeamTitle.text = factionName;
        intro.TeamTitle.color = color;
        intro.ImpostorText.text = factionName;
        intro.ImpostorText.color = color;
        if (intro.FrontMost != null)
            intro.FrontMost.color = color;
        if (intro.BackgroundBar != null)
            intro.BackgroundBar.material.color = color;
        if (intro.Foreground != null)
            intro.Foreground.material.color = color;

        // ShowRoleコルーチンは後段で標準の「クルー／インポスター」を再設定する。
        // その影響を受けない専用テキストを同じ位置に重ね、確定したカスタム役職を常に表示する。
        SetRoleIntroOverlay(intro.RoleText, "tempMOD_CustomRoleIntro", definition.DisplayName, color);
        SetRoleIntroOverlay(intro.RoleBlurbText, "tempMOD_CustomRoleBlurb", GetRoleIntroDescription(state.PrimaryRole), color);
        if (!_introRoleLogged)
        {
            _introRoleLogged = true;
            _log.LogInfo($"tempMOD: 開始演出へ{definition.DisplayName}を表示しました。");
        }
    }

    private static void SetRoleIntroOverlay(TextMeshPro source, string objectName, string value, Color color)
    {
        var overlayObject = GameObject.Find(objectName);
        if (overlayObject == null)
        {
            overlayObject = UnityEngine.Object.Instantiate(source.gameObject, source.transform.parent);
            overlayObject.name = objectName;
            overlayObject.transform.localPosition = source.transform.localPosition;
            overlayObject.transform.localScale = source.transform.localScale;
        }

        var overlay = overlayObject.GetComponent(Il2CppType.Of<TextMeshPro>()).TryCast<TextMeshPro>();
        if (overlay == null)
            return;
        overlay.text = value;
        overlay.color = color;
        overlay.fontSize = source.fontSize;
    }

    internal void OverrideIntroText(TMP_Text target, ref string value)
    {
        if (!_assignmentReceived || PlayerControl.LocalPlayer == null || IntroCutscene.Instance == null)
            return;
        if (!_engine.Players.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var state))
            return;

        var intro = IntroCutscene.Instance;
        if (target == intro.YouAreText)
            value = "あなたのロールは";
        else if (target == intro.RoleText)
            value = RoleCatalog.Get(state.PrimaryRole).DisplayName;
        else if (target == intro.RoleBlurbText)
            value = GetRoleIntroDescription(state.PrimaryRole);
    }

    internal void OnMeetingStarted()
    {
        if (_assignmentReceived)
            _engine.StartMeeting(Time.time);
    }

    internal void OnMeetingClosed()
    {
        // 追放対象の確定処理はMeetingHudの投票確定パッチから呼び出す。
        // 現段階では会議終了後の一時効果解除だけをRoleEngineに委譲する。
    }

    internal void OnPlayerTick(PlayerControl player)
    {
        if (!_assignmentReceived || player == null || player.Data == null || player.Data.IsDead)
            return;

        if (!_engine.Players.ContainsKey(player.PlayerId))
            _engine.RegisterPlayer(player.PlayerId, $"Player {player.PlayerId}");

        EnsureNativeRole(player);

        // 開始演出のコルーチンが標準の役職名を書き戻すため、演出が開いている間は
        // ローカル役職表示を毎フレーム独自の確定役職で上書きする。
        if (PlayerControl.LocalPlayer != null && player.PlayerId == PlayerControl.LocalPlayer.PlayerId && IntroCutscene.Instance != null)
            ApplyRoleIntro(IntroCutscene.Instance);

        var position = player.GetTruePosition();
        _engine.UpdatePosition(player.PlayerId, new Position(position.x, position.y), Time.time);
        if (PlayerControl.LocalPlayer != null && player.PlayerId == PlayerControl.LocalPlayer.PlayerId && Input.GetKeyDown(KeyCode.F))
            TryUsePrimaryAbility(player);
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            _engine.Tick(Time.time);
    }

    private void EnsureNativeRole(PlayerControl player)
    {
        if (_nativeRolesApplied.Contains(player.PlayerId) || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || RoleManager.Instance == null)
            return;
        if (!_engine.Players.TryGetValue(player.PlayerId, out var state))
            return;

        // カスタム役職がゲーム本体のクルー状態のままだと、キル・ベントHUDが生成されない。
        // インポスター陣営とキル可能な第三陣営を標準インポスター基盤へ同期し、
        // 実際の固有能力・キル判定はRoleEngineが引き続き制御する。
        var definition = RoleCatalog.Get(state.PrimaryRole);
        var nativeRole = definition.Faction == Faction.Impostor || definition.IsKillerNeutral
            ? RoleTypes.Impostor
            : RoleTypes.Crewmate;
        RoleManager.Instance.SetRole(player, nativeRole);
        _nativeRolesApplied.Add(player.PlayerId);
        _log.LogInfo($"tempMOD: {definition.DisplayName} をゲーム本体の{nativeRole}基盤へ同期しました。");
    }

    internal bool TryUsePrimaryAbility(PlayerControl actor)
    {
        if (!_assignmentReceived || actor == null || !_engine.Players.TryGetValue(actor.PlayerId, out var state) || !state.IsAlive)
            return false;

        if (state.PrimaryRole == RoleId.Doctor)
            return TryShowDoctorDeathEstimate(actor);

        var ability = GetPrimaryAbility(state);
        if (ability == 0)
            return false;

        byte? targetId = ability switch
        {
            AbilityId.Kill or AbilityId.Bite or AbilityId.EraseKill or AbilityId.Track or AbilityId.GrantBarrier or AbilityId.Curse or AbilityId.Puppet => FindNearestLivingPlayer(actor.PlayerId),
            AbilityId.CarryBody => FindNearestBody(actor.PlayerId),
            AbilityId.SpeakWithDead => FindAnyDeadPlayer(),
            _ => null,
        };

        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            return _engine.TryHandleAbility(new AbilityRequest(actor.PlayerId, ability, targetId, state.Position, Time.time), Time.time);

        SendAbilityRequest(actor, ability, targetId ?? byte.MaxValue);
        return true;
    }

    internal bool TryGetAbilityButtonState(out AbilityButtonState state)
    {
        state = default;
        var local = PlayerControl.LocalPlayer;
        if (!_assignmentReceived || local == null || !_engine.Players.TryGetValue(local.PlayerId, out var player) || !player.IsAlive || _engine.IsMeetingActive)
            return false;

        if (player.PrimaryRole == RoleId.Doctor)
        {
            var hasBody = FindNearestBody(player.PlayerId) is not null;
            state = new AbilityButtonState("検死", "死体の死亡推定時刻を確認", hasBody, 0f, 1, new Color(0.35f, 0.85f, 1f));
            return true;
        }

        var ability = GetPrimaryAbility(player);
        if (ability == 0)
            return false;

        var label = ability switch
        {
            AbilityId.CarryBody => "牽引",
            AbilityId.DropBody => "配置",
            AbilityId.OpenVitals => "バイタル",
            AbilityId.Track => "追跡",
            AbilityId.TimeWarp => "巻戻し",
            AbilityId.SpeakWithDead => "霊魂会話",
            AbilityId.GrantBarrier => "バリア付与",
            AbilityId.Curse => "呪い",
            AbilityId.Puppet => "操作",
            AbilityId.Bite => "噛みつき",
            AbilityId.EraseKill => "消去キル",
            _ => "キル",
        };
        var remaining = player.AbilityCooldowns.TryGetValue(ability, out var endsAt) ? Math.Max(0f, endsAt - Time.time) : 0f;
        var targetAvailable = ability switch
        {
            AbilityId.Kill or AbilityId.Bite or AbilityId.EraseKill or AbilityId.Track or AbilityId.GrantBarrier or AbilityId.Curse or AbilityId.Puppet => FindNearestLivingPlayer(player.PlayerId) is not null,
            AbilityId.CarryBody => FindNearestBody(player.PlayerId) is not null,
            AbilityId.SpeakWithDead => FindAnyDeadPlayer() is not null,
            _ => true,
        };
        var uses = player.PrimaryRole == RoleId.Sheriff ? player.SheriffKillsRemaining : -1;
        var color = RoleCatalog.Get(player.PrimaryRole).Faction switch
        {
            Faction.Crew => new Color(0.35f, 0.85f, 1f),
            Faction.Impostor => new Color(1f, 0.35f, 0.35f),
            _ => new Color(0.82f, 0.48f, 1f),
        };
        state = new AbilityButtonState(label, label, remaining <= 0f && targetAvailable, remaining, uses, color);
        return true;
    }

    private static AbilityId GetPrimaryAbility(PlayerState state) => state.PrimaryRole switch
    {
        RoleId.MadScientist => AbilityId.OpenVitals,
        RoleId.Tracker => AbilityId.Track,
        RoleId.TimeTraveler => AbilityId.TimeWarp,
        RoleId.BarrierNic => AbilityId.GrantBarrier,
        RoleId.Warlock => AbilityId.Curse,
        RoleId.Puppeteer => AbilityId.Puppet,
        RoleId.Undertaker => state.CarriedBodyOwnerId is null ? AbilityId.CarryBody : AbilityId.DropBody,
        RoleId.Seer => AbilityId.SpeakWithDead,
        RoleId.Vampire => AbilityId.Bite,
        RoleId.Eraser => AbilityId.EraseKill,
        _ when RoleCatalog.Get(state.PrimaryRole).CanDirectKill => AbilityId.Kill,
        _ => (AbilityId)0,
    };

    private bool TryShowDoctorDeathEstimate(PlayerControl doctor)
    {
        var bodyId = FindNearestBody(doctor.PlayerId);
        if (bodyId is not byte ownerId || !_engine.TryGetDeathAgeForDoctor(doctor.PlayerId, ownerId, Time.time, out var age))
        {
            HudManager.Instance?.ShowPopUp("近くに確認できる死体がありません。");
            return false;
        }

        var victim = _engine.Players.TryGetValue(ownerId, out var state) ? state.PlayerName : "不明";
        HudManager.Instance?.ShowPopUp($"{victim} の死亡推定時刻: {age:0.0} 秒前");
        return true;
    }

    internal readonly record struct AbilityButtonState(string Label, string Hint, bool IsReady, float CooldownRemaining, int UsesRemaining, Color Color);

    internal bool TryInterceptMurder(PlayerControl killer, PlayerControl target)
    {
        if (!_assignmentReceived || killer == null || target == null || killer.Data == null || target.Data == null || killer.Data.IsDead || target.Data.IsDead)
            return false;

        if (!_engine.Players.TryGetValue(killer.PlayerId, out var state))
            return false;

        var ability = state.PrimaryRole switch
        {
            RoleId.Vampire => AbilityId.Bite,
            RoleId.Eraser => AbilityId.EraseKill,
            _ => AbilityId.Kill,
        };

        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
        {
            _engine.TryHandleAbility(new AbilityRequest(killer.PlayerId, ability, target.PlayerId, null, Time.time), Time.time);
        }
        else
        {
            SendAbilityRequest(killer, ability, target.PlayerId);
        }
        return true;
    }

    internal void AddRoleLineToTaskText(ref string taskText)
    {
        if (!_assignmentReceived || PlayerControl.LocalPlayer == null)
            return;
        if (!_engine.Players.TryGetValue(PlayerControl.LocalPlayer.PlayerId, out var localState))
            return;
        if (taskText.Contains("tempMOD:", StringComparison.Ordinal))
            return;

        var definition = RoleCatalog.Get(localState.PrimaryRole);
        var color = definition.Faction switch
        {
            Faction.Crew => "#66D9FF",
            Faction.Impostor => "#FF5B5B",
            _ => "#D98CFF",
        };
        var abilityHint = GetAbilityHint(localState.PrimaryRole);
        taskText = $"<color={color}>tempMOD: {definition.DisplayName}{abilityHint}</color>\\n" + taskText;
    }

    private static string GetRoleIntroDescription(RoleId role)
    {
        // 開始演出では役職個別の能力説明より、まず陣営の目的を明確に示す。
        // クルーだけは固有能力が初見でも分かるよう個別説明を維持する。
        return RoleCatalog.Get(role).Faction switch
        {
            Faction.Impostor => "すべてを殺戮せよ",
            Faction.Neutral => "全員が敵だ！",
            _ => role switch
            {
                RoleId.Sheriff => "敵を直接キルできる",
                RoleId.Doctor => "死体から死亡推定時刻を読む",
                RoleId.MadScientist => "遠隔でバイタルを確認できる",
                RoleId.Tracker => "対象を追跡できる",
                RoleId.TimeTraveler => "過去の位置へ巻き戻せる",
                RoleId.Seer => "死者の霊魂と会話できる",
                RoleId.BarrierNic => "仲間にキル防止バリアを付与できる",
                RoleId.LightWorker => "停電中も視界を保つ",
                RoleId.Investigator => "足跡を追跡できる",
                RoleId.Mayor => "会議で二票を持つ",
                _ => "タスクを行う",
            },
        };
    }

    private static string GetAbilityHint(RoleId role)
    {
        return role switch
        {
            RoleId.MadScientist => "  [F: バイタル]",
            RoleId.Tracker => "  [F: 追跡]",
            RoleId.TimeTraveler => "  [F: 巻戻し]",
            RoleId.Seer => "  [F: 霊魂会話]",
            RoleId.BarrierNic => "  [F: バリア]",
            RoleId.Warlock => "  [F: 呪い]",
            RoleId.Puppeteer => "  [F: 操作支配]",
            RoleId.Undertaker => "  [F: 運搬／配置]",
            _ => string.Empty,
        };
    }

    internal bool HandleCustomRpc(byte callId, MessageReader reader, PlayerControl source)
    {
        if (callId is not ((byte)TempModRpc.AssignRoles) and not ((byte)TempModRpc.AbilityRequest) and not ((byte)TempModRpc.SyncState))
            return false;

        try
        {
            if (callId == (byte)TempModRpc.AssignRoles)
                ReceiveAssignment(reader);
            else if (callId == (byte)TempModRpc.SyncState)
                ReceiveReplicatedState(reader);
            else if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
                ReceiveAbilityRequest(reader, source);
            return true;
        }
        catch (Exception exception)
        {
            _log.LogError($"役職割当RPCの読込に失敗しました: {exception}");
            return true;
        }
    }

    private void SendAbilityRequest(PlayerControl sender, AbilityId ability, byte targetId)
    {
        if (AmongUsClient.Instance == null || PlayerControl.LocalPlayer == null)
            return;

        var writer = AmongUsClient.Instance.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId,
            (byte)TempModRpc.AbilityRequest,
            SendOption.Reliable,
            -1);
        writer.Write(sender.PlayerId);
        writer.Write((byte)ability);
        writer.Write(targetId);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    private void ReceiveAbilityRequest(MessageReader reader, PlayerControl source)
    {
        var senderId = reader.ReadByte();
        var ability = (AbilityId)reader.ReadByte();
        var rawTargetId = reader.ReadByte();
        byte? targetId = rawTargetId == byte.MaxValue ? null : rawTargetId;
        // RPC発信元のPlayerControlと要求者IDが一致しない要求は拒否する。
        if (source == null || source.PlayerId != senderId)
        {
            _log.LogWarning($"不正な能力要求を拒否しました: source={source?.PlayerId}, sender={senderId}");
            return;
        }
        _engine.TryHandleAbility(new AbilityRequest(senderId, ability, targetId, null, Time.time), Time.time);
    }

    private void BroadcastReplicatedState()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || PlayerControl.LocalPlayer == null)
            return;

        var writer = AmongUsClient.Instance.StartRpcImmediately(
            PlayerControl.LocalPlayer.NetId,
            (byte)TempModRpc.SyncState,
            SendOption.Reliable,
            -1);
        writer.Write((byte)_engine.Players.Count);
        foreach (var player in _engine.Players.Values)
        {
            writer.Write(player.PlayerId);
            writer.Write(player.PlayerName);
            writer.Write((byte)player.PrimaryRole);
            writer.Write(player.IsAlive);
            writer.Write(player.Position.X);
            writer.Write(player.Position.Y);
            writer.Write(player.HasBarrier);
            writer.Write(player.IsCursed);
            writer.Write(player.CurseExpiresAt);
            writer.Write(player.BiteExpiresAt);
            writer.Write(player.PuppetControllerId is not null);
            if (player.PuppetControllerId is byte puppetControllerId)
                writer.Write(puppetControllerId);
            writer.Write(player.PuppetExpiresAt);
            writer.Write(player.CarriedBodyOwnerId is not null);
            if (player.CarriedBodyOwnerId is byte carriedBodyOwnerId)
                writer.Write(carriedBodyOwnerId);
            writer.Write(player.RoleErasedOnDeath);
            writer.Write(player.SheriffKillsRemaining);
            writer.Write((byte)player.AbilityCooldowns.Count);
            foreach (var cooldown in player.AbilityCooldowns)
            {
                writer.Write((byte)cooldown.Key);
                writer.Write(cooldown.Value);
            }
        }

        writer.Write((byte)_engine.Bodies.Count);
        foreach (var body in _engine.Bodies.Values)
        {
            writer.Write(body.OwnerId);
            writer.Write(body.Position.X);
            writer.Write(body.Position.Y);
            writer.Write(body.DiedAt);
            writer.Write(body.IsCarried);
            writer.Write(body.RoleErased);
        }
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    private void ReceiveReplicatedState(MessageReader reader)
    {
        var players = new List<ReplicatedPlayerState>();
        var playerCount = reader.ReadByte();
        for (var index = 0; index < playerCount; index++)
        {
            var playerId = reader.ReadByte();
            var playerName = reader.ReadString();
            var role = (RoleId)reader.ReadByte();
            var isAlive = reader.ReadBoolean();
            var position = new Position(reader.ReadSingle(), reader.ReadSingle());
            var hasBarrier = reader.ReadBoolean();
            var isCursed = reader.ReadBoolean();
            var curseExpiresAt = reader.ReadSingle();
            var biteExpiresAt = reader.ReadSingle();
            byte? puppetControllerId = reader.ReadBoolean() ? reader.ReadByte() : null;
            var puppetExpiresAt = reader.ReadSingle();
            byte? carriedBodyOwnerId = reader.ReadBoolean() ? reader.ReadByte() : null;
            var roleErasedOnDeath = reader.ReadBoolean();
            var sheriffKillsRemaining = reader.ReadInt32();
            var cooldowns = new Dictionary<AbilityId, float>();
            var cooldownCount = reader.ReadByte();
            for (var cooldownIndex = 0; cooldownIndex < cooldownCount; cooldownIndex++)
                cooldowns[(AbilityId)reader.ReadByte()] = reader.ReadSingle();
            players.Add(new ReplicatedPlayerState(playerId, playerName, role, isAlive, position, hasBarrier, isCursed, curseExpiresAt, biteExpiresAt, puppetControllerId, puppetExpiresAt, carriedBodyOwnerId, roleErasedOnDeath, sheriffKillsRemaining, cooldowns));
        }

        var bodies = new List<BodyState>();
        var bodyCount = reader.ReadByte();
        for (var index = 0; index < bodyCount; index++)
            bodies.Add(new BodyState(reader.ReadByte(), new Position(reader.ReadSingle(), reader.ReadSingle()), reader.ReadSingle(), reader.ReadBoolean(), reader.ReadBoolean()));
        _engine.ApplyReplicatedState(players, bodies);
        _assignmentReceived = players.Count > 0;
        _log.LogDebug($"tempMOD: ホスト確定状態を受信しました。players={players.Count}, bodies={bodies.Count}");
    }

    private byte? FindNearestLivingPlayer(byte actorId)
    {
        if (!_engine.Players.TryGetValue(actorId, out var actor))
            return null;
        return _engine.Players.Values
            .Where(player => player.IsAlive && player.PlayerId != actorId)
            .OrderBy(player => player.Position.DistanceTo(actor.Position))
            .Select(player => (byte?)player.PlayerId)
            .FirstOrDefault();
    }

    private byte? FindNearestBody(byte actorId)
    {
        if (!_engine.Players.TryGetValue(actorId, out var actor))
            return null;
        return _engine.Bodies.Values
            .Where(body => !body.IsCarried)
            .OrderBy(body => body.Position.DistanceTo(actor.Position))
            .Select(body => (byte?)body.OwnerId)
            .FirstOrDefault();
    }

    private byte? FindAnyDeadPlayer()
        => _engine.Players.Values.Where(player => !player.IsAlive).Select(player => (byte?)player.PlayerId).FirstOrDefault();

    private void AssignAndBroadcastRoles()
    {
        var players = GetAllPlayers();
        var playerIds = players.Select(player => player.PlayerId).ToArray();
        var localPlayer = PlayerControl.LocalPlayer;
        if (playerIds.Length < 1 || localPlayer == null)
        {
            _log.LogWarning($"tempMOD役職抽選を見送りました。プレイヤー数={playerIds.Length}、ローカルプレイヤー有無={localPlayer != null}");
            return;
        }

        var seed = unchecked((int)AmongUsClient.Instance.GameId ^ Environment.TickCount);
        var assignment = RoleAssignmentPlanner.Create(playerIds, _settings.CreateAssignmentOptions(), new System.Random(seed));
        ApplyAssignment(assignment);

        var writer = AmongUsClient.Instance.StartRpcImmediately(
            localPlayer.NetId,
            (byte)TempModRpc.AssignRoles,
            SendOption.Reliable,
            -1);
        WriteAssignment(writer, assignment);
        AmongUsClient.Instance.FinishRpcImmediately(writer);
        BroadcastReplicatedState();
        _log.LogInfo($"{assignment.PrimaryRoles.Count}人のtempMOD役職をホストで割り当てました。");
    }

    private void ApplyAssignment(RoleAssignment assignment)
    {
        _engine = new RoleEngine(this, _settings.CreateRoleOptions());
        _nativeRolesApplied.Clear();
        _resultLines.Clear();
        _victoryLabel = null;
        _resultSentToChat = false;
        foreach (var pair in assignment.PrimaryRoles)
        {
            var gamePlayer = FindPlayer(pair.Key);
            var playerName = gamePlayer?.Data?.PlayerName ?? $"Player {pair.Key}";
            _engine.RegisterPlayer(pair.Key, playerName);
            _engine.AssignRole(pair.Key, pair.Value);
        }
        foreach (var pair in assignment.Modifiers)
        {
            foreach (var modifier in pair.Value)
                _engine.AddModifier(pair.Key, modifier);
        }
        foreach (var pair in assignment.LoversPairs)
            _engine.PairLovers(pair.First, pair.Second, Time.time);

        _assignmentReceived = true;
    }

    private static void WriteAssignment(MessageWriter writer, RoleAssignment assignment)
    {
        writer.Write((byte)assignment.PrimaryRoles.Count);
        foreach (var pair in assignment.PrimaryRoles)
        {
            writer.Write(pair.Key);
            writer.Write((byte)pair.Value);
        }

        writer.Write((byte)assignment.LoversPairs.Count);
        foreach (var (first, second) in assignment.LoversPairs)
        {
            writer.Write(first);
            writer.Write(second);
        }
    }

    private void ReceiveAssignment(MessageReader reader)
    {
        var roles = new Dictionary<byte, RoleId>();
        var roleCount = reader.ReadByte();
        for (var index = 0; index < roleCount; index++)
            roles[reader.ReadByte()] = (RoleId)reader.ReadByte();

        var pairs = new List<(byte First, byte Second)>();
        var pairCount = reader.ReadByte();
        for (var index = 0; index < pairCount; index++)
            pairs.Add((reader.ReadByte(), reader.ReadByte()));

        var modifiers = roles.Keys.ToDictionary(id => id, _ => (IReadOnlyList<ModifierId>)Array.Empty<ModifierId>());
        foreach (var (first, second) in pairs)
        {
            modifiers[first] = new[] { ModifierId.Lovers };
            modifiers[second] = new[] { ModifierId.Lovers };
        }
        ApplyAssignment(new RoleAssignment(roles, modifiers, pairs));
    }

    private static List<PlayerControl> GetAllPlayers()
    {
        var players = new List<PlayerControl>();
        var gamePlayers = PlayerControl.AllPlayerControls;
        for (var index = 0; index < gamePlayers.Count; index++)
            players.Add(gamePlayers[index]);
        return players;
    }

    private static PlayerControl? FindPlayer(byte playerId)
    {
        foreach (var player in GetAllPlayers())
        {
            if (player.PlayerId == playerId)
                return player;
        }
        return null;
    }

    public bool IsWalkable(Position position)
    {
        // 実マップ上の衝突判定はShipStatusのコライダー確認に置換する予定。
        // 位置履歴の復帰先については、現段階でも有限値のみ許可して安全性を確保する。
        return float.IsFinite(position.X) && float.IsFinite(position.Y);
    }

    public void Emit(GameEvent gameEvent)
    {
        _log.LogDebug($"[{gameEvent.Kind}] actor={gameEvent.ActorId}, target={gameEvent.TargetId}, detail={gameEvent.Detail}");
        if (_assignmentReceived && AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            BroadcastReplicatedState();
        if (gameEvent.Kind == GameEventKind.Victory)
        {
            _victoryLabel = VictoryLabel(gameEvent.Detail);
            foreach (var winnerId in gameEvent.ParticipantIds ?? Array.Empty<byte>())
            {
                if (_engine.Players.TryGetValue(winnerId, out var winner))
                    _resultLines.Add(new ResultLine(winner.PlayerName, "勝利", RoleCatalog.Get(winner.PrimaryRole).DisplayName));
            }
        }

        if (gameEvent.Kind == GameEventKind.PlayerDied && gameEvent.TargetId is byte deadPlayerId && _engine.Players.TryGetValue(deadPlayerId, out var deadPlayer))
        {
            var killerName = gameEvent.ActorId is byte killerId && _engine.Players.TryGetValue(killerId, out var killerState)
                ? killerState.PlayerName
                : "環境";
            var detail = string.IsNullOrWhiteSpace(gameEvent.Detail) ? "キル" : gameEvent.Detail;
            _resultLines.Add(new ResultLine(deadPlayer.PlayerName, $"{detail}({killerName})", RoleCatalog.Get(deadPlayer.PrimaryRole).DisplayName));
        }
        if (gameEvent.Kind == GameEventKind.TimeWarped && gameEvent.ActorId is byte timeTravelerId && gameEvent.Position is Position warpPosition)
        {
            var traveler = FindPlayer(timeTravelerId);
            if (traveler != null)
                traveler.transform.position = new Vector3(warpPosition.X, warpPosition.Y, traveler.transform.position.z);
        }

        if (gameEvent.Kind != GameEventKind.PlayerDied || gameEvent.TargetId is not byte targetId)
            return;
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            return;

        var target = FindPlayer(targetId);
        if (target == null)
            return;
        var killer = gameEvent.ActorId is byte actorId
            ? FindPlayer(actorId)
            : target;
        if (killer == null)
            killer = target;

        // Among Us本体の死亡RPCを使うことで、死体・死亡演出・会議・他クライアント同期を標準処理へ委譲する。
        killer.RpcMurderPlayer(target, true);
        BroadcastReplicatedState();
    }

    internal void ApplyEndGameResults(EndGameManager endGame)
    {
        if (!_assignmentReceived)
            return;

        var resultText = BuildResultText();
        if (string.IsNullOrWhiteSpace(resultText))
            return;

        if (!_resultSentToChat && HudManager.Instance != null && HudManager.Instance.Chat != null && PlayerControl.LocalPlayer != null)
        {
            foreach (var line in resultText.Split('\n'))
                HudManager.Instance.Chat.AddChat(PlayerControl.LocalPlayer, $"<color=#FFE76A>{line}</color>", false);
            _resultSentToChat = true;
        }

        var overlay = GameObject.Find("tempMOD_EndResult");
        if (overlay == null)
        {
            overlay = UnityEngine.Object.Instantiate(endGame.WinText.gameObject, endGame.WinText.transform.parent);
            overlay.name = "tempMOD_EndResult";
            overlay.transform.localPosition = endGame.WinText.transform.localPosition + new Vector3(0f, -1.55f, 0f);
            overlay.transform.localScale = Vector3.one * 0.62f;
        }
        var text = overlay.GetComponent<TextMeshPro>();
        if (text != null)
        {
            text.text = resultText;
            text.color = Color.white;
        }
    }

    private string BuildResultText()
    {
        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(_victoryLabel))
            lines.Add($"<color=#FFE76A>【{_victoryLabel}】</color>");
        lines.AddRange(_resultLines.Select(line => $"{line.PlayerName}  <color=#FFDE72>[{line.ActionText}]</color>  {line.RoleName}"));
        return string.Join("\n", lines.Distinct());
    }

    private static string VictoryLabel(string? value) => value switch
    {
        nameof(VictoryKind.Impostors) => "インポスター勝利",
        nameof(VictoryKind.Crewmates) => "クルー勝利",
        nameof(VictoryKind.Jester) => "ジェスター勝利",
        nameof(VictoryKind.Lovers) => "ラバーズ勝利",
        nameof(VictoryKind.Jackal) => "ジャッカル勝利",
        nameof(VictoryKind.Vampire) => "ヴァンパイア勝利",
        _ => "試合結果",
    };
}
