using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Facepunch;
using Rust;
using Rust.Ai;
using HarmonyLib;

#region Changelogs and ToDo

/**********************************************************************
* 
*   1.0.0   -   initial release
*   2.0.0   -   Rewrite V2
*   2.1.0   -   Patched for compile after Rust Update removing various AI
*   3.0.0   -   Rewrite V3
*           -   Added Scarecrow Attack Behaviour to be less stuck on targets
*           -   Added scientist and scarecrows Roaming Behaviour
*           -   Added support for kits for scientist and scarecrows
*           -   Added spawn amount for for scientist and scarecrows
*           -   Added scientist and scarecrows to get their prefix(title) + random name assigned
*           -   Added Seperate prefix for scarecrow with a chainsaw
*           -   Airdrops skips spawn if the barrel is not outside
*               and will gives the player a supply.signal or drops it on the floor
*               if player inventory is full
*           -   Added amount of animals spawning per animal type
*           -   Added Health settings per animal type
*           -   If more then 1 npc or animal are set to spawn they will give a different chat message
*           -   Added F1Grenade to the explosives spawn list
*   3.0.1   -   Added support for PolarBears
*   3.0.2   -   Added Global setting to only spawn outside
*               Added animal life duration
*               Added permission system
*   3.0.3   -   Added Max range to trigger events to the config
*   3.0.4   -   Fix for overload issue
*   3.1.0   -   Removed barrelless.globaltrigger permission
*               Added permission barrelless.exclude(excludes player from event triggers)
*               Added permission barrelless.fires
*               Code Cleanup for permissions system
*               Changed BaseCombatEntity to LootContainer
*               Fix for scientists getting a kit from NPCKits plugin
*               Added event triggers per barreltype (normal,oil,diesel)
*               Improved heightChecks
*               Events can now spawn inside buildings
*               Events can now spawn Ontop structures and powerlines
*               oilrigs and waterjunkpiles could have weird placements
*               Events can now spawn inside sewers and tunnels
*               Events can now spawn inside the subway and underwaterlabs
*               Added FX in cfg that executes each time a barrel triggers a event (remove and it will not be used)
*               Added small fires
*               Added Oil fires
*               Added Fire duration (max 20 seconds)
*               Added Fire and satchelcharge messages to the language file
*               Added satchelcharge on request
*   3.1.1       Patched for Rust Changes
*   3.1.2       Added HackCrates to the cfg and lang file
*               Some minor fixes
*            
*   Todo    -   Add Mono for animals (roam and other stuff)
*           -   Add day/night spawnrates
*           -   Add damagetypes (ea radiation/poison on triggers)
*           -   Add gametips using RandomTips/RandomTipsPlus plugins
*           -   Add API
* 
**********************************************************************/

#endregion

namespace Oxide.Plugins
{
    [Info("Barrelless", "Krungh Crow", "3.1.2")]
    [Description("various events after barrel kills")]
    class Barrelless : RustPlugin
    {
        [PluginReference] Plugin Kits;

        #region Variables

        #region Plugin
        bool BlockSpawn;
        bool BlockOutside;
        bool IsSpawned;
        bool fireallreadyspawned = false;

        bool Signaldropped;
        ulong chaticon;
        string prefix;
        System.Random rnd = new System.Random();
        private ConfigData configData;
        public static Barrelless instance;

        #endregion

        #region Permissions

        const string Exclude_Perm = "barrelless.exclude";
        const string SciTrigger_Perm = "barrelless.scientist";
        const string ScareTrigger_Perm = "barrelless.scarecrow";
        const string BearTrigger_Perm = "barrelless.bear";
        const string PBearTrigger_Perm = "barrelless.polarbear";
        const string BoarTrigger_Perm = "barrelless.boar";
        const string ChickenTrigger_Perm = "barrelless.chicken";
        const string WolfTrigger_Perm = "barrelless.wolf";
        const string ExploTrigger_Perm = "barrelless.explosive";
        const string FireTrigger_Perm = "barrelless.fires";
        const string AirdropTrigger_Perm = "barrelless.airdrop";
        const string HackCrateTrigger_Perm = "barrelless.hackcrate";

        #endregion

        #region NPC
        const string zombie = "assets/prefabs/npc/scarecrow/scarecrow.prefab";
        const string _scientist = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_roam.prefab";
        float SciDamageScale;
        int SciHealth;
        List<string> SciKit;
        float SciLifetime;
        string SciName;

        bool _UseKit;
        bool _FromHell;
        #endregion

        #region Animals
        const string bearString = "assets/rust.ai/agents/bear/bear.prefab";
        const string PBearString = "assets/rust.ai/agents/bear/polarbear.prefab";
        const string boarstring = "assets/rust.ai/agents/boar/boar.prefab";
        const string chickenString = "assets/rust.ai/agents/chicken/chicken.prefab";
        const string wolfString = "assets/rust.ai/agents/wolf/wolf.prefab";
        int _AnimalHealth;
        string _AnimalString;
        float _AnimalLife;
        #endregion

        #region Fires
        const string oilfire = "assets/bundled/prefabs/oilfireballsmall.prefab";//oil and diesel barrels
        const string fireball = "assets/bundled/prefabs/fireball.prefab";//OnFire
        #endregion

        #region Drops
        const string airdropString = "assets/prefabs/misc/supply drop/supply_drop.prefab";
        const string hackcrateString = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";
        const string beancanString = "assets/prefabs/weapons/beancan grenade/grenade.beancan.deployed.prefab";
        const string grenadeString = "assets/prefabs/weapons/f1 grenade/grenade.f1.deployed.prefab";
        const string satchelString = "assets/prefabs/weapons/satchelcharge/explosive.satchel.deployed.prefab";
        #endregion

        #region Data
        const string file_main = "barrelless_players/";
        #endregion

        #endregion

        #region Behaviours

        #region Scarecrow
        private class Zombies : FacepunchBehaviour
        {
            private ScarecrowNPC npc;
            public bool ReturningToHome = false;
            public bool isRoaming = true;
            public Vector3 SpawnPoint;

            private void Awake()
            {
                npc = GetComponent<ScarecrowNPC>();
                Invoke(nameof(_UseBrain), 0.1f);
                InvokeRepeating(Attack, 0.1f, 1.5f);
                InvokeRepeating(GoHome, 2.0f, 4.5f);
            }

            private void Attack()
            {
                BaseEntity entity = npc.Brain.Senses.GetNearestThreat(40);
                Chainsaw heldEntity = npc.GetHeldEntity() as Chainsaw;
                if (entity == null || Vector3.Distance(entity.transform.position, npc.transform.position) > 40.0f)
                {
                    npc.Brain.Navigator.ClearFacingDirectionOverride();
                    GoHome();
                }
                if (entity != null && Vector3.Distance(entity.transform.position, npc.transform.position) < 2.0f)
                {
                    npc.StartAttacking(entity);
                    npc.Brain.Navigator.SetFacingDirectionEntity(entity);
                    if (heldEntity is Chainsaw)
                    {
                        if (!(heldEntity as Chainsaw).EngineOn())
                        {
                            (heldEntity as Chainsaw).ServerNPCStart();
                        }
                        heldEntity.SetFlag(BaseEntity.Flags.Busy, true, false, true);
                        heldEntity.SetFlag(BaseEntity.Flags.Reserved8, true, false, true);
                    }
                }

                if (entity != null && Vector3.Distance(entity.transform.position, npc.transform.position) > 1.5f)
                {
                    if (heldEntity is Chainsaw)
                    {
                        if (!(heldEntity as Chainsaw).EngineOn())
                        {
                            (heldEntity as Chainsaw).ServerNPCStart();
                        }
                        heldEntity.SetFlag(BaseEntity.Flags.Busy, false, false, true);
                        heldEntity.SetFlag(BaseEntity.Flags.Reserved8, false, false, true);
                    }
                }

                if (entity != null && Vector3.Distance(entity.transform.position, npc.transform.position) > 2.0f)
                {
                    npc.Brain.Navigator.SetFacingDirectionEntity(entity);
                }
            }

