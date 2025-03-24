#define _DEBUG
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using UnityEngine.Networking;
using Facepunch;
 
// rev 0.4.25
//    some correction to the lang file

namespace Oxide.Plugins
{
    [Info("Excavator Lock", "Lorenzo", "0.4.37")]
    [Description("Lock excavator to a player/team/clan when it run")]
    class ExcavatorLock : CovalencePlugin
    {
        [PluginReference] private Plugin Clans;
        [PluginReference] private Plugin Notify;

        #region Variables

        private static ExcavatorLock _instance;
        private static DiscordComponent _discord = null;

        private const string PermissionAdmin = "excavatorlock.admin";
        private const ulong supplyDropSkinID = 234501;

        private const string PREFAB_ITEM_DROP = "assets/prefabs/misc/item drop/item_drop.prefab";

        string TraceFile = "Trace";     // file name in log directory

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
            public bool UsePermission = false;      // use permission or grant access to players

            [JsonProperty(PropertyName = "Permission")]
            public string Permission = "excavatorlock.use";   // name of permission

            [JsonProperty(PropertyName = "Allow access to excavator without permission (will not lock)")]
            public bool UseExcavatorWithoutPermission = false;

            [JsonProperty(PropertyName = "Multiplier permission")]
            public bool MultiplierPermission = false;      // use permission or grant access to every players

            [JsonProperty(PropertyName = "Permission For Multiplier")]
            public string PermissionMult = "excavatorlock.multiplier";   // name of permission for multiplier

            [JsonProperty(PropertyName = "CoolDown before releasing excavator")]
            public float CoolDown = 5;      // 5 minute cooldown after excavator finish and anybody can loot

            [JsonProperty(PropertyName = "Send Excavator available Message to All")]
            public bool MessageAll = true;

            [JsonProperty(PropertyName = "Enable engine loot after it is started to add diesel")]
            public bool engineloot = true;

            // In case clans pluginis installed but you dont want to use it with this plugin
            [JsonProperty(PropertyName = "Use Teams")]
            public bool UseTeams = true;

            // In case clans pluginis installed but you dont want to use it with this plugin
            [JsonProperty(PropertyName = "Use Clans plugin")]
            public bool UseClans = false;

            [JsonProperty(PropertyName = "Use clan table")]
            public bool UseClanTable = false;

            [JsonProperty(PropertyName = "CoolDown before a player or team can restart the excavator (0 is disabled)")]
            public float PlayerCoolDown = 30;      // 60 minute cooldown after excavator finish and anybody can loot

            [JsonIgnore]
            public float _PlayerCoolDown = 30;      // internal value

            [JsonProperty(PropertyName = "Apply cooldown to all excavators")]
            public bool CoolDownGlobal = true;      // 60 minute cooldown after excavator finish and anybody can loot

            [JsonProperty(PropertyName = "Enable fuel modifier")]
            public bool enablefuelModifier = true;

            [JsonProperty(PropertyName = "Maximum stack size for diesel engine (-1 to disable function)")]
            public int DieselFuelMaxStackSize = -1;

            [JsonProperty(PropertyName = "Enable signal Computer lock")]
            public bool enableComputeLock = true;

            [JsonProperty(PropertyName = "Enable signal Computer message")]
            public bool enableComputeMesssage = true;

            [JsonProperty(PropertyName = "Running time per fuel units (time in seconds)")]
            public float runningTimePerFuelUnit = 120f;

            [JsonProperty(PropertyName = "Sulfur production multiplier ")]
            public float SulfurMult = 1f;

            [JsonProperty(PropertyName = "HQM production multiplier")]
            public float HQMMult = 1f;

            [JsonProperty(PropertyName = "Metal production multiplier")]
            public float MetalMult = 1f;

            [JsonProperty(PropertyName = "Stone production multiplier")]
            public float StoneMult = 1f;

            [JsonProperty(PropertyName = "Excavator chat command")]
            public string excavatorquerry = "excavator";

            [JsonProperty(PropertyName = "Excavator clear status")]
            public string excavatorclearstatus = "excavatorclear";

            [JsonProperty(PropertyName = "Excavator stop command")]
            public string excavatorStop = "excavatorstop";

            [JsonProperty(PropertyName = "Empty the output piles when excavator start")]
            public bool FlushOutputPiles = false;

            [JsonProperty(PropertyName = "Charge needed for supply drop (0 to use default of 600sec)")]
            public long  chargeNeededForSupplies = 0;

            [JsonProperty(PropertyName = "Clear excavator lock after all player from team/clan disconnect ")]
            public bool ClearExcavatorLockOnAllTeamDisconnect = false;

            [JsonProperty(PropertyName = "Clear excavator lock after player owner disconnect")]
            public bool ClearExcavatorLockOnPlayerOwnerDisconnect = false;

            [JsonProperty(PropertyName = "Time after all player disconnect before excavator clear (minutes)")]
            public long CooldownExcavatorLockOnDisconnect = 10;    // default 10 minutes

            [JsonProperty(PropertyName = "Items in report list", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] ReportItems = { "stones", "metal.ore", "metal.fragments", "hq.metal.ore", "metal.refined", "sulfur.ore", "sulfur", "lowgradefuel", "crude.oil" };

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
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }

            _config.SulfurMult = Mathf.Clamp(_config.SulfurMult, 0f, 100000f);
            _config.HQMMult    = Mathf.Clamp(_config.HQMMult,   0f, 100000f);
            _config.MetalMult  = Mathf.Clamp(_config.MetalMult, 0f, 100000f);
            _config.StoneMult  = Mathf.Clamp(_config.StoneMult, 0f, 100000f);

            _config._PlayerCoolDown = Math.Max(_config.PlayerCoolDown, _config.CoolDown);

