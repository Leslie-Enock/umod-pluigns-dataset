using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Research Block", "Orange", "1.0.1")]
    [Description("Allows to block researching several items")]
    public class ResearchBlock : RustPlugin
    {
        #region Fields
        
        private List<ItemDefinition> _disabledResearchable = null;

        #endregion
        
        #region Oxide Hooks

        private void OnServerInitialized()
        {
            if (!config.blockExperimenting)
            {
                return;
            }
            
            Unsubscribe(nameof(CanResearchItem));
            
            _disabledResearchable = new List<ItemDefinition>();
            foreach (var itemShortname in config.shortnames)
            {
                var itemDef = ItemManager.FindItemDefinition(itemShortname);
                if (itemDef == null)
                {
                    PrintWarning("Unable to find Item Defintion for Shortname: " + itemShortname);
                    continue;
                }

                if (!itemDef.Blueprint.isResearchable)
                {
                    PrintWarning("Trying to blacklist item that isn't researchable by default: " + itemShortname);
                    continue;
                }

                itemDef.Blueprint.isResearchable = false;
                _disabledResearchable.Add(itemDef);
            }
        }

        private void Unload()
        {
            if (_disabledResearchable == null)
            {
                return;
            }
            
            foreach (var itemDef in _disabledResearchable)
            {
                itemDef.Blueprint.isResearchable = true;
            }
        }

        private object CanResearchItem(BasePlayer player, Item item)
        {
            return config.shortnames.Contains(item.info.shortname) ? false : (object) null;
        }

        #endregion
        
        #region Config
        
        private static ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "Blocked shortnames")]
            public List<string> shortnames;
            [JsonProperty(PropertyName = "Additionally block Experimenting")]
            public bool blockExperimenting;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                shortnames = new List<string>
                {
                    "rifle.ak",
                    "ammo.rifle"
                },
                blockExperimenting = false
            };
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
   
            try
            {
                config = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        
        #endregion
    }
}