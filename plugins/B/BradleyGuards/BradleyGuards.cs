/*
 * Copyright (c) 2023 Bazz3l
 * 
 * Bradley Guards cannot be copied, edited and/or (re)distributed without the express permission of Bazz3l.
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
using System.Collections;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System;
using Oxide.Plugins.BradleyGuardsExtensionMethods;
using Oxide.Core.Plugins;
using Oxide.Core;
using Rust;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Facepunch;
using HarmonyLib;
using Network;
using Action = System.Action;

namespace Oxide.Plugins
{
    [Info("Bradley Guards", "Bazz3l", "1.6.6")]
    [Description("Spawn reinforcements for bradley when destroyed at configured monuments.")]
    internal class BradleyGuards : RustPlugin
    {
        [PluginReference] private Plugin NpcSpawn, GUIAnnouncements;
        
        #region Fields
        
        private const string PERM_USE = "bradleyguards.use";
        
        private const float INITIALIZE_DELAY = 10f;
        
        private StoredData _storedData;
        private ConfigData _configData;
        private Coroutine _setupRoutine;
        
        private static BradleyGuards Instance;

        #endregion
        
        #region Local
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { MessageKeys.NoPermission, "Sorry you don't have permission to do that." },
                { MessageKeys.Prefix, "<color=#8a916f>Bradley Guards</color>:\n" },
                
                { MessageKeys.EventStart, "Armed reinforcements en route to <color=#e7cf85>{0}</color> eliminate the guards to gain access to high-value loot." },
                { MessageKeys.EventEnded, "Armed reinforcements have been eliminated at <color=#e7cf85>{0}</color>." },
                { MessageKeys.EventUpdated, "Event updated, please reload the plugin to take effect." },
                { MessageKeys.EventNotFound, "Event not found, please make sure you have typed the correct name." },
                { MessageKeys.DisplayNameEmpty, "Invalid event name provided." },
                { MessageKeys.InvalidGuardAmount, "Invalid guard amount must be between <color=#e7cf85>{0}</color> - <color=#e7cf85>{1}</color>." },
                { MessageKeys.InvalidBooleanValue, "Invalid boolean value provided, must be true or false." },
                
                { MessageKeys.HelpEventEnable, "<color=#e7cf85>/{0}</color> \"<color=#e7cf85><monument-path></color>\" enable <color=#e7cf85><true|false></color>\n" },
                { MessageKeys.HelpEventName, "<color=#e7cf85>/{0}</color> \"<color=#e7cf85><monument-path></color>\" display \"<color=#e7cf85><name-here></color>\"\n" },
                { MessageKeys.HelpGuardAmount, "<color=#e7cf85>/{0}</color> \"<color=#e7cf85><monument-path></color>\" amount <color=#e7cf85><number></color>\n" },
                { MessageKeys.HelpGuardLoadout, "<color=#e7cf85>/{0}</color> \"<color=#e7cf85><monument-path></color>\" loadout" },
            }, this);
        }

        private class MessageKeys
        {
            public static readonly string Prefix = "Prefix";
            public static readonly string NoPermission = "NoPermission";
            
            public static readonly string EventStart = "EventStart";
            public static readonly string EventEnded = "EventEnded";
            public static readonly string EventNotFound = "EventNotFound";
            public static readonly string EventUpdated = "EventUpdated";
            public static readonly string DisplayNameEmpty = "InvalidDisplayName";
            public static readonly string InvalidGuardAmount = "InvalidGuardAmount";
            public static readonly string InvalidBooleanValue = "InvalidBooleanValue";
            
            public static readonly string HelpEventEnable = "HelpEventEnable";
            public static readonly string HelpEventName = "HelpEventName";
            public static readonly string HelpGuardAmount = "HelpGuardAmount";
            public static readonly string HelpGuardLoadout = "HelpGuardLoadout";
        }

        private void MessagePlayer(BasePlayer player, string langKey, params object[] args)
        {
            if (player == null || !player.IsConnected) return;
            string message = lang.GetMessage(langKey, this);
            player.ChatMessage(args?.Length > 0 ? string.Format(message, args) : message);
        }
        
        private void MessagePlayers(string langKey, params object[] args)
        {
            string message =  lang.GetMessage(langKey, this);
            message = args?.Length > 0 ? string.Format(message, args) : message;
            
            if (_configData.MessageSettings.EnableChat)
                ConsoleNetwork.BroadcastToAllClients("chat.add", 2, _configData.MessageSettings.ChatIcon, _configData.MessageSettings.EnableChatPrefix ? (lang.GetMessage(MessageKeys.Prefix, this) + message) : message);
            
            if (_configData.MessageSettings.EnableToast)
                ConsoleNetwork.BroadcastToAllClients("gametip.showtoast_translated", 2, null, message);
            
            if (_configData.MessageSettings.EnableGuiAnnouncements && GUIAnnouncements.IsReady())
                GUIAnnouncements?.Call("CreateAnnouncement", message, _configData.MessageSettings.GuiAnnouncementsBgColor, _configData.MessageSettings.GuiAnnouncementsTextColor, null, 0.03f);
        }
        
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

                if (_configData.CommandName == null || _configData.MessageSettings == null)
                {
                    PrintWarning("Updated config.");
                    LoadDefaultConfig();
                    SaveConfig();
                }
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
            public string CommandName = "bguard";
            
            [JsonProperty("enable auto unlock crates when guards are eliminated")]
            public bool EnableAutoUnlock;
            
            [JsonProperty("enable auto extinguish crates when guards are eliminated")]
            public bool EnableAutoExtinguish;

            [JsonProperty("bradley starting health")]
            public float BradleyHealth;

            [JsonProperty("crate spawn amount")]
            public int CrateSpawnAmount;
            
            [JsonProperty("message notification settings")]
            public MessageSettings MessageSettings;

            public static ConfigData DefaultConfig()
            {
                return new ConfigData
                {
                    CommandName = "bguard",
                    EnableAutoUnlock = true,
                    EnableAutoExtinguish = true,
                    BradleyHealth = 1000f,
                    CrateSpawnAmount = 4,
                    MessageSettings = new MessageSettings()
                    {
                        EnableToast = false,
                        EnableChat = true, 
                        EnableChatPrefix = true,
                        ChatIcon = 76561199542550973,
                        EnableGuiAnnouncements = false,
                        GuiAnnouncementsBgColor = "Purple",
                        GuiAnnouncementsTextColor = "White"
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

        #endregion

        #region Storage

        private void LoadDefaultData()
        {
            _storedData = new StoredData
            {
                BradleyEventEntries = new Dictionary<string, EventEntry>
                {
                    ["assets/bundled/prefabs/autospawn/monument/xlarge/launch_site_1.prefab"] = new EventEntry
                    {
                        DisplayName = "Launch Site",
                        EnabledEvent = true,
                        BoundsPosition = new Vector3(0f, 0f, 0f),
                        BoundsSize = new Vector3(580f, 280f, 300f),
                        LandingPosition = new Vector3(152.3f, 3f, 0f),
                        LandingRotation = new Vector3(0f, 90f, 0f),
                        ChinookPosition = new Vector3(-195f, 150f, 25f),
                        GuardAmount = 10,
                        GuardConfig = new GuardConfig
                        {
                            Name = "Launch Site Guard",
                            WearItems = new List<GuardConfig.WearEntry>
                            {
                                new GuardConfig.WearEntry
                                {
                                    ShortName = "hazmatsuit_scientist_peacekeeper",
                                    SkinID = 0UL
                                }
                            },
                            BeltItems = new List<GuardConfig.BeltEntry>
                            {
                                new GuardConfig.BeltEntry
                                {
                                    ShortName = "smg.mp5",
                                    Amount = 1,
                                    SkinID = 0UL,
                                    Mods = new List<string>()
                                },
                                new GuardConfig.BeltEntry
                                {
                                    ShortName = "syringe.medical",
                                    Amount = 10,
                                    SkinID = 0UL,
                                    Mods = new List<string>()
                                },
                            },
                            Kit = "",
                            Health = 250f,
                            RoamRange = 25f,
                            ChaseRange = 40f,
                            SenseRange = 150f,
                            AttackRangeMultiplier = 8f,
                            CheckVisionCone = false,
                            VisionCone = 180f,
                            DamageScale = 1f,
                            TurretDamageScale = 0.25f,
                            AimConeScale = 0.35f,
                            DisableRadio = false,
                            CanRunAwayWater = true,
                            CanSleep = false,
                            Speed = 8.5f,
                            AreaMask = 1,
                            AgentTypeID = -1372625422,
                            MemoryDuration = 30f
                        }
                    },
                    ["assets/bundled/prefabs/autospawn/monument/large/airfield_1.prefab"] = new EventEntry
                    {
                        DisplayName = "Airfield Guard",
                        EnabledEvent = false,
                        BoundsPosition = new Vector3(0f, 0f, 0f),
                        BoundsSize = new Vector3(340f, 260f, 300f),
                        LandingPosition = new Vector3(0f, 0f, -28f),
                        LandingRotation = new Vector3(0f, 0f, 0f),
                        ChinookPosition = new Vector3(-195f, 150f, 25f),
                        GuardAmount = 10,
                        GuardConfig = new GuardConfig
                        {
                            Name = "Guarded Crate",
                            WearItems = new List<GuardConfig.WearEntry>
                            {
                                new GuardConfig.WearEntry
                                {
                                    ShortName = "hazmatsuit_scientist_peacekeeper",
                                    SkinID = 0UL
                                }
                            },
                            BeltItems = new List<GuardConfig.BeltEntry>
                            {
                                new GuardConfig.BeltEntry
                                {
                                    ShortName = "smg.mp5",
                                    Amount = 1,
                                    SkinID = 0UL,
                                    Mods = new List<string>()
                                },
                                new GuardConfig.BeltEntry
                                {
                                    ShortName = "syringe.medical",
                                    Amount = 10,
                                    SkinID = 0UL,
                                    Mods = new List<string>()
                                },
                            },
                            Kit = "",
                            Health = 250f,
                            RoamRange = 25f,
                            ChaseRange = 40f,
                            SenseRange = 150f,
                            AttackRangeMultiplier = 8f,
                            CheckVisionCone = false,
                            VisionCone = 180f,
                            DamageScale = 1f,
                            TurretDamageScale = 0.25f,
                            AimConeScale = 0.35f,
                            DisableRadio = false,
                            CanRunAwayWater = true,
                            CanSleep = false,
                            Speed = 8.5f,
                            AreaMask = 1,
                            AgentTypeID = -1372625422,
                            MemoryDuration = 30f
                        }
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
            if (_storedData == null || !_storedData.IsValid) 
                return;
            
            Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);
        }

        private class StoredData
        {
            public Dictionary<string, EventEntry> BradleyEventEntries = new(StringComparer.OrdinalIgnoreCase);

            [JsonIgnore] 
            public bool IsValid => BradleyEventEntries != null && BradleyEventEntries.Count > 0;
            
            public EventEntry FindEntryByName(string monumentName)
            {
                return BradleyEventEntries.TryGetValue(monumentName, out EventEntry eventEntry) ? eventEntry : null;
            }
        }

        private class EventEntry
        {
            [JsonProperty("display name")]
            public string DisplayName;
            
            [JsonProperty("enabled")]
            public bool EnabledEvent;
            
            [JsonProperty("bounds center")]
            public Vector3 BoundsPosition;
            [JsonProperty("bounds size")]
            public Vector3 BoundsSize;
            
            [JsonProperty("landing position")]
            public Vector3 LandingPosition;
            [JsonProperty("landing rotation")]
            public Vector3 LandingRotation;
            
            [JsonProperty("chinook position")]
            public Vector3 ChinookPosition;
            
            [JsonProperty("guard spawn amount")]
            public int GuardAmount;
            
            [JsonProperty("guard spawn profile")]
            public GuardConfig GuardConfig;

            public IEnumerator Create(Transform transform, bool enableAutoExtinguish, bool enableAutoUnlock)
            {
                Vector3 landingRotation = transform.TransformDirection(transform.rotation.eulerAngles - LandingRotation);
                Vector3 landingPosition = transform.TransformPoint(LandingPosition);
                Vector3 chinookPosition = transform.TransformPoint(ChinookPosition);
                
                if (GuardConfig.Parsed == null)
                    GuardConfig.CacheConfig();
                
                BradleyGuardsEvent component = Utils.CreateObjectWithComponent<BradleyGuardsEvent>(landingPosition, Quaternion.Euler(landingRotation), "Bradley_Guards_Event");
                component.bounds = new OBB(transform.position, transform.rotation, new Bounds(BoundsPosition, BoundsSize));
                component.chinookPosition = chinookPosition;
                component.guardConfig = GuardConfig;
                
                component.guardAmount = GuardAmount;
                component.displayName = DisplayName;
                component.enableAutoExtinguish = enableAutoExtinguish;
                component.enableAutoUnlock = enableAutoUnlock;
                component.CreateLandingZone();
                component.DisplayInfo();
                
                yield return CoroutineEx.waitForEndOfFrame;
            }
        }
        
        private class GuardConfig
        {
            public string Name;
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
            public bool DisableRadio;
            public bool CanRunAwayWater;
            public bool CanSleep;
            public float Speed;
            public int AreaMask;
            public int AgentTypeID;
            public float MemoryDuration;
            public List<WearEntry> WearItems;
            public List<BeltEntry> BeltItems; 

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
                        WearEntry wearEntry = new WearEntry
                        {
                            ShortName = item.info.shortname,
                            SkinID = item.skin
                        };
                        
                        items.Add(wearEntry);
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
                    ["BeltItems"] = new JArray { BeltItems.Select(x => new JObject { ["ShortName"] = x.ShortName, ["SkinID"] = x.SkinID, ["Amount"] = x.Amount, ["Ammo"] = x.Ammo, ["Mods"] = new JArray(x.Mods) }) },
                    ["Kit"] = Kit,
                    ["Health"] = Health,
                    ["RoamRange"] = RoamRange,
                    ["ChaseRange"] = ChaseRange,
                    ["SenseRange"] = SenseRange,
                    ["ListenRange"] = SenseRange / 2,
                    ["AttackRangeMultiplier"] = AttackRangeMultiplier,
                    ["CheckVisionCone"] = CheckVisionCone,
                    ["VisionCone"] = VisionCone,
                    ["DamageScale"] = DamageScale,
                    ["TurretDamageScale"] = TurretDamageScale,
                    ["AimConeScale"] = AimConeScale,
                    ["DisableRadio"] = DisableRadio,
                    ["CanRunAwayWater"] = CanRunAwayWater,
                    ["CanSleep"] = CanSleep,
                    ["Speed"] = Speed,
                    ["AreaMask"] = AreaMask,
                    ["AgentTypeID"] = AgentTypeID,
                    ["MemoryDuration"] = MemoryDuration,
                    ["States"] = new JArray
                    {
                        new HashSet<string> { "RoamState", "ChaseState", "CombatState", "RaidState" }
                    }
                };
            }
        }

        #endregion
        
        #region Oxide Hooks

        private void OnServerInitialized()
        {
            CustomProtection.Initialize();
            EntitiesLookup.Initialize();
            
            PerformSetupRoutine();
            
            if (string.IsNullOrEmpty(_configData.CommandName))
                return;
            
            cmd.AddChatCommand(_configData.CommandName, this, nameof(EventCommands));
        }

        private void Init()
        {
            permission.RegisterPermission(PERM_USE, this);
            
            Instance = this;
            
            LoadData();
        }

        private void Unload()
        {
            try
            {
                DestroySetupRoutine();
                BradleyGuardsEvent.OnUnload();
            }
            finally
            {
                CustomProtection.OnUnload();
                EntitiesLookup.OnUnload();
                Instance = null;
            }
        }
        
        private void OnEntityKill(ScientistNPC npc)
        {
            EntitiesLookup.FindEventByEntity(npc)
                ?.OnGuardDeath(npc, null);
        }

        private void OnEntitySpawned(BradleyAPC bradley)
        {
            if (bradley == null || bradley.IsDestroyed)
                return;
            
            Vector3 position = bradley.transform.position;
            if (position == Vector3.zero)
                return;
            
            BradleyGuardsEvent component = BradleyGuardsEvent.GetClosest(position);
            if (component == null)
                return;
            
            component.ResetEvent();
            
            bradley.maxCratesToSpawn = _configData.CrateSpawnAmount;
            bradley._maxHealth = _configData.BradleyHealth;
            bradley.InitializeHealth(_configData.BradleyHealth, _configData.BradleyHealth); 
        }

        private void OnEntityDeath(BradleyAPC bradley, HitInfo info)
        {
            if (bradley == null || bradley.IsDestroyed || info?.InitiatorPlayer == null)
                return;
            
            Vector3 position = bradley.transform.position;
            if (position == Vector3.zero)
                return;
            
            BradleyGuardsEvent component = BradleyGuardsEvent.GetClosest(position);
            if (component == null || component.IsStarted()) 
                return;
            
            if (!NpcSpawn.IsReady())
            {
                PrintWarning("Missing dependency [NpcSpawn v2.4.8] this can be found over at codefling.com thanks to KpucTaJl");
                return;
            }

            component.StartEvent(position);
        }
        
        private void OnEntityDeath(ScientistNPC npc, HitInfo hitInfo)
        {
            EntitiesLookup.FindEventByEntity(npc)
                ?.OnGuardDeath(npc, hitInfo?.InitiatorPlayer);
        }
        
        private void OnEntityDismounted(BaseMountable mountable, ScientistNPC scientist)
        {
            CH47HelicopterAIController chinook = mountable.GetParentEntity() as CH47HelicopterAIController;
            if (chinook == null || chinook.OwnerID != 111999 || chinook.IsDestroyed)
                return;
            
            scientist.Brain.Navigator.ForceToGround();
            scientist.Brain.Navigator.SetDestination(scientist.finalDestination);
            
            if (chinook.AnyMounted())
                return;
            
            ForceChinookLeave(chinook);
        }

        #endregion

        #region Event

        private void PerformSetupRoutine()
        {
            DestroySetupRoutine();
            
            _setupRoutine = ServerMgr.Instance.StartCoroutine(SetupEventsRoutine());
        }

        private void DestroySetupRoutine()
        {
            if (_setupRoutine != null)
                ServerMgr.Instance.StopCoroutine(_setupRoutine);
            
            _setupRoutine = null;
        }
        
        private IEnumerator SetupEventsRoutine()
        {
            yield return null;
            Interface.Oxide.LogDebug("Initializing (Bradley Guard Events) in ({0})s.", INITIALIZE_DELAY);
            yield return CoroutineEx.waitForSeconds(INITIALIZE_DELAY);
            
            using (new DebugStopwatch("Finished initializing (Bradley Guard Events) in ({0})ms."))
            {
                foreach (MonumentInfo monumentInfo in TerrainMeta.Path.Monuments)
                {
                    if (monumentInfo.name.IsNullOrEmpty())
                        continue;
                    
                    EventEntry monumentEntry = _storedData.FindEntryByName(monumentInfo.name);
                    if (monumentEntry == null || !monumentEntry.EnabledEvent)
                        continue;
                    
                    yield return monumentEntry.Create(monumentInfo.transform, _configData.EnableAutoExtinguish, _configData.EnableAutoUnlock);
                }
            }
            
            _setupRoutine = null;
            
            yield break;
        }
        
        private class BradleyGuardsEvent : FacepunchBehaviour
        {
            public static readonly List<BradleyGuardsEvent> EventComponents = new();
            
            private List<BaseEntity> _spawnInstances = new();
            private CH47LandingZone _eventLandingZone;
            private CurrentState _eventState;
            private Vector3 _eventPosition;
            private GameObject _go;
            
            public GuardConfig guardConfig;
            public Vector3 chinookPosition;
            public string displayName;
            public int guardAmount;
            public bool enableAutoExtinguish;
            public bool enableAutoUnlock;
            public OBB bounds;
            public BasePlayer winningPlayer;
            
            public static BradleyGuardsEvent GetClosest(Vector3 position)
            {
                for (int i = 0; i < EventComponents.Count; i++)
                {
                    BradleyGuardsEvent component = EventComponents[i];
                    if (component.bounds.Contains(position)) 
                        return component;
                }

                return (BradleyGuardsEvent)null;
            }
            
            public static void OnUnload()
            {
                if (Rust.Application.isQuitting)
                    return;
                
                for (int i = EventComponents.Count - 1; i >= 0; i--)
                    EventComponents[i]?.DestroyMe();
                
                EventComponents.Clear();
            }
            
            #region Unity
            
            public void Awake()
            {
                _go = gameObject;
                
                BradleyGuardsEvent.EventComponents.Add(this);
            }

            public void OnDestroy()
            {
                BradleyGuardsEvent.EventComponents.Remove(this);   
            }

            public void DestroyMe()
            {
                ClearGuards();
                UnityEngine.GameObject.Destroy(_go);
            }

            #endregion
            
            #region Event Management
            
            public bool IsStarted() => _eventState == CurrentState.Started;

            public void StartEvent(Vector3 deathPosition)
            {
                if (IsStarted())
                    return;
                
                winningPlayer = null;
                
                _eventPosition = deathPosition;
                _eventState = CurrentState.Started;
                
                SpawnChinook();
                RemoveDamage();
                
                Instance?.HookResubscribe();
                Instance?.MessagePlayers(MessageKeys.EventStart, displayName);
            }

            private void StopEvent()
            {
                _eventState = CurrentState.Waiting;
                
                Instance?.HookUnsubscribe();
                Instance?.MessagePlayers(MessageKeys.EventEnded, displayName);
            }

            public void ResetEvent()
            {
                ClearGuards();

                _eventState = CurrentState.Waiting;
            }

            public void CheckEvent()
            {
                if (enableAutoExtinguish)
                    RemoveFlames();
                
                if (enableAutoUnlock)
                    UnlockCrates();

                if (winningPlayer != null)
                    Interface.CallHook("OnBradleyGuardsEventEnded", winningPlayer);
                
                StopEvent();
            }

            private bool HasGuards() => _spawnInstances.Count > 0;

            private void UnlockCrates()
            {
                List<LockedByEntCrate> entities = Facepunch.Pool.Get<List<LockedByEntCrate>>();

                try
                {
                    Vis.Entities(_eventPosition, 25f, entities);

                    foreach (LockedByEntCrate entCrate in entities)
                    {
                        if (!entCrate.IsValid() || entCrate.IsDestroyed) 
                            continue;
                    
                        entCrate.SetLocked(false);
                        
                        if (entCrate.lockingEnt == null) 
                            continue;
                        
                        BaseEntity entity = entCrate.lockingEnt.GetComponent<BaseEntity>();
                        if (entity != null && !entity.IsDestroyed)
                            entity.Kill();
                    }
                }
                catch (Exception e)
                {
                    //
                }

                Facepunch.Pool.FreeUnmanaged<LockedByEntCrate>(ref entities);
            }

            private void RemoveFlames()
            {
                List<FireBall> entities = Facepunch.Pool.Get<List<FireBall>>();

                try
                {
                    Vis.Entities<FireBall>(_eventPosition, 25f, entities);
                    
                    foreach (FireBall fireball in entities)
                    {
                        if (fireball.IsValid() && !fireball.IsDestroyed)
                            fireball.Extinguish();
                    }
                }
                catch (Exception e)
                {
                    //
                }

                Facepunch.Pool.FreeUnmanaged<FireBall>(ref entities);
            }
            
            private void RemoveDamage()
            {
                List<FireBall> entities = Facepunch.Pool.Get<List<FireBall>>();

                try
                {
                    Vis.Entities<FireBall>(_eventPosition, 25f, entities);

                    foreach (FireBall fireball in entities)
                    {
                        if (fireball.IsValid() && !fireball.IsDestroyed)
                            fireball.ignoreNPC = true;
                    }
                }
                catch (Exception e)
                {
                    //
                }

                Facepunch.Pool.FreeUnmanaged<FireBall>(ref entities);
            }

            #endregion

            #region Chinook

            private void SpawnChinook()
            {
                CH47HelicopterAIController component = GameManager.server.CreateEntity("assets/prefabs/npc/ch47/ch47scientists.entity.prefab")?.GetComponent<CH47HelicopterAIController>();
                component.transform.position = chinookPosition;
                component.SetLandingTarget(_eventLandingZone.transform.position);
                component.OwnerID = 111999;
                component.Spawn();
                component.SetMinHoverHeight(0.0f);
                component.Invoke(new Action(() => SpawnGuards(component, guardAmount)), 0.25f);
                component.Invoke(new Action(() => DropSmokeGrenade(_eventLandingZone.transform.position)), 1f);
                
                CustomProtection.ModifyProtection(component);
            }
            
            private void DropSmokeGrenade(Vector3 position)
            {
                SmokeGrenade component = GameManager.server.CreateEntity("assets/prefabs/tools/smoke grenade/grenade.smoke.deployed.prefab", position, Quaternion.identity).GetComponent<SmokeGrenade>();
                component.smokeDuration = 45f;
                component.Spawn();
            }

            #endregion

            #region Landing

            public void CreateLandingZone()
            {
                _eventLandingZone = gameObject.AddComponent<CH47LandingZone>();
                _eventLandingZone.enabled = true;
            }

            #endregion

            #region Guard
            
            private void SpawnGuards(CH47HelicopterAIController chinook, int numToSpawn)
            {
                int num = Mathf.Clamp(numToSpawn, 1, chinook.mountPoints.Count);
                
                for (int i = 0; i < 2; i++)
                    SpawnGuard(chinook, chinook.transform.position + chinook.transform.forward * 10f);

                num -= 2;
                
                if (num <= 0)
                    return;
                
                for (int i = 0; i < num; i++)
                    SpawnGuard(chinook, chinook.transform.position - chinook.transform.forward * 15f);
            }

            private void SpawnGuard(CH47HelicopterAIController chinook, Vector3 position)
            {
                Vector3 destination = _eventPosition.GetPointAround(2f);
                guardConfig.Parsed["HomePosition"] = destination.ToString();
                
                ScientistNPC scientist = (ScientistNPC)Instance?.NpcSpawn.Call("SpawnNpc", position, guardConfig.Parsed);
                if (scientist == null || scientist.IsDestroyed)
                    return;
                
                scientist.finalDestination = destination;
                CachedGuardAdd(scientist);
                chinook.AttemptMount((BasePlayer) scientist, false);
            }
            
            public void ClearGuards()
            {
                for (int i = _spawnInstances.Count - 1; i >= 0; i--)
                {
                    BaseEntity entity = _spawnInstances[i];
                    if (entity != null && !entity.IsDestroyed)
                        entity.Kill();
                }
                
                _spawnInstances.Clear();
            }
            
            #endregion
            
            #region Oxide Hooks
            
            public void OnGuardDeath(ScientistNPC npc, BasePlayer player)
            {
                CachedGuardRemove(npc);
                
                if (HasGuards())
                    return;
                
                winningPlayer = player;
                CheckEvent();
            }
            
            #endregion
            
            #region Cache Guard Entity

            private void CachedGuardAdd(BaseEntity entity)
            {
                _spawnInstances.Add(entity);
                
                EntitiesLookup.CreateEntity(entity, this);
            }

            private void CachedGuardRemove(BaseEntity entity)
            {
                _spawnInstances.Remove(entity);
                
                EntitiesLookup.RemoveEntity(entity);
            }

            #endregion
            
            #region Debug Info

            public void DisplayInfo()
            {
                List<Connection> connections = Facepunch.Pool.Get<List<Connection>>();
                
                try
                {
                    connections.AddRange(Net.sv.connections.Where(x => x.connected && x.authLevel == 2));
                    
                    Utils.DText(connections, transform.position, "Landing Position", Color.magenta, 30f);
                    Utils.DText(connections, chinookPosition, "Chinook Position", Color.green, 30f);
                    Utils.DLine(connections, chinookPosition, _eventLandingZone.transform.position, Color.yellow, 30f);
                    Utils.DText(connections, bounds.position, "Bounds Position", Color.cyan, 30f);
                    Utils.DCube(connections, bounds.position, bounds.rotation, bounds.extents, Color.blue, 30f);
                }
                catch (Exception e)
                {
                    //
                }
                
                Facepunch.Pool.FreeUnmanaged<Connection>(ref connections);
            }

            #endregion
        }

        private void ForceChinookLeave(CH47HelicopterAIController chinook)
        {
            chinook.ClearLandingTarget();
            chinook.Invoke(() =>
            {
                chinook.EnableFacingOverride(true);
                chinook.SetMinHoverHeight(60f);
                chinook.SetMoveTarget(Vector3.right * 6000f);
                chinook.Invoke(new Action(chinook.KillMessage), 60f);

                if (chinook.TryGetComponent<CH47AIBrain>(out CH47AIBrain aiBrain)) 
                    aiBrain.SwitchToState(AIState.Egress, aiBrain.currentStateContainerID);
            }, 5f);
        }

        private enum CurrentState
        {
            Waiting, 
            Started
        }
        
        #endregion
        
        #region Rust Edit

        private object OnNpcRustEdit(ScientistNPC npc)
        {
            return EntitiesLookup.FindEventByEntity(npc) != null ? (object)true : null;
        }

        #endregion
        
        #region Hook Subscribe
        
        private void HookUnsubscribe() => Unsubscribe(nameof(OnEntityDismounted));
        
        private void HookResubscribe() => Subscribe(nameof(OnEntityDismounted));

        #endregion
        
        #region Custom Protection

        private static class CustomProtection
        {
            private static ProtectionProperties ProtectionInstance;
            
            public static void Initialize()
            {
                ProtectionInstance = ScriptableObject.CreateInstance<ProtectionProperties>();
                ProtectionInstance.name = "Bradley_Guards_Protection";
                ProtectionInstance.Add(1);
            }

            public static void OnUnload()
            {
                if (ProtectionInstance != null) 
                    UnityEngine.ScriptableObject.Destroy(ProtectionInstance);
                
                ProtectionInstance = null;
            }

            public static void ModifyProtection(BaseCombatEntity combatEntity)
            {
                if (combatEntity != null && !combatEntity.IsDestroyed) 
                    combatEntity.baseProtection = ProtectionInstance;
            }
        }

        #endregion
        
        #region Entities Lookup

        private static class EntitiesLookup
        {
            public static Dictionary<BaseEntity, BradleyGuardsEvent> Entities;

            public static BradleyGuardsEvent FindEventByEntity(BaseEntity entity)
            {
                return entity != null && Entities.TryGetValue(entity, out BradleyGuardsEvent component) ? component : null;
            }

            public static void Initialize()
            {
                Entities = new Dictionary<BaseEntity, BradleyGuardsEvent>();
            }
            
            public static void OnUnload()
            {
                Entities.Clear();
                Entities = null;
            }

            public static void CreateEntity(BaseEntity entity, BradleyGuardsEvent component)
            {
                if (Entities != null)
                    Entities.Add(entity, component);
            }

            public static void RemoveEntity(BaseEntity entity)
            {
                if (Entities != null)
                    Entities.Remove(entity);
            }
        }

        #endregion
        
        #region Chat Command
        
        private void EventCommands(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM_USE))
            {
                MessagePlayer(player, MessageKeys.NoPermission);
                return;
            }
            
            if (args.Length < 2)
            {
                EventHelpText(player);
                return;
            }
            
            if (!_storedData.BradleyEventEntries.TryGetValue(args[0], out EventEntry eventEntry))
            {
                MessagePlayer(player, MessageKeys.EventNotFound);
                return;
            }

            string option = args[1];
            if (option.Equals("enable"))
            {
                if (args.Length != 3)
                {
                    EventHelpText(player);
                    return;
                }
                
                bool enabled;
                if (bool.TryParse(args[2], out enabled))
                {
                    MessagePlayer(player, MessageKeys.InvalidBooleanValue);
                    return;
                }
                
                eventEntry.EnabledEvent = enabled;
                
                SaveData();
                MessagePlayer(player, MessageKeys.EventUpdated);
                return;
            }
            
            if (option.Equals("display"))
            {
                if (args.Length != 3)
                {
                    EventHelpText(player);
                    return;
                }
                
                string displayName = string.Join(" ", args.Skip(2));
                if (displayName.IsNullOrEmpty())
                {
                    MessagePlayer(player, MessageKeys.DisplayNameEmpty);
                    return;
                }
                
                eventEntry.DisplayName = displayName;
                
                SaveData();
                MessagePlayer(player, MessageKeys.EventUpdated);
                return;
            }
            
            if (option.Equals("amount"))
            {
                if (args.Length != 3)
                {
                    EventHelpText(player);
                    return;
                }

                int amount;
                if (!int.TryParse(args[2], out amount) || (amount < 2 || amount > 10))
                {
                    MessagePlayer(player, MessageKeys.InvalidGuardAmount);
                    return;
                }

                eventEntry.GuardAmount = amount;
                
                SaveData();
                MessagePlayer(player, MessageKeys.EventUpdated);
                return;
            }
            
            if (option.Equals("loadout"))
            {
                if (player.inventory == null)
                    return;
                
                eventEntry.GuardConfig.BeltItems = GuardConfig.BeltEntry.SaveItems(player.inventory.containerBelt);
                eventEntry.GuardConfig.WearItems = GuardConfig.WearEntry.SaveItems(player.inventory.containerWear);
                eventEntry.GuardConfig.CacheConfig();
                SaveData();
                MessagePlayer(player, MessageKeys.EventUpdated);
                return;
            }
            
            EventHelpText(player);
        }

        private void EventHelpText(BasePlayer player)
        {
            StringBuilder sb = Facepunch.Pool.Get<StringBuilder>();
            
            try
            {
                sb.Clear();
                sb.AppendFormat(lang.GetMessage(MessageKeys.Prefix, this, player.UserIDString))
                    .AppendFormat(lang.GetMessage(MessageKeys.HelpEventEnable, this, player.UserIDString), _configData.CommandName)
                    .AppendFormat(lang.GetMessage(MessageKeys.HelpEventName, this, player.UserIDString), _configData.CommandName)
                    .AppendFormat(lang.GetMessage(MessageKeys.HelpGuardAmount, this, player.UserIDString), _configData.CommandName)
                    .AppendFormat(lang.GetMessage(MessageKeys.HelpGuardLoadout, this, player.UserIDString), _configData.CommandName);
            
                player.ChatMessage(sb.ToString());
            }
            finally
            {
                sb.Clear();
                Facepunch.Pool.FreeUnmanaged(ref sb);
            }
        }

        #endregion

        #region Debug Stopwatch

        private class DebugStopwatch : IDisposable
        {
            private Stopwatch _stopwatch;
            private string _format;

            public DebugStopwatch(string format)
            {
                _format = format;

                _stopwatch = new Stopwatch();
                _stopwatch.Start();
            }

            public void Dispose()
            {
                _stopwatch.Stop();

                Interface.Oxide.LogDebug(_format, _stopwatch.ElapsedMilliseconds);
                
                _stopwatch = null;
            }
        }

        #endregion

        #region Harmony Patches

        [AutoPatch]
        [HarmonyPatch(typeof(CH47HelicopterAIController), "CheckSpawnScientists")]
        public static class Prevent_CH47_Scientists_Spawning_Patch
        {
            public static bool Prefix(CH47HelicopterAIController __instance)
            {
                return !(__instance != null && __instance.OwnerID == 111999);
            }
        }

        #endregion
    }
}

namespace Oxide.Plugins.BradleyGuardsExtensionMethods
{
    public static class Utils
    {
        public static T CreateObjectWithComponent<T>(Vector3 position, Quaternion rotation, string name) where T : MonoBehaviour
        {
            return new GameObject(name)
            {
                layer = (int)Layer.Reserved1,
                transform =
                {
                    position = position,
                    rotation = rotation
                }
            }.AddComponent<T>();
        }
        
        public static void Segments(List<Connection> connections, Vector3 origin, Vector3 target, Color color, float duration)
        {
            Vector3 delta = target - origin;
            float distance = delta.magnitude;
            Vector3 direction = delta.normalized;

            float segmentLength = 10f;
            int numSegments = Mathf.CeilToInt(distance / segmentLength);

            for (int i = 0; i < numSegments; i++)
            {
                float length = segmentLength;
                if (i == numSegments - 1 && distance % segmentLength != 0)
                    length = distance % segmentLength;

                Vector3 start = origin + i * segmentLength * direction;
                Vector3 end = start + length * direction;
                
                Utils.DLine(connections, start, end, color, duration);
            }
        }
        
        public static void DLine(List<Connection> connections, Vector3 start, Vector3 end, Color color, float duration)
        {
            if (connections?.Count > 0)     
                ConsoleNetwork.SendClientCommand(connections, "ddraw.line", duration, color, start, end);
        }
        
        public static void DText(List<Connection> connections, Vector3 origin, string text, Color color, float duration)
        {
            if (connections?.Count > 0)
                ConsoleNetwork.SendClientCommand(connections, "ddraw.text", duration, color, origin, text);
        }
        
        public static void DCube(List<Connection> connections, Vector3 center, Quaternion rotation, Vector3 extents, Color color, float duration)
        {
            Vector3 forwardUpperLeft = center + rotation * extents.WithX(-extents.x);
            Vector3 forwardUpperRight = center + rotation * extents;
            Vector3 forwardLowerLeft = center + rotation * extents.WithX(-extents.x).WithY(-extents.y);
            Vector3 forwardLowerRight = center + rotation * extents.WithY(-extents.y);
            Vector3 backLowerRight = center + rotation * -extents.WithX(-extents.x);
            Vector3 backLowerLeft = center + rotation * -extents;
            Vector3 backUpperRight = center + rotation * -extents.WithX(-extents.x).WithY(-extents.y);
            Vector3 backUpperLeft = center + rotation * -extents.WithY(-extents.y);
                
            Utils.Segments(connections, forwardUpperLeft, forwardUpperRight, color, duration);
            Utils.Segments(connections, forwardLowerLeft, forwardLowerRight, color, duration);
            Utils.Segments(connections, forwardUpperLeft, forwardLowerLeft, color, duration);
            Utils.Segments(connections, forwardUpperRight, forwardLowerRight, color, duration);

            Utils.Segments(connections, backUpperLeft, backUpperRight, color, duration);
            Utils.Segments(connections, backLowerLeft, backLowerRight, color, duration);
            Utils.Segments(connections, backUpperLeft, backLowerLeft, color, duration);
            Utils.Segments(connections, backUpperRight, backLowerRight, color, duration);

            Utils.Segments(connections, forwardUpperLeft, backUpperLeft, color, duration);
            Utils.Segments(connections, forwardLowerLeft, backLowerLeft, color, duration);
            Utils.Segments(connections, forwardUpperRight, backUpperRight, color, duration);
            Utils.Segments(connections, forwardLowerRight, backLowerRight, color, duration);
        }
    }
    
    public static class ExtensionMethods
    {
        public static bool IsNullOrEmpty(this string value) => string.IsNullOrEmpty(value);
        
        public static bool IsReady(this Plugin plugin) => plugin != null && plugin.IsLoaded;
        
        public static Vector3 GetPointAround(this Vector3 position, float radius)
        {
            float angle = UnityEngine.Random.value * 360f;
            
            Vector3 pointAround = position;
            pointAround.x = position.x + radius * Mathf.Sin(angle * Mathf.Deg2Rad);
            pointAround.z = position.z + radius * Mathf.Cos(angle * Mathf.Deg2Rad);
            pointAround.y = position.y;
            pointAround.y = TerrainMeta.HeightMap.GetHeight(pointAround);
            return pointAround;
        }
    }
}
