using BepInEx;
using BepInEx.Unity.IL2CPP;
using HarmonyLib;
using Hazel;
using TempMod.Plugin.Integration;
using TempMod.Plugin.UI;

namespace TempMod.Plugin;

/// <summary>
/// 役職再実装用のクリーン基盤。
/// ロゴ、1人開始、設定UIの入口、更新、試合制御、RPC予約範囲だけを維持します。
/// </summary>
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class TempModPlugin : BasePlugin
{
    public const string PluginGuid = "jp.tempmod.amongus";
    public const string PluginName = "tempMOD";
    internal const string PluginVersion = "0.3.0";

    internal static TempModPlugin Instance { get; private set; } = null!;
    internal static TempModRuntime Runtime { get; private set; } = null!;
    internal static MatchControlSettings MatchSettings { get; private set; } = null!;
    internal static MatchControlRuntime MatchRuntime { get; private set; } = null!;

    public override void Load()
    {
        Instance = this;
        Runtime = new TempModRuntime(Log);
        MatchSettings = new MatchControlSettings(Config);
        MatchRuntime = new MatchControlRuntime(Log);

        var harmony = new Harmony(PluginGuid);
        harmony.PatchAll(typeof(TempModPlugin).Assembly);
#if !TEMPMOD_ADMIN
        GitHubAutoUpdater.CheckInBackground(Log);
#endif
        Log.LogInfo($"{PluginName} {PluginVersion} を読み込みました。役職再実装用のクリーン基盤です。");
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
        TempModPlugin.Runtime.OnLobbyOrGameReset();
        TempModPlugin.Instance.Log.LogInfo("tempMOD: 最小開始人数を1人に設定しました。");
    }
}

[HarmonyPatch(typeof(GameStartManager), nameof(GameStartManager.Update))]
internal static class GameStartManagerUpdatePatch
{
    private static void Prefix(GameStartManager __instance)
    {
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

[HarmonyPatch(typeof(PlayerControl), nameof(PlayerControl.HandleRpc))]
internal static class PlayerControlHandleRpcPatch
{
    private static bool Prefix(PlayerControl __instance, byte callId, MessageReader reader)
    {
        return !TempModPlugin.Runtime.HandleCustomRpc(callId, reader, __instance);
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
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost)
            return true;
        return !TempModPlugin.MatchSettings.PreventGameEnd.Value;
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
