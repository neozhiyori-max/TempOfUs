using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace TempMod.NosAddon;

/// <summary>
/// 公式Nebula on the Ship本体と同居するための最小tempMODアドオン。
/// 役職、設定UI、Harmony、RPC、外部通信、プレイヤー生成には一切触れない。
/// </summary>
[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
[BepInProcess("Among Us.exe")]
public sealed class TempModNosAddonPlugin : BasePlugin
{
    public const string PluginGuid = "jp.neozhiyori.tempmod.nosaddon";
    public const string PluginName = "tempMOD NOS Addon";
    public const string PluginVersion = "0.1.0";

    internal static ManualLogSource? AddonLog { get; private set; }

    public override void Load()
    {
        AddonLog = base.Log;
        AddonLog.LogInfo("tempMOD NOS Addon 0.1.0 を読み込みました。");
        AddonLog.LogInfo("最小検証モード: 役職・設定UI・Harmony・RPC・外部通信は未有効です。");
    }
}
