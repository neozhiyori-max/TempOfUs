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
    private readonly HashSet<byte> _movementFrozenByTempMod = new();
    private readonly HashSet<byte> _morphVisualsApplied = new();
    private readonly List<ResultLine> _resultLines = new();
    private string? _victoryLabel;
    private bool _resultSentToChat;
    private bool _omniscienceShown;
    private AbilityId _armedMeetingAbility;
        private RoleId _meetingGuessRole = RoleId.Sheriff;
    private bool _roleDescriptionChatShown;
    private bool _freeplayPracticeApplied;
    private bool _freeplayPracticeWaitingLogged;
    private bool _wasInFreeplay;
    internal TempModRuntime(ManualLogSource log, TempModSettings settings)
    {
        _log = log;
        _settings = settings;
        _engine = new RoleEngine(this, _settings.CreateRoleOptions());
    }

    internal RoleEngine Engine => _engine;

    /// <summary>牽引中のアンダーテイカーだけにTownOfUs由来の移動速度倍率を適用する。</summary>
    internal float GetMovementSpeedMultiplier(PlayerControl? player)
        => player != null && _assignmentReceived && _engine.IsUndertakerCarrying(player.PlayerId)
            ? _engine.UndertakerSpeedMultiplier
            : 1f;

    private sealed record ResultLine(string PlayerName, string ActionText, string RoleName);

    internal void OnGameStarted()
    {
        if (IsFreeplayMode())
        {
            _wasInFreeplay = true;
            ResetRuntimeState();
            _freeplayPracticeApplied = false;
            _freeplayPracticeWaitingLogged = false;
            _log.LogInfo("tempMOD: フリープレイ開始を検出しました。本体の正規ダミーを待って固定検証役職を配布します。");
            return;
        }

        // フリープレイから戻った後に役職状態を持ち越さない。オンライン・ローカルロビーは従来の抽選フローへ戻す。
        if (_wasInFreeplay)
        {
            _wasInFreeplay = false;
            ResetRuntimeState();
        }

        // OnGameStartは開始演出の後に呼ばれる版がある。抽選済みの状態をここで初期化すると
        // タスク表示・キル判定から役職が消えるため、未割当時だけ初期化する。
        if (_assignmentReceived)
        {
            _log.LogInfo("tempMOD: PlayerControl.OnGameStart を受信しましたが、役職割当済みのため状態を保持します。");
            return;
        }
        ResetRuntimeState();
        _log.LogInfo("tempMOD: PlayerControl.OnGameStart を受信しました。開始演出で役職を確定します。");
    }

    internal void OnIntroStarted()
    {
        // フリープレイは本体の正規ダミーを待つため、オンライン用のカスタムRPC割当を使わない。
        if (IsFreeplayMode())
            return;
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
            // 第三陣営はインポスター赤と区別する水色で統一する。
            _ => new Color(0.33f, 0.84f, 1.00f),
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

    internal void OnMeetingClosed(MeetingHud? meetingHud)
    {
        // 会議専用能力を構えたまま閉じても、次の会議へ対象待機状態を持ち越さない。
        _armedMeetingAbility = 0;
        if (!_assignmentReceived || !_engine.IsMeetingActive)
            return;

        // MeetingHud.RpcCloseは、ゲーム本体が既に追放演出・死亡状態を反映した後に呼ばれる。
        // RoleEngine側も必ず会議状態を解除しないと、通常HUD・Tick・能力入力が永久に会議中のまま止まる。
        byte? exiledPlayerId = meetingHud?.exiledPlayer != null ? meetingHud.exiledPlayer.PlayerId : null;
        var isHost = AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost;
        _engine.EndMeeting(exiledPlayerId, Time.time, vanillaExileAlreadyApplied: true, evaluateVictory: isHost);
        if (isHost)
            BroadcastReplicatedState();
    }

    internal void OnPlayerTick(PlayerControl player)
    {
        if (IsFreeplayMode())
        {
            _wasInFreeplay = true;
            TryApplyFreeplayPracticeAssignment();
        }
        if (!_assignmentReceived || player == null || player.Data == null || player.Data.IsDead)
            return;

        if (!_engine.Players.ContainsKey(player.PlayerId))
            _engine.RegisterPlayer(player.PlayerId, $"Player {player.PlayerId}");

        EnsureNativeRole(player);

        // TownOfUsのUndertakerと同様に、死体を牽引したままベントへ入らせない。
        // ホストだけがBodyStateを変更し、既存DeadBodyを現在位置へ安全に配置する。
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost && player.inVent && _engine.IsUndertakerCarrying(player.PlayerId))
        {
            var ventPosition = player.GetTruePosition();
            _engine.ForceDropCarriedBody(player.PlayerId, new Position(ventPosition.x, ventPosition.y), Time.time, "ベント進入");
        }

        if (PlayerControl.LocalPlayer != null && player.PlayerId == PlayerControl.LocalPlayer.PlayerId && _engine.Players.TryGetValue(player.PlayerId, out var localState))
        {
            var mustFreeze = localState.ImmobilizedUntil > Time.time;
            if (mustFreeze && player.moveable)
            {
                // tempMOD自身が停止させたことを記録する。梯子・ベント・会議など本体側の停止状態は変更しない。
                player.moveable = false;
                _movementFrozenByTempMod.Add(player.PlayerId);
            }
            else if (!mustFreeze && _movementFrozenByTempMod.Remove(player.PlayerId) && localState.IsAlive)
            {
                // 自分で停止させたプレイヤーだけを復帰させる。
                player.moveable = true;
            }
        }

        // 開始演出のコルーチンが標準の役職名を書き戻すため、演出が開いている間は
        // ローカル役職表示を毎フレーム独自の確定役職で上書きする。
        if (PlayerControl.LocalPlayer != null && player.PlayerId == PlayerControl.LocalPlayer.PlayerId && IntroCutscene.Instance != null)
            ApplyRoleIntro(IntroCutscene.Instance);

        var position = player.GetTruePosition();
        _engine.UpdatePosition(player.PlayerId, new Position(position.x, position.y), Time.time);
        if (PlayerControl.LocalPlayer != null && player.PlayerId == PlayerControl.LocalPlayer.PlayerId)
        {
            ReconcileBodyVisuals();
            // SuperNewRolesのKnowOtherAbilityと同じ目的で、ジャッカル／サイドキック間だけ名前を赤く表示する。
            // 会議・役職同期・本体の表示更新で色が戻るため、ローカル視点の確定状態に基づき毎フレーム再適用する。
            ApplyJackalTeamNameColors();
            ApplyMorphVisuals();
            ShowRoleDescriptionChatIfNeeded(player);
            ShowOmniscienceIfNeeded(player);
            if (Input.GetKeyDown(KeyCode.F))
                TryUsePrimaryAbility(player);
        }
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            _engine.Tick(Time.time);
    }

    private void EnsureNativeRole(PlayerControl player)
    {
        if (_nativeRolesApplied.Contains(player.PlayerId) || AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || RoleManager.Instance == null)
            return;
        if (!_engine.Players.TryGetValue(player.PlayerId, out var state))
            return;

        // フリープレイでは、本体が管理するダミーのネイティブ役職へ介入しない。
        // キル・ベントを必要とするローカルプレイヤーだけを標準インポスター基盤へ同期する。
        if (IsFreeplayMode() && (PlayerControl.LocalPlayer == null || player.PlayerId != PlayerControl.LocalPlayer.PlayerId))
        {
            _nativeRolesApplied.Add(player.PlayerId);
            return;
        }

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
            AbilityId.Kill or AbilityId.Bite or AbilityId.EraseKill or AbilityId.Track or AbilityId.GrantBarrier or AbilityId.Curse or AbilityId.Puppet or AbilityId.CollectDna or AbilityId.PlantBomb or AbilityId.Silence or AbilityId.InfectKill or AbilityId.ConfusionGas or AbilityId.AbsoluteDefense or AbilityId.FanaticWorship or AbilityId.Assassinate or AbilityId.Douse => FindNearestLivingPlayer(actor.PlayerId),
            AbilityId.RecruitSidekick => FindNearestRecruitableCrewmate(actor.PlayerId),
            AbilityId.CarryBody or AbilityId.Clean or AbilityId.Devour or AbilityId.AnimateBody or AbilityId.CollectBody => FindNearestBody(actor.PlayerId),
            AbilityId.SpeakWithDead => FindAnyDeadPlayer(),
            _ => null,
        };

        // 勧誘可能な対象がいない場合は、ポップアップを出さず静かに失敗する。
        // 能力ボタンの非点灯と対象射程判定が、使用可能／不可のフィードバックとなる。
        if (ability == AbilityId.RecruitSidekick && targetId is null)
            return false;
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            return _engine.TryHandleAbility(new AbilityRequest(actor.PlayerId, ability, targetId, state.Position, Time.time), Time.time);

        SendAbilityRequest(actor, ability, targetId ?? byte.MaxValue);
        return true;
    }

    internal bool CanUseMadGuesserInMeeting()
    {
        var local = PlayerControl.LocalPlayer;
        return _assignmentReceived && local != null && _engine.CanMadGuesserShoot(local.PlayerId);
    }

    internal int GetMadGuesserShotsRemaining()
    {
        var local = PlayerControl.LocalPlayer;
        return local == null ? 0 : _engine.GetMadGuesserShotsRemaining(local.PlayerId);
    }

    internal bool TryUseMadGuesserGuess(byte targetId, RoleId guessedRole)
    {
        var local = PlayerControl.LocalPlayer;
        if (!CanUseMadGuesserInMeeting() || local == null)
            return false;
        var payload = new Position((byte)guessedRole, 0);
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            return _engine.TryHandleAbility(new AbilityRequest(local.PlayerId, AbilityId.GuessRole, targetId, payload, Time.time), Time.time);
        SendAbilityRequest(local, AbilityId.GuessRole, targetId, payload);
        return true;
    }

    internal bool TryGetMeetingAbilityButtonState(out AbilityButtonState state)
    {
        state = default;
        var local = PlayerControl.LocalPlayer;
        if (!_assignmentReceived || local == null || !_engine.IsMeetingActive || !_engine.Players.TryGetValue(local.PlayerId, out var player) || !player.IsAlive)
            return false;

        var ability = player.PrimaryRole switch
        {
            RoleId.Advocate => AbilityId.Bribe,
            RoleId.Deceptor => AbilityId.DeceiveVote,
            _ => (AbilityId)0,
        };
        if (ability == 0)
            return false;

        var label = ability == AbilityId.GuessRole
            ? $"推測: {RoleCatalog.Get(_meetingGuessRole).DisplayName}"
            : ability == AbilityId.Bribe ? "買収" : "票偽装";
        var remaining = player.AbilityCooldowns.TryGetValue(ability, out var endsAt) ? Math.Max(0f, endsAt - Time.time) : 0f;
        var factionColor = RoleCatalog.Get(player.PrimaryRole).Faction == Faction.Impostor
            ? new Color(1f, .35f, .35f)
            : new Color(.33f, .84f, 1f);
        state = new AbilityButtonState(label, "能力ボタンを押してから投票パネルの対象を選択", remaining <= 0f, remaining, -1, factionColor);
        return true;
    }

    internal bool ArmMeetingAbility()
    {
        var local = PlayerControl.LocalPlayer;
        if (!TryGetMeetingAbilityButtonState(out _) || local == null || !_engine.Players.TryGetValue(local.PlayerId, out var state))
            return false;
        _armedMeetingAbility = state.PrimaryRole switch
        {
            RoleId.Advocate => AbilityId.Bribe,
            RoleId.Deceptor => AbilityId.DeceiveVote,
            _ => (AbilityId)0,
        };
        if (_armedMeetingAbility == AbilityId.GuessRole)
        {
            var crewRoles = TempModSettings.SelectableRoles.Where(role => RoleCatalog.GetFaction(role) == Faction.Crew).ToArray();
            var index = Array.IndexOf(crewRoles, _meetingGuessRole);
            _meetingGuessRole = crewRoles[(index + 1 + crewRoles.Length) % crewRoles.Length];
            HudManager.Instance?.ShowPopUp($"<color=#FF6666>推測役職: {RoleCatalog.Get(_meetingGuessRole).DisplayName}</color>\n次に投票パネルから対象を選択してください。");
        }
        else
        {
            var action = _armedMeetingAbility == AbilityId.Bribe ? "買収" : "票偽装";
            HudManager.Instance?.ShowPopUp($"<color=#D890FF>{action}を準備しました</color>\n次に投票パネルから対象を選択してください。");
        }
        return true;
    }

    internal bool TryConsumeMeetingAbilityVote(byte voterId, byte targetId)
    {
        var local = PlayerControl.LocalPlayer;
        if (_armedMeetingAbility == 0 || local == null || voterId != local.PlayerId || !_engine.Players.TryGetValue(voterId, out var state))
            return false;
        var ability = _armedMeetingAbility;
        _armedMeetingAbility = 0;
        var payload = ability == AbilityId.GuessRole ? new Position((byte)_meetingGuessRole, 0) : (Position?)null;
        if (AmongUsClient.Instance != null && AmongUsClient.Instance.AmHost)
            return _engine.TryHandleAbility(new AbilityRequest(voterId, ability, targetId, payload, Time.time), Time.time);
        SendAbilityRequest(local, ability, targetId, payload);
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
            AbilityId.Clean => "清掃",
            AbilityId.GuessRole => "推測",
            AbilityId.CollectDna => "遺伝子採取",
            AbilityId.Morph => "変身",
            AbilityId.MarionetteKill => "糸操作",
            AbilityId.PlantBomb => "爆弾設置",
            AbilityId.Wiretap => "盗聴",
            AbilityId.SetTrap => "罠設置",
            AbilityId.Blackout => "目隠し",
            AbilityId.Phase => "幽体化",
            AbilityId.CheckBounty => "ターゲット確認",
            AbilityId.ReviveMinion => "従者蘇生",
            AbilityId.Hack => "偽装工作",
            AbilityId.CreateIllusion => "分身生成",
            AbilityId.Silence => "口封じ",
            AbilityId.Devour => "捕食",
            AbilityId.StealTime => "時間強奪",
            AbilityId.DeceiveVote => "票偽装",
            AbilityId.AnimateBody => "死体操縦",
            AbilityId.LinkCurse => "呪詛リンク",
            AbilityId.AlchemyStealth => "錬金ステルス",
            AbilityId.Omniscience => "全知",
            AbilityId.RecruitSidekick => "陣営勧誘",
            AbilityId.AlignFaction => "陣営同調",
            AbilityId.InfectKill => "感染キル",
            AbilityId.AbandonTasks => "タスク放棄",
            AbilityId.Bribe => "買収",
            AbilityId.ConfusionGas => "錯乱ガス",
            AbilityId.Douse => "ガソリン噴霧",
            AbilityId.Ignite => "点火",
            AbilityId.SelfDestruct => "自爆",
            AbilityId.CollectBody => "死体回収",
            AbilityId.StealItem => "アイテム強奪",
            AbilityId.AbsoluteDefense => "絶対防御",
            AbilityId.FanaticWorship => "狂信",
            AbilityId.StealSkin => "スキン強奪",
            AbilityId.CaptureGhost => "幽霊捕獲",
            AbilityId.ForceEject => "強制退場",
            AbilityId.Spectate => "観戦モード",
            AbilityId.Assassinate => "暗殺",
            _ => "キル",
        };
        var remaining = player.AbilityCooldowns.TryGetValue(ability, out var endsAt) ? Math.Max(0f, endsAt - Time.time) : 0f;
        var targetAvailable = ability switch
        {
            AbilityId.Kill or AbilityId.Bite or AbilityId.EraseKill or AbilityId.Track or AbilityId.GrantBarrier or AbilityId.Curse or AbilityId.Puppet or AbilityId.CollectDna or AbilityId.PlantBomb or AbilityId.Silence or AbilityId.InfectKill or AbilityId.ConfusionGas or AbilityId.AbsoluteDefense or AbilityId.FanaticWorship or AbilityId.Assassinate or AbilityId.Douse => FindNearestLivingPlayer(player.PlayerId) is not null,
            AbilityId.RecruitSidekick => FindNearestRecruitableCrewmate(player.PlayerId) is not null,
            AbilityId.CarryBody or AbilityId.Clean or AbilityId.Devour or AbilityId.AnimateBody or AbilityId.CollectBody => FindNearestBody(player.PlayerId) is not null,
            AbilityId.SpeakWithDead => FindAnyDeadPlayer() is not null,
            _ => true,
        };
        var uses = player.PrimaryRole == RoleId.Sheriff ? player.SheriffKillsRemaining : -1;
        var color = RoleCatalog.Get(player.PrimaryRole).Faction switch
        {
            Faction.Crew => new Color(0.35f, 0.85f, 1f),
            Faction.Impostor => new Color(1f, 0.35f, 0.35f),
            // ジャッカル／サイドキックを含む第三陣営は、インポスター赤ではなく水色で表示する。
            _ => new Color(0.33f, 0.84f, 1f),
        };
        state = new AbilityButtonState(label, label, remaining <= 0f && targetAvailable, remaining, uses, color);
        return true;
    }

    private AbilityId GetPrimaryAbility(PlayerState state) => state.PrimaryRole switch
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
        RoleId.Cleaner => AbilityId.Clean,
        // マッドゲッサーは会議中のプレイヤー横「推測」ボタンだけを使用する。
        RoleId.MadGuesser => (AbilityId)0,
        RoleId.Morphing => state.AbilityCooldowns.ContainsKey(AbilityId.CollectDna) ? AbilityId.Morph : AbilityId.CollectDna,
        RoleId.Marionette => AbilityId.MarionetteKill,
        RoleId.Bomber => AbilityId.PlantBomb,
        RoleId.Spy => AbilityId.Wiretap,
        RoleId.Trapper => AbilityId.SetTrap,
        RoleId.Blackout => AbilityId.Blackout,
        RoleId.Phantom => AbilityId.Phase,
        RoleId.BountyHunter => AbilityId.CheckBounty,
        RoleId.VampireLord => AbilityId.ReviveMinion,
        RoleId.Hacker => AbilityId.Hack,
        RoleId.Illusionist => AbilityId.CreateIllusion,
        RoleId.Silencer => AbilityId.Silence,
        RoleId.Gluttony => AbilityId.Devour,
        RoleId.TimeThief => AbilityId.StealTime,
        RoleId.Deceptor => AbilityId.DeceiveVote,
        RoleId.Necromancer => AbilityId.AnimateBody,
        RoleId.Witch => AbilityId.LinkCurse,
        RoleId.Alchemist => AbilityId.AlchemyStealth,
        RoleId.God => AbilityId.Omniscience,
        RoleId.Jackal => AbilityId.RecruitSidekick,
        RoleId.SchrodingerCat => AbilityId.AlignFaction,
        RoleId.Zombie => AbilityId.InfectKill,
        RoleId.Apathy => AbilityId.AbandonTasks,
        RoleId.Advocate => AbilityId.Bribe,
        RoleId.Clown => AbilityId.ConfusionGas,
        RoleId.Arsonist => _engine.Players.Values.Where(player => player.IsAlive && player.PlayerId != state.PlayerId).All(player => player.EffectTargets.TryGetValue(AbilityId.Douse, out var ownerId) && ownerId == state.PlayerId) ? AbilityId.Ignite : AbilityId.Douse,
        RoleId.Terrorist => AbilityId.SelfDestruct,
        RoleId.Vulture => AbilityId.CollectBody,
        RoleId.Collector => AbilityId.StealItem,
        RoleId.Guardian => AbilityId.AbsoluteDefense,
        RoleId.Fanatic => AbilityId.FanaticWorship,
        RoleId.Thief => AbilityId.StealSkin,
        RoleId.GhostHunter => AbilityId.CaptureGhost,
        RoleId.Bouncer => AbilityId.ForceEject,
        RoleId.Spectator => AbilityId.Spectate,
        RoleId.Assassin => AbilityId.Assassinate,
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
            RoleId.Alchemist => AbilityId.AlchemyStealth,
            RoleId.Thief => AbilityId.StealSkin,
            RoleId.Zombie => AbilityId.InfectKill,
            RoleId.Assassin => AbilityId.Assassinate,
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
            _ => "#55D7FF",
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
            RoleId.Cleaner => "  [F: 清掃]",
            RoleId.MadGuesser => "  [会議中: 対象横の推測]",
            RoleId.Morphing => "  [F: 遺伝子採取／変身]",
            RoleId.Marionette => "  [F: 糸操作]",
            RoleId.Bomber => "  [F: 爆弾設置]",
            RoleId.Spy => "  [F: 盗聴]",
            RoleId.Trapper => "  [F: 罠設置]",
            RoleId.Blackout => "  [F: 目隠し]",
            RoleId.Phantom => "  [F: 幽体化]",
            RoleId.BountyHunter => "  [F: ターゲット確認]",
            RoleId.VampireLord => "  [F: 従者蘇生]",
            RoleId.Hacker => "  [F: 偽装工作]",
            RoleId.Illusionist => "  [F: 分身生成]",
            RoleId.Silencer => "  [F: 口封じ]",
            RoleId.Gluttony => "  [F: 捕食]",
            RoleId.TimeThief => "  [F: 時間強奪]",
            RoleId.Deceptor => "  [F: 票偽装]",
            RoleId.Necromancer => "  [F: 死体操縦]",
            RoleId.Witch => "  [F: 呪詛リンク]",
            RoleId.Alchemist => "  [F: 錬金ステルス]",
            RoleId.God => "  [F: 全知]",
            RoleId.Jackal => "  [F: キル／陣営勧誘]",
            RoleId.SchrodingerCat => "  [F: 陣営同調]",
            RoleId.Zombie => "  [F: 感染キル]",
            RoleId.Apathy => "  [F: タスク放棄]",
            RoleId.Advocate => "  [F: 買収]",
            RoleId.Clown => "  [F: 錯乱ガス]",
            RoleId.Arsonist => "  [F: ガソリン噴霧／点火]",
            RoleId.Terrorist => "  [F: 自爆]",
            RoleId.Vulture => "  [F: 死体回収]",
            RoleId.Collector => "  [F: アイテム強奪]",
            RoleId.Guardian => "  [F: 絶対防御]",
            RoleId.Fanatic => "  [F: 狂信]",
            RoleId.Thief => "  [F: スキン強奪]",
            RoleId.GhostHunter => "  [F: 幽霊捕獲]",
            RoleId.Bouncer => "  [F: 強制退場]",
            RoleId.Spectator => "  [F: 観戦モード]",
            RoleId.Assassin => "  [F: 暗殺]",
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

    private void SendAbilityRequest(PlayerControl sender, AbilityId ability, byte targetId, Position? requestedPosition = null)
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
        writer.Write(requestedPosition is not null);
        if (requestedPosition is Position position)
        {
            writer.Write(position.X);
            writer.Write(position.Y);
        }
        AmongUsClient.Instance.FinishRpcImmediately(writer);
    }

    private void ReceiveAbilityRequest(MessageReader reader, PlayerControl source)
    {
        var senderId = reader.ReadByte();
        var ability = (AbilityId)reader.ReadByte();
        var rawTargetId = reader.ReadByte();
        byte? targetId = rawTargetId == byte.MaxValue ? null : rawTargetId;
        Position? requestedPosition = reader.ReadBoolean() ? new Position(reader.ReadSingle(), reader.ReadSingle()) : null;
        // RPC発信元のPlayerControlと要求者IDが一致しない要求は拒否する。
        if (source == null || source.PlayerId != senderId)
        {
            _log.LogWarning($"不正な能力要求を拒否しました: source={source?.PlayerId}, sender={senderId}");
            return;
        }
        _engine.TryHandleAbility(new AbilityRequest(senderId, ability, targetId, requestedPosition, Time.time), Time.time);
    }

    private void BroadcastReplicatedState()
    {
        // フリープレイは完全にオフラインの固定検証であり、tempMODのカスタムRPCを送信しない。
        if (IsFreeplayMode())
            return;
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
            writer.Write(player.MadGuesserShotsThisMeeting);
            writer.Write((byte)player.AbilityCooldowns.Count);
            foreach (var cooldown in player.AbilityCooldowns)
            {
                writer.Write((byte)cooldown.Key);
                writer.Write(cooldown.Value);
            }
            writer.Write((byte)player.EffectExpiresAt.Count);
            foreach (var effect in player.EffectExpiresAt)
            {
                writer.Write((byte)effect.Key);
                writer.Write(effect.Value);
            }
            writer.Write((byte)player.EffectTargets.Count);
            foreach (var target in player.EffectTargets)
            {
                writer.Write((byte)target.Key);
                writer.Write(target.Value);
            }
            writer.Write((byte)player.EffectCounts.Count);
            foreach (var count in player.EffectCounts)
            {
                writer.Write((byte)count.Key);
                writer.Write(count.Value);
            }
            writer.Write(player.SecondaryEffectTargetId is not null);
            if (player.SecondaryEffectTargetId is byte secondaryTargetId)
                writer.Write(secondaryTargetId);
            writer.Write(player.ImmobilizedUntil);
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
            writer.Write(body.InvisibleUntil);
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
            var madGuesserShotsThisMeeting = reader.ReadInt32();
            var cooldowns = new Dictionary<AbilityId, float>();
            var cooldownCount = reader.ReadByte();
            for (var cooldownIndex = 0; cooldownIndex < cooldownCount; cooldownIndex++)
                cooldowns[(AbilityId)reader.ReadByte()] = reader.ReadSingle();
            var effectExpiresAt = new Dictionary<AbilityId, float>();
            var effectCount = reader.ReadByte();
            for (var effectIndex = 0; effectIndex < effectCount; effectIndex++)
                effectExpiresAt[(AbilityId)reader.ReadByte()] = reader.ReadSingle();
            var effectTargets = new Dictionary<AbilityId, byte>();
            var effectTargetCount = reader.ReadByte();
            for (var effectTargetIndex = 0; effectTargetIndex < effectTargetCount; effectTargetIndex++)
                effectTargets[(AbilityId)reader.ReadByte()] = reader.ReadByte();
            var effectCounts = new Dictionary<AbilityId, int>();
            var effectCountCount = reader.ReadByte();
            for (var effectCountIndex = 0; effectCountIndex < effectCountCount; effectCountIndex++)
                effectCounts[(AbilityId)reader.ReadByte()] = reader.ReadInt32();
            byte? secondaryEffectTargetId = reader.ReadBoolean() ? reader.ReadByte() : null;
            var immobilizedUntil = reader.ReadSingle();
            players.Add(new ReplicatedPlayerState(playerId, playerName, role, isAlive, position, hasBarrier, isCursed, curseExpiresAt, biteExpiresAt, puppetControllerId, puppetExpiresAt, carriedBodyOwnerId, roleErasedOnDeath, sheriffKillsRemaining, cooldowns)
            {
                EffectExpiresAt = effectExpiresAt,
                EffectTargets = effectTargets,
                EffectCounts = effectCounts,
                MadGuesserShotsThisMeeting = madGuesserShotsThisMeeting,
                SecondaryEffectTargetId = secondaryEffectTargetId,
                ImmobilizedUntil = immobilizedUntil,
            });
        }

        var bodies = new List<BodyState>();
        var bodyCount = reader.ReadByte();
        for (var index = 0; index < bodyCount; index++)
            bodies.Add(new BodyState(reader.ReadByte(), new Position(reader.ReadSingle(), reader.ReadSingle()), reader.ReadSingle(), reader.ReadBoolean(), reader.ReadBoolean(), reader.ReadSingle()));
        _engine.ApplyReplicatedState(players, bodies);
        _assignmentReceived = players.Count > 0;
        _log.LogDebug($"tempMOD: ホスト確定状態を受信しました。players={players.Count}, bodies={bodies.Count}");
    }

    /// <summary>
    /// SuperNewRolesのKnowOtherAbility相当。ローカルプレイヤーがジャッカル陣営の時だけ、
    /// ジャッカルとサイドキックを相互に第三陣営の水色で可視化する。
    /// </summary>
    private void ApplyJackalTeamNameColors()
    {
        var localPlayer = PlayerControl.LocalPlayer;
        if (localPlayer == null || !_engine.IsJackalTeamMember(localPlayer.PlayerId))
            return;

        var allPlayers = PlayerControl.AllPlayerControls;
        for (var index = 0; index < allPlayers.Count; index++)
        {
            var player = allPlayers[index];
            if (player?.cosmetics?.nameText == null)
                continue;
            if (_engine.IsJackalTeamMember(player.PlayerId))
                player.cosmetics.nameText.color = new Color(0.33f, 0.84f, 1f);
        }
    }

    private byte? FindNearestLivingPlayer(byte actorId)
    {
        if (!_engine.Players.TryGetValue(actorId, out var actor))
            return null;
                return _engine.Players.Values
            .Where(player => player.IsAlive && player.PlayerId != actorId && player.Position.DistanceTo(actor.Position) <= _engine.TargetRange)
            .OrderBy(player => player.Position.DistanceTo(actor.Position))
            .Select(player => (byte?)player.PlayerId)
            .FirstOrDefault();
    }
    private byte? FindNearestRecruitableCrewmate(byte actorId)
    {
        if (!_engine.Players.TryGetValue(actorId, out var actor))
            return null;
        return _engine.Players.Values
            .Where(player => player.IsAlive && player.PlayerId != actorId && RoleCatalog.GetFaction(player.PrimaryRole) == Faction.Crew && player.Position.DistanceTo(actor.Position) <= _engine.TargetRange)
            .OrderBy(player => player.Position.DistanceTo(actor.Position))
            .Select(player => (byte?)player.PlayerId)
            .FirstOrDefault();
    }
    private byte? FindNearestBody(byte actorId)
    {
        if (!_engine.Players.TryGetValue(actorId, out var actor))
            return null;
        return _engine.Bodies.Values
            .Where(body => !body.IsCarried && body.Position.DistanceTo(actor.Position) <= _engine.TargetRange)
            .OrderBy(body => body.Position.DistanceTo(actor.Position))
            .Select(body => (byte?)body.OwnerId)
            .FirstOrDefault();
    }

    private byte? FindAnyDeadPlayer()
        => _engine.Players.Values.Where(player => !player.IsAlive).Select(player => (byte?)player.PlayerId).FirstOrDefault();

    internal void RequestFreeplayPracticeReapply()
    {
        if (!IsFreeplayMode())
        {
            _log.LogInfo("tempMOD: フリープレイ以外では1人用検証の適用を行いません。");
            return;
        }
        _freeplayPracticeApplied = false;
        _freeplayPracticeWaitingLogged = false;
        _log.LogInfo("tempMOD: 1人用フリープレイ検証の再適用を予約しました。");
    }

    private static bool IsFreeplayMode()
        => AmongUsClient.Instance != null && AmongUsClient.Instance.NetworkMode == NetworkModes.FreePlay;

    private void ResetRuntimeState()
    {
        _engine = new RoleEngine(this, _settings.CreateRoleOptions());
        _assignmentReceived = false;
        _nativeRolesApplied.Clear();
        _movementFrozenByTempMod.Clear();
        _morphVisualsApplied.Clear();
        _resultLines.Clear();
        _victoryLabel = null;
        _resultSentToChat = false;
        _introRoleLogged = false;
        _omniscienceShown = false;
        _roleDescriptionChatShown = false;
        _armedMeetingAbility = 0;
    }

    private void TryApplyFreeplayPracticeAssignment()
    {
        if (_freeplayPracticeApplied || !IsFreeplayMode())
            return;
        var localPlayer = PlayerControl.LocalPlayer;
        if (localPlayer == null || localPlayer.Data == null)
            return;

        // ここで扱うのは、ゲーム本体が既に生成・登録したPlayerControlだけである。
        // GameData.AddDummy、Spawn、PlayerPrefab複製、参加者追加は一切行わない。
        var players = GetAllPlayers()
            .Where(player => player != null && player.Data != null && !player.Data.Disconnected)
            .ToList();
        if (players.Count < 2)
        {
            if (!_freeplayPracticeWaitingLogged)
            {
                _freeplayPracticeWaitingLogged = true;
                _log.LogWarning("tempMOD: フリープレイの正規ダミーを待機中です。ゲーム本体のフリープレイ画面へ入り、ダミーが表示されてから検証を開始してください。");
            }
            return;
        }

        var roles = new Dictionary<byte, RoleId>();
        foreach (var player in players)
            roles[player.PlayerId] = player.PlayerId == localPlayer.PlayerId ? _settings.FreeplayPracticeRole.Value : _settings.FreeplayDummyRole.Value;
        var modifiers = roles.Keys.ToDictionary(id => id, _ => (IReadOnlyList<ModifierId>)Array.Empty<ModifierId>());
        ApplyAssignment(new RoleAssignment(roles, modifiers, Array.Empty<(byte First, byte Second)>()));
        _freeplayPracticeApplied = true;
        _freeplayPracticeWaitingLogged = false;
        var actorRoleName = RoleCatalog.Get(_settings.FreeplayPracticeRole.Value).DisplayName;
        var dummyRoleName = RoleCatalog.Get(_settings.FreeplayDummyRole.Value).DisplayName;
        _log.LogInfo($"tempMOD: 1人用プラクティス検証を固定配布しました。自分={actorRoleName}、正規ダミー={dummyRoleName}、対象数={players.Count - 1}");
        HudManager.Instance?.ShowPopUp($"<color=#8FE9FF>1人用プラクティス検証</color>\n自分: {actorRoleName}\n正規ダミー: {dummyRoleName}\n<color=#78FF91>自動適用完了</color>");
    }

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

    private void ShowRoleDescriptionChatIfNeeded(PlayerControl localPlayer)
    {
        if (_roleDescriptionChatShown || !_assignmentReceived || HudManager.Instance?.Chat == null)
            return;
        if (!_engine.Players.TryGetValue(localPlayer.PlayerId, out var state))
            return;

        var definition = RoleCatalog.Get(state.PrimaryRole);
        var color = definition.Faction switch
        {
            Faction.Crew => "#55D7FF",
            Faction.Impostor => "#FF6666",
            _ => "#D890FF",
        };
        var title = $"<color={color}>【あなたの役職: {definition.DisplayName}】</color>";
        HudManager.Instance.Chat.AddChat(localPlayer, title + "\n" + RoleDescriptionCatalog.Get(state.PrimaryRole), false);
        _roleDescriptionChatShown = true;
    }

    private void ShowOmniscienceIfNeeded(PlayerControl localPlayer)
    {
        if (_omniscienceShown || !_engine.Players.TryGetValue(localPlayer.PlayerId, out var localState) || localState.PrimaryRole != RoleId.God)
            return;
        if (!localState.EffectExpiresAt.ContainsKey(AbilityId.Omniscience))
            return;

        var lines = _engine.Players.Values
            .OrderBy(player => player.PlayerId)
            .Select(player => $"{player.PlayerName}: {RoleCatalog.Get(player.PrimaryRole).DisplayName}");
        HudManager.Instance?.ShowPopUp("<color=#FFE76A>全知</color>\n" + string.Join("\n", lines));
        _omniscienceShown = true;
    }

    /// <summary>
    /// TownOfUs MorphlingのMorph / Unmorphと同じ目的で、EffectTargetsに同期された対象の外見を既存PlayerControlへ表示する。
    /// SetOutfit系のネットワークRPCは使わず、各クライアントがホスト確定の状態を再現するため、他プレイヤーの外見データを改変しない。
    /// </summary>
    private void ApplyMorphVisuals()
    {
        var activeMorphs = new HashSet<byte>();
        foreach (var state in _engine.Players.Values)
        {
            if (state.PrimaryRole != RoleId.Morphing ||
                !state.EffectExpiresAt.TryGetValue(AbilityId.Morph, out var endsAt) || endsAt <= Time.time ||
                !state.EffectTargets.TryGetValue(AbilityId.Morph, out var targetId))
                continue;

            var morphingPlayer = FindPlayer(state.PlayerId);
            var targetPlayer = FindPlayer(targetId);
            if (morphingPlayer?.Data == null || targetPlayer?.Data == null)
                continue;

            // RawSetOutfitはローカルな見た目だけを更新する。本体のDataやネットワーク役職は変更しない。
            morphingPlayer.RawSetOutfit(targetPlayer.Data.DefaultOutfit, PlayerOutfitType.Default);
            activeMorphs.Add(state.PlayerId);
        }

        // 変身時間が切れたプレイヤーは本人のDefaultOutfitへ一度だけ戻す。
        foreach (var playerId in _morphVisualsApplied.Where(playerId => !activeMorphs.Contains(playerId)).ToArray())
        {
            var player = FindPlayer(playerId);
            if (player?.Data != null)
                player.RawSetOutfit(player.Data.DefaultOutfit, PlayerOutfitType.Default);
        }

        _morphVisualsApplied.Clear();
        _morphVisualsApplied.UnionWith(activeMorphs);
    }

    private void ReconcileBodyVisuals()
    {
        // TownOfUs JanitorのCoroutine.CleanCoroutineと同じ目的で、清掃中の既存DeadBodyをフェードさせる。
        // 進捗はホスト確定のEffectTargets / EffectExpiresAtだけから復元し、クライアント側で独自状態を持たない。
        var cleaningEndsAt = new Dictionary<byte, float>();
        foreach (var cleaner in _engine.Players.Values)
        {
            if (cleaner.EffectTargets.TryGetValue(AbilityId.Clean, out var bodyOwnerId) &&
                cleaner.EffectExpiresAt.TryGetValue(AbilityId.Clean, out var endsAt) && endsAt > Time.time)
                cleaningEndsAt[bodyOwnerId] = endsAt;
        }

        // 死体はPlayerControlやネットワーク参加者を複製せず、既存DeadBodyだけを役職エンジンの確定状態へ追従させる。
        foreach (var body in UnityEngine.Object.FindObjectsOfType<DeadBody>())
        {
            if (body == null || !_engine.Players.TryGetValue(body.ParentId, out var owner) || owner.IsAlive)
                continue;

            if (!_engine.Bodies.TryGetValue(body.ParentId, out var state))
            {
                // 清掃・捕食・回収済みの死体は無効化し、通報対象にならないようにする。
                body.Reported = true;
                body.gameObject.SetActive(false);
                continue;
            }

            if (state.IsCarried)
            {
                // TownOfUs DragBodyと同じく、牽引中の死体を運搬者の少し後ろへ既存DeadBodyとして追従させる。
                // 新しいPlayerControlやネットワーク参加者は作成しない。
                var carrier = _engine.Players.Values.FirstOrDefault(player => player.CarriedBodyOwnerId == body.ParentId);
                var carrierControl = carrier is null ? null : FindPlayer(carrier.PlayerId);
                if (carrierControl != null)
                {
                    var carrierPosition = carrierControl.transform.position;
                    body.transform.position = new Vector3(carrierPosition.x - .18f, carrierPosition.y - .36f, body.transform.position.z);
                }
                if (body.bodyRenderers != null)
                {
                    foreach (var renderer in body.bodyRenderers)
                    {
                        if (renderer == null) continue;
                        renderer.material.SetColor("_OutlineColor", Color.green);
                        renderer.material.SetFloat("_Outline", 1f);
                    }
                }
            }
            else
            {
                if (state.Position != Position.Zero)
                    body.transform.position = new Vector3(state.Position.X, state.Position.Y, body.transform.position.z);
                if (body.bodyRenderers != null)
                {
                    foreach (var renderer in body.bodyRenderers)
                    {
                        if (renderer != null)
                            renderer.material.SetFloat("_Outline", 0f);
                    }
                }
            }

            var isInvisible = state.InvisibleUntil > Time.time;
            var cleanFade = cleaningEndsAt.TryGetValue(body.ParentId, out var cleanEndsAt)
                ? Mathf.Clamp01((cleanEndsAt - Time.time) / Mathf.Max(.1f, _engine.CleanerDuration))
                : 1f;
            if (body.bloodSplatter != null)
                body.bloodSplatter.enabled = !isInvisible;
            if (body.bodyRenderers != null)
            {
                foreach (var renderer in body.bodyRenderers)
                {
                    if (renderer != null)
                    {
                        renderer.enabled = !isInvisible;
                        var color = renderer.color;
                        color.a = isInvisible ? 0f : cleanFade;
                        renderer.color = color;
                    }
                }
            }
        }
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
        if (gameEvent.Kind == GameEventKind.RoleChanged && gameEvent.TargetId is byte changedPlayerId)
        {
            // 勧誘・陣営変化では、役職エンジンだけでなくゲーム本体の役職も即座に更新する。
            // Sidekickはインポスター基盤となるため、ジャッカルの味方として名前色・標準キルHUDが本体側にも反映される。
            _nativeRolesApplied.Remove(changedPlayerId);
            var changedPlayer = FindPlayer(changedPlayerId);
            if (changedPlayer != null)
                EnsureNativeRole(changedPlayer);
            // 勧誘・昇格の成功通知はHUDポップアップを出さない。
            // 役職変更、名前色、ネイティブ役職更新、ホスト同期だけを即時適用する。
        }
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
        nameof(VictoryKind.God) => "神（ゴッド）勝利",
        nameof(VictoryKind.Zombie) => "ゾンビ陣営勝利",
        nameof(VictoryKind.Apathy) => "アパシー勝利",
        nameof(VictoryKind.Advocate) => "アドボケイト共同勝利",
        nameof(VictoryKind.Clown) => "ピエロ共同勝利",
        nameof(VictoryKind.Arsonist) => "アルソニスト勝利",
        nameof(VictoryKind.Terrorist) => "テロリスト勝利",
        nameof(VictoryKind.Vulture) => "ハゲタカ勝利",
        nameof(VictoryKind.Collector) => "コレクター勝利",
        nameof(VictoryKind.Guardian) => "ガーディアン勝利",
        nameof(VictoryKind.Fanatic) => "ファナティック共同勝利",
        nameof(VictoryKind.Thief) => "シーフ勝利",
        nameof(VictoryKind.GhostHunter) => "ゴーストハンター勝利",
        nameof(VictoryKind.Bouncer) => "バウンサー勝利",
        nameof(VictoryKind.Spectator) => "スペクテイター共同勝利",
        nameof(VictoryKind.Assassin) => "アサシン勝利",
        _ => "試合結果",
    };
}
