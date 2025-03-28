﻿using System;
using Oxide.Core;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("HolidayLoot", "FastBurst", "1.1.7")]
    [Description("Modify the loot tables for the Christmas Presents, Halloween Bags and Easter Eggs")]

    class HolidayLoot : RustPlugin
    {
        private bool Changed;

        #region Data
        private DynamicConfigFile PresentData;
        private DynamicConfigFile PresentData2;
        private DynamicConfigFile PresentData3;
        private DynamicConfigFile HalloweenData2;
        private DynamicConfigFile HalloweenData3;
        private DynamicConfigFile EasterData2;
        private DynamicConfigFile EasterData3;
        private DynamicConfigFile SantaData;
        private StoredData storedData;
        private StoredData storedData2;
        private StoredData storedData3;
        private StoredData HalloweenStoredData2;
        private StoredData HalloweenStoredData3;
        private StoredData EasterStoredData2;
        private StoredData EasterStoredData3;
        private StoredData SantaStoredData;

        private class StoredData
        {
            public List<ItemInfo> lootTable = new List<ItemInfo>();
        }

        private void SaveData()
        {
            storedData.lootTable = smallList;
            storedData2.lootTable = mediumList;
            storedData3.lootTable = largeList;
            HalloweenStoredData2.lootTable = mediumListHalloween;
            HalloweenStoredData3.lootTable = largeListHalloween;
            EasterStoredData2.lootTable = mediumListEaster;
            EasterStoredData3.lootTable = largeListEaster;
            SantaStoredData.lootTable = santaList;


            PresentData.WriteObject(storedData);
            PresentData2.WriteObject(storedData2);
            PresentData3.WriteObject(storedData3);
            HalloweenData2.WriteObject(HalloweenStoredData2);
            HalloweenData3.WriteObject(HalloweenStoredData3);
            EasterData2.WriteObject(EasterStoredData2);
            EasterData3.WriteObject(EasterStoredData3);
            SantaData.WriteObject(SantaStoredData);
        }

        private void LoadData()
        {
            try
            {
                storedData = PresentData.ReadObject<StoredData>();
                storedData2 = PresentData2.ReadObject<StoredData>();
                storedData3 = PresentData3.ReadObject<StoredData>();
                HalloweenStoredData2 = HalloweenData2.ReadObject<StoredData>();
                HalloweenStoredData3 = HalloweenData3.ReadObject<StoredData>();
                EasterStoredData2 = EasterData2.ReadObject<StoredData>();
                EasterStoredData3 = EasterData3.ReadObject<StoredData>();
                SantaStoredData = SantaData.ReadObject<StoredData>();
                smallList = storedData.lootTable;
                mediumList = storedData2.lootTable;
                largeList = storedData3.lootTable;
                mediumListHalloween = HalloweenStoredData2.lootTable;
                largeListHalloween = HalloweenStoredData3.lootTable;
                mediumListEaster = EasterStoredData2.lootTable;
                largeListEaster = EasterStoredData3.lootTable;
                santaList = SantaStoredData.lootTable;
            }
            catch
            {
                Puts("Failed to load data, creating new file");
                storedData = new StoredData();
            }
        }

        private class ItemInfo
        {
            public string itemName;
            public int minItemAmount;
            public int maxItemAmount;
            public Dictionary<string, float> attachments;
            public float chance;
            public int skinID;
        }
        #endregion

        #region Config
        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {
            smallMinItems = Convert.ToInt16(GetConfig("Small Presents", "Min Items", 1));
            smallMaxItems = Convert.ToInt16(GetConfig("Small Presents", "Max Items", 1));
            smallPresentsToUse = Convert.ToInt16(GetConfig("Small Presents", "Num Needed To Unwrap", 1));

            mediumMinItems = Convert.ToInt16(GetConfig("Medium Presents", "Min Items", 3));
            mediumMaxItems = Convert.ToInt16(GetConfig("Medium Presents", "Max Items", 3));
            mediumPresentsToUse = Convert.ToInt16(GetConfig("Medium Presents", "Num Needed To Unwrap", 1));

            largeMinItems = Convert.ToInt16(GetConfig("Large Presents", "Min Items", 5));
            largeMaxItems = Convert.ToInt16(GetConfig("Large Presents", "Max Items", 5));
            largePresentsToUse = Convert.ToInt16(GetConfig("Large Presents", "Num Needed To Unwrap", 1));

            mediumMinBagsItems = Convert.ToInt16(GetConfig("Medium Loot Bag", "Min Items", 3));
            mediumMaxBagsItems = Convert.ToInt16(GetConfig("Medium Loot Bag", "Max Items", 3));
            mediumBagsToUse = Convert.ToInt16(GetConfig("Medium Loot Bag", "Num Needed To Unwrap", 1));

            largeMinBagsItems = Convert.ToInt16(GetConfig("Large Loot Bag", "Min Items", 5));
            largeMaxBagsItems = Convert.ToInt16(GetConfig("Large Loot Bag", "Max Items", 5));
            largeBagsToUse = Convert.ToInt16(GetConfig("Large Loot Bag", "Num Needed To Unwrap", 1));

            mediumMinEggsItems = Convert.ToInt16(GetConfig("Silver Egg", "Min Items", 3));
            mediumMaxEggsItems = Convert.ToInt16(GetConfig("Silver Egg", "Max Items", 3));
            mediumEggsToUse = Convert.ToInt16(GetConfig("Silver Egg", "Num Needed To Unwrap", 1));

            largeMinEggsItems = Convert.ToInt16(GetConfig("Gold Egg", "Min Items", 5));
            largeMaxEggsItems = Convert.ToInt16(GetConfig("Gold Egg", "Max Items", 5));
            largeEggsToUse = Convert.ToInt16(GetConfig("Gold Egg", "Num Needed To Unwrap", 1));

            santaMinItems = Convert.ToInt16(GetConfig("Santa Drop", "Min Items", 6));
            santaMaxItems = Convert.ToInt16(GetConfig("Santa Drop", "Max Items", 12));

            weaponsSpawnWithAmmo = Convert.ToBoolean(GetConfig("General", "Weapons Spawn With Random Ammo", true));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (data.TryGetValue(datavalue, out value)) return value;
            value = defaultValue;
            data[datavalue] = value;
            Changed = true;
            return value;
        }
        #endregion

        #region Vars
        private List<ItemInfo> smallList = new List<ItemInfo>();
        private List<ItemInfo> mediumList = new List<ItemInfo>();
        private List<ItemInfo> largeList = new List<ItemInfo>();
        private List<ItemInfo> mediumListHalloween = new List<ItemInfo>();
        private List<ItemInfo> largeListHalloween = new List<ItemInfo>();
        private List<ItemInfo> mediumListEaster = new List<ItemInfo>();
        private List<ItemInfo> largeListEaster = new List<ItemInfo>();
        private List<ItemInfo> santaList = new List<ItemInfo>();

        private int smallPresentsToUse = 1;
        private int mediumPresentsToUse = 1;
        private int largePresentsToUse = 1;
        private int mediumBagsToUse = 1;
        private int largeBagsToUse = 1;
        private int mediumEggsToUse = 1;
        private int largeEggsToUse = 1;
        private int SantaDropToUse = 1;
        private Dictionary<string, int> amountNeededDic = new Dictionary<string, int>();

        private int smallMinItems = 1;
        private int smallMaxItems = 4;
        private int mediumMinItems = 3;
        private int mediumMaxItems = 3;
        private int largeMinItems = 5;
        private int largeMaxItems = 5;

        private int mediumMinEggsItems = 3;
        private int mediumMaxEggsItems = 3;
        private int largeMinEggsItems = 5;
        private int largeMaxEggsItems = 5;

        private int mediumMinBagsItems = 3;
        private int mediumMaxBagsItems = 3;
        private int largeMinBagsItems = 5;
        private int largeMaxBagsItems = 5;

        private int santaMinItems = 6;
        private int santaMaxItems = 12;

        private bool weaponsSpawnWithAmmo = true;
        private bool sureItemsWillAlwaysSpawn = false;
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            PresentData = Interface.Oxide.DataFileSystem.GetFile("HolidayLoot/SmallPresents");
            PresentData2 = Interface.Oxide.DataFileSystem.GetFile("HolidayLoot/MediumPresents");
            PresentData3 = Interface.Oxide.DataFileSystem.GetFile("HolidayLoot/LargePresents");
            HalloweenData2 = Interface.Oxide.DataFileSystem.GetFile("HolidayLoot/MediumHalloweenBags");
            HalloweenData3 = Interface.Oxide.DataFileSystem.GetFile("HolidayLoot/LargeHalloweenBags");
            EasterData2 = Interface.Oxide.DataFileSystem.GetFile("HolidayLoot/SilverEggs");
            EasterData3 = Interface.Oxide.DataFileSystem.GetFile("HolidayLoot/GoldEggs");
            SantaData = Interface.Oxide.DataFileSystem.GetFile("HolidayLoot/SantaDrop");
            LoadData();
            AddDefaultItems();
            LoadVariables();
            amountNeededDic.Add("xmas.present.large", largePresentsToUse);
            amountNeededDic.Add("xmas.present.medium", mediumPresentsToUse);
            amountNeededDic.Add("xmas.present.small", smallPresentsToUse);
            amountNeededDic.Add("halloween.lootbag.large", largeBagsToUse);
            amountNeededDic.Add("halloween.lootbag.medium", mediumBagsToUse);
            amountNeededDic.Add("easter.goldegg", largeEggsToUse);
            amountNeededDic.Add("easter.silveregg", mediumEggsToUse);
            amountNeededDic.Add("presentdrop", SantaDropToUse);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //chat
                ["No Permission"] = "You are not allowed to use this command",
                ["Tables Reloaded"] = "The Holiday Loot data tables have been reloaded",
                ["NotEnoughItems"] = "You do not have enough of this item to unwrap it!",
            }, this);
        }
        #endregion

        #region Functions
        private void AddDefaultItems()
        {
            bool updated = false;

            // Christmas Present Loot
            if (smallList.Count == 0)
            {
                smallList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 4, chance = 0.20f, itemName = "chocholate", maxItemAmount = 5, skinID = 0 });
                smallList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 10, chance = 0.20f, itemName = "metal.fragments", maxItemAmount = 50, skinID = 0 });
                updated = true;
            }
            if (mediumList.Count == 0)
            {
                mediumList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 3, chance = 0.20f, itemName = "metalspring", maxItemAmount = 5, skinID = 0 });
                mediumList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 1, chance = 0.20f, itemName = "pistol.revolver", maxItemAmount = 1, skinID = 0 });
                mediumList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 1, chance = 0.20f, itemName = "stocking.small", maxItemAmount = 1, skinID = 0 });
                updated = true;
            }
            if (largeList.Count == 0)
            {
                largeList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 1, chance = 0.20f, itemName = "stocking.large", maxItemAmount = 1, skinID = 0 });
                largeList.Add(new ItemInfo { attachments = new Dictionary<string, float> { { "weapon.mod.holosight", 0.5f }, { "weapon.mod.lasersight", 0.5f } }, minItemAmount = 1, chance = 0.20f, itemName = "shotgun.pump", maxItemAmount = 1, skinID = 0 });
                largeList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 5, chance = 0.20f, itemName = "ammo.shotgun", maxItemAmount = 15, skinID = 0 });
                updated = true;
            }

            // Halloween Loot Bags
            if (mediumListHalloween.Count == 0)
            {
                mediumListHalloween.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 3, chance = 0.20f, itemName = "metalspring", maxItemAmount = 5, skinID = 0 });
                mediumListHalloween.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 1, chance = 0.20f, itemName = "pistol.revolver", maxItemAmount = 1, skinID = 0 });
                updated = true;
            }
            if (largeListHalloween.Count == 0)
            {
                largeListHalloween.Add(new ItemInfo { attachments = new Dictionary<string, float> { { "weapon.mod.holosight", 0.5f }, { "weapon.mod.lasersight", 0.5f } }, minItemAmount = 1, chance = 0.20f, itemName = "shotgun.pump", maxItemAmount = 1, skinID = 0 });
                largeListHalloween.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 5, chance = 0.20f, itemName = "ammo.shotgun", maxItemAmount = 15, skinID = 0 });
                updated = true;
            }

            // Easter Egg Loot
            if (mediumListEaster.Count == 0)
            {
                mediumListEaster.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 3, chance = 0.20f, itemName = "metalspring", maxItemAmount = 5, skinID = 0 });
                mediumListEaster.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 1, chance = 0.20f, itemName = "pistol.revolver", maxItemAmount = 1, skinID = 0 });
                updated = true;
            }
            if (largeListEaster.Count == 0)
            {
                largeListEaster.Add(new ItemInfo { attachments = new Dictionary<string, float> { { "weapon.mod.holosight", 0.5f }, { "weapon.mod.lasersight", 0.5f } }, minItemAmount = 1, chance = 0.20f, itemName = "shotgun.pump", maxItemAmount = 1, skinID = 0 });
                largeListEaster.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 5, chance = 0.20f, itemName = "ammo.shotgun", maxItemAmount = 15, skinID = 0 });
                updated = true;
            }

            //Santa Drop
            if (santaList.Count == 0)
            {
                santaList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 3, chance = 0.20f, itemName = "metalspring", maxItemAmount = 5, skinID = 0 });
                santaList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 1, chance = 0.20f, itemName = "pistol.revolver", maxItemAmount = 1, skinID = 0 });
                santaList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 1, chance = 0.20f, itemName = "metal.fragments", maxItemAmount = 150, skinID = 0 });
                santaList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 1, chance = 0.20f, itemName = "m249", maxItemAmount = 1, skinID = 0 });
                santaList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 1, chance = 0.20f, itemName = "lr300", maxItemAmount = 150, skinID = 0 });
                santaList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 1, chance = 0.20f, itemName = "metal.refined", maxItemAmount = 150, skinID = 0 });
                santaList.Add(new ItemInfo { attachments = new Dictionary<string, float> { { "weapon.mod.holosight", 0.5f }, { "weapon.mod.lasersight", 0.5f } }, minItemAmount = 1, chance = 0.20f, itemName = "shotgun.pump", maxItemAmount = 1, skinID = 0 });
                santaList.Add(new ItemInfo { attachments = new Dictionary<string, float>(), minItemAmount = 5, chance = 0.20f, itemName = "ammo.shotgun", maxItemAmount = 15, skinID = 0 });
                updated = true;
            }

            if (!updated) return;
            SaveData();
            LoadData();
        }

        private object OnItemAction(Item item, string action)
        {
            if (item == null || action == null || action == "" || item.skin != 0)
                return null;
            if (item.info.shortname != "xmas.present.large" && item.info.shortname != "xmas.present.medium" && item.info.shortname != "xmas.present.small" && item.info.shortname != "halloween.lootbag.medium" && item.info.shortname != "halloween.lootbag.large" && item.info.shortname != "easter.silveregg" && item.info.shortname != "easter.goldegg" && item.info.shortname != "presentdrop")
                return null;
            if (action != "unwrap")
                return null;
            BasePlayer player = item.GetRootContainer().GetOwnerPlayer();
            if (player == null)
                return null;
            if (!CheckAmountNeeded(item.info.shortname, player))
            {
                player.ChatMessage(msg("NotEnoughItems", player.UserIDString));
                return false;
            }
            GiveThink(player, item.info.shortname);
            ItemRemovalThink(item, player, amountNeededDic[item.info.shortname]);
            Effect.server.Run("assets/prefabs/misc/xmas/presents/effects/unwrap.prefab", player.transform.position);
            return false;
        }

        private bool CheckAmountNeeded(string itemName, BasePlayer player)
        {
            bool sufficent = false;
            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (item.info.shortname == itemName)
                    if (item.amount >= amountNeededDic[item.info.shortname])
                        sufficent = true;
            }
            foreach (Item item in player.inventory.containerWear.itemList)
            {
                if (item.info.shortname == itemName)
                    if (item.amount >= amountNeededDic[item.info.shortname])
                        sufficent = true;
            }
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (item.info.shortname == itemName)
                    if (item.amount >= amountNeededDic[item.info.shortname])
                        sufficent = true;
            }
            return sufficent;
        }

        private void GiveThink(BasePlayer player, string presentName)
        {
            foreach (Item newItem in CreateItems(presentName))
            {
                if (newItem == null)
                {
                    Puts("Item was null");
                    continue;
                }
                if (player.inventory.containerMain.IsFull())
                    newItem.Drop(player.transform.position, Vector3.down);
                else
                    newItem.MoveToContainer(player.inventory.containerMain);
            }
        }

        private List<Item> CreateItems(string presentname)
        {
            List<Item> x = new List<Item>();
            int itemCount = 0;
            switch (presentname)
            {
                case "xmas.present.small":
                    {
                        if (sureItemsWillAlwaysSpawn)
                            foreach (var entry in CheckFor100Items(smallList))
                            {
                                x.Add(entry);
                                itemCount++;
                            }
                        int maxItemAmount = Mathf.RoundToInt(UnityEngine.Random.Range(Convert.ToSingle(smallMinItems), Convert.ToSingle(smallMaxItems)));
                        while (itemCount < maxItemAmount)
                        {
                            Item item = TryMakeItem(smallList);
                            if (item == null)
                                continue;
                            x.Add(item);
                            itemCount++;
                        }
                        return x;
                    }

                case "xmas.present.medium":
                    {
                        if (sureItemsWillAlwaysSpawn)
                            foreach (var entry in CheckFor100Items(mediumList))
                            {
                                x.Add(entry);
                                itemCount++;
                            }
                        int maxItemAmount = Mathf.RoundToInt(UnityEngine.Random.Range(Convert.ToSingle(mediumMinItems), Convert.ToSingle(mediumMaxItems)));
                        while (itemCount < maxItemAmount)
                        {
                            Item item = TryMakeItem(mediumList);
                            if (item == null)
                                continue;
                            x.Add(item);
                            itemCount++;
                        }
                        return x;
                    }

                case "xmas.present.large":
                    {
                        if (sureItemsWillAlwaysSpawn)
                            foreach (var entry in CheckFor100Items(largeList))
                            {
                                x.Add(entry);
                                itemCount++;
                            }
                        int maxItemAmount = Mathf.RoundToInt(UnityEngine.Random.Range(Convert.ToSingle(largeMinItems), Convert.ToSingle(largeMaxItems)));
                        while (itemCount < maxItemAmount)
                        {
                            Item item = TryMakeItem(largeList);
                            if (item == null)
                                continue;
                            x.Add(item);
                            itemCount++;
                        }
                        return x;
                    }
                case "halloween.lootbag.medium":
                    {
                        if (sureItemsWillAlwaysSpawn)
                            foreach (var entry in CheckFor100Items(mediumListHalloween))
                            {
                                x.Add(entry);
                                itemCount++;
                            }
                        int maxItemAmount = Mathf.RoundToInt(UnityEngine.Random.Range(Convert.ToSingle(largeMinItems), Convert.ToSingle(mediumMaxBagsItems)));
                        while (itemCount < maxItemAmount)
                        {
                            Item item = TryMakeItem(mediumListHalloween);
                            if (item == null)
                                continue;
                            x.Add(item);
                            itemCount++;
                        }
                        return x;
                    }
                case "halloween.lootbag.large":
                    {
                        if (sureItemsWillAlwaysSpawn)
                            foreach (var entry in CheckFor100Items(largeListHalloween))
                            {
                                x.Add(entry);
                                itemCount++;
                            }
                        int maxItemAmount = Mathf.RoundToInt(UnityEngine.Random.Range(Convert.ToSingle(largeMinItems), Convert.ToSingle(largeMaxBagsItems)));
                        while (itemCount < maxItemAmount)
                        {
                            Item item = TryMakeItem(largeListHalloween);
                            if (item == null)
                                continue;
                            x.Add(item);
                            itemCount++;
                        }
                        return x;
                    }
                case "easter.silveregg":
                    {
                        if (sureItemsWillAlwaysSpawn)
                            foreach (var entry in CheckFor100Items(mediumListEaster))
                            {
                                x.Add(entry);
                                itemCount++;
                            }
                        int maxItemAmount = Mathf.RoundToInt(UnityEngine.Random.Range(Convert.ToSingle(largeMinItems), Convert.ToSingle(mediumMaxEggsItems)));
                        while (itemCount < maxItemAmount)
                        {
                            Item item = TryMakeItem(mediumListEaster);
                            if (item == null)
                                continue;
                            x.Add(item);
                            itemCount++;
                        }
                        return x;
                    }
                case "easter.goldegg":
                    {
                        if (sureItemsWillAlwaysSpawn)
                            foreach (var entry in CheckFor100Items(largeListEaster))
                            {
                                x.Add(entry);
                                itemCount++;
                            }
                        int maxItemAmount = Mathf.RoundToInt(UnityEngine.Random.Range(Convert.ToSingle(largeMinItems), Convert.ToSingle(largeMaxEggsItems)));
                        while (itemCount < maxItemAmount)
                        {
                            Item item = TryMakeItem(largeListEaster);
                            if (item == null)
                                continue;
                            x.Add(item);
                            itemCount++;
                        }
                        return x;
                    }
                case "presentdrop":
                    {
                        if (sureItemsWillAlwaysSpawn)
                            foreach (var entry in CheckFor100Items(santaList))
                            {
                                x.Add(entry);
                                itemCount++;
                            }
                        int maxItemAmount = Mathf.RoundToInt(UnityEngine.Random.Range(Convert.ToSingle(santaMinItems), Convert.ToSingle(santaMaxItems)));
                        while (itemCount < maxItemAmount)
                        {
                            Item item = TryMakeItem(santaList);
                            if (item == null)
                                continue;
                            x.Add(item);
                            itemCount++;
                        }
                        return x;
                    }
            }
            return x;
        }

        private Item TryMakeItem(List<ItemInfo> list)
        {
            while (true)
            {
                ItemInfo entry = list[Mathf.RoundToInt(UnityEngine.Random.Range(0f, Convert.ToSingle(list.Count - 1)))];
                if (UnityEngine.Random.Range(0f, 1f) > entry.chance)
                    continue;
                Item newItem = ItemManager.CreateByName(entry.itemName, UnityEngine.Random.Range(entry.minItemAmount, entry.maxItemAmount), (ulong)entry.skinID);
                newItem.MarkDirty();
                if (newItem == null)
                {
                    Puts($"An item could not be created successfully {entry.itemName}");
                    continue;
                }
                foreach (var attachmentName in entry.attachments)
                    if (UnityEngine.Random.Range(0f, 1f) < attachmentName.Value)
                    {
                        Item attachment = ItemManager.CreateByName(attachmentName.Key);
                        if (attachment == null)
                        {
                            Puts($"An item could not be created successfully {entry.itemName}");
                            continue;
                        }
                        attachment.MoveToContainer(newItem.contents);
                    }
                if (newItem.info.category != ItemCategory.Weapon) return newItem;
                BaseProjectile ent = newItem.GetHeldEntity()?.GetComponent<BaseProjectile>();
                if (ent == null)
                    return newItem;
                if (ent.primaryMagazine != null)
                    ent.primaryMagazine.contents = ent.primaryMagazine.capacity;
                return newItem;
            }
        }

        private List<Item> CheckFor100Items(List<ItemInfo> list)
        {
            List<Item> x = new List<Item>();
            foreach (var entry in list)
            {
                if (entry.chance <= 0.99f)
                    continue;
                Item newItem = ItemManager.CreateByName(entry.itemName, UnityEngine.Random.Range(entry.minItemAmount, entry.maxItemAmount), (ulong)entry.skinID);
                newItem.MarkDirty();
                if (newItem == null)
                {
                    Puts($"An item could not be created successfully {entry.itemName}");
                    continue;
                }
                foreach (var attachmentName in entry.attachments)
                    if (UnityEngine.Random.Range(0f, 1f) < attachmentName.Value)
                    {
                        Item attachment = ItemManager.CreateByName(attachmentName.Key);
                        if (attachment == null)
                        {
                            Puts($"An item could not be created successfully {entry.itemName}");
                            continue;
                        }
                        attachment.MoveToContainer(newItem.contents);
                    }
                if (newItem.info.category != ItemCategory.Weapon)
                {
                    x.Add(newItem);
                    continue;
                }
                BaseProjectile ent = newItem.GetHeldEntity()?.GetComponent<BaseProjectile>();
                if (ent == null) continue;
                if (ent.primaryMagazine != null)
                    ent.primaryMagazine.contents = ent.primaryMagazine.capacity;
                x.Add(newItem);
            }
            return x;
        }

        private static void ItemRemovalThink(Item item, BasePlayer player, int itemsToTake)
        {
            if (item.amount == itemsToTake)
            {
                item.RemoveFromContainer();
                item.Remove();
            }
            else
            {
                item.amount = item.amount - itemsToTake;
                player.inventory.SendSnapshot();
            }
        }

        private string msg(string key, string id = null) { return lang.GetMessage(key, this, id); }
        #endregion
    }
}