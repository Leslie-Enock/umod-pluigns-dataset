using System;
using Oxide.Core;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("NoVood", "Sapnu Puas#3696", "1.0.4")]
    [Description("use furnaces with no wood and quick smelt options, by permission")]
    public class NoVood : RustPlugin
    {
        
        private void Init()
        {
            foreach (var entry in config.Permissions)
            {
                permission.RegisterPermission(entry.Key, this);
            }
        }

        private void OnServerInitialized()
        {
            config = Config.ReadObject<Configuration>();
            Config.WriteObject(config);
        }
        
        private object OnFindBurnable(BaseOven oven)
        {
            if (oven == null || oven.net == null) return null;

            string Permission = "";
            foreach (var check in config.Permissions)
            {
                if (permission.UserHasPermission(oven.OwnerID.ToString(), check.Key))
                {
                    Permission = check.Key;
                }
            }
            if (string.IsNullOrEmpty(Permission)) return null;
            

            var playerPermission = config.Permissions[Permission];
            FurnaceSetup configValue = null;
            
           if (playerPermission.furnaceSettings.ContainsKey(oven.ShortPrefabName))
                configValue = playerPermission.furnaceSettings[oven.ShortPrefabName];
            
             if (configValue != null)
            {
                bool usenowood = configValue.UseNoWood;
                bool raw = configValue.NeedRes;
                if (!string.IsNullOrEmpty(oven.ShortPrefabName))
                {
                    foreach (var check in oven.inventory.itemList)
                        if (check.info.shortname.Contains("wood"))
                            return null;

                    if (usenowood)
                    {
                        var fuel = ItemManager.CreateByItemID(-151838493);

                        if (!raw)
                        {
                            return fuel;
                        }

                        foreach (var check in oven.inventory.itemList)

                            if (config.AllowedRes.Contains(check.info.shortname))
                            {
                                return fuel;
                            }
                        return null;
                    }
                }
            }
            return null;
        }

        void OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (oven == null || oven.IsDestroyed) return;
            string Permission = "";
            foreach (var check in config.Permissions)
            {
                if (permission.UserHasPermission(oven.OwnerID.ToString(), check.Key))
                {
                    Permission = check.Key;
                }
            }
            if (string.IsNullOrEmpty(Permission)) return;

            var playerPermission = config.Permissions[Permission];
            FurnaceSetup configValue = null;

            if (playerPermission.furnaceSettings.ContainsKey(oven.ShortPrefabName))
                configValue = playerPermission.furnaceSettings[oven.ShortPrefabName];

            if (configValue != null)
            {
                int ovenspeed = configValue.Speed;
                NextTick(() =>
                {
                    if (!string.IsNullOrEmpty(oven.ShortPrefabName))
                    {
                        if (!oven.HasFlag(BaseEntity.Flags.On))
                            NextTick(() =>
                            {
                                NextTick(oven.StopCooking);
                            });
                        else
                        {
                            var ovenCookingTemperature = 10 / (0.5 * (oven.cookingTemperature / 200));
                            var speed = 0.5f / ovenspeed;
                            oven.CancelInvoke(oven.Cook);
                            oven.inventory.temperature = (float)ovenCookingTemperature;
                            //oven.inventory.temperature = oven.cookingTemperature;
                            oven.UpdateAttachmentTemperature();
                            oven.InvokeRepeating(oven.Cook, speed, speed);
                            return;
                        }
                    }
                    return;
                });
            }
        }

        #region Config
        private Configuration config;
        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(config = new Configuration()
            {
                Permissions = new Dictionary<string, Ovens>()
                {
                    [$"{Name}.vip"] = new Ovens()
                    {
                        furnaceSettings = new Dictionary<string, FurnaceSetup>()
                        {
                            ["furnace"] = new FurnaceSetup()
                            {
                                UseNoWood = false,
                                NeedRes = true,
                                Speed = 3
                            },
                            ["furnace.large"] = new FurnaceSetup
                            {
                                UseNoWood = false,
                                NeedRes = true,
                                Speed = 3
                            },
                            ["refinery_small_deployed"] = new FurnaceSetup
                            {
                                UseNoWood = false,
                                NeedRes = true,
                                Speed = 3
                            }
                           
                        }
                    },
                    [$"{Name}.vip+"] = new Ovens()
                    {
                        furnaceSettings = new Dictionary<string, FurnaceSetup>()
                        {
                            ["furnace"] = new FurnaceSetup()
                            {
                                UseNoWood = false,
                                NeedRes = true,
                                Speed = 5
                            },
                            ["furnace.large"] = new FurnaceSetup
                            {
                                UseNoWood = false,
                                NeedRes = true,
                                Speed = 5
                            },
                            ["refinery_small_deployed"] = new FurnaceSetup
                            {
                                UseNoWood = false,
                                NeedRes = true,
                                Speed = 5
                            },
                            ["bbq.deployed"] = new FurnaceSetup
                            {
                                UseNoWood = false,
                                NeedRes = true,
                                Speed = 5
                            }
                            
                        }
                    },
                    [$"{Name}.vip++"] = new Ovens()
                    {
                        furnaceSettings = new Dictionary<string, FurnaceSetup>()
                        {
                            ["furnace"] = new FurnaceSetup()
                            {
                                UseNoWood = true,
                                NeedRes = true,
                                Speed = 10
                            },
                            ["furnace.large"] = new FurnaceSetup
                            {
                                UseNoWood = true,
                                NeedRes = true,
                                Speed = 10
                            },
                            ["refinery_small_deployed"] = new FurnaceSetup
                            {
                                UseNoWood = true,
                                NeedRes = true,
                                Speed = 10
                            },
                            ["bbq.deployed"] = new FurnaceSetup
                            {
                                UseNoWood = true,
                                NeedRes = true,
                                Speed = 10
                            },


                            ["carvable.pumpkin"] = new FurnaceSetup
                            {
                                UseNoWood = true,
                                NeedRes = false,
                                Speed = 1
                            },
                            ["chineselantern.deployed"] = new FurnaceSetup
                            {
                                UseNoWood = true,
                                NeedRes = false,
                                Speed = 1
                            },
                            ["jackolantern.angry"] = new FurnaceSetup
                            {
                                UseNoWood = true,
                                NeedRes = false,
                                Speed = 1
                            },
                            ["jackolantern.happy"] = new FurnaceSetup
                            {
                                UseNoWood = true,
                                NeedRes = false,
                                Speed = 1
                            },
                            ["lantern.deployed"] = new FurnaceSetup
                            {
                                UseNoWood = true,
                                NeedRes = false,
                                Speed = 1
                            },
                            ["tunalight.deployed"] = new FurnaceSetup
                            {
                                UseNoWood = true,
                                NeedRes = false,
                                Speed = 1
                            },
                            
                            ["campfire"] = new FurnaceSetup
                            {
                                UseNoWood = true,
                                NeedRes = false,
                                Speed = 1
                            },
                            ["fireplace.deployed"] = new FurnaceSetup
                            {
                                UseNoWood = true,
                                NeedRes = false,
                                Speed = 1
                            },
                            ["hobobarrel.deployed"] = new FurnaceSetup
                            {
                                UseNoWood = true,
                                NeedRes = false,
                                Speed = 1
                            },
                            ["skull_fire_pit"] = new FurnaceSetup
                            {
                                UseNoWood = true,
                                NeedRes = false,
                                Speed = 1
                            }
                        }
                    },
                }
            }, true);
        }
        public class Configuration
        {
           [JsonProperty("Permissions", ObjectCreationHandling = ObjectCreationHandling.Replace)]
             public Dictionary<string, Ovens> Permissions = new Dictionary<string, Ovens>();

           [JsonProperty("Allowed Resources (Item Shortnames) items must be in this list to use without wood")]
           public HashSet<string> AllowedRes = new HashSet<string>()
            {
                { "metal.ore" },
                { "hq.metal.ore" },
                { "sulfur.ore" },
                { "crude.oil"},
                { "bearmeat" },
                { "chicken.raw" },
                { "horsemeat.raw" },
                { "meat.boar" },
                {"wolfmeat.raw" },
                { "deermeat.raw" },
                { "fish.raw"},
                {"humanmeat.raw" }
            };

        }

        public class Ovens
        {
            [JsonProperty("base oven short prefab name and settings")]
            public Dictionary<string, FurnaceSetup> furnaceSettings { get; set; }
        }

        public class FurnaceSetup
        {
            [JsonProperty("Uses no fuel ?")]
            public bool UseNoWood { get; set; }

            [JsonProperty("Require raw resources to start when using no fuel ?")]
            public bool NeedRes { get; set; }

            [JsonProperty("Quicksmelt speed (1 is default speed)")]
            public int Speed { get; set; }

        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

    }
}
