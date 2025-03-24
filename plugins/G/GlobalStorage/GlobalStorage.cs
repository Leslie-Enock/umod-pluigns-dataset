using Facepunch;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/* Ideas
    Add tabbed/page storage to the containers so users can access multiple boxes as tabs.
    Add config option to make it so chests are only deployable in TC range.
 */

/* Changes 1.0.5
 * Fixed a lang issue with black listed items being dropped.
 * Fixed a bug with ammo not being handled when destroying/rebuilding an item.
 * Added whitelist option.
 * Added RustEdit method to assign boxes. Requires 2 anchor entities.
 * Updated to support item flags.
 * Patched for May Update.
 */

namespace Oxide.Plugins
{
    [Info("Global Storage", "imthenewguy", "1.0.5")]
    [Description("Create global storage chests in safezone monuments and by placing the item.")]
    class GlobalStorage : RustPlugin
    {
        #region Config

        private Configuration config;
        public class Configuration
        {
            [JsonProperty("Storage prefab profile to use")]
            public string storage_prefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";

            [JsonProperty("Amount of slots that the players can access? Max value is the maximum number of slots for the container type.")]
            public int slot_count = 30;

            [JsonProperty("Box skin")]
            public ulong box_skin = 1499104921;

            [JsonProperty("Display floating text above the containers indicating that it is a storage unit?")]
            public bool draw_text = true;

            [JsonProperty("If floating text is enabled, how often should it update?")]
            public float draw_update = 5f;

            [JsonProperty("Maximum distance away from the box that the player can see the text?")]
            public float draw_distance = 30f;

            [JsonProperty("Floating text color [white, black, red, blue, green, cyan, grey, magenta, yellow]")]
            public string text_col = "cyan";

            [JsonProperty("Display floating text above manually deployed containers?")]
            public bool draw_text_non_monument = true;

            [JsonProperty("Make player deployed global storage chests invulnerable?")]
            public bool deployed_chests_invulnerable = true;

            [JsonProperty("Auto wipe monument and player data on new server wipe")]
            public bool auto_wipe = true;

            [JsonProperty("A list of item shortnames that cannot be placed into the chest")]
            public List<string> black_list = new List<string>();

            [JsonProperty("If populated with anything, these items will be the only items allowed in the container")]
            public List<string> white_list = new List<string>();

            [JsonProperty("Monument modifiers")]
            public List<Configuration.monumentInfo> monuments = new List<Configuration.monumentInfo>();

            [JsonProperty("Physical storage box prefab to spawn in the world")]
            public string spawn_prefab = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";

            [JsonProperty("Item that is given when using the giveglobalbox commands. Be sure to use the shortname that matches the prefab that will be spawning.")]
            public string item_shortname = "box.wooden.large";

            [JsonProperty("Static box spawns on a map that can access global storage")]
            public List<CustomPosInfo> customPosBoxes = new List<CustomPosInfo>();

            [JsonProperty("Anchored boxes placed in rust edit. Requires 2 anchor entities to find it.")]
            public List<AnchorInfo> RustEdit_Boxes = new List<AnchorInfo>();

            [JsonProperty("Custom item whitelist filter settings")]
            public FilterInfo filterSettings = new FilterInfo();            

            public class monumentInfo
            {
                public string name;
                public bool enabled;
                public Vector3 pos;
                public Vector3 rot;
                public monumentInfo(string monument, bool enabled, Vector3 pos, Vector3 rot)
                {
                    this.enabled = enabled;
                    this.pos = pos;
                    this.rot = rot;
                    this.name = monument;
                }
            }

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        public class FilterInfo
        {
            [JsonProperty("Prevent other items from being added to the box besides the items whose names/shortnames are included in the list below??")]
            public bool exclude_everything_else = false;

            [JsonProperty("List of items that you would like to allow (item names/shortnames)?")]
            public List<string> filters = new List<string>();
        }

        public class AnchorInfo
        {
            [JsonProperty("Enabled?")]
            public bool enabled = false;

            [JsonProperty("Primary anchor entity shortname")]
            public string primary_entity = "pookie_deployed";

            [JsonProperty("Secondary anchor entity shortname")]
            public string secondary_entity = "mailbox.deployed";            

            [JsonProperty("How far away from the chest are the anchor entities")]
            public float distance = 2f;
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
            config.monuments = DefaultMonuments;
            config.black_list = new List<string>() { "cassette", "cassette.medium", "cassette.short", "boombox", "fun.boomboxportable", "fun.casetterecorder" };
            config.RustEdit_Boxes.Add(new AnchorInfo());
            config.filterSettings.filters.Add("Transporters");
        }