            if (_config.ClearExcavatorLockOnAllTeamDisconnect == true && _config.ClearExcavatorLockOnPlayerOwnerDisconnect == true)
            {
                PrintError("Both ClearExcavatorLockOnAllTeamDisconnect and ClearExcavatorLockOnPlayerOwnerDisconnect are true. Using team mode by default");
                _config.ClearExcavatorLockOnPlayerOwnerDisconnect = false;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

#endregion Configuration

        // ############################################################################################

#region UserInfo

        private Dictionary<ulong, ExcavatorInfo> Excavators = new Dictionary<ulong, ExcavatorInfo>();

        // dict to link all the diff component entity to the right excavator
        private Dictionary<ulong, ulong> Associate = new Dictionary<ulong, ulong>();

        private class ExcavatorInfo
        {
            public ulong playerid;
            public string displayName;
            [JsonIgnore]
            public double FuelTimeRemaining;
            [JsonIgnore]
            public bool state;      // is the excavator running

            public DateTime fueltime;
            public DateTime stoptime;
            public string Name;

            public Dictionary<ulong, CooldownTime> PlayerCooldown;

            [JsonIgnore]
            public DateTime PlayerDisconnectTime;
            [JsonIgnore]
            public int CountTick;

            [JsonIgnore]
            public ExcavatorArm Arm;

            [JsonIgnore]
            public Timer timerStopped = null;

            [JsonIgnore]
            public float[] extraMineralProduced = { 0f, 0f, 0f, 0f };
            [JsonIgnore]
            public int pileIndex = 0;

            public ExcavatorInfo()
            {
                playerid = 0;
                displayName = string.Empty;
                FuelTimeRemaining = 0f;
                state = false;
                PlayerDisconnectTime = DateTime.MaxValue;

                fueltime  = DateTime.MinValue;
                stoptime  = DateTime.MinValue;
                Name = "nobody";
                PlayerCooldown = new Dictionary<ulong, CooldownTime>();
                CountTick = 10;
                Arm = null;
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
                Excavators = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, ExcavatorInfo>>(Name);
                if (Excavators == null) throw new Exception();
            }
            catch {
                Excavators = new Dictionary<ulong, ExcavatorInfo>();
                PrintWarning($"Data file corrupted or incompatible. Reinitialising data file ");
                SaveData();
            }
        }

        private void SaveData() { Interface.Oxide.DataFileSystem.WriteObject(Name, Excavators); }

#endregion UserInfo

        // ############################################################################################

#region Localization

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ExcavatorOff"]      = "<color=#FF1500>- Excavator just turn off !</color>",
                ["ExcavatorCoolDown"] = "<color=#FF1500>- You have {0} minutes to get the ore, before it unlock to all</color>",
                ["ExcavatorInUse"]    = "<color=#FFA500>Excavator currently in use by {0}</color>",
                ["ExcavatorStartEngine"] = "Excavator started for {0,2:F0} minutes",
                ["ExcavatorStartEngineHM"] = "Excavator started for {0,2:F0} hour, {1,2:F0} minutes",
                ["ExcavatorStarted_"]  = "Excavator {0}, started by {1}. loot is protected",
                ["ExcavatorStoped_"]   = "Excavator {0}, stopped and is available to all",
                ["ExcavatorFuel"]     = "<color=#FFA500>Excavator currently running with {0} fuel in storage</color>",
                ["ExcavatorPlCooldown"] = "Excavator Player cooldown will expire in {0} minutes",
                ["ExcavatorAvailableIn1"] = "Excavator {0}, used by {2}, will be available in {1,2:F0} minutes",
                ["ExcavatorClearStatus"] = "Clearing the excavator status",
                ["ExcavatorPermission"] = "Player does not have permission to use excavator",
                ["ExcavatorSupplySignal"] = "Airdrop was activated at excavator {0} by player {1}, please do not steal",
                ["ExcavatorClearOffline"] = "Excavator {0}, cleared because team/clan player are offline",
                ["NoExcavatorFoundNear"] = "No excavator found near to apply command",
                ["MoveFuelToOutput"] = "Stop excavator, moving fuel to output pile",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ExcavatorOff"]      = "<color=#FF1500>- l'excavateur viens de s'arrêter !</color>",
                ["ExcavatorCoolDown"] = "<color=#FF1500>- Vous avez {0} minutes pour récuperer le butin avant qu'il ne soit accessible pour tous !</color>",
                ["ExcavatorInUse"]    = "<color=#FFA500>l'excavateur est utilisé par {0}.</color>",
                ["ExcavatorStartEngine"] = "L'excavateur est démarré pour {0,2:F0} minutes.",
                ["ExcavatorStartEngineHM"] = "L'excavateur est démarré pour {0,2:F0} heure(s), {1,2:F0} minutes.",
                ["ExcavatorStarted_"]  = "L'excavateur {0} a été démarré par {1}. Le butin est protégé !",
                ["ExcavatorStoped_"]   = "Arret de l'excavateur {0}. Le butin est disponible pour tous !",
                ["ExcavatorFuel"]     = "<color=#FFA500>L'excavateur a été démarré avec {0} diesel(s) dans le reservoir.</color>",
                ["ExcavatorPlCooldown"] = "Le délai d'utilisation de l'Excavateur expire dans {0} minutes.",
                ["ExcavatorAvailableIn1"] = "L'excavateur {0}, utilisé par {2}, sera disponible dans {1,2:F0} minutes.",
                ["ExcavatorClearStatus"] = "Efface le status de l'excavateur.",
                ["ExcavatorPermission"] = "Le joueur n'a pas la permission d'utiliser l'excavateur.",
                ["ExcavatorSupplySignal"] = "Largage aérien activé a l'excavator {0} par le joueur {1}, s.v.p. ne pas voler.",
                ["ExcavatorClearOffline"] = "L'excavateur {0}, est libre, le l'equipe/clan s'est deconnecté !",
                ["NoExcavatorFoundNear"] = "Aucun excavateur trouvé, deplacez vous plus pres dèun excavateur",
                ["MoveFuelToOutput"] = "Arret de l'excavateur, transfert du diesel vers pile de sortie",
            }, this, "fr");
        }

        #endregion Localization

        #region Hooks Targets
        private void Unload()
        {
            SaveData();

            if (_config.chargeNeededForSupplies > 2) ExcavatorSignalComputer.chargeNeededForSupplies = 600f; // reset to default

            foreach (var KVP in Excavators)
            { 
                if (KVP.Value.timerStopped != null)
                {
                    timer.Destroy(ref KVP.Value.timerStopped);
                }
            }

            _instance = null;
           _discord = null;
		   _config = null;
        }

        private void Init()
        {
            _instance = this;

            permission.RegisterPermission(_config.Permission, this);
            permission.RegisterPermission(_config.PermissionMult, this);
            permission.RegisterPermission(PermissionAdmin, this);

            LoadData();

            if (!_config.ClearExcavatorLockOnAllTeamDisconnect &&
                !_config.ClearExcavatorLockOnPlayerOwnerDisconnect)
            {
                Unsubscribe(nameof(OnPlayerConnected));
                Unsubscribe(nameof(OnPlayerDisconnected));
            }

            AddCovalenceCommand(_config.excavatorquerry, nameof(Excavator_info));
            AddCovalenceCommand(_config.excavatorclearstatus, nameof(Excavator_clearstatus));
            AddCovalenceCommand(_config.excavatorStop, nameof(Excavator_stopcommand));
        }

        private void OnServerInitialized(bool initial)
        {
            if (_config.Discordena && !string.IsNullOrEmpty(_config.DiscordHookUrl))
            {
                var loader = new GameObject("WebObject");
                _discord = loader.AddComponent<DiscordComponent>().Configure(_config.DiscordHookUrl);
                PrintToLog($"Plugin Excavator Lock restarted");
            }

            // limit the range between 10 sec and 10 minutes per barrel 
            if (_config.runningTimePerFuelUnit < 10f)
            {
                PrintWarning("Warning, config.runningTimePerFuelUnit modifier is disabled. (runningTimePerFuelUnit < 10)");
                _config.runningTimePerFuelUnit = 10f;
            }

            if (_config.runningTimePerFuelUnit > 6000f)
            {
                PrintWarning("Warning, config.runningTimePerFuelUnit set to maximum at 6000 sec. (runningTimePerFuelUnit > 6000)");
                _config.runningTimePerFuelUnit = 6000f;
            }
            
            // this is just in case user think its in second and put a large value 
            if (_config.PlayerCoolDown > 120)
            {
                PrintWarning($"Configuration cooldown period {_config.PlayerCoolDown} minutes");
            }

            List<DieselEngine> engines = Pool.Get<List<DieselEngine>>();
            List<ExcavatorSignalComputer> Computes = Pool.Get<List<ExcavatorSignalComputer>>();
            engines.Clear();
            Computes.Clear();
            // count the excavator engine on the map
            foreach (var entarm in BaseNetworkable.serverEntities)
            {
                var arm = entarm as ExcavatorArm;
                if (arm != null)
                {
                    Vis.Entities<DieselEngine>(arm.transform.position, 20f, engines, -1);
                    Vis.Entities<ExcavatorSignalComputer>(arm.transform.position, 20f, Computes, -1);

                    ExcavatorInfo excavatorinfo;
                    if (!Excavators.TryGetValue(arm.net.ID.Value, out excavatorinfo))
                    {
                        excavatorinfo = new ExcavatorInfo();                        
                        Excavators.Add(arm.net.ID.Value, excavatorinfo);
                    }
                    excavatorinfo.Name = PositionToString(arm.transform.position);
                    excavatorinfo.Arm = arm;

                    // Associate engines to excavator in dictionary
                    foreach (var engine in engines)
                    {
                        // experiemntal max stack size
                        if (_config.DieselFuelMaxStackSize >= 0) engine.inventory.maxStackSize = _config.DieselFuelMaxStackSize;

                        excavatorinfo.state = engine.IsOn();

                        Associate.Add(engine.net.ID.Value, arm.net.ID.Value);
                        if (_config.enablefuelModifier) engine.runningTimePerFuelUnit = _config.runningTimePerFuelUnit;

                        if (excavatorinfo.state) excavatorinfo.FuelTimeRemaining = CalcFuelTime(engine);
                        else excavatorinfo.FuelTimeRemaining = 0;
                        excavatorinfo.fueltime = DateTime.Now.AddMinutes(excavatorinfo.FuelTimeRemaining);

                        break; // only one engine per excavator, ignore duplicates
                    }

                    // Associate outputpiles to excavator in dictionary
                    foreach (var outputpile in arm.outputPiles)
                    {
                        Associate.Add(outputpile.net.ID.Value, arm.net.ID.Value);
                    }


                    if (_config.chargeNeededForSupplies > 2) ExcavatorSignalComputer.chargeNeededForSupplies = (float)_config.chargeNeededForSupplies;

                    // Associate engines to excavator in dictionary
                    foreach (var compute in Computes)
                    {
                        Associate.Add(compute.net.ID.Value, arm.net.ID.Value);
                        break; // only one compute, ignore duplicates
                    }
                }
                engines.Clear();
                Computes.Clear();
            }
            Pool.FreeUnmanaged(ref engines);
            Pool.FreeUnmanaged(ref Computes);

            NextTick(() =>
            {
                // Check if the excavator entry exist 
                // If not remove it. This append when server wipe
                List<ulong> removals = new List<ulong>();
                foreach (KeyValuePair<ulong, ExcavatorInfo> kvp in  Excavators)
                {
                    if (kvp.Value.Arm == null)
                    {
                        PrintWarning($"Removing excavator info for {kvp.Key} ");
                        removals.Add(kvp.Key);
                    }
                }

                // cleanup dict entry of unused info
                foreach (ulong removeid in removals)
                {
                    Excavators.Remove(removeid);
                }

                if (_config.UseClans && !IspluginLoaded(Clans))
                {
                    PrintWarning("Optional Clans plugin not found. Clans disabled");
                }

                if (_config.useNotify && !IspluginLoaded(Notify))
                {
                    PrintWarning("Optional Notify plugin not found. Notify disabled");
                }
            });
        }

        object OnDieselEngineToggle(DieselEngine engine, BasePlayer player)
        {
            if (engine.skinID != 0UL)
                return null;
				
            ulong armID;
            ExcavatorInfo excavatorinfo = null;

            if (!Associate.TryGetValue(engine.net.ID.Value, out armID)) return null;  // allow if engine not associated with excavator
            if (!Excavators.TryGetValue(armID, out excavatorinfo)) return true;  // 

            bool perm = CanPlayerUse((ulong)player.userID);
            if (!perm && !_config.UseExcavatorWithoutPermission)
            {
                SendChatMessage(player, "ExcavatorPermission");
                return true;
            }

            // check if engine is off. 
            if (!engine.IsOn())
            {
                var checkcooldown = CheckPlayerCooldown((ulong)player.userID, excavatorinfo);

                // if Player cooldown is active, dont allow excavator restart
                if (checkcooldown)
                {
                    SendChatMessage(player, "ExcavatorPlCooldown", PlayerCooldownTime((ulong)player.userID, excavatorinfo));
                    return true;
                }

                // If excavator is captured, dont allow it to restart
                if (!IsExcavatorAvailable(excavatorinfo))
                {
                    SendChatMessage(player, "ExcavatorInUse", excavatorinfo.displayName);
                    return true;    // do not start excavator
                }

                if (perm)
                {
                    excavatorinfo.playerid = (ulong)player.userID;
                    excavatorinfo.displayName = player.displayName;
                    excavatorinfo.PlayerDisconnectTime = DateTime.MaxValue;
                }
                else
                {
                    excavatorinfo.playerid = 0;
                    excavatorinfo.displayName = "nobody";
                    excavatorinfo.PlayerDisconnectTime = DateTime.MaxValue;
                }

                PrintToLog($"Excavator engine {excavatorinfo.Name} started at : {DateTime.Now.ToString("hh:mm tt")}");
                return null;
            }

            return null;
        }

        double CalcFuelTime(DieselEngine engine) => (engine.cachedFuelTime + engine.runningTimePerFuelUnit * (float)engine.GetFuelAmount()) / 60f;

        // The engine was toggled. 
        void OnDieselEngineToggled(DieselEngine engine)
        {
            if (engine.skinID != 0UL)
                return;

            ulong armID;
            ExcavatorInfo excavatorinfo = null;

            if (!Associate.TryGetValue(engine.net.ID.Value, out armID)) return;  // 
            if (!Excavators.TryGetValue(armID, out excavatorinfo)) return;  // 

            excavatorinfo.state = engine.IsOn();
            if (excavatorinfo.state)
            {
                if (excavatorinfo.playerid != 0)
                {
                    // Excavator state is On
                    excavatorinfo.FuelTimeRemaining = CalcFuelTime(engine);
                    excavatorinfo.fueltime = DateTime.Now.AddMinutes(excavatorinfo.FuelTimeRemaining);
                    excavatorinfo.PlayerDisconnectTime = DateTime.MaxValue;

                    BasePlayer player = BasePlayer.FindByID(excavatorinfo.playerid);
                    if (player != null)
                    {
                        if (excavatorinfo.FuelTimeRemaining < 60) SendChatMessage(player, "ExcavatorStartEngine", Math.Ceiling(excavatorinfo.FuelTimeRemaining));
                        else SendChatMessage(player, "ExcavatorStartEngineHM", excavatorinfo.FuelTimeRemaining / 60, excavatorinfo.FuelTimeRemaining % 60);

                        if (_config.MessageAll)
                        {
                            BroadcastMessage("ExcavatorStarted_", excavatorinfo.Name, player.displayName);
                        }

                    }

                    // Start 
                    AddPlayerCooldown(excavatorinfo.playerid, excavatorinfo.FuelTimeRemaining, excavatorinfo);

                    if (_config.FlushOutputPiles)
                    {
                        var arm = excavatorinfo.Arm;
                        if (arm != null)
                        {
                            int cnt = 1;
                            foreach (var piles in arm.outputPiles)
                            {
                                var storage = piles.GetComponent<StorageContainer>();
                                PrintToLog($"Flush {cnt}/2 {ReportInventory(storage)} items from loot pile");
                                DropItemContainer(storage.inventory, piles.transform.position, Quaternion.identity);
                                cnt++;
                            }
                        }
                    }
                    if (excavatorinfo.timerStopped != null)
                    {
                        timer.Destroy(ref excavatorinfo.timerStopped);
                    }
                }
            }
            else
            {
                // Excavator state is Off
                if (excavatorinfo.playerid != 0)
                {
                    excavatorinfo.stoptime = DateTime.Now;
                    // Update cooldown info when the excavator stop
                    AddPlayerCooldown(excavatorinfo.playerid, 0f, excavatorinfo);

                    // player will be null when disconnected and wont receive message
                    var player = BasePlayer.FindByID(excavatorinfo.playerid);
                    if (player != null)
                    {
                        SendChatMessage(player, "ExcavatorOff");
                        SendChatMessage(player, "ExcavatorCoolDown", _config.CoolDown);
                    }

                    PrintToLog($"Excavator {excavatorinfo.Name} stopped at : {DateTime.Now.ToString("hh:mm tt")}");

                    if (excavatorinfo.timerStopped != null)
                    {
                        timer.Destroy(ref excavatorinfo.timerStopped);
                    }
                    excavatorinfo.timerStopped = timer.Once(_config.CoolDown * 60f, () =>
                    {
                        // In case excavator is resarted after cooldown
                        if (excavatorinfo!=null && !excavatorinfo.state)
                        {
                            if (_config.MessageAll) BroadcastMessage("ExcavatorStoped_", excavatorinfo.Name);
                            excavatorinfo.playerid = 0;
                            excavatorinfo.displayName = "nobody";
                            excavatorinfo.PlayerDisconnectTime = DateTime.MaxValue;
                            excavatorinfo.timerStopped = null;
                        }                            
                    });
                }
            }

            SaveData();
        }

        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (container.skinID != 0UL)
                return null;

            // ID excavator_output_pile
            // ID engine
            if (container.prefabID == 673116596) return LootExcavatorOutputPile(player, container);    // "excavator_output_pile"
            else if (container.prefabID == 2982299738) return LootDieselEngine(player, container);     // "engine"
            else if (container.prefabID == 3632568684) return LootAirdrop(player, container);     // Airdrop
            return null;
        }

        void OnLootEntityEnd(BasePlayer player, StorageContainer container)
        {
            if (container.skinID != 0UL)
                return;

            if (container.prefabID == 2982299738) LootDieselEngineEnd(player, container);   // "engine"
        }

        void LootDieselEngineEnd(BasePlayer player, StorageContainer container)
        {
            if (container.skinID != 0UL)
                return;

            ulong armID;
            ExcavatorInfo excavatorinfo = null;

            var engine = container as DieselEngine;
            if (engine == null) return;

            if (!Associate.TryGetValue(engine.net.ID.Value, out armID)) return;
            if (!Excavators.TryGetValue(armID, out excavatorinfo)) return;

            if (excavatorinfo.state == true)
            {
                excavatorinfo.FuelTimeRemaining = CalcFuelTime(engine);
                excavatorinfo.fueltime = DateTime.Now.AddMinutes(excavatorinfo.FuelTimeRemaining);
                AddPlayerCooldown(excavatorinfo.playerid, excavatorinfo.FuelTimeRemaining, excavatorinfo);
            }

        }



        object LootExcavatorOutputPile(BasePlayer player, StorageContainer container)
        {
            ulong armID;
            ExcavatorInfo excavatorinfo = null;

            if (!Associate.TryGetValue(container.net.ID.Value, out armID)) return true;  // 
            if (!Excavators.TryGetValue(armID, out excavatorinfo)) return true;  // 

            if (IsAdmin((ulong)player.userID) || 
                excavatorinfo.playerid == 0 || 
                IsExcavatorAvailable(excavatorinfo))
            {
                PrintToLog($"Player {player.displayName}/{(ulong)player.userID} looting pile not locked    loc:{excavatorinfo.Name}, {ReportInventory(container)} items in pile");
                return null;
            }

            if (((ulong)player.userID == excavatorinfo.playerid) ||
                (SameTeam((ulong)player.userID, excavatorinfo.playerid)) ||
                (SameClan((ulong)player.userID, excavatorinfo.playerid)))
            {
                PrintToLog($"Player {player.displayName}/{(ulong)player.userID} looting pile owned by:{excavatorinfo.displayName}/{excavatorinfo.playerid}  loc:{excavatorinfo.Name}, {ReportInventory(container)} item in pile");
                return null;
            }

            SendChatMessage(player, "ExcavatorInUse", excavatorinfo.displayName);
            return true;
        }

        object LootDieselEngine(BasePlayer player, StorageContainer container)
        {
            if (container.skinID != 0UL)
                return null;

            ulong armID;
            ExcavatorInfo excavatorinfo = null;

            if (!Associate.TryGetValue(container.net.ID.Value, out armID)) return null;  // ignore engine not associated
            if (!Excavators.TryGetValue(armID, out excavatorinfo)) return true;  // 

            if (IsAdmin((ulong)player.userID) ||
                excavatorinfo.playerid == 0 ||
                (IsExcavatorAvailable(excavatorinfo) && !CheckPlayerCooldown((ulong)player.userID, excavatorinfo)))
            {
                PrintToLog($"Player {player.displayName}/{(ulong)player.userID} Looting engine, not locked  pos:{excavatorinfo.Name}");
                return null;
            }
             
            if (((ulong)player.userID == excavatorinfo.playerid) ||
                (SameTeam((ulong)player.userID, excavatorinfo.playerid)) ||
                (SameClan((ulong)player.userID, excavatorinfo.playerid)))
            {
                if (!_config.engineloot) // cannot loot diesel engine if current user
                {
                    var amount = (container.inventory.itemList.Count != 0) ? container.inventory.itemList[0].amount + 1 : 1;
                    if (excavatorinfo.state) SendChatMessage(player, "ExcavatorFuel", amount.ToString());
                    else
                    {
                        SendChatMessage(player, "ExcavatorAvailableIn1", excavatorinfo.Name, PlayerCooldownTime((ulong)player.userID, excavatorinfo), excavatorinfo.displayName);
                    }
                    return true;   // disable engine loot
                }

                PrintToLog($"Player {player.displayName}/{(ulong)player.userID} Looting engine, locked to {excavatorinfo.displayName}/{excavatorinfo.playerid}  pos:{excavatorinfo.Name}");
                return null;
            }

            SendChatMessage(player, "ExcavatorInUse", excavatorinfo.displayName);
            return true;
        }

        object LootAirdrop(BasePlayer player, StorageContainer container)
        {
            ulong armID;
            ExcavatorInfo excavatorinfo;

            List<ExcavatorSignalComputer> Computes = Pool.Get<List<ExcavatorSignalComputer>>();
            Computes.Clear();
            Vis.Entities<ExcavatorSignalComputer>(container.transform.position, 90f, Computes, -1);

            if (Computes.Count != 0)
            {
                if (Associate.TryGetValue(Computes[0].net.ID.Value, out armID))
                {
                    if (Excavators.TryGetValue(armID, out excavatorinfo))
                    {
                        PrintToLog($"Player {player.displayName}/{(ulong)player.userID} Looting supply drop near excavator, locked to {excavatorinfo.displayName}/{excavatorinfo.playerid}  pos:{excavatorinfo.Name}  dist:{Vector3.Distance(Computes[0].transform.position, container.transform.position).ToString("F1")}");
                    }
                }
            }
            Pool.FreeUnmanaged(ref Computes);
            return null;
        }


        object OnExcavatorResourceSet(ExcavatorArm arm, string resourceName, BasePlayer player)
        {
            ExcavatorInfo excavatorinfo;
            if (!Excavators.TryGetValue(arm.net.ID.Value, out excavatorinfo)) return true;

            if (IsAdmin((ulong)player.userID) ||
                excavatorinfo.playerid == 0 ||
                IsExcavatorAvailable(excavatorinfo))
            {
                PrintToLog($"Excavator {excavatorinfo.Name} started at : {DateTime.Now.ToString("hh:mm tt")} for {excavatorinfo.FuelTimeRemaining} minutes, not locked to player {excavatorinfo.displayName}/{excavatorinfo.playerid}. Mining {resourceName}");
                return null;
            }

            // check if a different player/team/clan is trying to use excavator
            if (!(((ulong)player.userID == excavatorinfo.playerid) ||
                 (SameTeam((ulong)player.userID, excavatorinfo.playerid)) ||
                 (SameClan((ulong)player.userID, excavatorinfo.playerid))))
            {
                SendChatMessage(player, "ExcavatorInUse", excavatorinfo.displayName);
                return true;
            }

            PrintToLog($"Excavator {excavatorinfo.Name} started at : {DateTime.Now.ToString("hh:mm tt")} for {excavatorinfo.FuelTimeRemaining} minutes, by player {excavatorinfo.displayName}/{excavatorinfo.playerid}. Mining {resourceName}");

            return null;
        }

        object OnExcavatorGather(ExcavatorArm arm, Item item)
        {
            float multiplier = 1f;
            ExcavatorInfo excavatorinfo;
            if (!Excavators.TryGetValue(arm.net.ID.Value, out excavatorinfo)) return null;
            if (excavatorinfo.pileIndex == 0) excavatorinfo.pileIndex = excavatorinfo.Arm.outputPiles.Count - 1;
            else excavatorinfo.pileIndex--;

            if (excavatorinfo.playerid!=0 && IsMultiplierActive(excavatorinfo.playerid))
            {
                if (item.info.shortname == "sulfur.ore" && _config.SulfurMult != 0f) multiplier = _config.SulfurMult;
                else if (item.info.shortname == "hq.metal.ore" && _config.HQMMult != 0f) multiplier = _config.HQMMult;
                else if (item.info.shortname == "metal.fragments" && _config.MetalMult != 0f) multiplier = _config.MetalMult;
                else if (item.info.shortname == "stones" && _config.StoneMult != 0f) multiplier = _config.StoneMult;
            }

            float tmp = excavatorinfo.extraMineralProduced[excavatorinfo.pileIndex] + ((float)item.amount * (multiplier -1f));
            float tmp2 = Mathf.Floor(tmp);
            item.amount = Math.Max(0, item.amount + (int)tmp2);
            excavatorinfo.extraMineralProduced[excavatorinfo.pileIndex] = tmp - tmp2;

            // Evaluate every ~10 sec.
            if ((_config.ClearExcavatorLockOnAllTeamDisconnect || _config.ClearExcavatorLockOnPlayerOwnerDisconnect) && --excavatorinfo.CountTick == 0)
            {
                excavatorinfo.CountTick = 10;

                if (excavatorinfo.PlayerDisconnectTime != DateTime.MaxValue)
                {
                    if (excavatorinfo.PlayerDisconnectTime.AddMinutes(_config.CooldownExcavatorLockOnDisconnect) <= DateTime.Now)
                    {
                        // Clear quarry lock
                        if (_config.ClearExcavatorLockOnPlayerOwnerDisconnect)
                        {
                            PrintToLog($"Excavator unlocked because player owner disconnected for {_config.CooldownExcavatorLockOnDisconnect} minutes {excavatorinfo.displayName}/{excavatorinfo.playerid} ");
                        }
                        if (_config.ClearExcavatorLockOnAllTeamDisconnect)
                        {
                            PrintToLog($"Excavator unlocked because all team/clan disconnected for {_config.CooldownExcavatorLockOnDisconnect} minutes {excavatorinfo.displayName}/{excavatorinfo.playerid} ");
                        }

                        if (_config.MessageAll) BroadcastMessage("ExcavatorClearOffline", excavatorinfo.Name);
                        excavatorinfo.PlayerDisconnectTime = DateTime.MaxValue;
                        ClearExcavator(excavatorinfo, false);
                    }
                }
            }
            return null;
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            foreach (KeyValuePair<ulong, ExcavatorInfo> excav in Excavators)
            {
                foreach (var kvp in excav.Value.PlayerCooldown)
                {
                    if (_config.ClearExcavatorLockOnPlayerOwnerDisconnect)
                    {
                        if ((ulong)player.userID == kvp.Key && (ulong)player.userID == excav.Value.playerid)
                        {
                            kvp.Value.IsOnline = true;
                            excav.Value.PlayerDisconnectTime = DateTime.MaxValue;
                            PrintToLog($"Player connected {player.displayName}/{(ulong)player.userID} at {DateTime.Now}   isconnected:{BasePlayer.FindAwakeOrSleeping(player.UserIDString)?.IsConnected ?? false}");

                        }
                    }

                    if (_config.ClearExcavatorLockOnAllTeamDisconnect)
                    {
                        if ((ulong)player.userID == kvp.Key)
                        {
                            kvp.Value.IsOnline = true;
                            excav.Value.PlayerDisconnectTime = DateTime.MaxValue;
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

            foreach (KeyValuePair<ulong, ExcavatorInfo> excav in Excavators)
            {
                if (_config.ClearExcavatorLockOnPlayerOwnerDisconnect) PlayerCount = 1;
                else PlayerCount = excav.Value.PlayerCooldown.Count;

                foreach (var kvp in excav.Value.PlayerCooldown)
                {
                    if (_config.ClearExcavatorLockOnPlayerOwnerDisconnect)
                    {
                        if ((ulong)player.userID == kvp.Key && (ulong)player.userID == excav.Value.playerid)
                        {
                            kvp.Value.IsOnline = false;
                            PlayerCount--;
                            PrintToLog($"Player disconnected {player.displayName}/{(ulong)player.userID} at {DateTime.Now}   isconnected:{BasePlayer.FindAwakeOrSleeping(player.UserIDString)?.IsConnected ?? false}");
                        }
                    }

                    if (_config.ClearExcavatorLockOnAllTeamDisconnect)
                    {
                        if (kvp.Key == (ulong)player.userID)
                        {
                            kvp.Value.IsOnline = false;
                            PrintToLog($"Player disconnected {player.displayName}/{(ulong)player.userID} at {DateTime.Now}   isconnected:{BasePlayer.FindAwakeOrSleeping(player.UserIDString)?.IsConnected ?? false}");
                        }
                        if (!kvp.Value.IsOnline || kvp.Value.IgnoreOnline) PlayerCount--;
                    }
                }

                if (PlayerCount == 0)
                {
                    excav.Value.PlayerDisconnectTime = DateTime.Now;
                    PrintToLog($"Activate disconnect countdown {player.displayName}/{(ulong)player.userID} at {DateTime.Now}  {excav.Value.Name} by {excav.Value.displayName}/{excav.Value.playerid} ");
                }
            }
        }


        bool CheckPlayerCooldown(ulong playerID, ExcavatorInfo excavatorinfo)
        {
            List<ulong> removals = new List<ulong>();
            DateTime now = DateTime.Now;
            bool result = false;

            if (_config.CoolDownGlobal)
            {
                foreach (KeyValuePair<ulong, ExcavatorInfo> excav in Excavators)
                {
                    foreach (KeyValuePair<ulong, CooldownTime> kvp in excav.Value.PlayerCooldown)
                    {
                        if (now >= kvp.Value.Time) removals.Add(kvp.Key);
                        if (kvp.Key == playerID && now < kvp.Value.Time) result=true;
                    }

                    // cleanup dict entry of older time info
                    foreach (ulong removeid in removals)
                    {
                        excav.Value.PlayerCooldown.Remove(removeid);
                    }
                    removals.Clear();
                }
            }
            else
            {
                foreach (KeyValuePair<ulong, CooldownTime> kvp in excavatorinfo.PlayerCooldown)
                {
                    if (now >= kvp.Value.Time) removals.Add(kvp.Key);
                    if (kvp.Key == playerID && now < kvp.Value.Time) result=true;
                }

                // cleanup dict entry of older time info
                foreach (ulong removeid in removals)
                {
                    excavatorinfo.PlayerCooldown.Remove(removeid);
                }
            }

            return result;
        }

        int PlayerCooldownTime(ulong playerID, ExcavatorInfo excavatorinfo)
        {
            DateTime now = DateTime.Now;
            CooldownTime cooldown;

            if (_config.CoolDownGlobal)
            {
                foreach (KeyValuePair<ulong, ExcavatorInfo> excav in Excavators)
                {
                    if (excav.Value.PlayerCooldown.TryGetValue(playerID, out cooldown) && now < cooldown.Time)
                    {
                        var diff = cooldown.Time.Subtract(now);
                        return (int)Math.Ceiling(diff.TotalMinutes);
                    }
                }
            }
            else
            {
                if (excavatorinfo.PlayerCooldown.TryGetValue(playerID, out cooldown) && now < cooldown.Time)
                {
                    var diff = cooldown.Time.Subtract(now);
                    return (int)Math.Ceiling(diff.TotalMinutes);
                }
            }
            return 0;
        }

        void AddPlayerCooldown(ulong playerID, double extra, ExcavatorInfo excavatorinfo)
        {
            var teammanager = RelationshipManager.ServerInstance;
            foreach (KeyValuePair<ulong, CooldownTime> kvp in excavatorinfo.PlayerCooldown)
            {
                kvp.Value.IgnoreOnline = true;
            }

            //player.IsConnected
            CooldownTime CoolDown = new CooldownTime();
            CoolDown.IsOnline = BasePlayer.FindAwakeOrSleeping(playerID.ToString())?.IsConnected ?? false;
            CoolDown.Time =  DateTime.Now.AddMinutes(_config._PlayerCoolDown + extra);
            AddSinglePlayerCooldown(playerID, excavatorinfo, CoolDown);

            if (CoolDown.IsOnline == false)
            {
                PrintWarning($"Player {playerID} just activated quarry, but is flagged Offline. Something is wrong ");
                PrintToLog($"Player {playerID} just activated quarry, but is flagged Offline. Something is wrong ");
            }

            // If enable, Process all team members
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
                        AddSinglePlayerCooldown(ent, excavatorinfo, CoolDown);
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
                            AddSinglePlayerCooldown(ulong.Parse(ent), excavatorinfo, CoolDown);
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
                                AddSinglePlayerCooldown(member.SteamId, excavatorinfo, CoolDown);
                            }
                        }
                    }
                }
            }
        }

        void AddSinglePlayerCooldown(ulong playerID, ExcavatorInfo excavatorinfo, CooldownTime Cooldown)
        {
			// filter some bogus 0 entry comming from Team and Clan plugin
            if (playerID == 0) return;
            if (excavatorinfo.PlayerCooldown.ContainsKey(playerID))
            {
                excavatorinfo.PlayerCooldown[playerID] = Cooldown;
            }
            else
            {
                excavatorinfo.PlayerCooldown.Add(playerID, Cooldown);
            }
        }

        object OnExcavatorSuppliesRequest(ExcavatorSignalComputer computer, BasePlayer player)
        {		
            if (player == null || computer == null || !_config.enableComputeLock) return null;

            if (IsAdmin((ulong)player.userID) == true) return null;

            ulong armID;
            ExcavatorInfo excavatorinfo = null;
			if (!Associate.TryGetValue(computer.net.ID.Value, out armID)) return true;  //
            if (!Excavators.TryGetValue(armID, out excavatorinfo)) return null;  // 

            if (excavatorinfo.playerid != 0 && !IsExcavatorAvailable(excavatorinfo))
            {

                if ((ulong)player.userID == excavatorinfo.playerid) return null;
                if ((SameTeam((ulong)player.userID, excavatorinfo.playerid)) ||
                   (SameClan((ulong)player.userID, excavatorinfo.playerid))) return null;

                SendChatMessage(player, "ExcavatorInUse", excavatorinfo.displayName);

                return true;
            }

            return null;
        }

        void OnExcavatorSuppliesRequested(ExcavatorSignalComputer computer, BasePlayer player, CargoPlane plane)
        {
            if (player == null || computer == null || plane==null) return;

            ulong armID;
            ExcavatorInfo excavatorinfo = null;
            if (!Associate.TryGetValue(computer.net.ID.Value, out armID)) return;
            if (!Excavators.TryGetValue(armID, out excavatorinfo)) return;

            if (_config.enableComputeMesssage && excavatorinfo.playerid!=0) BroadcastMessage("ExcavatorSupplySignal", excavatorinfo.Name, excavatorinfo.displayName);
            // tag plane like loot defender 
            PrintToLog($"Supply signal activated at excavator {excavatorinfo.Name} by {player.displayName}/{(ulong)player.userID}");

            plane.OwnerID = (ulong)player.userID;
            plane.skinID = supplyDropSkinID;
        }

