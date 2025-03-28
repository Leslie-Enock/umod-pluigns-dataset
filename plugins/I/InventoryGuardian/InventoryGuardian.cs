﻿using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("InventoryGuardian", "k1lly0u", "0.2.7"), Description("Restore players inventory even after server wipe")]
    class InventoryGuardian : RustPlugin
    {
        #region Fields
        private IGData igData;
        private DynamicConfigFile Inventory_Data;

        private Dictionary<ulong, PlayerInfo> cachedInventories = new Dictionary<ulong, PlayerInfo>();
        private bool isNewSave;
        #endregion

        #region Oxide Hooks
        private void Loaded() => Inventory_Data = Interface.Oxide.DataFileSystem.GetFile("Inventory-Guardian");

        private void OnServerInitialized()
        {
            LoadVariables();
            LoadData();
            RegisterPermisions();
            CheckProtocol();
            SaveLoop();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        private void OnNewSave(string filename) => isNewSave = true;

        private void OnPlayerConnected(BasePlayer player)
        {
            if (igData.IsActivated)
                if (cachedInventories.ContainsKey(player.userID))
                    if (cachedInventories[player.userID].RestoreOnce)
                    {
                        RestoreInventory(player);
                        RemoveInventory(player);
                    }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (igData.IsActivated)
                SaveInventory(player);
        }

        private void Unload() => SaveData();
        #endregion

        #region Functions
        private void CheckProtocol()
        {
            if (igData.AutoRestore && isNewSave)
            {
                foreach (KeyValuePair<ulong, PlayerInfo> entry in cachedInventories)
                    entry.Value.RestoreOnce = true;
                Puts("Map wipe detected! Activating Auto Restore for all saved inventories");
            }
        }

        private void RestoreAll()
        {
            foreach (BasePlayer player in BasePlayer.allPlayerList)
                RestoreInventory(player);            
            SaveData();
        }

        private void SaveAll()
        {
            foreach (var player in BasePlayer.activePlayerList)
                SaveInventory(player);
            foreach (var player in BasePlayer.sleepingPlayerList)
                SaveInventory(player);
            SaveData();
        }

        private void RemoveAll()
        {
            cachedInventories.Clear();
            SaveData();
        }

        private BasePlayer FindPlayer(BasePlayer player, string arg)
        {
            List<BasePlayer> foundPlayers = new List<BasePlayer>();
            ulong steamid;
            ulong.TryParse(arg, out steamid);
            string lowerarg = arg.ToLower();

            foreach (BasePlayer p in BasePlayer.activePlayerList)
            {
                if (p != null)
                {
                    if (steamid != 0L)
                        if (p.userID == steamid) return p;
                    string lowername = p.displayName.ToLower();
                    if (lowername.Contains(lowerarg))
                    {
                        foundPlayers.Add(p);
                    }
                }
            }

            if (foundPlayers.Count == 0)
            {
                foreach (BasePlayer sleeper in BasePlayer.sleepingPlayerList)
                {
                    if (sleeper != null)
                    {
                        if (steamid != 0L)
                            if (sleeper.userID == steamid)
                            {
                                foundPlayers.Clear();
                                foundPlayers.Add(sleeper);
                                return foundPlayers[0];
                            }
                        string lowername = player.displayName.ToLower();
                        if (lowername.Contains(lowerarg))
                        {
                            foundPlayers.Add(sleeper);
                        }
                    }
                }
            }

            if (foundPlayers.Count == 0)
            {
                if (player != null)
                    SendReply(player, configData.Messages_MainColor + "No players found.</color>");
                return null;
            }

            if (foundPlayers.Count > 1)
            {
                if (player != null)
                    SendReply(player, configData.Messages_MainColor + "Multiple players found with that name.</color>");
                return null;
            }

            return foundPlayers[0];
        }
        #endregion

        #region Messaging
        private void MSG(BasePlayer player, string message, string key = "", bool title = false)
        {
            message = configData.Messages_MainColor + key + "</color>" + configData.Messages_MsgColor + message + "</color>";
            if (title)
                message = configData.Messages_MainColor + Title + ": </color>" + message;
            SendReply(player, message);
        }
        #endregion

        #region Class Saving
        private bool SaveInventory(BasePlayer player)
        {
            List<SavedItem> items = GetPlayerItems(player);
            if (!cachedInventories.ContainsKey(player.userID))
                cachedInventories.Add(player.userID, new PlayerInfo {  });
            cachedInventories[player.userID].Items = items;
            return true;  
        }

        private List<SavedItem> GetPlayerItems(BasePlayer player)
        {
            List<SavedItem> kititems = new List<SavedItem>();
            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (item != null)
                {
                    kititems.Add(ProcessItem(item, "belt"));
                }
            }
            foreach (Item item in player.inventory.containerWear.itemList)
            {
                if (item != null)
                {
                    kititems.Add(ProcessItem(item, "wear"));
                }
            }
            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (item != null)
                {
                    kititems.Add(ProcessItem(item, "main"));
                }
            }
            return kititems;
        }

        private SavedItem ProcessItem(Item item, string container)
        {
            SavedItem iItem = new SavedItem()
            {
                container = container,
                itemid = item.info.itemid,
                amount = item.amount,
                ammo = item.GetHeldEntity() is BaseProjectile ? (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents : item.GetHeldEntity() is FlameThrower ? (item.GetHeldEntity() as FlameThrower).ammo : 0,
                ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null,
                position = item.position,
                skin = item.skin,
                condition = item.condition,
                maxCondition = item.maxCondition,
                frequency = ItemModAssociatedEntity<PagerEntity>.GetAssociatedEntity(item)?.GetFrequency() ?? -1,
                instanceData = new SavedItem.InstanceData(item),
                contents = item.contents?.itemList.Select(item1 => new SavedItem
                {
                    itemid = item1.info.itemid,
                    amount = item1.amount,
                    condition = item1.condition
                }).ToArray()
            };
            return iItem;
        }

        private bool RemoveInventory(BasePlayer player)
        {
            if (cachedInventories.ContainsKey(player.userID))
            {
                cachedInventories.Remove(player.userID);
                return true;
            }
            return false;
        }
        #endregion        

        #region Give
        private bool RestoreInventory(BasePlayer player)
        {
            if (!cachedInventories.ContainsKey(player.userID))
                return false;
            
            player.inventory.Strip();
            foreach (SavedItem kitem in cachedInventories[player.userID].Items)
            {
                GiveItem(player, BuildItem(kitem), kitem.container);
            }
            return true;
        }

        private void GiveItem(BasePlayer player, Item item, string container)
        {
            if (item == null) return;
            ItemContainer cont;
            switch (container)
            {
                case "wear":
                    cont = player.inventory.containerWear;
                    break;
                case "belt":
                    cont = player.inventory.containerBelt;
                    break;
                default:
                    cont = player.inventory.containerMain;
                    break;
            }
            player.inventory.GiveItem(item, cont);
        }

        private Item BuildItem(SavedItem itemData)
        {
            Item item = ItemManager.CreateByItemID(itemData.itemid, itemData.amount, itemData.skin);
            item.condition = itemData.condition;
            item.maxCondition = itemData.maxCondition;

            if (itemData.frequency > 0)
            {
                ItemModRFListener rfListener = item.info.GetComponentInChildren<ItemModRFListener>();
                if (rfListener != null)
                {
                    PagerEntity pagerEntity = BaseNetworkable.serverEntities.Find(item.instanceData.subEntity) as PagerEntity;
                    if (pagerEntity != null)
                    {
                        pagerEntity.ChangeFrequency(itemData.frequency);
                        item.MarkDirty();
                    }
                }
            }

            if (itemData.instanceData?.IsValid() ?? false)
                itemData.instanceData.Restore(item);

            BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                if (!string.IsNullOrEmpty(itemData.ammotype))
                    weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemData.ammotype);
                weapon.primaryMagazine.contents = itemData.ammo;
            }

            FlameThrower flameThrower = item.GetHeldEntity() as FlameThrower;
            if (flameThrower != null)
                flameThrower.ammo = itemData.ammo;

            if (itemData.contents != null)
            {
                foreach (SavedItem contentData in itemData.contents)
                {
                    Item newContent = ItemManager.CreateByItemID(contentData.itemid, contentData.amount);
                    if (newContent != null)
                    {
                        newContent.condition = contentData.condition;
                        newContent.MoveToContainer(item.contents);
                    }
                }
            }
            return item;
        }
        #endregion

        #region Permissions
        private void RegisterPermisions()
        {
            permission.RegisterPermission("inventoryguardian.admin", this);
            permission.RegisterPermission("inventoryguardian.use", this);
        }

        private bool IsAdmin(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, "inventoryguardian.admin") || player.net.connection.authLevel >= igData.AuthLevel)
                return true;            
            return false;
        }

        private bool IsUser(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, "inventoryguardian.use") || IsAdmin(player))
                return true;
            return false;
        }
        #endregion

        #region Chat Commands
        [ChatCommand("ig")]
        private void cmdInvGuard(BasePlayer player, string command, string[] args)
        {
            if (args == null || args.Length == 0)
            {
                if (IsUser(player))
                {
                    MSG(player, " - Save your inventory", "/ig save");
                    MSG(player, " - Restore your inventory", "/ig restore");
                    MSG(player, " - Delete your saved inventory", "/ig delsaved");
                    MSG(player, " - Strip your current inventory", "/ig strip");
                }
                if (IsAdmin(player))
                {
                    MSG(player, " - Save <playername>'s inventory", "/ig save <playername>");
                    MSG(player, " - Restore <playername>'s inventory", "/ig restore <playername>");
                    MSG(player, " - Delete <playername>'s saved inventory", "/ig delsaved <playername>");
                    MSG(player, " - Strip <playername>'s current inventory", "/ig strip <playername>");
                    MSG(player, " - Change the minimum authlevel required to use admin commands", "/ig authlevel <1/2>");                    
                    MSG(player, " - Toggles InventoryGuardian on/off", "/ig toggle");
                    MSG(player, " - Toggles the auto restore funtion", "/ig autorestore");                    
                    MSG(player, " - Toggles the restoration of item condition", "/ig keepcondition");
                }
                return;
            }
            if (args.Length >= 1)
            { 
                if (!igData.IsActivated)
                {
                    if (args[0].ToLower() == "toggle")
                        if (IsAdmin(player))
                        {
                            igData.IsActivated = true;
                            SaveData();
                            MSG(player, "", "You have enabled Inventory Guardian");
                            return;
                        }
                    MSG(player, "", "Inventory Guardian is currently disabled");
                    return;
                }
            else if (igData.IsActivated)
                    switch (args[0].ToLower())
                    {
                        case "save":
                            if (IsAdmin(player))
                            {
                                if (args.Length == 2)
                                {
                                    BasePlayer target = FindPlayer(player, args[1]);
                                    if (target != null)
                                    {
                                        if (SaveInventory(target))
                                        {
                                            MSG(player, "", $"You have successfully saved {target.displayName}'s inventory");
                                            return;
                                        }
                                        MSG(player, "", $"The was a error saving {target.displayName}'s inventory");
                                    }
                                    return;
                                }
                            }
                            else if (IsUser(player))
                            {
                                if (SaveInventory(player))
                                {
                                    MSG(player, "", "You have successfully saved your inventory");
                                    return;
                                }
                                MSG(player, "", "The was a error saving your inventory");
                            }
                            else MSG(player, "You do not have permission to use this command", "", true);
                            return;
                        case "restore":
                            if (IsAdmin(player))
                            {
                                if (args.Length == 2)
                                {
                                    BasePlayer target = FindPlayer(player, args[1]);
                                    if (target != null)
                                    {
                                        if (RestoreInventory(target))
                                        {
                                            MSG(player, "", $"You have successfully restored {target.displayName}'s inventory");
                                            return;
                                        }
                                        MSG(player, "", $"{target.displayName} does not have a saved inventory");
                                    }
                                    return;
                                }
                            }
                            else if (IsUser(player))
                            {
                                if (RestoreInventory(player))
                                {
                                    MSG(player, "", "You have successfully restored your inventory");
                                    return;
                                }
                                MSG(player, "", "You do not have a saved inventory");
                            }
                            else MSG(player, "You do not have permission to use this command", "", true);
                            return;
                        case "delsaved":
                            if (IsAdmin(player))
                            {
                                if (args.Length == 2)
                                {
                                    BasePlayer target = FindPlayer(player, args[1]);
                                    if (target != null)
                                    {
                                        if (RemoveInventory(target))
                                        {
                                            MSG(player, "", $"You have successfully removed {target.displayName}'s inventory");
                                            return;
                                        }
                                        MSG(player, "", $"{target.displayName} does not have a saved inventory");
                                    }
                                    return;
                                }
                            }
                            else if (IsUser(player))
                            {
                                if (RemoveInventory(player))
                                {
                                    MSG(player, "", "You have successfully removed your saved inventory");
                                    return;
                                }
                                MSG(player, "", "You do not have a saved inventory");
                            }
                            else MSG(player, "You do not have permission to use this command", "", true);
                            return;                       
                        case "toggle":
                            if (IsAdmin(player))
                            {
                                if (igData.IsActivated)
                                {
                                    igData.IsActivated = false;
                                    SaveData();
                                    MSG(player, "", "You have disabled Inventory Guardian");
                                    return;
                                }
                            }
                            return;
                        case "autorestore":
                            if (IsAdmin(player))
                            {
                                if (igData.AutoRestore)
                                {
                                    igData.AutoRestore = false;
                                    SaveData();
                                    MSG(player, "You have disabled Auto-Restore", "", true);
                                    return;
                                }
                                else
                                {
                                    igData.AutoRestore = true;
                                    SaveData();
                                    MSG(player, "You have enabled Auto-Restore", "", true);
                                    return;
                                }
                            }
                            return;
                        case "authlevel":
                            if (IsAdmin(player))
                                if (args.Length == 2)
                                {
                                    int i;
                                    if (!int.TryParse(args[1], out i))
                                    {
                                        MSG(player, "", "To set the auth level you must enter either 1/2");
                                        return;
                                    }
                                    igData.AuthLevel = i;
                                    SaveData();
                                    MSG(player, "", $"You have successfully set the required auth level to {i}");
                                }
                            return;
                        case "strip":
                            if (IsAdmin(player))
                            {
                                if (args.Length == 2)
                                {
                                    BasePlayer target = FindPlayer(player, args[1]);
                                    if (target != null)
                                    {
                                        target.inventory.Strip();
                                        MSG(player, "", $"You have successfully stripped {target.displayName}'s inventory");
                                    }
                                    return;
                                }
                            }
                            else if (IsUser(player))
                            {
                                player.inventory.Strip();
                                MSG(player, "", $"You have successfully stripped your inventory");
                            }
                            else MSG(player, "You do not have permission to use this command", "", true);
                            return;
                        case "keepcondition":
                            if (IsAdmin(player))
                            {
                                if (igData.KeepCondition)
                                {
                                    igData.KeepCondition = false;
                                    SaveData();
                                    MSG(player, "You have disabled condition restoration", "", true);
                                    return;
                                }
                                else
                                {
                                    igData.KeepCondition = true;
                                    SaveData();
                                    MSG(player, "You have enabled condition restoration", "", true);
                                    return;
                                }
                            }
                            return;
                    }                    
                }
        }
        #endregion

        #region Console Commands
        [ConsoleCommand("ig")]
        private void ccmdInvGuard(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
            { 
            if (arg.Args == null || arg.Args.Length == 0)
            {
                SendReply(arg, "ig save <playername> - Save <playername>'s inventory");
                SendReply(arg, "ig restore <playername> - Restore <playername>'s inventory");
                SendReply(arg, "ig delsaved <playername> - Delete <playername>'s saved inventory");
                SendReply(arg, "ig strip <playername> - Strip <playername>'s current inventory");
                SendReply(arg, "ig save all - Save all inventories");
                SendReply(arg, "ig restore all - Restore all inventories");
                SendReply(arg, "ig delete all - Delete all saved inventories");
                SendReply(arg, "ig strip all - Strip all player inventories");
                SendReply(arg, "ig authlevel <1/2> - Change the minimum authlevel required to use admin commands");
                SendReply(arg, "ig toggle - Toggles InventoryGuardian on/off");
                SendReply(arg, "ig autorestore - Toggles the auto restore funtion");
                SendReply(arg, "ig keepcondition - Toggles the restoration of item condition");
                return;
            }
            if (arg.Args.Length >= 1)
                switch (arg.Args[0].ToLower())
                {
                    case "save":
                        if (arg.Args.Length == 2)
                        {
                            if (arg.Args[1].ToLower() == "all")
                            {
                                SaveAll();
                                SendReply(arg, "You have successfully saved all player inventories");
                                return;
                            }
                            BasePlayer target = FindPlayer(null, arg.Args[1]);
                            if (target != null)
                            {
                                if (SaveInventory(target))
                                {
                                    SendReply(arg, $"You have successfully saved {target.displayName}'s inventory");
                                    return;
                                }
                                SendReply(arg, $"The was a error saving {target.displayName}'s inventory");
                            }
                        }
                        return;
                    case "restore":
                        if (arg.Args.Length == 2)
                        {
                            if (arg.Args[1].ToLower() == "all")
                            {
                                RestoreAll();
                                SendReply(arg, "You have successfully restored all player inventories");
                                return;
                            }
                            BasePlayer target = FindPlayer(null, arg.Args[1]);
                            if (target != null)
                            {
                                if (RestoreInventory(target))
                                {
                                    SendReply(arg, $"You have successfully restored {target.displayName}'s inventory");
                                    return;
                                }
                                SendReply(arg, $"{target.displayName} does not have a saved inventory");
                            }
                            return;
                        }
                        return;
                    case "delsaved":
                        if (arg.Args.Length == 2)
                        {
                            BasePlayer target = FindPlayer(null, arg.Args[1]);
                            if (target != null)
                            {
                                if (RemoveInventory(target))
                                {
                                    SendReply(arg, $"You have successfully removed {target.displayName}'s inventory");
                                    return;
                                }
                                SendReply(arg, $"{target.displayName} does not have a saved inventory");
                            }
                            return;
                        }
                        return;
                    case "delete":
                        if (arg.Args.Length == 2)
                            if (arg.Args[1].ToLower() == "all")
                            {
                                RemoveAll();
                                SendReply(arg, "You have successfully removed all player inventories");
                            }
                        return;                    
                    case "toggle":
                        if (igData.IsActivated)
                        {
                            igData.IsActivated = false;
                            SaveData();
                            SendReply(arg, "You have disabled Inventory Guardian", true);
                            return;
                        }
                        else
                        {
                            igData.IsActivated = true;
                            SaveData();
                            SendReply(arg, "You have enabled Inventory Guardian", true);
                        }
                        return;
                    case "autorestore":
                        if (igData.AutoRestore)
                        {
                            igData.AutoRestore = false;
                            SaveData();
                            SendReply(arg, "You have disabled Auto-Restore", true);
                            return;
                        }
                        else
                        {
                            igData.AutoRestore = true;
                            SaveData();
                            SendReply(arg, "You have enabled Auto-Restore", true);
                            return;
                        }
                    case "authlevel":
                        if (arg.Args.Length == 2)
                        {
                            int i;
                            if (!int.TryParse(arg.Args[1], out i))
                            {
                                SendReply(arg, "", "To set the auth level you must enter either 1/2");
                                return;
                            }
                            igData.AuthLevel = i;
                            SaveData();
                            SendReply(arg, "", $"You have successfully set the required auth level to {i}");
                        }
                        return;
                    case "strip":
                        if (arg.Args.Length == 2)
                        {
                            if (arg.Args[1].ToLower() == "all")
                            {
                                foreach (var player in BasePlayer.activePlayerList)
                                    player.inventory.Strip();
                                foreach (var player in BasePlayer.sleepingPlayerList)
                                    player.inventory.Strip();
                                SendReply(arg, "You have successfully stripped all player inventories");
                                return;
                            }
                            BasePlayer target = FindPlayer(null, arg.Args[1]);
                            if (target != null)
                            {
                                target.inventory.Strip();
                                SendReply(arg, "", $"You have successfully stripped {target.displayName}'s inventory");
                            }
                        }
                        return;
                    case "keepcondition":
                        if (igData.KeepCondition)
                        {
                            igData.KeepCondition = false;
                            SaveData();
                            SendReply(arg, "", "You have disabled condition restoration", true);
                            return;
                        }
                        else
                        {
                            igData.KeepCondition = true;
                            SaveData();
                            SendReply(arg, "", "You have enabled condition restoration", true);
                            return;
                        }
                }
            }
        }
        #endregion

        #region Classes
        private class IGData
        {
            public bool IsActivated = true;
            public bool AutoRestore = true;
            public bool KeepCondition = true;
            public int AuthLevel = 2;
            public Dictionary<ulong, PlayerInfo> Inventories = new Dictionary<ulong, PlayerInfo>();
        }

        private class PlayerInfo
        {
            public bool RestoreOnce = false;
            public List<SavedItem> Items;
        }

        private class SavedItem
        {
            public string container;
            public int itemid;
            public ulong skin;
            public int amount;
            public float condition;
            public float maxCondition;
            public int ammo;
            public string ammotype;
            public int position;
            public int frequency;
            public InstanceData instanceData;
            public SavedItem[] contents;

            public class InstanceData
            {
                public int dataInt;
                public int blueprintTarget;
                public int blueprintAmount;
                public uint subEntity;

                public InstanceData() { }
                public InstanceData(Item item)
                {
                    if (item.instanceData == null)
                        return;

                    dataInt = item.instanceData.dataInt;
                    blueprintAmount = item.instanceData.blueprintAmount;
                    blueprintTarget = item.instanceData.blueprintTarget;
                }

                public void Restore(Item item)
                {
                    if (item.instanceData == null)
                        item.instanceData = new ProtoBuf.Item.InstanceData();

                    item.instanceData.ShouldPool = false;

                    item.instanceData.blueprintAmount = blueprintAmount;
                    item.instanceData.blueprintTarget = blueprintTarget;
                    item.instanceData.dataInt = dataInt;

                    item.MarkDirty();
                }

                public bool IsValid()
                {
                    return dataInt != 0 || blueprintAmount != 0 || blueprintTarget != 0;
                }
            }
        }
        #endregion

        #region Data Management
        private void SaveData()
        {
            igData.Inventories = cachedInventories;
            Inventory_Data.WriteObject(igData);
            Puts("Saved data");
        }

        private void SaveLoop() => timer.Once(900, () => { SaveData(); SaveLoop(); });

        private void LoadData()
        {
            try
            {
                igData = Inventory_Data.ReadObject<IGData>();
                cachedInventories = igData.Inventories;
            }
            catch
            {
                Puts("Couldn't load player data, creating new datafile");
                igData = new IGData();
            }           
        }
        #endregion

        #region Config        
        private ConfigData configData;

        private class ConfigData
        {
            public string Messages_MainColor { get; set; }
            public string Messages_MsgColor { get; set; }           
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                Messages_MainColor = "<color=#FF8C00>",
                Messages_MsgColor = "<color=#939393>"
            };
            SaveConfig(config);
        }

        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();

        private void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion

    }
}
