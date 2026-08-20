using AmongUs.GameOptions;

namespace TownOfUs.Extensions
{
    /// <summary>
    /// Small compatibility surface for APIs whose generated IL2CPP collection
    /// types changed after TOU-R's upstream game version.  It deliberately
    /// avoids LINQ on Il2CppSystem.Collections.Generic.List.
    /// </summary>
    internal static class CurrentGameCompatibility
    {
        internal static RoleBehaviour FindRoleBehaviour(RoleTypes roleType)
        {
            var manager = RoleManager.Instance;
            if (manager == null || manager.AllRoles == null) return null;

            var roles = manager.AllRoles;
            for (var index = 0; index < roles.Count; index++)
            {
                var candidate = roles[index];
                if (candidate != null && candidate.Role == roleType) return candidate;
            }

            return null;
        }
    }
}
