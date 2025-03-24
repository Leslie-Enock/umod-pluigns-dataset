using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Water Works", "nivex", "1.1.0")]
    [Description("Control the monopoly on your water supplies.")]
    class WaterWorks : RustPlugin
    {
        private readonly List<string> _itemShortnames = new()
        {
            "bucket.water",
            "waterjug",
            "pistol.water",
            "gun.water",
            "smallwaterbottle",
            "botabag"  // Новый предмет
        };

        private readonly Dictionary<string, ItemConfig> _itemConfigs = new();

        private readonly Dictionary<string, EntityConfig> _entityConfigs = new();

        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
            AddCovalenceCommand("wwstats", nameof(CommandStats));
        }

        private void OnServerInitialized()
        {
            foreach (var shortname in _itemShortnames)
            {
                ItemDefinition def = ItemManager.FindItemDefinition(shortname);
                if (def != null && def.TryGetComponent<ItemModContainer>(out var container))
                {
                    var itemConfig = new ItemConfig
                    {
                        Shortname = shortname,
                        ItemId = def.itemid,
                        DefaultMaxStackSize = container.maxStackSize,
                        ConfigMaxStackSize = GetConfigValue(shortname)
                    };

                    _itemConfigs[shortname] = itemConfig;

                    if (itemConfig.ConfigMaxStackSize > 0)
                    {
                        container.maxStackSize = itemConfig.ConfigMaxStackSize;
                    }
                }
            }

            InitializeEntityConfigs();
            SetLiquidContainerStackSize(true);
            ConfigureWaterDefinitions(true);
            Subscribe(nameof(OnEntitySpawned));

            if (WaterTypes.WaterItemDef.stackable < int.MaxValue)
            {
                WaterTypes.WaterItemDef.stackable = int.MaxValue;
            }
        }

        private void InitializeEntityConfigs()
        {
            _entityConfigs["waterbarrel"] = new EntityConfig
            {
                ShortPrefabName = "waterbarrel",
                DefaultMaxStackSize = 20000, // Vanilla value
                ConfigMaxStackSize = config.WaterBarrel
            };

            _entityConfigs["water_catcher_small"] = new EntityConfig
            {
                ShortPrefabName = "water_catcher_small",
                DefaultMaxStackSize = 10000, // Vanilla value
                ConfigMaxStackSize = config.SmallWaterCatcher.StackSize,
                MaxItemToCreate = config.SmallWaterCatcher.MaxItemToCreate,
                Interval = config.SmallWaterCatcher.Interval,
            };

            _entityConfigs["water_catcher_large"] = new EntityConfig
            {
                ShortPrefabName = "water_catcher_large",
                DefaultMaxStackSize = 50000, // Vanilla value
                ConfigMaxStackSize = config.LargeWaterCatcher.StackSize,
                MaxItemToCreate = config.LargeWaterCatcher.MaxItemToCreate,
                Interval = config.LargeWaterCatcher.Interval,
            };

            _entityConfigs["poweredwaterpurifier.deployed"] = new EntityConfig
            {
                ShortPrefabName = "poweredwaterpurifier.deployed",
                DefaultMaxStackSize = 5000, // Vanilla value
                ConfigMaxStackSize = config.PoweredWaterPurifier
            };

            _entityConfigs["waterpurifier.deployed"] = new EntityConfig
            {
                ShortPrefabName = "waterpurifier.deployed",
                DefaultMaxStackSize = 5000, // Vanilla value
                ConfigMaxStackSize = config.WaterPurifier
            };

            _entityConfigs["water.pump.deployed"] = new EntityConfig
            {
                ShortPrefabName = "water.pump.deployed",
                DefaultMaxStackSize = 2000, // Vanilla value
                ConfigMaxStackSize = config.WaterPump.StackSize,
                PumpInterval = config.WaterPump.Interval,
                AmountPerPump = config.WaterPump.Amount
            };

            _entityConfigs["paddlingpool.deployed"] = new EntityConfig
            {
                ShortPrefabName = "paddlingpool.deployed",
                DefaultMaxStackSize = 500, // Vanilla value
                ConfigMaxStackSize = config.GroundPool
            };

            _entityConfigs["abovegroundpool.deployed"] = new EntityConfig
            {
                ShortPrefabName = "abovegroundpool.deployed",
                DefaultMaxStackSize = 2000, // Vanilla value
                ConfigMaxStackSize = config.BigGroundPool
            };
        }

        private void Unload()
        {
            if (config.Reset && !Interface.Oxide.IsShuttingDown)
            {
                SetLiquidContainerStackSize(false);
                ConfigureWaterDefinitions(false);
            }
        }

        private void OnEntitySpawned(LiquidContainer lc)
        {
            SetLiquidContainerStackSize(lc, true);
        }

        private void CommandStats(IPlayer user, string command, string[] args)
        {
            if (!user.IsAdmin) return;

            Puts("=== Item Configurations ===");
            foreach (var itemConfig in _itemConfigs.Values)
            {
                Puts("Item: {0}, ItemID: {1}, Current MaxStackSize: {2}, Default MaxStackSize: {3}",
                    itemConfig.Shortname,
                    itemConfig.ItemId,
                    itemConfig.ConfigMaxStackSize > 0 ? itemConfig.ConfigMaxStackSize : itemConfig.DefaultMaxStackSize,
                    itemConfig.DefaultMaxStackSize);
            }

            Puts("=== Entity Configurations ===");
            foreach (var entityConfig in _entityConfigs.Values)
            {
                Puts("Entity: {0}, Current MaxStackSize: {1}, Default MaxStackSize: {2}",
                    entityConfig.ShortPrefabName,
                    entityConfig.ConfigMaxStackSize > 0 ? entityConfig.ConfigMaxStackSize : entityConfig.DefaultMaxStackSize,
                    entityConfig.DefaultMaxStackSize);

                if (entityConfig.ShortPrefabName.Contains("water_catcher"))
                {
                    Puts("  MaxItemToCreate: {0}, Interval: {1}", entityConfig.MaxItemToCreate, entityConfig.Interval);
                }
                if (entityConfig.ShortPrefabName == "water.pump.deployed")
                {
                    Puts("  PumpInterval: {0}, AmountPerPump: {1}", entityConfig.PumpInterval, entityConfig.AmountPerPump);
                }
            }
        }

        private void SetLiquidContainerStackSize(bool state)
        {
            foreach (BaseNetworkable networkable in BaseNetworkable.serverEntities)
            {
                if (networkable is LiquidContainer lc)
                {
                    SetLiquidContainerStackSize(lc, state);
                }
            }
        }

        private void SetLiquidContainerStackSize(LiquidContainer lc, bool state)
        {
            if (lc == null || lc.IsDestroyed)
            {
                return;
            }

            if (!_entityConfigs.TryGetValue(lc.ShortPrefabName, out var entityConfig))
            {
                return;
            }

            switch (lc.ShortPrefabName)
            {
                case "waterbarrel":
                    UpdateWaterBarrel(lc, state, entityConfig);
                    break;

                case "water_catcher_small":
                case "water_catcher_large":
                    UpdateWaterCatcher(lc as WaterCatcher, state, entityConfig);
                    break;

                case "poweredwaterpurifier.deployed":
                    UpdatePoweredWaterPurifier(lc, state, entityConfig);
                    break;

                case "waterpurifier.deployed":
                    UpdateWaterPurifier(lc as WaterPurifier, state, entityConfig);
                    break;

                case "water.pump.deployed":
                    UpdateWaterPump(lc as WaterPump, state, entityConfig);
                    break;

                case "paddlingpool.deployed":
                    UpdateGroundPool(lc, state, entityConfig);
                    break;

                case "abovegroundpool.deployed":
                    UpdateBigGroundPool(lc, state, entityConfig);
                    break;

                default:
                    break;
            }
        }

        private void SetSlotAmounts(LiquidContainer lc, int num)
        {
            if (num > 0)
            {
                Item slot = lc.inventory.GetSlot(0);

                if (slot != null && slot.amount > num)
                {
                    slot.amount = num;
                }
            }
        }

        private static void SetMaxStackSize(LiquidContainer lc, int num)
        {
            lc.maxStackSize = num;
            lc.inventory.maxStackSize = num;
            lc.inventory.MarkDirty();
            // lc.MarkDirtyForceUpdateOutputs();
            lc.SendNetworkUpdateImmediate();
        }

        private void ConfigureWaterDefinitions(bool state)
        {
            foreach (var itemConfig in _itemConfigs.Values)
            {
                ItemDefinition def = ItemManager.FindItemDefinition(itemConfig.Shortname);
                if (def != null && def.TryGetComponent<ItemModContainer>(out var container))
                {
                    if (state && itemConfig.ConfigMaxStackSize > 0)
                    {
                        container.maxStackSize = itemConfig.ConfigMaxStackSize;
                    }
                    else if (!state && itemConfig.DefaultMaxStackSize > 0)
                    {
                        container.maxStackSize = itemConfig.DefaultMaxStackSize;
                    }
                }
            }
        }

        private void UpdateWaterBarrel(LiquidContainer lc, bool state, EntityConfig entityConfig)
        {
            if (entityConfig.ConfigMaxStackSize > 0)
            {
                int num = state ? entityConfig.ConfigMaxStackSize : entityConfig.DefaultMaxStackSize;

                if (num > 0)
                {
                    SetMaxStackSize(lc, num);

                    SetSlotAmounts(lc, num);
                }
            }
        }

        private void UpdateWaterCatcher(WaterCatcher wc, bool state, EntityConfig entityConfig)
        {
            if (wc == null) return;

            if (state)
            {
                if (entityConfig.ConfigMaxStackSize > 0)
                {
                    wc.maxStackSize = wc.inventory.maxStackSize = entityConfig.ConfigMaxStackSize;
                }
                if (entityConfig.MaxItemToCreate > 0)
                {
                    wc.maxItemToCreate = entityConfig.MaxItemToCreate;
                }

                SetSlotAmounts(wc, wc.maxStackSize);

                if (entityConfig.Interval > 0)
                {
                    wc.Invoke(() =>
                    {
                        if (wc.IsDestroyed) return;
                        wc.CancelInvoke(wc.CollectWater);
                        wc.InvokeRepeating(wc.CollectWater, entityConfig.Interval, entityConfig.Interval);
                    }, 0.1f);
                }
            }
            else
            {
                if (entityConfig.ConfigMaxStackSize > 0 && entityConfig.DefaultMaxStackSize > 0)
                {
                    wc.maxStackSize = wc.inventory.maxStackSize = entityConfig.DefaultMaxStackSize;
                }
                if (entityConfig.MaxItemToCreate > 0 && entityConfig.DefaultMaxItemToCreate > 0)
                {
                    wc.maxItemToCreate = entityConfig.DefaultMaxItemToCreate;
                }

                SetSlotAmounts(wc, wc.maxStackSize);

                if (entityConfig.Interval > 0)
                {
                    wc.Invoke(() =>
                    {
                        if (wc.IsDestroyed) return;
                        wc.CancelInvoke(wc.CollectWater);
                        wc.InvokeRandomized(wc.CollectWater, WaterCatcher.collectInterval, WaterCatcher.collectInterval, 6f);
                    }, 0.1f);
                }
            }
        }

        private void UpdatePoweredWaterPurifier(LiquidContainer lc, bool state, EntityConfig entityConfig)
        {
            if (entityConfig.ConfigMaxStackSize > 0)
            {
                int num = state ? entityConfig.ConfigMaxStackSize : entityConfig.DefaultMaxStackSize;

                if (num > 0)
                {
                    SetMaxStackSize(lc, num);

                    SetSlotAmounts(lc, num);
                }
            }
        }

        private void UpdateWaterPurifier(WaterPurifier purifier, bool state, EntityConfig entityConfig)
        {
            if (purifier == null) return;

            if (state && entityConfig.ConfigMaxStackSize > 0)
            {
                purifier.stopWhenOutputFull = true;
                purifier.waterStorage.maxStackSize = entityConfig.ConfigMaxStackSize;
                purifier.waterStorage.MarkDirtyForceUpdateOutputs();

                SetMaxStackSize(purifier, entityConfig.ConfigMaxStackSize);
            }
            else if (!state && entityConfig.DefaultMaxStackSize > 0)
            {
                purifier.waterStorage.maxStackSize = entityConfig.DefaultMaxStackSize;
                purifier.waterStorage.MarkDirtyForceUpdateOutputs();

                SetMaxStackSize(purifier, entityConfig.DefaultMaxStackSize);
            }
        }

        private void UpdateWaterPump(WaterPump pump, bool state, EntityConfig entityConfig)
        {
            if (pump == null) return;

            if (state && entityConfig.ConfigMaxStackSize > 0)
            {
                if (entityConfig.PumpInterval > 0)
                {
                    pump.PumpInterval = entityConfig.PumpInterval;
                }

                if (entityConfig.AmountPerPump > 0)
                {
                    pump.AmountPerPump = entityConfig.AmountPerPump;
                }

                if (entityConfig.ConfigMaxStackSize > 0)
                {
                    SetMaxStackSize(pump, entityConfig.ConfigMaxStackSize);

                    SetSlotAmounts(pump, entityConfig.ConfigMaxStackSize);
                }

                //Puts("Modified water pump ({0} maxStackSize, {1} interval, {2} amount per pump)", pump.maxStackSize, pump.PumpInterval, pump.AmountPerPump);

                if (!pump.IsPowered())
                {
                    pump.CancelInvoke(pump.CreateWater);
                    return;
                }

                pump.CancelInvoke(pump.CreateWater);
                pump.InvokeRandomized(pump.CreateWater, pump.PumpInterval, pump.PumpInterval, pump.PumpInterval * 0.1f);
            }
            else if (!state)
            {
                if (entityConfig.DefaultPumpInterval > 0)
                {
                    pump.PumpInterval = entityConfig.DefaultPumpInterval;
                }

                if (entityConfig.DefaultAmountPerPump > 0)
                {
                    pump.AmountPerPump = entityConfig.DefaultAmountPerPump;
                }

                if (entityConfig.DefaultMaxStackSize > 0)
                {
                    SetMaxStackSize(pump, entityConfig.DefaultMaxStackSize);

                    SetSlotAmounts(pump, entityConfig.DefaultMaxStackSize);
                }

                //Puts("Reverted water pump ({0} maxStackSize, {1} interval, {2} amount per pump)", pump.maxStackSize, pump.PumpInterval, pump.AmountPerPump);

                if (pump.IsPowered())
                {
                    pump.CancelInvoke(pump.CreateWater);
                    pump.InvokeRandomized(pump.CreateWater, pump.PumpInterval, pump.PumpInterval, pump.PumpInterval * 0.1f);
                }
            }
        }

        private void UpdateGroundPool(LiquidContainer lc, bool state, EntityConfig entityConfig)
        {
            if (entityConfig.ConfigMaxStackSize > 0)
            {
                int num = state ? entityConfig.ConfigMaxStackSize : entityConfig.DefaultMaxStackSize;

                if (num > 0)
                {
                    SetMaxStackSize(lc, num);

                    SetSlotAmounts(lc, num);
                }
            }
        }

        private void UpdateBigGroundPool(LiquidContainer lc, bool state, EntityConfig entityConfig)
        {
            if (entityConfig.ConfigMaxStackSize > 0)
            {
                int num = state ? entityConfig.ConfigMaxStackSize : entityConfig.DefaultMaxStackSize;

                if (num > 0)
                {
                    SetMaxStackSize(lc, num);

                    SetSlotAmounts(lc, num);
                }
            }
        }

        #region Configuration

        private Configuration config;

        private int GetConfigValue(string shortname) => shortname switch
        {
            "bucket.water" => config.WaterBucket,
            "waterjug" => config.WaterJug,
            "pistol.water" => config.WaterPistol,
            "gun.water" => config.WaterGun,
            "smallwaterbottle" => config.SmallWaterBottle,
            "botabag" => config.BotaBag,
            _ => 0
        };

        private class SmallWaterCatcherSettings
        {
            [JsonProperty(PropertyName = "Stack Size (vanilla: 10000)")]
            public int StackSize { get; set; } = 50000;

            [JsonProperty(PropertyName = "Add Amount Of Water (vanilla: 10)")]
            public float MaxItemToCreate { get; set; } = 20f;

            [JsonProperty(PropertyName = "Add Water Every X Seconds (vanilla: 60)")]
            public float Interval { get; set; } = 30f;
        }

        private class LargeWaterCatcherSettings
        {
            [JsonProperty(PropertyName = "Stack Size (vanilla: 50000)")]
            public int StackSize { get; set; } = 250000;

            [JsonProperty(PropertyName = "Add Amount Of Water (vanilla: 30)")]
            public float MaxItemToCreate { get; set; } = 60f;

            [JsonProperty(PropertyName = "Add Water Every X Seconds (vanilla: 60)")]
            public float Interval { get; set; } = 30f;
        }

        private class WaterPumpSettings
        {
            [JsonProperty(PropertyName = "ML Capacity (vanilla: 2000)")]
            public int StackSize { get; set; } = 10000;

            [JsonProperty(PropertyName = "Add Amount Of Water To Water Pumps (Vanilla: 85)")]
            public int Amount { get; set; } = 130;

            [JsonProperty(PropertyName = "Add Water To Water Pumps Every X Seconds (Vanilla: 10)")]
            public float Interval { get; set; } = 10f;
        }

        private class Configuration
        {
            [JsonProperty(PropertyName = "Small Water Catcher")]
            public SmallWaterCatcherSettings SmallWaterCatcher { get; set; } = new();

            [JsonProperty(PropertyName = "Large Water Catcher")]
            public LargeWaterCatcherSettings LargeWaterCatcher { get; set; } = new();

            [JsonProperty(PropertyName = "Water Pump")]
            public WaterPumpSettings WaterPump { get; set; } = new();

            [JsonProperty(PropertyName = "Water Jug ML Capacity (vanilla: 5000)")]
            public int WaterJug { get; set; } = 25000;

            [JsonProperty(PropertyName = "Water Barrel ML Capacity (vanilla: 20000)")]
            public int WaterBarrel { get; set; } = 100000;

            [JsonProperty(PropertyName = "Water Bucket ML Capacity (vanilla: 2000)")]
            public int WaterBucket { get; set; } = 10000;

            [JsonProperty(PropertyName = "Water Ground Pool Capacity (vanilla: 500)")]
            public int GroundPool { get; set; } = 500;

            [JsonProperty(PropertyName = "Water Big Ground Pool Capacity (vanilla: 2000)")]
            public int BigGroundPool { get; set; } = 2000;

            [JsonProperty(PropertyName = "Water Purifier ML Capacity (vanilla: 5000)")]
            public int WaterPurifier { get; set; } = 25000;

            [JsonProperty(PropertyName = "Water Purifier (Powered) ML Capacity (vanilla: 5000)")]
            public int PoweredWaterPurifier { get; set; } = 25000;

            [JsonProperty(PropertyName = "Water Pistol ML Capacity (vanilla: 250)")]
            public int WaterPistol { get; set; } = 500;

            [JsonProperty(PropertyName = "Water Gun ML Capacity (vanilla: 1000)")]
            public int WaterGun { get; set; } = 2000;

            [JsonProperty(PropertyName = "Small Water Bottle ML Capacity (vanilla: 250)")]
            public int SmallWaterBottle { get; set; } = 1000;

            [JsonProperty(PropertyName = "Bota Bag ML Capacity (default: 3000)")]
            public int BotaBag { get; set; } = 3000;

            [JsonProperty(PropertyName = "Reset To Vanilla Defaults On Unload")]
            public bool Reset { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            canSaveConfig = false;
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                canSaveConfig = true;
                SaveConfig();
            }
            catch (Exception ex)
            {
                Puts(ex.ToString());
                LoadDefaultConfig();
            }
        }

        private bool canSaveConfig = true;

        protected override void SaveConfig()
        {
            if (canSaveConfig)
            {
                Config.WriteObject(config);
            }
        }

        protected override void LoadDefaultConfig() => config = new();

        #endregion

        #region Configuration Classes

        private class ItemConfig
        {
            public string Shortname { get; set; }
            public int ItemId { get; set; }
            public int DefaultMaxStackSize { get; set; }
            public int ConfigMaxStackSize { get; set; }
        }

        private class EntityConfig
        {
            public string ShortPrefabName { get; set; }
            public int DefaultMaxStackSize { get; set; }
            public int ConfigMaxStackSize { get; set; }

            // For Water Catchers
            public float DefaultMaxItemToCreate { get; set; } = 10f; // Vanilla value
            public float MaxItemToCreate { get; set; }
            public float Interval { get; set; }

            // For Water Pumps
            public float DefaultPumpInterval { get; set; } = 10f; // Vanilla value
            public float PumpInterval { get; set; }
            public int DefaultAmountPerPump { get; set; } = 85; // Vanilla value
            public int AmountPerPump { get; set; }
        }

        #endregion
    }
}