using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("No Experiments", "Orange", "1.0.1")]
    [Description("Plugin disables experiments in workbenches")]
    public class NoExperiments : RustPlugin
    {
        #region Oxide Hooks

        private object CanExperiment(BasePlayer player, Workbench workbench)
        {
            switch (workbench.Workbenchlevel)
            {
                case 1:
                    return config.block1 ? false : (object) null;
                case 2:
                    return config.block2 ? false : (object) null;
                case 3:
                    return config.block3 ? false : (object) null;
                default:
                    return false;
            }
        }

        #endregion
        
        #region Configuration
        
        private static ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "Block on lvl 1")]
            public bool block1;
            
            [JsonProperty(PropertyName = "Block on lvl 2")]
            public bool block2;
            
            [JsonProperty(PropertyName = "Block on lvl 3")]
            public bool block3;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData 
            {
                block1 = true,
                block2 = true,
                block3 = true
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