            public void _UseBrain()
            {
                #region navigation
                npc.Brain.Navigator.Agent.agentTypeID = -1372625422;
                npc.Brain.Navigator.DefaultArea = "Walkable";
                npc.Brain.Navigator.Agent.autoRepath = true;
                npc.Brain.Navigator.enabled = true;
                npc.Brain.Navigator.CanUseNavMesh = true;
                npc.Brain.Navigator.BestRoamPointMaxDistance = instance.configData.CrowData.NPCRoamMax;
                npc.Brain.Navigator.MaxRoamDistanceFromHome = instance.configData.CrowData.NPCRoamMax;
                npc.Brain.Navigator.Init(npc, npc.Brain.Navigator.Agent);
                npc.Brain.Navigator.SetDestination(SpawnPoint, BaseNavigator.NavigationSpeed.Slow, 0f, 0f);
                #endregion

                #region senses & Targeting
                npc.Brain.ForceSetAge(0);
                npc.Brain.AllowedToSleep = false;
                npc.Brain.sleeping = false;
                npc.Brain.SenseRange = 30f;
                npc.Brain.ListenRange = 40f;
                npc.Brain.Senses.Init(npc,npc.Brain, 5f, 30, 40f, 135f, true, true, true, 60f, false, false, true, EntityType.Player, true);
                npc.Brain.TargetLostRange = 20f;
                npc.Brain.HostileTargetsOnly = false;
                npc.Brain.IgnoreSafeZonePlayers = true;
                #endregion
            }

            void GoHome()
            {
                if (npc == null || npc.IsDestroyed || npc.isMounted)
                    return;

                if (!npc.HasBrain)
                    return;
                if (npc.Brain.Senses.Memory.Targets.Count > 0)
                {
                    for (var i = 0; i < npc.Brain.Senses.Memory.Targets.Count; i++)
                    {
                        BaseEntity target = npc.Brain.Senses.Memory.Targets[i];
                        BasePlayer player = target as BasePlayer;

                        if (target == null || !player.IsAlive() || player.IsSleeping() || player.IsFlying)
                        {
                            WipeMemory();
                            ReturningToHome = true;
                            isRoaming = false;
                            return;
                        }
                        if (npc.Distance(player.transform.position) > 25f)
                        {
                            WipeMemory();
                            ReturningToHome = true;
                            isRoaming = false;
                            return;
                        }
                        if (player.IsSleeping() || player.IsFlying)
                        {
                            WipeMemory();
                            ReturningToHome = true;
                            isRoaming = false;
                            return;
                        }

                    }
                }

                var distanceHome = Vector3.Distance(npc.transform.position, SpawnPoint);
                if (ReturningToHome == false)
                {
                    if (isRoaming == true && distanceHome > instance.configData.CrowData.NPCRoamMax)
                    {
                        ReturningToHome = true;
                        isRoaming = false;
                        return;
                    }
                    if (isRoaming == true && distanceHome < instance.configData.CrowData.NPCRoamMax)
                    {
                        Vector3 random = UnityEngine.Random.insideUnitCircle.normalized * instance.configData.CrowData.NPCRoamMax;
                        Vector3 newPos = instance.GetNavPoint(SpawnPoint + new Vector3(random.x, 0f, random.y));
                        SettargetDestination(newPos);
                        return;
                    }
                }
                if (ReturningToHome && distanceHome > 2)
                {
                    if (npc.Brain.Navigator.Destination == SpawnPoint)
                    {
                        return;
                    }

                    WipeMemory();
                    SettargetDestination(SpawnPoint);
                    return;
                }
                ReturningToHome = false;
                isRoaming = true;
            }

            private void SettargetDestination(Vector3 position)
            {
                npc.Brain.Navigator.Destination = position;
                npc.Brain.Navigator.SetDestination(position, BaseNavigator.NavigationSpeed.Slow, 0f, 0f);
            }

            void WipeMemory()
            {
                if (!npc.HasBrain)
                {
                    return;
                }

                npc.Brain.Senses.Players.Clear();
                npc.Brain.Senses.Memory.Players.Clear();
                npc.Brain.Senses.Memory.Targets.Clear();
                npc.Brain.Senses.Memory.Threats.Clear();
                npc.Brain.Senses.Memory.LOS.Clear();
                npc.Brain.Senses.Memory.All.Clear();
            }

            void OnDestroy()
            {
                if (npc != null && !npc.IsDestroyed)
                {
                    npc.Kill();
                }
                CancelInvoke(GoHome);
                CancelInvoke(Attack);
                CancelInvoke(nameof(_UseBrain));
            }
        }
        #endregion

        #region Scientist
        public class Scientists : FacepunchBehaviour
        {
            public global::HumanNPC npc;
            public bool ReturningToHome = false;
            public bool isRoaming = true;
            public Vector3 SpawnPoint;

            void Start()
            {
                npc = GetComponent<global::HumanNPC>();

                InvokeRepeating("GoHome", 2.0f, 4.5f);
                Invoke(nameof(_UseBrain), 0.1f);
            }
            public void _UseBrain()
            {
                #region navigation
                npc.Brain.Navigator.Agent.agentTypeID = -1372625422;
                npc.Brain.Navigator.DefaultArea = "Walkable";
                npc.Brain.Navigator.Agent.autoRepath = true;
                npc.Brain.Navigator.enabled = true;
                npc.Brain.Navigator.CanUseNavMesh = true;
                npc.Brain.Navigator.BestRoamPointMaxDistance = instance.configData.SCIData.NPCRoamMax;
                npc.Brain.Navigator.MaxRoamDistanceFromHome = instance.configData.SCIData.NPCRoamMax;
                npc.Brain.Navigator.Init(npc, npc.Brain.Navigator.Agent);
                npc.Brain.Navigator.SetDestination(SpawnPoint, BaseNavigator.NavigationSpeed.Slow, 0f, 0f);
                #endregion

                #region senses & Targeting
                npc.Brain.ForceSetAge(0);
                npc.Brain.AllowedToSleep = false;
                npc.Brain.sleeping = false;
                npc.Brain.SenseRange = 30f;
                npc.Brain.ListenRange = 40f;
                npc.Brain.Senses.Init(npc,npc.Brain , 5f, 50f, 50f, -1f, true, false, true, 60f, false, false, false, EntityType.Player, false);
                npc.Brain.TargetLostRange = 25f;
                npc.Brain.HostileTargetsOnly = false;
                npc.Brain.IgnoreSafeZonePlayers = true;
                #endregion
            }

