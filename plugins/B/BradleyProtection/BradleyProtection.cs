using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Bradley Protection", "Nobu", "1.0.2")]
    [Description("Protects you from the tank and vice versa")]

    class BradleyProtection : RustPlugin
    {
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Use permissions to enable")]
            public bool perms = false;
            [JsonProperty(PropertyName = "Time on server before bradley targets them")]
            public double contime = 600;
            [JsonProperty(PropertyName = "Protect players from bradley")]
            public bool protectplayer = true;
            [JsonProperty(PropertyName = "Protect bradley from players")]
            public bool protecttank = true;
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
            permission.RegisterPermission("BradleyProtection.protect", this);
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

        bool? CanBradleyApcTarget(BradleyAPC apc, BaseEntity entity)
        { 
            BasePlayer player = entity as BasePlayer;
            if(permission.UserHasPermission(player.UserIDString, "BradleyProtection.protect") && configData.perms == true)
            { 
              if (configData.protectplayer == true)
              {
                   if (player.Connection.GetSecondsConnected() <= configData.contime)
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
            else
            {
                if (configData.protectplayer == true)
                {
                    if (player.Connection.GetSecondsConnected() <= configData.contime)
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
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            BasePlayer player = entity as BasePlayer;
            if (permission.UserHasPermission(player.UserIDString, "BradleyProtection.protect") && configData.perms == true)
            {
                if (configData.protecttank == true)
                {
                   if (info.Initiator is BasePlayer)
                   {
                       if (player.Connection.GetSecondsConnected() <= configData.contime && player != null)
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
            else
            {
                if (configData.protecttank == true)
                {
                    if (info.Initiator is BasePlayer)
                    {
                        if (player.Connection.GetSecondsConnected() <= configData.contime && player != null)
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
}