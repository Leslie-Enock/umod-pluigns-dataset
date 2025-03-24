#define _DEBUG
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Oxide.Core.Libraries.Covalence;
using System.Text;
using Facepunch;

//
// rev 1.0.0
//    Initial release
// rev 1.0.1
//    Fix plugin Info not properly formatted
// rev 1.0.2
//    Fix plugin Info not properly formatted
//    cleanup messsage log
// rev 1.0.4
//    bug fix relatuve to player quarry
// rev 1.0.5
//    bug fix relatuve to player quarry
// rev 1.0.6
//    Unlock quarry when player / team / clan goes offline for too long
//    bug fix relative to player quarry
// rev 1.0.7
//    bug fix relative to Clear quarry lock when going offline
// rev 1.0.8
//    log quarry location
// rev 1.0.9
//    Bug fix related to cooldown and /quarry command
// rev 1.0.10
//    Bug fix related to cooldown
// rev 1.0.11
//    separate permission for quarry and pumpjack usage 
//    fix Lang for "MiningQuarry" and "Pumpjack" not translating for broadcast
//    add loot qty to message when looting
// rev 1.0.11
//    correction to lang file
// rev 1.0.15
//    bug fix where some parameter affected player deployed quarrys
//    Option to support clan table 


namespace Oxide.Plugins
{
    [Info("Public Quarry Lock", "Lorenzo", "1.0.29")]
    [Description("Lock public Quarry and pumpjack to a player/team/clan when it run")]
    class PublicQuarryLock : CovalencePlugin
    {
        [PluginReference] private Plugin Clans;
        [PluginReference] private Plugin ZoneManager;
        [PluginReference] private Plugin DynamicPVP;
        [PluginReference] private Plugin Notify;

        #region Variables

        private static PublicQuarryLock _instance =null;
        private static DiscordComponent _discord = null;

        private const string PREFAB_ITEM_DROP = "assets/prefabs/misc/item drop/item_drop.prefab";
        private readonly Vector3 DropVect = new Vector3(0f, 1f, 2f);

        string TraceFile = "Trace";     // file name in log directory

        private const string ReadableNameQuarry = "Mining Quarry";
        private const string ReadableNamePump = "Pump Jack";

        private const uint FuelStoragePumpID  = 4260630588;
        private const uint OutputHopperPumpID = 70163214;

        private const uint FuelStorageQuarryID  = 362963830;
        private const uint OutputHopperQuarryID = 875142383;

        private int QuarryDefaultDieselStack = 0;
        private int QuarryDefaultFuelCapacity = 6;
        private int QuarryDefaultHopperSlots = 18;

        private int PumpDefaultDieselStack = 0;
        private int PumpDefaultFuelCapacity = 6;
        private int PumpDefaultHopperSlots = 18;

        private float workPerFuelRestore = -1;
        private float workToAddRestore = -1;
        private float processRateRestore = -1;

#if CARBON
        const char Platform = 'C';
#else
        const char Platform = 'O';
#endif

        #endregion

#region Configuration

    private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Use permission")]
            public bool UsePermission = false;      // use permission or grant access to every players

            [JsonProperty(PropertyName = "PermissionAdmin")]
            public string PermissionAdmin = "publicquarrylock.admin";   // name of permission

            [JsonProperty(PropertyName = "Permission for quarry only")]
            public string PermissionUseQuarry = "publicquarrylock.quarryonly";   // name of permission

            [JsonProperty(PropertyName = "Permission for pumpjack only")]
            public string PermissionUsePump = "publicquarrylock.pumpjackonly";   // name of permission

            [JsonProperty(PropertyName = "Permission bypass global cooldown")]
            public string PermissionBypassCooldown = "publicquarrylock.bypassglobalcooldown";
			
            [JsonProperty(PropertyName = "Allow access to quarry without permission (will not lock)")]
            public bool UseQuarryWithoutPermission = false;			
                                                                                  
            [JsonProperty(PropertyName = "Enable for mining quarry")]
            public bool enableMiningQuarry = true;

            [JsonProperty(PropertyName = "Enable for pump jack")]
            public bool enablePumpJack = true;

            [JsonProperty(PropertyName = "Enable player quarry")]
            public bool enablePlayerQuarry = false;

            [JsonProperty(PropertyName = "Enable player pumpjack")]
            public bool enablePlayerPump = false;

            [JsonProperty(PropertyName = "CoolDown before releasing mining quarry (min)")]
            public float CoolDown = 5;      // 5 minute cooldown after quarry finish and anybody can loot

            [JsonProperty(PropertyName = "Send quarry available message to all")]
            public bool MessageAll = true;

            [JsonProperty(PropertyName = "Enable engine loot after it is started, to add fuel")]
            public bool engineloot = true;

            // In case clans pluginis installed but you dont want to use it with this plugin
            [JsonProperty(PropertyName = "Use Teams")]
            public bool UseTeams = true;

            // In case clans pluginis installed but you dont want to use it with this plugin
            [JsonProperty(PropertyName = "Use Clans plugin")]
            public bool UseClans = false;

            [JsonProperty(PropertyName = "Use clan table")]
            public bool UseClanTable = false;

            // option to use the dynamicPVP plugin to exclude quarrys
            [JsonProperty(PropertyName = "Use DynamicPVP to disable lock in PVP zones")]
            public bool UseDynamicPVP = false;

            [JsonProperty(PropertyName = "CoolDown in min. before a player or team can restart the quarry (0 is disabled)")]
            public float PlayerCoolDown = 30;      // 60 minute cooldown after quarry finish and anybody can loot

            [JsonIgnore]
            public float _PlayerCoolDown = 30;      // internal value

            [JsonProperty(PropertyName = "Usage cooldown to all quarry")]
            public bool CoolDownGlobal = true;      // 60 minute cooldown after quarry finish and anybody can loot
		
            [JsonProperty(PropertyName = "Maximum stack size for diesel engine (-1 to disable function)")]
            public int DieselFuelMaxStackSize = -1;

            [JsonProperty(PropertyName = "Number of slots for diesel storage (-1 to disable function)")]
            public int FuelSlots = -1;

            [JsonProperty(PropertyName = "Number of slots for output storage (-1 to disable function)")]
            public int HopperSlots = -1;

            [JsonProperty(PropertyName = "Time per barrel of diesel in second (-1 to disable function, default time 125 sec)")]
            public int TimePerBarrel = -1;

            [JsonProperty(PropertyName = "quarry chat command")]
            public string  quarryquerry = "quarry";

            [JsonProperty(PropertyName = "Quarry clear status")]
            public string quarryclearstatus = "quarryclear";

            [JsonProperty(PropertyName = "Quarry stop command")]
            public string quarrystopcommand = "quarrystop";

            [JsonProperty(PropertyName = "Empty the output hopper when quarry/pumpjack start")]
            public bool FlushOutputHopper = false;

            [JsonProperty(PropertyName = "Clear quarry lock after all player from team/clan disconnect")]
            public bool ClearQuarryLockOnAllTeamDisconnect = false;

            [JsonProperty(PropertyName = "Clear quarry lock after player owner disconnect")]
            public bool ClearQuarryLockOnPlayerOwnerDisconnect = false;

            [JsonProperty(PropertyName = "Time after all player disconnect before quarry clear (minutes)")]
            public long CooldownQuarryLockOnDisconnect = 10;    // default 10 minutes

            [JsonProperty(PropertyName = "Items in report list", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] ReportItems = {"stones", "metal.ore", "metal.fragments", "hq.metal.ore", "metal.refined", "sulfur.ore", "sulfur", "lowgradefuel", "crude.oil"};

            [JsonProperty("Use Discord hook")]
            public bool Discordena = false;

            [JsonProperty("Use Discord timestamp")]
            public bool UseDiscordTimestamp = true;

            [JsonProperty("Discord hook url")]
            public string DiscordHookUrl = "";

            [JsonProperty(PropertyName = "Use Notify plugin")]
            public bool useNotify = false;

            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;

            [JsonProperty(PropertyName = "Log to file")]
            public bool LogToFile = false;			
        };


        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }

            if  (_config.FuelSlots > 6)  _config.FuelSlots = 6;
            if  (_config.FuelSlots == 0)  _config.FuelSlots = -1;

            if (_config.HopperSlots > 48) _config.HopperSlots = 48;

            _config._PlayerCoolDown = Math.Max(_config.PlayerCoolDown, _config.CoolDown);

            if (_config.ClearQuarryLockOnAllTeamDisconnect == true && _config.ClearQuarryLockOnPlayerOwnerDisconnect == true)
            {
                PrintError("Both ClearQuarryLockOnAllTeamDisconnect and ClearQuarryLockOnPlayerOwnerDisconnect are true. Using team mode by default");
                _config.ClearQuarryLockOnPlayerOwnerDisconnect = false;
            }

