using HarmonyLib;
using Reactor.Utilities;
using System.Linq;
using TownOfUs.Patches.NeutralRoles;
using TownOfUs.Roles;

namespace TownOfUs.NeutralRoles.ExecutionerMod
{
    [HarmonyPatch(typeof(ExileController), nameof(ExileController.BeginForGameplay))]
    internal class MeetingExiledEnd
    {
        private static void Postfix(ExileController __instance)
        {
            var exiled = __instance.initData.networkedPlayer;
            if (exiled == null) return;
            var player = exiled.Object;

            foreach (var role in Role.GetRoles(RoleEnum.Executioner))
                if (player.PlayerId == ((Executioner)role).target.PlayerId)
                {
                    ((Executioner)role).Wins();

                    if (CustomGameOptions.ExecutionerWin != WinEndsGame.Kills) return;
                    if (PlayerControl.LocalPlayer != ((Executioner)role).Player) return;
                    role.PauseEndCrit = true;

                    bool IsExecutionerVoter(PlayerControl candidate) => MeetingHud.Instance.playerStates.Any(x => x.PlayerId == candidate.PlayerId && !Utils.PlayerById(x.PlayerId).Is(RoleEnum.Pestilence) && x.VotedForId == ((Executioner)role).target.PlayerId);
                    var pk = new PlayerMenu((x) => {
                        Utils.RpcMultiMurderPlayer(((Executioner)role).Player, x);
                        role.PauseEndCrit = false;
                    }, (y) => {
                        return IsExecutionerVoter(y);
                    });
                    Coroutines.Start(pk.Open(3f));
                }
                    
        }
    }
}