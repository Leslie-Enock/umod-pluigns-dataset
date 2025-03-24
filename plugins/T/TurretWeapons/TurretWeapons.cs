using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Turret Weapons", "Iv Misticos", "1.0.1")]
    [Description("Control weapons placement in turrets")]
    class TurretWeapons : CovalencePlugin
    {
        #region Configuration
        
        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Weapons Allowed", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, bool> Weapons = new Dictionary<string, bool>();
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

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();
        
        #endregion
        
        #region Hooks

        private void OnServerInitialized()
        {
            foreach (var item in ItemManager.itemList)
            {
                var proj = item.GetComponent<ItemModEntity>()?.entityPrefab?.Get()?.GetComponent<BaseProjectile>();
                if (proj != null)
                {
                    bool isAllowed;
                    if (!_config.Weapons.TryGetValue(item.shortname, out isAllowed))
                    {
                        _config.Weapons[item.shortname] = false;
                        proj.usableByTurret = false;
                        continue;
                    }

                    proj.usableByTurret = isAllowed;
                }
            }
            
            SaveConfig();

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is HeldEntity)
                    OnEntitySpawned(entity as HeldEntity);
            }
        }

        private void OnEntitySpawned(HeldEntity entity)
        {
            var proj = entity.GetComponent<BaseProjectile>();
            if (proj == null)
                return;

            proj.usableByTurret = true;
        }
        
        #endregion
    }
}