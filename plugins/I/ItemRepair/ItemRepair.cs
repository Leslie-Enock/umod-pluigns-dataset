using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Item Repair", "birthdates", "1.1.0")]
    [Description("Repair your active item to full health")]
    public class ItemRepair : RustPlugin
    {
        private const string UsePermission = "itemrepair.use";

        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
        }

        [ChatCommand("repair")]
        private void ChatCommand(BasePlayer player)
        {
            if (!player.IPlayer.HasPermission(UsePermission))
            {
                player.ChatMessage(lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }
            var item = player.GetActiveItem();
            if (item == null)
            {
                player.ChatMessage(lang.GetMessage("NoActiveItem", this, player.UserIDString));
                return;
            }

            if (_config.Blacklist.Contains(item.info.shortname))
            {
                player.ChatMessage(lang.GetMessage("BlacklistedItem", this, player.UserIDString));
                return;
            }
            
            item.condition = player.GetActiveItem().maxCondition;
            player.ChatMessage(lang.GetMessage("Repaired", this, player.UserIDString));
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NoPermission", "You don't have permission to execute this command."},
                {"NoActiveItem", "You don't have an item in your hand."},
                {"Repaired", "You have repaired your active item."},
                {"BlacklistedItem", "You cannot repair this item!"}
            }, this);
        }
        
        private ConfigFile _config;

        public class ConfigFile
        {
            [JsonProperty("Blacklisted Items (shortnames)")] public IList<string> Blacklist;
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    Blacklist = new List<string> {"rock"}
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
    }
}