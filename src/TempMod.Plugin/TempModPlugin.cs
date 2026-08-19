using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Hazel;
using Il2CppInterop.Runtime;
using Il2CppInterop.Runtime.Injection;
using TempMod.Core;
using TempMod.Plugin.Integration;
using TempMod.Plugin.UI;
using TMPro;

namespace TempMod.Plugin;

[BepInPlugin(PluginGuid, PluginName, "0.2.3")]
public sealed class TempModPlugin : BasePlugin
{
    public const string PluginGuid = "jp.tempmod.amongus";
    public const string PluginName = "tempMOD";
    internal const string PluginVersion = "0.2.3";

    internal static TempModPlugin Instance { get; private set; } = null!;
    internal static TempModRuntime Runtime { get; private set; } = null!;
    internal static TempModSettings Settings { get; private set; } = null!;
    internal static MatchControlSettings MatchSettings { get; private set; } = null!;
    internal static MatchControlRuntime MatchRuntime { get; private set; } = null!;

    public override void Load()
    {
        Instance = this;
        Settings = new TempModSettings(Config);
        Runtime = new TempModRuntime(Log, Settings);
        MatchSettings = new MatchControlSettings(Config);
        MatchRuntime = new MatchControlRuntime(Log);
        ClassInjector.RegisterTypeInIl2Cpp<IntroRoleDisplayGuard>();
        ClassInjector.RegisterTypeInIl2Cpp<RoleHoverHint>();
        var harmony = new Harmony(PluginGuid);
        harmony.PatchAll(typeof(TempModPlugin).Assembly);
#if !TEMPMOD_ADMIN
        GitHubAutoUpdater.CheckInBackground(Log);
#endif
        Log.LogInfo($"{PluginName} {PluginVersion} を読み込みました。");
    }
}

[HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]
internal static class MainMenuManagerStartPatch
{
    private static void Postfix()
    {
        TitleLogoPresenter.ShowAtInitialTitleOnly();
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Start))]
internal static class GameStartManagerStartPatch
{
    private static void Postfix(GameStartManager __instance)
    {
        __instance.MinPlayers = 1;
        TempModPlugin.Instance.Log.LogInfo("tempMOD: 最小開始人数を1人に設定しました。");
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
internal static class GameStartManagerUpdatePatch
{
    private static void Prefix(GameStartManager __instance)
    {
        // バニラ側のロビー更新が最小人数を戻す場合にも、開始可能人数を維持する。
        if (__instance.MinPlayers != 1)
            __instance.MinPlayers = 1;
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.OnGameStart))]
internal static class PlayerControlOnGameStartPatch
{
    private static void Postfix()
    {
        TempModPlugin.Runtime.OnGameStarted();
    }
}

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.CoBegin))]
internal static class IntroCutsceneCoBeginPatch
{
    private static void Postfix(IntroCutscene __instance)
    {
        TempModPlugin.Runtime.OnIntroStarted();
        if (__instance != null && __instance.gameObject.GetComponent(Il2CppType.Of<IntroRoleDisplayGuard>()) == null)
            __instance.gameObject.AddComponent(Il2CppType.Of<IntroRoleDisplayGuard>());
    }
}

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginCrewmate))]
internal static class IntroCutsceneBeginCrewmatePatch
{
    private static void Postfix(IntroCutscene __instance)
    {
        TempModPlugin.Runtime.ApplyRoleIntro(__instance);
    }
}

[HarmonyPatch(typeof(IntroCutscene), nameof(IntroCutscene.BeginImpostor))]
internal static class IntroCutsceneBeginImpostorPatch
{
    private static void Postfix(IntroCutscene __instance)
    {
        TempModPlugin.Runtime.ApplyRoleIntro(__instance);
    }
}

[HarmonyPatch(typeof(IntroCutscene._ShowRole_d__41), nameof(IntroCutscene._ShowRole_d__41.MoveNext))]
internal static class IntroCutsceneShowRoleMoveNextPatch
{
    private static void Postfix(IntroCutscene._ShowRole_d__41 __instance)
    {
        if (__instance?.__4__this != null)
            TempModPlugin.Runtime.ApplyRoleIntro(__instance.__4__this);
    }
}

[HarmonyPatch(typeof(TMP_Text), "set_text")]
internal static class TmpTextSetTextPatch
{
    private static void Prefix(TMP_Text __instance, ref string value)
    {
        TempModPlugin.Runtime.OverrideIntroText(__instance, ref value);
    }
}

[HarmonyPatch(typeof(GameData), nameof(GameData.OnMeetingStart))]
internal static class GameDataOnMeetingStartPatch
{
    private static void Postfix()
    {
        TempModPlugin.Runtime.OnMeetingStarted();
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.Update))]
internal static class MeetingHudUpdatePatch
{
    private static void Postfix(MeetingHud __instance)
    {
        MeetingAbilityHudPresenter.Refresh(__instance);
        MadGuesserMeetingPresenter.Refresh(__instance);
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.CmdCastVote))]
internal static class MeetingHudCmdCastVotePatch
{
    private static bool Prefix(byte playerId, byte suspectIdx)
    {
        // 会議能力を構えたローカルプレイヤーだけは、次に押した投票対象を能力対象としてホストへ送る。
        return !TempModPlugin.Runtime.TryConsumeMeetingAbilityVote(playerId, suspectIdx);
    }
}

