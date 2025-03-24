using System;
using System.Collections.Generic;
using System.Linq;
using CompanionServer.Handlers;
using Facepunch;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

/*
Fixed noclip not always being set when connecting to server
Fix for Rust update
*/

namespace Oxide.Plugins
{
    [Info("Underworld", "nivex", "1.0.8")]
    [Description("Teleports youu under the world when disconnected.")]
    class Underworld : RustPlugin
    {
        [PluginReference] Plugin Vanish;

        private const string permBlocked = "underworld.blocked";
        private const string permName = "underworld.use";
        private StoredData storedData = new();
        private List<ulong> protect = new();

        public class StoredData
        {
            public Dictionary<string, UserInfo> Users = new();
        }

        public class UserInfo
        {
            public string Home { get; set; } = Vector3.zero.ToString();
            public bool WakeOnLand { get; set; } = true;
            public bool SaveInventory { get; set; } = true;
            public bool AutoNoClip { get; set; } = true;
            public List<UnderworldItem> Items { get; set; } = new();
        }

        public class UnderworldItem
        {
            public List<UnderworldItem> contents { get; set; }
            public string container { get; set; } = "main";
            public int ammo { get; set; }
            public int amount { get; set; }
            public string ammoType { get; set; }
            public float condition { get; set; }
            public float fuel { get; set; }
            public int frequency { get; set; }
            public int itemid { get; set; }
            public float maxCondition { get; set; }
            public string name { get; set; }
            public int position { get; set; } = -1;
            public ulong skin { get; set; }
            public string text { get; set; }
            public int blueprintAmount { get; set; }
            public int blueprintTarget { get; set; }
            public int dataInt { get; set; }
            public ulong subEntity { get; set; }
            public bool shouldPool { get; set; }

            public UnderworldItem() { }

            public UnderworldItem(string container, Item item)
            {
                this.container = container;
                itemid = item.info.itemid;
                name = item.name;
                text = item.text;
                amount = item.amount;
                condition = item.condition;
                maxCondition = item.maxCondition;
                fuel = item.fuel;
                position = item.position;
                skin = item.skin;

                if (item.instanceData != null)
                {
                    dataInt = item.instanceData.dataInt;
                    blueprintAmount = item.instanceData.blueprintAmount;
                    blueprintTarget = item.instanceData.blueprintTarget;
                    subEntity = item.instanceData.subEntity.Value;
                    shouldPool = item.instanceData.ShouldPool;
                }

                if (item.GetHeldEntity() is HeldEntity e)
                {
                    if (e is BaseProjectile baseProjectile)
                    {
                        ammo = baseProjectile.primaryMagazine.contents;
                        ammoType = baseProjectile.primaryMagazine.ammoType.shortname;
                    }
                    else if (e is FlameThrower flameThrower)
                    {
                        ammo = flameThrower.ammo;
                    }
                }

                if (ItemModAssociatedEntity<PagerEntity>.GetAssociatedEntity(item) is PagerEntity pagerEntity)
                {
                    frequency = pagerEntity.GetFrequency();
                }

                if (item.contents?.itemList?.Count > 0)
                {
                    contents = new();

                    foreach (var mod in item.contents.itemList)
                    {
                        if (!Blacklisted(mod))
                        {
                            contents.Add(new("default", mod));
                        }
                    }
                }
            }