            if (_config.CooldownQuarryLockOnDisconnect < 0) _config.CooldownQuarryLockOnDisconnect = 0;
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

#endregion Configuration


        // ############################################################################################
#region UserInfo

        private Dictionary<ulong, QuarryInfo> Quarrys = new Dictionary<ulong, QuarryInfo>();

        // dict to link all the diff component entity to the right quarry
        private Dictionary<ulong, ulong> Associate = new Dictionary<ulong, ulong>();

        enum miningtype { none, miningquarry, miningpumpjack };

        private class QuarryInfo
        {
            public ulong playerid;
            public string displayName;
            public string ShortPrefabName;
            [JsonIgnore]
            public string readablename;
            [JsonIgnore]
            public miningtype type;

            [JsonIgnore]
            public double FuelTimeRemaining;
            //[JsonIgnore]
            public bool state;      // capture or not by playerid
            public bool EnableLock;      // capture or not by playerid

            public DateTime checkfueltime;
            public DateTime stoptime;
            public string Name;
            public float processRate = 5f;    // default value
            public Dictionary<ulong, CooldownTime> PlayerCooldown;

            [JsonIgnore]
            public DateTime PlayerDisconnectTime;
            [JsonIgnore]
            public int CountTick;

            [JsonIgnore]
            public MiningQuarry Quarry;

            [JsonIgnore]
            public Timer timerQuarryStopped = null;

            public QuarryInfo()
            {
                playerid = 0;
                displayName = "nobody";
                readablename = "none";
                type = miningtype.none;
                FuelTimeRemaining = 0f;
                state = false;
                EnableLock = false;
                PlayerDisconnectTime = DateTime.MaxValue;

                checkfueltime = DateTime.MinValue;
                stoptime  = DateTime.MinValue;
                Name = string.Empty;
                PlayerCooldown = new Dictionary<ulong, CooldownTime>();
                CountTick = 1;
                Quarry = null;
            }
        }

        public class CooldownTime
        {
            public bool IsOnline;
            public bool IgnoreOnline;
            public DateTime Time;
            public CooldownTime() { IsOnline = true; Time = DateTime.MinValue; IgnoreOnline = false; }
        }

        private void LoadData()
        {
            try {
                Quarrys = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, QuarryInfo>>(Name);
                if (Quarrys == null) throw new Exception();
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
                Quarrys = new Dictionary<ulong, QuarryInfo>();
                PrintWarning($"Data file corrupted or incompatible. Reinitialising data file ");
                SaveData();
            }
        }
        private void SaveData() { Interface.Oxide.DataFileSystem.WriteObject(Name, Quarrys); }

#endregion UserInfo

