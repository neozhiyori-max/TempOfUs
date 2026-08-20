using BepInEx.Logging;
using Hazel;
using UnityEngine;

namespace TempMod.Plugin.Integration;

/// <summary>
/// tempMOD専用RPCの共通ヘッダーです。
/// 役職再実装後に新しい同期メッセージを追加するまで、この版はメッセージを送信しません。
/// </summary>
internal static class TempModRpcProtocol
{
    internal const byte Magic = 0x54; // 'T'
    internal const byte Version = 2;

    internal static void WriteHeader(MessageWriter writer)
    {
        writer.Write(Magic);
        writer.Write(Version);
    }

    internal static bool TryReadHeader(MessageReader reader)
    {
        if (reader.BytesRemaining < 2)
            return false;
        return reader.ReadByte() == Magic && reader.ReadByte() == Version;
    }
}

/// <summary>
/// 役職再実装前の最小ゲーム連携層です。
/// この世代では役職割当、能力の横取り、カスタム勝利、結果改変、プレイヤー複製を一切行いません。
/// </summary>
internal sealed class TempModRuntime
{
    private readonly ManualLogSource _log;
    private bool _gameStartedLogged;

    internal TempModRuntime(ManualLogSource log)
    {
        _log = log;
    }

    internal void OnGameStarted()
    {
        if (_gameStartedLogged)
            return;

        _gameStartedLogged = true;
        _log.LogInfo("tempMOD: 役職再実装用のクリーン基盤として試合を開始しました。カスタム役職は未導入です。");
    }

    internal void OnLobbyOrGameReset()
    {
        _gameStartedLogged = false;
    }

    /// <summary>
    /// 旧バージョンの役職RPCを含め、250～252番のtempMOD予約範囲だけを安全に破棄します。
    /// ヘッダー不一致のRPCは読取位置を復元して本体・他MODへ渡します。
    /// </summary>
    internal bool HandleCustomRpc(byte callId, MessageReader reader, PlayerControl source)
    {
        if (callId is not 250 and not 251 and not 252)
            return false;

        var originalPosition = reader.Position;
        if (!TempModRpcProtocol.TryReadHeader(reader))
        {
            reader.Position = originalPosition;
            return false;
        }

        _log.LogDebug($"tempMOD: 役職再実装前の予約RPCを破棄しました。callId={callId}, source={source?.PlayerId}");
        return true;
    }

    internal void OnPlayerTick(PlayerControl player)
    {
        // 将来の役職状態更新用のフックです。現時点では本体の移動・キル・会議状態を変更しません。
    }
}
