using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Player Rankings", "Ankawi", "3.0.3")]
    [Description("Gives players ranks based on playtime on a server")]
    class PlayerRankings : RustPlugin
    {
        [PluginReference]
        Plugin ConnectionDB, PlaytimeTracker, BetterChat;

        #region Plugin Related

        void OnServerInitialized()
        {
            if (!BetterChat)
                PrintWarning("Better Chat required in order to grant group titles; http://umod.org/plugins/better-chat/");

            SaveConfig();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                UpdateGroups(player);

            timer.Every(15f, () =>
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                    UpdateGroups(player);
            });

            foreach (var rank in Config)
            {
                if (rank.Key == "Settings")
                    continue;

                if (Config[rank.Key, "Oxide Group"] == null)
                {
                    PrintWarning(rank.Key + " does not have an Oxide Group specified");
                    continue;
                }

                if (!GroupExists(Config[rank.Key, "Oxide Group"].ToString()))
                    CreateGroup(Config[rank.Key, "Oxide Group"].ToString());
            }
        }

        #endregion

        #region Helpers

        bool IsUserInGroup(BasePlayer player, string group)
        {
            return permission.UserHasGroup(player.UserIDString, group);
        }

        void AddUserToGroup(BasePlayer player, string group) => permission.AddUserGroup(player.UserIDString, group);

        void RemoveUserFromGroup(BasePlayer player, string group) => permission.RemoveUserGroup(player.UserIDString, group);

        void CreateGroup(string group) => permission.CreateGroup(group, string.Empty, 0);

        bool GroupExists(string group) => permission.GroupExists(group);

        #endregion

        #region Configuration

        new void LoadConfig()
        {
            SetConfig("A", "Oxide Group", "Regular");
            SetConfig("A", "Playtime", 10D);

            SetConfig("B", "Oxide Group", "Pro");
            SetConfig("B", "Playtime", 25D);

            SetConfig("Settings", "Ignore Admins", false);

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            LoadConfig();
            PrintWarning("Creating a new configuration file...");
        }

        ////////////////////////////////////////
        ///  Config Setup - by LaserHydra
        ////////////////////////////////////////

        void SetConfig(params object[] args)
        {
            List<string> stringArgs = (from arg in args select arg.ToString()).ToList<string>();
            stringArgs.RemoveAt(args.Length - 1);
            if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args);
        }

        #endregion

        #region Commands

        [ChatCommand("ranks")]
        private void RanksCommand(BasePlayer player, string command, string[] args)
        {
            foreach (var rank in Config)
            {
                if (rank.Key == "Settings")
                    continue;
                PrintToChat(player, $"<color=#ff0000>Rank</color>: " + Convert.ToString(Config[rank.Key, "Oxide Group"]) + "\n<color=#00FF00>Playtime Required:</color> " + Convert.ToString(Config[rank.Key, "Playtime"]) + " hours");
            }
            PrintToChat(player, $"<color=#ff0000>Your Playtime</color>: " + Math.Round(GetPlayTime(player), 2) + " hours");
        }

        #endregion

        #region Subject Related

        void UpdateGroups(BasePlayer player)
        {
            if (player.net.connection.authLevel != 0 && (bool)Config["Settings", "Ignore Admins"]) return;

            double playTime = GetPlayTime(player);

            Dictionary<string, object> newRank = new Dictionary<string, object>{
                {"Oxide Group", ""},
                {"Playtime", 0.0},
                {"Name", "none"}
            };

            foreach (KeyValuePair<string, object> rank in Config)
            {
                if (rank.Key == "Settings")
                    continue;

                double time = Convert.ToDouble(Config[rank.Key, "Playtime"]);

                if (playTime >= time && time > Convert.ToDouble(newRank["Playtime"]))
                {
                    newRank = rank.Value as Dictionary<string, object>;
                    newRank["Name"] = rank.Key;
                }
            }

            if (!IsUserInGroup(player, (string)newRank["Oxide Group"]) && GroupExists((string)newRank["Oxide Group"]))
            {
                SendReply(player, $"<color=#ff0000>PlayerRankings</color>: You have been ranked up to {newRank["Oxide Group"] as string}");
                Puts($"{player.displayName} has been ranked up to {newRank["Oxide Group"] as string}");

                AddUserToGroup(player, (string)newRank["Oxide Group"]);

                RevokeLower(player, Convert.ToDouble(newRank["Playtime"]));
            }
        }

        void RevokeLower(BasePlayer player, double time)
        {
            foreach (var rank in Config)
            {
                if (rank.Key == "Settings")
                    continue;
                if (time > Convert.ToDouble(Config[rank.Key, "Playtime"]) && IsUserInGroup(player, (string)Config[rank.Key, "Oxide Group"]))
                    RemoveUserFromGroup(player, (string)Config[rank.Key, "Oxide Group"]);
            }
        }

        double GetPlayTime(BasePlayer player)
        {
            double playTime;
            if (PlaytimeTracker)
            {
                playTime = Convert.ToDouble(PlaytimeTracker.Call("GetPlayTime", player.UserIDString)) / 60 / 60;
                return playTime;
            }
            if (ConnectionDB)
            {
                playTime = ConnectionDB.Call<uint>("API_GetSecondsPlayed", player.userID) / 60 / 60;
                return playTime;
            }
            Puts("There is no plugin tracking the playtime for players");
            return 0f;
        }

        #endregion
    }
}