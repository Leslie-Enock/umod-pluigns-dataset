/*
 * Copyright (c) 2023 Bazz3l
 * 
 * Guarded Crate cannot be copied, edited and/or (re)distributed without the express permission of Bazz3l.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 * 
 */

using System.Collections.Generic;
using System.Globalization;
using System.Collections;
using System.Text;
using System.Linq;
using System;
using Oxide.Core.Plugins;
using Oxide.Core;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using GuardedCrateEx;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    [Info("Guarded Crate", "Bazz3l", "2.0.5")]
    [Description("Spawn high value loot guarded by scientists at random or specified locations.")]
    internal class GuardedCrate : RustPlugin
    {
        [PluginReference] private Plugin NpcSpawn, Clans, ZoneManager, HackableLock, GUIAnnouncements;
        
        #region Fields
        
        private const string PERM_USE = "guardedcrate.use";
        
        private const TerrainTopology.Enum BLOCKED_TOPOLOGY 
            = TerrainTopology.Enum.Cliffside | TerrainTopology.Enum.Cliff | TerrainTopology.Enum.Lake | 
              TerrainTopology.Enum.Ocean | TerrainTopology.Enum.Monument | TerrainTopology.Enum.Building | 
              TerrainTopology.Enum.Offshore | TerrainTopology.Enum.River | TerrainTopology.Enum.Swamp;
        
        private const string MARKER_PREFAB = "assets/prefabs/tools/map/genericradiusmarker.prefab";
        private const string CRATE_PREFAB = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";
        private const string PLANE_PREFAB = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
        
        private ConfigData _configData;
        private StoredData _storedData;
        
        private static GuardedCrate _instance;

        #endregion

        #region Config
        
        protected override void LoadDefaultConfig()
        {
            _configData = ConfigData.DefaultConfig();
            PrintWarning("Loaded default config.");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null) throw new JsonException();
                
                bool hasChanged = false;
                
                if (_configData.CommandName == null)
                {
                    PrintWarning("Updated config.");
                    LoadDefaultConfig();
                    
                    hasChanged = true;
                }
                
                if (_configData.MessageSettings == null)
                {
                    _configData.MessageSettings = new MessageSettings
                    {
                        EnableToast = false,
                        EnableChat = true,
                        EnableChatPrefix = true,
                        ChatIcon = 76561199542824781,
                        EnableGuiAnnouncements = false,
                        GuiAnnouncementsBgColor = "Purple",
                        GuiAnnouncementsTextColor = "White"
                    };
                    
                    hasChanged = true;
                }

                if (_configData.ZoneManagerSettings == null)
                {
                    _configData.ZoneManagerSettings = new ZoneMangerSettings
                    {
                        EnabledIgnoredZones = false,
                        IgnoredZones = new List<string>()
                    };
                    
                    hasChanged = true;
                }
                
                if (hasChanged)
                    SaveConfig();
            }
            catch(Exception e)
            {
                PrintWarning(e.Message);
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_configData, true);
        
        private class ConfigData
        {
            [JsonProperty("command name")]
            public string CommandName;
            [JsonProperty("enable auto spawning events")]
            public bool EnableAutoStart;
            [JsonProperty("auto start event after x duration")]
            public float AutoStartDuration;
            [JsonProperty("message notification settings")]
            public MessageSettings MessageSettings;
            [JsonProperty("zone manager settings")]
            public ZoneMangerSettings ZoneManagerSettings;

            public static ConfigData DefaultConfig()
            {
                return new ConfigData
                {
                    CommandName = "gcrate",
                    EnableAutoStart = true,
                    AutoStartDuration = 3600,
                    MessageSettings = new MessageSettings
                    {
                        EnableToast = false,
                        EnableChat = true, 
                        EnableChatPrefix = true,
                        ChatIcon = 76561199542824781,
                        EnableGuiAnnouncements = false,
                        GuiAnnouncementsBgColor = "Purple",
                        GuiAnnouncementsTextColor = "White"
                    },
                    ZoneManagerSettings = new ZoneMangerSettings
                    {
                        EnabledIgnoredZones = false,
                        IgnoredZones = new List<string>()
                    }
                };
            }
        }

        private class MessageSettings
        {
            [JsonProperty("enable toast message")]
            public bool EnableToast;
            [JsonProperty("enable chat message")]
            public bool EnableChat;
            [JsonProperty("enable chat prefix")]
            public bool EnableChatPrefix;
            [JsonProperty("custom chat message icon (steam64)")]
            public ulong ChatIcon;
            [JsonProperty("enable gui announcements plugin from umod.org")]
            public bool EnableGuiAnnouncements;
            [JsonProperty("gui announcements text color")]
            public string GuiAnnouncementsTextColor;
            [JsonProperty("gui announcements background color")]
            public string GuiAnnouncementsBgColor;
        }
        
        private class ZoneMangerSettings
        {
            [JsonProperty("enable ignored zones")]
            public bool EnabledIgnoredZones;
            [JsonProperty("ignore these zone ids, leave empty to exclude all zones")]
            public List<string> IgnoredZones;
        }

        #endregion

        #region Storage

        private void LoadDefaultData()
        {
            _storedData = new StoredData();
            _storedData.CrateEventEntries = new List<EventEntry>
            {
                new()
                {
                    EventName = "Easy",
                    EventDuration = 1800.0f,

                    EnableAutoHack = true,
                    HackSeconds = 60f,

                    EnableLockToPlayer = true,
                    EnableClanTag = true,

                    EnableMarker = true,
                    MapMarkerColor1 = "#32A844",
                    MapMarkerColor2 = "#000000",
                    MapMarkerOpacity = 0.6f,
                    MapMarkerRadius = 0.7f,

                    EnableLootTable = false,
                    LootMinAmount = 6,
                    LootMaxAmount = 10,
                    LootTable = new List<ItemEntry>(),

                    EnableEliminateGuards = true,
                    GuardAmount = 8,
                    GuardConfig = new GuardConfig
                    {
                        Name = "Easy Crate",
                        WearItems = new List<GuardConfig.WearEntry>
                        {
                            new()
                            {
                                ShortName = "hazmatsuit_scientist_peacekeeper",
                                SkinID = 0UL
                            }
                        },
                        BeltItems = new List<GuardConfig.BeltEntry>
                        {
                            new()
                            {
                                ShortName = "smg.mp5",
                                Amount = 1,
                                SkinID = 0UL,
                                Mods = new List<string>()
                            },
                            new()
                            {
                                ShortName = "syringe.medical",
                                Amount = 10,
                                SkinID = 0UL,
                                Mods = new List<string>()
                            },
                        },
                        Kit = "",
                        Health = 200.0f,
                        RoamRange = 5.0f,
                        ChaseRange = 40.0f,
                        SenseRange = 150.0f,
                        AttackRangeMultiplier = 8.0f,
                        CheckVisionCone = false,
                        VisionCone = 180.0f,
                        DamageScale = 1.0f,
                        TurretDamageScale = 0.25f,
                        AimConeScale = 0.25f,
                        DisableRadio = false,
                        CanRunAwayWater = true,
                        CanSleep = false,
                        SleepDistance = 100f,
                        Speed = 8.5f,
                        AboveOrUnderGround = false,
                        Stationary = false,
                        MemoryDuration = 30.0f
                    }
                },
                new()
                {
                    EventName = "Medium",
                    EventDuration = 1800.0f,

                    EnableAutoHack = true,
                    HackSeconds = 120.0f,

                    EnableLockToPlayer = true,
                    EnableClanTag = true,

                    EnableMarker = true,
                    MapMarkerColor1 = "#EDDF45",
                    MapMarkerColor2 = "#000000",
                    MapMarkerOpacity = 0.6f,
                    MapMarkerRadius = 0.7f,

                    EnableLootTable = false,
                    LootMinAmount = 6,
                    LootMaxAmount = 10,
                    LootTable = new List<ItemEntry>(),

                    EnableEliminateGuards = true,
                    GuardAmount = 10,
                    GuardConfig = new GuardConfig
                    {
                        Name = "Medium Guard",
                        WearItems = new List<GuardConfig.WearEntry>
                        {
                            new()
                            {
                                ShortName = "hazmatsuit_scientist_peacekeeper",
                                SkinID = 0UL
                            }
                        },
                        BeltItems = new List<GuardConfig.BeltEntry>
                        {
                            new()
                            {
                                ShortName = "smg.mp5",
                                Amount = 1,
                                SkinID = 0UL,
                                Mods = new List<string>()
                            },
                            new()
                            {
                                ShortName = "syringe.medical",
                                Amount = 10,
                                SkinID = 0UL,
                                Mods = new List<string>()
                            },
                        },
                        Kit = "",
                        Health = 250.0f,
                        RoamRange = 5.0f,
                        ChaseRange = 40.0f,
                        SenseRange = 150.0f,
                        AttackRangeMultiplier = 8.0f,
                        CheckVisionCone = false,
                        VisionCone = 180.0f,
                        DamageScale = 1.0f,
                        TurretDamageScale = 0.25f,
                        AimConeScale = 0.25f,
                        DisableRadio = false,
                        CanRunAwayWater = true,
                        CanSleep = false,
                        SleepDistance = 100f,
                        Speed = 8.5f,
                        AboveOrUnderGround = false,
                        Stationary = false,
                        MemoryDuration = 30.0f
                    }
                },
                new()
                {
                    EventName = "Hard",
                    EventDuration = 1800.0f,

                    EnableAutoHack = true,
                    HackSeconds = 180.0f,

                    EnableLockToPlayer = true,
                    EnableClanTag = true,

                    EnableMarker = true,
                    MapMarkerColor1 = "#3060D9",
                    MapMarkerColor2 = "#000000",
                    MapMarkerOpacity = 0.6f,
                    MapMarkerRadius = 0.7f,

                    EnableLootTable = false,
                    LootMinAmount = 6,
                    LootMaxAmount = 10,
                    LootTable = new List<ItemEntry>(),

                    EnableEliminateGuards = true,
                    GuardAmount = 12,
                    GuardConfig = new GuardConfig
                    {
                        Name = "Hard Guard",
                        WearItems = new List<GuardConfig.WearEntry>
                        {
                            new()
                            {
                                ShortName = "hazmatsuit_scientist_peacekeeper",
                                SkinID = 0UL
                            }
                        },
                        BeltItems = new List<GuardConfig.BeltEntry>
                        {
                            new()
                            {
                                ShortName = "rifle.ak",
                                Amount = 1,
                                SkinID = 0UL,
                                Mods = new List<string>()
                            },
                            new()
                            {
                                ShortName = "syringe.medical",
                                Amount = 10,
                                SkinID = 0UL,
                                Mods = new List<string>()
                            },
                        },
                        Kit = "",
                        Health = 300.0f,
                        RoamRange = 5.0f,
                        ChaseRange = 40.0f,
                        SenseRange = 150.0f,
                        AttackRangeMultiplier = 8.0f,
                        CheckVisionCone = false,
                        VisionCone = 180.0f,
                        DamageScale = 1.0f,
                        TurretDamageScale = 0.25f,
                        AimConeScale = 0.15f,
                        DisableRadio = false,
                        CanRunAwayWater = true,
                        CanSleep = false,
                        SleepDistance = 100f,
                        Speed = 8.5f,
                        AboveOrUnderGround = false,
                        Stationary = false,
                        MemoryDuration = 30.0f
                    }
                }
            };
            
            SaveData();
        }

        private void LoadData()
        {
            try
            {
                _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
                if (_storedData == null || !_storedData.IsValid) throw new Exception();
            }
            catch
            {
                PrintWarning("Loaded default data.");
                LoadDefaultData();
            }
        }

        private void SaveData()
        {
            if (_storedData == null || !_storedData.IsValid) return;
            Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);
        }

        private class StoredData
        {
            public List<EventEntry> CrateEventEntries = new();

            [JsonIgnore] 
            private string[] _eventNames;

            [JsonIgnore]
            public string[] EventNames
            {
                get
                {
                    return _eventNames ??= CrateEventEntries
                        .Select(x => x.EventName)
                        .ToArray();
                }
            }
            
            [JsonIgnore] 
            public bool IsValid => CrateEventEntries != null && CrateEventEntries.Count > 0;

            public EventEntry FindEventByName(string eventName)
            {
                return CrateEventEntries.Find(x => x.EventName.Equals(eventName, StringComparison.OrdinalIgnoreCase));
            }
        }

        private class EventEntry
        {
            [JsonProperty("event display name)")]
            public string EventName;
            [JsonProperty("event duration")]
            public float EventDuration;

            [JsonProperty("enable lock to player when completing the event")]
            public bool EnableLockToPlayer;

            [JsonProperty("enable clan tag")]
            public bool EnableClanTag;
            
            [JsonProperty("enable auto hacking of crate when an event is finished")]
            public bool EnableAutoHack;
            [JsonProperty("hackable locked crate")]
            public float HackSeconds;

            [JsonProperty("enable marker")]
            public bool EnableMarker;
            [JsonProperty("marker color 1")]
            public string MapMarkerColor1;
            [JsonProperty("marker color 2")]
            public string MapMarkerColor2;
            [JsonProperty("marker radius")]
            public float MapMarkerRadius;
            [JsonProperty("marker opacity")]
            public float MapMarkerOpacity;

            [JsonProperty("enable loot table")]
            public bool EnableLootTable;
            [JsonProperty("min loot items")]
            public int LootMinAmount;
            [JsonProperty("max loot items")]
            public int LootMaxAmount;
            [JsonProperty("enable eliminate all guards before looting")]
            public bool EnableEliminateGuards;
            
            [JsonProperty("guard spawn amount")]
            public int GuardAmount;
            [JsonProperty("guard spawn config")]
            public GuardConfig GuardConfig;

            [JsonProperty("create loot items")]
            public List<ItemEntry> LootTable;
        }

        private class ItemEntry
        {
            public string DisplayName;
            public string Shortname; 
            public ulong SkinID = 0UL;
            public int MinAmount;
            public int MaxAmount;
            
            public static List<ItemEntry> SaveItems(ItemContainer container)
            {
                List<ItemEntry> items = new List<ItemEntry>();

                foreach (Item item in container.itemList)
                {
                    items.Add(new ItemEntry
                    {
                        DisplayName = item.name,
                        Shortname = item.info.shortname,
                        SkinID = item.skin,
                        MinAmount = item.amount,
                        MaxAmount = item.amount,
                    });
                }

                return items;
            }

            public Item CreateItem()
            {
                Item item =  ItemManager.CreateByName(Shortname, UnityEngine.Random.Range(MinAmount, MaxAmount), SkinID);
                item.name = DisplayName;
                item.MarkDirty();
                return item;
            }
        }
        
        private class GuardConfig
        {
            public string Name;
            public List<WearEntry> WearItems;
            public List<BeltEntry> BeltItems;            
            public string Kit;
            public float Health;
            public float RoamRange;
            public float ChaseRange;
            public float SenseRange;
            public float AttackRangeMultiplier;
            public bool CheckVisionCone;
            public float VisionCone;
            public float DamageScale;
            public float TurretDamageScale;
            public float AimConeScale;
            public bool HostileTargetsOnly;
            public bool DisableRadio;
            public bool CanRunAwayWater;
            public bool CanSleep;
            public float SleepDistance;
            public float Speed;
            public bool AboveOrUnderGround;
            public bool Stationary;
            public float MemoryDuration;
            
            [JsonIgnore] 
            public JObject Parsed;

            public class BeltEntry
            {
                public string ShortName;
                public ulong SkinID;
                public int Amount;
                public string Ammo;
                public List<string> Mods;

                public static List<BeltEntry> SaveItems(ItemContainer container)
                {
                    List<BeltEntry> items = new List<BeltEntry>();
                    
                    foreach (Item item in container.itemList)
                    {
                        BeltEntry beltEntry = new BeltEntry
                        {
                            ShortName = item.info.shortname,
                            SkinID = item.skin,
                            Amount = item.amount,
                            Mods = new List<string>()
                        };

                        if (item.GetHeldEntity() is BaseProjectile projectile && projectile?.primaryMagazine != null && projectile.primaryMagazine.ammoType != null)
                            beltEntry.Ammo = projectile.primaryMagazine.ammoType.shortname;
                        
                        if (item?.contents?.itemList != null)
                        {
                            foreach (Item itemContent in item.contents.itemList)
                                beltEntry.Mods.Add(itemContent.info.shortname);
                        }
                        
                        items.Add(beltEntry);
                    }

                    return items;
                }
            }

            public class WearEntry
            {
                public string ShortName; 
                public ulong SkinID;

                public static List<WearEntry> SaveItems(ItemContainer container)
                {
                    List<WearEntry> items = new List<WearEntry>();

                    foreach (Item item in container.itemList)
                    {
                        items.Add(new WearEntry
                        {
                            ShortName = item.info.shortname,
                            SkinID = item.skin
                        });
                    }

                    return items;
                }
            }

            public void CacheConfig()
            {
                Parsed = new JObject
                {
                    ["Name"] = Name,
                    ["WearItems"] = new JArray { WearItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID }) },
                    ["BeltItems"] = new JArray { BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["Amount"] = x.Amount, ["SkinID"] = x.SkinID, ["Mods"] = new JArray { x.Mods }, ["Ammo"] = x.Ammo }) },
                    ["Kit"] = Kit,
                    ["Health"] = Health,
                    ["RoamRange"] = RoamRange,
                    ["ChaseRange"] = ChaseRange,
                    ["SenseRange"] = SenseRange,
                    ["ListenRange"] = SenseRange / 2f,
                    ["AttackRangeMultiplier"] = AttackRangeMultiplier,
                    ["CheckVisionCone"] = CheckVisionCone,
                    ["VisionCone"] = VisionCone,
                    ["HostileTargetsOnly"] = HostileTargetsOnly,
                    ["DamageScale"] = DamageScale,
                    ["TurretDamageScale"] = TurretDamageScale,
                    ["AimConeScale"] = AimConeScale,
                    ["DisableRadio"] = DisableRadio,
                    ["CanRunAwayWater"] = CanRunAwayWater,
                    ["CanSleep"] = CanSleep,
                    ["SleepDistance"] = SleepDistance,
                    ["Speed"] = Speed,
                    ["AreaMask"] = !AboveOrUnderGround ? 1 : 25,
                    ["AgentTypeID"] = !AboveOrUnderGround ? -1372625422 : 0,
                    ["HomePosition"] = string.Empty,
                    ["MemoryDuration"] = MemoryDuration,
                    ["States"] = new JArray
                    {
                        Stationary 
                            ? new HashSet<string> { "IdleState", "CombatStationaryState" }
                            : new HashSet<string> { "RoamState", "CombatState", "ChaseState", "RaidState" }
                    }
                };
            }
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { LangKeys.NoPermission, "Sorry you don't have permission to do that." },
                { LangKeys.Prefix, "<color=#8a916f>Guarded Crate</color>:\n" },

                { LangKeys.FailedToStartEvent, "Failed to start event." },
                { LangKeys.StartEvent, "Event starting." },
                { LangKeys.ClearEvents, "Cleaning up all running events." },
                
                { LangKeys.EventStart, "Special delivery is on its way to <color=#e7cf85>{0}</color> watch out it is heavily contested by guards, severity level <color=#e7cf85>{1}</color>.\nBe fast before the event ends in <color=#e7cf85>{2}</color>." },
                { LangKeys.EventCompleted, "<color=#e7cf85>{1}</color> has cleared the event at <color=#e7cf85>{0}</color>." },
                { LangKeys.EventEnded, "Event ended at <color=#e7cf85>{0}</color>; You were not fast enough; better luck next time!" },
                { LangKeys.EventNotFound, "Event not found, please make sure you have typed the correct name." },
                { LangKeys.EventIntersecting, "Another event is intersecting this position." },
                { LangKeys.EventPositionInvalid, "Event position invalid." },
                { LangKeys.EliminateGuards, "The crate is still contested eliminate all guards to gain access to high-valued loot." },
                { LangKeys.EventUpdated, "Event updated, please reload the plugin to take effect." },
                { LangKeys.InvalidGuardAmount, "Invalid guard amount must be between {0} - {1}." },
                
                { LangKeys.HelpStartEvent, "<color=#e7cf85>/{0}</color> start \"<color=#e7cf85><{1}></color>\", start an event of a specified type." },
                { LangKeys.HelpStopEvent, "<color=#e7cf85>/{0}</color> stop, stop all currently running events.\n\n" },
                { LangKeys.HelpHereEvent, "<color=#e7cf85>/{0}</color> here \"<color=#e7cf85><event-name></color>\", start an event at your position\n\n" },
                { LangKeys.HelpPositionEvent, "<color=#e7cf85>/{0}</color> position \"<color=#e7cf85><event-name></color>\" \"<color=#e7cf85>x y z</color>\", start an event at a specified position.\n\n" },
                { LangKeys.HelpLootEvent, "<color=#e7cf85>/{0}</color> loot \"<color=#e7cf85><event-name></color>\", create loot items that you wish to spawn in the crate, add the items to your inventory and run the command.\n\n" },
                { LangKeys.HelpGuardAmount, "<color=#e7cf85>/{0}</color> amount \"<color=#e7cf85><event-name></color>\", specify the guard amount to spawn.\n\n" },
                { LangKeys.HelpGuardLoadout, "<color=#e7cf85>/{0}</color> loadout \"<color=#e7cf85><event-name></color>\", set guard loadout using items in your inventory." }
            }, this);
        }

        private struct LangKeys
        {
            public const string Prefix = "Prefix";
            public const string NoPermission = "NoPermission";
            
            public const string FailedToStartEvent = "FailedToStartEvent";
            public const string ClearEvents = "ClearEvents";
            public const string StartEvent = "StartEvent";
            
            public const string EventCompleted = "EventCompleted";
            public const string EventStart = "EventStart";
            public const string EventEnded = "EventClear";
            public const string EventNotFound = "EventNotFound";
            public const string EventPositionInvalid = "EventPosInvalid";
            public const string EventIntersecting = "EventNearby";
            public const string EventUpdated = "EventUpdated";
            public const string EliminateGuards = "EliminateGuards";
            public const string InvalidGuardAmount = "InvalidGuardAmount";
            
            public const string HelpStartEvent = "HelpStartEvent";
            public const string HelpStopEvent = "HelpStopEvent";
            public const string HelpHereEvent = "HelpHereEvent";
            public const string HelpPositionEvent = "HelpPositionEvent";
            public const string HelpLootEvent = "HelpLootEvent";
            public const string HelpGuardAmount = "HelpGuardAmount";
            public const string HelpGuardLoadout = "HelpGuardLoadout";
        }

        #endregion
        
        #region Oxide Hooks

        private void OnServerInitialized()
        {
            SpawnManager.FindSpawnPoints();
            
            if (!string.IsNullOrEmpty(_configData.CommandName))
            {
                cmd.AddConsoleCommand(_configData.CommandName, this, nameof(GuardedCrateConsoleCommand));
                cmd.AddChatCommand(_configData.CommandName, this, nameof(EventCommandCommands));                
            }

            if (_configData.EnableAutoStart && _configData.AutoStartDuration > 0) 
                timer.Every(_configData.AutoStartDuration, () => TryStartEvent(null));
        }
        
        private void Init()
        {
            permission.RegisterPermission(PERM_USE, this);
            
            _instance = this;
            
            LoadData();
            
            SpawnManager.Initialize();
        }
        
        private void Unload()
        {
            GuardedCrateManager.OnUnload();
            EntitiesCache.OnUnload();
            SpawnManager.OnUnload();
            
            _instance = null;
        }

        private void OnEntityDeath(ScientistNPC scientist, HitInfo info)
        {
            EntitiesCache.FindCrateInstance(scientist)?.OnGuardKilled(scientist, info?.InitiatorPlayer);
            EntitiesCache.RemoveEntity(scientist);
        }
        
        private void OnEntityKill(ScientistNPC scientist)
        {
            EntitiesCache.FindCrateInstance(scientist)?.OnGuardKilled(scientist, null);
            EntitiesCache.RemoveEntity(scientist);
        }
        
        private void OnEntityKill(LootContainer container)
        {
            EntitiesCache.FindCrateInstance(container)?.OnCrateKilled(container);
            EntitiesCache.RemoveEntity(container);
        }
        
        private void OnEntityKill(CargoPlane plane)
        {
            EntitiesCache.FindCrateInstance(plane)?.OnPlaneKilled(plane);
            EntitiesCache.RemoveEntity(plane);
        }

        private object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            return EntitiesCache.FindCrateInstance(crate)
                ?.CanHackCrate(player);
        }

        #endregion
        
        #region Spawn Manager

        private static class SpawnManager
        {
            private static readonly LayerMask InterestedLayers = LayerMask.GetMask("Prevent_Building", "Vehicle_World", "Vehicle_Large", "Vehicle_Detailed");
            private static List<ZoneInfo> _ignoredZones;
            private static List<Collider> _tempColliders;
            private static List<Vector3> _spawnPoints;
            private static Coroutine _spawnRoutine;
            
            public static void Initialize()
            {
                _ignoredZones = new List<ZoneInfo>();
                _tempColliders = new List<Collider>();
                _spawnPoints = new List<Vector3>();
            }

            public static void OnUnload()
            {
                if (_spawnRoutine != null)
                    ServerMgr.Instance.StopCoroutine(_spawnRoutine);
                
                _ignoredZones.Clear();
                _tempColliders.Clear();
                _spawnPoints.Clear();
                _ignoredZones = null;
                _tempColliders = null;
                _spawnPoints = null;
                _spawnRoutine = null;
            }

            public static void FindSpawnPoints()
            {
                if (_spawnRoutine != null)
                    return;
                
                _spawnRoutine = ServerMgr.Instance.StartCoroutine(GenerateSpawnPoints(true));
            }
            
            private static IEnumerator GenerateSpawnPoints(bool exclusionZones, int attempts = 5000)
            {
                yield return null;
                
                if (exclusionZones && _instance._configData.ZoneManagerSettings.EnabledIgnoredZones)
                {
                    GetIgnoredZones();
                    yield return CoroutineEx.waitForEndOfFrame;
                    yield return CoroutineEx.waitForEndOfFrame;
                }
                
                float mapSizeX = TerrainMeta.Size.x / 2;
                float mapSizeZ = TerrainMeta.Size.z / 2;
                Vector3 spawnPoint = Vector3.zero;
                
                List<Vector3> list = Facepunch.Pool.Get<List<Vector3>>();
                
                for (int i = 0; i < attempts; i++)
                {
                    spawnPoint.x = UnityEngine.Random.Range(-mapSizeX, mapSizeX);
                    spawnPoint.z = UnityEngine.Random.Range(-mapSizeZ, mapSizeZ);
                    
                    if (TestSpawnPoint(ref spawnPoint))
                        list.Add(spawnPoint);

                    if (i % 10 == 0)
                        yield return CoroutineEx.waitForEndOfFrame;
                }

                _spawnPoints.AddRange(list);
                _tempColliders.Clear();
                
                Facepunch.Pool.FreeUnmanaged<Vector3>(ref list);
                Interface.Oxide.LogDebug("GuardedCrate: successfully found {0} spawn points.", _spawnPoints.Count);
                
                _spawnRoutine = null;
            }

            public static Vector3 GetSpawnPoint()
            {
                if (!(_spawnPoints?.Count > 0)) 
                    return Vector3.zero;
                
                for (int i = 0; i < 50; i++)
                {
                    Vector3 spawnPoint = _spawnPoints.GetRandom();
                    if (IsSpawnPointValid(spawnPoint)) 
                        return spawnPoint;
                    
                    _spawnPoints.Remove(spawnPoint);
                    
                    if (_spawnRoutine == null && _spawnPoints.Count < 50)
                        _spawnRoutine = ServerMgr.Instance.StartCoroutine(GenerateSpawnPoints(false));
                }

                return Vector3.zero;
            }
            
            private static void GetIgnoredZones()
            {
                if (_instance._configData.ZoneManagerSettings?.IgnoredZones == null ||
                    _instance._configData.ZoneManagerSettings.IgnoredZones.Count == 0)
                    return;

                if (_instance.ZoneManager == null || !_instance.ZoneManager.IsLoaded)
                    return;
                
                string[] zoneIds = _instance?.ZoneManager.Call("GetZoneIDs") as string[];
                if (zoneIds == null)
                    return;
                
                foreach (string zoneId in zoneIds)
                    AddIgnoredZone(zoneId);
            }

            private static void AddIgnoredZone(string zoneId)
            {
                if (!_instance._configData.ZoneManagerSettings.IgnoredZones.Contains(zoneId))
                    return;
                
                if (_instance.ZoneManager.Call("GetZoneLocation", zoneId) is not Vector3 position ||
                    position == Vector3.zero)
                    return;

                if (_instance.ZoneManager.Call("GetZoneRadius", zoneId) is not float radius)
                    return;
                
                _ignoredZones.Add(new ZoneInfo(position, radius));
            }

            private static bool TestSpawnPoint(ref Vector3 spawnPoint)
            {
                if (!Physics.Raycast(spawnPoint + Vector3.up * 300f, Vector3.down, out RaycastHit hit, 400f, Layers.Solid) || hit.GetEntity() != null) 
                    return false;
                
                spawnPoint.y = hit.point.y;
                
                if (!ValidBounds.TestInnerBounds(spawnPoint)) 
                    return false;
                
                if (AntiHack.TestInsideTerrain(spawnPoint)) 
                    return false;
                
                if (AntiHack.IsInsideMesh(spawnPoint)) 
                    return false;

                if (IsIgnoredZone(spawnPoint))
                    return false;

                return !IsBlockedTopology(spawnPoint) && IsSpawnPointValid(spawnPoint);
            }
            
            private static bool IsBlockedTopology(Vector3 spawnPoint)
            {
                return (TerrainMeta.TopologyMap.GetTopology(spawnPoint) & (int)BLOCKED_TOPOLOGY) != 0;
            }
            
            private static bool IsIgnoredZone(Vector3 spawnPoint)
            {
                foreach (ZoneInfo zoneInfo in _ignoredZones)
                {
                    if (zoneInfo.IsInBounds(spawnPoint))
                        return true;
                }
                
                return false;
            }
            
            private static bool IsSpawnPointValid(Vector3 spawnPoint) 
            {
                if (WaterLevel.Test(spawnPoint, true, true))
                    return false;

                try
                {
                    Vis.Colliders(spawnPoint, 15f, _tempColliders);
                    
                    foreach (Collider collider in _tempColliders)
                    {
                        if ((1 << collider.gameObject.layer & InterestedLayers) > 0)
                            return false;
                        
                        if (collider.name.Contains("radiation", CompareOptions.IgnoreCase))
                            return false;
                        
                        if (collider.name.Contains("rock", CompareOptions.IgnoreCase))
                            return false;
                        
                        if (collider.name.Contains("cliff", CompareOptions.IgnoreCase))
                            return false;
                        
                        if (collider.name.Contains("fireball", CompareOptions.IgnoreCase) ||
                            collider.name.Contains("iceberg", CompareOptions.IgnoreCase) ||
                            collider.name.Contains("ice_sheet", CompareOptions.IgnoreCase))
                            return false;
                    }
                }
                finally
                {
                    _tempColliders.Clear();
                }
                
                List<BasePlayer> players = Facepunch.Pool.Get<List<BasePlayer>>();

                try
                {
                    Vis.Entities(spawnPoint, 150, players, Layers.Mask.Player_Server);

                    foreach (BasePlayer player in players)
                    {
                        if (!player.IsSleeping() && !player.IsVisibleAndCanSee(spawnPoint))
                            return false;
                    }
                }
                finally
                {
                    Facepunch.Pool.FreeUnmanaged<BasePlayer>(ref players);
                }
                
                List<BaseEntity> entities = Facepunch.Pool.Get<List<BaseEntity>>();

                try
                {
                    Vis.Entities(spawnPoint, 150f, entities, Layers.PlayerBuildings);
                    
                    if (entities.Count > 0)
                        return false;
                }
                finally
                {
                    Facepunch.Pool.FreeUnmanaged<BaseEntity>(ref entities);
                }
                
                return true;
            }
        }

        private class ZoneInfo
        {
            public Vector3 Position;
            public float Radius;

            public ZoneInfo() { }

            public ZoneInfo(Vector3 position, float radius)
            {
                Position = position;
                Radius = radius;
            }
            
            public bool IsInBounds(Vector3 position)
            {
                return Vector3Ex.Distance2D(Position, position) <= Radius;
            }
        }

        #endregion

        #region Guarded Crate Manager

        private static class GuardedCrateManager
        {
            private static readonly List<GuardedCrateInstance> GuardedCrateInstances = new();

            public static void OnUnload() => CleanupInstances();

            public static bool HasIntersectingEvent(Vector3 position)
            {
                for (int i = 0; i < GuardedCrateInstances.Count; i++)
                {
                    GuardedCrateInstance crateInstance = GuardedCrateInstances[i];
                    if (crateInstance != null && Vector3Ex.Distance2D(crateInstance.transform.position, position) < 80f)
                        return true;
                }

                return false;
            }

            public static void CleanupInstances()
            {
                for (int i = GuardedCrateInstances.Count - 1; i >= 0; i--)
                    GuardedCrateInstances[i]?.EventEnded(true);
                
                GuardedCrateInstances.Clear();
            }
            
            public static void RegisterInstance(GuardedCrateInstance crateInstance)
            {
                GuardedCrateInstances.Add(crateInstance);
                
                _instance?.SubscribeToHooks(GuardedCrateInstances.Count);
            }
            
            public static void UnregisterInstance(GuardedCrateInstance crateInstance)
            {
                GuardedCrateInstances.Remove(crateInstance);
                
                _instance?.SubscribeToHooks(GuardedCrateInstances.Count);
            }
        }

        #endregion

        #region Guarded Crate Event

        private bool TryStartEvent(string eventName = null)
        {
            if (!NpcSpawn.IsPluginReady())
            {
                PrintWarning("NpcSpawn not loaded please download from https://codefling.com");
                Interface.Oxide.UnloadPlugin(Name);
                return false;
            }

            Vector3 position = SpawnManager.GetSpawnPoint();
            if (position == Vector3.zero)
            {
                PrintWarning("Failed to find a valid spawn point.");
                return false;
            }

            EventEntry eventEntry = !string.IsNullOrEmpty(eventName) ? _storedData.FindEventByName(eventName) : _storedData.CrateEventEntries.GetRandom();
            if (eventEntry == null)
            {
                PrintWarning("Failed to find a valid event entry please check your configuration.");
                return false;
            }
            
            GuardedCrateInstance.CreateInstance(eventEntry, position);
            return true;
        }
        
        private class GuardedCrateInstance : MonoBehaviour
        {
            public List<BaseEntity> guardSpawnInstances = new();
            public MapMarkerGenericRadius markerSpawnInstance;
            public HackableLockedCrate crateSpawnInstance;
            public CargoPlane planeSpawnInstance;
            public GuardConfig guardConfig;
            public List<ItemEntry> lootTable;
            private EventState eventState;

            public string eventName;
            public float eventSeconds = 120f;
            
            public bool enableLockToPlayer;
            public bool enableClanTag;

            public bool enableAutoHack;
            public float hackSeconds;
            
            public bool enableMarker;
            public Color markerColor1;
            public Color markerColor2;
            public float markerRadius;
            public float markerOpacity;

            public bool enableEliminateGuards;
            public int guardAmount;

            public bool enableLootTable;
            public int minLootAmount;
            public int maxLootAmount;
            
            public float thinkEvery = 1f;
            public float lastThinkTime;
            public float timePassed;
            public bool timeEnded;
            
            public static void CreateInstance(EventEntry eventEntry, Vector3 position)
            {
                if (eventEntry.GuardConfig.Parsed == null)
                    eventEntry.GuardConfig.CacheConfig();
                
                GuardedCrateInstance crateInstance = CustomUtils.CreateObjectWithComponent<GuardedCrateInstance>(position, Quaternion.identity, "Guarded_Create_Event");
                crateInstance.gameObject.AddComponent<SphereCollider>().radius = 15f;
                
                crateInstance.eventName = eventEntry.EventName;
                crateInstance.eventSeconds = eventEntry.EventDuration;
                
                crateInstance.enableLockToPlayer = eventEntry.EnableLockToPlayer;
                crateInstance.enableClanTag = eventEntry.EnableClanTag;
                
                crateInstance.enableAutoHack = eventEntry.EnableAutoHack;
                crateInstance.hackSeconds = eventEntry.HackSeconds;
                
                crateInstance.enableMarker = eventEntry.EnableMarker;
                crateInstance.markerColor1 = CustomUtils.GetColor(eventEntry.MapMarkerColor1);
                crateInstance.markerColor2 = CustomUtils.GetColor(eventEntry.MapMarkerColor2);
                crateInstance.markerRadius = eventEntry.MapMarkerRadius;
                crateInstance.markerOpacity = eventEntry.MapMarkerOpacity;
                
                crateInstance.enableLootTable = eventEntry.EnableLootTable;
                crateInstance.lootTable = eventEntry.LootTable;
                crateInstance.minLootAmount = eventEntry.LootMinAmount;
                crateInstance.maxLootAmount = eventEntry.LootMaxAmount;
                
                crateInstance.enableEliminateGuards = eventEntry.EnableEliminateGuards;
                crateInstance.guardConfig = eventEntry.GuardConfig;
                crateInstance.guardAmount = eventEntry.GuardAmount;
                crateInstance.EventStartup();
                
                GuardedCrateManager.RegisterInstance(crateInstance);
            }
            
            public static void RemoveInstance(GuardedCrateInstance crateInstance)
            {
                GuardedCrateManager.UnregisterInstance(crateInstance);
                
                if (crateInstance?.gameObject != null)
                    UnityEngine.GameObject.DestroyImmediate(crateInstance.gameObject);
            }

            #region Unity

            private void FixedUpdate()
            {
                if (lastThinkTime < thinkEvery)
                {
                    lastThinkTime += UnityEngine.Time.deltaTime;
                }
                else
                {
                    if (timeEnded)
                        return;

                    timePassed += lastThinkTime;

                    if (timePassed >= eventSeconds)
                    {
                        timeEnded = true;
                        EventFailed();
                        return;
                    }

                    lastThinkTime = 0.0f;
                }
            }

            #endregion
            
            #region Setup / Destroy

            private IEnumerator Initialize()
            {
                yield return CoroutineEx.waitForEndOfFrame;
                eventState = EventState.Active;
                yield return SpawnPlane();
                Notification.MessagePlayers(LangKeys.EventStart, MapHelper.PositionToString(transform.position), eventName, eventSeconds.ToStringTime());
            }
            
            public void Dispose()
            {
                StopAllCoroutines();
                CancelInvoke();
                ClearEntities();
                
                GuardedCrateInstance.RemoveInstance(this);
            }
            
            #endregion
            
            #region Management

            public void EventStartup()
            {
                enabled = true;
                eventState = EventState.Starting;
                StartCoroutine(Initialize());
                Interface.Oxide.CallHook("OnGuardedCrateEventStart", transform.position);
            }
            
            public void EventComplete(BasePlayer player)
            {
                if (eventState != EventState.Active)
                    return;
                
                eventState = EventState.Completed;
                EventWinner(player);
                UnlockCrates();
                EventEnded();
            }

            public void EventFailed()
            {
                eventState = EventState.Failed;
                Interface.Oxide.CallHook("OnGuardedCrateEventFailed", transform.position);
                Notification.MessagePlayers(LangKeys.EventEnded, MapHelper.PositionToString(transform.position));
                EventEnded(true);
            }

            public void EventEnded(bool forced = false) => Dispose();
            
            private void EventWinner(BasePlayer player)
            {
                if (player == null) 
                    return;
                
                string displayName = player.displayName;
                
                if (enableClanTag)
                {
                    if (_instance != null && _instance.Clans.IsPluginReady())
                        displayName = string.Format("[{0}]{1}", _instance.Clans.Call<string>("GetClanOf", player.userID), displayName);
                }

                if (enableLockToPlayer)
                {
                    if (_instance != null && _instance.HackableLock.IsPluginReady())
                        _instance.HackableLock.Call("LockCrateToPlayer", player, crateSpawnInstance);
                }
                
                Interface.CallHook("OnGuardedCrateEventEnded", player, crateSpawnInstance);
                Notification.MessagePlayers(LangKeys.EventCompleted, MapHelper.PositionToString(transform.position), displayName);
            }
            
            private void UnlockCrates()
            {
                if (eventState != EventState.Completed) 
                    return;

                if (enableAutoHack)
                    crateSpawnInstance.StartHacking();
                
                crateSpawnInstance.shouldDecay = true;
                crateSpawnInstance.RefreshDecay();
            }
            
            #endregion
            
            #region Spawning
            
            public IEnumerator SpawnEntities()
            {
                yield return CoroutineEx.waitForEndOfFrame;
                yield return SpawnMarker();
                yield return SpawnCrate();
                yield return SpawnGuards();
            }
            
            private IEnumerator SpawnPlane()
            {
                yield return CoroutineEx.waitForEndOfFrame;
                
                planeSpawnInstance = (CargoPlane)GameManager.server.CreateEntity(PLANE_PREFAB);
                planeSpawnInstance.InitDropPosition(transform.position);
                planeSpawnInstance.Spawn();
                planeSpawnInstance.secondsTaken = 0f;
                planeSpawnInstance.secondsToTake = 30f;
                planeSpawnInstance.gameObject.AddComponent<CargoPlaneComponent>().guardedCrate = this;
                
                EntitiesCache.CreateEntity(planeSpawnInstance, this);
            }
            
            private IEnumerator SpawnGuards()
            {
                yield return CoroutineEx.waitForEndOfFrame;
                
                if (_instance == null || !_instance.NpcSpawn.IsPluginReady())
                {
                    Interface.Oxide.LogDebug("NpcSpawn not loaded, please load the plugin.");
                    yield break;
                }

                for (int i = 0; i < guardAmount; i++)
                {
                    Vector3 position = transform.position.GetPointAround(10551297, 5f, (360f / guardAmount) * i);
                    
                    yield return SpawnGuard(position);
                }
            }

            private IEnumerator SpawnGuard(Vector3 position)
            {
                yield return CoroutineEx.waitForEndOfFrame;
                
                guardConfig.Parsed["HomePosition"] = position.ToString();
                
                ScientistNPC entity = (ScientistNPC)_instance?.NpcSpawn.Call("SpawnNpc", position, guardConfig.Parsed);
                guardSpawnInstances.Add(entity);
                EntitiesCache.CreateEntity(entity, this);
            }
            
            private IEnumerator SpawnMarker()
            {
                yield return CoroutineEx.waitForEndOfFrame;

                if (!enableMarker)
                    yield break;
                
                markerSpawnInstance = CustomUtils.CreateEntity<MapMarkerGenericRadius>(MARKER_PREFAB, transform.position, Quaternion.identity);
                markerSpawnInstance.EnableSaving(false);
                markerSpawnInstance.color1 = markerColor1;
                markerSpawnInstance.color2 = markerColor2;
                markerSpawnInstance.radius = markerRadius;
                markerSpawnInstance.alpha = markerOpacity;
                markerSpawnInstance.Spawn();
                markerSpawnInstance.SendUpdate();
                markerSpawnInstance.InvokeRepeating(nameof(MapMarkerGenericRadius.SendUpdate), 10.0f, 10.0f);
            }
            
            private IEnumerator SpawnCrate()
            {
                yield return CoroutineEx.waitForEndOfFrame;
                
                crateSpawnInstance = CustomUtils.CreateEntity<HackableLockedCrate>(CRATE_PREFAB, transform.position + (Vector3.up * 100f), Quaternion.identity);
                crateSpawnInstance.EnableSaving(false);
                crateSpawnInstance.shouldDecay = false;
                crateSpawnInstance.hackSeconds = HackableLockedCrate.requiredHackSeconds - hackSeconds;
                crateSpawnInstance.Spawn();
                crateSpawnInstance.Invoke(RefillCrate, 2f);
                
                EntitiesCache.CreateEntity(crateSpawnInstance, this);
            }
            
            private void ClearGuards()
            {
                for (int i = guardSpawnInstances.Count - 1; i >= 0; i--)
                    guardSpawnInstances[i].SafeKill();
                
                guardSpawnInstances.Clear();
            }
            
            private void ClearMarker()
            {
                markerSpawnInstance.SafeKill();
                markerSpawnInstance = null;
            }
            
            private void ClearCrate(bool completed)
            {
                if (!completed)
                    crateSpawnInstance.SafeKill();
                
                crateSpawnInstance = null;
            }
            
            private void ClearCargoPlane()
            {
                planeSpawnInstance.SafeKill();
                planeSpawnInstance = null;
            }
            
            private void ClearEntities()
            {
                ClearCrate(eventState == EventState.Completed);
                ClearCargoPlane();
                ClearMarker();
                ClearGuards();
            }
            
            #endregion

            #region Loot
            
            public object CanPopulateCrate()
            {
                return !enableLootTable || lootTable.Count <= 0 ? null : (object)false;
            }

            private List<ItemEntry> GenerateLoot()
            {
                int itemCount = UnityEngine.Random.Range(minLootAmount, maxLootAmount);
                int itemTries = 100;

                List<ItemEntry> items = new List<ItemEntry>();

                do
                {
                    ItemEntry lootItem = lootTable.GetRandom();
                    if (!items.Contains(lootItem))
                        items.Add(lootItem);
                } 
                while (items.Count < itemCount && --itemTries > 0);

                return items;
            }

            private void RefillCrate()
            {
                if (!enableLootTable)
                    return;
                
                if (lootTable == null || lootTable.Count == 0) 
                    return;
                
                List<ItemEntry> items = GenerateLoot();
                if (items == null || items.Count == 0)
                    return;

                crateSpawnInstance.inventory.onItemAddedRemoved = null;
                crateSpawnInstance.inventory.SafeClear();
                crateSpawnInstance.inventory.capacity = items.Count;
                
                foreach (ItemEntry lootItem in items)
                {
                    Item item = lootItem.CreateItem();
                    if (!item.MoveToContainer(crateSpawnInstance.inventory))
                        item.Remove();
                }

                items.Clear();
            }
            
            #endregion

            #region Oxide Hooks
            
            public object CanHackCrate(BasePlayer player)
            {
                if (!enableEliminateGuards)
                    return null;

                if (guardSpawnInstances.Count > 0)
                {
                    Notification.MessagePlayer(player, LangKeys.EliminateGuards);
                    return true;
                }
                
                return null;
            }

            public void OnGuardKilled(ScientistNPC scientist, BasePlayer player)
            {
                guardSpawnInstances.Remove(scientist);
                
                timePassed = 0f;
                
                if (guardSpawnInstances.Count > 0) 
                    return;
                
                EventComplete(player);
            }

            public void OnPlaneKilled(CargoPlane plane)
            {
                planeSpawnInstance = null;
            }

            public void OnCrateKilled(LootContainer container)
            {
                crateSpawnInstance = null;
            }

            #endregion
        }

        private class CargoPlaneComponent : MonoBehaviour
        {
            public GuardedCrateInstance guardedCrate;
            public CargoPlane cargoPlane;
            public bool hasDropped;
            
            private void Awake()
            {
                cargoPlane = GetComponent<CargoPlane>();
                cargoPlane.dropped = true;
            }

            private void Update()
            {
                if (hasDropped) 
                    return;
                
                float time = Mathf.InverseLerp(0.0f, cargoPlane.secondsToTake, cargoPlane.secondsTaken);
                if (!(time >= 0.5)) 
                    return;
                
                hasDropped = true;

                if (!guardedCrate.IsUnityNull())
                    guardedCrate.StartCoroutine(guardedCrate.SpawnEntities());
                
                Destroy(this);
            }
        }
        
        private enum EventState
        { 
            Starting,
            Active,
            Failed,
            Completed,
        }

        #endregion
        
        #region Entities Cache

        private static class EntitiesCache
        {
            private static Dictionary<BaseEntity, GuardedCrateInstance> Entities = new();
            
            public static void OnUnload() => Entities.Clear();

            public static GuardedCrateInstance FindCrateInstance(BaseEntity entity)
            {
                if (entity == null) 
                    return null;
                
                return Entities.TryGetValue(entity, out GuardedCrateInstance component) ? component : null;
            }
            
            public static void CreateEntity(BaseEntity entity, GuardedCrateInstance component) => Entities.Add(entity, component);

            public static void RemoveEntity(BaseEntity entity) => Entities.Remove(entity);
        }

        #endregion

        #region Notification

        private static class Notification
        {
            public static void MessagePlayer(ConsoleSystem.Arg arg, string langKey, params object[] args)
            {
                if (_instance == null || arg == null) 
                    return;
                
                arg.ReplyWith((args.Length > 0 ? string.Format(_instance.lang.GetMessage(langKey, _instance), args) : _instance.lang.GetMessage(langKey, _instance)));
            }
        
            public static void MessagePlayer(BasePlayer player, string langKey, params object[] args)
            {
                if (_instance == null || player == null) 
                    return;
                
                player.ChatMessage((args.Length > 0 ? string.Format(_instance.lang.GetMessage(langKey, _instance, player.UserIDString), args) : _instance.lang.GetMessage(langKey, _instance, player.UserIDString)));
            }

            public static void MessagePlayers(string langKey, params object[] args)
            {
                if (_instance == null) 
                    return;
                
                string message = args?.Length > 0 ? string.Format(_instance.lang.GetMessage(langKey, _instance), args) : _instance.lang.GetMessage(langKey, _instance);
                if (_instance._configData.MessageSettings.EnableChat)
                    ConsoleNetwork.BroadcastToAllClients("chat.add", 2, _instance._configData.MessageSettings.ChatIcon, _instance._configData.MessageSettings.EnableChatPrefix ? (_instance.lang.GetMessage(LangKeys.Prefix, _instance) + message) : message);
                if (_instance._configData.MessageSettings.EnableToast)
                    ConsoleNetwork.BroadcastToAllClients("gametip.showtoast_translated", 2, null, message);
                if (_instance._configData.MessageSettings.EnableGuiAnnouncements && _instance.GUIAnnouncements.IsPluginReady())
                    _instance.GUIAnnouncements?.Call("CreateAnnouncement", message, _instance._configData.MessageSettings.GuiAnnouncementsBgColor, _instance._configData.MessageSettings.GuiAnnouncementsTextColor, null, 0.03f);
            }
        }

        #endregion

        #region Console Command
        
        private void GuardedCrateConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null || !arg.IsRcon) 
                return;
            
            if (!arg.HasArgs())
            {
                DisplayHelpText(arg.Player());
                return;
            }

            string option = arg.GetString(0);
            if (option.Equals("start", StringComparison.OrdinalIgnoreCase))
            {
                bool started = TryStartEvent(string.Join(" ", arg.Args.Skip(1).ToArray()));
                Notification.MessagePlayer(arg, (started ? LangKeys.StartEvent : LangKeys.FailedToStartEvent));
                return;
            }
            
            if (option.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                GuardedCrateManager.CleanupInstances();
                Notification.MessagePlayer(arg, LangKeys.ClearEvents);
                return;
            }
            
            if (option.Equals("position", StringComparison.OrdinalIgnoreCase))
            {
                if (!arg.HasArgs(3))
                {
                    DisplayHelpText(arg.Player());
                    return;
                }
                
                EventEntry eventEntry = _storedData.FindEventByName(arg.GetString(1));
                if (eventEntry == null)
                {
                    Notification.MessagePlayer(arg, LangKeys.EventNotFound);
                    return;
                }

                Vector3 position = arg.GetVector3(2);
                if (position == Vector3.zero)
                {
                    Notification.MessagePlayer(arg, LangKeys.EventPositionInvalid);
                    return;
                }

                if (GuardedCrateManager.HasIntersectingEvent(position))
                {
                    Notification.MessagePlayer(arg, LangKeys.EventIntersecting);
                    return;
                }
                
                GuardedCrateInstance.CreateInstance(eventEntry, position);
                return;
            }
            
            DisplayHelpText(arg.Player());
        }

        #endregion

        #region Chat Command
        
        private void EventCommandCommands(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM_USE))
            {
                Notification.MessagePlayer(player, LangKeys.NoPermission);
                return;
            }

            if (args.Length < 1)
            {
                DisplayHelpText(player);
                return;
            }

            string option = args[0];
            if (option.Equals("start", StringComparison.OrdinalIgnoreCase))
            {
                bool started = TryStartEvent(string.Join(" ", args.Skip(1).ToArray()));
                Notification.MessagePlayer(player, (started ? LangKeys.StartEvent : LangKeys.FailedToStartEvent));
                return;
            }
            
            if (option.Equals("stop", StringComparison.OrdinalIgnoreCase))
            {
                GuardedCrateManager.CleanupInstances();
                Notification.MessagePlayer(player, LangKeys.ClearEvents);
                return;
            }
            
            if (option.Equals("here", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    DisplayHelpText(player);
                    return;
                }
                
                EventEntry eventEntry = _storedData.FindEventByName(args[1]);
                if (eventEntry == null)
                {
                    Notification.MessagePlayer(player, LangKeys.EventNotFound);
                    return;
                }
                
                GuardedCrateInstance.CreateInstance(eventEntry, player.transform.position);
                return;
            }

            if (option.Equals("position", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3)
                {
                    DisplayHelpText(player);
                    return;
                }
                
                EventEntry eventEntry = _storedData.FindEventByName(args[1]);
                if (eventEntry == null)
                {
                    Notification.MessagePlayer(player, LangKeys.EventNotFound);
                    return;
                }

                Vector3 position = args[2].ToVector3();
                if (position == Vector3.zero)
                {
                    Notification.MessagePlayer(player, LangKeys.EventPositionInvalid);
                    return;
                }

                if (GuardedCrateManager.HasIntersectingEvent(position))
                {
                    Notification.MessagePlayer(player, LangKeys.EventIntersecting);
                    return;
                }

                GuardedCrateInstance.CreateInstance(eventEntry, position);
                return;
            }
            
            if (option.Equals("loot", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    DisplayHelpText(player);
                    return;
                }

                EventEntry eventEntry = _storedData.FindEventByName(args[1]);
                if (eventEntry == null)
                {
                    Notification.MessagePlayer(player, LangKeys.EventNotFound);
                    return;
                }
                
                if (player.inventory == null)
                    return;
                
                eventEntry.LootTable.Clear();
                eventEntry.LootTable.AddRange(ItemEntry.SaveItems(player.inventory.containerMain));
                eventEntry.LootTable.AddRange(ItemEntry.SaveItems(player.inventory.containerBelt));
                
                SaveData();
                Notification.MessagePlayer(player, LangKeys.EventUpdated);
                return;
            }
            
            if (option.Equals("amount", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 3)
                {
                    DisplayHelpText(player);
                    return;
                }

                EventEntry eventEntry = _storedData.FindEventByName(args[1]);
                if (eventEntry == null)
                {
                    Notification.MessagePlayer(player, LangKeys.EventNotFound);
                    return;
                }
                
                int amount;
                if (!int.TryParse(args[2], out amount) || amount < 1)
                {
                    Notification.MessagePlayer(player, LangKeys.InvalidGuardAmount);
                    return;
                }
                
                eventEntry.GuardAmount = amount;
                
                SaveData();
                Notification.MessagePlayer(player, LangKeys.EventUpdated);
                return;
            }
            
            if (option.Equals("loadout", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length < 2)
                {
                    DisplayHelpText(player);
                    return;
                }
                
                EventEntry eventEntry = _storedData.FindEventByName(args[1]);
                if (eventEntry == null)
                {
                    Notification.MessagePlayer(player, LangKeys.EventNotFound);
                    return;
                }
                
                if (player.inventory == null)
                    return;
                
                eventEntry.GuardConfig.BeltItems.Clear();
                eventEntry.GuardConfig.WearItems.Clear();
                
                eventEntry.GuardConfig.BeltItems = GuardConfig.BeltEntry.SaveItems(player.inventory.containerBelt);
                eventEntry.GuardConfig.WearItems = GuardConfig.WearEntry.SaveItems(player.inventory.containerWear);
                
                SaveData();
                Notification.MessagePlayer(player, LangKeys.EventUpdated);
                return;
            }
            
            DisplayHelpText(player);
        }
        
        private void DisplayHelpText(BasePlayer player)
        {
            StringBuilder sb = Facepunch.Pool.Get<StringBuilder>();

            try
            {
                sb.Clear();
                sb.AppendFormat(lang.GetMessage(LangKeys.Prefix, this, player.UserIDString))
                    .AppendFormat(lang.GetMessage(LangKeys.HelpStartEvent, this, player.UserIDString), _configData.CommandName, string.Join("|", _storedData.EventNames))
                    .AppendFormat(lang.GetMessage(LangKeys.HelpStopEvent, this, player.UserIDString), _configData.CommandName)
                    .AppendFormat(lang.GetMessage(LangKeys.HelpHereEvent, this, player.UserIDString), _configData.CommandName)
                    .AppendFormat(lang.GetMessage(LangKeys.HelpPositionEvent, this, player.UserIDString), _configData.CommandName)
                    .AppendFormat(lang.GetMessage(LangKeys.HelpLootEvent, this, player.UserIDString), _configData.CommandName)
                    .AppendFormat(lang.GetMessage(LangKeys.HelpGuardAmount, this, player.UserIDString), _configData.CommandName)
                    .AppendFormat(lang.GetMessage(LangKeys.HelpGuardLoadout, this, player.UserIDString), _configData.CommandName);
                
                Notification.MessagePlayer(player, sb.ToString());
            }
            finally
            {
                sb.Clear();
                Facepunch.Pool.FreeUnmanaged(ref sb);
            }
        }

        #endregion
        
        #region Hook Subscribing

        private readonly HashSet<string> _hooks = new()
        {
            "OnEntityDeath",
            "OnEntityKill",
            "CanHackCrate"
        };

        private void SubscribeToHooks(int count)
        {
            if (count > 0)
            {
                foreach (string hook in _hooks) 
                    Subscribe(hook);
                
                return;
            }

            if (count == 0)
            {
                foreach (string hook in _hooks) 
                    Unsubscribe(hook);
            }
        }

        #endregion
        
        #region Alpha Loot

        private object CanPopulateLoot(HackableLockedCrate crate)
        {
            return EntitiesCache.FindCrateInstance(crate)
                ?.CanPopulateCrate();
        }

        #endregion

        #region Rust Edit

        private object OnNpcRustEdit(ScientistNPC npc)
        {
            return EntitiesCache.FindCrateInstance(npc) != null ? (object)true : null;
        }

        #endregion

        #region API Hooks
        
        private bool API_IsGuardedCrateCargoPlane(CargoPlane entity)
        {
            return EntitiesCache.FindCrateInstance(entity) != null;
        }
        
        private bool API_IsGuardedCrateEntity(BaseEntity entity)
        {
            return EntitiesCache.FindCrateInstance(entity) != null;
        }

        #endregion
    }
}

