using System;
using System.Collections.Generic;
using Newtonsoft.Json;


namespace Oxide.Plugins
{
    [Info("Repair Blocker", "Camoec", 1.1)]
    [Description("Prevents certain objects from being repaired")]

    public class RepairBlocker : RustPlugin
    {  
        private const string BypassPerm = "repairblocker.bypass";
        private class PluginConfig
        {
            [JsonProperty(PropertyName = "ChatPrefix")]
            public string ChatPrefix = "<color=#eb4213>Repair Blocker</color>:";
            [JsonProperty(PropertyName = "BlackList")]
            public List<string> BlackList = new List<string>();
        }

        private PluginConfig _config;

        protected override void SaveConfig() => Config.WriteObject(_config, true);
        protected override void LoadDefaultConfig()
        {
            //base.LoadDefaultConfig();
            _config = new PluginConfig();
            _config.BlackList.Add("rifle.lr300"); // rifle.lr300
            _config.BlackList.Add("rifle.ak"); // rifle.ak
            _config.BlackList.Add("repairbench_deployed"); // repair bench
            SaveConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                    throw new Exception();

                SaveConfig(); // override posible obsolet / outdated config
            }
            catch (Exception)
            {
                PrintError("Loaded default config.");

                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoRepair"] = "You are not allowed to repair that"
            }, this);
        }

        private string Lang(string key, string userid) => lang.GetMessage(key, this, userid);

        private void Init()
        {
            permission.RegisterPermission(BypassPerm, this);
        }
        object OnItemRepair(BasePlayer player, Item item)
        {
            if (_config.BlackList.Contains(item.info.shortname) && !permission.UserHasPermission(player.UserIDString, BypassPerm))
            {
                PrintToChat(player, $"{_config.ChatPrefix} {Lang("NoRepair", player.UserIDString)}");
                return false;
            }
            return null;
        }
        object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (_config.BlackList.Contains(info.HitEntity.ShortPrefabName) && !permission.UserHasPermission(player.UserIDString, BypassPerm))
            {
                PrintToChat(player, $"{_config.ChatPrefix} {Lang("NoRepair", player.UserIDString)}");
                return false;
            }
            return null;
        }
    }
}