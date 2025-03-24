// © 2019 Ts3Hosting All Rights Reserved
using System;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Oxide.Core.Configuration;
using Oxide.Core;

using System.Globalization;

namespace Oxide.Plugins
{
    [Info("Item Printer", "Ts3Hosting", "1.1.19")]
    [Description("Craft set items in a large wooden box with power hookups and box decay")]
    public class ItemPrinter : RustPlugin
    {
        itemEntity npcData;
        playerEntity playerData;
        IEntity iData;

        boxEntity pcdData;
        private DynamicConfigFile PCDDATA;
        private DynamicConfigFile NPCDATA;
        private DynamicConfigFile PLAYERDATA;
        private DynamicConfigFile I;
        private const string adminAllow = "itemprinter.admin";
        private const string printerAllow = "itemprinter.printer";
        private const string useAllow = "itemprinter.use";
        public ulong CardskinID;
        private int damage;
        private int itemamounts;
        private bool Changed;
        public int itemID;
        public ulong skinID;
        public string itemname;
        public string printername;
        public bool craftmode;
        public int seconds;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["nope"] = "You do not have perms to use this command.",
                ["copy"] = "All items, item skins, and stack size in your inventory main now required to run printer..",
                ["copymode2"] = "All items, item skins, and stack size in your inventory main now required to run printer and print {0}.",
                ["missingitems"] = "You are missing items to print.",
                ["nogroup"] = "There is no items set for this group so you can not print.",
                ["help"] = "/printer printer =  Give your self a printer.",
                ["help1"] = "/printer items <group> = Copy all items in your inventory main to items needed by the printers to print.",
                ["mode2help1"] = "/printer items <ShortName/ItemID> <ammountTogive> = Copy all items in your inventory main to items needed by the printers to print.",
                ["noitem"] = "Sorry you can not print this item in slot 1 of the box",
                ["help3"] = "/printer setgroup <steamid> <group>",
                ["nobp"] = "You do not have the bp to print this item",
                ["noitemdef"] = "Could not find that item def",
                ["groupupdate"] = "Player group updated",
				["WrongCommand"] = "Wrong command usage",
				["noplayer"] = "player not found",
            }, this);
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file!");
            LoadVariables();
        }

        void LoadVariables()
        {
            craftmode = Convert.ToBoolean(GetConfig("Printer Mode", "Mode two", false));
            CardskinID = Convert.ToUInt64(GetConfig("Box", "The Box SkinID", 1722250254));
            seconds = Convert.ToInt32(GetConfig("Box", "tprinter tick no less then 1", 4));
            printername = Convert.ToString(GetConfig("Box", "Printer Name", "Printer"));
            damage = Convert.ToInt32(GetConfig("Box", "Damage Amount Per Tick", 1));

            itemID = Convert.ToInt32(GetConfig("ItemsAsCash", "itemID", -1779183908));
            skinID = Convert.ToUInt64(GetConfig("ItemsAsCash", "skinID", 916068443));
            itemname = Convert.ToString(GetConfig("ItemsAsCash", "ItemName", "Money"));
            itemamounts = Convert.ToInt32(GetConfig("ItemsAsCash", "Item Amount to create Per Tick", 1));

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        void Init()
        {
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/printer");
            NPCDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/item");
            PLAYERDATA = Interface.Oxide.DataFileSystem.GetFile(Name + "/player");
            I = Interface.Oxide.DataFileSystem.GetFile(Name + "/Data");

            RegisterPermissions();
            LoadData();
            LoadVariables();
        }

        private void OnServerInitialized()
        {
            checkbox();
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission(adminAllow, this);
            permission.RegisterPermission(printerAllow, this);
            permission.RegisterPermission(useAllow, this);
        }

        void LoadData()
        {
            try
            {
                pcdData = Interface.Oxide.DataFileSystem.ReadObject<boxEntity>(Name + "/printer");
            }
            catch
            {
                PrintWarning("Couldn't load entity data, creating new entity");
                pcdData = new boxEntity();
            }
            try
            {
                npcData = Interface.Oxide.DataFileSystem.ReadObject<itemEntity>(Name + "/item");
            }
            catch
            {
                PrintWarning("Couldn't load Item data, creating new item file");
                npcData = new itemEntity();
            }
            try
            {
                playerData = Interface.Oxide.DataFileSystem.ReadObject<playerEntity>(Name + "/player");
            }
            catch
            {
                PrintWarning("Couldn't load player data, creating new player file");
                playerData = new playerEntity();
            }
            try
            {
                iData = Interface.Oxide.DataFileSystem.ReadObject<IEntity>(Name + "/Data");
            }
            catch
            {
                PrintWarning("Couldn't load craftmode data, creating new craftmode file");
                iData = new IEntity();
            }
        }

        void checkbox()
        {
            PrintWarning("Checking and removing unused printers in datafile.");
            foreach (var printer in pcdData.pEntity.Values.ToList())
            {
                if (pcdData.pEntity.ContainsKey(printer.batt))
                {
                    var find = BaseNetworkable.serverEntities.Find(printer.batt);
                    if (find == null)
                    {
                        pcdData.pEntity.Remove(printer.batt);
                    }
                    else if (find != null)
                    {
                        SpawnRefresh(find);
                        var printer1 = BaseNetworkable.serverEntities.Find(printer.printer);
                        if (printer1 != null) SpawnRefresh(printer1);
                        var counter = BaseNetworkable.serverEntities.Find(printer.counter);
                        if (counter != null) SpawnRefresh(counter);
                        var light = BaseNetworkable.serverEntities.Find(printer.light);
                        if (light != null) SpawnRefresh(light);
                    }
                }
            }
            SaveData();
        }

        public class vipData1
        {
            public int item;
            public ulong skinid;
            public int amount;
            public string name;
            public string displayName;
            public string Permission;
            public string totalGive;
        }

        public class vipData
        {
            public int item;
            public ulong skinid;
            public int amount;
            public string name;
            public string displayName;
            public string Permission;
            public string totalGive;
        }
        class itemEntity
        {
            public Dictionary<string, NPCInfo> iEntity = new Dictionary<string, NPCInfo>();

            public itemEntity() { }
        }
        class NPCInfo
        {
            public int item;
            public ulong skinid;
            public int amount;
            public string name;
            public string displayName;
            public string Permission;
        }

        void SaveitemData()
        {
            NPCDATA.WriteObject(npcData);
        }

        class boxEntity
        {
            public Dictionary<ulong, PCDInfo> pEntity = new Dictionary<ulong, PCDInfo>();
            public boxEntity() { }
        }

        class PCDInfo
        {
            public uint batt;
            public uint printer;
            public uint light;
            public uint counter;
        }

        void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }

        class playerEntity
        {
            public Dictionary<ulong, PInfo> playerpEntity = new Dictionary<ulong, PInfo>();
            public playerEntity() { }
        }

        class PInfo
        {
            public string displayName;
            public string Permission;
        }
        void SavePlayerData()
        {
            PLAYERDATA.WriteObject(playerData);
        }

        class IEntity
        {
            public Dictionary<string, IInfo> iEntity = new Dictionary<string, IInfo>();
        }

        class IInfo
        {
            public int totalGive;
            public Dictionary<string, vipData> items = new Dictionary<string, vipData>();
        }
        void SaveIData()
        {
            I.WriteObject(iData);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!playerData.playerpEntity.ContainsKey(player.userID))
            {
                playerData.playerpEntity.Add(player.userID, new PInfo());
                playerData.playerpEntity[player.userID].displayName = player.displayName;
                playerData.playerpEntity[player.userID].Permission = "null";
                SavePlayerData();
            }
        }

        [ConsoleCommand("printer")]
        private void CmdConsolePage(ConsoleSystem.Arg args)
        {
            if (args == null || args.Args.Length == 0) return;

            BasePlayer player = args.Player();
            if (player != null) return;
            if (args.Args.Length == 3 && args.Args[0].ToLower() == "setgroup") updategroup(null, args.Args[1].ToLower(), args.Args[2].ToLower(), args);

            if (args.Args.Length == 2 && args.Args[0].ToLower() == "printer")
            {
                var ids = default(ulong);
                if (!ulong.TryParse(args.Args[1], out ids))
                {
					SendReply(args, string.Format(lang.GetMessage("WrongCommand", this, player.UserIDString)));
                    return;
                }
                var playernull = BasePlayer.FindByID(ids);
                if (playernull == null)
                {
					SendReply(args, string.Format(lang.GetMessage("noplayer", this, player.UserIDString)));
                    return;
                }
                var card = ItemManager.CreateByItemID(833533164, 1, CardskinID);
                if (card == null) return;
                card.name = printername;
                playernull.GiveItem(card);
            }
        }

        private void updategroup(BasePlayer player, string id, string perm, ConsoleSystem.Arg args)
        {
            if (player.net?.connection != null && !permission.UserHasPermission(player.UserIDString, adminAllow))
            {
                SendReply(player, string.Format(lang.GetMessage("nope", this, player.UserIDString)));
                return;
            }

            if (id == null || perm == null)
            {
                if (player.net?.connection != null) SendReply(player, string.Format(lang.GetMessage("help3", this, player.UserIDString)));
                else SendReply(args, string.Format(lang.GetMessage("WrongCommand", this, player.UserIDString)));
                return;
            }

            var ids = default(uint);
            if (!uint.TryParse(id, out ids))
            {
                return;
            }

            if (playerData.playerpEntity.ContainsKey(ids))
            {
                playerData.playerpEntity[ids].Permission = perm;
                SavePlayerData();
                if (player.net?.connection != null) SendReply(player, string.Format(lang.GetMessage("groupupdate", this, player.UserIDString)));
                else SendReply(args, string.Format(lang.GetMessage("groupupdate", this, player.UserIDString)));
            }
        }

        object CanPickupEntity(BasePlayer player, BaseCombatEntity entity)
        {
            StorageContainer thebox = entity?.GetComponentInParent<StorageContainer>();
            if (thebox != null && entity.net.ID != thebox.net.ID)
            {
                if (thebox.skinID == CardskinID)
                    return false;
            }
            if (entity.name == printername)
            {
                var health = entity.health;
                var maxh = entity.MaxHealth();
                var ids = entity.net.ID.ToString();

                foreach (var printer in pcdData.pEntity.Values.ToList())
                {
                    if (printer.printer.ToString() == ids)
                    {
                        entity.Kill();
                        var card = ItemManager.CreateByItemID(833533164, 1, CardskinID);
                        if (card == null) return null;
                        card.name = printername;
                        var total = maxh - health;
                        card.condition = card.condition - total - 5;
                        player.GiveItem(card);
                        Effect.server.Run("assets/prefabs/deployable/recycler/effects/start.prefab", player.transform.position);
                        pcdData.pEntity.Remove(printer.batt);
                        SaveData();
                        break;
                    }
                }
                return false;
            }
            return null;
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (item.skin == skinID)
            {
                if (item.name != itemname)
                {
                    item.name = itemname;
                }
            }
        }

        object OnSwitchToggle(ElectricSwitch sw, BasePlayer player)
        {
            bool onOff = sw.IsOn();

            var oSlotArray = sw.outputs;
            if (oSlotArray == null || oSlotArray.Length <= 0) return null;
            IOEntity.IOSlot oSlot3 = oSlotArray[0];
            if (oSlot3 == null) return null;
            var oEntity = oSlot3.connectedTo.Get(true);
            if (oEntity == null) return null;
            if (!pcdData.pEntity.ContainsKey(oEntity.net.ID))
            {
                return null;
            }
            if (!onOff && sw.IsPowered())
            {
                var box = pcdData.pEntity[oEntity.net.ID].printer;
                var boxlight = pcdData.pEntity[oEntity.net.ID].light;
                var counterID = pcdData.pEntity[oEntity.net.ID].counter;
                DoPrinterThings(player, sw, box, boxlight, counterID);
                FlasherLight lights = BaseNetworkable.serverEntities.Find(boxlight) as FlasherLight;
                if (lights != null)
                {
                    lights.SetFlag(BaseEntity.Flags.Reserved8, true, false, true);
                    lights.SetFlag(BaseEntity.Flags.On, true);
                }
            }
            if (onOff)
            {
                var boxx = pcdData.pEntity[oEntity.net.ID].printer;
                if (timers.ContainsKey(boxx)) timers[boxx].Destroy();
                StorageContainer box6 = BaseNetworkable.serverEntities.Find(boxx) as StorageContainer;
                if (box6 == null) return null;
                box6.inventory.SetLocked(false);
                var boxlight = pcdData.pEntity[oEntity.net.ID].light;
                FlasherLight lights = BaseNetworkable.serverEntities.Find(boxlight) as FlasherLight;
                if (lights != null)
                {
                    lights.SetFlag(BaseEntity.Flags.Reserved8, false, false, false);
                    lights.SetFlag(BaseEntity.Flags.On, false);
                }
            }
            return null;
        }

        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity != null && pcdData.pEntity.ContainsKey(entity.net.ID))
            {
                pcdData.pEntity.Remove(entity.net.ID);
                SaveData();
            }
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            var item1 = go?.ToBaseEntity() as BoxStorage ?? null;
            if (item1 != null && item1 is BoxStorage)
            {
                if (item1.skinID == CardskinID)
                {
                    item1.name = printername;
                    additem(null, item1);
                    item1.SendNetworkUpdateImmediate();
                }
            }
        }

        void SpawnRefresh(BaseNetworkable entity1)
        {
            UnityEngine.Object.Destroy(entity1.GetComponent<Collider>());
        }

        private void additem(BasePlayer player, BoxStorage box1)
        {
            SpawnRefresh(box1);
            ElectricBattery batt;

            batt = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/batteries/smallrechargablebattery.deployed.prefab") as ElectricBattery;
            if (batt == null) return;
            batt.SetParent(box1, box1.GetSlotAnchorName(BaseEntity.Slot.Lock));
            batt.transform.localPosition = new Vector3(0.28f, -0.05f, 0f);
            batt.transform.localRotation = Quaternion.Euler(Vector3.zero);
            batt.Spawn();
            SpawnRefresh(batt);
            batt.SendNetworkUpdateImmediate();

            FlasherLight light;

            light = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/lights/flasherlight/electric.flasherlight.deployed.prefab") as FlasherLight;
            if (light == null) return;
            light.SetParent(box1, box1.GetSlotAnchorName(BaseEntity.Slot.Lock));
            light.transform.localPosition = new Vector3(0.28f, 0f, 0f);
            light.transform.localRotation = Quaternion.Euler(Vector3.zero);
            light.Spawn();
            SpawnRefresh(light);
            light.SendNetworkUpdateImmediate();

            PowerCounter counter;

            counter = GameManager.server.CreateEntity("assets/prefabs/deployable/playerioents/counter/counter.prefab") as PowerCounter;
            if (counter == null) return;
            counter.SetParent(box1, box1.GetSlotAnchorName(BaseEntity.Slot.Lock));
            counter.transform.localPosition = new Vector3(0.038f, -0.10f, -0.50f);
            counter.transform.localRotation = Quaternion.Euler(new Vector3(0, 270, 0));
            counter.Spawn();
            SpawnRefresh(counter);
            IOEntity.IOSlot ioOutput = batt.outputs[0];
            if (ioOutput != null)
            {
                ioOutput.connectedTo = new IOEntity.IORef();
                ioOutput.connectedTo.Set(counter);
                ioOutput.connectedToSlot = 0;
                ioOutput.connectedTo.Init();

                counter.inputs[0].connectedTo = new IOEntity.IORef();
                counter.inputs[0].connectedTo.Set(batt);
                counter.inputs[0].connectedToSlot = 0;
                counter.inputs[0].connectedTo.Init();
            }

            if (!pcdData.pEntity.ContainsKey(batt.net.ID))
            {
                pcdData.pEntity.Add(batt.net.ID, new PCDInfo());
                //SaveData();
            }
            pcdData.pEntity[batt.net.ID].printer = box1.net.ID;
            pcdData.pEntity[batt.net.ID].batt = batt.net.ID;
            pcdData.pEntity[batt.net.ID].light = light.net.ID;
            pcdData.pEntity[batt.net.ID].counter = counter.net.ID;
            SaveData();
        }

        [ChatCommand("printer")]
        void GetPrinter(BasePlayer player, string command, string[] args)
        {
            if (args.Length <= 0)
            {
                if (permission.UserHasPermission(player.UserIDString, printerAllow) && permission.UserHasPermission(player.UserIDString, adminAllow))
                {
                    SendReply(player, string.Format(lang.GetMessage("help", this, player.UserIDString)));
                }
                if (permission.UserHasPermission(player.UserIDString, adminAllow))
                {
                    if (!craftmode) SendReply(player, string.Format(lang.GetMessage("help1", this, player.UserIDString)));
                    if (craftmode) SendReply(player, string.Format(lang.GetMessage("mode2help1", this, player.UserIDString)));
                    SendReply(player, string.Format(lang.GetMessage("help3", this, player.UserIDString)));
                }
                return;
            }

            switch (args[0].ToLower())
            {
                case "printer":
                    GetPrint(player);
                    return;

                case "items":
                    if (args.Length == 1)
                        GetPrinte1r(player, "null", "1");
                    else if (args.Length == 3)
                        GetPrinte1r(player, args[1].ToLower(), args[2].ToLower());
                    return;

                case "setgroup":
                    if (args.Length == 3)
                        updategroup(player, args[1].ToLower(), args[2].ToLower(), null);
                    return;

                default:
                    break;
            }

            if (permission.UserHasPermission(player.UserIDString, printerAllow) || permission.UserHasPermission(player.UserIDString, adminAllow))
            {
                SendReply(player, string.Format(lang.GetMessage("help", this, player.UserIDString)));
            }
            if (permission.UserHasPermission(player.UserIDString, adminAllow))
            {
                if (!craftmode)
                {
                    SendReply(player, string.Format(lang.GetMessage("help1", this, player.UserIDString)));
                    SendReply(player, string.Format(lang.GetMessage("help3", this, player.UserIDString)));
                }
                if (craftmode) SendReply(player, string.Format(lang.GetMessage("mode2help1", this, player.UserIDString)));
            }
            return;
        }

        void GetPrint(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, adminAllow) || permission.UserHasPermission(player.UserIDString, printerAllow))
            {
                var card = ItemManager.CreateByItemID(833533164, 1, CardskinID);
                if (card == null) return;
                card.name = printername;
                player.GiveItem(card);
                return;
            }
            SendReply(player, string.Format(lang.GetMessage("nope", this, player.UserIDString)));
            return;
        }

        void GetPrinte1r(BasePlayer player, string perm, string amount)
        {
            if (!permission.UserHasPermission(player.UserIDString, adminAllow))
            {
                SendReply(player, string.Format(lang.GetMessage("nope", this, player.UserIDString)));
                return;
            }

            if (!craftmode)
            {
                GetPlayerItems(player, perm);
                SendReply(player, string.Format(lang.GetMessage("copy", this, player.UserIDString)));
                return;
            }
            var pM = default(int);
            if (!int.TryParse((perm), out pM))
            {
                SendReply(player, string.Format(lang.GetMessage("noitemdef", this, player.UserIDString)));
                return;
            }
            ItemDefinition itemdef = ItemManager.FindItemDefinition(pM);
            if (itemdef != null)
            {
                string permID = itemdef.itemid.ToString();
                GetPlayerItemsvip(player, permID, amount);
                SendReply(player, string.Format(lang.GetMessage("copymode2", this, player.UserIDString), itemdef.displayName.english));
            }
            else
            {
                SendReply(player, "Could not find item: " + perm, player);
                return;
            }
        }

        private void GetPlayerItems(BasePlayer player, string perm)
        {
            foreach (var i in npcData.iEntity.Values.ToList())
            {
                if (i.Permission == perm)
                {
                    string itemids = i.item.ToString() + perm;
                    npcData.iEntity.Remove(itemids);
                    SaveitemData();
                }
            }

            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (item != null)
                {
                    ProcessItem(item, "main", perm);
                }
            }
        }

        private void ProcessItem(Item item, string container, string perm)
        {
            string itemids = item.info.itemid.ToString() + perm;
            if (!npcData.iEntity.ContainsKey(itemids))
            {
                npcData.iEntity.Add(itemids, new NPCInfo());
                //SaveData();
            }

            npcData.iEntity[itemids].amount = item.amount;
            npcData.iEntity[itemids].skinid = item.skin;
            npcData.iEntity[itemids].item = item.info.itemid;
            npcData.iEntity[itemids].displayName = item.info.displayName.english.ToString();
            npcData.iEntity[itemids].name = item.name;
            npcData.iEntity[itemids].Permission = perm;
            SaveitemData();
        }

        private void GetPlayerItemsvip(BasePlayer player, string perm, string amount)
        {
            if (iData.iEntity.ContainsKey(perm))
            {
                iData.iEntity.Remove(perm);
            }

            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (item != null)
                {
                    ProcessItemvip(item, "main", perm, amount);
                }
            }
        }

        private void ProcessItemvip(Item item, string container, string perm, string amount)
        {
            string itemids = perm;
            if (!iData.iEntity.ContainsKey(itemids))
            {
                iData.iEntity.Add(itemids, new IInfo());
                //SaveData();
            }
            var amount1 = default(int);

            if (!int.TryParse(amount, out amount1))
            {
                return;
            }
            iData.iEntity[itemids].totalGive = amount1;
            iData.iEntity[itemids].items.Add(item.info.itemid.ToString(), new vipData());
            iData.iEntity[itemids].items[item.info.itemid.ToString()].amount = item.amount;
            iData.iEntity[itemids].items[item.info.itemid.ToString()].skinid = item.skin;
            iData.iEntity[itemids].items[item.info.itemid.ToString()].item = item.info.itemid;
            iData.iEntity[itemids].items[item.info.itemid.ToString()].displayName = item.info.displayName.english.ToString();
            iData.iEntity[itemids].items[item.info.itemid.ToString()].name = item.name;
            iData.iEntity[itemids].items[item.info.itemid.ToString()].Permission = perm;
            iData.iEntity[itemids].items[item.info.itemid.ToString()].totalGive = amount;

            SaveIData();
        }

        public bool giveitem(StorageContainer box, int number1, string item)
        {
            Item itemc = null;
            if (!craftmode)
            {
                itemc = ItemManager.CreateByItemID(itemID, number1, skinID);
                if (itemc == null) return false;
                itemc.name = itemname;
            }

            if (craftmode)
            {
                var ids = default(int);
                if (int.TryParse(item, out ids))
                {
                    itemc = ItemManager.CreateByItemID(ids, number1);
                }
            }

            if (itemc == null) return false;
            if (itemc.MoveToContainer(box.inventory, -1, true))
            {
                return true;
            }
            Vector3 velocity = Vector3.zero;
            itemc.Drop(box.transform.position + new Vector3(0.5f, 1f, 0), velocity);
            return false;
        }

        public bool hasbp(BasePlayer player, string itemboxid)
        {
			if (itemID == 1540934679 || itemID == -946369541) return true;
			var persistantPlayer = SingletonComponent<ServerMgr>.Instance.persistance.GetPlayerInfo(player.userID);
            if (persistantPlayer != null)
            {
				
                foreach (var itemId in persistantPlayer.unlockedItems)
                {
                    if (itemboxid == itemId.ToString())
                        return true;
                }
            }
            return false;
        }

        private void stopbox(BasePlayer player, ElectricSwitch sw, StorageContainer box, FlasherLight light, uint box1)
        {
            NextTick(() =>
              {
                  if (sw != null)
                  {
                      sw.SetFlag(BaseEntity.Flags.On, false);
                      sw.SendNetworkUpdate();
                  }

                  if (timers.ContainsKey(box1)) timers[box1].Destroy();
                  if (box != null) box.inventory.SetLocked(false);
                  if (light != null)
                  {
                      light.SetFlag(BaseEntity.Flags.Reserved8, false, false, false);
                      light.SetFlag(BaseEntity.Flags.On, false);
                  }
              });
        }

        readonly Dictionary<ulong, Timer> timers = new Dictionary<ulong, Timer>();

        void DoPrinterThings(BasePlayer player, ElectricSwitch sw, uint box1, uint light, uint counterID)
        {
            var totalGive = 1;
            StorageContainer box = BaseNetworkable.serverEntities.Find(box1) as StorageContainer;
            PowerCounter counter = BaseNetworkable.serverEntities.Find(counterID) as PowerCounter;
            FlasherLight lights = BaseNetworkable.serverEntities.Find(light) as FlasherLight;

            if (!playerData.playerpEntity.ContainsKey(player.userID))
            {
                playerData.playerpEntity.Add(player.userID, new PInfo());
                playerData.playerpEntity[player.userID].displayName = player.displayName;
                playerData.playerpEntity[player.userID].Permission = "null";
                SavePlayerData();
            }
            if (counter != null)
            {
                counter.SetFlag(BaseEntity.Flags.On, true);
                counter.UpdateFromInput(400, 3);
                counter.SendNetworkUpdateImmediate();
            }

            if (box == null)
            {
                return;
            }
            List<vipData1> saveditems = new List<vipData1>();
            int t = 0;
            string itemboxid = "";
            if (craftmode)
            {
                Item itemtype = box.inventory.GetSlot(0);
                if (itemtype == null)
                {
                    if (player.net?.connection != null) SendReply(player, string.Format(lang.GetMessage("noitem", this, player.UserIDString)));
                    stopbox(player, sw, box, lights, box1);
                    return;
                }

                itemboxid = itemtype.info.itemid.ToString();

                if (!iData.iEntity.ContainsKey(itemboxid))
                {
                    if (player.net?.connection != null && craftmode) SendReply(player, string.Format(lang.GetMessage("noitem", this, player.UserIDString)));
                    stopbox(player, sw, box, lights, box1);
                    return;
                }

                bool checkbp = hasbp(player, itemboxid);
                if (!checkbp && itemboxid != "-265876753")
                {
                    if (player.net?.connection != null) SendReply(player, string.Format(lang.GetMessage("nobp", this, player.UserIDString)));
                    stopbox(player, sw, box, lights, box1);
                }

                totalGive = iData.iEntity[itemboxid].totalGive;
                foreach (var k in iData.iEntity[itemboxid].items.Values.ToList())
                {
                    saveditems.Add(new vipData1
                    {
                        item = k.item,
                        skinid = k.skinid,
                        amount = k.amount,
                        name = k.name,
                        totalGive = k.totalGive,
                        displayName = k.displayName,
                        Permission = k.Permission
                    });
                    t++;
                }
            }

            if (!craftmode)
            {
                if (playerData.playerpEntity.ContainsKey(player.userID))
                {
                    var p = playerData.playerpEntity[player.userID].Permission;
                    foreach (var h in npcData.iEntity.Values.ToList())
                    {
                        if (h.Permission == p)
                        {
                            saveditems.Add(new vipData1
                            {
                                item = h.item,
                                skinid = h.skinid,
                                amount = h.amount,
                                name = h.name,
                                displayName = h.displayName,
                                Permission = h.Permission
                            });
                            t++;
                        }
                    }
                }

                if (t <= 0)
                {
                    stopbox(player, sw, box, lights, box1);
                    return;
                }
            }

            if (timers.ContainsKey(box1))
                timers[box1].Destroy();
            box.inventory.SetLocked(true);
            Effect.server.Run("assets/prefabs/npc/autoturret/effects/online.prefab", box.transform.position);
            timers[box1] = timer.Every(seconds, () =>
            {
                foreach (var i in saveditems)
                {
                    int totals = 0;
                    if (box == null)
                    {
                        return;
                    }

                    var items = box.inventory.FindItemByItemID(i.item);
                    if (items == null || items.skin != i.skinid || box == null)
                    {
                        if (counter != null) counter.SetFlag(BaseEntity.Flags.Reserved8, false, false, false);
                        if (player.net?.connection != null) SendReply(player, string.Format(lang.GetMessage("missingitems", this, player.UserIDString)));
                        Effect.server.Run("assets/prefabs/npc/autoturret/effects/offline.prefab", box.transform.position);
                        stopbox(player, sw, box, lights, box1);
                        return;
                    }

                    totals = box.inventory.GetAmount(i.item, true);
                    if (totals <= 0 || totals < i.amount || !sw.IsPowered() || !sw.IsOn())
                    {
                        Effect.server.Run("assets/prefabs/npc/autoturret/effects/offline.prefab", box.transform.position);
                        stopbox(player, sw, box, lights, box1);
                        if (player.net?.connection != null) SendReply(player, string.Format(lang.GetMessage("missingitems", this, player.UserIDString)));
                        return;
                    }
                }

                foreach (var j in saveditems)
                {
                    box.inventory.Take(null, j.item, j.amount);
                }
                bool giveitems = false;

                if (!craftmode) giveitems = giveitem(box, itemamounts, itemID.ToString());
                if (craftmode) giveitems = giveitem(box, totalGive, itemboxid);
                box.health = box.health - damage;
                if (counter != null) counter.UpdateFromInput(400, 1);
                Effect.server.Run("assets/prefabs/npc/autoturret/effects/online.prefab", box.transform.position);
                if (!giveitems)
                {
                    stopbox(player, sw, box, lights, box1);
                }
            });
        }
    }
}