#if TEMPMOD_ADMIN
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace TempMod.Plugin.Integration;

/// <summary>
/// 管理者検証版だけに含まれる安全な運用機能。
/// プレイヤー、描画オブジェクト、ネットワーク参加者を生成するテストダミー機能は含めない。
/// </summary>
internal sealed class AdminTestSettings
{
    private readonly ConfigFile _config;

    internal ConfigEntry<bool> PreventGameEnd { get; }

    internal AdminTestSettings(ConfigFile config)
    {
        _config = config;
        PreventGameEnd = config.Bind("管理者検証", "ゲームを終了しない", false, "有効時、通常の勝利条件によるゲーム終了を抑止します。管理者検証版専用です。");
    }

    internal void SetPreventGameEnd(bool enabled)
    {
        PreventGameEnd.Value = enabled;
        _config.Save();
    }
}

internal sealed class AdminTestRuntime
{
    private readonly ManualLogSource _log;

    internal AdminTestRuntime(ManualLogSource log, AdminTestSettings settings)
    {
        _log = log;
    }

    internal void CheckAbandonHotkey()
    {
        if (AmongUsClient.Instance == null || !AmongUsClient.Instance.AmHost || GameManager.Instance == null)
            return;

        var shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (shift && Input.GetKey(KeyCode.L) && Input.GetKeyDown(KeyCode.Return))
        {
            _log.LogWarning("管理者ホットキーでゲームを廃村します。");
            GameManager.Instance.RpcEndGame(GameOverReason.ImpostorDisconnect, false);
        }
    }
}
#endif