            public static Item Create(UnderworldItem uwi)
            {
                if (uwi.itemid == 0 || string.IsNullOrEmpty(uwi.container))
                {
                    return null;
                }

                Item item;
                if (uwi.blueprintTarget != 0)
                {
                    item = ItemManager.Create(Workbench.GetBlueprintTemplate());
                    item.blueprintTarget = uwi.blueprintTarget;
                    item.amount = uwi.blueprintAmount;
                }
                else item = ItemManager.CreateByItemID(uwi.itemid, uwi.amount, uwi.skin);

                if (item == null)
                {
                    return null;
                }

                if (uwi.blueprintAmount != 0 || uwi.blueprintTarget != 0 || uwi.dataInt != 0 || uwi.subEntity != 0)
                {
                    item.instanceData = uwi.shouldPool ? Pool.Get<ProtoBuf.Item.InstanceData>() : new ProtoBuf.Item.InstanceData();
                    item.instanceData.ShouldPool = uwi.shouldPool;
                    item.instanceData.blueprintAmount = uwi.blueprintAmount;
                    item.instanceData.blueprintTarget = uwi.blueprintTarget;
                    item.instanceData.dataInt = uwi.dataInt;
                    item.instanceData.subEntity = new(uwi.subEntity);
                }

                if (!string.IsNullOrEmpty(uwi.name))
                {
                    item.name = uwi.name;
                }

                if (!string.IsNullOrEmpty(uwi.text))
                {
                    item.text = uwi.text;
                }

                if (item.GetHeldEntity() is HeldEntity e)
                {
                    if (item.skin != 0)
                    {
                        e.skinID = item.skin;
                    }

                    if (e is BaseProjectile baseProjectile)
                    {
                        baseProjectile.DelayedModsChanged();
                        baseProjectile.primaryMagazine.contents = uwi.ammo;
                        if (!string.IsNullOrEmpty(uwi.ammoType))
                        {
                            baseProjectile.primaryMagazine.ammoType = ItemManager.FindItemDefinition(uwi.ammoType);
                        }
                    }
                    else if (e is FlameThrower flameThrower)
                    {
                        flameThrower.ammo = uwi.ammo;
                    }
                    else if (e is Chainsaw chainsaw)
                    {
                        chainsaw.ammo = uwi.ammo;
                    }

                    e.SendNetworkUpdate();
                }

                if (uwi.frequency > 0 && item.info.GetComponentInChildren<ItemModRFListener>() is ItemModRFListener rfListener)
                {
                    if (item.instanceData.subEntity.IsValid && BaseNetworkable.serverEntities.Find(item.instanceData.subEntity) is PagerEntity pagerEntity)
                    {
                        pagerEntity.ChangeFrequency(uwi.frequency);
                    }
                }

                if (uwi.contents != null)
                {
                    foreach (var uwm in uwi.contents)
                    {
                        Item mod = Create(uwm);

                        if (mod != null && !mod.MoveToContainer(item.contents))
                        {
                            mod.Remove();
                        }
                    }
                }

                if (item.hasCondition)
                {
                    item._maxCondition = uwi.maxCondition;
                    item._condition = uwi.condition;
                }

                item.fuel = uwi.fuel;
                item.MarkDirty();

                return item;
            }

            public static void Restore(BasePlayer player, UnderworldItem uwi)
            {
                Item item = Create(uwi);

                if (item == null)
                {
                    return;
                }

                ItemContainer newcontainer = uwi.container switch
                {
                    "belt" => player.inventory.containerBelt,
                    "wear" => player.inventory.containerWear,
                    "main" or _ => player.inventory.containerMain,
                };

                if (!item.MoveToContainer(newcontainer, uwi.position, true))
                {
                    player.GiveItem(item);
                }
            }
        }

        private void OnNewSave()
        {
            if (wipeSaves)
            {
                wipeSaves = false; 
                
                foreach (var pair in storedData.Users)
                {
                    pair.Value.Items.Clear();
                }

                SaveData();
            }
        }

        private void Init()
        {
            Unsubscribe(nameof(OnPlayerSleep));
            Unsubscribe(nameof(OnPlayerSleepEnded));
            Unsubscribe(nameof(OnPlayerRespawned));

            permission.RegisterPermission(permBlocked, this);
            permission.RegisterPermission(permName, this);

            LoadData();
            LoadVariables();
        }

        private void OnServerInitialized()
        {
            Subscribe(nameof(OnPlayerSleep));
            Subscribe(nameof(OnPlayerSleepEnded));
            Subscribe(nameof(OnPlayerRespawned));

            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }

        private void Unload()
        {
            SaveData();
            Blacklist.Clear();
        }

        private object OnPlayerViolation(BasePlayer player, AntiHackType type, float amount)
        {
            if (type == AntiHackType.InsideTerrain && GetUser(player, false) != null)
            {
                return true;
            }
            return null;
        }

        private void OnEntityTakeDamage(BasePlayer player, HitInfo hitInfo)
        {
            if (protect.Count == 0) Unsubscribe(nameof(OnEntityTakeDamage));
            if (hitInfo == null || !player || !protect.Contains(player.userID)) return;
            if (hitInfo.damageTypes.Has(Rust.DamageType.Cold)) hitInfo.damageTypes = new();
            if (hitInfo.damageTypes.Has(Rust.DamageType.Drowned)) hitInfo.damageTypes = new();
            if (hitInfo.damageTypes.Has(Rust.DamageType.Suicide) && player.IsSleeping() && player.IdleTime > 0.5f) hitInfo.damageTypes = new();
        }

