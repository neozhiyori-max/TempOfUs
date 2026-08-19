using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace TempMod.Plugin.Integration;

/// <summary>
/// 公開版・管理者検証版に共通の試合制御設定。
/// プレイヤー、描画オブジェクト、ネットワーク参加者を生成するテストダミー機能は含めない。
/// </summary>
internal sealed class MatchControlSettings
{
    private readonly ConfigFile _config;

    internal ConfigEntry<bool> PreventGameEnd { get; }

    internal MatchControlSettings(ConfigFile config)
    {
        _config = config;
        PreventGameEnd = config.Bind(
            "ゲーム制御",
            "ゲームを終了しない",
            false,
            "有効時、通常の勝利条件によるゲーム終了を抑止します。ホストがロビー設定から変更できます。");
    }

    internal void SetPreventGameEnd(bool enabled)
    {
        PreventGameEnd.Value = enabled;
        _config.Save();
    }
}

/// <summary>
/// 公開版・管理者検証版で共有する廃村ホットキーを処理する。
/// </summary>
internal sealed class MatchControlRuntime
{
    private readonly ManualLogSource _log;

    internal MatchControlRuntime(ManualLogSource log)
    {
        _log = log;
    }

    internal void CheckAbandonHotkey()
    {
        // ホストだけが試合を終了できる。ロビーや終了画面では何もしない。
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || GameManager.Instance == null)
            return;

        var shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (shift && Input.GetKey(KeyCode.L) && Input.GetKeyDown(KeyCode.Return))
        {
            _log.LogWarning("tempMODホットキーで試合を廃村します。");
            GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
        }
    }
}
