using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Perma Death", "Kappasaurus", "1.0.1")]
    [Description("Deletes all of a player's entities after a set amount of deaths")]
    class PermaDeath : RustPlugin
    {
        private int maximumDeaths = 5;
        private bool includeSuicide = true;

        #region Data

        static PermaDeath Instance;

        class StoredData
        {
            public List<PlayerInfo> Players = new List<PlayerInfo>();

            public PlayerInfo GetInfo(BasePlayer player)
            {
                var playerInfo = Players.FirstOrDefault(x => x.SteamID == player.userID);
                if (playerInfo == null)
                {
                    playerInfo = new PlayerInfo(player);
                    Players.Add(playerInfo);
                    Instance.SaveData();
                }

                return playerInfo;
            }
        }

        StoredData storedData;

        class PlayerInfo
        {
            public ulong SteamID;
            public int Deaths;

            public PlayerInfo()
            {
            }

            public PlayerInfo(BasePlayer player)
            {
                SteamID = player.userID;
                Deaths = 0;
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject<StoredData>(this.Title, storedData);
        private void ReadData() => storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Title);

        #endregion

        #region Hooks

        private void Init()
        {
            ReadData();
            Instance = this;

            LoadConfig();
            permission.RegisterPermission("permadeath.admin", this);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (storedData.Players.Any(x => x.SteamID == player.userID)) return;

            var info = new PlayerInfo(player);
            storedData.Players.Add(info);
            SaveData();
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            var playerInfo = storedData.GetInfo(player);

            playerInfo.Deaths++;
            SaveData();

            if (playerInfo.Deaths >= maximumDeaths)
            {
                ConVar.Entity.DeleteBy(player.userID);
                while (playerInfo.Deaths > 0)
                {
                    playerInfo.Deaths--;
                    SaveData();
                }
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            var playerInfo = storedData.GetInfo(player);
            if (playerInfo.Deaths > 0)
                PrintToChat(lang.GetMessage("Warning", this, player.UserIDString), maximumDeaths - playerInfo.Deaths);
        }

        private void OnNewSave() => storedData.Players.Clear();

        #endregion

        #region Command

        [ChatCommand("permadeath")]
        private void PermadeathCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "permadeath.admin"))
            {
                PrintToChat(player, lang.GetMessage("No Permission", this, player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                PrintToChat(player, lang.GetMessage("No Arguments", this, player.UserIDString));
                return;
            }

            switch (args[0].ToLower())
            {
                case "reset":
                    storedData.Players.Clear();
                    PrintToChat(player, lang.GetMessage("Reset Message", this, player.UserIDString));
                    break;
            }
        }

        #endregion

        #region Config

        private new void LoadConfig()
        {
            GetConfig(ref maximumDeaths, "Maximum deaths");
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file...");

        #endregion

        #region Helpers

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
                return;

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "<size=12>Error, no permission.</size>",
                ["No Arguments"] = "<size=12>Error, no arguments.</size>",
                ["Reset Message"] = "<size=12>Reset all death data.</size>",
                ["Warning"] = "<size=12>Warning, you have <i>only</i> {0} lives left until all your entities reset!</size>",
            }, this);
        }

        #endregion
    }
}