            void GoHome()
            {
                if (npc == null || npc.IsDestroyed || npc.isMounted)
                    return;

                if (!npc.HasBrain)
                    return;
                if (npc.Brain.Senses.Memory.Targets.Count > 0)
                {
                    for (var i = 0; i < npc.Brain.Senses.Memory.Targets.Count; i++)
                    {
                        BaseEntity target = npc.Brain.Senses.Memory.Targets[i];
                        BasePlayer player = target as BasePlayer;

                        if (target == null || !player.IsAlive())
                        {
                            WipeMemory();
                            ReturningToHome = true;
                            isRoaming = false;
                            return;
                        }
                        if (npc.Distance(player.transform.position) > 40f)
                        {
                            WipeMemory();
                            ReturningToHome = true;
                            isRoaming = false;
                            return;
                        }
                    }
                }

                var distanceHome = Vector3.Distance(npc.transform.position, SpawnPoint);
                if (ReturningToHome == false)
                {
                    if (isRoaming == true && distanceHome > instance.configData.SCIData.NPCRoamMax)
                    {
                        ReturningToHome = true;
                        isRoaming = false;
                        return;
                    }
                    if (isRoaming == true && distanceHome < instance.configData.SCIData.NPCRoamMax)
                    {
                        Vector3 random = UnityEngine.Random.insideUnitCircle.normalized * instance.configData.SCIData.NPCRoamMax;
                        Vector3 newPos = instance.GetNavPoint(SpawnPoint + new Vector3(random.x, 0f, random.y));
                        SettargetDestination(newPos);
                        return;
                    }
                }
                if (ReturningToHome && distanceHome > 2)
                {
                    if (npc.Brain.Navigator.Destination == SpawnPoint)
                    {
                        return;
                    }

                    WipeMemory();
                    SettargetDestination(SpawnPoint);
                    return;
                }
                ReturningToHome = false;
                isRoaming = true;
            }
            private void SettargetDestination(Vector3 position)
            {
                npc.Brain.Navigator.Destination = position;
                npc.Brain.Navigator.SetDestination(position, BaseNavigator.NavigationSpeed.Slow, 0f, 0f);
            }
            void WipeMemory()
            {
                if (!npc.HasBrain)
                {
                    return;
                }

                npc.Brain.Senses.Players.Clear();
                npc.Brain.Senses.Memory.Players.Clear();
                npc.Brain.Senses.Memory.Targets.Clear();
                npc.Brain.Senses.Memory.Threats.Clear();
                npc.Brain.Senses.Memory.LOS.Clear();
                npc.Brain.Senses.Memory.All.Clear();
            }

            void OnDestroy()
            {
                if (npc != null && !npc.IsDestroyed)
                {
                    npc.Kill();
                }
                CancelInvoke("GoHome");
                CancelInvoke(nameof(_UseBrain));

            }
        }
        #endregion

        #endregion

        #region Configuration

        class ConfigData
        {
            [JsonProperty(PropertyName = "Plugin Settings")]
            public SettingsDrop DropData = new SettingsDrop();
            [JsonProperty(PropertyName = "Airdrop Settings")]
            public SettingsAirdrop AirdropData = new SettingsAirdrop();
            [JsonProperty(PropertyName = "Hack crate Settings")]
            public SettingsHack HackData = new SettingsHack();
            [JsonProperty(PropertyName = "Scarecrow Settings")]
            public NPCSettings CrowData = new NPCSettings();
            [JsonProperty(PropertyName = "Scientist Settings")]
            public SCISettings SCIData = new SCISettings();
            [JsonProperty(PropertyName = "Animal Settings")]
            public SettingsAnimal AnimalData = new SettingsAnimal();
            [JsonProperty(PropertyName = "Explosives Settings")]
            public SettingsExplosives ExplosivesData = new SettingsExplosives();
            [JsonProperty(PropertyName = "Fire Settings")]
            public SettingsFire FireData = new SettingsFire();
        }

        #region Plugin settings
        class SettingsDrop
        {
            [JsonProperty(PropertyName = "Chat Prefix")]
            public string Prefix = "[<color=green>Barrelles</color>] ";
            [JsonProperty(PropertyName = "Drop : Count random per x barrels")]
            public int Barrelcountdrop { get; set; } = 1;
            [JsonProperty(PropertyName = "Drop : Max range to trigger events")]
            public int RangeTrigger { get; set; } = 10;
            [JsonProperty(PropertyName = "Drop : Spawn only 1 entity on trigger")]
            public bool Trigger = false;
            [JsonProperty(PropertyName = "Only allow spawning outside")]
            public bool TriggerOut = true;
            [JsonProperty(PropertyName = "Show messages")]
            public bool ShowMsg = true;
            [JsonProperty(PropertyName = "FX on trigger")]
            public string FX = "assets/prefabs/misc/halloween/lootbag/effects/gold_open.prefab";
        }
        #endregion

        #region Hackable locked crate
        class SettingsHack
        {
            [JsonProperty(PropertyName = "Spawn chance (0-100)")]
            public int HackdropRate { get; set; } = 1;
            [JsonProperty(PropertyName = "Drop height")]
            public int HackdropHeight { get; set; } = 120;
            [JsonProperty(PropertyName = "Normal Barrels")]
            public bool Normal = true;
            [JsonProperty(PropertyName = "Diesel Barrels (diesel_barrel_world)")]
            public bool Diesel = true;
            [JsonProperty(PropertyName = "Oil Barrels")]
            public bool Oil = true;
        }
        #endregion

        #region Airdrop
        class SettingsAirdrop
        {
            [JsonProperty(PropertyName = "Spawn chance (0-100)")]
            public int AirdropRate { get; set; } = 1;
            [JsonProperty(PropertyName = "Drop height")]
            public int AirdropHeight { get; set; } = 120;
            [JsonProperty(PropertyName = "Normal Barrels")]
            public bool Normal = true;
            [JsonProperty(PropertyName = "Diesel Barrels (diesel_barrel_world)")]
            public bool Diesel = true;
            [JsonProperty(PropertyName = "Oil Barrels")]
            public bool Oil = true;
        }
        #endregion

        #region Scarecrow
        class NPCSettings
        {
            [JsonProperty(PropertyName = "Spawn chance (0-100)")]
            public int SpawnRate  = 10;
            [JsonProperty(PropertyName = "Normal Barrels")]
            public bool Normal = true;
            [JsonProperty(PropertyName = "Diesel Barrels (diesel_barrel_world)")]
            public bool Diesel = true;
            [JsonProperty(PropertyName = "Oil Barrels")]
            public bool Oil = true;
            [JsonProperty(PropertyName = "Spawn Amount")]
            public int NPCAmount = 1;
            [JsonProperty(PropertyName = "NPC Spawns on fire")]
            public bool FromHell = false;
            [JsonProperty(PropertyName = "Max Roam Distance")]
            public int NPCRoamMax= 15;
            [JsonProperty(PropertyName = "Prefix (Title)")]
            public string NPCName = "Scarecrow";
            [JsonProperty(PropertyName = "Prefix (Title) if chainsaw equiped")]
            public string NPCName2 = "Chainsaw Murderer";
            [JsonProperty(PropertyName = "Health (HP)")]
            public int NPCHealth = 250;
            [JsonProperty(PropertyName = "Life Duration (minutes)")]
            public float NPCLife = 5f;
            [JsonProperty(PropertyName = "Damage multiplier")]
            public float NPCDamageScale = 0.6f;
            [JsonProperty(PropertyName = "Use kit (clothing)")]
            public bool UseKit = false;
            [JsonProperty(PropertyName = "Kit ID")]
            public List<string> KitName = new List<string>();
        }
        #endregion

        #region Scientist
        class SCISettings
        {
            [JsonProperty(PropertyName = "Spawn chance (0-100)")]
            public int SpawnRate = 10;
            [JsonProperty(PropertyName = "Normal Barrels")]
            public bool Normal = true;
            [JsonProperty(PropertyName = "Diesel Barrels (diesel_barrel_world)")]
            public bool Diesel = true;
            [JsonProperty(PropertyName = "Oil Barrels")]
            public bool Oil = true;
            [JsonProperty(PropertyName = "Spawn Amount")]
            public int NPCAmount = 1;
            [JsonProperty(PropertyName = "NPC Spawns on fire")]
            public bool FromHell = false;
            [JsonProperty(PropertyName = "Max Roam Distance")]
            public int NPCRoamMax = 15;
            [JsonProperty(PropertyName = "Prefix (Title)")]
            public string NPCName = "BarrelScientist";
            [JsonProperty(PropertyName = "Health (HP)")]
            public int NPCHealth = 250;
            [JsonProperty(PropertyName = "Life Duration (minutes)")]
            public float NPCLife = 5f;
            [JsonProperty(PropertyName = "Damage multiplier")]
            public float NPCDamageScale = 0.6f;
            [JsonProperty(PropertyName = "Use kit (clothing)")]
            public bool UseKit = false;
            [JsonProperty(PropertyName = "Kit ID")]
            public List<string> KitName = new List<string>();
        }
        #endregion