        private void OnPlayerSleep(BasePlayer player)
        {
            NextTick(() =>
            {
                if (!player || player.IsDestroyed || player.IsConnected || !IsAllowed(player))
                {
                    return;
                }

                var user = GetUser(player, false);

                if (user == null)
                {
                    return;
                }

                var userHome = user.Home.ToVector3();
                var position = userHome == Vector3.zero ? defaultPos : userHome;

                if (position == Vector3.zero)
                {
                    position = new Vector3(player.transform.position.x, TerrainMeta.HeightMap.GetHeight(player.transform.position) - 5f, player.transform.position.z);
                }

                SaveInventory(player, user);

                player.Teleport(position);
                player.ChatMessage("Sleep underworld");
            });
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            var user = GetUser(player);

            if (user == null || !player.IsConnected)
            {
                return;
            }

            if (!protect.Contains(player.userID))
            {
                protect.Add(player.userID);
                Subscribe(nameof(OnEntityTakeDamage));
            }

            if (player.IsDead())
            {
                return;
            }

            if (player.IsSleeping())
            {
                StopDrowning(player);
                timer.Once(0.5f, () => OnPlayerConnected(player));
                return;
            }

            if (user.WakeOnLand)
            {
                float y = TerrainMeta.HeightMap.GetHeight(player.transform.position);
                player.Teleport(player.transform.position.WithY(y + 2f));
                player.SendNetworkUpdateImmediate();
                player.ChatMessage("Awake on land");
            }

            if (user.AutoNoClip)
            {
                player.Invoke(() =>
                {
                    if (!player.IsFlying)
                    {
                        player.SendConsoleCommand("noclip");
                    }
                }, 0.1f);
            }

            Disappear(player);

            ulong userid = player.userID;

            timer.Once(5f, () =>
            {
                protect.Remove(userid);
                if (protect.Count == 0) Unsubscribe(nameof(OnEntityTakeDamage));
            });
        }

