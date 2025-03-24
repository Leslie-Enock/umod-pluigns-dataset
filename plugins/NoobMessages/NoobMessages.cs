using System;
using System.Collections.Generic;
using Oxide.Core;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("NoobMessages", "FastBurst", "2.0.3")]
    [Description("Displays a message when a player joins the server for the first time")]
    class NoobMessages : RustPlugin
    {
        #region Vars
        List<ulong> allplayers = new();
        List<BasePlayer> unAwake = new();
        private static NoobMessages ins { get; set; }
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            ins = this;
        }

        private void Loaded()
        {
            ins = this;
            lang.RegisterMessages(Messages, this);
            LoadData();

            foreach (var player in BasePlayer.activePlayerList)
                if (!allplayers.Contains(player.userID))
                    allplayers.Add(player.userID);
        }

        private void OnServerSave() => SaveData();

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!allplayers.Contains(player.userID))
            {
                allplayers.Add(player.userID);

                if (configData.General.Announce)
                    SendChatMessage("AnnounceAll", player.displayName);

                if (configData.General.Welcome)
                    unAwake.Add(player);
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (unAwake.Contains(player))
            {
                SendMessage(player, string.Format(msg("WelcomePlayer"), player.displayName));
                unAwake.Remove(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (unAwake.Contains(player))
                unAwake.Remove(player);
            SaveData();
        }

        private void Unload()
        {
            unAwake.Clear();
            SaveData();
        }
        #endregion

        #region Configuration
        private static ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "General Settings")]
            public GeneralOptions General { get; set; }

            public class GeneralOptions
            {
                [JsonProperty(PropertyName = "Announce to all players")]
                public bool Announce { get; set; }
                [JsonProperty(PropertyName = "Display a welcome message for new player")]
                public bool Welcome { get; set; }
                [JsonProperty(PropertyName = "Enable Chat Prefix")]
                public bool enablePrefix { get; set; }
                [JsonProperty(PropertyName = "Chat Prefix")]
                public string Prefix { get; set; }
                [JsonProperty(PropertyName = "Chat Icon (example 7656110000000000)")]
                public ulong ChatIcon { get; set; }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                General = new ConfigData.GeneralOptions
                {
                    Announce = true,
                    Welcome = true,
                    enablePrefix = true,
                    Prefix = "[Welcome Announcer]",
                    ChatIcon = 0
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new Core.VersionNumber(2, 0, 0))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
        #endregion

        #region Data Management
        private void SaveData() => Interface.Oxide.DataFileSystem.GetFile(this.Name).WriteObject(allplayers);

        private void LoadData()
        {
            try
            {
                allplayers = Interface.Oxide.DataFileSystem.GetFile(this.Name).ReadObject<List<ulong>>();
            }
            catch
            {
                allplayers = new List<ulong>();
            }
        }
        #endregion

        #region Localization
        private void SendMessage(BasePlayer player, string key, params object[] args)
        {
            string prefix;
            if (configData.General.enablePrefix) prefix = configData.General.Prefix;
            else prefix = null;

            Player.Message(player, (args != null ? string.Format(msg(key, player.UserIDString), args) : msg(key, player.UserIDString)), prefix, configData.General.ChatIcon);
        }
        private void SendChatMessage(string key, params object[] args)
        {
            string prefix;
            if (configData.General.enablePrefix) prefix = configData.General.Prefix;
            else prefix = null;

            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];
                Player.Message(player, (args != null ? string.Format(msg(key, player.UserIDString), args) : msg(key, player.UserIDString)), prefix, configData.General.ChatIcon);
            }
        }

        private static string msg(string key, string playerId = null) => ins.lang.GetMessage(key, ins, playerId);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["AnnounceAll"] = "<size=13><color=green>{0} is a new player, be helpful and try not to KOS!</color></size>",
            ["WelcomePlayer"] = "<color=green>Welcome to the server, {0}! Have a good time!</color>"
        };
        #endregion
    }
}