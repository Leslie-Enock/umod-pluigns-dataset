using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("PVP Stats", "Dana", "1.4.7")]
    class PVPStats : RustPlugin
    {
        #region Declaration

        static PVPStats ins;
        static Dictionary<ulong, PVPStatsData> cachedPlayerStats = new Dictionary<ulong, PVPStatsData>();
        private const string AdminPermission = "pvpstats.admin";
        #endregion

        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PlayerStatisticsMSG"] = "<size=13><color=#ffc300>PVP STATS</color></size>\n<size=12><color=#ffc300>{0}</color> Kills. <color=#ffc300>{1}</color> Deaths. <color=#ffc300>{2}</color> KD Ratio.</size>",
                ["PlayerStatisticsAdminMSG"] = "<size=13><color=#ffc300>PVP STATS</color></size>\n<size=12><color=#13ffa2>{0}</color> • {1}\n\n<color=#ffc300>{2}</color> Kills. <color=#ffc300>{3}</color> Deaths. <color=#ffc300>{4}</color> KD Ratio.</size>",
                ["PlayerStatisticsConsoleMSG"] = "{0} {1} Kills. {2} Deaths. {3} KD.",
                ["ConsoleWipeMSG"] = "{0} Players PVP Stats were wiped",
                ["ConsoleResetMSG"] = "{0} PVP Stats has been reset",
                ["ConsoleNotFoundMSG"] = "{0} not found",
                ["PlayerNotFound"] = "Player not found",
                ["NoPermission"] = "You don't have permission to use this command"
            }, this);
        }

        private void OnServerInitialized()
        {
            ins = this;
            permission.RegisterPermission(AdminPermission, this);
            foreach (BasePlayer player in BasePlayer.activePlayerList) OnPlayerInit(player);
        }

        private void OnPlayerInit(BasePlayer player) => PVPStatsData.TryLoad(player.userID);

        private void OnPlayerDeath(BasePlayer victim, HitInfo info)
        {
            BasePlayer killer = info?.Initiator as BasePlayer;

            if (killer == null || killer == victim) return;
            if (victim.IsNpc || killer.IsNpc) return;

            if (cachedPlayerStats.ContainsKey(killer.userID)) cachedPlayerStats[killer.userID].Kills++;
            if (cachedPlayerStats.ContainsKey(victim.userID)) cachedPlayerStats[victim.userID].Deaths++;

            return;
        }

        private void OnServerShutDown() => Unload();

        private void Unload()
        {
            foreach (var data in cachedPlayerStats) data.Value.Save(data.Key);
        }

        #endregion

        #region Commands

        [ConsoleCommand("stats.wipe")]
        private void WipeStatsCmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon) return;

            GetAllPlayers().ForEach(ID => PVPStatsData.Reset(ID));
            PrintWarning(string.Format(msg("ConsoleWipeMSG"), new object[] { GetAllPlayers().Count }));
        }

        [ConsoleCommand("stats.reset")]
        private void ResetStatsCmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon) return;
            if (!arg.HasArgs()) return;

            if (arg.Args.Count() != 1)
            {
                PrintWarning($"Usage : stats.reset <SteamID64>");
                return;
            }

            string ID = arg.Args[0];

            if (!ID.IsSteamId())
            {
                PrintWarning(string.Format(msg("ConsoleNotFoundMSG"), new object[] { ID }));
                return;
            }

            string Name = GetPlayer(ulong.Parse(ID));

            PrintWarning(string.Format(msg("ConsoleResetMSG"), new object[] { Name }));
        }

        [ConsoleCommand("stats")]
        private void ShowStatisticsCmd(ConsoleSystem.Arg arg)
        {
            if (!arg.IsRcon) return;
            if (!arg.HasArgs()) return;

            if (arg.Args.Count() != 1)
            {
                PrintWarning($"Usage : stats <SteamID64>");
                return;
            }

            var player = BasePlayer.FindAwakeOrSleeping(arg.Args[0]);
            if (player == null)
            {
                PrintWarning(msg("PlayerNotFound"));
            }
            if (!cachedPlayerStats.ContainsKey(player.userID))
                PVPStatsData.TryLoad(player.userID);
            PrintWarning(string.Format(msg("PlayerStatisticsConsoleMSG"), $"{player.displayName}[{player.userID}]", cachedPlayerStats[player.userID].Kills, cachedPlayerStats[player.userID].Deaths, cachedPlayerStats[player.userID].KDR));
        }

        [ChatCommand("stats")]
        private void cmdShowStatistics(BasePlayer player, string command, string[] args)
        {
            var targetPlayerId = player.userID;
            BasePlayer targetPlayer = null;
            if (args.Length > 0)
            {
                if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
                {
                    PlayerMsg(player, string.Format(msg("NoPermission", player.userID)));
                    return;
                }

                var nameOrId = args[0];
                targetPlayer = BasePlayer.FindAwakeOrSleeping(nameOrId);
                if (targetPlayer == null)
                {
                    PlayerMsg(player, string.Format(msg("PlayerNotFound", player.userID)));
                    return;
                }
                targetPlayerId = targetPlayer.userID;
            }
            if (!cachedPlayerStats.ContainsKey(targetPlayerId))
                PVPStatsData.TryLoad(targetPlayerId);
            if (targetPlayer != null)
            {
                PlayerMsg(player, string.Format(msg("PlayerStatisticsAdminMSG", player.userID), targetPlayer.displayName, targetPlayer.userID, cachedPlayerStats[targetPlayerId].Kills, cachedPlayerStats[targetPlayerId].Deaths, cachedPlayerStats[targetPlayerId].KDR));
            }
            else
            {
                PlayerMsg(player, string.Format(msg("PlayerStatisticsMSG", player.userID), new object[] { cachedPlayerStats[targetPlayerId].Kills, cachedPlayerStats[targetPlayerId].Deaths, cachedPlayerStats[targetPlayerId].KDR }));
            }
        }

        #endregion

        #region Methods

        public List<ulong> GetAllPlayers()
        {
            List<ulong> PlayersID = new List<ulong>();
            covalence.Players.All.ToList().ForEach(IPlayer => PlayersID.Add(ulong.Parse(IPlayer.Id)));
            return PlayersID;
        }

        public string GetPlayer(ulong id)
        {
            IPlayer player = covalence.Players.FindPlayerById(id.ToString());
            if (player == null) return string.Empty;
            return player.Name;
        }

        public void PlayerMsg(BasePlayer player, string msg) => SendReply(player, msg);

        #endregion

        #region Classes

        private class PVPStatsData
        {
            public int Kills = 0;
            public int Deaths = 0;
            public float KDR => Deaths == 0 ? Kills : (float)Math.Round(((float)Kills) / Deaths, 2);

            internal static void TryLoad(ulong id)
            {
                if (cachedPlayerStats.ContainsKey(id)) return;

                PVPStatsData data = Interface.Oxide.DataFileSystem.ReadObject<PVPStatsData>($"PVPStats/{id}");

                if (data == null) data = new PVPStatsData();

                cachedPlayerStats.Add(id, data);
            }

            internal static void Reset(ulong id)
            {
                PVPStatsData data = Interface.Oxide.DataFileSystem.ReadObject<PVPStatsData>($"PVPStats/{id}");

                if (data == null) return;

                data = new PVPStatsData();
                data.Save(id);
            }

            internal void Save(ulong id) => Interface.Oxide.DataFileSystem.WriteObject(($"PVPStats/{id}"), this, true);
        }

        #endregion

        #region Localization

        public string msg(string key, ulong playerId = 0U) => lang.GetMessage(key, this, playerId != 0U ? playerId.ToString() : null);

        #endregion
    }
}