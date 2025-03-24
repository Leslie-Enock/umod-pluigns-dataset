using Rust;
using System;
using Network;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Extended Disconnect Info", "Strrobez", "2.0.2")]
    [Description("Extends the disconnect reasons with more information upon reconnecting.")]
    public class ExtendedDisconnectInfo : RustPlugin
    {
        #region Declaration
        private readonly Dictionary<ulong, string> CachedPlayers = new Dictionary<ulong, string>();
        private int CachedServerProtocol;
        #endregion

        #region Config
        private static Configuration _config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Log All Disconnect Messages")]
            public bool LogAllDisconnects;
            [JsonProperty(PropertyName = "Show Ban Reason on Reconnect? (if false, shows message in language file)")]
            public bool ShowBanReason;
            [JsonProperty(PropertyName = "Cache Removal Interval")]
            public float CacheRemovalInterval;
            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    LogAllDisconnects = true,
                    ShowBanReason = false,
                    CacheRemovalInterval = 30f
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();

                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion

        #region Hooks
        private void Init()
        {
            CachedServerProtocol = Protocol.network;
            CachedPlayers.Clear();
        }

        private object CanClientLogin(Connection connection)
        {
            if (connection.protocol > CachedServerProtocol)
                return lang.GetMessage("Server Wrong Version", this);

            if (connection.protocol < CachedServerProtocol)
                return lang.GetMessage("Client Wrong Version", this);

            timer.Once(_config.CacheRemovalInterval, () => CachedPlayers.Remove(connection.userid));

            return CachedPlayers.ContainsKey(connection.userid)
                ? CachedPlayers[connection.userid]
                : null;
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (_config.LogAllDisconnects)
                Puts($"{1} {0} was disconnected from the server. Reason: {2}", player.displayName,
                    player.UserIDString, reason);

            if (reason.Contains("World File Mismatch"))
                CachedPlayers.Add(player.userID, lang.GetMessage("World File Mismatch", this));
        }

        private void OnPlayerBanned(ulong userID, string reason)
        {
            if (!_config.ShowBanReason)
                CachedPlayers.Add(userID, lang.GetMessage("Banned", this));
        }

        #endregion

        #region Language
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["World Map Mismatch"] = "Delete all files in C:\\Program Files (x86)\\Steam\\steamapps\\common\\Rust\\maps and reconnect.",
                ["Server Wrong Version"] = "Server has not yet been updated to the latest Rust server update. Contact the Admins.",
                ["Client Wrong Version"] = "Please update your Rust client to the latest version.",
                ["Banned"] = "Banned: Appeal @ noobgaming.com"
            }, this);
        }
        #endregion
    }
}