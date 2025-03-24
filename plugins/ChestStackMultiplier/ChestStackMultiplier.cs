using System.Collections.Generic;
using System.ComponentModel;

using Newtonsoft.Json;
using UnityEngine;

using Pool = Facepunch.Pool;

namespace Oxide.Plugins
{
    [Info("Chest Stack Multiplier", "MON@H", "1.6.1")]
    [Description("Higher stack sizes in storage containers.")]

    public class ChestStackMultiplier : RustPlugin
    {
        #region Variables

        private const string PermissionUseShift = "cheststackmultiplier.useshift";

        private static readonly object _true = true;

        private readonly Hash<ulong, float> _cacheMultipliers = new();
        private readonly HashSet<ulong> _cacheBackpackContainers = new();
        private readonly HashSet<ulong> _cacheBackpackEntities = new();
        private ItemContainer _targetContainer;
        private uint _backpackPrefabID;
        private uint _playerPrefabID;

        #endregion Variables

        #region Initialization

        private void Init() => HooksUnsubscribe();

        private void OnServerInitialized()
        {
            RegisterPermissions();
            CachePrefabIDs();
            CacheMultipliers();
            HooksSubscribe();
        }

        #endregion Initialization

        #region Configuration

        private PluginConfig _pluginConfig;

        public class PluginConfig
        {
            [JsonProperty(PropertyName = "Default Multiplier for new containers")]
            [DefaultValue(1f)]
            public float DefaultMultiplier { get; set; }

            [JsonProperty(PropertyName = "Containers list (PrefabName: multiplier)")]
            public SortedDictionary<string, float> ContainerMultipliers { get; set; }
        }

        protected override void LoadDefaultConfig() => PrintWarning("Loading Default Config");

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        public PluginConfig AdditionalConfig(PluginConfig config)
        {
            if (config.DefaultMultiplier <= 0)
            {
                PrintWarning("LoadConfig: Default Multiplier can't be less than or equal to 0, resetting to 1");
                config.DefaultMultiplier = 1f;
            }
            if (config.ContainerMultipliers == null)
            {
                config.ContainerMultipliers = new();
                foreach (ItemDefinition def in ItemManager.GetItemDefinitions())
                {
                    BoxStorage entity = def.GetComponent<ItemModDeployable>()?.entityPrefab.Get().GetComponent<BoxStorage>();
                    if (!entity || config.ContainerMultipliers.ContainsKey(entity.PrefabName))
                    {
                        continue;
                    }

                    config.ContainerMultipliers[entity.PrefabName] = config.DefaultMultiplier;
                }
            }

            List<string> invalidValues = Pool.GetList<string>();
            foreach (KeyValuePair<string, float> containerMultiplier in config.ContainerMultipliers)
            {
                if (containerMultiplier.Value > 0)
                {
                    continue;
                }

                PrintWarning($"LoadConfig: {containerMultiplier.Key} Multiplier can't be less than or equal to 0, resetting to default");
                invalidValues.Add(containerMultiplier.Key);
            }
            foreach (string invalidValue in invalidValues)
            {
                config.ContainerMultipliers[invalidValue] = config.DefaultMultiplier;
            }
            Pool.FreeList(ref invalidValues);
            return config;
        }

        #endregion Configuration

        #region Oxide Hooks

        private object OnMaxStackable(Item item)
        {
            if (item.info.stackable == 1 || item.info.itemType == ItemContainer.ContentsType.Liquid)
            {
                return null;
            }

            BaseEntity entity;

            if (_targetContainer != null)
            {
                entity = _targetContainer.GetEntityOwner() ?? _targetContainer.GetOwnerPlayer();
                if (entity.IsValid())
                {
                    _targetContainer = null;
                    float stackMultiplier = GetStackMultiplier(entity);
                    if (stackMultiplier == 1f)
                    {
                        return null;
                    }

                    return Mathf.FloorToInt(stackMultiplier * item.info.stackable);
                }
            }

            entity = item.GetEntityOwner() ?? item.GetOwnerPlayer();

