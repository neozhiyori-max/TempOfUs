namespace TempMod.Core;

/// <summary>
/// 役職再実装の開始点を表す、ゲーム非依存の最小コアです。
/// この版にはカスタム役職、能力、役職抽選、勝利条件を含めません。
/// </summary>
public static class RoleRebuildFoundation
{
    /// <summary>カスタム役職が未導入であることを示します。</summary>
    public const bool HasCustomRoles = false;

    /// <summary>
    /// 新しい役職を追加する際は、この世代の基盤へ役職ID・状態・受入テストを
    /// 役職単位で追加します。過去の役職IDの互換目的での再利用はしません。
    /// </summary>
    public const string Generation = "role-rebuild-1";
}
