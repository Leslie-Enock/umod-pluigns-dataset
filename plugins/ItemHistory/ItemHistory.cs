using System;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Item History", "birthdates", "1.2.1")]
    [Description("Keep history of an item")]
    public class ItemHistory : RustPlugin
    {
        #region Variables
        private const string Permission = "ItemHistory.use";
        #endregion

        #region Hooks
        private void Init() => permission.RegisterPermission(Permission, this);

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (item == null || _config.Blacklist?.Contains(item.info.shortname) == true) return;
            var player = item.GetOwnerPlayer() ?? container.GetOwnerPlayer();
            if (player == null || !permission.UserHasPermission(player.UserIDString, Permission) || string.IsNullOrEmpty(item.info.displayName.english) || player.inventory.FindContainer(container.uid) == null) return;
            item.name = player.displayName + "'" + (player.displayName.EndsWith("s", StringComparison.InvariantCultureIgnoreCase) ? "" : "s") + " " + item.info.displayName.english;
        }

        private ConfigFile _config;

        public class ConfigFile
        {
            [JsonProperty("Blacklisted Items (Won't get any history)")]
            public List<string> Blacklist { get; set; }
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    Blacklist = new List<string>
                    {
                        "shotgun.spas12"
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null)
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion
    }
}