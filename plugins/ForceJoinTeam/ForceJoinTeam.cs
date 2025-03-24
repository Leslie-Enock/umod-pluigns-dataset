using System;
using System.Linq; 
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Force Join Team", "Akkariin", "1.0.2")]
    [Description("Force join a team")]
    public class ForceJoinTeam : RustPlugin
    {
        #region Variables
        private const string Perm = "forcejointeam.use";
        #endregion

        #region Hooks
        private void Init()
        {
            permission.RegisterPermission(Perm, this);
        }
		
        // Command for force joining a team
		[ChatCommand("forcejoin")]
        private void ForceJoinTeamCommand(BasePlayer player, string command, string[] args)
        {
			if (!permission.UserHasPermission(player.UserIDString, Perm) && !player.IsAdmin)
            {
                SendReply(player, FormatMessage("NoPermissionMsg", player));
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, FormatMessage("InvalidArgsMsg", player));
                return;
            }

            BasePlayer target = FindPlayerByName(args[0]);

            if (target == null)
            {
                SendReply(player, FormatMessage("PlayerNotFoundMsg", player));
                return;
            }

            if (target.currentTeam == 0)
            {
                SendReply(player, FormatMessage("PlayerNotInTeamMsg", player));
                return;
            }

            if (player.currentTeam == target.currentTeam)
            {
                SendReply(player, FormatMessage("AlreadyInTeamMsg", player));
                return;
            }

            // player.currentTeam = target.currentTeam;
            var team = RelationshipManager.ServerInstance.FindTeam(target.currentTeam);
            if (team == null)
            {
                SendReply(player, FormatMessage("PlayerNotInTeamMsg", player));
                return;
            }
            team.AddPlayer(player);
            SendReply(player, string.Format(FormatMessage("JoinSuccessfulMsg", player), target.displayName));
		}
        #endregion

        #region Helpers
        private BasePlayer FindPlayerByName(string keyword)
        {
            foreach (var player in BasePlayer.allPlayerList)
            {
                if (player == null || string.IsNullOrEmpty(player.displayName))
                {
                    continue;
                }

                if (player.UserIDString == keyword || player.displayName.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return player;
                }
            }
            return null;
        }

        private String FormatMessage(string message, BasePlayer player)
        {
            String prefix = lang.GetMessage("MessagePrefix", this, player.UserIDString);
            return String.Format("{0} {1}", prefix, lang.GetMessage(message, this, player.UserIDString));
        }
        #endregion

        #region Language

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string,string>
            {
                {"MessagePrefix", "<color=#00FFFF>[ForceJoinTeam]</color>"},
                {"JoinSuccessfulMsg", "You have successfully joined the team of player {0}"},
                {"PlayerNotInTeamMsg", "This player does not have a team"},
                {"AlreadyInTeamMsg", "You are already in the same team as the player"},
                {"PlayerNotFoundMsg", "Cannot find this player, please check the name"},
                {"NoPermissionMsg", "You have no permission to use this command."},
                {"InvalidArgsMsg", "Invalid args, using /forcejoin <Name | SteamID>"}
            }, this);
        }
        #endregion
    }
}