        #region Animals
        class SettingsAnimal
        {
            [JsonProperty(PropertyName = "Bear Settings")]
            public BearSettings Bear = new BearSettings();
            [JsonProperty(PropertyName = "PolarBear Settings")]
            public PBearSettings PBear = new PBearSettings();
            [JsonProperty(PropertyName = "Boar Settings")]
            public BoarSettings Boar = new BoarSettings();
            [JsonProperty(PropertyName = "Chicken Settings")]
            public ChickenSettings Chicken = new ChickenSettings();
            [JsonProperty(PropertyName = "Wolf Settings")]
            public WolfSettings Wolf = new WolfSettings();
        }

        class BearSettings
        {
            [JsonProperty(PropertyName = "Chance on spawn (0-100)")]
            public int BearRate = 5;
            [JsonProperty(PropertyName = "Normal Barrels")]
            public bool Normal = true;
            [JsonProperty(PropertyName = "Diesel Barrels (diesel_barrel_world)")]
            public bool Diesel = true;
            [JsonProperty(PropertyName = "Oil Barrels")]
            public bool Oil = true;
            [JsonProperty(PropertyName = "Amount")]
            public int BearAmount = 1;
            [JsonProperty(PropertyName = "Health")]
            public int BearHealth = 450;
            [JsonProperty(PropertyName = "Life Duration (minutes)")]
            public float Life = 5f;
        }

        class PBearSettings
        {
            [JsonProperty(PropertyName = "Chance on spawn (0-100)")]
            public int PBearRate = 5;
            [JsonProperty(PropertyName = "Normal Barrels")]
            public bool Normal = true;
            [JsonProperty(PropertyName = "Diesel Barrels (diesel_barrel_world)")]
            public bool Diesel = true;
            [JsonProperty(PropertyName = "Oil Barrels")]
            public bool Oil = true;
            [JsonProperty(PropertyName = "Amount")]
            public int PBearAmount = 1;
            [JsonProperty(PropertyName = "Health")]
            public int PBearHealth = 450;
            [JsonProperty(PropertyName = "Life Duration (minutes)")]
            public float Life = 5f;
        }

        class BoarSettings
        {
            [JsonProperty(PropertyName = "Chance on spawn (0-100)")]
            public int BoarRate = 10;
            [JsonProperty(PropertyName = "Normal Barrels")]
            public bool Normal = true;
            [JsonProperty(PropertyName = "Diesel Barrels (diesel_barrel_world)")]
            public bool Diesel = true;
            [JsonProperty(PropertyName = "Oil Barrels")]
            public bool Oil = true;
            [JsonProperty(PropertyName = "Amount")]
            public int BoarAmount = 1;
            [JsonProperty(PropertyName = "Health")]
            public int BoarHealth = 250;
            [JsonProperty(PropertyName = "Life Duration (minutes)")]
            public float Life = 5f;
        }

        class ChickenSettings
        {
            [JsonProperty(PropertyName = "Chance on spawn (0-100)")]
            public int ChickenRate = 10;
            [JsonProperty(PropertyName = "Normal Barrels")]
            public bool Normal = true;
            [JsonProperty(PropertyName = "Diesel Barrels (diesel_barrel_world)")]
            public bool Diesel = true;
            [JsonProperty(PropertyName = "Oil Barrels")]
            public bool Oil = true;
            [JsonProperty(PropertyName = "Amount")]
            public int ChickenAmount = 1;
            [JsonProperty(PropertyName = "Health")]
            public int ChickenHealth = 40;
            [JsonProperty(PropertyName = "Life Duration (minutes)")]
            public float Life = 5f;
        }

        class WolfSettings
        {
            [JsonProperty(PropertyName = "Chance on spawn (0-100)")]
            public int WolfRate = 10;
            [JsonProperty(PropertyName = "Normal Barrels")]
            public bool Normal = true;
            [JsonProperty(PropertyName = "Diesel Barrels (diesel_barrel_world)")]
            public bool Diesel = true;
            [JsonProperty(PropertyName = "Oil Barrels")]
            public bool Oil = true;
            [JsonProperty(PropertyName = "Amount")]
            public int WolfAmount = 1;
            [JsonProperty(PropertyName = "Health")]
            public int WolfHealth = 250;
            [JsonProperty(PropertyName = "Life Duration (minutes)")]
            public float Life = 5f;
        }
        #endregion

        #region Explosives
        class SettingsExplosives
        {
            [JsonProperty(PropertyName = "Beancan : Chance on spawn (0-100)")]
            public int BeancanRate = 5;
            [JsonProperty(PropertyName = "F1 Grenade : Chance on spawn (0-100)")]
            public int GrenadeRate = 3;
            [JsonProperty(PropertyName = "Satchel Charge : Chance on spawn (0-100)")]
            public int SatchelRate = 3;
            [JsonProperty(PropertyName = "Normal Barrels")]
            public bool Normal = true;
            [JsonProperty(PropertyName = "Diesel Barrels (diesel_barrel_world)")]
            public bool Diesel = true;
            [JsonProperty(PropertyName = "Oil Barrels")]
            public bool Oil = true;
        }
        #endregion

        #region Fires
        class SettingsFire
        {
            [JsonProperty(PropertyName = "Small fire : Chance on spawn (0-100)")]
            public int SmallfireRate = 5;
            [JsonProperty(PropertyName = "Oil fire : Chance on spawn (0-100)")]
            public int OilfireRate = 3;
            [JsonProperty(PropertyName = "Oil fire : Duration (max 20s)")]
            public float Duration = 10f;
            [JsonProperty(PropertyName = "Normal Barrels")]
            public bool Normal = false;
            [JsonProperty(PropertyName = "Diesel Barrels (diesel_barrel_world)")]
            public bool Diesel = true;
            [JsonProperty(PropertyName = "Oil Barrels")]
            public bool Oil = true;
        }
        #endregion

        #region cfg save/load
        private bool LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConf();
            return true;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Fresh install detected Creating a new config file.");
            configData = new ConfigData();
            SaveConf();
        }

        void SaveConf() => Config.WriteObject(configData, true);
        #endregion

        #endregion