        // ############################################################################################
#region Localization

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["QuarryOff"]      = "<color=#FF1500>- {1} at {0} just turn off !</color>",
                ["QuarryCoolDown"] = "<color=#FF1500>- You have {0} min. to get the ore, before quarry unlock to all</color>",
                ["QuarryInUse"]    = "<color=#FFA500>{2} at {1} currently in use by {0}</color>",
                ["QuarryStartEngine"] = "{1} started for {0,2:F0} minutes",
                ["QuarryStartEngineHM"] = "{2} started for {0,2:F0} hour, {1,2:F0} minutes",
                ["QuarryStartEngineNolock"] = "{0} started in Dynamic PVP zone (not locked)",
                ["QuarryStarted"]  = "{2} at {1}, started by {0}. loot is protected",
                ["QuarryStoped"]   = "{2} at {1}, stopped and is available to all",
                ["QuarryStoped_B"] = "{2} at {1}, is available to all",
                ["QuarryFuel"]     = "<color=#FFA500>{1} currently running with {0} fuel in storage</color>",
                ["QuarryPlCooldown"] = "{1} player cooldown will expire in {0} min.",
                ["QuarryAvailableIn2"] = "{2} at {0},used by {3}, will be available in {1,2:F0} min.",
                ["QuarryClearStatus"] = "Clearing the status of mining quarry and pump jack",
                [ReadableNameQuarry] = "mining quarry",
                [ReadableNamePump] = "pump jack",
                ["QuarryClearOffline"] = "Quarry {0}, cleared because team/clan player are offline",
                ["QuarryAccessDenied"] = "Access denied. you do not have permission",
                ["QuarryInfoCmd"] = "Quarry info.",
                ["NoQuarryFoundNear"] = "No Quarry found near to apply command.",
                ["MoveFuelToOutput"] = "Stop quarry, Moving fuel to output hopper.",
                

            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["QuarryOff"]      = "<color=#FF1500>- {1} a {0} viens de s'arreter !</color>",
                ["QuarryCoolDown"] = "<color=#FF1500>- Vous avez {0} min. pour récuperer le butin avant que l'acces débloque</color>",
                ["QuarryInUse"]    = "<color=#FFA500>{2} a {1} est utilisé par {0}</color>",
                ["QuarryStartEngine"] = "{1} démarré pour {0,2:F0} minutes",
                ["QuarryStartEngineHM"] = "{1} démarré pour {0,2:F0} heure, {1,2:F0} minutes",
                ["QuarryStartEngineNolock"] = "{0} démarré en zone PVP Dynamique (non protégé)",
                ["QuarryStarted"]  = "{2} a {1}, est démarré par {0}. butin protégé",
                ["QuarryStoped"]   = "Arret de {2} a {1}, disponible pour tous",
                ["QuarryStoped_B"] = "{2} a {1}, disponible pour tous",
                ["QuarryFuel"]     = "<color=#FFA500>{1} démarré avec {0} fuel dans le reservoir</color>",
                ["QuarryPlCooldown"] = "Le delai d'utilisation de {1} par joueur expire dans {0} min.",
                ["QuarryAvailableIn2"] = "{2} a {0},utilisé par {3}, sera disponible dans {1,2:F0} min.",
                ["QuarryClearStatus"] = "Efface le status des carrière minière et chevalet de pompage",
                [ReadableNameQuarry] = "carrière minière",
                [ReadableNamePump] = "chevalet de pompage",
                ["QuarryClearOffline"] = "Carrière minière {0}, libéré car l'équipe/clan des joueurs est hors-ligne",
                ["QuarryAccessDenied"] = "Access bloqué. vous n'avez pas la permission",
                ["QuarryInfoCmd"] = "Info des carrières minière",
                ["NoQuarryFoundNear"] = "Pas de carrières minière trouvé a proximité, pour cette commande",
                ["MoveFuelToOutput"] = "Arret de la carrières minière, transfer du carburant vers le contenant de sortie.",
            }, this, "fr");
        }
        #endregion Localization

        #region Hooks 

        private void Init()
        {
            StorageContainer fuelstorage;
            StorageContainer hopper;

            LoadData();

            var staticQuarry = GameManager.server.FindPrefab("assets/bundled/prefabs/static/miningquarry_static.prefab")?.GetComponent<MiningQuarry>();
            if (staticQuarry != null)
            {
                fuelstorage = staticQuarry?.fuelStoragePrefab.prefabToSpawn.GetEntity() as StorageContainer;
                QuarryDefaultDieselStack = fuelstorage?.maxStackSize ?? 0;
                QuarryDefaultFuelCapacity = fuelstorage?.inventorySlots ?? 6;

                hopper = staticQuarry.hopperPrefab.prefabToSpawn.GetEntity() as StorageContainer;
                QuarryDefaultHopperSlots = hopper?.inventorySlots ?? 18;
            }
            
            var staticpumpjack = GameManager.server.FindPrefab("assets/bundled/prefabs/static/pumpjack-static.prefab")?.GetComponent<MiningQuarry>();
            if (staticpumpjack != null)
            {
                fuelstorage = staticpumpjack.fuelStoragePrefab.prefabToSpawn.GetEntity() as StorageContainer;
                PumpDefaultDieselStack = fuelstorage?.maxStackSize ?? 0;
                PumpDefaultFuelCapacity = fuelstorage?.inventorySlots ?? 6;

                hopper = staticpumpjack.hopperPrefab.prefabToSpawn.GetEntity() as StorageContainer;
                PumpDefaultHopperSlots = hopper?.inventorySlots ?? 18;
            }

            _instance = this;

            permission.RegisterPermission(_config.PermissionUseQuarry, this);
            permission.RegisterPermission(_config.PermissionUsePump, this);
            permission.RegisterPermission(_config.PermissionBypassCooldown, this);            

            permission.RegisterPermission(_config.PermissionAdmin, this);

            if (!_config.ClearQuarryLockOnAllTeamDisconnect &&
                !_config.ClearQuarryLockOnPlayerOwnerDisconnect)
            {
                Unsubscribe(nameof(OnPlayerConnected));
                Unsubscribe(nameof(OnPlayerDisconnected));
            }
            Unsubscribe(nameof(OnEntitySpawned));

            AddCovalenceCommand(_config.quarryquerry, nameof(Quarry_info));
            AddCovalenceCommand(_config.quarryclearstatus, nameof(Quarry_clearstatus));
            AddCovalenceCommand(_config.quarrystopcommand, nameof(Mining_stopcommand));
            
        }

        private void Unload()
        {
            foreach (var entquarry in BaseNetworkable.serverEntities)
            {
                var quarry = entquarry as MiningQuarry;
                if (quarry != null)
                {
                    QuarryDetector quarrydetector = quarry.gameObject.GetComponent<QuarryDetector>();
                    if (quarrydetector != null) UnityEngine.Object.Destroy(quarrydetector);

                    QuarryInfo quarryinfo;
                    if (Quarrys.TryGetValue(quarry.net.ID.Value, out quarryinfo))
                    {
                        if (quarryinfo.EnableLock == true)
                        {
                            // restore default value

                            try
                            {
                                StorageContainer fuelstorage = quarry.fuelStoragePrefab.instance.GetComponent<StorageContainer>();
                                if (quarry.ShortPrefabName == "miningquarry_static")
                                {
                                    if (_config.DieselFuelMaxStackSize >= 0) fuelstorage.inventory.maxStackSize = QuarryDefaultDieselStack;
                                    if (_config.FuelSlots > 0) fuelstorage.inventory.capacity = QuarryDefaultFuelCapacity;  // restore default
                                }
                                if (quarry.ShortPrefabName == "pumpjack-static")
                                {
                                    if (_config.DieselFuelMaxStackSize >= 0) fuelstorage.inventory.maxStackSize = PumpDefaultDieselStack;
                                    if (_config.FuelSlots > 0) fuelstorage.inventory.capacity = PumpDefaultFuelCapacity;  // restore default
                                }
                            }
                            catch (Exception ex)
                            {
                                PrintError(ex.Message);
                                PrintError($"Quarry at location {quarry.transform.position} is missing fuel storage container.");
                                quarry.fuelStoragePrefab?.DoSpawn(quarry);
                            }

                            try
                            {
                                StorageContainer hopper = quarry.hopperPrefab.instance.GetComponent<StorageContainer>();
                                if (quarry.ShortPrefabName == "miningquarry_static")
                                {
                                    if (_config.HopperSlots > 0) hopper.inventory.capacity = QuarryDefaultHopperSlots;
                                }
                                if (quarry.ShortPrefabName == "pumpjack-static")
                                {
                                    if (_config.HopperSlots > 0) hopper.inventory.capacity = PumpDefaultHopperSlots;
                                }
                            }
                            catch (Exception ex)
                            {
                                PrintError(ex.Message);
                                PrintError($"Quarry at location {quarry.transform.position} is missing hopper storage container.");
                                quarry.hopperPrefab?.DoSpawn(quarry);
                            }

                            if (_config.TimePerBarrel > 0) quarry.workPerFuel = 1000f;
							if (quarry.pendingWork > quarry.workPerFuel) quarry.pendingWork = quarry.workPerFuel;
                        }

                        if (quarryinfo.timerQuarryStopped != null)
                        {
                            timer.Destroy(ref quarryinfo.timerQuarryStopped);
                        }
                    }
                }
            }

            SaveData();
        
            // clear static
            _instance = null;
            _discord = null;
		    _config = null;
        }

        //
        private void OnServerInitialized(bool initial)
        {
            // setup discord info
            if (_config.Discordena && !string.IsNullOrEmpty(_config.DiscordHookUrl))
            {
                var loader = new GameObject("WebObject");
                _discord = loader.AddComponent<DiscordComponent>().Configure(_config.DiscordHookUrl);
                PrintToLog($"Plugin PublicQuarryLock restarted");
            }

            // this is just in case user think its in second and put a large value 
            if (_config.PlayerCoolDown > 120)
            {
                PrintWarning($"Configuration cooldown period {_config.PlayerCoolDown} minutes");
            }



            if (_config.UseClans && !IspluginLoaded(Clans))
            {
                PrintWarning("Optional Clans plugin not found. Clans disabled");
            }

            if (_config.useNotify && !IspluginLoaded(Notify))
            {
                PrintWarning("Optional Notify plugin not found. Notify disabled");
            }

            timer.Once(1f, () =>
            {
                Subscribe(nameof(OnEntitySpawned));
                // count the MiningQuarry engine on the map
                foreach (var entquarry in BaseNetworkable.serverEntities)
                {
                    var quarry = entquarry as MiningQuarry;
                    if (quarry != null)
                    {
                        OnEntitySpawned(quarry);
                    }
                }

                // Check if the quarry entry exist 
                // If not remove it. This append when server wipe
                List<ulong> removals = Pool.Get<List<ulong>>();
                removals.Clear();
                foreach (KeyValuePair<ulong, QuarryInfo> KVP in Quarrys)
                {
                    var quarry = KVP.Value.Quarry;
                    if (quarry == null)
                    {
                        removals.Add(KVP.Key);
                    }
                }

                if (removals.Count != 0) PrintWarning($"Removing {removals.Count} unused entry from the data file ");

                // cleanup dict entry of unused info
                foreach (ulong removeid in removals)
                {
                    Quarrys.Remove(removeid);
                }
                Pool.FreeUnmanaged(ref removals);
            });
        }

        //OnEntitySpawned execute before OnServerInitialized
        void OnEntitySpawned(MiningQuarry quarry)
        {
            QuarryInfo quarryinfo;
            if (!Quarrys.TryGetValue(quarry.net.ID.Value, out quarryinfo))
            {
                quarryinfo = new QuarryInfo();
                quarryinfo.ShortPrefabName = quarry.ShortPrefabName;
                Quarrys.Add(quarry.net.ID.Value, quarryinfo);
            }
            quarryinfo.Name = PositionToString(quarry.transform.position);
            quarryinfo.Quarry = quarry;

            if ((_config.enableMiningQuarry && quarry.ShortPrefabName == "miningquarry_static" && !quarry.OwnerID.IsSteamId()) ||
                (_config.enablePumpJack && quarry.ShortPrefabName == "pumpjack-static" && !quarry.OwnerID.IsSteamId()) ||
                (_config.enablePlayerQuarry && _config.enableMiningQuarry && quarry.ShortPrefabName == "mining_quarry") ||
                (_config.enablePlayerPump && _config.enablePumpJack && quarry.ShortPrefabName == "mining.pumpjack") ||
                (_config.enablePlayerQuarry && _config.enableMiningQuarry && quarry.ShortPrefabName == "miningquarry_static" && quarry.OwnerID.IsSteamId()) ||
                (_config.enablePlayerPump && _config.enablePumpJack && quarry.ShortPrefabName == "pumpjack-static") && quarry.OwnerID.IsSteamId())
            {
                quarryinfo.EnableLock = true;
                try
                {
                    // experimental max stack size
                    StorageContainer fuelstorage = quarry.fuelStoragePrefab.instance.GetComponent<StorageContainer>();
                    if (_config.DieselFuelMaxStackSize >= 0) fuelstorage.inventory.maxStackSize = _config.DieselFuelMaxStackSize;
                    if (_config.FuelSlots > 0) fuelstorage.inventory.capacity = _config.FuelSlots;
                }
                catch (Exception ex)
                {
                    PrintError(ex.Message);
                    PrintError($"Quarry at location {quarry.transform.position} is missing fuel storage container.");
                    quarry.fuelStoragePrefab?.DoSpawn(quarry);
                }

                try
                {
                    StorageContainer hopper = quarry.hopperPrefab.instance.GetComponent<StorageContainer>();
                    if (_config.HopperSlots > 0) hopper.inventory.capacity = _config.HopperSlots;
                }
                catch (Exception ex)
                {
                    PrintError(ex.Message);
                    PrintError($"Quarry at location {quarry.transform.position} is missing hopper storage container.");
                    quarry.hopperPrefab?.DoSpawn(quarry);
                }

                try
                {
                    quarryinfo.processRate = quarry.processRate;
                    if (!Associate.ContainsKey(quarry.fuelStoragePrefab.instance.net.ID.Value))
                        Associate.Add(quarry.fuelStoragePrefab.instance.net.ID.Value, quarry.net.ID.Value);
                    if (!Associate.ContainsKey(quarry.hopperPrefab.instance.net.ID.Value))
                        Associate.Add(quarry.hopperPrefab.instance.net.ID.Value, quarry.net.ID.Value);
                    if (!Associate.ContainsKey(quarry.engineSwitchPrefab.instance.net.ID.Value))
                        Associate.Add(quarry.engineSwitchPrefab.instance.net.ID.Value, quarry.net.ID.Value);
                }
                catch (Exception ex)
                {
                    PrintError(ex.Message);
                    PrintError($"Quarry spawn associate error : {quarry.ShortPrefabName} at {quarry.transform.position}");
                }

                if (_config.TimePerBarrel > 0.0f)
                {
                    quarry.workPerFuel = quarry.workToAdd / quarry.processRate * (float)_config.TimePerBarrel;                    
                }
                if (quarry.pendingWork > quarry.workPerFuel) quarry.pendingWork = quarry.workPerFuel;
            }
            else
            { 
                quarryinfo.EnableLock = false;
            }

            if (quarry.ShortPrefabName == "miningquarry_static" || quarry.ShortPrefabName == "mining_quarry")
            {
                quarryinfo.readablename = ReadableNameQuarry;
                quarryinfo.type = miningtype.miningquarry;
            }
            else
            {
                quarryinfo.readablename = ReadableNamePump;
                quarryinfo.type = miningtype.miningpumpjack;
            }
            
            QuarryDetector quarrydetector = quarry.gameObject.GetComponent<QuarryDetector>();
            if (quarrydetector == null) quarry.gameObject.AddComponent<QuarryDetector>();
        }

        private object OnQuarryToggle(MiningQuarry quarry, BasePlayer player)
        {
            if (quarry == null || player==null) return null;

            QuarryInfo quarryinfo = null;
            if (!Quarrys.TryGetValue(quarry.net.ID.Value, out quarryinfo))
            {
                PrintWarning("Unregistered Quarry ?");
                return true;  // 
            }

            if (quarryinfo.EnableLock)
            {
                bool permquarry = CanPlayerUseQuarry((ulong)player.userID);
                bool permpump = CanPlayerUsePump((ulong)player.userID);
                quarryinfo.state = quarry.IsEngineOn();

                if (!quarryinfo.state && _config.UseDynamicPVP && isEntityInDynamicPVP(quarry))
                {
                    SendChatMessage(player, "QuarryStartEngineNolock", quarryinfo.readablename);
                    quarryinfo.playerid = 0;
                    quarryinfo.displayName = "nobody";
                    quarryinfo.checkfueltime = DateTime.Now;
                    quarryinfo.PlayerDisconnectTime = DateTime.MaxValue;
                    int itemcnt = quarry.fuelStoragePrefab.instance.GetComponent<StorageContainer>().inventory.TotalItemAmount() + 1;
                    PrintToLog($"Quarry [{quarryinfo.Name}] started at : {DateTime.Now.ToString("h:mm tt")} by {player.displayName}/{(ulong)player.userID} with {itemcnt} diesel");
                    return null;
                }

                if (!_config.UseQuarryWithoutPermission &&
                    !(permquarry && quarryinfo.type == miningtype.miningquarry ||
                      permpump && quarryinfo.type == miningtype.miningpumpjack))
                {
                    SendChatMessage(player, "QuarryAccessDenied");
                    return true;
                }

                // check if engine is off. 
                if (!quarryinfo.state)
                {
                    bool checkcooldown = CheckPlayerCooldown((ulong)player.userID, quarryinfo);

                    // check if player is allowed to switch engine
                    if ((quarryinfo.playerid == 0 && !checkcooldown) ||    // not locked
                        ((ulong)player.userID == quarryinfo.playerid) ||        // already lock by same player
                        (SameTeam((ulong)player.userID, quarryinfo.playerid)) ||
                        (SameClan((ulong)player.userID, quarryinfo.playerid)))
                    {
                        if (permquarry && quarryinfo.type == miningtype.miningquarry ||
                            permpump && quarryinfo.type == miningtype.miningpumpjack)
                        {
                            quarryinfo.playerid = (ulong)player.userID;
                            quarryinfo.displayName = player.displayName;
                            quarryinfo.checkfueltime = DateTime.Now;
                            quarryinfo.PlayerDisconnectTime = DateTime.MaxValue;
                            return null;
                        }
                        else
                        {
                            quarryinfo.playerid = 0;
                            quarryinfo.displayName = "nobody";
                            quarryinfo.checkfueltime = DateTime.Now;
                            quarryinfo.PlayerDisconnectTime = DateTime.MaxValue;
                            return null;
                        }
                    }
                    else
                    {
                        if (checkcooldown)
                        {
                            SendChatMessage(player, "QuarryPlCooldown", PlayerCooldownTime((ulong)player.userID, quarryinfo), quarryinfo.readablename);
                        }
                        else
                        {
                            SendChatMessage(player, "QuarryInUse", quarryinfo.displayName, quarryinfo.Name, quarryinfo.readablename);
                        }
                        return true;
                    }                    
                }
                else  // try to turn off quarry
                {                    
                    if (quarryinfo.playerid != 0)
                    {
                        // Quarry state is Off
                        if ((((ulong)player.userID == quarryinfo.playerid) ||
                            SameTeam((ulong)player.userID, quarryinfo.playerid) ||
                            SameClan((ulong)player.userID, quarryinfo.playerid)) && _config.engineloot)
                        {
                            return null;
                        }
                        else
                        {
                            SendChatMessage(player, "QuarryInUse", quarryinfo.displayName, quarryinfo.Name, quarryinfo.readablename);
                            return true;
                        }
                    }
                    return null;
                }
            }
            return null; // not lockable
        }

        float CalcFuelTime(MiningQuarry quarry)
        {
            if (quarry == null) return 0f;
            int itemcnt = quarry.fuelStoragePrefab.instance.GetComponent<StorageContainer>().inventory.TotalItemAmount();
            var FuelTime = (quarry.processRate * (quarry.pendingWork + quarry.workPerFuel * (float)itemcnt) / (quarry.workToAdd * 60f));
            return FuelTime;
        }

        // The engine was toggled by user. not called whe stop for fuel or full
        void OnQuarryToggled(MiningQuarry quarry, BasePlayer player)
        {
            if (player == null) return;

            QuarryInfo quarryinfo;
            if (!Quarrys.TryGetValue(quarry.net.ID.Value, out quarryinfo))
            {
                PrintWarning("Unregistered Quarry ?");
                return;
            }
            quarryinfo.state = quarry.IsEngineOn();

            if (quarryinfo.EnableLock)
            {
                if (quarryinfo.state) // turn on
                {
                    if (quarryinfo.playerid != 0)
                    {
                        bool checkcooldown = CheckPlayerCooldown((ulong)player.userID, quarryinfo);
                        if (((ulong)player.userID == quarryinfo.playerid) ||       // already lock by same player
                            (SameTeam((ulong)player.userID, quarryinfo.playerid)) ||
                            (SameClan((ulong)player.userID, quarryinfo.playerid)))
                        {
                            // Quarry state is On
                            quarryinfo.checkfueltime = DateTime.Now;
                            quarryinfo.PlayerDisconnectTime = DateTime.MaxValue;
                            quarryinfo.FuelTimeRemaining = CalcFuelTime(quarry);

                            if (quarryinfo.FuelTimeRemaining < 60) SendChatMessage(player, "QuarryStartEngine", Math.Ceiling(quarryinfo.FuelTimeRemaining), quarryinfo.readablename);
                            else SendChatMessage(player, "QuarryStartEngineHM", quarryinfo.FuelTimeRemaining / 60, quarryinfo.FuelTimeRemaining % 60, quarryinfo.readablename);

                            if (_config.MessageAll)
                            {
                                BroadcastMessage("QuarryStarted", player.displayName, quarryinfo.Name, quarryinfo.readablename);
                            }

                            int itemcnt = quarry.fuelStoragePrefab.instance.GetComponent<StorageContainer>().inventory.TotalItemAmount() + 1;
                            PrintToLog($"Quarry [{quarryinfo.Name}] started at : {DateTime.Now.ToString("h:mm tt")} by {player.displayName}/{(ulong)player.userID} with {itemcnt} diesel");

                            // Start 
                            AddPlayerCooldown(quarryinfo.playerid, quarryinfo.FuelTimeRemaining, quarryinfo);

                            if (_config.FlushOutputHopper)
                            {
                                var hopper = quarry.hopperPrefab.instance.GetComponent<StorageContainer>();
                                PrintToLog($"Flush {ReportInventory(hopper)} items from hopper");
                                DropItemContainer(hopper.inventory, hopper.transform.position + quarry.transform.rotation * DropVect, quarry.transform.rotation);
                            }

                            if (quarryinfo.timerQuarryStopped != null)
                            {
                                timer.Destroy(ref quarryinfo.timerQuarryStopped);
                            }
                        }
                    }
                }
                else // turn off
                {
                    if (quarryinfo.playerid != 0)
                    {
                        // Quarry state is Off
                        if ((((ulong)player.userID == quarryinfo.playerid) ||
                        SameTeam((ulong)player.userID, quarryinfo.playerid) ||
                        SameClan((ulong)player.userID, quarryinfo.playerid)) && _config.engineloot)
                        {
                            OnQuarryToggledOff(quarry, false);
                            PrintToLog($"Quarry [{quarryinfo.Name}] stopped at : {DateTime.Now.ToString("h:mm tt")} by {player.displayName}/{(ulong)player.userID}");
                        }
                    }
                }
            }
        }

        void OnQuarryToggledOff(MiningQuarry quarry, bool enableMsg = true)
        {
            QuarryInfo quarryinfo;
            if (Quarrys.TryGetValue(quarry.net.ID.Value, out quarryinfo))
            {
                // Quarry state is Off
                quarryinfo.state = quarry.IsEngineOn();
                quarryinfo.stoptime = DateTime.Now;
                quarryinfo.checkfueltime = DateTime.Now;
                quarryinfo.FuelTimeRemaining = 0;

                if (quarryinfo.playerid != 0 && quarryinfo.EnableLock)
                {
                    // Update cooldown info when the quarry stop
                    AddPlayerCooldown(quarryinfo.playerid, 0f, quarryinfo);

                    // player will be null when disconnected and wont receive message
                    var player = BasePlayer.FindByID(quarryinfo.playerid);
                    if (player != null && enableMsg)
                    {
                        SendChatMessage(player, "QuarryOff", quarryinfo.Name, quarryinfo.readablename);
                        SendChatMessage(player, "QuarryCoolDown", _config.CoolDown);
                    }

                    if (enableMsg)
                    {
                        int fuelcnt = quarry.fuelStoragePrefab.instance.GetComponent<StorageContainer>().inventory.TotalItemAmount();
                        int hoppercnt = quarry.hopperPrefab.instance.GetComponent<StorageContainer>().inventory.TotalItemAmount();
                        PrintToLog($"Quarry [{quarryinfo.Name}] stopped at : {DateTime.Now.ToString("h:mm tt")} because of fuel or hopper is full ({fuelcnt} diesel / {ReportInventory(quarry.hopperPrefab.instance.GetComponent<StorageContainer>())} items in hopper)");
                    }

                    if (quarryinfo.timerQuarryStopped != null)
                    {
                        timer.Destroy(ref quarryinfo.timerQuarryStopped);
                    }
                    quarryinfo.timerQuarryStopped = timer.Once(_config.CoolDown * 60f, () =>
                    {
                        // In case quarry is restarted after cooldown
                        if (quarryinfo!=null && !quarryinfo.state)
                        {
                            if (_config.MessageAll) BroadcastMessage("QuarryStoped", quarryinfo.displayName, quarryinfo.Name, quarryinfo.readablename);
                            PrintToLog($"Quarry [{quarryinfo.Name}] Unlocked to all players");
                            quarryinfo.playerid = 0;
                            quarryinfo.displayName = "nobody";
                            quarryinfo.PlayerDisconnectTime = DateTime.MaxValue;
                            quarryinfo.timerQuarryStopped = null;
                        }
                        
                    });
                }
            }
        }

        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (player == null) return null;
            if (container == null) return null;          

            // pumpjack
            if (_config.enableMiningQuarry)
            {
                if (container.prefabID == FuelStoragePumpID) return LootQuarryFuel(player, container); // "fuelstorage"
                else if (container.prefabID == OutputHopperPumpID) return LootQuarryOutput(player, container);    // "crudeoutput"
            }

            // mining quarry
            if (_config.enablePumpJack)
            {
                if (container.prefabID == FuelStorageQuarryID) return LootQuarryFuel(player, container);    // "fuelstorage"
                else if (container.prefabID == OutputHopperQuarryID) return LootQuarryOutput(player, container);    // "hopperoutput"
            }

            return null;
        }

        void OnLootEntityEnd(BasePlayer player, StorageContainer container)
        {
            if (container.prefabID == FuelStoragePumpID ||
     			container.prefabID == FuelStorageQuarryID) LootDieselEngineEnd(player, container);   // fuelstorage for quarry or pumpjack
        }
		
        void LootDieselEngineEnd(BasePlayer player, StorageContainer fueltank)
        {
            ulong quarryID;
            QuarryInfo quarryinfo;
            if (!Associate.TryGetValue(fueltank.net.ID.Value, out quarryID)) return;
            if (!Quarrys.TryGetValue(quarryID, out quarryinfo)) return;
            if (!quarryinfo.EnableLock) return;

            var quarry = quarryinfo.Quarry;
            var amount = fueltank.inventory.GetAmount(1568388703, true);    // lowgradefuel == -946369541, Diesel == 1568388703

            if (quarry != null && quarry.IsEngineOn())
            {
				quarryinfo.checkfueltime = DateTime.Now;
                quarryinfo.FuelTimeRemaining = CalcFuelTime(quarry);

                AddPlayerCooldown(quarryinfo.playerid, quarryinfo.FuelTimeRemaining, quarryinfo);
            }
            PrintToLog($"Player {player.displayName}/{(ulong)player.userID} looting fuel in engine at [{quarryinfo.Name}],  {amount} diesel");
        }

        object LootQuarryOutput(BasePlayer player, StorageContainer container)
        {
            ulong quarryID;
            QuarryInfo quarryinfo;

            if (IsAdmin((ulong)player.userID) == true) return null;
            if (!Associate.TryGetValue(container.net.ID.Value, out quarryID)) return null;
            if (!Quarrys.TryGetValue(quarryID, out quarryinfo)) return null;  // 

            if (!quarryinfo.EnableLock) return null;
            if ((!_config.UseQuarryWithoutPermission && quarryinfo.type == miningtype.miningquarry && !CanPlayerUseQuarry((ulong)player.userID)) ||
                (!_config.UseQuarryWithoutPermission && quarryinfo.type == miningtype.miningpumpjack && !CanPlayerUsePump((ulong)player.userID))) 
            {
                SendChatMessage(player, "QuarryAccessDenied");
                return true;
            }

            if (quarryinfo.playerid == 0 ||
                IsQuarryAvailable(quarryinfo))
            {
                int hoppercnt = container.inventory.TotalItemAmount();
                PrintToLog($"Player {player.displayName}/{(ulong)player.userID} looting quarry [{quarryinfo.Name}], {ReportInventory(container)} items in hopper");
                return null;
            }

            if (((ulong)player.userID == quarryinfo.playerid) ||
                (SameTeam((ulong)player.userID, quarryinfo.playerid)) ||
                (SameClan((ulong)player.userID, quarryinfo.playerid)))
            {
                int hoppercnt = container.inventory.TotalItemAmount();
                PrintToLog($"Player {player.displayName}/{(ulong)player.userID} looting quarry [{quarryinfo.Name}] owned by:{quarryinfo.displayName}/{quarryinfo.playerid}, {ReportInventory(container)} items in hopper");
                return null;
            }

            SendChatMessage(player, "QuarryInUse", quarryinfo.displayName, quarryinfo.Name, quarryinfo.readablename);

            return true;
        }

        object LootQuarryFuel(BasePlayer player, StorageContainer container)
        {
            ulong quarryID;
            QuarryInfo quarryinfo;
            
            if (IsAdmin((ulong)player.userID) == true) return null;

            if (!Associate.TryGetValue(container.net.ID.Value, out quarryID)) return null;
            if (!Quarrys.TryGetValue(quarryID, out quarryinfo)) return null;  // 
            if (!quarryinfo.EnableLock) return null;

            var quarry = quarryinfo.Quarry;

            if ((!_config.UseQuarryWithoutPermission && quarryinfo.type == miningtype.miningquarry && !CanPlayerUseQuarry((ulong)player.userID)) ||
               (!_config.UseQuarryWithoutPermission && quarryinfo.type == miningtype.miningpumpjack && !CanPlayerUsePump((ulong)player.userID)))
            {
                SendChatMessage(player, "QuarryAccessDenied"); 
                return true;
            }

            if (quarryinfo.playerid == 0 ||
                IsQuarryAvailable(quarryinfo))
            {
                return null;
            }

            if (!_config.engineloot)
            {
                float amount = (quarry!=null) ? quarry.pendingWork/quarry.workPerFuel : 0;
                amount += (float)container.inventory.GetAmount(1568388703, true);   // check lowgradefuel = -946369541, diesel == 1568388703
                if (quarryinfo.state) SendChatMessage(player, "QuarryFuel", Math.Ceiling(amount).ToString(), quarryinfo.readablename);                    
                else SendChatMessage(player, "QuarryInUse", quarryinfo.displayName, quarryinfo.Name, quarryinfo.readablename);
                return true;   // disable engine loot
            }

            if (((ulong)player.userID == quarryinfo.playerid) ||
                (SameTeam((ulong)player.userID, quarryinfo.playerid)) ||
                (SameClan((ulong)player.userID, quarryinfo.playerid))) return null;

            SendChatMessage(player, "QuarryInUse", quarryinfo.displayName, quarryinfo.Name, quarryinfo.readablename);
            return true;
        }

        void OnQuarryGather(MiningQuarry quarry, Item item)
        {
            QuarryInfo quarryinfo;
            if (!Quarrys.TryGetValue(quarry.net.ID.Value, out quarryinfo)) return;

            if ((_config.ClearQuarryLockOnAllTeamDisconnect || _config.ClearQuarryLockOnPlayerOwnerDisconnect) && --quarryinfo.CountTick == 0)
            {
                quarryinfo.CountTick = 2;

                if (quarryinfo.PlayerDisconnectTime != DateTime.MaxValue)
                {
                    if (quarryinfo.PlayerDisconnectTime.AddMinutes(_config.CooldownQuarryLockOnDisconnect) <= DateTime.Now)
                    {
                        // Clear quarry lock                        
                        if (_config.ClearQuarryLockOnPlayerOwnerDisconnect)
                        {
                            PrintToLog($"Quarry [{quarryinfo.Name}] unlocked because player owner disconnected for {_config.CooldownQuarryLockOnDisconnect} minutes {quarryinfo.displayName}/{quarryinfo.playerid}  isconnected:{BasePlayer.FindAwakeOrSleeping(quarryinfo.playerid.ToString())?.IsConnected ?? false}");
                        }
                        if (_config.ClearQuarryLockOnAllTeamDisconnect)
                        {
                            PrintToLog($"Quarry [{quarryinfo.Name}] unlocked because all team/clan disconnected for {_config.CooldownQuarryLockOnDisconnect} minutes {quarryinfo.displayName}/{quarryinfo.playerid} ");
                        }

                        if (_config.MessageAll) BroadcastMessage("QuarryClearOffline", quarryinfo.Name);
                        quarryinfo.PlayerDisconnectTime = DateTime.MaxValue;
                        ClearQuarry(quarryinfo, false);
                    }
                }
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            foreach (KeyValuePair<ulong, QuarryInfo> quarrys in Quarrys)
            {
                foreach (var kvp in quarrys.Value.PlayerCooldown)
                {
                    if (_config.ClearQuarryLockOnPlayerOwnerDisconnect)
                    {
                        if ((ulong)player.userID == kvp.Key && (ulong)player.userID == quarrys.Value.playerid)
                        {
                            kvp.Value.IsOnline = true;
                            quarrys.Value.PlayerDisconnectTime = DateTime.MaxValue;
                            PrintToLog($"Player connected {player.displayName}/{(ulong)player.userID} at {DateTime.Now}   isconnected:{BasePlayer.FindAwakeOrSleeping(player.UserIDString)?.IsConnected ?? false}");

                        }
                    }

                    if (_config.ClearQuarryLockOnAllTeamDisconnect)
                    {
                        if ((ulong)player.userID == kvp.Key)
                        {
                            kvp.Value.IsOnline = true;
                            quarrys.Value.PlayerDisconnectTime = DateTime.MaxValue;
                            PrintToLog($"Player connected {player.displayName}/{(ulong)player.userID} at {DateTime.Now}   isconnected:{BasePlayer.FindAwakeOrSleeping(player.UserIDString)?.IsConnected ?? false}");
                        }
                    }
                }
            }
        }

        void OnPlayerDisconnected(BasePlayer player, string strReason)
        {
            int PlayerCount;
            if (player == null) return;

            foreach (KeyValuePair<ulong, QuarryInfo> quarrys in Quarrys)
            {
                if (_config.ClearQuarryLockOnPlayerOwnerDisconnect) PlayerCount = 1;
                else PlayerCount = quarrys.Value.PlayerCooldown.Count;

                foreach (var kvp in quarrys.Value.PlayerCooldown)
                {
                    if (_config.ClearQuarryLockOnPlayerOwnerDisconnect)
                    {
                        if ((ulong)player.userID == kvp.Key && (ulong)player.userID == quarrys.Value.playerid)
                        {
                            kvp.Value.IsOnline = false;
                            PlayerCount--;
                            PrintToLog($"Player disconnected {player.displayName}/{(ulong)player.userID} at {DateTime.Now} ");
                        }
                    }
                    if (_config.ClearQuarryLockOnAllTeamDisconnect)
                    {
                        if ((ulong)player.userID == kvp.Key)
                        {
                            kvp.Value.IsOnline = false;
                            PrintToLog($"Player disconnected {player.displayName}/{(ulong)player.userID} at {DateTime.Now} ");
                        }
                        if (!kvp.Value.IsOnline || kvp.Value.IgnoreOnline) PlayerCount--;                        
                    }
                }

                if (PlayerCount <= 0)
                {
                    quarrys.Value.PlayerDisconnectTime = DateTime.Now;
                    PrintToLog($"Activate disconnect countdown {player.displayName}/{(ulong)player.userID} at {DateTime.Now}  {quarrys.Value.Name} by {quarrys.Value.displayName}/{quarrys.Value.playerid} ");
                }
                if (PlayerCount < 0)
                {
                    PrintToLog($"Warning : PlayerCount < 0 {player.displayName}/{(ulong)player.userID} at {DateTime.Now}  {quarrys.Value.Name}");
                }
            }
        }

#endregion Hooks

#region Helpers
        bool CheckPlayerCooldown(ulong PlayerID, QuarryInfo quarryinfo)
        {
            List<ulong> removals = Pool.Get<List<ulong>>();
            removals.Clear();
            DateTime now = DateTime.Now;
            bool result = false;

            if (_config.CoolDownGlobal && !CanPlayerBypassCooldown(PlayerID))
            {
                foreach (KeyValuePair<ulong, QuarryInfo> quarrykvp in Quarrys)
                {
                    foreach (KeyValuePair<ulong, CooldownTime> kvp in quarrykvp.Value.PlayerCooldown)
                    {
                        if (now >= kvp.Value.Time) removals.Add(kvp.Key);
                        if (kvp.Key == PlayerID && now < kvp.Value.Time) result=true;
                    }

                    // cleanup dict entry of older time info
                    foreach (ulong removeid in removals)
                    {
                        quarrykvp.Value.PlayerCooldown.Remove(removeid);
                    }
                    removals.Clear();
                }
            }
            else
            {
                foreach (KeyValuePair<ulong, CooldownTime> kvp in quarryinfo.PlayerCooldown)
                {
                    if (now >= kvp.Value.Time) removals.Add(kvp.Key);
                    if (kvp.Key == PlayerID && now < kvp.Value.Time) result=true;
                }

                // cleanup dict entry of older time info
                foreach (ulong removeid in removals)
                {
                    quarryinfo.PlayerCooldown.Remove(removeid);
                }
            }

            Pool.FreeUnmanaged(ref removals);
            return result;
        }

        int PlayerCooldownTime(ulong PlayerID, QuarryInfo quarryinfo)
        {
            DateTime now = DateTime.Now;
            CooldownTime cooldown;

            if (_config.CoolDownGlobal && !CanPlayerBypassCooldown(PlayerID))
            {
                foreach (KeyValuePair<ulong, QuarryInfo> quarrykvp in Quarrys)
                {
                    if (quarrykvp.Value.PlayerCooldown.TryGetValue(PlayerID, out cooldown) && now < cooldown.Time)
                   {
                        var diff = cooldown.Time.Subtract(now);
                        return (int)Math.Ceiling(diff.TotalMinutes);
                    }
                }
            }
            else
            {
                if (quarryinfo.PlayerCooldown.TryGetValue(PlayerID, out cooldown) && now < cooldown.Time)			
               {
                    var diff = cooldown.Time.Subtract(now);
                    return (int)Math.Ceiling(diff.TotalMinutes);
                }
            }
            return 0;
        }

        void AddPlayerCooldown(ulong playerID, double extra, QuarryInfo quarryinfo)
        {
            foreach (KeyValuePair<ulong, CooldownTime> kvp in quarryinfo.PlayerCooldown)
            {
                kvp.Value.IgnoreOnline = true; // Ignore previous users of the quarry
            }

            CooldownTime CoolDown = new CooldownTime();
            CoolDown.IsOnline = BasePlayer.FindAwakeOrSleeping(playerID.ToString())?.IsConnected ?? false;
            CoolDown.Time = DateTime.Now.AddMinutes(_config._PlayerCoolDown + extra);
            AddSinglePlayerCooldown(playerID, quarryinfo, CoolDown);

            if (CoolDown.IsOnline == false)
            {
                PrintWarning($"quarry [{quarryinfo.Name}] change on/off state, Player {playerID} is flagged Offline.");
            }

            // If enable, Process all team members
            var teammanager = RelationshipManager.ServerInstance;
            if (_config.UseTeams && teammanager != null)
            {
                var team = teammanager.FindPlayersTeam(playerID);
                if (team != null)
                {
                    foreach (var ent in team.members)
                    {
                        CoolDown = new CooldownTime();
                        CoolDown.IsOnline = BasePlayer.FindAwakeOrSleeping(ent.ToString())?.IsConnected ?? false;
                        CoolDown.IgnoreOnline = false;
                        CoolDown.Time = DateTime.Now.AddMinutes(_config._PlayerCoolDown + extra);
                        AddSinglePlayerCooldown(ent, quarryinfo, CoolDown);
                    }
                }
            }

            // if enable, process the clan member
            if (_config.UseClans && IspluginLoaded(Clans))
            {
                BasePlayer friend;
                var member = Clans.Call("GetClanMembers", playerID.ToString());
                if (member!=null)
                {
                    List<string> memberlist = member as List<string>;

                    foreach (var ent in memberlist)
                    {
                        friend = BasePlayer.FindAwakeOrSleeping(ent);
                        if (friend != null)
                        {
                            CoolDown = new CooldownTime();
                            CoolDown.IsOnline = friend.IsConnected;
                            CoolDown.IgnoreOnline = false;
                            CoolDown.Time = DateTime.Now.AddMinutes(_config._PlayerCoolDown + extra);
                            AddSinglePlayerCooldown(ulong.Parse(ent), quarryinfo, CoolDown);
                        }
                    }
                }
            }

            // if enable, process the clan member from clantable
            if (_config.UseClanTable)
            {
                BasePlayer friend;
                long clanId = BasePlayer.FindAwakeOrSleeping(playerID.ToString())?.clanId ?? 0;
                if (clanId != 0)
                {
                    IClan clan = null;
                    if (ClanManager.ServerInstance.Backend?.TryGet(clanId, out clan) ?? false)
                    {
                        foreach (ClanMember member in clan.Members)
                        {
                            friend = BasePlayer.FindAwakeOrSleeping(member.SteamId.ToString());
                            if (friend != null)
                            {
                                CoolDown = new CooldownTime();
                                CoolDown.IsOnline = friend.IsConnected;
                                CoolDown.IgnoreOnline = false;
                                CoolDown.Time = DateTime.Now.AddMinutes(_config._PlayerCoolDown + extra);
                                AddSinglePlayerCooldown(member.SteamId, quarryinfo, CoolDown);
                            }
                        }
                    }
                }
            }
        }

        void AddSinglePlayerCooldown(ulong PlayerID, QuarryInfo quarryinfo, CooldownTime Cooldown)
        {
			// filter some bogus 0 entry comming from Team and Clan plugin
            if (PlayerID == 0) return;
            if (quarryinfo.PlayerCooldown.ContainsKey(PlayerID))
            {
                quarryinfo.PlayerCooldown[PlayerID] = Cooldown;
            }
            else
            {
                quarryinfo.PlayerCooldown.Add(PlayerID, Cooldown);
            }
        }

        bool isEntityInDynamicPVP(BaseEntity entity)
        {
            if (!IspluginLoaded(ZoneManager) || !IspluginLoaded(DynamicPVP)) return false;

            var zones = (string[])ZoneManager.Call("GetEntityZoneIDs", entity);
            foreach (var zone in zones)
            {
                if ((bool)DynamicPVP.Call("IsDynamicPVPZone", zone)) return true;
            }
            return false;
        }

        bool IspluginLoaded(Plugin a) => (a?.IsLoaded ?? false);

        #endregion Helpers

        #region Commands
        private void Quarry_info(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (player == null) return;

            SendChatMessage(player, "QuarryInfoCmd");

            foreach (var KVP in Quarrys)
            {
                MiningQuarry quarry = KVP.Value.Quarry;
                if (quarry == null) continue;
                if (!KVP.Value.EnableLock) continue;
                if (_config.UseDynamicPVP && isEntityInDynamicPVP(quarry)) continue;

                var remaining = Math.Max(0, (KVP.Value.FuelTimeRemaining + _config.CoolDown) - (DateTime.Now - KVP.Value.checkfueltime).TotalMinutes);

                if (remaining > 0)
                {
                    SendChatMessage(player, "QuarryAvailableIn2", KVP.Value.Name, Math.Ceiling(remaining), KVP.Value.readablename, KVP.Value.displayName);
                }
                else
                {
                    SendChatMessage(player, "QuarryStoped_B", KVP.Value.displayName, KVP.Value.Name, KVP.Value.readablename);
                }

                var checkcooldown = CheckPlayerCooldown((ulong)player.userID, KVP.Value);
                if (checkcooldown)
                {
                    SendChatMessage(player, "QuarryPlCooldown", PlayerCooldownTime((ulong)player.userID, KVP.Value), KVP.Value.readablename);
                }                
            }

            return;
        }

        
        private void Quarry_clearstatus(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (iplayer.Id == "server_console" ||
                (player != null && IsAdmin((ulong)player.userID)))
            {
                foreach (var quarry in Quarrys)
                {
                    ClearQuarry(quarry.Value);

                }

                if (player != null)
                {
                    SendChatMessage(player, "QuarryClearStatus");
                    PrintWarning(Lang("Quarryclearstatus", player.UserIDString));
                }
                else
                {
                    PrintWarning(Lang("Quarryclearstatus", "0"));
                }

                SaveData();
                PrintToLog($"Quarry clear status {iplayer.Id} ");
            }

            return;
        }
        
        private void Mining_stopcommand(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (player == null) return;
            
            MiningQuarry quarry = null;
            List<MiningQuarry> quarries = Pool.Get<List<MiningQuarry>>();
            Vis.Entities<MiningQuarry>(player.transform.position, 20f, quarries, -1);
            if (quarries.Count > 0)
            {
                quarry = quarries[0];
                var distance = Vector3Ex.Distance2D(player.transform.position, quarries[0].transform.position);
            }
            Pool.FreeUnmanaged(ref quarries);

            QuarryInfo quarryinfo;
            if ((quarry == null) ||
                !Quarrys.TryGetValue(quarry.net.ID.Value, out quarryinfo))
            {
                SendChatMessage(player, "NoQuarryFoundNear");
                return;
            }

            StorageContainer fuelstorage = quarry.fuelStoragePrefab.instance.GetComponent<StorageContainer>();
            StorageContainer hopper  = quarry.hopperPrefab.instance.GetComponent<StorageContainer>();
            if (fuelstorage == null || hopper == null) { Puts("missing storage in Mining_stopcommand"); return; }

            int fuelmoved = 0;
            if (((ulong)player.userID == quarryinfo.playerid) ||
                (SameTeam((ulong)player.userID, quarryinfo.playerid)) ||
                (SameClan((ulong)player.userID, quarryinfo.playerid)))
            {
                List<Item> items = fuelstorage.inventory.FindItemsByItemID(1568388703);    // lowgradefuel == -946369541, Diesel == 1568388703
                foreach (Item item in items)
                {
                    if (!item.MoveToContainer(hopper.inventory, -1, true, false, null, true))
                    {
                        item.Drop(hopper.GetDropPosition(), hopper.GetDropVelocity(), default(Quaternion));
                    }
                    fuelmoved += item.amount;
                }
                SendChatMessage(player, "MoveFuelToOutput");
                PrintToLog($"Quarry stop command by {player.displayName}/{(ulong)player.userID}  at {quarryinfo.Name}. moving {fuelmoved} fuel ");
            }
            else SendChatMessage(player, "NoQuarryFoundNear");

            return;
        }
        

        #endregion Commands

        #region Helpers

        void ClearQuarry(QuarryInfo quarry, bool clearcooldownlist = true)
        {
            quarry.playerid = 0;
            quarry.displayName = "nobody";
            quarry.FuelTimeRemaining = 0f;
            quarry.state = false;
            quarry.PlayerDisconnectTime = DateTime.MaxValue;

            quarry.checkfueltime = DateTime.MinValue;
            quarry.stoptime = DateTime.MinValue;
            if (clearcooldownlist) quarry.PlayerCooldown = new Dictionary<ulong, CooldownTime>();
        }

        bool IsQuarryAvailable(QuarryInfo quarryinfo)
        {
            quarryinfo.state = quarryinfo.Quarry.IsEngineOn();
            // check if quarry is off longer then cooldown
            if ((quarryinfo.state == false) && ((DateTime.Now - quarryinfo.stoptime).TotalMinutes > _config.CoolDown))            
            {
                // enough time elapse, allow loot, clear owner info
                quarryinfo.playerid = 0;
                quarryinfo.displayName = "nobody";
                quarryinfo.PlayerDisconnectTime = DateTime.MaxValue;
                return true;
            }
            return false;
        }

        private static void DropItemContainer(ItemContainer itemContainer, Vector3 dropPosition, Quaternion rotation) => itemContainer?.Drop(PREFAB_ITEM_DROP, dropPosition, rotation, 0);

        string ReportInventory(StorageContainer storage)
        {
            StringBuilder Report = new StringBuilder(" [");
            int itemcount;
            int count = 0;
            foreach (var itemstr in _config.ReportItems)
            {
                Item item = storage.inventory.FindItemByItemName(itemstr);
                if (item == null) continue;
                itemcount = storage.inventory.GetTotalItemAmount(item, 0, storage.inventory.capacity - 1);
                if (count != 0) Report.Append(", ");
                Report.Append(string.Format("{1} {0}", item.info.displayName.english, itemcount.ToString()));
                count++;
            }
            if (count == 0) Report.Append("Empty");
            Report.Append("]");
            return Report.ToString();
        }

        private void PrintToLog(string message)
        {
            if (_config.Debug)
                UnityEngine.Debug.Log($"[{_instance.Name}] [{DateTime.Now.ToString("h:mm tt")}]{Platform} {message}");

            if (_config.LogToFile)
                LogToFile(TraceFile, $"[{DateTime.Now.ToString("h:mm tt")}]{Platform} {message}", this);

            if (_config.Discordena)
                PrintToDiscord(message);
        }


        private bool IsAdmin(ulong id) =>
            (permission.UserHasPermission(id.ToString(), _config.PermissionAdmin));
        private bool IsAdmin(string id) =>
            (permission.UserHasPermission(id, _config.PermissionAdmin));


        private bool CanPlayerUseQuarry(ulong id) =>
            (!_config.UsePermission || 
                permission.UserHasPermission(id.ToString(), _config.PermissionUseQuarry) ||
                permission.UserHasPermission(id.ToString(), _config.PermissionAdmin));
        private bool CanPlayerUsePump(ulong id) =>
            (!_config.UsePermission ||
                permission.UserHasPermission(id.ToString(), _config.PermissionUsePump) ||
                permission.UserHasPermission(id.ToString(), _config.PermissionAdmin));

        private bool CanPlayerBypassCooldown(ulong id) =>
            (!_config.UsePermission ||
                permission.UserHasPermission(id.ToString(), _config.PermissionBypassCooldown) ||
                permission.UserHasPermission(id.ToString(), _config.PermissionAdmin));        

        private bool SameClan(ulong playerID, ulong friendID)
        {
            if (_config.UseClans == true && IspluginLoaded(Clans))
            {
                //Clans
                var isMember = Clans.Call("IsClanMember", playerID.ToString(), friendID.ToString());
                if (isMember != null && (bool)isMember) return true;
            }

            if (_config.UseClanTable == true)
            {
                long playerclan = BasePlayer.FindAwakeOrSleeping(playerID.ToString())?.clanId ?? 0;
                IClan clan = null;
                if (playerclan != 0 && (ClanManager.ServerInstance.Backend?.TryGet(playerclan, out clan) ?? false))
                {
                    foreach (ClanMember member in clan.Members)
                    {
                        if (member.SteamId == friendID) return true;
                    }
                }
            }

            return false;
        }

        // Check if in same team
        private bool SameTeam(ulong playerID, ulong friendID)
        {
            if (playerID==0 || friendID==0 || _config.UseTeams == false) return false;

            var teammanager = RelationshipManager.ServerInstance;
            if (teammanager == null) return false;

            var team = teammanager.FindPlayersTeam(playerID);
            if (team == null) return false;

            if (team.members.Contains(friendID)) return true;
            return false;
        }

        private void BroadcastMessage(string msg, params object[] args)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                SendChatMessage(player, msg, args);                
            }
        }

        public void SendChatMessage(BasePlayer player, string msg, params object[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is string) args[i] = (object)Lang(args[i] as string, player.UserIDString);
            }

            if (_config.useNotify && IspluginLoaded(Notify))
            {
                Notify.Call("SendNotify", player, 0, Lang(msg, player.UserIDString, args));
            }
            else
            {
                player.ChatMessage(Lang(msg, player.UserIDString, args));
            }
        }

        private void PrintToDiscord(string message, double seconds = 0)
        {
            if (_discord == null) return;

            if (_discord.MsgCooldown <= DateTime.Now.ToBinary())
            {
                _discord.MsgCooldown = DateTime.Now.AddSeconds(seconds).ToBinary();
               
                long unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();                
                if (_config.UseDiscordTimestamp)
                    _discord.SendTextMessage($"[<t:{unixTime}:t>]{Platform} {message}");
                else
                    _discord.SendTextMessage($"[{DateTime.Now.ToString("h:mm tt")}]{Platform} {message}");
            }
        }

        private string PositionToString(Vector3 position) => MapHelper.PositionToString(position);


        #endregion helpers

        //######################################################
        #region Shared.Components

        private class DiscordComponent : MonoBehaviour
        {
            private const float PostDelay = 2.5f; // set min 2 sec delay between two discord post, rate limit
            public long MsgCooldown = DateTime.Now.ToBinary();

            private readonly Queue<object> _queue = new Queue<object>();
            private string _url;
            private bool _busy = false;

            public DiscordComponent Configure(string url)
            {
                if (url == null) throw new ArgumentNullException(nameof(url));
                _url = url;

                return this;
            }

            public DiscordComponent SendTextMessage(string message, params object[] args)
            {
                message = args.Length > 0 ? string.Format(message, args) : message;
                return AddQueue(new MessageRequest(message));
            }

#region Send requests to server

            private DiscordComponent AddQueue(object request)
            {
                _queue.Enqueue(request);

                if (!_busy)
                    StartCoroutine(ProcessQueue());

                return this;
            }

            private IEnumerator ProcessQueue()
            {
                if (_busy) yield break;
                _busy = true;

                while (_queue.Count != 0)
                {
                    var request = _queue.Dequeue();
                    yield return ProcessRequest(request);
                }

                _busy = false;
            }
            
            private IEnumerator ProcessRequest(object request)
            {
                if (string.IsNullOrEmpty(_url))
                {
                    print("[ERROR] Discord webhook URL wasn't specified");
                    yield break;
                }

                var data = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
                var uh = new UploadHandlerRaw(data) { contentType = "application/json" };
                var www = UnityWebRequest.PostWwwForm(_url, UnityWebRequest.kHttpVerbPOST);
                www.uploadHandler = uh;

                yield return www.SendWebRequest();

                if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
                    print($"ERROR: {www.error} | {www.downloadHandler?.text}");

                www.Dispose();

                // to avoid spam requests to Discord
                yield return new WaitForSeconds(PostDelay);
            }            

#endregion


#region Requests
            private class MessageRequest
            {
                [JsonProperty("content")]
                public string Content { get; set; }

                public MessageRequest(string content)
                {
                    if (content == null) throw new ArgumentNullException(nameof(content));
                    Content = content;
                }
            }

#endregion
        }
#endregion

#region proximity
        // This component adds a method to evaluate if quarry is stopped from fuel or full output 
        public class QuarryDetector : MonoBehaviour
        {
            private MiningQuarry quarry;
            private bool state;

            void Awake()
            {
                quarry = GetComponent<MiningQuarry>();

                state = quarry.IsEngineOn();

                // randomize the start of the invoke to avoid server lag if all quarry evaluate at the same time
                var delay = Oxide.Core.Random.Range(0f, 2.0f);
                InvokeRepeating("CheckMiningQuarry", delay, 2.0f);
            }

            void OnDestroy() => CancelInvoke("CheckMiningQuarry");

            void CheckMiningQuarry()
            {
                if (!quarry.IsEngineOn() && state)    // switch off
                {
                    state = false;
                    notifyQuarryStopped(quarry);
                }
                else if (quarry.IsEngineOn() && !state) // switch on
                {
                    state = true;
                }
            }

            void notifyQuarryStopped(MiningQuarry quarry) => Interface.Oxide.CallHook("OnQuarryToggledOff", quarry);
        }

#endregion


    }
}