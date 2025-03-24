using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Unique Names", "Wulf/lukespragg", "1.0.3")]
    [Description("Automatic renames and/or kicks players with non-unique/duplicate names")]
    class UniqueNames : CovalencePlugin
    {
        #region Initialization

        private const string permBypass = "uniquenames.bypass";

        private void Init()
        {
            permission.RegisterPermission(permBypass, this);
        }

        #endregion Initialization

        #region Configuration

        private Configuration config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Kick player with non-unique name")]
            public bool KickPlayer { get; set; } = true;

            [JsonProperty(PropertyName = "Rename player with non-unqiue name")]
            public bool RenamePlayer { get; set; } = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }
            }
            catch
            {
                string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}";
                LogWarning($"Could not load a valid configuration file, creating a new configuration file at {configPath}.json");
                Config.WriteObject(config, false, $"{configPath}_invalid.json");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["KickReason"] = "The name {0} is already in use",
                ["Renamed"] = "{0} already in use, you have been renamed to {1}"
            }, this);
        }

        private string Lang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }

        #endregion Localization

        #region Name Handling

        private void OnUserConnected(IPlayer player)
        {
            if (!permission.UserHasPermission(player.Id, permBypass))
            {
                List<IPlayer> duplicates = players.Connected.ToList().Where(p => p.Name == player.Name).ToList();
                if (duplicates.Count > 1)
                {
                    if (config.RenamePlayer)
                    {
                        string newName = player.Name + duplicates.Count;
                        player.Message(Lang("Renamed", player.Id, player.Name, newName));
                        player.Rename(newName);
                    }
                    if (config.KickPlayer)
                    {
                        player.Kick(Lang("KickReason", player.Id, player.Name));
                    }
                }
            }
        }

        #endregion Name Handling
    }
}
