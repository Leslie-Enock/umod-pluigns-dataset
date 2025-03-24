using System.Collections.Generic;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{
    [Info("Customizable Magazines", "Razor", "1.1.5")]
    [Description("Change the Magazines Around.")]
    public class CustomizableMagazines : RustPlugin
    {
        #region Init/Unloading
        bool debug = false;
        static CustomizableMagazines Instance;
        private ItemModMagazine _itemModMagazine;
        private string MagazineUse = "customizablemagazines.admin";

        private void Init()
        {
            permission.RegisterPermission(MagazineUse, this);

            if (configData.mags.Count <= 0)
            {
                configData.mags.Add(2892143123, new customMagazines("Extended Magazine 15%", 1.5f, 1.0f, new List<string>() { "crate_basic", "crate_normal", "crate_normal_2" }));
                configData.mags.Add(2892142979, new customMagazines("Extended Magazine 30%", 1.75f, 1.0f, new List<string>() { "crate_basic", "crate_normal", "crate_normal_2" }));
                configData.mags.Add(2892142846, new customMagazines("Extended Magazine 50%", 2.0f, 1.0f, new List<string>() { "crate_basic", "crate_normal", "crate_normal_2" }));
                configData.mags.Add(2892142705, new customMagazines("Extended Magazine 100%", 3.0f, 1.0f, new List<string>() { "crate_basic", "crate_normal", "crate_normal_2" }));
                SaveConfig();
            }
        }

        private void OnServerInitialized()
        {
            var magazineItemDef = ItemManager.FindItemDefinition("weapon.mod.extendedmags");
            _itemModMagazine = magazineItemDef.gameObject.AddComponent<ItemModMagazine>();
            AddToItemDefinition(magazineItemDef, _itemModMagazine);
        }

        private void Unload()
        {
            var magazineItemDef = ItemManager.FindItemDefinition("weapon.mod.extendedmags");
            if (_itemModMagazine != null)
            {
                RemoveFromItemDefinition(magazineItemDef, _itemModMagazine);
            }

            UnityEngine.Object.DestroyImmediate(_itemModMagazine);
            Instance = null;
        }
        #endregion

        #region ItemModMagazine
        private static void AddToItemDefinition(ItemDefinition itemDefinition, ItemMod itemMod)
        {
            if (itemDefinition.itemMods.Contains(itemMod))
                return;

            var length = itemDefinition.itemMods.Length;
            Array.Resize(ref itemDefinition.itemMods, length + 1);
            itemDefinition.itemMods[length] = itemMod;
        }

        private static void RemoveFromItemDefinition(ItemDefinition itemDefinition, ItemMod itemMod)
        {
            if (!itemDefinition.itemMods.Contains(itemMod))
                return;

            itemDefinition.itemMods = itemDefinition.itemMods.Where(mod => mod != itemMod).ToArray();
        }

        private class ItemModMagazine : ItemMod
        {
            public override void OnParentChanged(Item item)
            {
                if (Instance != null && item != null && item.parent != null && item.skin != 0UL && Instance.configData.mags.ContainsKey(item.skin))
                {
                    ProjectileWeaponMod held = item.GetHeldEntity() as ProjectileWeaponMod;
                    if (held == null)
                        return;

                    SetupMagazine(held, item, item.skin);
                }
            }
        }
        #endregion

        #region hooks
        void OnLootSpawn(LootContainer container)
        {
            if (container == null || container.inventory == null || !configData.settings.randomMagazine || container.ShortPrefabName == "stocking_large_deployed" ||
                container.ShortPrefabName == "stocking_small_deployed") return;
            foreach (var ItemsConfig in configData.mags)
            {
                bool ItemAdded = false;
                foreach (var LootContainers in ItemsConfig.Value.LootContainers)
                {
                    if (LootContainers == null || ItemsConfig.Value.SpawnChance <= 0)
                        continue;

                    if (LootContainers.Contains(container.ShortPrefabName))
                    {
                        if (UnityEngine.Random.Range(0, 100) < ItemsConfig.Value.SpawnChance)
                        {
                            if (container.inventory.itemList.Count == container.inventory.capacity)
                            {
                                container.inventory.capacity++;
                            }
                            string name = ItemsConfig.Value.displayName;
                            var MagazineItem = ItemManager.CreateByItemID(2005491391, 1, ItemsConfig.Key);
                            MagazineItem.MoveToContainer(container.inventory);
                            if (debug) PrintWarning($"{name} Spawned in container {LootContainers} At: {container.transform.position}");
                            ItemAdded = true;
                            break;
                        }
                    }
                }
                if (ItemAdded)
                    break;
            }
        }

        private static void SetupMagazine(ProjectileWeaponMod mag, Item item, ulong configName)
        {
            if (mag != null && Instance.configData.mags.ContainsKey(configName))
            {
                customMagazines magconfig = Instance.configData.mags[configName];

                mag.magazineCapacity.scalar = magconfig.totalAmmo;
                mag.skinID = configName;
                item.skin = configName;
                mag.name = magconfig.displayName;
                item.name = magconfig.displayName;
                item.MarkDirty();
                mag.SendNetworkUpdateImmediate();
            }
        }
        #endregion

        #region Class Definitions
        public class customMagazines
        {
            [JsonProperty(PropertyName = "Magazine Display Name")]
            public string displayName;
            [JsonProperty(PropertyName = "Ammo Multiplier 1.0 = default Gun")]
            public float totalAmmo;
            [JsonProperty(PropertyName = "Can Spawn In LootContainer types")]
            public List<string> LootContainers;
            [JsonProperty(PropertyName = "LootContainer Spawn Chance 1-100")]
            public float SpawnChance;

            public customMagazines(string displayName, float totalAmmo, float reloadTime, List<string> lootContainers)
            {
                this.displayName = displayName;
                this.totalAmmo = totalAmmo;
                this.LootContainers = lootContainers;
                this.SpawnChance = 20f;
            }
        }

        #endregion

        #region Configuration
        [JsonObject(MemberSerialization.OptIn)]
        class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public Settings settings { get; set; } = new Settings();

            [JsonProperty(PropertyName = "Magazine settings")]
            public Dictionary<ulong, customMagazines> mags { get; set; } = new Dictionary<ulong, customMagazines>();

            public class Settings
            {
                [JsonProperty("Enable Loot Container Spawns")]
                public bool randomMagazine { get; set; }
            }

            [JsonProperty(PropertyName = "Version")]
            public VersionNumber Version { get; set; } = Instance.Version;

            public VersionNumber LastBreakingChange { get; private set; } = new VersionNumber(1, 1, 2);
        }
        #endregion

        #region Configuration Handling
        private ConfigData configData;

        protected override void LoadConfig()
        {
            Instance = this;

            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                    LoadDefaultConfig();
                UpdateConfigVersion();
            }
            catch
            {
                PrintError("Your configuration file is invalid");
                UpdateConfig();
                return;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => configData = new ConfigData();

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfig()
        {
            PrintWarning("Invalid config file detected! Backing up current and creating new config...");
            var outdatedConfig = Config.ReadObject<object>();
            Config.WriteObject(outdatedConfig, filename: $"{Name}.Backup");
            LoadDefaultConfig();
            PrintWarning("Config update completed!");
        }

        void UpdateConfigVersion() => configData.Version = Version;
        #endregion

        #region Commands    
        [ConsoleCommand("magazine")]
        private void CmdConsoleMagazine(ConsoleSystem.Arg args)
        {
            if (args == null || args.Args.Length < 2) return;

            string userID = args.Args[0];
            string portal = args.Args[1];

            BasePlayer player = null;
            var ids = default(ulong);
            var skinId = default(ulong);
            int total = 1;

            if (ulong.TryParse(userID, out ids))
            {
                player = BasePlayer.FindByID(ids);
            }

            if (!ulong.TryParse(portal, out skinId))
            {
                SendReply(args, "Incorrect SkinID format");
                return;
            }

            if (args.Args.Length >= 3)
            {
                if (!int.TryParse(args.Args[2], out total))
                {
                    SendReply(args, "Amount not set correctly");
                    return;
                }
            }

            if (player != null)
            {
                string[] theItemConfig = args.Args.ToArray();
                GetTheMagazine(null, "", theItemConfig);
            }
            else
            {
                SendReply(args, "Player not found");
            }
        }

        [ChatCommand("magazine")]
        private void GetTheMagazine(BasePlayer player, string command, string[] args)
        {
            if (player == null)
            {
                var ids = default(ulong);
                if (ulong.TryParse(args[0], out ids))
                {
                    player = BasePlayer.FindByID(ids);
                    if (player == null)
                        return;
                    args = args.Skip(1).ToArray();
                }
                else
                    return;
            }

            else if (player.net?.connection != null && !permission.UserHasPermission(player.UserIDString, MagazineUse))
            {
                SendReply(player, string.Format(lang.GetMessage("NoPerm", this, player.UserIDString)));
                return;
            }

            if (args == null || args.Length <= 0)
            {
                messagePlayer(player);
                return;
            }

            if (args[0].ToLower() == "list")
            {
                messagePlayer(player);
                return;
            }

            var theItemConfig = default(ulong);
            if (!ulong.TryParse(args[0], out theItemConfig))
            {
                messagePlayer(player);
                return;
            }

            int total = 1;

            if (args.Length > 1)
            {
                if (!int.TryParse(args[1], out total))
                    total = 1;
            }

            if (!configData.mags.ContainsKey(theItemConfig))
            {
                messagePlayer(player);
            }

            else if (configData.mags.ContainsKey(theItemConfig))
            {
                GetMagazineItem(player, theItemConfig, total, true);
                return;
            }
            SendReply(player, lang.GetMessage("NoValidItem", this, player.UserIDString), theItemConfig);
        }

        private void messagePlayer(BasePlayer player)
        {
            string configitems = "<color=#ce422b>Magazine Item List Usage /magazine <SkinID></color>\n\n";
            foreach (var key in configData.mags)
            {
                configitems += $"<color=#FFFF00>Item Skin</color>: {key.Key} <color=#FFFF00>Item Name:</color> {key.Value.displayName}\n";
            }
            SendReply(player, configitems);
        }

        private void GetMagazineItem(BasePlayer player, ulong skinID, int total, bool message)
        {
            customMagazines magconfig = Instance.configData.mags[skinID];
            var MagazineItem = ItemManager.CreateByItemID(2005491391, total, skinID);
            if (MagazineItem == null) return;
            MagazineItem.name = magconfig.displayName;
            ProjectileWeaponMod held = MagazineItem.GetHeldEntity() as ProjectileWeaponMod;

            if (MagazineItem.MoveToContainer(player.inventory.containerBelt, -1, true))
            {
                if (message) SendReply(player, lang.GetMessage("gaveProtector", this), magconfig.displayName);
                return;
            }
            else if (MagazineItem.MoveToContainer(player.inventory.containerMain, -1, true))
            {
                if (message) SendReply(player, lang.GetMessage("gaveProtector", this), magconfig.displayName);
                return;
            }
            Vector3 velocity = new Vector3(-107.3504f, 12.1489f, -107.7641f);
            velocity = Vector3.zero;
            MagazineItem.Drop(player.transform.position + new Vector3(0.5f, 1f, 0), velocity);
            if (message) SendReply(player, lang.GetMessage("droped", this), magconfig.displayName);
        }

        #endregion

        #region Messages
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPerm"] = "<color=#ce422b>You lack the permishions to use this command!</color>",
                ["gaveProtector"] = "<color=#ce422b>You have just got a {0}!</color>",
                ["droped"] = "<color=#ce422b>You'r inventory was full so i dropped your {0} on the ground!</color>",
                ["blocked"] = "<color=#ce422b>You are building blocked!</color>",
                ["NoValidItem"] = "That is not a valid config item {0}!",
                ["NoPlayer"] = "Player not found!",
                ["ammountNot"] = "Amount not set correctly",
                ["SkinIDformat"] = "Incorrect skinID format"
            }, this);
        }
        #endregion
    }
}
    
