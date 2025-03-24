//Requires: StackModifier

using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

/*
 * Credits
 * Original Author/Creator Garsia up to 1.0.3
 * Current Maintainer Khan
 */

/*
 * Todo
 * Add checks for when they reduce the storage sizes it drops excess items or stacks them higher.
 * If they set it back to -1 i need to reset the storages back to the correct panel names etc.
 */

/*
 * This update 1.0.4
 * Seperated into 2 lists to fix slot number issues with other storages
 * Added displayname info this can be names anything if adding new ones.
 * Bug fixes
 *
 * This update 1.0.5
 * Added Backpack Support
 * Removed Additional Hook.
 * Now only relies on 1 hook to function
 *
 * Patch For Rust Update
 * 
 * Patch For Rust Update
 */

namespace Oxide.Plugins
{
    [Info("Stack Item Storage", "Khan", "1.0.8")]
    [Description("Change specific item stack capacity on specific storages.")]
    class StackItemStorage : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin StackModifier;

        private Configuration _config;
        private const string BackpackPrefab = "assets/prefabs/player/player.prefab";
        private Dictionary<ulong, int> _itemStack = new Dictionary<ulong, int>();
        private Dictionary<string, string> _include = new Dictionary<string, string>
        {
            ["assets/prefabs/player/player.prefab"] = "Backpack",
            ["assets/prefabs/deployable/bbq/bbq.deployed.prefab"] = "Barbeque",
            //["assets/bundled/prefabs/static/bbq.static.prefab"] = "Barbeque static",
            ["assets/prefabs/deployable/campfire/campfire.prefab"] = "Camp Fire",
            //["assets/bundled/prefabs/static/campfire_static.prefab"] = "Camp Fire Static",
            ["assets/prefabs/misc/casino/slotmachine/slotmachinestorage.prefab"] = "Casino SlotMachines Storage",
            ["assets/prefabs/deployable/composter/composter.prefab"] = "Composter",
            ["assets/prefabs/misc/halloween/cursed_cauldron/cursedcauldron.deployed.prefab"] = "Cursed Cauldron",
            ["assets/prefabs/deployable/dropbox/dropbox.deployed.prefab"] = "Drop Box",
            ["assets/bundled/prefabs/radtown/foodbox.prefab"] = "Food Box",
            ["assets/prefabs/deployable/fridge/fridge.deployed.prefab"] = "Fridge",
            ["assets/prefabs/misc/xmas/giftbox/giftbox_loot.prefab"] = "Gift Box",
            ["assets/prefabs/deployable/hitch & trough/hitchtrough.deployed.prefab"] = "Hitch & Trough",
            ["assets/prefabs/misc/twitch/hobobarrel/hobobarrel.deployed.prefab"] = "Hobo Barrel",
           // ["assets/bundled/prefabs/static/hobobarrel_static.prefab"] = "Hobo Barrel static",
            ["assets/prefabs/vehicle/seats/saddletest.prefab"] = "Horse Inventory", //??
            ["assets/prefabs/deployable/furnace.large/furnace.large.prefab"] = "Large Furnace",
            ["assets/prefabs/deployable/furnace/furnace.prefab"] = "Small Furnace",
            //["assets/bundled/prefabs/static/furnace_static.prefab"] = "Static Furnace",
            ["assets/prefabs/deployable/planters/planter.large.deployed.prefab"] = "Large Planter Box",
            ["assets/prefabs/deployable/planters/planter.small.deployed.prefab"] = "Small Planter Box",
            ["assets/prefabs/misc/xmas/stockings/stocking_large_deployed.prefab"] = "SUPER Stocking",
            ["assets/prefabs/misc/xmas/stockings/stocking_small_deployed.prefab"] = "Small Stocking",
            ["assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab"] = "Tool Cupboard",
            ["assets/prefabs/deployable/small stash/small_stash_deployed.prefab"] = "Small Stash",
            ["assets/prefabs/deployable/locker/locker.deployed.prefab"] = "Locker",
            ["assets/prefabs/deployable/mailbox/mailbox.deployed.prefab"] = "Mail Box",
            ["assets/prefabs/deployable/mixingtable/mixingtable.deployed.prefab"] = "Mixing Table",
            ["assets/prefabs/npc/sam_site_turret/sam_site_turret_deployed.prefab"] = "Sam Site Turret", //???
            ["assets/prefabs/npc/sam_site_turret/rocket_sam.prefab"] = "Sam Site Rocket",
            ["assets/prefabs/deployable/single shot trap/guntrap.deployed.prefab"] = "Shotgun Trap",
            ["assets/prefabs/misc/halloween/skull_fire_pit/skull_fire_pit.prefab"] = "Skull Fire Pit",
            ["assets/prefabs/deployable/oil refinery/refinery_small_deployed.prefab"] = "Small Oil Refinery", //think this is correct?? idk if it works?
            //["assets/bundled/prefabs/static/small_refinery_static.prefab"] = "Small Oil Refinery Static",
            ["assets/prefabs/deployable/survivalfishtrap/survivalfishtrap.deployed.prefab"] = "Survival Fish Trap",
            ["assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab"] = "Vending Machine",
            ["assets/prefabs/deployable/liquidbarrel/waterbarrel.prefab"] = "Water Barrel",
            ["assets/prefabs/weapons/waterbucket/waterbucket.entity.prefab"] = "Water Bucket", //??
            ["assets/prefabs/food/water jug/waterjug.entity.prefab"] = "Water Jug", //???
            ["assets/prefabs/misc/xmas/snow_machine/models/snowmachine.prefab"] = "Snow Machine",
            ["assets/prefabs/misc/halloween/pumpkin_bucket/pumpkin_basket.entity.prefab"] = "Pumpkin Basket"
        };
        private Dictionary<string, string> _includeSpecial = new Dictionary<string, string>
        {
            ["assets/prefabs/misc/halloween/coffin/coffinstorage.prefab"] = "Coffin",
            ["assets/prefabs/deployable/large wood storage/box.wooden.large.prefab"] = "Large Box",
            ["assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab"] = "Wood Box",
        };

