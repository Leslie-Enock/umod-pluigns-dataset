using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Helicopter Protection", "Nobu", "1.0.2")]
    [Description("Protects you from the helicopter and vice versa")]

    class HelicopterProtection : RustPlugin
    {
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Time on server before heli targets them")]
            public double contime = 600;
            [JsonProperty(PropertyName = "Protect players from heli")]
            public bool protectplayer = true;
            [JsonProperty(PropertyName = "Protect heli from players")]
            public bool protectheli = true;
        }
        private bool LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConfig(configData);
            return true;
        }

        void Init()
        {
            if (!LoadConfigVariables())
            {
                Puts("Config Issue Detected, delete file or check syntax");
                return;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file");
            configData = new ConfigData();
            SaveConfig(configData);
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        bool? CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer player)
        {
            if(configData.protectplayer == true)
            { 
                if(player.Connection.GetSecondsConnected() <= configData.contime)
                {
                    return false;
                }
                else
                {
                   return null;
                }
            }
            else
            {
                return null;
            }
        }
        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (configData.protectheli == true)
            {
                if(info.Initiator is BasePlayer)
                { 
                   BasePlayer player = info.Initiator.GetComponent<BasePlayer>();
                   if (player.Connection.GetSecondsConnected() <= configData.contime)
                   {
                        return null;
                   }
                   else if(player == null)
                   {
                        return null;
                   }
                   else
                    {
                        return null;    
                    }
                }
                else
                {
                    return false;
                }
            }
            else
            {
                return null;
            }
        }
    }
}