        private void Disappear(BasePlayer player)
        {
            if (autoVanish)
            {
                if (!player.limitNetworking) Vanish?.Call("Disappear", player);
                else Vanish?.Call("VanishGui", player);
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            var user = GetUser(player);

            if (user == null)
            {
                return;
            }

            protect.Remove(player.userID);

            if (maxHHT)
            {
                player.health = 100f;
                player.metabolism.hydration.value = player.metabolism.hydration.max;
                player.metabolism.calories.value = player.metabolism.calories.max;
            }

            if (allowSaveInventory && user.SaveInventory && user.Items.Count > 0)
            {
                List<Item> items = Pool.Get<List<Item>>();
                int count = player.inventory.GetAllItems(items);
                Pool.FreeUnmanaged(ref items);
                if (count == 2)
                {
                    if (player.inventory.GetAmount(ItemManager.FindItemDefinition("rock").itemid) == 1)
                    {
                        if (player.inventory.GetAmount(ItemManager.FindItemDefinition("torch").itemid) == 1)
                        {
                            player.inventory.Strip();
                        }
                    }
                }

                foreach (var uwi in user.Items.ToList())
                {
                    UnderworldItem.Restore(player, uwi);
                }

                user.Items.Clear();
                SaveData();
            }

            Disappear(player);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (!player || !player.IsConnected)
            {
                return;
            }

            var user = GetUser(player);

            if (user == null)
            {
                return;
            }

            Disappear(player);

            if (user.AutoNoClip)
            {
                player.Invoke(() =>
                {
                    if (!player.IsFlying)
                    {
                        player.SendConsoleCommand("noclip");
                    }
                }, 0.1f);
            }
        }

        [ChatCommand("uw")]
        private void cmdUnderworld(BasePlayer player, string command, string[] args)
        {
            if (!IsAllowed(player))
            {
                Message(player, "NoPermission");
                return;
            }

            var user = GetUser(player);

            if (user == null)
            {
                Message(player, "NoPermission");
                return;
            }

            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "tp":
                        {
                            player.Teleport(user.Home.ToVector3());
                            break;
                        }
                    case "save":
                        {
                            if (!allowSaveInventory)
                                return;

                            user.SaveInventory = !user.SaveInventory;
                            Message(player, user.SaveInventory ? "SavingInventory" : "NotSavingInventory");
                            SaveData();
                        }
                        return;
                    case "set":
                        {
                            var position = player.transform.position;

                            if (args.Length == 4)
                            {
                                if (args[1].All(char.IsDigit) && args[2].All(char.IsDigit) && args[3].All(char.IsDigit))
                                {
                                    var customPos = new Vector3(float.Parse(args[1]), 0f, float.Parse(args[3]));

                                    if (Vector3.Distance(customPos, Vector3.zero) <= TerrainMeta.Size.x / 1.5f)
                                    {
                                        customPos.y = float.Parse(args[2]);

                                        if (customPos.y > -100f && customPos.y < 4400f)
                                            position = customPos;
                                        else
                                            Message(player, "OutOfBounds");
                                    }
                                    else
                                        Message(player, "OutOfBounds");
                                }
                                else
                                    Message(player, "Help1", FormatPosition(user.Home.ToVector3()));
                            }

                            user.Home = position.ToString();
                            Message(player, "PositionAdded", FormatPosition(position));
                            SaveData();
                        }
                        return;
                    case "reset":
                        {
                            user.Home = Vector3.zero.ToString();

                            if (defaultPos != Vector3.zero)
                            {
                                user.Home = defaultPos.ToString();
                                Message(player, "PositionRemoved2", user.Home);
                            }
                            else
                                Message(player, "PositionRemoved1");

                            SaveData();
                        }
                        return;
                    case "wakeup":
                        {
                            user.WakeOnLand = !user.WakeOnLand;
                            Message(player, user.WakeOnLand ? "PlayerWakeUp" : "PlayerWakeUpReset");
                            SaveData();
                        }
                        return;
                    case "noclip":
                        {
                            user.AutoNoClip = !user.AutoNoClip;
                            Message(player, user.AutoNoClip ? "PlayerNoClipEnabled" : "PlayerNoClipDisabled");
                            SaveData();
                        }
                        return;
                    case "g":
                    case "ground":
                        {
                            player.Teleport(new Vector3(player.transform.position.x, TerrainMeta.HeightMap.GetHeight(player.transform.position) + 1f, player.transform.position.z));
                        }
                        return;
                }
            }

            string homePos = FormatPosition(user.Home.ToVector3() == Vector3.zero ? defaultPos : user.Home.ToVector3());

            Message(player, "Help0", user.SaveInventory && allowSaveInventory);
            Message(player, "Help1", homePos);
            Message(player, "Help2");
            Message(player, "Help3", user.WakeOnLand);
            Message(player, "Help4", user.AutoNoClip);
            Message(player, "Help5");
        }

        public void StopDrowning(BasePlayer player)
        {
            if (player.transform.position.y < WaterSystem.OceanLevel && !player.IsFlying)
            {
                player.SendConsoleCommand("noclip");
            }
        }

        private UserInfo GetUser(BasePlayer player, bool f = true)
        {
            if (!player || f && !player.IsConnected || !IsAllowed(player) || permission.UserHasPermission(player.UserIDString, permBlocked))
            {
                return null;
            }

            if (!storedData.Users.TryGetValue(player.UserIDString, out var user))
            {
                storedData.Users[player.UserIDString] = user = new();
            }

            return user;
        }

        public string FormatPosition(Vector3 position)
        {
            return $"{position.x:N2} {position.y:N2} {position.z:N2}";
        }

