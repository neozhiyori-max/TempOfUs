using System;
using System.Collections.Generic;
using SuperNewRoles.Roles;

namespace SuperNewRoles;

/// <summary>
/// tempMODで段階的に有効化するSNR由来役職を定義します。
/// 役職を5件単位で追加し、未検証のSNR役職・修飾子・ゴースト役職は登録しません。
/// </summary>
internal static class TempModRoleScope
{
    private static readonly HashSet<string> EnabledRoleTypeNames = new(StringComparer.Ordinal)
    {
        "SuperNewRoles.Roles.Impostor.Kunoichi",        // tempMOD表示: ニンジャ
        "SuperNewRoles.Roles.Impostor.Mafia",           // tempMOD表示: マフィア
        "SuperNewRoles.Roles.Impostor.RemoteController",// tempMOD表示: パペッティア
        "SuperNewRoles.Roles.Impostor.EvilGuesser",     // tempMOD表示: マッドゲッサー
        "SuperNewRoles.Roles.Impostor.Jammer",          // tempMOD表示: ブラックアウト
    };

    internal static IReadOnlyCollection<string> FirstImpostorWave => EnabledRoleTypeNames;

    internal static bool IsEnabledRoleType(Type type)
        => EnabledRoleTypeNames.Contains(type.FullName ?? string.Empty);

    internal static bool IsRoleLikeType(Type type)
        => typeof(IRoleBase).IsAssignableFrom(type)
           || typeof(IModifierBase).IsAssignableFrom(type)
           || typeof(IGhostRoleBase).IsAssignableFrom(type);

    internal static bool ShouldDiscoverOptionsFrom(Type type)
        => !IsRoleLikeType(type) || IsEnabledRoleType(type);
}
