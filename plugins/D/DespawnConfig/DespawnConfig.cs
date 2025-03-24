using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Despawn Config", "Wulf", "2.2.3")]
    [Description("Configurable despawn times for dropped items and item containers")]
    class DespawnConfig : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Global despawn time")]
            public float GlobalDespawnTime = -1f;

            [JsonProperty("Item container despawn time")]
            public float ItemContainerDespawnTime = 30f;

            [JsonProperty("Despawn item container with items")]
            public bool DespawnContainerWithItems = false;

            [JsonProperty("Item despawn times", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public SortedDictionary<string, float> ItemDespawnTimes = new SortedDictionary<string, float>();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());

            public float GetItemDespawnTime(Item item)
            {
                if (item == null)
                {
                    return 0;
                }

                if (GlobalDespawnTime >= 0)
                {
                    return GlobalDespawnTime * 60f;
                }

                if (!ItemDespawnTimes.ContainsKey(item.info.shortname))
                {
                    _plugin.LogError($"Couldn't find despawn time for {item.info.shortname}; using default of 5 minutes");
                    return 5;
                }

                return ItemDespawnTimes[item.info.shortname] * 60f;
            }
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Initialization

        private static DespawnConfig _plugin;

        private void Init()
        {
            _plugin = this;

            Unsubscribe(nameof(OnEntitySpawned));
        }
        
        private void Unload()
        {
            _plugin = null;
        }

        private void OnServerInitialized()
        {
            Subscribe(nameof(OnEntitySpawned));

            int origDespawnTimes = config.ItemDespawnTimes.Count;
            foreach (ItemDefinition item in ItemManager.itemList)
            {
                if (!config.ItemDespawnTimes.ContainsKey(item.shortname))
                {
                    config.ItemDespawnTimes.Add(item.shortname, item.quickDespawn ? 30 : Mathf.Clamp(((int)item.rarity - 1) * 4, 1, 100) * 300 / 60);
                }
            }
            int newDespawnTimes = config.ItemDespawnTimes.Count;
            if (!origDespawnTimes.Equals(newDespawnTimes))
            {
                Log($"Saved {newDespawnTimes - origDespawnTimes} new items to configuration");
                SaveConfig();
            }

            foreach (BaseNetworkable networkable in BaseNetworkable.serverEntities)
            {
                SetDespawnTime(networkable);
            }

            if (config.GlobalDespawnTime > -1)
            {
                LogWarning($"Global respawn time is currently set to {config.GlobalDespawnTime}; individal item despawn times overridden");
            }
        }

        #endregion Initialization

        #region Despawn Handling

        private void OnDroppedItemCombined(DroppedItem item) => SetDespawnTime(item);

        private void OnEntitySpawned(DroppedItem item) => SetDespawnTime(item);

        private void OnEntitySpawned(DroppedItemContainer itemContainer) => SetDespawnTime(itemContainer);

        private void SetDespawnTime(BaseNetworkable itemOrContainer)
        {
            if (!(itemOrContainer is ItemPickup) && itemOrContainer is DroppedItem)
            {
                DroppedItem item = itemOrContainer as DroppedItem;
                
                if (item != null && !item.IsDestroyed)
                {
                    item.CancelInvoke(item.IdleDestroy);
                    item.Invoke(item.IdleDestroy, config.GetItemDespawnTime(item.item));
                }
            }
            
            if (itemOrContainer is DroppedItemContainer)
            {
                DroppedItemContainer container = itemOrContainer as DroppedItemContainer;
                
                if (!config.DespawnContainerWithItems || container.inventory == null)
                {
                    container.ResetRemovalTime(config.ItemContainerDespawnTime * 60f);
                    return;
                }
                
                List<Item> items = container.inventory.itemList;
                float despawnTime = 0;
                
                for (int i = 0; i < items.Count; i++)
                {
                    float time = config.GetItemDespawnTime(items[i]);

                    if (time <= despawnTime)
                    {
                        continue;
                    }

                    despawnTime = time;
                }

                timer.In(1f, () =>
                {
                    container.CancelInvoke(container.RemoveMe);
                    container?.ResetRemovalTime(despawnTime);
                });
            }
        }

        #endregion Despawn Handling
    }
}