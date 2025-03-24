using System;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("ClothingSlots", "Jake_Rich", "1.1.4")]
    [Description("Available Inventory Slots Depends On Clothing Worn")]

    public partial class ClothingSlots : RustPlugin
    {
        public static ClothingSlots _plugin;
        public static JSONFile<ConfigData> _settingsFile; //I know static stuff persists, it's handled in this case :)
        public static ConfigData Settings { get { return _settingsFile.Instance; } }
        public PlayerDataController<SlotPlayerData> PlayerData;

        void Init()
        {
            _plugin = this;
        }

        void Loaded()
        {
            _settingsFile = new JSONFile<ConfigData>($"{Name}", ConfigLocation.Config, extension: ".json");
            PlayerData = new PlayerDataController<SlotPlayerData>();

            if (lang_en.Count > 0)
            {
                lang.RegisterMessages(lang_en, this); //Setup lang now by default in case it is needed
            }
        }

        void OnServerInitialized()
        {
            Settings.Setup();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }

        void Unload()
        {
            foreach (var data in PlayerData.All) data.SetSlots(30); //Reset slot limit when unloading plugin

            PlayerData.Unload();
        }

        void OnPlayerConnected(BasePlayer player) => PlayerData.Get(player).UpdateSlots();

        void OnPlayerDisconnected(BasePlayer player) => PlayerData.Get(player).UpdateSlots();

        void OnPlayerRespawned(BasePlayer player) => PlayerData.Get(player).UpdateSlots();

        void OnPlayerDeath(BasePlayer player) => PlayerData.Get(player).UpdateSlots();

        object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot, int amount)
        {
            if (item == null || playerLoot == null)
            {
                return null;
            }

            BasePlayer _BasePlayer = playerLoot.baseEntity;
            if (!_BasePlayer.IsValid())
            {
                return null;
            }

            BasePlayer player = playerLoot.GetComponent<BasePlayer>();
            if (player == null)
            {
                return null;
            }

            ItemContainer _targetContainer = playerLoot.FindContainer(targetContainer);
            if (_targetContainer == null)
            {
                return null;
            }

            if (targetSlot != -1)
            {
                return null;
            }

            if (item.parent != playerLoot.containerBelt)
            {
                return null;
            }

            if (item.info.GetComponent<ItemModWearable>() != null)
            {
                if (item.MoveToContainer(playerLoot.containerWear))
                {
                    return false;
                }
            }

            return null;
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null)
            {
                return;
            }

            BasePlayer player = container.GetOwnerPlayer();

            if (player == null || !player.userID.IsSteamId())
            {
                return;
            }

            if (container != player.inventory.containerWear || player.inventory.containerWear.uid != container.uid)
            {
                return;
            }

            PlayerData.Get(player).UpdateSlots();
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (container == null || item == null)
            {
                return;
            }

            BasePlayer player = container.GetOwnerPlayer();

            if (player == null || !player.userID.IsSteamId())
            {
                return;
            }

            if (container != player.inventory.containerWear || player.inventory.containerWear.uid != container.uid)
            {
                return;
            }

            NextFrame(() =>
            {
                //NextFrame it, so when clothing is put into the inventory it will move when capacity decreases
                if (player == null || !player.userID.IsSteamId() || player.IsDead())
                {
                    return;
                }

                PlayerData.Get(player).UpdateSlots();
            });
        }

        public class ClothingSetting
        {
            [JsonProperty(PropertyName = "Available inventory slots (Main)")]
            public int Slots = 0;

            public ClothingSetting()
            {

            }

            public ClothingSetting(ItemDefinition clothing)
            {

            }
        }

        public class ConfigData
        {
            [JsonProperty(PropertyName = "Available inventory slots (Belt)")]
            public int DefaultSlots = 6;

            [JsonProperty(PropertyName = "Clothing settings (item shortname) and (available inventory slots)")]
            public Dictionary<string, ClothingSetting> Clothing = new Dictionary<string, ClothingSetting>() //Default values are as follows
            {
                {"hazmatsuit", new ClothingSetting() { Slots = 12, } },
                {"hazmatsuit.arcticsuit", new ClothingSetting() { Slots = 12, } },
                {"hazmatsuit.spacesuit", new ClothingSetting() { Slots = 12, } },
                {"hazmatsuit.lumberjack", new ClothingSetting() { Slots = 12, } },
                {"mask.balaclava", new ClothingSetting() { Slots = 1, } },
                {"diving.tank", new ClothingSetting() { Slots = 5, } },
                {"mask.bandana", new ClothingSetting() { Slots = 1, } },
                {"shoes.boots", new ClothingSetting() { Slots = 4, } },
                {"scientistsuit_heavy", new ClothingSetting() { Slots = 24, } },
                {"roadsign.jacket", new ClothingSetting() { Slots = 1, } },
                {"tshirt.long", new ClothingSetting() { Slots = 1, } },
                {"hat.beenie", new ClothingSetting() { Slots = 1, } },
                {"diving.wetsuit", new ClothingSetting() { Slots = 4, } },
                {"wood.armor.pants", new ClothingSetting() { Slots = 1, } },
                {"wood.armor.jacket", new ClothingSetting() { Slots = 1, } },
                {"wood.armor.helmet", new ClothingSetting() { Slots = 1, } },
                {"metal.facemask", new ClothingSetting() { Slots = 1, } },
                {"metal.facemask.hockey", new ClothingSetting() { Slots = 1, } },
                {"attire.hide.vest", new ClothingSetting() { Slots = 1, } },
                {"jacket.snow", new ClothingSetting() { Slots = 16, } },
                {"hat.cap", new ClothingSetting() { Slots = 1, } },
                {"roadsign.kilt", new ClothingSetting() { Slots = 1, } },
                {"burlap.gloves", new ClothingSetting() { Slots = 1, } },
                {"jumpsuit.suit", new ClothingSetting() { Slots = 8, } },
                {"attire.ninja.suit", new ClothingSetting() { Slots = 6, } },
                {"hazmatsuit_scientist_arctic", new ClothingSetting() { Slots = 24, } },
                {"hazmatsuit.nomadsuit", new ClothingSetting() { Slots = 12, } },
                {"attire.bunny.onesie", new ClothingSetting() { Slots = 8, } },
                {"halloween.mummysuit", new ClothingSetting() { Slots = 5, } },
                {"ghostsheet", new ClothingSetting() { Slots = 1, } },
                {"hazmatsuit_scientist", new ClothingSetting() { Slots = 24, } },
                {"hazmatsuit_scientist_peacekeeper", new ClothingSetting() { Slots = 24, } },
                {"halloween.surgeonsuit", new ClothingSetting() { Slots = 5, } },
                {"scarecrow.suit", new ClothingSetting() { Slots = 5, } },
                {"barrelcostume", new ClothingSetting() { Slots = 24, } },
                {"attire.egg.suit", new ClothingSetting() { Slots = 4, } },
                {"bone.armor.suit", new ClothingSetting() { Slots = 4, } },
                {"deer.skull.mask", new ClothingSetting() { Slots = 1, } },
                {"attire.bunnyears", new ClothingSetting() { Slots = 1, } },
                {"jacket", new ClothingSetting() { Slots = 8, } },
                {"diving.fins", new ClothingSetting() { Slots = 1, } },
                {"shirt.tanktop", new ClothingSetting() { Slots = 1, } },
                {"hat.oxmask", new ClothingSetting() { Slots = 1, } },
                {"hat.dragonmask", new ClothingSetting() { Slots = 1, } },
                {"hat.ratmask", new ClothingSetting() { Slots = 1, } },
                {"hat.tigermask", new ClothingSetting() { Slots = 1, } },
                {"metal.plate.torso", new ClothingSetting() { Slots = 1, } },
                {"attire.hide.helterneck", new ClothingSetting() { Slots = 1, } },
                {"twitch.headset", new ClothingSetting() { Slots = 1, } },
                {"burlap.shoes", new ClothingSetting() { Slots = 1, } },
                {"attire.banditguard", new ClothingSetting() { Slots = 24, } },
                {"attire.reindeer.headband", new ClothingSetting() { Slots = 1, } },
                {"nightvisiongoggles", new ClothingSetting() { Slots = 1, } },
                {"hat.boonie", new ClothingSetting() { Slots = 1, } },
                {"roadsign.gloves", new ClothingSetting() { Slots = 1, } },
                {"burlap.gloves.new", new ClothingSetting() { Slots = 1, } },
                {"burlap.headwrap", new ClothingSetting() { Slots = 1, } },
                {"scarecrowhead", new ClothingSetting() { Slots = 1, } },
                {"diving.mask", new ClothingSetting() { Slots = 1, } },
                {"attire.hide.poncho", new ClothingSetting() { Slots = 1, } },
                {"partyhat", new ClothingSetting() { Slots = 1, } },
                {"hat.gas.mask", new ClothingSetting() { Slots = 1, } },
                {"shirt.collared", new ClothingSetting() { Slots = 4, } },
                {"burlap.shirt", new ClothingSetting() { Slots = 1, } },
                {"attire.hide.boots", new ClothingSetting() { Slots = 3, } },
                {"boots.frog", new ClothingSetting() { Slots = 2, } },
                {"gloweyes", new ClothingSetting() { Slots = 1, } },
                {"pumpkin", new ClothingSetting() { Slots = 1, } },
                {"jumpsuit.suit.blue", new ClothingSetting() { Slots = 8, } },
                {"sunglasses", new ClothingSetting() { Slots = 1, } },
                {"tactical.gloves", new ClothingSetting() { Slots = 1, } },
                {"lumberjack hoodie", new ClothingSetting() { Slots = 12, } },
                {"hoodie", new ClothingSetting() { Slots = 12, } },
                {"heavy.plate.pants", new ClothingSetting() { Slots = 1, } },
                {"heavy.plate.jacket", new ClothingSetting() { Slots = 1, } },
                {"heavy.plate.helmet", new ClothingSetting() { Slots = 1, } },
                {"twitchsunglasses", new ClothingSetting() { Slots = 1, } },
                {"hat.miner", new ClothingSetting() { Slots = 1, } },
                {"tshirt", new ClothingSetting() { Slots = 1, } },
                {"hat.wolf", new ClothingSetting() { Slots = 1, } },
                {"hat.bunnyhat", new ClothingSetting() { Slots = 1, } },
                {"santahat", new ClothingSetting() { Slots = 1, } },
                {"clatter.helmet", new ClothingSetting() { Slots = 1, } },
                {"riot.helmet", new ClothingSetting() { Slots = 1, } },
                {"bucket.helmet", new ClothingSetting() { Slots = 1, } },
                {"coffeecan.helmet", new ClothingSetting() { Slots = 1, } },
                {"attire.snowman.helmet", new ClothingSetting() { Slots = 1, } },
                {"hat.candle", new ClothingSetting() { Slots = 1, } },
                {"attire.nesthat", new ClothingSetting() { Slots = 1, } },
                {"pants.shorts", new ClothingSetting() { Slots = 2, } },
                {"pants", new ClothingSetting() { Slots = 4, } },
                {"burlap.trousers", new ClothingSetting() { Slots = 1, } },
                {"attire.hide.pants", new ClothingSetting() { Slots = 2, } },
                {"attire.hide.skirt", new ClothingSetting() { Slots = 1, } },
                {"cratecostume", new ClothingSetting() { Slots = 24, } },
            };

            public void Setup()
            {
                var clothing = ItemManager.itemList.Where(x => x.GetComponent<ItemModWearable>());
                bool modified = false;
                foreach (var attire in clothing)
                {
                    if (_plugin.ItemNotClothing(attire.shortname))
                    {
                        //_plugin.Puts($"Item Not Clothing (shortname - {attire.shortname})");
                        continue;
                    }

                    if (!Clothing.ContainsKey(attire.shortname))
                    {
                        Clothing.Add(attire.shortname, new ClothingSetting(attire));
                        modified = true;
                    }
                }

                if (modified)
                {
                    _settingsFile.Save();
                }

                //Outputs default values I put above
                //_plugin.Puts(string.Join("\n", Clothing.Select(x=>$"{{\"{x.Key}\", new ClothingSetting() {{ Slots = {x.Value.Slots}, }} }},").ToArray()));
            }
        }

        bool ItemNotClothing(string shortname)
        {
            switch (shortname)
            {
                case "movembermoustache":
                    return true;
                case "santabeard":
                    return true;
                case "movembermoustachecard":
                    return true;
                case "frankensteins.monster.01.head":
                    return true;
                case "frankensteins.monster.01.legs":
                    return true;
                case "frankensteins.monster.01.torso":
                    return true;
                case "frankensteins.monster.02.head":
                    return true;
                case "frankensteins.monster.02.legs":
                    return true;                
                case "frankensteins.monster.02.torso":
                    return true;
                case "frankensteins.monster.03.head":
                    return true;
                case "frankensteins.monster.03.legs":
                    return true;
                case "frankensteins.monster.03.torso":
                    return true;
                case "sunglasses02black":
                    return true;
                case "sunglasses02camo":
                    return true;
                case "sunglasses02red":
                    return true;
                case "sunglasses03black":
                    return true;
                case "sunglasses03chrome":
                    return true;
                case "sunglasses03gold":
                    return true;
                default:
                    return false;
            }
        }

        public class SlotPlayerData : BasePlayerData
        {
            public void SetSlots(int slots)
            {
                if (Player == null || !Player.userID.IsSteamId() || !Player.IsConnected)
                {
                    return;
                }

                Player.inventory.containerBelt.MarkDirty();
                Player.inventory.containerMain.MarkDirty();

                if (slots <= 6)
                {
                    Player.inventory.containerBelt.capacity = slots;
                    Player.inventory.containerMain.capacity = 0;
                    UpdateInventories();
                    return;
                }

                Player.inventory.containerBelt.capacity = 6;

                if (slots >= 30)
                {
                    Player.inventory.containerMain.capacity = 24;
                }
                else
                {
                    Player.inventory.containerMain.capacity = slots - 6;
                }

                UpdateInventories();
            }

            private void UpdateInventory(ItemContainer container)
            {
                foreach (var invalidItem in container.itemList.ToList())
                {
                    if (invalidItem.position >= container.capacity)
                    {
                        bool hasMovedItem = false;
                        for (int slot = 0; slot < Player.inventory.containerMain.capacity; slot++)
                        {
                            var slotItem = container.GetSlot(slot);
                            if (slotItem != null && invalidItem != null)
                            {
                                if (slotItem.CanStack(invalidItem))
                                {
                                    int maxStack = slotItem.MaxStackable();
                                    if (slotItem.amount < maxStack)
                                    {
                                        slotItem.amount += invalidItem.amount;
                                        if (slotItem.amount > maxStack)
                                        {
                                            invalidItem.amount = slotItem.amount - maxStack;
                                            slotItem.amount = maxStack;
                                        }
                                        else
                                        {
                                            //Combined item
                                            hasMovedItem = true;
                                            invalidItem.Remove();
                                            ItemManager.DoRemoves();
                                            break;
                                        }
                                    }
                                }

                                continue;
                            }

                            invalidItem.position = slot;
                            hasMovedItem = true;
                            break;
                        }

                        if (hasMovedItem == false)
                        {
                            invalidItem.Drop(Player.GetDropPosition(), Player.GetDropVelocity());
                        }
                    }
                }
            }

            private void UpdateInventories()
            {
                if (Player.inventory.containerBelt.capacity < 6)
                {
                    UpdateInventory(Player.inventory.containerBelt);
                }

                if (Player.inventory.containerMain.capacity < 24)
                {
                    UpdateInventory(Player.inventory.containerMain);
                }
            }

            public void UpdateSlots()
            {
                int targetSlots = Settings.DefaultSlots;
                foreach (var clothing in Player.inventory.containerWear.itemList.ToList())
                {
                    targetSlots += Settings.Clothing[clothing.info.shortname].Slots;
                }

                SetSlots(targetSlots);
            }
        }

        #region Lang API

        public Dictionary<string, string> lang_en = new Dictionary<string, string>()
        {

        };

        public static string GetLangMessage(string key, BasePlayer player)
        {
            return _plugin.lang.GetMessage(key, _plugin, player.UserIDString);
        }

        public static string GetLangMessage(string key, ulong player)
        {
            return _plugin.lang.GetMessage(key, _plugin, player.ToString());
        }

        public static string GetLangMessage(string key, string player)
        {
            return _plugin.lang.GetMessage(key, _plugin, player);
        }

        #endregion

        #region PlayerData

        public class BasePlayerData
        {
            [JsonIgnore]
            public BasePlayer Player { get; set; }

            public string userID { get; set; } = "";

            public BasePlayerData()
            {

            }
            public BasePlayerData(BasePlayer player) : base()
            {
                userID = player.UserIDString;
                Player = player;
            }
        }

        public class PlayerDataController<T> where T : BasePlayerData
        {
            [JsonPropertyAttribute(Required = Required.Always)]
            private Dictionary<string, T> playerData { get; set; } = new Dictionary<string, T>();
            private JSONFile<Dictionary<string, T>> _file;
            private Timer _timer;
            public IEnumerable<T> All { get { return playerData.Values; } }

            public PlayerDataController()
            {

            }

            public PlayerDataController(string filename = null)
            {
                if (filename == null)
                {
                    return;
                }

                _file = new JSONFile<Dictionary<string, T>>(filename);
                _timer = _plugin.timer.Every(60.0f, () =>
                {
                    _file.Save();
                });
            }

            public void Unload()
            {
                if (_file == null)
                {
                    return;
                }

                _file.Save();
            }

            public T Get(string identifer)
            {
                T data;
                if (!playerData.TryGetValue(identifer, out data))
                {
                    data = Activator.CreateInstance<T>();
                    playerData[identifer] = data;
                }

                return data;
            }

            public T Get(ulong userID)
            {
                return Get(userID.ToString());
            }

            public T Get(BasePlayer player)
            {
                var data = Get(player.UserIDString);
                data.Player = player;
                return data;
            }

            public bool Has(ulong userID)
            {
                return playerData.ContainsKey(userID.ToString());
            }

            public void Set(string userID, T data)
            {
                playerData[userID] = data;
            }

            public bool Remove(string userID)
            {
                return playerData.Remove(userID);
            }

            public void Update(T data)
            {
                playerData[data.userID] = data;
            }
        }

        #endregion

        #region Configuration Files

        public enum ConfigLocation
        {
            Data = 0,
            Config = 1,
            Logs = 2,
            Plugins = 3,
            Lang = 4,
            Custom = 5,
        }

        public class JSONFile<Type> where Type : class
        {
            private DynamicConfigFile _file;
            public string _name { get; set; }
            public Type Instance { get; set; }
            private ConfigLocation _location { get; set; }
            private string _path { get; set; }

            public JSONFile(string name, ConfigLocation location = ConfigLocation.Data, string path = null, string extension = ".json")
            {
                _name = name.Replace(".json", "");
                _location = location;

                switch (location)
                {
                    case ConfigLocation.Data:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.DataDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Config:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.ConfigDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Logs:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.LogDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Lang:
                        {
                            _path = $"{Oxide.Core.Interface.Oxide.LangDirectory}/{name}{extension}";
                            break;
                        }
                    case ConfigLocation.Custom:
                        {
                            _path = $"{path}/{name}{extension}";
                            break;
                        }
                }

                _file = new DynamicConfigFile(_path);
                _file.Settings = new JsonSerializerSettings() { ReferenceLoopHandling = ReferenceLoopHandling.Ignore };

                Init();
            }

            public virtual void Init()
            {
                Load();
                Save();
                Load();
            }

            public virtual void Load()
            {

                if (!_file.Exists())
                {
                    Save();
                }

                Instance = _file.ReadObject<Type>();

                if (Instance == null)
                {
                    Instance = Activator.CreateInstance<Type>();
                    Save();
                }
                return;
            }

            public virtual void Save()
            {
                _file.WriteObject(Instance);
                return;
            }

            public virtual void Reload()
            {
                Load();
            }

            private void Unload(Plugin sender, PluginManager manager)
            {

            }
        }

        #endregion
    }
}