            if (entity.IsValid())
            {
                float stackMultiplier;

                if (entity.prefabID == _playerPrefabID && !item.parent.HasFlag(ItemContainer.Flag.IsPlayer))
                {
                    stackMultiplier = _cacheMultipliers[_backpackPrefabID];
                }
                else
                {
                    stackMultiplier = GetStackMultiplier(entity);
                }

                if (stackMultiplier != 1f)
                {
                    return Mathf.FloorToInt(stackMultiplier * item.info.stackable);
                }
            }

            return null;
        }

        private object CanMoveItem(Item movedItem, PlayerInventory playerInventory, ItemContainerId targetContainerID, int targetSlot, int amount)
        {
            if (movedItem == null || playerInventory == null)
            {
                return null;
            }

            BasePlayer player = playerInventory.baseEntity;
            if (!player.IsValid())
            {
                return null;
            }

            BaseEntity sourceEntity = movedItem.GetEntityOwner() ?? movedItem.GetOwnerPlayer();
            if (IsExcluded(sourceEntity, player))
            {
                return null;
            }

            if (targetContainerID.Value == 0)
            {//Moving From Player Inventory
                if (sourceEntity == player)
                {
                    if (playerInventory.loot.containers.Count > 0)
                    {
                        targetContainerID.Value = playerInventory.loot.containers[0].uid.Value;
                        //Puts($"Moving item {movedItem} into looting container {targetContainerID}");
                    }
                    else
                    {
                        return null;
                        //targetContainerID = player.GetIdealContainer(player, movedItem, false);
                        //Puts($"Moving item {movedItem} to another player inventory container {targetContainerID}");
                    }
                }
                else if (sourceEntity == playerInventory.loot.entitySource)
                {
                    targetContainerID = playerInventory.containerMain.uid;
                    //Puts($"Moving item {movedItem} into player inventory from container {targetContainerID}");
                }
            }

            ItemContainer targetContainer = playerInventory.FindContainer(targetContainerID);
            if (targetContainer == null)
            {
                return null;
            }

            BaseEntity targetEntity = targetContainer.GetEntityOwner() ?? targetContainer.GetOwnerPlayer();

            if (sourceEntity == targetEntity || IsExcluded(targetEntity, player))
            {

                return null;
            }

            ItemContainer lootContainer = playerInventory.loot?.FindContainer(targetContainerID);
            _targetContainer = targetContainer;

