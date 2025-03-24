using Oxide.Core;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Heli Editor", "Mabel", "1.0.0")]
    [Description("Modify several characteristics of aircrafts")]
    class HeliEditor : RustPlugin
    {
        #region Variables
        private PluginConfig _config;

        #region Permissions
        public string permissionTakeoff = "helieditor.takeoff";
        #endregion

        #endregion

        #region Config

        private class MinicopterSettings
        {
            public float maxHealth { get; set; } = 750;
            public bool invincible { get; set; } = false;
            public bool blockExplosions { get; set; } = false;
            public bool instantTakeoff { get; set; } = false;
            public bool hydrophobic { get; set; } = false;
        }

        private class ScrapheliSettings
        {
            public float maxHealth { get; set; } = 1000;
            public bool invincible { get; set; } = false;
            public bool blockExplosions { get; set; } = false;
            public bool instantTakeoff { get; set; } = false;
            public bool hydrophobic { get; set; } = false;
        }

        private class AttackHelicopterSettings
        {
            public float maxHealth { get; set; } = 850;
            public bool invincible { get; set; } = false;
            public bool blockExplosions { get; set; } = false;
            public bool instantTakeoff { get; set; } = false;
            public bool hydrophobic { get; set; } = false;
        }

        private class PluginConfig
        {
            public MinicopterSettings minicopter { get; set; }
            public ScrapheliSettings scrapheli { get; set; }
            public AttackHelicopterSettings attackHelicopter { get; set; }
            public VersionNumber version { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    minicopter = new MinicopterSettings(),
                    scrapheli = new ScrapheliSettings(),
                    attackHelicopter = new AttackHelicopterSettings(),
                    version = new VersionNumber()
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            bool shouldSaveConfig = false;

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    PrintWarning("No configuration file found, loading default configuration...");
                    LoadDefaultConfig();
                    _config = Config.ReadObject<PluginConfig>();
                    shouldSaveConfig = true;
                }

                if (_config.attackHelicopter == null)
                {
                    _config.attackHelicopter = new AttackHelicopterSettings
                    {
                        maxHealth = 850,
                        invincible = false,
                        blockExplosions = false,
                        instantTakeoff = false,
                        hydrophobic = false
                    };
                    shouldSaveConfig = true;
                }

                if (_config.version == null)
                {
                    _config.version = new VersionNumber(1,0,0);
                    shouldSaveConfig = true;
                }

                if (_config.version < Version)
                    UpdateConfig();
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                PrintWarning("Error loading configuration, creating new configuration file...");
                LoadDefaultConfig();
                shouldSaveConfig = true;
            }
            finally
            {
                if (shouldSaveConfig)
                {
                    SaveConfig();
                }
            }
        }

        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_config);

        private void UpdateConfig()
        {
            PrintWarning("Config update detected! Updating config values...");

            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (_config.version < new VersionNumber(1, 0, 0))
                _config = baseConfig;

            _config.version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Hooks
        private void Init()
        {
            permission.RegisterPermission(permissionTakeoff, this);
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is Minicopter && _config.minicopter.invincible)
            {
                info.damageTypes.Clear();
                return true;
            }
            if (entity is ScrapTransportHelicopter && _config.scrapheli.invincible)
            {
                info.damageTypes.Clear();
                return true;
            }
            if (entity is AttackHelicopter && _config.attackHelicopter.invincible)
            {
                info.damageTypes.Clear();
                return true;
            }
            return null;
        }

        private void OnServerInitialized()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity == null) continue;

                if (entity is Minicopter minicopter)
                {
                    OnEntitySpawned(minicopter);
                }
                else if (entity is ScrapTransportHelicopter scraphelicopter)
                {
                    OnEntitySpawned(scraphelicopter);
                }
                else if (entity is AttackHelicopter attackhelicopter)
                {
                    OnEntitySpawned(attackhelicopter);
                }
            }
        }

        private void OnEntitySpawned(BaseHelicopter entity)
        {
            if (entity is Minicopter)
            {
                var miniEntity = entity as PlayerHelicopter;

                //Remove explosion effect references to not have it spawn
                if (_config.minicopter.blockExplosions)
                {
                    entity.explosionEffect.guid = null;
                    entity.serverGibs.guid = null;
                    entity.fireBall.guid = null;
                }

                //Set health set in config
                entity.SetMaxHealth(_config.minicopter.maxHealth);
                entity.health = _config.minicopter.maxHealth;

                //Unparent the water sample object to prevent it from moving with the minicopter
                if (_config.minicopter.hydrophobic)
                {
                    miniEntity.waterSample.transform.SetParent(null);
                    miniEntity.waterSample.position = new Vector3(1000, 1000, 1000);
                }

                //Remove killtriggers for invincibility
                if (_config.minicopter.invincible)
                {
                    entity.killTriggers = new GameObject[0];
                }
                    
            }
            if (entity is ScrapTransportHelicopter)
            {
                var scrapEntity = entity as PlayerHelicopter;

                //Remove explosion effect references to not have it spawn
                if (_config.scrapheli.blockExplosions)
                {
                    entity.explosionEffect.guid = null;
                    entity.serverGibs.guid = null;
                    entity.fireBall.guid = null;
                }

                //Set health set in config
                entity.SetMaxHealth(_config.scrapheli.maxHealth);
                entity.health = _config.scrapheli.maxHealth;

                //Unparent the water sample object to prevent it from moving with the minicopter
                if (_config.scrapheli.hydrophobic)
                {
                    scrapEntity.waterSample.transform.SetParent(null);
                    scrapEntity.waterSample.position = new Vector3(1000, 1000, 1000);
                }

                //Remove killtriggers for invincibility
                if (_config.scrapheli.invincible)
                {
                    entity.killTriggers = new GameObject[0];
                } 
                return;
            }
            if (entity is AttackHelicopter)
            {
                var attackEntity = entity as PlayerHelicopter;

                //Remove explosion effect references to not have it spawn
                if (_config.attackHelicopter.blockExplosions)
                {
                    entity.explosionEffect.guid = null;
                    entity.serverGibs.guid = null;
                    entity.fireBall.guid = null;
                }

                //Set health set in config
                entity.SetMaxHealth(_config.attackHelicopter.maxHealth);
                entity.health = _config.attackHelicopter.maxHealth;

                //Unparent the water sample object to prevent it from moving with the minicopter
                if (_config.attackHelicopter.hydrophobic)
                {
                    attackEntity.waterSample.transform.SetParent(null);
                    attackEntity.waterSample.position = new Vector3(1000, 1000, 1000);
                }

                //Remove killtriggers for invincibility
                if (_config.attackHelicopter.invincible)
                {
                    entity.killTriggers = new GameObject[0];
                }
                return;
            }
        }

        void OnEngineStarted(BaseVehicle vehicle, BasePlayer driver)
        {
            if (!permission.UserHasPermission(driver.UserIDString, permissionTakeoff)) return;

            if (vehicle is ScrapTransportHelicopter && _config.scrapheli.instantTakeoff)
            {
                (vehicle as PlayerHelicopter).engineController.FinishStartingEngine();
            }

            if (vehicle is Minicopter && _config.minicopter.instantTakeoff)
            {
                (vehicle as PlayerHelicopter).engineController.FinishStartingEngine();
            }

            if (vehicle is AttackHelicopter && _config.attackHelicopter.instantTakeoff)
            {
                (vehicle as PlayerHelicopter).engineController.FinishStartingEngine();
            }          
        }
        #endregion
    }
}