using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Horse Storage", "Bazz3l", "1.0.5")]
    [Description("Gives horses the ability to carry items")]
    public class HorseStorage : RustPlugin
    {
        #region Fields

        private const string STASH_PREFAB = "assets/prefabs/deployable/small stash/small_stash_deployed.prefab";

        private PluginConfig _config;

        #endregion
        
        #region Config

        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new JsonException();
                }
            }
            catch
            {
                PrintWarning("Loaded default config.");
                
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        class PluginConfig
        {
            public bool EnableStorage;
            
            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    EnableStorage = true
                };
            }
        }
        
        #endregion

        #region Oxide Hooks

        void OnEntitySpawned(RidableHorse entity)
        {
            if (!_config.EnableStorage || entity == null) return;

            NextTick(() => {
                if (entity == null) return;

                foreach (StorageContainer child in entity.GetComponentsInChildren<StorageContainer>(true))
                {
                    if (child.name == STASH_PREFAB) return;
                }

                entity.gameObject.AddComponent<AddStorageBox>();
            });
        }
        
        void OnEntityDeath(RidableHorse entity, HitInfo info)
        {
            if (!_config.EnableStorage || entity == null) return;

            entity.GetComponent<AddStorageBox>()?.OnDeath();
        }

        #endregion
        
        #region Component

        class AddStorageBox : MonoBehaviour
        {
            public RidableHorse entity;
            public StashContainer stash1;
            public StashContainer stash2;

            void Awake()
            {
                entity = GetComponent<RidableHorse>();
                stash1 = CreateStorageContainer(new Vector3(0.4f, 1.15f, -0.45f), new Vector3(90.0f, 90.0f, 0.0f));
                stash2 = CreateStorageContainer(new Vector3(-0.4f, 1.15f, -0.45f), new Vector3(90.0f, 270.0f, 0.0f));
            }
            
            public void OnDeath()
            {
                RemoveStorageContainer(stash1);
                RemoveStorageContainer(stash2);
            }

            StashContainer CreateStorageContainer(Vector3 localPosition, Vector3 rotation)
            {
                StashContainer stash = (StashContainer) GameManager.server.CreateEntity(STASH_PREFAB, entity.transform.position);
                if (stash == null) return null;
                
                stash.Spawn();
                stash.SetParent(entity);
                stash.transform.localPosition = localPosition;
                stash.transform.Rotate(rotation);
                stash.SendNetworkUpdateImmediate(true);

                return stash;
            }
            
            void RemoveStorageContainer(StashContainer stash)
            {
                if (stash.IsValid() && !stash.IsDestroyed)
                    stash.DropItems();
            }
        }

        #endregion
    }
}