            //Puts($"TargetSlot {targetSlot} Amount {amount} TargetContainer {targetContainerID}");
            // Right-Click Overstack into Player Inventory
            if (targetSlot == -1)
            {
                if (lootContainer == null)
                {
                    if (movedItem.amount > movedItem.info.stackable)
                    {
                        int loops = 1;
                        if (IsUsingShift(player))
                        {
                            loops = Mathf.CeilToInt((float)movedItem.amount / movedItem.info.stackable);
                        }
                        for (int i = 0; i < loops; i++)
                        {
                            if (movedItem.amount <= movedItem.info.stackable)
                            {
                                if (targetContainer != null)
                                {
                                    movedItem.MoveToContainer(targetContainer);
                                }
                                else
                                {
                                    playerInventory.GiveItem(movedItem);
                                }
                                break;
                            }
                            Item itemToMove = movedItem.SplitItem(movedItem.info.stackable);
                            bool moved;

                            if (targetContainer != null)
                            {
                                moved = itemToMove.MoveToContainer(targetContainer, targetSlot);
                            }
                            else
                            {
                                moved = playerInventory.GiveItem(itemToMove);
                            }
                            if (moved == false)
                            {
                                movedItem.amount += itemToMove.amount;
                                itemToMove.Remove();
                                break;
                            }
                            movedItem?.MarkDirty();
                        }
                        playerInventory.ServerUpdate(0f);
                        return _true;
                    }
                }
                // Shift Right click into storage container
                else
                {
                    if (IsUsingShift(player))
                    {
                        //Puts($"Shift Right click into storage container {lootContainer}");
                        List<Item> itemsToMove = Pool.GetList<Item>();
                        int i = 0;
                        foreach (Item item in playerInventory.containerMain.itemList)
                        {
                            if (item.info.itemid == movedItem.info.itemid && item != movedItem)
                            {
                                itemsToMove.Add(item);
                            }
                        }
                        foreach (Item item in playerInventory.containerBelt.itemList)
                        {
                            if (item.info.itemid == movedItem.info.itemid && item != movedItem)
                            {
                                itemsToMove.Add(item);
                            }
                        }
                        foreach (Item item in itemsToMove)
                        {
                            if (!item.MoveToContainer(lootContainer))
                            {
                                break;
                            }
                            i++;
                        }
                        Pool.FreeList(ref itemsToMove);
                        if (i > 0)
                        {
                            playerInventory.ServerUpdate(0f);
                            return null;
                        }
                    }
                }
            }
            // Moving Overstacks Around In Chest
            if (amount > movedItem.info.stackable && lootContainer != null)
            {
                Item targetItem = targetContainer.GetSlot(targetSlot);
                if (targetItem == null)
                {// Split item into chest
                    if (amount < movedItem.amount)
                    {
                        ItemHelper.SplitMoveItem(movedItem, amount, targetContainer, targetSlot);
                    }
                    else
                    {// Moving items when amount > info.stacksize
                        movedItem.MoveToContainer(targetContainer, targetSlot);
                    }
                }
                else
                {
                    if (!targetItem.CanStack(movedItem) && amount == movedItem.amount)
                    {// Swapping positions of items
                        ItemHelper.SwapItems(movedItem, targetItem);
                    }
                    else
                    {
                        if (amount < movedItem.amount)
                        {
                            ItemHelper.SplitMoveItem(movedItem, amount, playerInventory);
                        }
                        else
                        {
                            movedItem.MoveToContainer(targetContainer, targetSlot);
                        }
                        // Stacking items when amount > info.stacksize
                    }
                }
                playerInventory.ServerUpdate(0f);
                return _true;
            }
            // Prevent Moving Overstacks To Inventory
            if (lootContainer != null)
            {
                Item targetItem = targetContainer.GetSlot(targetSlot);
                if (targetItem != null)
                {
                    if (movedItem.GetOwnerPlayer() == player)
                    {
                        if (!movedItem.CanStack(targetItem))
                        {
                            if (targetItem.amount > targetItem.info.stackable)
                            {
                                return _true;
                            }
                        }
                    }
                }
            }

            return null;
        }

        // Covers dropping overstacks from chests onto the ground
        private void OnItemDropped(Item item, BaseEntity entity)
        {
            if (item == null || !entity.IsValid())
            {
                return;
            }

            item.RemoveFromContainer();
            int stackSize = item.MaxStackable();

            if (item.amount > stackSize)
            {
                int loops = Mathf.FloorToInt((float)item.amount / stackSize);
                if (loops > 20)
                {
                    return;
                }

                for (int i = 0; i < loops; i++)
                {
                    if (item.amount <= stackSize)
                    {
                        break;
                    }

                    Item splitItem = item.SplitItem(stackSize);
                    splitItem?.Drop(entity.transform.position, entity.GetDropVelocity() + Vector3Ex.Range(-1f, 1f));
                }
            }
        }

        private void OnBackpackOpened(BasePlayer player, ulong backpackOwnerID, ItemContainer backpackContainer)
        {
            try
            {
                if (backpackContainer != null && !_cacheBackpackContainers.Contains(backpackContainer.uid.Value))
                {
                    CacheAddBackpack(backpackContainer);
                }
            }
            catch (System.Exception ex)
            {
                PrintError($"OnBackpackOpened threw exception\n:{ex}");
                throw;
            }
        }

        #endregion Oxide Hooks

        #region Core Methods

        public void CachePrefabIDs()
        {
            _playerPrefabID = StringPool.Get("assets/prefabs/player/player.prefab");

            if (!_pluginConfig.ContainerMultipliers.ContainsKey("Backpack"))
            {
                _pluginConfig.ContainerMultipliers["Backpack"] = _pluginConfig.DefaultMultiplier;
                Config.WriteObject(_pluginConfig);
            }

            _backpackPrefabID = StringPool.closest;
            while (StringPool.toString.ContainsKey(_backpackPrefabID))
            {
                _backpackPrefabID++;
            }
        }

