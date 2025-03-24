using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Empty Ovens", "Orange", "1.0.0")]
    [Description("Remove wood from campfires and same on placing")]
    public class EmptyOvens : RustPlugin
    {
        #region Oxide Hooks
        
        private void OnEntitySpawned(BaseOven entity)
        {
            var name = entity.ShortPrefabName;
            if (config.whitelist.Contains(name))
            {
                return;
            }

            foreach (var item in entity.inventory.itemList.ToList())
            {
                item.GetHeldEntity()?.Kill();
                item.DoRemove();
            }
        }
        
        #endregion
        
        #region Configuration 1.0.0

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Whitelist")]
            public List<string> whitelist;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                whitelist = new List<string>
                {
                    "shortname here",
                    "that shortnames will be excluded"
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
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