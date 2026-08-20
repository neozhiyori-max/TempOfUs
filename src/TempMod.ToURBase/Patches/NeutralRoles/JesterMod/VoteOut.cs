using HarmonyLib;
using Reactor.Utilities;
using System.Linq;
using TownOfUs.NeutralRoles.ExecutionerMod;
using TownOfUs.Patches.NeutralRoles;
using TownOfUs.Roles;

namespace TownOfUs.NeutralRoles.JesterMod
{
    [HarmonyPatch(typeof(ExileController), nameof(ExileController.BeginForGameplay))]
    internal class MeetingExiledEnd
    {
        private static void Postfix(ExileController __instance)
        {
            var exiled = __instance.initData.networkedPlayer;
            if (exiled == null) return;
            var player = exiled.Object;

            var role = Role.GetRole(player);
            if (role == null) return;
            if (role.RoleType == RoleEnum.Jester)
            {
                ((Jester)role).Wins();

                if (CustomGameOptions.JesterWin != WinEndsGame.Kills) return;
                if (PlayerControl.LocalPlayer != player) return;
                role.PauseEndCrit = true;

                bool IsJesterVoter(PlayerControl candidate) => MeetingHud.Instance.playerStates.Any(x => x.PlayerId == candidate.PlayerId && !Utils.PlayerById(x.PlayerId).Is(RoleEnum.Pestilence) && x.VotedForId == player.PlayerId);
                var pk = new PlayerMenu((x) =>
                {
                    Utils.RpcMultiMurderPlayer(player, x);
                    role.PauseEndCrit = false;
                }, (y) =>
                {
                    return IsJesterVoter(y);
                });
                Coroutines.Start(pk.Open(3f));
            }
        }
    }
}