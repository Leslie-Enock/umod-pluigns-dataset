using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Configuration;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("The Gathering", "Razor", "2.1.0")]
    [Description("Gives players a gather multiplier for wearing certain clothes")]
    public class TheGathering : RustPlugin
    {
        public bool debug = false;
        ItemTypes itemData;
        private DynamicConfigFile ITEMDATA;
        private const string adminAllow = "thegathering.admin";
        static TheGathering _instance;
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Nope"] = "<color=#ce422b>You are not allowed to use this command</color>",
                ["Help"] = "<color=#ce422b>Error: /tg <add/remove></color>",
                ["HelpAdd"] = "<color=#ce422b>Error: /tg add <\"Item Custom Name\"> <itemID> <skinID> do not forget the quotes around your custom name.</color>",
                ["SkinIdUsed"] = "<color=#ce422b>SkinID {0} is already in use. Edit it in the datafile.</color>",
                ["ItemAdded"] = "<color=#ce422b>You just added a new item with skinID {0} to the datafile</color>",
                ["ItemRemoved"] = "<color=#ce422b>You have just removed skinID {0}</color>",
                ["ItemRemovedNoFind"] = "<color=#ce422b>We could not find skinID {0} in the datafile</color>",
                ["HelpRemove"] = "<color=#ce422b>Error: /tg remove <skinID></color>",
                ["item"] = "<color=#ce422b>You just gave yourself {0} </color>",
                ["Noitem"] = "<color=#ce422b>Could not find item {0} </color>"

            }, this);
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission(adminAllow, this);
        }
         
        void Unload() => _instance = null;

        void Init()
        {
            ITEMDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/Items");
            LoadData();
            RegisterPermissions();
            SaveItemData();
        }

        void LoadData()
        {
            try
            {
                itemData = Interface.Oxide.DataFileSystem.ReadObject<ItemTypes>(Name + "/Items");
            }
            catch
            {
                Puts("Couldn't load item data, creating new Item file");
                itemData = new ItemTypes();
            }
        }

        class ItemTypes
        {
            public Dictionary<ulong, ItemInfo> Items = new Dictionary<ulong, ItemInfo>();
        }

        class ItemInfo
        {
            public string ItemName;
            public int ItemID;
            public int RateMultiplier;
            public int SpawnChance;
            public bool SpawnInContainers;
            public float ConditionLoss;
            public List<string> LootContainers = new List<string>();
        }

        void SaveItemData()
        {
            ITEMDATA.WriteObject(itemData);
        }

        void OnLootSpawn(LootContainer container)
        {
            if (container.ShortPrefabName == "stocking_large_deployed" ||
                container.ShortPrefabName == "stocking_small_deployed") return;
            foreach (var itemsConfig in itemData.Items)
            {
                foreach (var LootContainers in itemData.Items[itemsConfig.Key].LootContainers)
                {
                    if (LootContainers.Contains(container.ShortPrefabName))
                    {
                        if (!itemData.Items[itemsConfig.Key].SpawnInContainers) return;
                        if (UnityEngine.Random.Range(0, 100) < itemData.Items[itemsConfig.Key].SpawnChance)
                        {
                            if (container.inventory.itemList.Count == container.inventory.capacity)
                            {
                                container.inventory.capacity++;
                            }
                            var theitem = itemData.Items[itemsConfig.Key].ItemID;
                            Item i = ItemManager.CreateByItemID(theitem, 1, itemsConfig.Key);
                            i.name = itemData.Items[itemsConfig.Key].ItemName;
                            i.MoveToContainer(container.inventory);
                            if (debug) PrintWarning(itemData.Items[itemsConfig.Key].ItemName + " Spawned in container " + LootContainers);
                        }
                    }
                }
            }
        }

        private bool? CanCombineDroppedItem(DroppedItem item, DroppedItem targetItem)
        {
            if (item.GetItem() == null || targetItem.GetItem() == null)
                return null;

            if (itemData.Items.ContainsKey(item.GetItem().skin))
                if (item.GetItem().skin != targetItem.GetItem().skin)
                    return false;

            return null;
        }

        private bool? CanStackItem(Item item, Item targetItem)
        {
            if (itemData.Items.ContainsKey(item.skin))
                if (item.skin != targetItem.skin)
                    return false;

            return null;
        }

        void OnCollectiblePickup(Item item, BasePlayer player, CollectibleEntity entity)
        {
            int totals = 0;
            foreach (Item customware in player.inventory.containerWear.itemList.ToList())
            {
                if (itemData.Items.ContainsKey(customware.skin))
                {
                    if (!customware.hasCondition)
                        totals = totals + itemData.Items[customware.skin].RateMultiplier;
                    else if (customware.hasCondition && customware.condition != 0)
                    {
                        totals = totals + itemData.Items[customware.skin].RateMultiplier;
                        customware.LoseCondition(itemData.Items[customware.skin].ConditionLoss);
                    }
                }
            }
            if (totals == 0) return;
            item.amount *= totals;
        }

        void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (player == null || dispenser == null || item == null) return;
            int totals = 0;
            foreach (Item customware in player.inventory.containerWear.itemList.ToList())
            {
                if (itemData.Items.ContainsKey(customware.skin))
                {
                    if (!customware.hasCondition)
                        totals = totals + itemData.Items[customware.skin].RateMultiplier;
                    else if (customware.hasCondition && customware.condition != 0)
                    {
                        totals = totals + itemData.Items[customware.skin].RateMultiplier;
                        customware.LoseCondition(itemData.Items[customware.skin].ConditionLoss);
                    }
                }
            }
            if (totals == 0) return;
            item.amount *= totals;
        }

        void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (dispenser == null || player == null) return;
            int totals = 0;
            foreach (Item customware in player.inventory.containerWear.itemList.ToList())
            {
                if (itemData.Items.ContainsKey(customware.skin))
                {
                    if (!customware.hasCondition)
                        totals = totals + itemData.Items[customware.skin].RateMultiplier;
                    else if (customware.hasCondition && customware.condition != 0)
                    {
                        totals = totals + itemData.Items[customware.skin].RateMultiplier;
                        customware.LoseCondition(itemData.Items[customware.skin].ConditionLoss);
                    }
                }
            }
            if (totals == 0) return;
            item.amount *= totals;
        }

        [ChatCommand("tg")]
        void MasterGatherItems(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, adminAllow))
            {
                SendReply(player, lang.GetMessage("Nope", this, player.UserIDString));
                return;
            }

            if (args.Length <= 0)
            {
                SendReply(player, lang.GetMessage("Help", this, player.UserIDString));
                return;
            }

            switch (args[0].ToLower())
            {
                case "add":
                    if (args.Length != 4)
                    {
                        SendReply(player, lang.GetMessage("HelpAdd", this, player.UserIDString));
                        return;
                    }

                    var ids = default(int);
                    if (!int.TryParse(args[2], out ids))
                    {
                        SendReply(player, lang.GetMessage("HelpAdd", this, player.UserIDString));
                        return;
                    }

                    var Skinids = default(ulong);
                    if (!ulong.TryParse(args[3], out Skinids))
                    {
                        SendReply(player, lang.GetMessage("HelpAdd", this, player.UserIDString));
                        return;
                    }

                    var setup = new ItemInfo { ItemName = args[1], RateMultiplier = 2, SpawnChance = 2, SpawnInContainers = false, ItemID = ids, LootContainers = { "crate_basic", "crate_normal", "crate_normal_2" } };
                    if (itemData.Items.ContainsKey(Skinids))
                    {
                        SendReply(player, lang.GetMessage("SkinIdUsed", this, player.UserIDString), Skinids.ToString());
                        return;
                    }
                    else
                    {
                        itemData.Items.Add(Skinids, setup);
                        SaveItemData();
                        SendReply(player, lang.GetMessage("ItemAdded", this, player.UserIDString), Skinids.ToString());
                    }
                    return;

                case "remove":

                    if (args.Length != 2)
                    {
                        SendReply(player, lang.GetMessage("HelpAdd", this, player.UserIDString));
                        return;
                    }
                    var Skinid = default(ulong);
                    if (!ulong.TryParse(args[1], out Skinid))
                    {
                        SendReply(player, lang.GetMessage("HelpRemove", this, player.UserIDString));
                        return;
                    }
                    if (itemData.Items.ContainsKey(Skinid))
                    {
                        itemData.Items.Remove(Skinid);
                        SaveItemData();
                        SendReply(player, lang.GetMessage("ItemRemoved", this, player.UserIDString), Skinid.ToString());
                    }
                    else
                    {
                        SendReply(player, lang.GetMessage("ItemRemovedNoFind", this, player.UserIDString), Skinid.ToString());
                    }
                    return;

                case "get":
                    if (args.Length != 2)
                    {
                        return;
                    }
                    string getItem = args[1].ToLower();

                    foreach (var itemconfig in itemData.Items.ToList())
                    {
                        if (itemconfig.Value == null)
                            continue;

                        if (itemconfig.Value.ItemName.ToLower() == getItem)
                        {
                            int itemid = itemconfig.Value.ItemID;
                            string name = itemconfig.Value.ItemName;
                            var item = ItemManager.CreateByItemID(itemid, 1, itemconfig.Key);
                            if (item == null)
                                continue;
                            item.name = name;
                            player.GiveItem(item);
                            SendReply(player, lang.GetMessage("item", this, player.UserIDString), getItem);
                            return;
                        }
                    }
                    SendReply(player, lang.GetMessage("Noitem", this, player.UserIDString), getItem);
                    return;

                    SendReply(player, lang.GetMessage("Help", this, player.UserIDString));
                default:
                    break;
            }
        }
    }
}

