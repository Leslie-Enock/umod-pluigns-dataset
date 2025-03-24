using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Junkyard Shredder", "Clearshot", "1.1.0")]
    [Description("Configure the items output by the junkyard shredder")]
    class JunkyardShredder : CovalencePlugin
    {
        private PluginConfig _config;
        private Dictionary<string, Dictionary<string, int>> _vanillaItemAmounts = new Dictionary<string, Dictionary<string, int>>();

        void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
        }

        void OnServerInitialized()
        {
            IEnumerable<MagnetLiftable> magnetLiftableList = BaseNetworkable.serverEntities
                .Where(e => !(e.GetComponent<MagnetLiftable>() == null))
                .Select(e => e.GetComponent<MagnetLiftable>());
            
            foreach (MagnetLiftable ml in magnetLiftableList)
            {
                var prefabName = ml.baseEntity.ShortPrefabName;
                if (!_vanillaItemAmounts.ContainsKey(prefabName))
                {
                    var shredRes = ml.shredResources.ToDictionary(i => i.itemDef.shortname, i => (int)i.amount);
                    _vanillaItemAmounts.Add(prefabName, shredRes);

                    if (!_config.shredderConfig.ContainsKey(prefabName))
                    {
                        _config.shredderConfig.Add(ml.baseEntity.ShortPrefabName, new ShredConfig {
                            vanilla = shredRes,
                            modified = shredRes
                        });
                    }
                    else
                        _config.shredderConfig[prefabName].vanilla = shredRes;
                }

                UpdateShredResources(ml, _config.shredderConfig[prefabName].modified);
            }

            Config.WriteObject(_config, true);
            Subscribe(nameof(OnEntitySpawned));
        }

        void OnEntitySpawned(BaseEntity ent)
        {
            if (ent == null) return;

            if (_config.shredderConfig.ContainsKey(ent.ShortPrefabName))
            {
                MagnetLiftable ml = ent.GetComponent<MagnetLiftable>();
                if (ml == null) return;

                UpdateShredResources(ml, _config.shredderConfig[ent.ShortPrefabName].modified);
            }
        }

        void Unload()
        {
            IEnumerable<MagnetLiftable> magnetLiftableList = BaseNetworkable.serverEntities
                .Where(e => !(e.GetComponent<MagnetLiftable>() == null))
                .Select(e => e.GetComponent<MagnetLiftable>());

            foreach (MagnetLiftable ml in magnetLiftableList)
            {
                var prefabName = ml.baseEntity.ShortPrefabName;
                if (_vanillaItemAmounts.ContainsKey(prefabName))
                {
                    UpdateShredResources(ml, _vanillaItemAmounts[prefabName]);
                }
            }
        }

        void UpdateShredResources(MagnetLiftable ml, Dictionary<string, int> list)
        {
            List<ItemAmount> itemAmts = new List<ItemAmount>();
            foreach (var item in list)
            {
                ItemDefinition itemDef = ItemManager.FindItemDefinition(item.Key);
                if (itemDef != null)
                {
                    itemAmts.Add(new ItemAmount(itemDef, item.Value));
                }
            }

            if (itemAmts.Count > 0)
            {
                ml.shredResources = itemAmts.ToArray();
            }
        }

        #region Config
        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        private class PluginConfig
        {
            public Dictionary<string, ShredConfig> shredderConfig = new Dictionary<string, ShredConfig>();
        }

        private class ShredConfig
        {
            public Dictionary<string, int> vanilla;
            public Dictionary<string, int> modified;
        }
        #endregion
    }
}