namespace GuardedCrateEx
{
    internal static class CustomUtils
    {
        public static T CreateObjectWithComponent<T>(Vector3 position, Quaternion rotation, string name) where T : MonoBehaviour
        {
            return new GameObject(name)
            {
                layer = (int)Layer.Prevent_Building,
                transform =
                {
                    position = position,
                    rotation = rotation
                }
            }.AddComponent<T>();
        }
        
        public static T CreateEntity<T>(string prefab, Vector3 position, Quaternion rotation) where T : BaseEntity
        {
            T baseEntity = (T)GameManager.server.CreateEntity(prefab, position, rotation);
            baseEntity.enableSaving = false;
            return baseEntity;
        }
        
        public static Color GetColor(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.yellow;
        }
    }
    
    internal static class ExtensionMethods
    {
        public static bool IsPluginReady(this Plugin plugin) => plugin != null && plugin.IsLoaded;
        
        public static string ToStringTime(this float seconds) 
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
            if (timeSpan.Days >= 1) return $"{timeSpan.Days} day{(timeSpan.Days != 1 ? "(s)" : "")}";
            if (timeSpan.Hours >= 1) return $"{timeSpan.Hours} hour{(timeSpan.Hours != 1 ? "(s)" : "")}";
            return timeSpan.Minutes >= 1 ? $"{timeSpan.Minutes} minute{(timeSpan.Minutes != 1 ? "(s)" : "")}" : $"{timeSpan.Seconds} second{(timeSpan.Seconds == 1 ? "(s)" : "")}";
        }
        
        public static Vector3 GetPointAround(this Vector3 origin, int layers, float radius, float angle)
        {
            Vector3 pointAround = Vector3.zero;
            pointAround.x = origin.x + radius * Mathf.Sin(angle * Mathf.Deg2Rad);
            pointAround.z = origin.z + radius * Mathf.Cos(angle * Mathf.Deg2Rad);
            pointAround.y = TerrainMeta.HeightMap.GetHeight(origin) + 100f;
            
            if (Physics.Raycast(pointAround, Vector3.down, out RaycastHit hit, 200f, layers, QueryTriggerInteraction.Ignore))
                pointAround.y = hit.point.y;
            
            pointAround.y += 0.25f;
            return pointAround;
        }
        
        public static void SafeKill(this BaseEntity entity)
        {
            if (entity != null && !entity.IsDestroyed)
                entity.Kill();
        }

        public static void SafeClear(this ItemContainer container)
        {
            for (int i = container.itemList.Count - 1; i >= 0; i--)
            {
                Item item = container.itemList[i];
                item.RemoveFromContainer();
                item.Remove();
            }
        }
    }
}