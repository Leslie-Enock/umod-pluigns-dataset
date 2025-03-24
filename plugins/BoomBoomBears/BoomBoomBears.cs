using System.Collections.Generic;
using Oxide.Core;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{ 
    [Info("Boom Boom Bears", "August", "1.0.6")]
    [Description("Bears explode with a fire rocket on death")]

    public class BoomBoomBears : RustPlugin
    {
        #region Fields/Localization/Config
        
        private PluginConfig config;
        private const string Perm = "boomboombears.use";
        private const string PermManage = "boomboombears.manage";
        private Random Num = new Random();
        
        private string[] SupportedPrefabs =
        {
            "assets/prefabs/ammo/rocket/rocket_basic.prefab",
            "assets/prefabs/ammo/rocket/rocket_fire.prefab",
            "assets/prefabs/ammo/rocket/rocket_hv.prefab",
            "assets/prefabs/ammo/rocket/rocket_smoke.prefab",
        };
            
        void Init()
        {

            permission.RegisterPermission(Perm, this);
            permission.RegisterPermission(PermManage, this);
            
            config = Config.ReadObject<PluginConfig>();

            if (!SupportedPrefabs.Contains(config.Prefab))
            {
                PrintWarning(Lang("InvalidPrefab"));
                config.Prefab = "assets/prefabs/ammo/rocket/rocket_fire.prefab";
            }
        }
        private class PluginConfig
        {
            public bool IsEnabled;
            public bool RandomEnabled;
            public int Chance;
            public string Prefab;
        }
        
        private void SaveConfig()
        {
            Config.WriteObject(config, true);
        }
        
        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                IsEnabled = true,
                RandomEnabled = true,
                Chance = 5,
                Prefab = "assets/prefabs/ammo/rocket/rocket_fire.prefab"
            };
        }
        protected override void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPerm"] = "Error: No Permission",
                ["Syntax"] = "Error: Syntax",
                ["InvalidPrefab"] = "Error: An invalid prefab is set in the config. Please change the prefab and reload the plugin.",

                ["Status"] = "Boom boom bears: {0}; Random feature: {1}; Chance: 1/{2}",

                ["BoomEnabled"] = "Boom boom bears are now enabled.",
                ["BoomDisabled"] = "Boom boom bears are now disabled.",

                ["RandomEnabled"] = "If enabled, bears now have a chance to explode on death",
                ["RandomDisabled"] = "If enabled, bears are now guaranteed to explode on death.",

                ["NewChance"] = "If all features are enabled, bears now have a 1/{0} chance to explode on death."

            }, this);
        }
        #endregion
        
        #region Hooks
        void OnEntityDeath(Bear bear, HitInfo info)
        {
            if (!bear || !(bear.lastAttacker is BasePlayer))
            {
                return;
            }
            
            BasePlayer player = bear.lastAttacker.ToPlayer();
            
            if (!permission.UserHasPermission(player.UserIDString, Perm))
            {
                return;
            }

            if (!config.RandomEnabled && !GetRandom())
            {
                return;
            }

            BaseEntity entity = GameManager.server?.CreateEntity(config.Prefab, new Vector3(bear.ServerPosition.x,
                                                                                bear.ServerPosition.y + 1,
                                                                                   bear.ServerPosition.z));
            
            if (!entity)
            {
                return;
            }

            entity.Spawn();
        }
        #endregion
        
        #region Commands
        [ChatCommand("bbbears")]
        void BoomBearsCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermManage))
            {
                player.ChatMessage(Lang("NoPerm", player.UserIDString));
                return;
            }
            if (args.Length == 0 || args.Length > 2)
            {
                player.ChatMessage(Lang("Syntax", player.UserIDString));
                return;
            }
            switch (args[0].ToLower())
            {
                case "stat":
                case "status":
                    player.ChatMessage(Lang("Status", player.UserIDString, config.IsEnabled, config.RandomEnabled, config.Chance));
                    return;
                
                case "toggle":

                    if (args.Length != 2)
                    {
                        player.ChatMessage(Lang("Syntax", player.UserIDString));
                        return;
                    }
                    
                    switch(args[1].ToLower())
                        {
                            case "bears":
                            case "bear":
                                ToggleEnabled(player);
                                break;
                            
                            case "random":
                                ToggleRandom(player);
                                break;
                            
                            default:
                                player.ChatMessage(Lang("Syntax", player.UserIDString));
                                return;
                        }
                    break;
                
                default:
                    int chance;

                    if (int.TryParse(args[0], out chance) && chance > 0)
                    {
                        config.Chance = chance;
                        player.ChatMessage(Lang("NewChance", player.UserIDString, config.Chance));
                        return;
                    }
                    
                    player.ChatMessage(Lang("Syntax", player.UserIDString));
                    break;
            }
            SaveConfig();
        }
        #endregion
        
        #region Methods and Helpers
        bool GetRandom()
        {
            if (Num.Next(0, config.Chance) == 0)
            {
                return true;
            }
            return false;
        }

        void ToggleEnabled(BasePlayer player)
        {
            if (config.IsEnabled)
            {
                player.ChatMessage(Lang("BoomDisabled", player.UserIDString));
                Unsubscribe(nameof(OnEntityDeath));
            }
            else
            {
                player.ChatMessage(Lang("BoomEnabled", player.UserIDString));
                Subscribe(nameof(OnEntityDeath));
            }
            config.IsEnabled = !config.IsEnabled;
        }

        void ToggleRandom(BasePlayer player)
        {
            if (config.RandomEnabled)
            {
                player.ChatMessage(Lang("RandomDisabled", player.UserIDString));
            }
            else
            {
                player.ChatMessage(Lang("RandomEnabled", player.UserIDString));
            }

            config.RandomEnabled = !config.RandomEnabled;
        }
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args); 
        #endregion
    }
}