        #region LanguageApi

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AirdropSpawned"] = "You lucky bastard a Airdrop is comming to your location" ,
                ["HackCrateSpawned"] = "You lucky bastard a Hackable Crate is comming to your location" ,
                ["Beancanspawned"] = "A small explosive fell out of the barrel",
                ["Grenadespawned"] = "A Grenade fell out of the barrel",
                ["Bearspawned"] = "A wild bear just apeared",
                ["MoreBearspawned"] = "Runnn... a sleuth of Bears apeared",
                ["PBearspawned"] = "A wild PolarBear just apeared",
                ["MorePBearspawned"] = "Runnn... they hungry",
                ["Boarspawned"] = "Oink...",
                ["MoreBoarspawned"] = "Oinks...A Sounder of boars apeared",
                ["Chickenspawned"] = "tok tok...",
                ["MoreChickenspawned"] = "tok...toktoktok!",
                ["Scientistspawned"] = "A Scientist was freed from his barrel prison",
                ["MoreScientistsSpawned"] = "Multiple Scientists where freed from their barrel prison",
                ["SignalDropped"] = "You found a supply signal but you dropped it on the floor",
                ["SignalRecieved"] = "Your recieved a supply signal in your inventory",
                ["Wolfspawned"] = "A wild wolf just apeared",
                ["MoreWolvesspawned"] = "Runnn... it is a pack of wolves",
                ["Zombiespawned"] = "A Zombie was freed from his barrel prison",
                ["MoreZombiesSpawned"] = "Multiple Zombies where freed from their barrel prison",
                ["Firespawned"] = "Carefull a spark set the barrel on fire",
                ["OilFirespawned"] = "Ouch a spark had the barrel lit up",
                ["Satchelspawned"] = "Run !!! A hidden satchelcharge was activated"
            } , this);
        }

        #endregion

        #region Hooks

        void OnServerInitialized()
        {
            instance = this;
        }

        void Unload()
        {
            Zombies[] zombies = UnityEngine.Object.FindObjectsOfType<Zombies>();
            if (zombies != null)
            {
                foreach (Zombies zombie in zombies)
                    UnityEngine.Object.Destroy(zombie);
            }
            Scientists[] scientists = UnityEngine.Object.FindObjectsOfType<Scientists>();
            if (scientists != null)
            {
                foreach (Scientists scientist in scientists)
                    UnityEngine.Object.Destroy(scientist);
            }
        }

        void Init()
        {
            if (!LoadConfigVariables())
            {
                Puts("Config file issue detected. Please delete file, or check syntax and fix.");
                return;
            }
            prefix = configData.DropData.Prefix;
            chaticon = 76561199200662608;
            BlockOutside = configData.DropData.TriggerOut;
            permission.RegisterPermission(Exclude_Perm , this);
            permission.RegisterPermission(SciTrigger_Perm, this);
            permission.RegisterPermission(ScareTrigger_Perm, this);
            permission.RegisterPermission(BearTrigger_Perm, this);
            permission.RegisterPermission(PBearTrigger_Perm, this);
            permission.RegisterPermission(BoarTrigger_Perm, this);
            permission.RegisterPermission(ChickenTrigger_Perm, this);
            permission.RegisterPermission(WolfTrigger_Perm, this);
            permission.RegisterPermission(ExploTrigger_Perm , this);
            permission.RegisterPermission(FireTrigger_Perm , this);
            permission.RegisterPermission(AirdropTrigger_Perm , this);
            permission.RegisterPermission(HackCrateTrigger_Perm , this);
        }

        void OnEntityDeath(LootContainer entity, HitInfo info)
        {
            if (info?.InitiatorPlayer == null) return;
            if (IsLootBarrel(entity) == false) return;
            if (CheckPlayer(info) == false) return;
            if (!entity.IsOutside() &&  BlockOutside == true) return;

            BasePlayer player = info.InitiatorPlayer;

            if (HasPerm(player, Exclude_Perm)) return;

            var BarrelDistance = Vector3.Distance(entity.transform.position, player.transform.position);

            if (BarrelDistance > configData.DropData.RangeTrigger) return;

            else
            {
                Playerinfo user = get_user(info.InitiatorPlayer);
                if (user.barrelCount < configData.DropData.Barrelcountdrop)
                {
                    user.barrelCount += 1;
                    update_user(info.InitiatorPlayer , user);
                }
                else
                {
                    user.barrelCount = 0;
                    update_user(info.InitiatorPlayer , user);

                    if (entity.transform.position != null)
                    {
                        #region Scarecrow
                        if (SpawnRate(configData.CrowData.SpawnRate) == true && HasPerm(player, ScareTrigger_Perm))
                        {
                            IsSpawned = false;
                            for (int i = 0; i < configData.CrowData.NPCAmount; i++)
                            {
                                if (IsNormalBarrel(entity) && configData.CrowData.Normal == false) continue;
                                if (IsOilBarrel(entity) && configData.CrowData.Oil == false) continue;
                                if (IsDiesel(entity) && configData.CrowData.Diesel == false) continue;
                                Spawnnpc(entity.transform.position);
                                IsSpawned = true;
                                RunFX(player);
                            }
                            if (configData.CrowData.NPCAmount == 1 && configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("Zombiespawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            else if (configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("MoreZombiesSpawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            if (configData.DropData.Trigger) return;
                        }
                        #endregion

                        #region Scientists
                        if (SpawnRate(configData.SCIData.SpawnRate) == true && HasPerm(player, SciTrigger_Perm))
                        {
                            IsSpawned = false;
                            for (int i = 0; i < configData.SCIData.NPCAmount; i++)
                            {
                                if (IsNormalBarrel(entity) && configData.SCIData.Normal == false) continue;
                                if (IsDiesel(entity) && configData.SCIData.Diesel == false) continue;
                                if (IsOilBarrel(entity) && configData.SCIData.Oil == false) continue;
                                _FromHell = configData.SCIData.FromHell;
                                SciHealth = configData.SCIData.NPCHealth;
                                SciDamageScale = configData.SCIData.NPCDamageScale;
                                SciLifetime = configData.SCIData.NPCLife;
                                SciName = configData.SCIData.NPCName;
                                _UseKit = configData.SCIData.UseKit;
                                SciKit = configData.SCIData.KitName;
                                SpawnScientist(entity.transform.position);
                                IsSpawned = true;
                                RunFX(player);
                            }
                            if (configData.SCIData.NPCAmount == 1 && configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("Scientistspawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            else if (configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("MoreScientistsSpawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            if (configData.DropData.Trigger) return;
                        }
                        #endregion

                        #region Airdrop
                        if (SpawnRate(configData.AirdropData.AirdropRate) == true && entity.IsOutside() && HasPerm(player , AirdropTrigger_Perm))
                        {
                            IsSpawned = false;
                            for (int i = 0; i < 1; i++)
                            {
                                if (IsNormalBarrel(entity) && configData.AirdropData.Normal == false) continue;
                                if (IsDiesel(entity) && configData.AirdropData.Diesel == false) continue;
                                if (IsOilBarrel(entity) && configData.AirdropData.Oil == false) continue;

                                SpawnSupplyCrate(airdropString , entity.transform.position);
                                IsSpawned = true;
                                RunFX(player);
                                if (configData.DropData.ShowMsg == true && IsSpawned == true)
                                {
                                    Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("AirdropSpawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                                }
                            }
                        }
                        else if (SpawnRate(configData.AirdropData.AirdropRate) == true && !entity.IsOutside())
                        {
                            IsSpawned = false;
                            for (int i = 0; i < 1; i++)
                            {
                                if (IsNormalBarrel(entity) && configData.AirdropData.Normal == false) continue;
                                if (IsDiesel(entity) && configData.AirdropData.Diesel == false) continue;
                                if (IsOilBarrel(entity) && configData.AirdropData.Oil == false) continue;

                                var _signal = ItemManager.CreateByName("supply.signal" , 1 , 0);
                                player.inventory.GiveItem(_signal , null);
                                IsSpawned = true;
                                RunFX(player);
                                if (player.inventory.containerMain.IsFull())
                                {
                                    _signal.DropAndTossUpwards(player.transform.position);
                                    if (configData.DropData.ShowMsg == true && IsSpawned == true)
                                    {
                                        Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("SignalDropped" , info.InitiatorPlayer.UserIDString)) , chaticon);
                                    }
                                    Signaldropped = true;
                                }
                                if (Signaldropped == false && configData.DropData.ShowMsg == true && IsSpawned == true)
                                {
                                    Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("SignalRecieved" , info.InitiatorPlayer.UserIDString)) , chaticon);
                                }
                            }
                            if (configData.DropData.Trigger) return;
                        }
                        #endregion

                        #region HackCrate
                        if (SpawnRate(configData.HackData.HackdropRate) == true && entity.IsOutside() && HasPerm(player , HackCrateTrigger_Perm))
                        {
                            IsSpawned = false;
                            for (int i = 0; i < 1; i++)
                            {
                                if (IsNormalBarrel(entity) && configData.HackData.Normal == false) continue;
                                if (IsDiesel(entity) && configData.HackData.Diesel == false) continue;
                                if (IsOilBarrel(entity) && configData.HackData.Oil == false) continue;

                                SpawnHackCrate(hackcrateString , entity.transform.position);
                                IsSpawned = true;
                                RunFX(player);
                                if (configData.DropData.ShowMsg == true && IsSpawned == true)
                                {
                                    Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("HackCrateSpawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                                }
                            }
                        }
                        #endregion

                        #region Animals
                        if (SpawnRate(configData.AnimalData.Bear.BearRate) == true && HasPerm(player, BearTrigger_Perm))
                        {
                            IsSpawned = false;
                            for (int i = 0; i < configData.AnimalData.Bear.BearAmount; i++)
                            {
                                if (IsNormalBarrel(entity) && configData.AnimalData.Bear.Normal == false) continue;
                                if (IsDiesel(entity) && configData.AnimalData.Bear.Diesel == false) continue;
                                if (IsOilBarrel(entity) && configData.AnimalData.Bear.Oil == false) continue;
                                _AnimalHealth = configData.AnimalData.Bear.BearHealth;
                                _AnimalLife = configData.AnimalData.Bear.Life;
                                _AnimalString = bearString;
                                SpawnAnimal(entity.transform.position);
                                IsSpawned = true;
                                RunFX(player);
                            }
                            if (configData.AnimalData.Bear.BearAmount == 1 && configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("Bearspawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            else if (configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("MoreBearspawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            if (configData.DropData.Trigger) return;
                        }

                        if (SpawnRate(configData.AnimalData.PBear.PBearRate) == true && HasPerm(player , PBearTrigger_Perm))
                        {
                            IsSpawned = false;
                            for (int i = 0; i < configData.AnimalData.PBear.PBearAmount; i++)
                            {
                                if (IsNormalBarrel(entity) && configData.AnimalData.PBear.Normal == false) continue;
                                if (IsDiesel(entity) && configData.AnimalData.PBear.Diesel == false) continue;
                                if (IsOilBarrel(entity) && configData.AnimalData.PBear.Oil == false) continue;
                                _AnimalHealth = configData.AnimalData.PBear.PBearHealth;
                                _AnimalLife = configData.AnimalData.PBear.Life;
                                _AnimalString = PBearString;
                                SpawnAnimal(entity.transform.position);
                                IsSpawned = true;
                                RunFX(player);
                            }
                            if (configData.AnimalData.PBear.PBearAmount == 1 && configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("PBearspawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            else if (configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("MorePBearspawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            if (configData.DropData.Trigger) return;
                        }

                        if (SpawnRate(configData.AnimalData.Boar.BoarRate) == true && HasPerm(player , BoarTrigger_Perm))
                        {
                            IsSpawned = false;
                            for (int i = 0; i < configData.AnimalData.Boar.BoarAmount; i++)
                            {
                                if (IsNormalBarrel(entity) && configData.AnimalData.Boar.Normal == false) continue;
                                if (IsDiesel(entity) && configData.AnimalData.Boar.Diesel == false) continue;
                                if (IsOilBarrel(entity) && configData.AnimalData.Boar.Oil == false) continue;
                                _AnimalHealth = configData.AnimalData.Boar.BoarHealth;
                                _AnimalLife = configData.AnimalData.Boar.Life;
                                _AnimalString = boarstring;
                                SpawnAnimal(entity.transform.position);
                                IsSpawned = true;
                                RunFX(player);
                            }
                            if (configData.AnimalData.Boar.BoarAmount == 1 && configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("Boarspawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            else if (configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("MoreBoarspawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            if (configData.DropData.Trigger) return;
                        }

                        if (SpawnRate(configData.AnimalData.Chicken.ChickenRate) == true && HasPerm(player , ChickenTrigger_Perm))
                        {
                            IsSpawned = false;  
                            for (int i = 0; i < configData.AnimalData.Chicken.ChickenAmount; i++)
                            {
                                if (IsNormalBarrel(entity) && configData.AnimalData.Chicken.Normal == false) continue;
                                if (IsDiesel(entity) && configData.AnimalData.Chicken.Diesel == false) continue;
                                if (IsOilBarrel(entity) && configData.AnimalData.Chicken.Oil == false) continue;
                                _AnimalHealth = configData.AnimalData.Chicken.ChickenHealth;
                                _AnimalLife = configData.AnimalData.Chicken.Life;
                                _AnimalString = chickenString;
                                SpawnAnimal(entity.transform.position);
                                IsSpawned = true;
                                RunFX(player);
                            }
                            if (configData.AnimalData.Chicken.ChickenAmount == 1 && configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("Chickenspawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            else if (configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("MoreChickenspawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            if (configData.DropData.Trigger) return;
                        }

                        if (SpawnRate(configData.AnimalData.Wolf.WolfRate) == true && HasPerm(player , WolfTrigger_Perm))
                        {
                            IsSpawned = false;
                            for (int i = 0; i < configData.AnimalData.Wolf.WolfAmount; i++)
                            {
                                if (IsNormalBarrel(entity) && configData.AnimalData.Wolf.Normal == false) continue;
                                if (IsDiesel(entity) && configData.AnimalData.Wolf.Diesel == false) continue;
                                if (IsOilBarrel(entity) && configData.AnimalData.Wolf.Oil == false) continue;

                                _AnimalHealth = configData.AnimalData.Wolf.WolfHealth;
                                _AnimalLife = configData.AnimalData.Wolf.Life;
                                _AnimalString = wolfString;
                                SpawnAnimal(entity.transform.position);
                                IsSpawned = true;
                                RunFX(player);
                            }
                            if (configData.AnimalData.Wolf.WolfAmount == 1 && configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("Wolfspawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            else if (configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("MoreWolvesspawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            if (configData.DropData.Trigger)
                                return;
                        }
                        #endregion

                        #region explosives
                        if (SpawnRate(configData.ExplosivesData.BeancanRate) == true && HasPerm(player , ExploTrigger_Perm))
                        {
                            IsSpawned = false;
                            for (int i = 0; i < 1; i++)
                            {
                                if (IsNormalBarrel(entity) && configData.ExplosivesData.Normal == false) continue;
                                if (IsDiesel(entity) && configData.ExplosivesData.Diesel == false) continue;
                                if (IsOilBarrel(entity) && configData.ExplosivesData.Oil == false) continue;
                                SpawnThrowable(beancanString , entity.transform.position);
                                IsSpawned = true;
                                RunFX(player);
                            }
                            if (configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("Beancanspawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            if (configData.DropData.Trigger) return;
                        }

                        if (SpawnRate(configData.ExplosivesData.GrenadeRate) == true && HasPerm(player , ExploTrigger_Perm))
                        {
                            IsSpawned = false;
                            for (int i = 0; i < 1; i++)
                            {
                                if (IsNormalBarrel(entity) && configData.ExplosivesData.Normal == false) continue;
                                if (IsDiesel(entity) && configData.ExplosivesData.Diesel == false) continue;
                                if (IsOilBarrel(entity) && configData.ExplosivesData.Oil == false) continue;

                                SpawnThrowable(grenadeString , entity.transform.position);
                                IsSpawned = true;
                                RunFX(player);
                            }

                            if (configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("Grenadespawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            if (configData.DropData.Trigger) return;
                        }
                        if (SpawnRate(configData.ExplosivesData.SatchelRate) == true && HasPerm(player , ExploTrigger_Perm))
                        {
                            IsSpawned = false;
                            for (int i = 0; i < 1; i++)
                            {
                                if (IsNormalBarrel(entity) && configData.ExplosivesData.Normal == false) continue;
                                if (IsDiesel(entity) && configData.ExplosivesData.Diesel == false) continue;
                                if (IsOilBarrel(entity) && configData.ExplosivesData.Oil == false) continue;

                                SpawnThrowable(satchelString , entity.transform.position);
                                IsSpawned = true;
                                RunFX(player);
                            }

                            if (configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("Satchelspawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            if (configData.DropData.Trigger) return;
                        }
                        #endregion

                        #region Fires

                        fireallreadyspawned = false;

                        if (SpawnRate(configData.FireData.SmallfireRate) == true && HasPerm(player , FireTrigger_Perm))
                        {
                            IsSpawned = false;
                            fireallreadyspawned = false;
                            for (int i = 0; i < 1; i++)
                            {
                                if (IsNormalBarrel(entity) && configData.FireData.Normal == false) continue;
                                if (IsDiesel(entity) && configData.FireData.Diesel == false) continue;
                                if (IsOilBarrel(entity) && configData.FireData.Oil == false) continue;
                                SpawnFire(fireball , entity.transform.position, configData.FireData.Duration);
                                IsSpawned = true;
                                fireallreadyspawned = true;
                                RunFX(player);
                            }
                            if (configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("Firespawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            if (configData.DropData.Trigger) return;
                        }
                        if (SpawnRate(configData.FireData.OilfireRate) == true && HasPerm(player , FireTrigger_Perm) && fireallreadyspawned == false)
                        {
                            IsSpawned = false;
                            for (int i = 0; i < 1; i++)
                            {
                                if (IsNormalBarrel(entity) && configData.FireData.Normal == false) continue;
                                if (IsDiesel(entity) && configData.FireData.Diesel == false) continue;
                                if (IsOilBarrel(entity) && configData.FireData.Oil == false) continue;
                                SpawnFire(oilfire , entity.transform.position , configData.FireData.Duration);
                                IsSpawned = true;
                                RunFX(player);
                            }
                            if (configData.DropData.ShowMsg == true && IsSpawned == true)
                            {
                                Player.Message(info.InitiatorPlayer , prefix + string.Format(msg("OilFirespawned" , info.InitiatorPlayer.UserIDString)) , chaticon);
                            }
                            if (configData.DropData.Trigger) return;
                        }
                        #endregion
                    }
                }
            }
        }

        object OnNpcKits(BasePlayer player)
        {
            if (player?.gameObject?.GetComponent<Zombies>() != null) return true;
            if (player?.gameObject?.GetComponent<Scientists>() != null) return true;
            return null;
        }

        private object OnNpcTarget(BaseEntity attacker, BaseEntity target)
        {
            if (attacker?.gameObject?.GetComponent<Zombies>())
            {
                if (target is ScarecrowNPC|| target is BaseNpc)
                {
                    return true;
                }

                if (target is TunnelDweller || target is UnderwaterDweller)
                {
                    return true;
                }
                return null;
            }

            if (target?.gameObject?.GetComponent<Zombies>())
            {
                return true;
            }
            return null;
        }

        private object OnNpcTarget(BaseEntity attacker, BasePlayer target)
        {
            if (attacker?.gameObject?.GetComponent<Zombies>())
            {
                if (target.IsSleeping() || target.IsFlying || !(target.userID.IsSteamId()))
                    return false;
            }
            return null;
        }

        private void OnFireBallDamage(FireBall fire, ScarecrowNPC npc, HitInfo info)
        {
            if (!(npc?.gameObject?.GetComponent<Zombies>() && info.Initiator is FireBall))
            {
                return;
            }

            info.damageTypes = new DamageTypeList();
            info.DoHitEffects = false;
            info.damageTypes.ScaleAll(0f);
        }

        #endregion

        #region Event Helpers

        private void RunFX(BasePlayer player)
        {
            if (configData.DropData.FX == string.Empty) return;
            Effect.server.Run(configData.DropData.FX , player.transform.position + new Vector3(0f , 2.0f , 0f));
        }

        #region Nav & Checks
        public Vector3 GetNavPoint(Vector3 position)
        {
            NavMeshHit hit;
            if (!NavMesh.SamplePosition(position, out hit, 5, -1))
            {
                return position;
            }
            else if (Physics.RaycastAll(hit.position + new Vector3(0, 100, 0), Vector3.down, 99f, 1235288065).Any())
            {
                return position;
            }
            else if (hit.position.y < TerrainMeta.WaterMap.GetHeight(hit.position))
            {
                return position;
            }
            position = hit.position;
            return position;
        }

        private bool SpawnRate(int npcRate)
        {
        if (rnd.Next(1, 101) <= npcRate)
            {
                return true;
            }
        return false;
        }

        private bool CheckPlayer(HitInfo info)
        {
            bool Checker = false;
            BasePlayer player = info.InitiatorPlayer;
            if (player != null || player.IsValid() || info?.Initiator != null)
            {
                Checker = true;
            }
            return Checker;
        }

        private bool IsLootBarrel(BaseCombatEntity entity)
        {
            if (entity.ShortPrefabName.StartsWith("loot-barrel")) return true;
            if (entity.ShortPrefabName.StartsWith("loot_barrel")) return true;
            if (entity.ShortPrefabName.StartsWith("oil_barrel")) return true;
            if (entity.ShortPrefabName.StartsWith("diesel_barrel_world")) return true;
            return false;
        }

        private bool IsOilBarrel(BaseCombatEntity entity)
        {
            if (entity.ShortPrefabName.StartsWith("oil_barrel")) return true;
            return false;
        }

        private bool IsDiesel(BaseCombatEntity entity)
        {
            if (entity.ShortPrefabName.StartsWith("diesel_barrel_world")) return true;
            return false;
        }

        private bool IsNormalBarrel(BaseCombatEntity entity)
        {
            if (entity.ShortPrefabName.StartsWith("loot-barrel")) return true;
            if (entity.ShortPrefabName.StartsWith("loot_barrel")) return true;
            return false;
        }

        #endregion

        #region Scarecrow
        private void Spawnnpc(Vector3 position)
        {
            Vector3 pos = position + UnityEngine.Random.onUnitSphere * 1.5f;
            pos = GetNavPoint(pos);

            ScarecrowNPC npc = (ScarecrowNPC)GameManager.server.CreateEntity(zombie, pos, new Quaternion(), true);
            npc.Spawn();

            NextTick(() =>
            {
                if (npc == null)
                    return;

                var mono = npc.gameObject.AddComponent<Zombies>();
                mono.SpawnPoint = pos;

                npc.startHealth = configData.CrowData.NPCHealth;
                npc.InitializeHealth(configData.CrowData.NPCHealth, configData.CrowData.NPCHealth);
                npc.CanAttack();
                timer.Once(0.5f, () =>
                {
                Chainsaw heldEntity = npc.GetHeldEntity() as Chainsaw;
                if (heldEntity != null)
                {
                    npc.displayName = configData.CrowData.NPCName2 + " " + RandomUsernames.Get(npc.userID.Get());
                    heldEntity.SetFlag(Chainsaw.Flags.On, true);
                    heldEntity.SendNetworkUpdateImmediate();
                }
                    else
                    {
                        npc.displayName = configData.CrowData.NPCName + " " + RandomUsernames.Get(npc.userID.Get());
                    }
                });


                if (configData.CrowData.FromHell)
                {
                    var Fire = GameManager.server.CreateEntity(fireball, new Vector3(0, 1, 0), Quaternion.Euler(0, 0, 0));
                    Fire.gameObject.Identity();
                    Fire.SetParent(npc);
                    Fire.Spawn();
                    timer.Once(1f, () =>
                    {
                        if (Fire != null) Fire.Kill();
                        Puts($"{npc} spawned From Hell (on Fire)");
                    });
                }

                npc.damageScale = configData.CrowData.NPCDamageScale;

                if (configData.CrowData.UseKit && configData.CrowData.KitName.Count > 0)
                {
                    object checkKit = Kits?.CallHook("GetKitInfo", configData.CrowData.KitName[new System.Random().Next(configData.CrowData.KitName.Count())]);
                    if (checkKit == null)
                        NextTick(() =>
                        {
                            var inv_belt = npc.inventory.containerBelt;
                            var inv_wear = npc.inventory.containerWear;
                            Item eyes = ItemManager.CreateByName("gloweyes", 1, 0);
                            if (eyes != null) eyes.MoveToContainer(inv_wear);
                            PrintWarning($"Kit for {npc} does not exist - Using a default outfit.");
                        });
                    else
                    {
                        npc.inventory.Strip();
                        Kits?.Call($"GiveKit", npc, configData.CrowData.KitName[new System.Random().Next(configData.CrowData.KitName.Count())]);
                    }
                }
                if (!configData.CrowData.UseKit || configData.CrowData.KitName.Count == 0)
                {
                    var inv_belt = npc.inventory.containerBelt;
                    var inv_wear = npc.inventory.containerWear;
                    Item eyes = ItemManager.CreateByName("gloweyes", 1, 0);
                    if (eyes != null) eyes.MoveToContainer(inv_wear);
                }
            });

            if (npc.IsHeadUnderwater())
            {
                npc.Kill();
                Puts($"{npc} got destroyed for being under water!!!");
                return;
            }

            timer.Once(configData.CrowData.NPCLife * 60, () =>
            {
                if (npc != null)
                {
                    npc.Kill();
                    Puts($"{npc} Died of old age");
                }
                return;
            });
        }
        #endregion

        #region Scientist
        private void SpawnScientist(Vector3 position)
        {
            Vector3 pos = position + UnityEngine.Random.onUnitSphere * 1.5f;
            pos = GetNavPoint(pos);

            NPCPlayer npc = (NPCPlayer)GameManager.server.CreateEntity(_scientist, pos, new Quaternion(), true);
            npc.Spawn();

            NextTick(() =>
            {
                if (npc == null)
                    return;

                var mono = npc.gameObject.AddComponent<Scientists>();
                mono.SpawnPoint = pos;

                npc.startHealth = SciHealth;
                npc.InitializeHealth(SciHealth, SciHealth);
                npc.CanAttack();
                timer.Once(0.5f, () =>
                {
                    npc.displayName = SciName + " " + RandomUsernames.Get(npc.userID.Get());
                });


                if (_FromHell)
                {
                    var Fire = GameManager.server.CreateEntity(fireball, new Vector3(0, 1, 0), Quaternion.Euler(0, 0, 0));
                    Fire.gameObject.Identity();
                    Fire.SetParent(npc);
                    Fire.Spawn();
                    timer.Once(1f, () =>
                    {
                        if (Fire != null) Fire.Kill();
                        Puts($"{npc} spawned From Hell (on Fire)");
                    });
                }

                npc.damageScale = SciDamageScale;

                if (_UseKit && SciKit.Count > 0)
                {
                    object checkKit = Kits?.CallHook("GetKitInfo", SciKit[new System.Random().Next(SciKit.Count())]);
                    if (checkKit == null)
                    timer.Once(1f, () =>
                    {
                        PrintWarning($"Kit for {npc} does not exist - Using a default outfit.");
                    });
                    else
                    {
                        npc.inventory.Strip();
                        Kits?.Call($"GiveKit", npc, SciKit[new System.Random().Next(SciKit.Count())]);
                        timer.Once(1f, () =>
                        {
                            PrintWarning($"Kit for {npc} succesfully equiped.");
                        });

                    }
                }
                if (!_UseKit || SciKit.Count == 0)
                {
                    timer.Once(1f, () =>
                    {
                        PrintWarning($"{npc} spawned Using a default outfit.");
                    });
                }
            });

            if (npc.IsHeadUnderwater())
            {
                npc.Kill();
                timer.Once(1f, () =>
                {
                    PrintWarning($"{npc} got destroyed for being under water!!!");
                });
                return;
            }

            timer.Once(SciLifetime * 60, () =>
            {
                if (npc != null)
                {
                    npc.Kill();
                    Puts($"{npc} Died of old age");
                }
                return;
            });
        }

        #endregion

        #region Airdrop
        private void SpawnSupplyCrate(string prefab, Vector3 position)
        {
            Vector3 newPosition = position + new Vector3(0, configData.AirdropData.AirdropHeight, 0);
            BaseEntity SupplyCrateEntity = GameManager.server.CreateEntity(prefab, newPosition);
            if (SupplyCrateEntity != null)
            {
                SupplyDrop Drop = SupplyCrateEntity.GetComponent<SupplyDrop>();
                Drop.Spawn();
            }
        }
        #endregion

        #region hack crate
        private void SpawnHackCrate(string prefab , Vector3 position)
        {
            Vector3 newPosition = position + new Vector3(0 , configData.HackData.HackdropHeight , 0);
            BaseEntity HackCrateEntity = GameManager.server.CreateEntity(prefab , newPosition);
            if (HackCrateEntity != null)
            {
                HackableLockedCrate HackCrate = HackCrateEntity.GetComponent<HackableLockedCrate>();
                HackCrate.Spawn();
            }
        }
        #endregion

        #region Animals
        private void SpawnAnimal(Vector3 position)
        {
            Vector3 pos = position + UnityEngine.Random.onUnitSphere * 1.0f;
            pos = GetNavPoint(pos);
            BaseNpc Animal = (BaseNpc)GameManager.server.CreateEntity(_AnimalString, pos, new Quaternion(), true);
            if (Animal != null)
            {
                Animal.Spawn();
                Animal.startHealth = _AnimalHealth;
                Animal.InitializeHealth(_AnimalHealth, _AnimalHealth);
            }

            timer.Once(_AnimalLife * 60, () =>
            {
                if (Animal != null)
                {
                    Animal.Kill();
                    Puts($"The {Animal.ShortPrefabName.ToString()} Died of old age");
                }
                return;
            });
        }
        #endregion

        #region Explosives
        private void SpawnThrowable(string prefab, Vector3 position)
        {
            Vector3 newPosition = GetNavPoint(position) + new Vector3(0, 1.4f, 0);
            BaseEntity throwingitem = GameManager.server.CreateEntity(prefab , newPosition);
            if (throwingitem != null)
            {
                throwingitem.Spawn();
            }
        }

        private void SpawnFire(string prefab, Vector3 position, float Killtime)
        {
            Vector3 newPosition = GetNavPoint(position) + new Vector3(0, 1.4f, 0);
            BaseEntity fire = GameManager.server.CreateEntity(prefab , newPosition);
            if (Killtime > 20 || Killtime == 0) Killtime = 20f;
            if (fire != null)
            {
                fire.Spawn();
                timer.Once(Killtime , () =>
                {
                    fire.Kill();
                    //Puts("Killed fire"); //example
                });
            }
        }

        #endregion

        #endregion

        #region Message and helpers

        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);
        bool HasPerm(BasePlayer player , string perm) { return permission.UserHasPermission(player.UserIDString , perm); }//Check for permission

        #endregion

        #region Data helpers
        Playerinfo get_user(BasePlayer player)
        {
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(file_main + player.UserIDString))
            {
                Playerinfo user = new Playerinfo();
                user.userName = player.displayName.ToString();
                user.barrelCount = 0;
                update_user(player, user);
                return user;
            }
            else
            {
                string raw_player_file = Interface.Oxide.DataFileSystem.ReadObject<string>(file_main + player.UserIDString);
                return JsonConvert.DeserializeObject<Playerinfo>(raw_player_file);
            }
        }

        void update_user(BasePlayer player, Playerinfo user)
        {
            Interface.Oxide.DataFileSystem.WriteObject<string>(file_main + player.UserIDString, JsonConvert.SerializeObject(user));
        }

        public class Playerinfo
        {
            private string _userName;
            private int _barrelCount;

            public Playerinfo()
            {

            }

            public int barrelCount
            {
                get { return _barrelCount; }
                set { _barrelCount = value; }
            }

            public string userName
            {
                get { return _userName; }
                set { _userName = value; }
            }

        }
        #endregion
    }
}