        private List<Configuration.monumentInfo> DefaultMonuments
        {
            get
            {
                return new List<Configuration.monumentInfo>
                {
                    new Configuration.monumentInfo("assets/bundled/prefabs/autospawn/monument/fishing_village/fishing_village_b.prefab", true, new Vector3(-10.1f, 2f, 20.4f), new Vector3(0, 270f, 0)),
                    new Configuration.monumentInfo("assets/bundled/prefabs/autospawn/monument/medium/bandit_town.prefab", true, new Vector3(9.0f, 2.8f, 0.7f), new Vector3(0, -90f, 0)),
                    new Configuration.monumentInfo("assets/bundled/prefabs/autospawn/monument/medium/compound.prefab", true, new Vector3(-24.1f, 0.2f, 13.1f), new Vector3(0, 90f, 0)),
                    new Configuration.monumentInfo("assets/bundled/prefabs/autospawn/monument/fishing_village/fishing_village_a.prefab", true, new Vector3(19.0f, 2.0f, -3.8f), new Vector3(0, 0, 0)),
                    new Configuration.monumentInfo("assets/bundled/prefabs/autospawn/monument/fishing_village/fishing_village_c.prefab", true, new Vector3(-5.3f, 2.0f, -2.0f), new Vector3(0, 180, 0))
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintToConsole("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintToConsole($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            PrintToConsole($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion

        #region Data

        PlayerEntity pcdData;
        private DynamicConfigFile PCDDATA;

        const string perms_admin = "globalstorage.admin";
        const string perms_chat = "globalstorage.chat";
        const string perms_access = "globalstorage.access";

        void Init()
        {
            PCDDATA = Interface.Oxide.DataFileSystem.GetFile(this.Name);
            permission.RegisterPermission(perms_admin, this);
            permission.RegisterPermission(perms_chat, this);
            permission.RegisterPermission(perms_access, this);
            LoadData();

            usingWhiteList = config.white_list.Count > 0 ? true : false;

            if (config.RustEdit_Boxes.Count == 0)
            {
                config.RustEdit_Boxes.Add(new AnchorInfo());
                SaveConfig();
            }
        }

        public bool usingWhiteList;

        void Unload()
        {
            UnityEngine.Object.Destroy(NoDamageProtection);
            foreach (var player in BasePlayer.activePlayerList)
            {
                player.EndLooting();
            }

            foreach (var container in boxes.ToList())
            {
                if ((pcdData.monuments.ContainsKey(container.Key) && pcdData.monuments[container.Key].monument == "deployed") || (map_boxes.Contains(container.Value))) continue;
                pcdData.monuments.Remove(container.Key);
                container.Value.KillMessage();
            }
            
            foreach (var storage in pcdData.storage)
            {
                List<StorageInfo> _storage = Pool.GetList<StorageInfo>();
                _storage.AddRange(storage.Value._storage);
                foreach (var item in _storage)
                {
                    if ((usingWhiteList && !config.white_list.Contains(item.shortname)) || (!usingWhiteList && config.black_list.Contains(item.shortname))) storage.Value._storage.Remove(item);
                }
                Pool.FreeList(ref _storage);
            }
            SaveData();
        }

        void OnServerSave()
        {
            SaveData();
        }


        void SaveData()
        {
            PCDDATA.WriteObject(pcdData);
        }

        void LoadData()
        {
            try
            {
                pcdData = Interface.Oxide.DataFileSystem.ReadObject<PlayerEntity>(this.Name);
            }
            catch
            {
                Puts("Couldn't load player data, creating new Playerfile");
                pcdData = new PlayerEntity();
            }
        }

        class StorageBox
        {
            public List<StorageInfo> _storage = new List<StorageInfo>();            
        }

        public class StorageInfo
        {
            public string shortname;
            public string displayName;
            public ulong skin;
            public int amount;
            public int slot;
            public float condition;
            public float maxCondition;
            public int ammo;
            public string ammotype;
            public StorageInfo[] contents;
            public InstancedInfo instanceData;
            public Item.Flag flags;
            public class InstancedInfo
            {
                public bool ShouldPool;
                public int dataInt;
                public int blueprintTarget;
                public int blueprintAmount;
                public ulong subEntity;
            }
            public string text;
        }

        class PlayerEntity
        {
            public Dictionary<ulong, StorageBox> storage = new Dictionary<ulong, StorageBox>();
            public Dictionary<ulong, monumentInfo> monuments = new Dictionary<ulong, monumentInfo>();
            public bool purgeActive = false;
            public Dictionary<ulong, AnchorInfo> RustEdit_storage_data = new Dictionary<ulong, AnchorInfo>();
        }

        public class monumentInfo
        {
            public string monument;
            public bool enabled;
            public Vector3 pos;
            public Vector3 rot;
            public monumentInfo(string monument, bool enabled, Vector3 pos, Vector3 rot)
            {
                this.monument = monument;
                this.enabled = enabled;
                this.pos = pos;
                this.rot = rot;
            }
        }

        public class CustomPosInfo
        {
            public Vector3 pos;
            public Vector3 rot;
            public bool enabled;
            public CustomPosInfo(bool enabled, Vector3 pos, Vector3 rot)
            {
                this.enabled = enabled;
                this.pos = pos;
                this.rot = rot;
            }
        }

        #endregion;

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["WarnDelayTime"] = "Please wait a moment before attempting to open the box again.",
                ["FloatingText"] = "<size=26>Global Storage Chest</size>",
                ["MorePlayersFound"] = "More than one player found: {0}",
                ["NoMatch"] = "No player was found that matched: {0}",
                ["GaveBoxes"] = "Gave {0} {1}x Global Storage Boxes",
                ["ReceiveBoxes"] = "You received {0}x Global Storage Boxes",
                ["AlreadySetup"] = "This box is already setup as a Global Storage Chest.",
                ["SetupBox"] = "Set {0} up as a Global Storage container. OwnerID: {1}",
                ["NoAccessPerms"] = "You do not have permissions to access global storage.",
                ["NoLock"] = "You cannot deploy a lock on a global storage chest.",
                ["BlackList"] = "This item has been black listed from global storage.",
                ["Whitelist"] = "This item is not white listed for global storage.",
                ["ValidUsage"] = "Valid usage: /giveglobalbox <name/id> <quantity>",
                ["ConsoleGave"] = "Gave {0} {1}x Global Storage Boxes",
                ["BlacklistedItemsFound"] = "Found items in your container that are black listed. They have been returned/dropped near you.\n",
                ["WhitelistedItemsFound"] = "Found items in your container that are not on the whitelist. They have been returned/dropped near you.\n",
                ["RemovedContainer"] = "Removed container at: {0}",
                ["PurgeActive"] = "You cannot access global storage while purge is active!",
                ["PurgeEnabledAnnouncement"] = "Purge is enabled. Global Storage can no longer be active.",
                ["PurgeDisabledAnnouncement"] = "Purge is no longer enabled. You can now access global storage."
            }, this);
        }

        #endregion

        #region Storage

        List<StorageContainer> containers = new List<StorageContainer>();

        Dictionary<ulong, float> bagCooldownTimer = new Dictionary<ulong, float>();

        StorageBox storageData;

        private void OpenStorage(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, perms_access))
            {
                PrintToChat(player, lang.GetMessage("NoAccessPerms", this, player.UserIDString));
                return;
            }
            if (bagCooldownTimer.ContainsKey(player.userID))
            {
                if (bagCooldownTimer[player.userID] > Time.time)
                {
                    PrintToChat(player, lang.GetMessage("WarnDelayTime", this, player.UserIDString));
                    return;
                }
                bagCooldownTimer.Remove(player.userID);
            }
            if (!bagCooldownTimer.ContainsKey(player.userID))
            {
                bagCooldownTimer.Add(player.userID, Time.time + 2f);                
            }
            player.EndLooting();

