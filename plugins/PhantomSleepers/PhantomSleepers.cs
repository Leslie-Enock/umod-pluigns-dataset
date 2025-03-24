using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Phantom Sleepers", "nivex", "1.0.0")]
    [Description("Create phantom sleepers to use as bait against suspected cheaters.")]
    class PhantomSleepers : RustPlugin
    {
        private const string playerPrefab = "assets/prefabs/player/player.prefab";
        private const string permissionName = "phantomsleepers.use";
        private const ulong phantomId = 612306;

        private Dictionary<ulong, Phantom> phantoms = new();

        private class Phantom : BasePlayer
        {
            internal BasePlayer phantom;
            internal BasePlayer sleeper;
            internal PhantomSleepers _instance;
            internal List<Item> items = new();
            internal Vector3 spawnPos;
            internal bool Invisibility;
            internal bool NoLooting;
            internal bool NoCorpse;
            internal bool NoCorpseLoot;
            internal ulong networkId;
            internal float nextUpdateCheck;

            //FieldInfo _inventoryValue = typeof(BasePlayer).GetField("inventoryValue", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            private void Awake()
            {
                phantom = GetComponent<BasePlayer>();

                //HiddenValue<PlayerInventory> inventoryValue = _inventoryValue.GetValue(phantom) as HiddenValue<PlayerInventory>;
                //inventoryValue.Set(GetComponent<PlayerInventory>());
                spawnPos = phantom.transform.position;
            }

            private void OnDestroy()
            {
                if (phantom != null && !phantom.IsDestroyed)
                {
                    phantom.Kill();
                }

                _instance.phantoms.Remove(networkId);

                SendShouldNetworkToUpdateImmediate();
            }

            private void FixedUpdate()
            {
                if (Invisibility && Time.time > nextUpdateCheck)
                {
                    if (sleeper == null || sleeper.IsConnected)
                    {
                        SendShouldNetworkToUpdateImmediate();
                        Invisibility = false;
                    }
                    nextUpdateCheck = Time.time + 1f;
                }
            }

            public void EntityDestroyOnClient(BasePlayer target)
            {
                if (target.IsValid() && target.net.group != null)
                {
                    target.OnNetworkSubscribersLeave(target.net.group.subscribers);
                }
                target.limitNetworking = true;
                target.syncPosition = false;
                sleeper = target;
                Invisibility = true;
            }

            public void SendShouldNetworkToUpdateImmediate()
            {
                if (sleeper)
                {
                    sleeper.limitNetworking = false;
                    sleeper.syncPosition = true;
                    sleeper.SendNetworkUpdateImmediate();
                }
            }

            public override bool ShouldDropActiveItem() => false;

            public override bool CanBeLooted(BasePlayer player)
            {
                if (NoLooting)
                {
                    return player.IsAdmin;
                }
                return base.CanBeLooted(player);
            }

            public override void OnDied(HitInfo info)
            {
                if (NoCorpseLoot)
                {
                    foreach (Item item in items)
                    {
                        if (item == null) continue;
                        item.RemoveFromContainer();
                        item.Remove(0f);
                    }
                    ItemManager.DoRemoves();
                }

                items.Clear();

                base.OnDied(info);
            }

            public override BaseCorpse CreateCorpse(PlayerFlags flagsOnDeath, Vector3 posOnDeath, Quaternion rotOnDeath, List<TriggerBase> triggersOnDeath, bool forceServerSide = false)
            {
                if (NoCorpse)
                {
                    return null;
                }
                return base.CreateCorpse(flagsOnDeath, posOnDeath, rotOnDeath, triggersOnDeath, forceServerSide);
            }
        }

        private void ccmdCreatePhantom(ConsoleSystem.Arg arg)
        {
            void CopySerializableFields<T>(T src, T dst)
            {
                foreach (FieldInfo field in typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public))
                {
                    field.SetValue(dst, field.GetValue(src));
                }
            }

            BasePlayer player = arg.Player();

            if (player == null)
            {
                arg.ReplyWith("Type it in game");
                return;
            }

            if (!arg.IsAdmin && !permission.UserHasPermission(player.userID.ToString(), permissionName))
            {
                player.ChatMessage(LangAPI("NoPermission", player.UserIDString));
                return;
            }

            if (!Physics.Raycast(player.eyes.HeadRay(), out var hit, config.Settings.MaxRaycastDistance))
            {
                player.ChatMessage(LangAPI("LookElsewhere", player.UserIDString));
                return;
            }

            GameObject prefab = GameManager.server.FindPrefab(playerPrefab);
            GameObject go = Facepunch.Instantiate.GameObject(prefab, hit.point, player.transform.rotation);

            go.SetActive(false);

            go.name = playerPrefab;

            BasePlayer target = go.GetComponent<BasePlayer>();
            Phantom phantom = go.AddComponent<Phantom>();

            CopySerializableFields(target, phantom);

            UnityEngine.Object.DestroyImmediate(target, true);

            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, Rust.Server.EntityScene);

            go.SetActive(true);

            phantom._instance = this;
            phantom.userID = phantomId;
            phantom.UserIDString = phantomId.ToString(); 
            phantom.NoCorpseLoot = config.Settings.StripPhantomsOnDeath;
            phantom.NoCorpse = config.Settings.DestroyPhantomCorpses;
            phantom.NoLooting = config.Settings.PreventPhantomLooting;

            phantom.Spawn();

            phantom.networkId = phantom.net.ID.Value;

            phantoms[phantom.net.ID.Value] = phantom;

            if (config.Settings.RandomHealth)
            {
                phantom.health = (float)Math.Round(UnityEngine.Random.Range(35f, 100f), 2);
            }
            else phantom.health = 100f;
            
            if (config.Settings.PhantomsSpawnSleeping)
            {
                phantom.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
            }

            if (config.Settings.RandomNames.Count == 0)
            {
                List<BasePlayer> sleepers = new(BasePlayer.sleepingPlayerList);

                if (sleepers.Count > 0)
                {
                    float sqrDistance = Mathf.Pow(config.Settings.MinDistanceFromRealSleeper, 2f);

                    BasePlayer sleeper;

                    do
                    {
                        sleeper = sleepers.GetRandom();
                        sleepers.Remove(sleeper);
                    } while ((sleeper.transform.position - phantom.transform.position).sqrMagnitude < sqrDistance && sleepers.Count > 0);

                    if (config.Invisibility.HideRealSleepers)
                    {
                        phantom.EntityDestroyOnClient(sleeper);
                    }

                    phantom.displayName = sleeper.displayName;
                }
                else phantom.displayName = config.Settings.DefaultNameIfNoSleepers;
            }
            else phantom.displayName = config.Settings.RandomNames.GetRandom();

            Equip(phantom);

            phantom.SendNetworkUpdateImmediate(true);
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permissionName, this);
            cmd.AddConsoleCommand(config.Settings.ConsoleCommand, this, nameof(ccmdCreatePhantom));
        }

        private void Unload()
        {
            List<Phantom> objects = new(phantoms.Values);
            foreach (var obj in objects)
            {
                UnityEngine.Object.Destroy(obj);
            }
            phantoms.Clear();
        }

        private void Equip(Phantom phantom)
        {
            EquipItem(phantom, config.Gear.Gloves);
            EquipItem(phantom, config.Gear.Boots);
            EquipItem(phantom, config.Gear.Helms);
            EquipItem(phantom, config.Gear.Vests);
            EquipItem(phantom, config.Gear.Shirts);
            EquipItem(phantom, config.Gear.Pants);
            EquipItem(phantom, config.Gear.Weapons);
        }

        private void EquipItem(Phantom phantom, List<string> itemNames)
        {
            if (itemNames.Count == 0)
            {
                return;
            }
            Item item = ItemManager.CreateByName(itemNames.GetRandom());
            if (item == null)
            {
                return;
            }
            if (!item.info.skins2.IsNullOrEmpty())
            {
                item.skin = item.info.skins2.GetRandom().WorkshopId;
                item.MarkDirty();
            }
            ItemContainer container = phantom.inventory.containerWear;
            if (item.GetHeldEntity() is HeldEntity heldEntity)
            {
                container = phantom.inventory.containerBelt;
                heldEntity.skinID = item.skin;
                heldEntity.SendNetworkUpdateImmediate();
            }
            if (item.MoveToContainer(container, -1, false))
            {
                phantom.items.Add(item);
            }
            else
            {
                item.Remove();
            }
        }

        #region Config

        private Configuration config;

        private class Configuration
        {
            [JsonProperty("Invisibility")]
            public InvisibilityConfig Invisibility { get; set; } = new();

            [JsonProperty("Settings")]
            public SettingsConfig Settings { get; set; } = new();

            [JsonProperty("Gear")]
            public GearConfig Gear { get; set; } = new();
        }

        private class InvisibilityConfig
        {
            [JsonProperty("Hide Real Sleepers")]
            public bool HideRealSleepers { get; set; }
        }

        private class SettingsConfig
        {
            [JsonProperty("Random Names", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> RandomNames { get; set; } = new();

            [JsonProperty("Strip Phantoms On Death")]
            public bool StripPhantomsOnDeath { get; set; } = true;

            [JsonProperty("Destroy Phantom Corpses")]
            public bool DestroyPhantomCorpses { get; set; } = true;

            [JsonProperty("Prevent Phantom Looting")]
            public bool PreventPhantomLooting { get; set; } = true;

            [JsonProperty("Console Command")]
            public string ConsoleCommand { get; set; } = "createphantom";

            [JsonProperty("Max Raycast Distance")]
            public float MaxRaycastDistance { get; set; } = 100f;

            [JsonProperty("Min Distance From Real Sleeper")]
            public float MinDistanceFromRealSleeper { get; set; } = 450f;

            [JsonProperty("Default Name If No Sleepers")]
            public string DefaultNameIfNoSleepers { get; set; } = "luke";

            [JsonProperty("Phantoms Spawn Sleeping")]
            public bool PhantomsSpawnSleeping { get; set; } = true;

            [JsonProperty("Random Starting Health")]
            public bool RandomHealth { get; set; }
        }

        private class GearConfig
        {
            [JsonProperty("Shirts", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Shirts { get; set; } = new() { "tshirt", "tshirt.long", "shirt.tanktop", "shirt.collared" };

            [JsonProperty("Pants", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Pants { get; set; } = new() { "pants" };

            [JsonProperty("Helms", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Helms { get; set; } = new() { "metal.facemask" };

            [JsonProperty("Vests", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Vests { get; set; } = new() { "metal.plate.torso" };

            [JsonProperty("Gloves", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Gloves { get; set; } = new() { "burlap.gloves" };

            [JsonProperty("Boots", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Boots { get; set; } = new() { "shoes.boots" };

            [JsonProperty("Weapons", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Weapons { get; set; } = new() { "pistol.semiauto" };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            canSaveConfig = false;
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                canSaveConfig = true;
                SaveConfig();
            }
            catch (Exception ex)
            {
                Puts(ex.ToString());
                LoadDefaultConfig();
            }
        }

        private bool canSaveConfig = true;

        protected override void SaveConfig()
        {
            if (canSaveConfig)
            {
                Config.WriteObject(config);
            }
        }

        protected override void LoadDefaultConfig() => config = new();

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new()
            {
                ["LookElsewhere"] = "Unable to find a position. Try looking at the ground or another object.",
                ["NoPermission"] = "You do not have permission to use this command.",
            }, this);
        }

        private string LangAPI(string key, string id = null, params object[] args) => args.Length > 0 ? string.Format(lang.GetMessage(key, this, id), args) : lang.GetMessage(key, this, id);

        #endregion Config
    }
}