        #endregion
        
        #region Classes
        private class ItemMaxStack
        {
            public string ShortName;
            public int MaxStackSize;
            public ItemMaxStack(string ShotName, int MaxStackSizes)
            {
                ShortName = ShotName;
                MaxStackSize = MaxStackSizes;
            }
        }
        private class Container
        {
            public string DisplayName;
            public List<ItemMaxStack> Items;
        }
        private class Container2
        {
            public string DisplayName;
            public int MaxCapacity;
            public List<ItemMaxStack> Items;
            public Container2()
            {
                MaxCapacity = -1;
            }
        }
        private class Configuration
        {
            [JsonProperty(PropertyName = "Customize Slot Sizes - Higher/Lower Storage Slots")]
            public Dictionary<string, Container2> ContainersSpecial;
            
            [JsonProperty(PropertyName = "Custom Stack Sizes per Container")]
            public Dictionary<string, Container> Containers;
        }

        #endregion

        #region Config
        private void CheckConfig()
        {
            foreach (var prefab in _includeSpecial)
            {
                string storageName = prefab.Key;

                Container2 storages;
                
                if (!_config.ContainersSpecial.TryGetValue(storageName, out storages))
                {
                    _config.ContainersSpecial[storageName] = storages = new Container2
                    {
                        DisplayName = prefab.Value
                    };
                    _config.ContainersSpecial[storageName].Items = new List<ItemMaxStack>();
                    _config.ContainersSpecial[storageName].Items.Add(new ItemMaxStack("shortname", -1));
                }
            }
         
            foreach (var prefab in _include)
            {
                string storageName = prefab.Key;

                Container storages;

                if (!_config.Containers.TryGetValue(storageName, out storages))
                {
                    _config.Containers[storageName] = storages = new Container
                    {
                        DisplayName = prefab.Value
                    };
                    _config.Containers[storageName].Items = new List<ItemMaxStack>();
                    _config.Containers[storageName].Items.Add(new ItemMaxStack("shortname", -1));
                }
            }
            
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            _config = new Configuration
            {
                ContainersSpecial = new Dictionary<string, Container2>(),
                Containers = new Dictionary<string, Container>()
            };
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (_config == null)
            {
                LoadDefaultConfig();
                SaveConfig();
            }
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion

        #region Hooks
        private void CheckStorage(StorageContainer T)
        {
            if (T == null || T.PrefabName == null) return;
            if (!_config.ContainersSpecial.ContainsKey(T.PrefabName) || _config.ContainersSpecial[T.PrefabName].MaxCapacity <= -1 ) return;
            T.inventory.capacity = _config.ContainersSpecial[T.PrefabName].MaxCapacity;
            T.panelName = "generic_resizable";
            if (T.inventory.capacity > 36)
            {
                T.panelName = "generic_resizable";
            }
            T.SendNetworkUpdate();
        }

        private void OnServerInitialized()
        {
            CheckConfig();
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (!(entity is StorageContainer)) continue;
                StorageContainer storage = (StorageContainer)entity;
                if (storage != null)
                {
                    CheckStorage(storage);
                }
            }
        }
        
        private int OnMaxStackable(Item item)
        {
            if (_itemStack.ContainsKey(item.uid.Value))
            {
                int t = _itemStack[item.uid.Value];
                _itemStack.Remove(item.uid.Value);
                return t;
            }

            if (item.parent?.entityOwner != null && item.info.itemType != ItemContainer.ContentsType.Liquid && !item.parent.HasFlag(ItemContainer.Flag.IsPlayer) && item?.GetRootContainer()?.entityOwner.PrefabName == BackpackPrefab)
            {
                if (_config.Containers.ContainsKey(BackpackPrefab))
                {
                    foreach (ItemMaxStack Item in _config.Containers[BackpackPrefab].Items)
                    {
                        if (Item.ShortName == item.info.shortname)
                        {
                            if (Item.MaxStackSize > -1)
                            {
                                item.GetRootContainer().maxStackSize = Item.MaxStackSize;
                            }
                        }
                    }
                }
            }
            
            if (item.parent != null && item.parent.entityOwner != null && item.parent.entityOwner.PrefabName != BackpackPrefab)
            {
                if (_config.Containers.ContainsKey(item.parent.entityOwner.name))
                {
                    foreach (ItemMaxStack Item in _config.Containers[item.parent.entityOwner.name].Items)
                    {
                        if (Item.ShortName == item.info.shortname)
                        {
                            if (Item.MaxStackSize > -1)
                            {
                                return Item.MaxStackSize;
                            }
                        }
                    }
                }
                if (_config.ContainersSpecial.ContainsKey(item.parent.entityOwner.name))
                {
                    foreach (ItemMaxStack Item in _config.ContainersSpecial[item.parent.entityOwner.name].Items)
                    {
                        if (Item.ShortName == item.info.shortname)
                        {
                            if (Item.MaxStackSize > -1)
                            {
                                return Item.MaxStackSize;
                            }
                        }
                    }
                }
            }

            int num = item.info.stackable;
            if (item.parent != null && item.parent.maxStackSize > 0)
            {
                num = Mathf.Min(item.parent.maxStackSize, num);
            }
            return num;
        }
        
        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            StorageContainer T = go.GetComponent<StorageContainer>();
            CheckStorage(T);
        }

        #endregion

    }
}