            if (pcdData.purgeActive)
            {
                PrintToChat(player, lang.GetMessage("PurgeActive", this, player.UserIDString));
                return;
            }

            object hookResult = Interface.CallHook("CanAccessGlobalStorage", player);
            if (hookResult is string && hookResult != null) return;

            var pos = new Vector3(player.transform.position.x, player.transform.position.y - 1000, player.transform.position.z);
            var storage = GameManager.server.CreateEntity(config.storage_prefab, pos) as StorageContainer;            
            storage.Spawn();
            storage.inventory.capacity = config.slot_count;
            storage.inventorySlots = config.slot_count;
            storage.baseProtection = NoDamageProtection;
            UnityEngine.Object.DestroyImmediate(storage.GetComponent<GroundWatch>());
            UnityEngine.Object.DestroyImmediate(storage.GetComponent<DestroyOnGroundMissing>());
            storage.OwnerID = player.userID;            

            if (pcdData.storage.TryGetValue(player.userID, out storageData) && storageData._storage.Count > 0)
            {
                foreach (var itemDef in storageData._storage)
                {
                    var item = ItemManager.CreateByName(itemDef.shortname, itemDef.amount, itemDef.skin);
                    if (itemDef.displayName != null) item.name = itemDef.displayName;
                    item.condition = itemDef.condition;
                    item.maxCondition = itemDef.maxCondition;
                    BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;
                    if (weapon != null)
                    {
                        if (!string.IsNullOrEmpty(itemDef.ammotype))
                            weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(itemDef.ammotype);
                        weapon.primaryMagazine.contents = itemDef.ammo;
                    }
                    FlameThrower flameThrower = item.GetHeldEntity() as FlameThrower;
                    if (flameThrower != null)
                        flameThrower.ammo = itemDef.ammo;
                    if (itemDef.contents != null)
                    {
                        foreach (StorageInfo contentData in itemDef.contents)
                        {
                            Item newContent = ItemManager.CreateByName(contentData.shortname, contentData.amount);
                            if (newContent != null)
                            {
                                newContent.condition = contentData.condition;
                                newContent.MoveToContainer(item.contents);
                            }
                        }
                    }
                    item.flags = itemDef.flags;
                    if (itemDef.instanceData != null)
                    {
                        item.instanceData = new ProtoBuf.Item.InstanceData();
                        item.instanceData.ShouldPool = itemDef.instanceData.ShouldPool;
                        item.instanceData.dataInt = itemDef.instanceData.dataInt;
                        item.instanceData.blueprintTarget = itemDef.instanceData.blueprintTarget;
                        item.instanceData.blueprintAmount = itemDef.instanceData.blueprintAmount;
                    }

                    if (itemDef.text != null) item.text = itemDef.text;

                    item.MoveToContainer(storage.inventory, itemDef.slot, true, true);
                }
            }

            containers.Add(storage);

            timer.Once(0.1f, () =>
            {
                if (storage != null) storage.PlayerOpenLoot(player, "", false);
                Interface.CallHook("OnGlobalStorageOpened", player, storage);
            });
        }

