using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Repair Delay Modifier", "Ryz0r", "1.0.1")]
    [Description("Takes away the repair delay, or increases/decreases it for players with permission.")]
    public class RepairDelayModifier : RustPlugin
    {
        private Configuration _config;
        private const string NoDelayPerm = "norepairdelay.perm";
        private const string TimeDelayPerm = "norepairdelay.timed";

        private class Configuration
        {
            [JsonProperty(PropertyName = "RepairTime")]
            public float RepairTime = 20f;
        }
    		
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }
		
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["RepairInstant"] = "Congratulations, your structure has been instantly repaired.",
                ["RepairTimed"] = "You have been upgraded before the 30 second timer.",
                ["RepairTimedWait"] = "It has only been {0} seconds since damage, and you must wait {1} seconds."
            }, this);
        }
		
        protected override void LoadDefaultConfig()
        {
            PrintWarning("A new configuration file is being generated.");
            _config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);    

        private void Init()
        {
            permission.RegisterPermission(TimeDelayPerm, this);
            permission.RegisterPermission(NoDelayPerm, this);
        }
        private object OnStructureRepair(BaseCombatEntity entity, BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, NoDelayPerm) && 
                entity.SecondsSinceAttacked >= 0)
            {
                player.ChatMessage(lang.GetMessage("RepairInstant", this, player.UserIDString));
                entity.lastAttackedTime = float.MinValue;
                return null;
            }
            else if(permission.UserHasPermission(player.UserIDString, TimeDelayPerm) && 
                    entity.SecondsSinceAttacked >= _config.RepairTime)
            {
                player.ChatMessage(lang.GetMessage("RepairTimed", this, player.UserIDString));
                entity.lastAttackedTime = float.MinValue;
                return null;
            }
            else if (permission.UserHasPermission(player.UserIDString, TimeDelayPerm) &&
                     entity.SecondsSinceAttacked <= _config.RepairTime)
            {
                player.ChatMessage(string.Format(lang.GetMessage("RepairTimedWait", this, player.UserIDString), entity.SecondsSinceAttacked, _config.RepairTime));
                return true;
            }
            
            return null;
        }
    }
}