        public void CacheMultipliers()
        {
            foreach (KeyValuePair<string, float> container in _pluginConfig.ContainerMultipliers)
            {
                if (container.Key == "Backpack")
                {
                    _cacheMultipliers[_backpackPrefabID] = _pluginConfig.ContainerMultipliers["Backpack"];
                }
                else
                {
                    uint id = StringPool.Get(container.Key);

                    if (id > 0)
                    {
                        _cacheMultipliers[id] = container.Value;
                    }
                }
            }
        }

        public void CacheAddBackpack(ItemContainer itemContainer)
        {
            BaseEntity baseEntity = itemContainer.GetEntityOwner();

            if (baseEntity.IsValid() && !_cacheBackpackEntities.Contains(baseEntity.net.ID.Value))
            {
                _cacheBackpackContainers.Add(itemContainer.uid.Value);
                _cacheBackpackEntities.Add(baseEntity.net.ID.Value);
            }
        }

        public bool IsExcluded(BaseEntity entity, BasePlayer player) => !entity.IsValid() || entity.HasFlag(BaseEntity.Flags.Locked) || entity is BigWheelBettingTerminal || entity is ShopFront || entity is VendingMachine vendingMachine && !vendingMachine.PlayerBehind(player);

        public bool IsUsingShift(BasePlayer player) => permission.UserHasPermission(player.UserIDString, PermissionUseShift) && player.serverInput.IsDown(BUTTON.SPRINT);

        public class ItemHelper
        {
            public static bool SplitMoveItem(Item item, int amount, ItemContainer targetContainer, int targetSlot)
            {
                Item splitItem = item.SplitItem(amount);
                if (splitItem == null)
                {
                    return false;
                }

                if (!splitItem.MoveToContainer(targetContainer, targetSlot))
                {
                    item.amount += splitItem.amount;
                    splitItem.Remove();
                }

                return true;
            }

            public static bool SplitMoveItem(Item item, int amount, PlayerInventory inventory)
            {
                Item splitItem = item.SplitItem(amount);
                if (splitItem == null)
                {
                    return false;
                }

                if (!inventory.GiveItem(splitItem))
                {
                    item.amount += splitItem.amount;
                    splitItem.Remove();
                }

                return true;
            }

            public static void SwapItems(Item item1, Item item2)
            {
                ItemContainer container1 = item1.parent;
                ItemContainer container2 = item2.parent;
                int slot1 = item1.position;
                int slot2 = item2.position;
                item1.RemoveFromContainer();
                item2.RemoveFromContainer();
                item1.MoveToContainer(container2, slot2);
                item2.MoveToContainer(container1, slot1);
            }
        }

        public float GetStackMultiplier(BaseEntity entity)
        {
            switch (entity)
            {
                case LootContainer:
                case BaseCorpse:
                case BasePlayer:
                    return 1f;
            }

            if (_cacheBackpackEntities.Contains(entity.net.ID.Value))
            {
                return _cacheMultipliers[_backpackPrefabID];
            }

            float multiplier = _cacheMultipliers[entity.prefabID];
            if (multiplier == 0)
            {
                if (!_pluginConfig.ContainerMultipliers.TryGetValue(entity.PrefabName, out multiplier))
                {
                    multiplier = _pluginConfig.DefaultMultiplier;
                    _pluginConfig.ContainerMultipliers[entity.PrefabName] = multiplier;
                    Config.WriteObject(_pluginConfig);
                }
                _cacheMultipliers[entity.prefabID] = multiplier;
            }

            return multiplier;
        }

        #endregion Core Methods

        #region Helpers

        public void HooksUnsubscribe()
        {
            Unsubscribe(nameof(CanMoveItem));
            Unsubscribe(nameof(OnBackpackOpened));
            Unsubscribe(nameof(OnItemDropped));
            Unsubscribe(nameof(OnMaxStackable));
        }

        public void HooksSubscribe()
        {
            Subscribe(nameof(CanMoveItem));
            Subscribe(nameof(OnBackpackOpened));
            Subscribe(nameof(OnItemDropped));
            Subscribe(nameof(OnMaxStackable));
        }

        public void RegisterPermissions() => permission.RegisterPermission(PermissionUseShift, this);

        #endregion Helpers
    }
}