        void StoreContainerLoot(BasePlayer player, StorageContainer container)
        {
            if (!pcdData.storage.TryGetValue(player.userID, out storageData))
            {
                pcdData.storage.Add(player.userID, new StorageBox());
                storageData = pcdData.storage[player.userID];
            }
            if (storageData._storage.Count != 0) storageData._storage.Clear();
            List<StorageInfo> items = new List<StorageInfo>();
            var droppedItemsStr = "";
            List<Item> temp_items_list = Pool.GetList<Item>();
            temp_items_list.AddRange(container.inventory.itemList);
            
            foreach (var item in temp_items_list)
            {
                if ((usingWhiteList && !config.white_list.Contains(item.info.shortname)) || (!usingWhiteList && config.black_list.Contains(item.info.shortname)))
                {
                    player.GiveItem(item);
                    //item.DropAndTossUpwards(player.transform.position, 2);
                    droppedItemsStr += $"{item.name ?? item.info.displayName.english}\n";
                    continue;
                }
                if (config.filterSettings.exclude_everything_else)
                {
                    bool allowed = false;
                    foreach (var entry in config.filterSettings.filters)
                    {
                        if (item.name != null && item.name.Contains(entry))
                        {
                            allowed = true;
                            break;
                        }
                        if (item.info.shortname.Contains(entry))
                        {
                            allowed = true;
                            break;
                        }
                    }
                    if (!allowed)
                    {
                        player.GiveItem(item);
                        droppedItemsStr += $"{item.name ?? item.info.displayName.english}\n";
                        continue;
                    }                    
                }
                var displayName = item.name ?? null;
                StorageInfo itemData;

                storageData._storage.Add(itemData = new StorageInfo()
                {
                    shortname = item.info.shortname,
                    skin = item.skin,
                    slot = item.position,
                    displayName = displayName,
                    amount = item.amount,
                    condition = item.condition,
                    maxCondition = item.maxCondition,
                    ammo = item.GetHeldEntity() is BaseProjectile ? (item.GetHeldEntity() as BaseProjectile).primaryMagazine.contents : item.GetHeldEntity() is FlameThrower ? (item.GetHeldEntity() as FlameThrower).ammo : 0,
                    ammotype = (item.GetHeldEntity() as BaseProjectile)?.primaryMagazine.ammoType.shortname ?? null,
                    contents = item.contents?.itemList.Select(item1 => new StorageInfo
                    {
                        shortname = item1.info.shortname,
                        amount = item1.amount,
                        condition = item1.condition
                    }).ToArray()
                });
                itemData.flags = item.flags;
                if (item.instanceData != null)
                {
                    itemData.instanceData = new StorageInfo.InstancedInfo()
                    {
                        ShouldPool = item.instanceData.ShouldPool,
                        dataInt = item.instanceData.dataInt,
                        blueprintTarget = item.instanceData.blueprintTarget,
                        blueprintAmount = item.instanceData.blueprintAmount,
                        subEntity = item.instanceData.subEntity.Value
                    };
                }

                if (item.text != null) itemData.text = item.text;

                items.Add(itemData);
            }
            if (!string.IsNullOrEmpty(droppedItemsStr))
            {
                if (usingWhiteList) PrintToChat(player, lang.GetMessage("WhitelistedItemsFound", this, player.UserIDString) + droppedItemsStr);
                else PrintToChat(player, lang.GetMessage("BlacklistedItemsFound", this, player.UserIDString) + droppedItemsStr);
            }
            containers.Remove(container);
            container.Invoke(container.KillMessage, 0.01f);

            Pool.FreeList(ref temp_items_list);
        }

        void OnLootEntityEnd(BasePlayer player, StorageContainer entity)
        {            
            if (containers.Contains(entity))
            {
                StoreContainerLoot(player, entity);
            }
        }

        #endregion

        #region Monument handling

        StorageContainer CreateCustomBox(Vector3 pos, Vector3 rot)
        {
            var entity = GameManager.server.CreateEntity(config.spawn_prefab, pos, Quaternion.Euler(rot.x, rot.y, rot.z)) as StorageContainer;
            if (entity == null) return null;
            entity.skinID = config.box_skin;
            entity.Spawn();
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());
            entity.baseProtection = NoDamageProtection;
            return entity;
        }