        private void LoadData()
        {
            try { storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name); } catch (Exception ex) { Puts(ex.ToString()); } finally { storedData ??= new(); }
        }

        private void SaveData()
        {
            if (storedData != null)
            {
                Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
            }
        }

        private void SaveInventory(BasePlayer player, UserInfo user)
        {
            if (!allowSaveInventory || !user.SaveInventory)
            {
                return;
            }

            List<Item> itemList = Pool.Get<List<Item>>();
            int num = player.inventory.GetAllItems(itemList);
            Pool.FreeUnmanaged(ref itemList);

            if (num == 0)
            {
                user.Items.Clear();
                SaveData();
                return;
            }

            List<UnderworldItem> items = new();

            foreach (Item item in player.inventory.containerWear.itemList.ToList())
            {
                if (Blacklisted(item)) continue;
                items.Add(new("wear", item));
                item.Remove();
            }

            foreach (Item item in player.inventory.containerMain.itemList.ToList())
            {
                if (Blacklisted(item)) continue;
                items.Add(new("main", item));
                item.Remove();
            }

            foreach (Item item in player.inventory.containerBelt.itemList.ToList())
            {
                if (Blacklisted(item)) continue;
                items.Add(new("belt", item));
                item.Remove();
            }

            if (items.Count == 0)
            {
                return;
            }

            ItemManager.DoRemoves();
            user.Items.Clear();
            user.Items.AddRange(items);
            SaveData();
        }

        private bool IsAllowed(BasePlayer player)
        {
            return player != null && (player.IsAdmin || DeveloperList.Contains(player.userID) || permission.UserHasPermission(player.UserIDString, permName));
        }

        private static bool Blacklisted(Item item)
        {
            return item == null || Blacklist.Contains(item.info.shortname) || Blacklist.Contains(item.info.itemid.ToString());
        }

        #region Config

        private bool Changed;
        private Vector3 defaultPos;
        private bool allowSaveInventory;
        private bool maxHHT;
        private bool autoVanish;
        private static List<string> Blacklist = new();
        private bool wipeSaves;

        private List<object> DefaultBlacklist
        {
            get
            {
                return new()
                {
                    "2080339268",
                    "can.tuna.empty"
                };
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new()
            {
                ["PositionAdded"] = "You will now teleport to <color=yellow>{0}</color> on disconnect.",
                ["PositionRemoved1"] = "You will now teleport under ground on disconnect.",
                ["PositionRemoved2"] = "You will now teleport to <color=yellow>{0}</color> on disconnect.",
                ["PlayerWakeUp"] = "You will now teleport above ground when you wake up.",
                ["PlayerWakeUpReset"] = "You will no longer teleport above ground when you wake up.",
                ["PlayerNoClipEnabled"] = "You will now automatically be noclipped on reconnect.",
                ["PlayerNoClipDisabled"] = "You will no longer be noclipped on reconnect.",
                ["SavingInventory"] = "Your inventory will be saved and stripped on disconnect, and restored when you wake up.",
                ["NotSavingInventory"] = "Your inventory will no longer be saved.",
                ["Help0"] = "/uw save - toggles saving inventory (enabled: {0})",
                ["Help1"] = "/uw set <x y z> - sets your log out position. can specify coordinates <color=yellow>{0}</color>",
                ["Help2"] = "/uw reset - resets your log out position to be underground unless a position is configured in the config file",
                ["Help3"] = "/uw wakeup - toggle waking up on land (enabled: {0})",
                ["Help4"] = "/uw noclip - toggle auto noclip on reconnect (enabled: {0})",
                ["Help5"] = "/uw g - teleport to the ground",
                ["OutOfBounds"] = "The specified coordinates are not within the allowed boundaries of the map.",
                ["NoPermission"] = "You do not have permission to use this command."
            }, this);
        }

        private void LoadVariables()
        {
            maxHHT = Convert.ToBoolean(GetConfig("Settings", "Set Health, Hunger and Thirst to Max", false));
            defaultPos = GetConfig("Settings", "Default Teleport To Position On Disconnect", "(0, 0, 0)").ToString().ToVector3();
            allowSaveInventory = Convert.ToBoolean(GetConfig("Settings", "Allow Save And Strip Admin Inventory On Disconnect", true));
            Blacklist = (GetConfig("Settings", "Blacklist", DefaultBlacklist) as List<object>).Where(o => o != null && o.ToString().Length > 0).Cast<string>().ToList();
            autoVanish = Convert.ToBoolean(GetConfig("Settings", "Auto Vanish On Connect", true));
            wipeSaves = Convert.ToBoolean(GetConfig("Settings", "Wipe Saved Inventories On Map Wipe", false));
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
            LoadVariables();
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new();
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

        private void Message(BasePlayer player, string key, params object[] args)
        {
            if (player != null)
            {
                Player.Message(player, GetMessage(key, player.UserIDString, args));
            }
        }

        private string GetMessage(string key, string id = null, params object[] args)
        {
            return args.Length > 0 ? string.Format(lang.GetMessage(key, this, id), args) : lang.GetMessage(key, this, id);
        }

        #endregion
    }
}