#endregion Hooks

#region Commands

        private void Excavator_info(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (player == null) return;

            foreach (var excator in Excavators)
            {
                var remaining = Math.Max(0, ((excator.Value.fueltime - DateTime.Now).TotalMinutes) + _config.CoolDown );
                if (!IsExcavatorAvailable(excator.Value))
                {
                    PrintToLog($" {excator.Value.Name} remaining time {remaining}");
                    SendChatMessage(player, "ExcavatorAvailableIn1", excator.Value.Name, Math.Ceiling(remaining), excator.Value.displayName);
                }
                else
                {
                    PrintToLog($" {excator.Value.Name} not running");
                    SendChatMessage(player, "ExcavatorStoped_", excator.Value.Name);
                }

                var checkcooldown = CheckPlayerCooldown((ulong)player.userID, excator.Value);
                if (checkcooldown)
                {
                    SendChatMessage(player, "ExcavatorPlCooldown", PlayerCooldownTime((ulong)player.userID, excator.Value));
                }
            }

            return;
        }

        private void Excavator_clearstatus(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (iplayer.Id == "server_console" ||
                (player != null && IsAdmin((ulong)player.userID)))
            {
                foreach (var excator in Excavators)
                {
                    ClearExcavator(excator.Value);
                }

                if (player != null)
                {
                    SendChatMessage(player, "ExcavatorClearStatus");
                    Puts(Lang("ExcavatorClearStatus"));
                }
                else
                {
                    Puts(Lang("ExcavatorClearStatus", "0"));
                }

                SaveData();

                PrintToLog($"Excavator clear status {iplayer.Id} ");
            }

            return;
        }

        private void Excavator_stopcommand(IPlayer iplayer, string command, string[] args)
        {
            var player = iplayer.Object as BasePlayer;
            if (player == null) return;

            DieselEngine dieselEngine = null;
            List<DieselEngine> engines = Pool.Get<List<DieselEngine>>();
            Vis.Entities<DieselEngine>(player.transform.position, 120f, engines, -1);
            if (engines.Count > 0)
            {
                dieselEngine = engines[0];
                var distance = Vector3Ex.Distance2D(player.transform.position, dieselEngine.transform.position);
            }
            Pool.FreeUnmanaged(ref engines);

            ulong armID;
            ExcavatorInfo excavatorinfo = null;
            if (!Associate.TryGetValue(dieselEngine.net.ID.Value, out armID)) return;  // allow if engine not associated with excavator
            if (!Excavators.TryGetValue(armID, out excavatorinfo)) return;  // 

            if (dieselEngine.skinID != 0UL ||
                excavatorinfo == null)
            {
                SendChatMessage(player, "NoExcavatorFoundNear");
                return;
            }

            int fuelmoved = 0;
            if (((ulong)player.userID == excavatorinfo.playerid) ||
                (SameTeam((ulong)player.userID, excavatorinfo.playerid)) ||
                (SameClan((ulong)player.userID, excavatorinfo.playerid)))
            {
                List<Item> items = dieselEngine.inventory.FindItemsByItemID(1568388703);    // lowgradefuel == -946369541, Diesel == 1568388703
                foreach (Item item in items)
                {
                    if (!item.MoveToContainer(excavatorinfo.Arm.outputPiles[0].inventory, -1, true, false, null, true))
                    {
                        item.Drop(excavatorinfo.Arm.outputPiles[0].GetDropPosition(), excavatorinfo.Arm.outputPiles[0].GetDropVelocity(), default(Quaternion));
                    }
                    fuelmoved += item.amount;
                }
                SendChatMessage(player, "MoveFuelToOutput");
                PrintToLog($"Excavator stop command by {player.displayName}/{(ulong)player.userID}  at {excavatorinfo.Name}. moving {fuelmoved} fuel ");
            }
            else SendChatMessage(player, "NoExcavatorFoundNear");

            return;
        }


        #endregion Commands

        #region Helpers


        void ClearExcavator(ExcavatorInfo excavator, bool clearcooldownlist = true)
        {
            excavator.playerid = 0;
            excavator.displayName = string.Empty;
            excavator.FuelTimeRemaining = 0f;
            excavator.state = false;
            excavator.PlayerDisconnectTime = DateTime.MaxValue;
            excavator.fueltime = DateTime.MinValue;
            excavator.stoptime = DateTime.MinValue;
            if (clearcooldownlist) excavator.PlayerCooldown = new Dictionary<ulong, CooldownTime>();
        }

        bool IsExcavatorAvailable(ExcavatorInfo excavatorinfo)
        {
            // check if excavator is off longer then cooldown
            if ((excavatorinfo.state == false) && ((DateTime.Now - excavatorinfo.stoptime).TotalMinutes > _config.CoolDown))
            {
                // enough time elapse, allow loot
                excavatorinfo.playerid = 0;
                excavatorinfo.displayName = "nobody";
                excavatorinfo.PlayerDisconnectTime = DateTime.MaxValue;
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
            (permission.UserHasPermission(id.ToString(), PermissionAdmin));
        private bool IsAdmin(string id) =>
            (permission.UserHasPermission(id, PermissionAdmin));


        private bool CanPlayerUse(ulong id) =>
            (!_config.UsePermission || permission.UserHasPermission(id.ToString(), _config.Permission) || permission.UserHasPermission(id.ToString(), PermissionAdmin));
        private bool CanPlayerUse(string id) =>
            (!_config.UsePermission || permission.UserHasPermission(id, _config.Permission) || permission.UserHasPermission(id, PermissionAdmin));

        private bool IsMultiplierActive(ulong id) =>
            (!_config.MultiplierPermission || permission.UserHasPermission(id.ToString(), _config.PermissionMult));
        private bool IsMultiplierActive(string id) =>
            (!_config.MultiplierPermission || permission.UserHasPermission(id, _config.PermissionMult));

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
				
				DateTime currentTime = DateTime.UtcNow;
				long unixTime = ((DateTimeOffset)currentTime).ToUnixTimeSeconds();
				if (_config.UseDiscordTimestamp)
					_discord.SendTextMessage($"[<t:{unixTime}:t>]{Platform} " + message);
				else
			        _discord.SendTextMessage($"[{DateTime.Now.ToString("h:mm tt")}]{Platform} " + message);
            }
        }

        private string PositionToString(Vector3 position) => MapHelper.PositionToString(position);
        // correction for grid offset vs FP code


        bool IspluginLoaded(Plugin a) => (a?.IsLoaded ?? false);

#endregion helpers

        //######################################################

#region Shared.Components

        private class DiscordComponent : MonoBehaviour
        {
            private const float PostDelay = 2.0f; // set 2 sec min delay between two discord post
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

                while (_queue.Count!=0)
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

                byte[]  data = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(request));
                UploadHandlerRaw uh = new UploadHandlerRaw(data) { contentType = "application/json" };
                UnityWebRequest www = UnityWebRequest.PostWwwForm(_url, UnityWebRequest.kHttpVerbPOST);
                www.uploadHandler = uh;

                yield return www.SendWebRequest();

                if  (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
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
    }
}