[HarmonyPatch(typeof(MeetingHud), nameof(MeetingHud.RpcClose))]
internal static class MeetingHudRpcClosePatch
{
    private static void Postfix(MeetingHud __instance)
    {
        MadGuesserMeetingPresenter.CloseRoleList();
        TempModPlugin.Runtime.OnMeetingClosed(__instance);
    }
}

[HarmonyPatch(typeof(TaskPanelBehaviour), nameof(TaskPanelBehaviour.SetTaskText))]
internal static class TaskPanelBehaviourSetTaskTextPatch
{
    private static void Prefix(ref string str)
    {
        TempModPlugin.Runtime.AddRoleLineToTaskText(ref str);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.CheckMurder))]
internal static class PlayerControlCheckMurderPatch
{
    private static bool Prefix(PlayerControl __instance, PlayerControl target)
    {
        // trueを返した場合だけ本体のキル処理を抑止し、RoleEngineの確定結果を使用する。
        return !TempModPlugin.Runtime.TryInterceptMurder(__instance, target);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
internal static class PlayerControlHandleRpcPatch
{
    private static bool Prefix(PlayerControl __instance, byte callId, MessageReader reader)
    {
        return !TempModPlugin.Runtime.HandleCustomRpc(callId, reader, __instance);
    }
}

[HarmonyPatch(typeof(HudManager), nameof(HudManager.Update))]
internal static class HudManagerUpdatePatch
{
    private static void Postfix(HudManager __instance)
    {
        CustomAbilityHudPresenter.Refresh(__instance);
    }
}

[HarmonyPatch(typeof(PassiveButton), nameof(PassiveButton.ReceiveMouseOver))]
internal static class RoleDescriptionMouseOverPatch
{
    private static void Postfix(PassiveButton __instance)
    {
        RoleHoverHint.Show(__instance);
    }
}

[HarmonyPatch(typeof(PassiveButton), nameof(PassiveButton.ReceiveMouseOut))]
internal static class RoleDescriptionMouseOutPatch
{
    private static void Postfix(PassiveButton __instance)
    {
        RoleHoverHint.Restore(__instance);
    }
}

[HarmonyPatch(typeof(AbilityButton), nameof(AbilityButton.DoClick))]
internal static class AbilityButtonDoClickPatch
{
    private static bool Prefix(AbilityButton __instance)
    {
        // 会議専用能力、通常能力の順に標準ボタンを再利用する。
        if (MeetingAbilityHudPresenter.TryHandleClick(__instance))
            return false;
        return !CustomAbilityHudPresenter.TryHandleClick(__instance);
    }
}

[HarmonyPatch(typeof(PlayerPhysics), "get_SpeedMod")]
internal static class PlayerPhysicsGetSpeedModPatch
{
    private static void Postfix(PlayerPhysics __instance, ref float __result)
    {
        // TownOfUsのUpdateSpeedと同じく、通常の速度計算後に牽引中だけ倍率を掛ける。
        // Rigidbodyの速度や梯子状態を直接書き換えないため、本体の移動状態へ干渉しない。
        if (__instance?.myPlayer != null)
            __result *= TempModPlugin.Runtime.GetMovementSpeedMultiplier(__instance.myPlayer);
    }
}

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.FixedUpdate))]
internal static class PlayerControlFixedUpdatePatch
{
    private static void Postfix(PlayerControl __instance)
    {
        TempModPlugin.Runtime.OnPlayerTick(__instance);
        if (PlayerControl.LocalPlayer != null && __instance.PlayerId == PlayerControl.LocalPlayer.PlayerId)
            TempModPlugin.MatchRuntime.CheckAbandonHotkey();
    }
}

[HarmonyPatch(typeof(LogicGameFlowNormal), nameof(LogicGameFlowNormal.CheckEndCriteria))]
internal static class PreventGameEndPatch
{
    private static bool Prefix()
    {
        // 終了判定を抑止できるのはホストだけ。参加者のローカル設定は試合進行へ影響させない。
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            return true;
        return !TempModPlugin.MatchSettings.PreventGameEnd.Value;
    }
}

[HarmonyPatch(typeof(EndGameManager), nameof(EndGameManager.Start))]
internal static class EndGameManagerStartPatch
{
    private static void Postfix(EndGameManager __instance)
    {
        TempModPlugin.Runtime.ApplyEndGameResults(__instance);
    }
}

[HarmonyPatch(typeof(GameSettingMenu), "Start")]
internal static class GameSettingMenuStartPatch
{
    private static void Postfix(GameSettingMenu __instance)
    {
        TempModSettingsPanelPresenter.AddButton(__instance);
    }
}

[HarmonyPatch(typeof(GameSettingMenu), "Update")]
internal static class GameSettingMenuUpdatePatch
{
    private static void Postfix(GameSettingMenu __instance)
    {
        TempModSettingsPanelPresenter.RefreshButtonLabel(__instance);
    }
}
