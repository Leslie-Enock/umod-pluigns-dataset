using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CompanionServer.Handlers;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using UnityEngine;
using Network;

namespace Oxide.Plugins
{
    [Info("Admin Utilities", "dFxPhoeniX", "2.1.4")]
    [Description("Toggle NoClip, teleport Under Terrain and more")]
    public class AdminUtilities : RustPlugin
    {
        private const string permTerrain = "adminutilities.disconnectteleport";
        private const string permInventory = "adminutilities.saveinventory";
        private const string permHHT = "adminutilities.maxhht";
        private const string permNoClip = "adminutilities.nocliptoggle";
        private const string permGodMode = "adminutilities.godmodetoggle";

        private DataFileSystem dataFile;
        private DataFileSystem dataFileItems;

        private bool newSave;

        ////////////////////////////////////////////////////////////
        // Files
        ////////////////////////////////////////////////////////////

        private class PlayerInfo
        {
            public string Teleport { get; set; } = Vector3.zero.ToString();
            public bool SaveInventory { get; set; } = true;
            public bool UnderTerrain { get; set; } = false;
            public bool NoClip { get; set; } = false;
            public bool GodMode { get; set; } = false;
        }

        private Dictionary<string, PlayerInfo> playerInfoCache = new Dictionary<string, PlayerInfo>();

        private class PlayerInfoItems
        {
            public List<AdminUtilitiesItem> Items { get; set; } = new List<AdminUtilitiesItem>();
        }

        private Dictionary<string, PlayerInfoItems> playerInfoItemsCache = new Dictionary<string, PlayerInfoItems>();

        private class AdminUtilitiesItem
        {
            public List<AdminUtilitiesItem> contents { get; set; }
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

            public AdminUtilitiesItem() { }

