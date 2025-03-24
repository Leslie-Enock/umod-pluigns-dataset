using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Team Info", "Lorddy", "0.1.7")]
    [Description("Get Team Info")]
    public class TeamInfo : RustPlugin
    {
        #region Fields

        private const string PERMISSION_USE = "teaminfo.use";

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CmdHelp1"] = "<color=orange>/teaminfo <name|ID></color> - Show team info",
                ["PlayerNotFound"] = "Player Not Found",
                ["TeamNotFound"] = "Player {0} has no team",
                ["NoPerm"] = "You don't have permission to use this command",
                ["Result1"] = "<color=orange>TeamID:</color> {0}",
                ["Result2"] = "\n<color=orange>Leader:</color> {0}",
                ["Result3"] = "\n<color=orange>Name:</color> {0}",
                ["Result4"] = "\n<color=orange>Start Time:</color> {0}",
                ["Result5"] = "\n<color=orange>Life Time:</color> {0}",
                ["Result6"] = "\n<color=orange>Teammates:</color> {0}",
            }, this);
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PERMISSION_USE, this);
        }

        #endregion

        #region Helpers

        private string FormatSeconds(double seconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds((float) seconds);
            int days = timeSpan.Days;
            int hours = timeSpan.Hours + (days * 24);
            int mins = timeSpan.Minutes;
            int secs = timeSpan.Seconds;

            if (hours > 0)
                return $"{hours:00}:{mins:00}:{secs:00}s";

            if (mins > 0)
                return $"{mins:00}:{secs:00}s";

            return $"{secs:00}s";
        }

        #endregion

        #region Commands

        [ChatCommand("teaminfo")]
        private void TeamInfoCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_USE))
            {
                PrintToChat(player, lang.GetMessage("NoPerm", this, player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                PrintToChat(player, lang.GetMessage("CmdHelp1", this, player.UserIDString));
                return;
            }

            BasePlayer target = BasePlayer.FindAwakeOrSleeping(args[0]);

            if (target == null)
            {
                PrintToChat(player, lang.GetMessage("PlayerNotFound", this, player.UserIDString));
                return;
            }

            RelationshipManager.PlayerTeam targetTeam = target.Team;

            if (targetTeam == null)
            {
                PrintToChat(player, lang.GetMessage("TeamNotFound", this, player.UserIDString), target.displayName);
                return;
            }

            string result = string.Empty;
            string teamMembers = string.Join(", ", targetTeam.members.Select(teamMember => covalence.Players.FindPlayerById(teamMember.ToString())?.Name ?? "NA"));
            result += string.Format(lang.GetMessage("Result1", this, player.UserIDString), targetTeam.teamID);
            result += string.Format(lang.GetMessage("Result2", this, player.UserIDString), targetTeam.GetLeader()?.displayName ?? "NA");
            result += string.Format(lang.GetMessage("Result3", this, player.UserIDString), targetTeam.teamName ?? "NA");
            result += string.Format(lang.GetMessage("Result4", this, player.UserIDString), DateTime.Now.AddSeconds(-targetTeam.teamLifetime));
            result += string.Format(lang.GetMessage("Result5", this, player.UserIDString), FormatSeconds(targetTeam.teamLifetime));
            result += string.Format(lang.GetMessage("Result6", this, player.UserIDString), teamMembers);

            PrintToChat(player, result);
        }

        #endregion
    }
}