        StorageContainer CreateCustomBox(Vector3 pos, Quaternion rot)
        {
            var entity = GameManager.server.CreateEntity(config.spawn_prefab, pos, rot) as StorageContainer;
            if (entity == null) return null;
            entity.skinID = config.box_skin;
            entity.Spawn();
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());
            entity.baseProtection = NoDamageProtection;
            return entity;
        }

        StorageContainer CreateBox(MonumentInfo monument, Vector3 pos_settings, Vector3 rot_settings, ulong ownerID = 0)
        {
            Vector3 pos = monument.transform.localToWorldMatrix.MultiplyPoint3x4(pos_settings);
            Quaternion rot = monument.transform.localToWorldMatrix.rotation * Quaternion.Euler(rot_settings);
            BaseEntity box = GameManager.server.CreateEntity(config.spawn_prefab, pos, rot);
            if (box == null)
            {
                Puts("Asset path is invalid. Please update the config with the correct path.");
                return null;
            }
            box.skinID = config.box_skin;
            box.Spawn();
            var container = box as StorageContainer;
            container.baseProtection = NoDamageProtection;
            UnityEngine.Object.DestroyImmediate(container.GetComponent<GroundWatch>());
            UnityEngine.Object.DestroyImmediate(container.GetComponent<DestroyOnGroundMissing>());
            container.OwnerID = ownerID;
            return container;
        }
        Dictionary<ulong, StorageContainer> boxes = new Dictionary<ulong, StorageContainer>();

        #endregion

        #region Hooks

        void OnNewSave(string filename)
        {
            if (config.auto_wipe)
            {
                pcdData.storage.Clear();
                pcdData.monuments.Clear();

            }            
        }

        private ProtectionProperties NoDamageProtection;

        void OnServerInitialized(bool initial)
        {
            if (config.black_list.Count == 0) Unsubscribe("CanMoveItem");
            var delay = 0.0f;

            NoDamageProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
            NoDamageProtection.name = "InstancedContainerProtection";
            NoDamageProtection.Add(1);

            foreach (var config_monument in config.monuments)
            {
                var found = false;
                var key = 0ul;
                if (pcdData.monuments.Count > 0)
                {
                    foreach (KeyValuePair<ulong, monumentInfo> kvp in pcdData.monuments)
                    {
                        if (config_monument.name == kvp.Value.monument)
                        {
                            found = true;
                            key = kvp.Key;
                            break;
                        }
                    }
                }                
                if (found)
                {
                    BaseNetworkable box = BaseNetworkable.serverEntities.Find(new NetworkableId(key));

                    if (box == null)
                    {
                        if (!config_monument.enabled)
                        {
                            pcdData.monuments.Remove(key);
                            continue;
                        }
                        delay += 0.1f;
                        pcdData.monuments.Remove(key);

                        MonumentInfo Monument = TerrainMeta.Path.Monuments.Where(x => x.name == config_monument.name).FirstOrDefault();
                        if (Monument == null)
                        {
                            Puts($"Could not find {config_monument.name}");
                            continue;
                        }

                        timer.Once(delay, () =>
                        {
                            var newBox = CreateBox(Monument, config_monument.pos, config_monument.rot);
                            pcdData.monuments.Add(newBox.net.ID.Value, new monumentInfo(Monument.name, true, config_monument.pos, config_monument.rot));
                            boxes.Add(newBox.net.ID.Value, newBox);
                        });                        
                    }
                    else
                    {
                        if (!config_monument.enabled)
                        {
                            box.KillMessage();
                            pcdData.monuments.Remove(key);
                            continue;
                        }
                        var container = box as StorageContainer;
                        if (container.skinID != config.box_skin)
                        {
                            container.skinID = config.box_skin;
                            container.SendNetworkUpdateImmediate();
                        }
                        boxes.Add(container.net.ID.Value, container);
                    }
                }
                else
                {
                    if (!config_monument.enabled) continue;
                    MonumentInfo Monument = TerrainMeta.Path.Monuments.Where(x => x.name == config_monument.name).FirstOrDefault();
                    if (Monument == null)
                    {
                        Puts($"Could not find {config_monument.name}");
                        continue;
                    }

                    delay += 0.1f;

                    timer.Once(delay, () =>
                    {
                        var newBox = CreateBox(Monument, config_monument.pos, config_monument.rot);
                        pcdData.monuments.Add(newBox.net.ID.Value, new monumentInfo(Monument.name, true, config_monument.pos, config_monument.rot));
                       
                        boxes.Add(newBox.net.ID.Value, newBox);
                    });                    
                }
            }

            if (pcdData.monuments.Count > 0)
            {
                foreach (KeyValuePair<ulong, monumentInfo> kvp in pcdData.monuments.ToList())
                {
                    if (kvp.Value.monument == "deployed")
                    {
                        var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(kvp.Key));
                        if (entity == null) pcdData.monuments.Remove(kvp.Key);
                        else boxes.Add(kvp.Key, entity as StorageContainer);
                    }
                }
            }

            foreach (var entry in config.customPosBoxes)
            {
                if (!entry.enabled) continue;
                var newBox = CreateCustomBox(entry.pos, entry.rot);
                if (newBox != null) boxes.Add(newBox.net.ID.Value, newBox);
            } 
            
            if (config.draw_text)
            {
                timer.Every(config.draw_update, () =>
                {
                    if (boxes.Count == 0 || BasePlayer.activePlayerList.Count == 0) return;
                    foreach (var box in boxes)
                    {
                        if (box.Value == null) continue;
                        if (!config.draw_text_non_monument && box.Value.OwnerID > 0) continue;
                        var pos = box.Value.transform.position;
                        pos.y += 1f;
                        foreach (var player in BasePlayer.activePlayerList)
                        {
                            if (Vector3.Distance(player.transform.position, box.Value.transform.position) < config.draw_distance)
                            {
                                if (player.Connection.authLevel == 0)
                                {
                                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                                    player.SendNetworkUpdateImmediate();
                                }
                                
                                player.SendConsoleCommand("ddraw.text", config.draw_update, GetColor(config.text_col), pos, lang.GetMessage("FloatingText", this, player.UserIDString));

                                if (player.Connection.authLevel == 0)
                                {
                                    player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                                    player.SendNetworkUpdateImmediate();
                                }
                            }
                        }
                    }
                });
            }
            foreach (var entry in config.RustEdit_Boxes)
            {
                FindAnchorBoxes(entry);
            }

            if (config.filterSettings.filters.Count == 0)
            {
                config.filterSettings.filters.Add("Transporters");
                SaveConfig();
                
            }
        }

        Color GetColor(string color)
        {
            switch (color)
            {
                case "white": return Color.white;
                case "black": return Color.black;
                case "red": return Color.red;
                case "blue":return Color.blue;
                case "green":return Color.green;
                case "cyan":return Color.cyan;
                case "grey":return Color.grey;
                case "magenta":return Color.magenta;
                case "yellow": return Color.yellow;
            }
            return Color.cyan;
        }

        object OnEntityTakeDamage(StorageContainer entity, HitInfo info)
        {
            if (entity == null || info == null) return null;
            if (boxes.ContainsKey(entity.net.ID.Value))
            {
                monumentInfo mi;
                if (!pcdData.monuments.TryGetValue(entity.net.ID.Value, out mi)) return null;
                if (config.deployed_chests_invulnerable || (!string.IsNullOrEmpty(mi.monument) && mi.monument != "deployed")) info?.damageTypes?.ScaleAll(0f);
            }               
            return null;
        }

        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container != null && boxes.ContainsKey(container.net.ID.Value))
            {
                OpenStorage(player);
                return true;
            }
            return null;
        }

        void OnEntityKill(StorageContainer entity)
        {
            if (entity == null) return;            
            if (boxes.ContainsKey(entity.net.ID.Value))
            {                
                monumentInfo mi;
                if (pcdData.monuments.TryGetValue(entity.net.ID.Value, out mi))
                {
                    if (mi.monument == "deployed") pcdData.monuments.Remove(entity.net.ID.Value);
                    else
                    {
                        MonumentInfo Monument = TerrainMeta.Path.Monuments.Where(x => x.name == mi.monument).FirstOrDefault();
                        var configData = config.monuments.Where(x => x.name == mi.monument).FirstOrDefault();
                        if (Monument != null && configData != null)
                        {
                            pcdData.monuments.Remove(entity.net.ID.Value);
                            var box = CreateBox(Monument, configData.pos, configData.rot);
                            boxes.Add(box.net.ID.Value, box);
                            pcdData.monuments.Add(box.net.ID.Value, new monumentInfo(Monument.name, true, configData.pos, configData.rot));
                        }                        
                    }
                }
                else
                {
                    var box = CreateCustomBox(entity.transform.position, entity.transform.rotation);
                    boxes.Add(box.net.ID.Value, box);
                }
                boxes.Remove(entity.net.ID.Value);
            }
        }

        object CanPickupEntity(BasePlayer player, StorageContainer entity)
        {
            if (entity != null && boxes.ContainsKey(entity.net.ID.Value))
            {
                if (entity.OwnerID == player.userID)
                {
                    pcdData.monuments.Remove(entity.net.ID.Value);
                    boxes.Remove(entity.net.ID.Value);
                    return null;
                }
                return false;
            }
            return null;
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            var entity = go?.ToBaseEntity();
            if (entity == null || entity.skinID != config.box_skin) return;
            var player = plan?.GetOwnerPlayer();
            if (player == null) return;
            entity.OwnerID = player.userID;
            pcdData.monuments.Add(entity.net.ID.Value, new monumentInfo("deployed", true, entity.transform.position, new Vector3()));
            boxes.Add(entity.net.ID.Value, entity as StorageContainer);
        }

        object CanDeployItem(BasePlayer player, Deployer deployer, ulong entityId)
        {
            if (boxes.ContainsKey(entityId))
            {
                PrintToChat(player, lang.GetMessage("NoLock", this, player.UserIDString));
                return true;
            }
            return null;
        }

        object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot, int amount)
        {
            if (config.white_list.Count > 0)
            {
                foreach (var container in containers)
                {
                    if (container.inventory.uid == targetContainer && !config.white_list.Contains(item.info.shortname))
                    {
                        var player = item.GetOwnerPlayer();
                        PrintToChat(player, lang.GetMessage("Whitelist", this, player.UserIDString));
                        return true;
                    }
                }
                    
            }
            else if (config.black_list.Count > 0 && config.black_list.Contains(item.info.shortname))
            {
                foreach (var container in containers)
                {
                    if (container.inventory.uid == targetContainer)
                    {
                        var player = item.GetOwnerPlayer();
                        PrintToChat(player, lang.GetMessage("BlackList", this, player.UserIDString));
                        return true;
                    }
                }
            }            
            return null;
        }

        #endregion

        #region Helpers

        void GiveBoxItem(BasePlayer player, int quantity = 1)
        {
            var item = ItemManager.CreateByName(config.item_shortname, quantity, config.box_skin);
            item.name = "global storage box";
            player.GiveItem(item);
        }

        private BasePlayer FindPlayerByName(string Playername, BasePlayer SearchingPlayer = null)
        {
            var lowered = Playername.ToLower();
            var targetList = BasePlayer.allPlayerList.Where(x => x.displayName.ToLower().Contains(lowered)).OrderBy(x => x.displayName.Length);
            if (targetList.Count() == 1)
            {
                return targetList.First();
            }
            if (targetList.Count() > 1)
            {
                if (targetList.First().displayName.Equals(Playername, StringComparison.OrdinalIgnoreCase))
                {
                    return targetList.First();
                }
                if (SearchingPlayer != null)
                {
                    PrintToChat(SearchingPlayer, string.Format(lang.GetMessage("MorePlayersFound", this, SearchingPlayer.UserIDString), String.Join(",", targetList.Select(x => x.displayName))));
                }
                else Puts(string.Format(lang.GetMessage("MorePlayersFound", this), String.Join(",", targetList.Select(x => x.displayName))));
                return null;
            }
            if (targetList.Count() == 0)
            {
                if (SearchingPlayer != null)
                {
                    PrintToChat(SearchingPlayer, string.Format(lang.GetMessage("NoMatch", this, SearchingPlayer.UserIDString), Playername));
                }
                else Puts(string.Format(lang.GetMessage("NoMatch", this), Playername));
                return null;
            }
            return null;
        }

        private const int LAYER_TARGET = ~(1 << 2 | 1 << 3 | 1 << 4 | 1 << 10 | 1 << 18 | 1 << 28 | 1 << 29);
        private BaseEntity GetTargetEntity(BasePlayer player)
        {
            RaycastHit raycastHit;
            bool flag = Physics.Raycast(player.eyes.HeadRay(), out raycastHit, 5, LAYER_TARGET);
            var targetEntity = flag ? raycastHit.GetEntity() : null;
            return targetEntity;
        }

        #endregion

        #region Chat commands

        [ConsoleCommand("gspurge")]
        void PurgeCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, "globalstorage.admin")) return;

            if (arg.Args == null || arg.Args.Length == 0 || (!arg.Args[0].Equals("true", StringComparison.OrdinalIgnoreCase) && !arg.Args[0].Equals("false", StringComparison.OrdinalIgnoreCase)))
            {
                arg.ReplyWith(string.Format(lang.GetMessage("PurgeEnabled", this, player != null ? player.UserIDString : null), pcdData.purgeActive));
                return;
            }
            if (arg.Args[0].Equals("true", StringComparison.OrdinalIgnoreCase))
            {
                pcdData.purgeActive = true;
                PrintToChat(lang.GetMessage("PurgeEnabledAnnouncement", this));
            }
            else
            {
                pcdData.purgeActive = false;
                PrintToChat(lang.GetMessage("PurgeDisabledAnnouncement", this));
            }
            SaveData();
        }

        [ChatCommand("addcustomlocation")]
        void AddCustomLocation(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, perms_admin)) return;
            Puts($"Rot: {player.transform.root}");
            var box = CreateCustomBox(player.transform.position, new Vector3(Vector3.zero.x, player.viewAngles.y, player.viewAngles.z));
            if (box != null)
            {
                config.customPosBoxes.Add(new CustomPosInfo(true, player.transform.position, player.viewAngles));
                SaveConfig();
                boxes.Add(box.net.ID.Value, box);
            }
        }

        [ChatCommand("removecustomlocation")]
        void RemoveCustomLocation(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, perms_admin)) return;
            var entity = GetTargetEntity(player);
            if (entity == null || !(entity is StorageContainer) || entity.PrefabName != config.spawn_prefab) return;
            var container = entity as StorageContainer;
            foreach (var entry in config.customPosBoxes)
            {
                if (entry.pos == container.transform.position)
                {
                    config.customPosBoxes.Remove(entry);
                    boxes.Remove(container.net.ID.Value);
                    PrintToChat(player, string.Format(lang.GetMessage("RemovedContainer", this, player.UserIDString), container.transform.position));
                    container.KillMessage();
                    SaveConfig();
                    return;
                }
            }
        }

        [ChatCommand("giveglobalbox")]
        void GiveBox(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, perms_admin)) return;
            var amount = 1;
            if (args.Length == 0)
            {
                GiveBoxItem(player, amount);
                PrintToChat(player, string.Format(lang.GetMessage("GaveBoxes", this, player.UserIDString), player.displayName, amount));
                return;
            }
            if (args.Length > 0)
            {
                var target = FindPlayerByName(args[0], player);
                if (target == null) return;                
                if (args.Length == 2 && args[1].IsNumeric()) amount = Convert.ToInt32(args[1]);
                GiveBoxItem(target, amount);
                PrintToChat(player, string.Format(lang.GetMessage("GaveBoxes", this, player.UserIDString), target.displayName, amount));
                PrintToChat(player, string.Format(lang.GetMessage("ReceiveBoxes", this, target.UserIDString), amount));
            }
        }

        [ConsoleCommand("giveglobalbox")]
        void GiveBoxConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !permission.UserHasPermission(player.UserIDString, perms_admin)) return;
            if (arg.Args.Length == 0)
            {
                if (player != null) arg.ReplyWith(lang.GetMessage("ValidUsage", this, player.UserIDString));
                else arg.ReplyWith(lang.GetMessage("ValidUsage", this));
                return;
            }
            var target = FindPlayerByName(arg.Args[0], player ?? null);
            if (target == null) return;
            var amount = 1;
            if (arg.Args.Length == 2 && arg.Args[1].IsNumeric()) amount = Convert.ToInt32(arg.Args[1]);
            arg.ReplyWith(string.Format(lang.GetMessage("ConsoleGave", this), target.displayName, amount));
            GiveBoxItem(target, amount);
            PrintToChat(target, string.Format(lang.GetMessage("ReceiveBoxes", this, target.UserIDString), amount));
        }

        [ChatCommand("gstorage")]
        void GlobalStorageCMD(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, perms_chat)) return;
            OpenStorage(player);
        }

        [ChatCommand("addglobalstorage")]
        void AddGlobalStorageCMD(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, perms_admin)) return;
            var entity = GetTargetEntity(player);
            if (entity == null || !(entity is StorageContainer) || entity.PrefabName != config.spawn_prefab) return;
            var container = entity as StorageContainer;
            if (boxes.ContainsKey(container.net.ID.Value))
            {
                PrintToChat(player, lang.GetMessage("AlreadySetup", this, player.UserIDString));
                return;
            }
            var ownerID = 0ul;
            if (args.Length > 0)
            {
                var target = FindPlayerByName(args[0], player);
                if (target == null) return;
                ownerID = target.userID;
            }
            container.OwnerID = ownerID;
            container.skinID = config.box_skin;
            container.SendNetworkUpdateImmediate();
            if (!pcdData.monuments.ContainsKey(container.net.ID.Value)) pcdData.monuments.Add(container.net.ID.Value, new monumentInfo("deployed", true, container.transform.position, new Vector3()));
            boxes.Add(container.net.ID.Value, container);
            PrintToChat(player, string.Format(lang.GetMessage("SetupBox", this, player.UserIDString), container.net.ID.Value, ownerID));
        }

        #endregion

        #region Custom RustEdit chests

        List<StorageContainer> map_boxes = new List<StorageContainer>();

        bool FindAnchorBoxes(AnchorInfo anchorInfo) 
        {
            if (anchorInfo == null || string.IsNullOrEmpty(anchorInfo.primary_entity) || string.IsNullOrEmpty(anchorInfo.secondary_entity)) return false;
            if (!anchorInfo.enabled) return false;
            // Search through data to see if our chests has already been found this wipe.
            foreach (var anchorSet in pcdData.RustEdit_storage_data)
            {
                
                if (anchorInfo.primary_entity == anchorSet.Value.primary_entity && anchorInfo.secondary_entity == anchorSet.Value.secondary_entity)
                {
                    var chest = BaseNetworkable.serverEntities.Find(new NetworkableId(anchorSet.Key)) as StorageContainer;
                    if (chest != null)
                    {   
                        if (chest.skinID != config.box_skin)
                        {
                            chest.skinID = config.box_skin;
                            chest.SendNetworkUpdateImmediate();
                        }
                        boxes.Add(anchorSet.Key, chest);
                        map_boxes.Add(chest);
                        return true;
                    }                        
                }
            }

            // We obtain the first entity and use it as our main anchor.
            List<BaseNetworkable> PrimaryEntities = Pool.GetList<BaseNetworkable>();
            PrimaryEntities.AddRange(BaseNetworkable.serverEntities.Where(x => x.ShortPrefabName.Equals(anchorInfo.primary_entity, StringComparison.OrdinalIgnoreCase)));

            // We search the second entities and store them.
            List<BaseNetworkable> SecondaryEntities = Pool.GetList<BaseNetworkable>();
            SecondaryEntities.AddRange(BaseNetworkable.serverEntities.Where(x => x.ShortPrefabName.Equals(anchorInfo.secondary_entity, StringComparison.OrdinalIgnoreCase)));

            var chests = BaseNetworkable.serverEntities.Where(x => x.PrefabName.Equals(config.spawn_prefab, StringComparison.OrdinalIgnoreCase));

            // We use the first entity to check the distance between all entities, and if we find all 3 int he same place we move forward.
            foreach (var primary in PrimaryEntities)
            {
                foreach (var secondary in SecondaryEntities)
                {
                    if (primary == secondary) continue;
                    if (Vector3.Distance(primary.transform.position, secondary.transform.position) < 0.5)
                    {
                        // We found our 2 entities. Now we see if the chest is close by.
                        var chest = chests.Where(x => Vector3.Distance(x.transform.position, primary.transform.position) <= anchorInfo.distance).FirstOrDefault() as StorageContainer;
                        if (chest != null)
                        {
                            if (!pcdData.RustEdit_storage_data.ContainsKey(chest.net.ID.Value)) pcdData.RustEdit_storage_data.Add(chest.net.ID.Value, new AnchorInfo());
                            pcdData.RustEdit_storage_data[chest.net.ID.Value] = anchorInfo;
                            if (chest.skinID != config.box_skin)
                            {
                                chest.skinID = config.box_skin;
                                chest.SendNetworkUpdateImmediate();
                            }
                            boxes.Add(chest.net.ID.Value, chest);
                            map_boxes.Add(chest);
                            SaveData();
                            return true;
                        }
                    }
                }
            }
            Puts($"Failed to find anchor points - Primary: {anchorInfo.primary_entity}. Secondary: {anchorInfo.secondary_entity}. Dist: {anchorInfo.distance}");
            return false;
        }

        #endregion
    }
}