            public AdminUtilitiesItem(string container, Item item)
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
                        contents.Add(new("default", mod));
                    }
                }
            }

            public static Item Create(AdminUtilitiesItem aui)
            {
                if (aui.itemid == 0 || string.IsNullOrEmpty(aui.container))
                {
                    return null;
                }

                Item item;
                if (aui.blueprintTarget != 0)
                {
                    item = ItemManager.Create(Workbench.GetBlueprintTemplate());
                    item.blueprintTarget = aui.blueprintTarget;
                    item.amount = aui.blueprintAmount;
                }
                else item = ItemManager.CreateByItemID(aui.itemid, aui.amount, aui.skin);

                if (item == null)
                {
                    return null;
                }

                if (aui.blueprintAmount != 0 || aui.blueprintTarget != 0 || aui.dataInt != 0 || aui.subEntity != 0)
                {
                    item.instanceData = aui.shouldPool ? Pool.Get<ProtoBuf.Item.InstanceData>() : new ProtoBuf.Item.InstanceData();
                    item.instanceData.ShouldPool = aui.shouldPool;
                    item.instanceData.blueprintAmount = aui.blueprintAmount;
                    item.instanceData.blueprintTarget = aui.blueprintTarget;
                    item.instanceData.dataInt = aui.dataInt;
                    item.instanceData.subEntity = new(aui.subEntity);
                }

                if (!string.IsNullOrEmpty(aui.name))
                {
                    item.name = aui.name;
                }

                if (!string.IsNullOrEmpty(aui.text))
                {
                    item.text = aui.text;
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
                        baseProjectile.primaryMagazine.contents = aui.ammo;
                        if (!string.IsNullOrEmpty(aui.ammoType))
                        {
                            baseProjectile.primaryMagazine.ammoType = ItemManager.FindItemDefinition(aui.ammoType);
                        }
                    }
                    else if (e is FlameThrower flameThrower)
                    {
                        flameThrower.ammo = aui.ammo;
                    }
                    else if (e is Chainsaw chainsaw)
                    {
                        chainsaw.ammo = aui.ammo;
                    }

                    e.SendNetworkUpdate();
                }

                if (aui.frequency > 0 && item.info.GetComponentInChildren<ItemModRFListener>() is ItemModRFListener rfListener)
                {
                    if (item.instanceData.subEntity.IsValid && BaseNetworkable.serverEntities.Find(item.instanceData.subEntity) is PagerEntity pagerEntity)
                    {
                        pagerEntity.ChangeFrequency(aui.frequency);
                    }
                }

                if (aui.contents != null)
                {
                    foreach (var aum in aui.contents)
                    {
                        Item mod = Create(aum);

                        if (mod != null && !mod.MoveToContainer(item.contents))
                        {
                            mod.Remove();
                        }
                    }
                }

                if (item.hasCondition)
                {
                    item._maxCondition = aui.maxCondition;
                    item._condition = aui.condition;
                }

                item.fuel = aui.fuel;
                item.MarkDirty();

                return item;
            }

            public static void Restore(BasePlayer player, AdminUtilitiesItem aui)
            {
                Item item = Create(aui);

                if (item == null)
                {
                    return;
                }

                ItemContainer newcontainer = aui.container switch
                {
                    "belt" => player.inventory.containerBelt,
                    "wear" => player.inventory.containerWear,
                    "main" or _ => player.inventory.containerMain,
                };

                if (!item.MoveToContainer(newcontainer, aui.position, true))
                {
                    player.GiveItem(item);
                }
            }
        }

        private PlayerInfo LoadPlayerInfo(BasePlayer player)
        {
            if (dataFile == null)
                return null;

            if (playerInfoCache.ContainsKey(player.UserIDString))
            {
                return playerInfoCache[player.UserIDString];
            }
            else
            {
                PlayerInfo user = dataFile.ReadObject<PlayerInfo>($"{player.UserIDString}");

                playerInfoCache[player.UserIDString] = user;

                return user;
            }
        }

        private void SavePlayerInfo(BasePlayer player, PlayerInfo playerInfo)
        {
            if (dataFile == null)
                return;

            dataFile.WriteObject<PlayerInfo>($"{player.UserIDString}", playerInfo);

            if (playerInfoCache.ContainsKey(player.UserIDString))
            {
                playerInfoCache.Remove(player.UserIDString);
            }
        }

        private PlayerInfoItems LoadPlayerInfoItems(BasePlayer player)
        {
            if (dataFileItems == null)
                return null;

            if (playerInfoItemsCache.ContainsKey(player.UserIDString))
            {
                return playerInfoItemsCache[player.UserIDString];
            }
            else
            {
                PlayerInfoItems userItems = dataFileItems.ReadObject<PlayerInfoItems>($"{player.UserIDString}");

                playerInfoItemsCache[player.UserIDString] = userItems;

                return userItems;
            }
        }

        private void SavePlayerInfoItems(BasePlayer player, PlayerInfoItems playerInfoItems)
        {
            if (dataFileItems == null)
                return;

            dataFileItems.WriteObject<PlayerInfoItems>($"{player.UserIDString}", playerInfoItems);

            if (playerInfoItemsCache.ContainsKey(player.UserIDString))
            {
                playerInfoItemsCache.Remove(player.UserIDString);
            }
        }

        ////////////////////////////////////////////////////////////
        // Oxide Hooks
        ////////////////////////////////////////////////////////////

        private void OnNewSave()
        {
            newSave = true;
        }

        private void Init()
        {
            InitConfig();

            dataFile = new DataFileSystem($"{Interface.Oxide.DataDirectory}\\AdminUtilities\\Settings");
            dataFileItems = new DataFileSystem($"{Interface.Oxide.DataDirectory}\\AdminUtilities\\Items");
        }

        private void Loaded()
        {
            permission.RegisterPermission(permTerrain, this);
            permission.RegisterPermission(permInventory, this);
            permission.RegisterPermission(permHHT, this);
            permission.RegisterPermission(permNoClip, this);
            permission.RegisterPermission(permGodMode, this);
        }

        private void OnServerInformationUpdated()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                PlayerInfo user = LoadPlayerInfo(player);

                if (user == null)
                    return;

                timer.Once(0.2f, () => {
                    if (player.IsDeveloper && !(player.IsFlying || player.IsGod()))
                    {
                        player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, false);
                    }
                });
            }
        }

        private object OnClientCommand(Connection connection, string command)
        {
            if (command.Contains("/"))
                return null;

            BasePlayer player = BasePlayer.FindByID(connection.userid);

            if (player == null)
                return null;

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return null;

            string lowerCommand = command.ToLower();

            if (lowerCommand.Contains("god"))
            {
                if (!HasPermission(player, permGodMode) && !user.GodMode && !player.IsGod())
                    return false;
            }

            return null;
        }

        private void OnServerInitialized()
        {
            if (GetSave())
            {
                if (wipeSettings)
                {
                    string folderPath = $"{Interface.Oxide.DataDirectory}/AdminUtilities/Settings";

                    if (!Directory.Exists(folderPath))
                    {
                        return;
                    }

                    string[] Files = Directory.GetFiles(folderPath);
                    foreach (string file in Files)
                    {
                        File.Delete(file);
                    }
                }

                if (wipeItems)
                {
                    string folderItemsPath = $"{Interface.Oxide.DataDirectory}/AdminUtilities/Items";

                    if (!Directory.Exists(folderItemsPath))
                    {
                        return;
                    }

                    string[] ItemsFiles = Directory.GetFiles(folderItemsPath);
                    foreach (string file in ItemsFiles)
                    {
                        File.Delete(file);
                    }
                }

                newSave = false;
            }
        }

        private void OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null)
                return;

            if (player.IsNpc || (player is NPCPlayer))
                return;

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            if (user.UnderTerrain)
            {
                info.damageTypes = new Rust.DamageTypeList();
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            if (!HasPermission(player, permNoClip) && user.NoClip)
            {
                user.NoClip = false;
                SavePlayerInfo(player, user);
            }

            if (!HasPermission(player, permGodMode) && user.GodMode)
            {
                user.GodMode = false;
                SavePlayerInfo(player, user);
            }

            if (!persistentNoClip && user.NoClip)
            {
                user.NoClip = false;
                SavePlayerInfo(player, user);
            }

            if (!persistentGodMode && user.GodMode)
            {
                user.GodMode = false;
                SavePlayerInfo(player, user);
            }

            if (player.IsDead())
                return;

            if (HasPermission(player, permTerrain))
            {
                DisconnectTeleport(player);
            }

            if (HasPermission(player, permInventory) && user.SaveInventory)
            {
                SaveInventory(player);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            if (!HasPermission(player, permTerrain) && user.UnderTerrain)
            {
                NoUnderTerrain(player);
            }

            if (persistentNoClip && HasPermission(player, permNoClip) && user.NoClip)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, true);

                timer.Once(0.2f, () => {
                    player.SendConsoleCommand("noclip");
                });
            }

            if (persistentGodMode && HasPermission(player, permGodMode) && user.GodMode)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, true);

                timer.Once(0.2f, () => {
                    player.SendConsoleCommand("setinfo \"global.god\" \"True\"");
                });
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (HasPermission(player, permHHT))
            {
                player.health = 100f;
                player.metabolism.hydration.value = player.metabolism.hydration.max;
                player.metabolism.calories.value = player.metabolism.calories.max;
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!player || !player.IsConnected)
                return;

            if (HasPermission(player, permTerrain))
            {
                NoUnderTerrain(player);
            }

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            PlayerInfoItems userItems = LoadPlayerInfoItems(player);

            if (userItems == null)
                return;

            if (HasPermission(player, permInventory) && user.SaveInventory && userItems.Items.Count > 0)
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

                if (userItems.Items.Any(item => item.amount > 0))
                {
                    foreach (var aui in userItems.Items.ToList())
                    {
                        if (aui.amount > 0)
                        {
                            AdminUtilitiesItem.Restore(player, aui);
                        }
                    }
                }

                userItems.Items.Clear();
                SavePlayerInfoItems(player, userItems);
            }
        }

        private object OnPlayerViolation(BasePlayer player, AntiHackType type)
        {
            if (player.IsSleeping() && (type == AntiHackType.InsideTerrain) && HasPermission(player, permTerrain))
                return true;

            if (player.IsFlying && (type == AntiHackType.FlyHack || type == AntiHackType.InsideTerrain || type == AntiHackType.NoClip) && HasPermission(player, permNoClip))
                return true;

            return null;
        }

        ////////////////////////////////////////////////////////////
        // Commands
        ////////////////////////////////////////////////////////////

        [ChatCommand("disconnectteleport")]
        private void cmdDisconnectTeleport(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permTerrain))
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            if (args.Length >= 1)
            {
                switch (args[0].ToLower())
                {
                    case "set":
                    {
                        if (args.Length == 4 && float.TryParse(args[1], out float x) && float.TryParse(args[2], out float y) && float.TryParse(args[3], out float z))
                        {
                            var customPos = new Vector3(x, y, z);

                            if (Vector3.Distance(customPos, Vector3.zero) <= TerrainMeta.Size.x / 1.5f && customPos.y > -100f && customPos.y < 4400f)
                            {
                                user.Teleport = customPos.ToString();
                                Player.Message(player, msg("PositionAdded", player.UserIDString, FormatPosition(customPos)));
                            }
                            else
                            {
                                Player.Message(player, msg("OutOfBounds", player.UserIDString));
                            }
                        }
                        else
                        {
                            Player.Message(player, msg("DisconnectTeleportSet", player.UserIDString, FormatPosition(user.Teleport.ToVector3())));
                        }

                        SavePlayerInfo(player, user);
                        return;
                    }
                    case "reset":
                    {
                        user.Teleport = defaultPos.ToString();
                        string message = defaultPos != Vector3.zero ? msg("PositionRemoved2", player.UserIDString, defaultPos) : msg("PositionRemoved1", player.UserIDString);
                        Player.Message(player, message);
                        SavePlayerInfo(player, user);
                        return;
                    }
                }
            }

            string teleportPos = FormatPosition(user.Teleport.ToVector3() == Vector3.zero ? defaultPos : user.Teleport.ToVector3());
            Player.Message(player, msg("DisconnectTeleportSet", player.UserIDString, teleportPos));
            Player.Message(player, msg("DisconnectTeleportReset", player.UserIDString));
        }

        [ChatCommand("saveinventory")]
        private void cmdSaveInventory(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permInventory))
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            user.SaveInventory = !user.SaveInventory;
            Player.Message(player, msg(user.SaveInventory ? "SavingInventory" : "NotSavingInventory", player.UserIDString));
            SavePlayerInfo(player, user);
        }

        [ChatCommand("noclip")]
        private void cmdNoClip(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permNoClip))
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            ToggleNoClip(player);
        }

        [ChatCommand("god")]
        private void cmdGodMode(BasePlayer player, string command, string[] args)
        {
            if (!HasPermission(player, permGodMode))
            {
                Player.Message(player, msg("NoPermission", player.UserIDString));
                return;
            }

            ToggleGodMode(player);
        }

        ////////////////////////////////////////////////////////////
        // General Methods
        ////////////////////////////////////////////////////////////

        private bool GetSave()
        {
            if (newSave || BuildingManager.server.buildingDictionary.Count == 0)
            {
                return true;
            }

            return false;
        }

        private void ToggleNoClip(BasePlayer player)
        {
            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, true);
            if (!player.IsFlying)
            {
                timer.Once(0.2f, () => {
                    player.SendConsoleCommand("noclip");
                    Player.Message(player, msg("FlyEnabled", player.UserIDString));
                });
            }
            else
            {
                player.SendConsoleCommand("noclip");
                Player.Message(player, msg("FlyDisabled", player.UserIDString));
            }

            user.NoClip = !player.IsFlying;
            SavePlayerInfo(player, user);
        }

        private void ToggleGodMode(BasePlayer player)
        {
            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsDeveloper, true);
            if (!player.IsGod())
            {
                timer.Once(0.2f, () => {
                    player.SendConsoleCommand("setinfo \"global.god\" \"True\"");
                    Player.Message(player, msg("GodEnabled", player.UserIDString));
                });
            }
            else
            {
                player.SendConsoleCommand("setinfo \"global.god\" \"False\"");
                Player.Message(player, msg("GodDisabled", player.UserIDString));
            }

            user.GodMode = !player.IsGod();
            SavePlayerInfo(player, user);
        }

        private void DisconnectTeleport(BasePlayer player)
        {
            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            var userTeleport = user.Teleport.ToVector3();
            var position = userTeleport == Vector3.zero ? defaultPos : userTeleport;

            if (position == Vector3.zero)
            {
                position = new Vector3(player.transform.position.x, TerrainMeta.HeightMap.GetHeight(player.transform.position) - 5f, player.transform.position.z);
            }

            player.Teleport(position);

            float terrainHeight = TerrainMeta.HeightMap.GetHeight(player.transform.position);
            bool isUnderTerrain = player.transform.position.y < terrainHeight || player.IsHeadUnderwater();

            if (isUnderTerrain)
            {
                player.metabolism.temperature.min = 20;
                player.metabolism.temperature.max = 20;
                player.metabolism.radiation_poison.max = 0;
                player.metabolism.oxygen.min = 1;
                player.metabolism.wetness.max = 0;
                player.metabolism.calories.min = player.metabolism.calories.value;
                player.metabolism.isDirty = true;
                player.metabolism.SendChangesToClient();
                user.UnderTerrain = true;
                SavePlayerInfo(player, user);
            }
        }

        private void NoUnderTerrain(BasePlayer player)
        {
            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return;

            float terrainHeight = TerrainMeta.HeightMap.GetHeight(player.transform.position);
            bool isUnderTerrain = player.transform.position.y < terrainHeight || player.IsHeadUnderwater();

            if (isUnderTerrain)
            {
                float newY = terrainHeight + 2f;
                player.Teleport(new Vector3(player.transform.position.x, newY, player.transform.position.z));
                player.SendNetworkUpdateImmediate();
                player.metabolism.temperature.min = -100;
                player.metabolism.temperature.max = 100;
                player.metabolism.radiation_poison.max = 500;
                player.metabolism.oxygen.min = 0;
                player.metabolism.calories.min = 0;
                player.metabolism.wetness.max = 1;
                player.metabolism.SendChangesToClient();
                user.UnderTerrain = false;
                SavePlayerInfo(player, user);
            }
        }

        private string FormatPosition(Vector3 position)
        {
            string x = position.x.ToString("N2");
            string y = position.y.ToString("N2");
            string z = position.z.ToString("N2");

            return $"{x} {y} {z}";
        }

        private void SaveInventory(BasePlayer player)
        {
            PlayerInfoItems userItems = LoadPlayerInfoItems(player);

            if (userItems == null)
                return;

            List<Item> itemList = Pool.Get<List<Item>>();
            int num = player.inventory.GetAllItems(itemList);
            Pool.FreeUnmanaged(ref itemList);

            if (num == 0)
            {
                userItems.Items.Clear();
                SavePlayerInfoItems(player, userItems);
                return;
            }

            var items = new List<AdminUtilitiesItem>();

            AddItemsFromContainer(player.inventory.containerWear, "wear", items);
            AddItemsFromContainer(player.inventory.containerMain, "main", items);
            AddItemsFromContainer(player.inventory.containerBelt, "belt", items);

            if (items.Count == 0)
            {
                return;
            }

            ItemManager.DoRemoves();
            userItems.Items.Clear();
            userItems.Items.AddRange(items);
            SavePlayerInfoItems(player, userItems);
        }

        private void AddItemsFromContainer(ItemContainer container, string containerName, List<AdminUtilitiesItem> items)
        {
            foreach (Item item in container.itemList)
            {
                items.Add(new AdminUtilitiesItem(containerName, item));
                item.Remove();
            }

            container.itemList.Clear();
        }

        private bool HasPermission(BasePlayer player, string permName)
        {
            return player != null && player.IPlayer.HasPermission(permName);
        }

        private string msg(string key, string id = null, params object[] args)
        {
            string message = id == null ? RemoveFormatting(lang.GetMessage(key, this, id)) : lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(message, args) : message;
        }

        private string RemoveFormatting(string source)
        {
            return source.Contains(">") ? Regex.Replace(source, "<.*?>", string.Empty) : source;
        }

        ////////////////////////////////////////////////////////////
        // Configs
        ////////////////////////////////////////////////////////////

        private bool ConfigChanged;
        private Vector3 defaultPos;
        private bool wipeItems;
        private bool wipeSettings;
        private bool persistentNoClip;
        private bool persistentGodMode;

        protected override void LoadDefaultConfig() => PrintWarning("Generating default configuration file...");

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PositionAdded"] = "You will now teleport to <color=orange>{0}</color> on disconnect.",
                ["PositionRemoved1"] = "You will now teleport under ground on disconnect.",
                ["PositionRemoved2"] = "You will now teleport to <color=orange>{0}</color> on disconnect.",
                ["SavingInventory"] = "Your inventory will be saved on disconnect and restored when you wake up.",
                ["NotSavingInventory"] = "Your inventory will no longer be saved.",
                ["DisconnectTeleportSet"] = "/disconnectteleport set <x y z> - sets your log out position. can specify coordinates <color=orange>{0}</color>",
                ["DisconnectTeleportReset"] = "/disconnectteleport reset - resets your log out position to be underground unless a position is configured in the config file",
                ["OutOfBounds"] = "The specified coordinates are not within the allowed boundaries of the map.",
                ["FlyDisabled"] = "You switched NoClip off!",
                ["FlyEnabled"] = "You switched NoClip on!",
                ["GodDisabled"] = "You switched GodMode off!",
                ["GodEnabled"] = "You switched GodMode on!",
                ["NoPermission"] = "You do not have permission to use this command."
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PositionAdded"] = "Te vei teleporta la <color=orange>{0}</color> odată cu deconectarea.",
                ["PositionRemoved1"] = "Te vei teleporta sub pământ odată cu deconectarea.",
                ["PositionRemoved2"] = "Te vei teleporta la <color=orange>{0}</color> odată cu deconectarea.",
                ["SavingInventory"] = "Inventarul tău va fi salvat la deconectare și recuperat la reconectare.",
                ["NotSavingInventory"] = "Inventarul tău nu va mai fi salvat.",
                ["DisconnectTeleportSet"] = "/disconnectteleport set <x y z> - setează poziția ta de deconectare. poți specifica coordonatele <color=orange>{0}</color>",
                ["DisconnectTeleportReset"] = "/disconnectteleport reset - resetează poziția ta de deconectare pentru a fi sub pământ, doar dacă o poziție nu este configurată într-o filă de configurare",
                ["OutOfBounds"] = "Coordonatele specificate nu se încadrează în limitele hărții.",
                ["FlyDisabled"] = "Ai dezactivat NoClip-ul!!",
                ["FlyEnabled"] = "Ai activat NoClip-ul!",
                ["GodDisabled"] = "Ai dezactivat GodMode-ul!",
                ["GodEnabled"] = "Ai activat GodMode-ul!",
                ["NoPermission"] = "Nu ai permisiunea de a folosi această comandă."
            }, this, "ro");
        }

        private void InitConfig()
        {
            defaultPos = GetConfig("(0, 0, 0)", "Settings", "Default Teleport To Position On Disconnect").ToVector3();
            wipeItems = GetConfig(false, "Settings", "Wipe Saved Inventories On Map Wipe");
            wipeSettings = GetConfig(false, "Settings", "Wipe Players Settings On Map Wipe");
            persistentNoClip = GetConfig(false, "Settings", "Enable Persistent NoClip");
            persistentGodMode = GetConfig(false, "Settings", "Enable Persistent GodMode");

            if (ConfigChanged)
            {
                PrintWarning("Updated configuration file with new/changed values.");
                SaveConfig();
            }
        }

        private T GetConfig<T>(T defaultVal, params string[] path)
        {
            var data = Config.Get(path);
            if (data != null)
            {
                return Config.ConvertValue<T>(data);
            }

            Config.Set(path.Concat(new object[] { defaultVal }).ToArray());
            ConfigChanged = true;
            return defaultVal;
        }

        ////////////////////////////////////////////////////////////
        // Plugin Hooks
        ////////////////////////////////////////////////////////////

        [HookMethod(nameof(API_ToggleNoClip))]
        public void API_ToggleNoClip(BasePlayer player)
        {
            ToggleNoClip(player);
        }

        [HookMethod(nameof(API_ToggleGodMode))]
        public void API_ToggleGodMode(BasePlayer player)
        {
            ToggleGodMode(player);
        }

        [HookMethod(nameof(API_GetDisconnectTeleportPos))]
        public object API_GetDisconnectTeleportPos(string userid)
        {
            BasePlayer player = BasePlayer.FindByID(Convert.ToUInt64(userid));

            if (player == null)
                return null;

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return null;

           return user.Teleport;
        }

        [HookMethod(nameof(API_GetSaveInventoryStatus))]
        public bool API_GetSaveInventoryStatus(string userid)
        {
            BasePlayer player = BasePlayer.FindByID(Convert.ToUInt64(userid));

            if (player == null)
                return false;

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return false;

            return user.SaveInventory;
        }

        [HookMethod(nameof(API_GetUnderTerrainStatus))]
        public bool API_GetUnderTerrainStatus(string userid)
        {
            BasePlayer player = BasePlayer.FindByID(Convert.ToUInt64(userid));

            if (player == null)
                return false;

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return false;

            return user.UnderTerrain;
        }

        [HookMethod(nameof(API_GetNoClipStatus))]
        public bool API_GetNoClipStatus(string userid)
        {
            BasePlayer player = BasePlayer.FindByID(Convert.ToUInt64(userid));

            if (player == null)
                return false;

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return false;

            return user.NoClip;
        }

        [HookMethod(nameof(API_GetGodModeStatus))]
        public bool API_GetGodModeStatus(string userid)
        {
            BasePlayer player = BasePlayer.FindByID(Convert.ToUInt64(userid));

            if (player == null)
                return false;

            PlayerInfo user = LoadPlayerInfo(player);

            if (user == null)
                return false;

            return user.GodMode;
        }
    }
}