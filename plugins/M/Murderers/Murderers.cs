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

#region Changelogs and ToDo

/**********************************************************************
* 
*   1.1.0   Cleanup and refacturing
*           Added Debug option
*           Changed murderer for ScarecrowNPC
*           Added npc health
*           Added npc random names
*           Added support for Kits and use random kits from list
*           Added support for Chainsaw when used
*           Added NpcKits block (plugin) to avoid overriding current kits
*           Improved info through responce messages
*           Added option to change displayname as murderer or scarecrow (npc will remain scarecrow)
*           Removed Wipe command (was giving issues)
*   1.1.1   Fix for some Compiler issues
*           Reformatted Turret targeting
*           Added check if kits is installed
*   1.1.2   Patch by Whispers88
*   1.1.3   Fix for compiler issues
*           
**********************************************************************/
#endregion

namespace Oxide.Plugins
{
    [Info("Murderers", "Krungh Crow", "1.1.3")]
    [Description("Murderers Reborn")]
    class Murderers : RustPlugin
    {
        [PluginReference]
        Plugin Kits;

        #region Variable

        ulong chaticon = 0;
        string prefix;
        bool Debug = false;
        public string Kit;
        public bool Turret_Safe;
        public bool Animal_Safe;
        public bool SpawnScarecrow;
        private const string _PermKill = "murderers.kill";
        private const string _PermAddLoc = "murderers.point";
        private const string _PermSpawn = "murderers.spawn";
        private Dictionary<string, int> murdersPoint = new Dictionary<string, int>();
        private Dictionary<ulong, string> npcCreated = new Dictionary<ulong, string>();
        private string npcPrefab = "assets/prefabs/npc/scarecrow/scarecrow.prefab";
        int npcCoolDown;

        #endregion

        #region Configuration
        void Init()
        {
            if (!LoadConfigVariables())
            {
                Puts("Config file issue detected. Please delete file, or check syntax and fix.");
                return;
            }

            permission.RegisterPermission(_PermKill, this);
            permission.RegisterPermission(_PermAddLoc, this);
            permission.RegisterPermission(_PermSpawn, this);

            Debug = configData.PlugCFG.Debug;
            prefix = configData.PlugCFG.Prefix;
            chaticon = configData.PlugCFG.Chaticon;
            if (Debug) Puts($"[Debug] trigger for Debug is true");

            if (!Interface.Oxide.DataFileSystem.ExistsDatafile("murdersPoint"))
            {
                if (Debug) Puts(msg("AddPoint"));
                return;
            }
            murdersPoint = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, int>>("murdersPoint");
            npcCoolDown = configData.NPCCFG.RespawnTime;
            Kit = configData.NPCCFG.KitName.ToString();
            Turret_Safe = configData.NPCCFG._TurretSafe;
            Animal_Safe = configData.NPCCFG._AnimalSafe;
            SpawnScarecrow = configData.NPCCFG.Scarecrow;
        }

        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Main config")]
            public SettingsPlugin PlugCFG = new SettingsPlugin();
            [JsonProperty(PropertyName = "NPC config")]
            public SettingsNPC NPCCFG = new SettingsNPC();
        }

        class SettingsPlugin
        {
            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
            [JsonProperty(PropertyName = "Chat Steam64ID")]
            public ulong Chaticon = 0;
            [JsonProperty(PropertyName = "Chat Prefix")]
            public string Prefix = "[<color=yellow>Murderers<color=red>Reborn</color></color>] ";
        }

        class SettingsNPC
        {
            [JsonProperty(PropertyName = "Spawn as Scarecrow (displayname only)")]
            public bool Scarecrow = true;
            [JsonProperty(PropertyName = "Health (HP)")]
            public int NPCHealth = 150;
            [JsonProperty(PropertyName = "Respawn time (seconds)")]
            public int RespawnTime = 300;
            [JsonProperty(PropertyName = "AnimalSafe")]
            public bool _AnimalSafe = true;
            [JsonProperty(PropertyName = "TurretSafe")]
            public bool _TurretSafe = true;
            [JsonProperty(PropertyName = "Kit ID")]
            public List<string> KitName = new List<string>();
        }

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

        #region LanguageAPI
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Remove"] = "Removing all Murderers",
                ["Delete"] = "Removing all Murderers Points",
                ["StartSpawn"] = "Start spawning all Murderers on their points",
                ["SavePoint"] = "Saving spawn location {0}",
                ["SaveConsole"] = "{0} Saving spawn location {1}",
                ["EndSpawn"] = "All Murderers spawned on their points",
                ["Added"] = "Added Murderers point to Data-file!",
                ["AddPoint"] = "No Murderers points found! Add new!"
            }, this);
        }

        #endregion

        #region Initialize <---- Obsolete needs to be converted

        private void OnServerInitialized()
        {
            startSpawn();
        }

        private void Unload()
        {
            Kill();
        }
        #endregion

        #region FacepunchBehaviour

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
                if (entity == null || Vector3.Distance(entity.transform.position, npc.transform.position) > 30.0f)
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
                npc.Brain.Navigator.BestRoamPointMaxDistance = 20f;
                npc.Brain.Navigator.MaxRoamDistanceFromHome = 20f;
                npc.Brain.Navigator.Init(npc, npc.Brain.Navigator.Agent);
                npc.Brain.Navigator.SetDestination(SpawnPoint, BaseNavigator.NavigationSpeed.Slow, 0f, 0f);
                #endregion

                #region senses & Targeting
                npc.Brain.ForceSetAge(0);
                npc.Brain.AllowedToSleep = false;
                npc.Brain.sleeping = false;
                npc.Brain.SenseRange = 30f;
                npc.Brain.ListenRange = 40f;
                npc.Brain.Senses.Init(npc, npc.Brain, 30, 40f, 135f,100f, true, true, true, 60f, false, false, true, EntityType.Player, true);
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
                    if (isRoaming == true && distanceHome > 20f)
                    {
                        ReturningToHome = true;
                        isRoaming = false;
                        return;
                    }
                    if (isRoaming == true && distanceHome < 20f)
                    {
                        SettargetDestination(SpawnPoint);
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

        #endregion

        #region Hooks
        void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (npcCreated.ContainsKey(entity.net.ID.Value))
            {
                startSpawn(npcCreated[entity.net.ID.Value]);
                npcCreated.Remove(entity.net.ID.Value);
            }
        }

        object CanBeTargeted(ScarecrowNPC target, MonoBehaviour turret)//stops autoturrets targetting bots
        {
            if (target != null && Turret_Safe) return false;
            return null;
        }

        object CanNpcAttack(BaseNpc npc, BaseEntity target) //nulls animal damage to bots
        {
            if (target is ScarecrowNPC && Animal_Safe) return true;
            return null;
        }


        #endregion

        #region external hooks

        #region NpcKits plugin
        object OnNpcKits(BasePlayer player)
        {
            if (player?.gameObject?.GetComponent<Zombies>() != null)
                return true;
            return null;
        }
        #endregion

        #endregion

        #region Helpers
        void startSpawn(string position = null)
        {
            var _Health = configData.NPCCFG.NPCHealth;

            if (position != null)
            {
                timer.Once(npcCoolDown, () =>
                {
                    ScarecrowNPC npc = (ScarecrowNPC)GameManager.server.CreateEntity(npcPrefab, position.ToVector3(), new Quaternion(), true);
                    if (npc != null)
                        npc.Spawn();
                    Vector3 pos = npc.transform.position;

                    npc.displayName = "Scarecrow" + " " + RandomUsernames.Get((int)npc.userID.Get());
                    if (SpawnScarecrow == false)
                    {
                        npc.inventory.containerWear.Clear();
                        npc.displayName = "Murderer" + " " + RandomUsernames.Get((int)npc.userID.Get());
                    }
                    NextTick(() =>
                    {
                        if (npc == null) return;

                        var mono = npc.gameObject.AddComponent<Zombies>();
                        mono.SpawnPoint = pos;
                        npc.startHealth = _Health;
                        npc.InitializeHealth(_Health, _Health);
                        if (Kits && configData.NPCCFG.KitName.Count > 0)
                        {
                            object checkKit = Kits?.CallHook("GetKitInfo", configData.NPCCFG.KitName[new System.Random().Next(configData.NPCCFG.KitName.Count())]);
                            if (checkKit == null)
                            {
                                if (Debug) Puts("Kit does not exist!");
                            }
                            else
                            {
                                npc.inventory.containerWear.Clear();
                                Kits?.Call($"GiveKit", npc, configData.NPCCFG.KitName[new System.Random().Next(configData.NPCCFG.KitName.Count())]);
                            }
                        }
                        npc.SendNetworkUpdate();
                    });
                });
                return;
            }

            foreach (var check in murdersPoint)
            {
                ScarecrowNPC npc = (ScarecrowNPC)GameManager.server.CreateEntity(npcPrefab, check.Key.ToVector3(), new Quaternion(), true);
                if (npc != null)
                    npc.Spawn();

                Vector3 pos = npc.transform.position;

                npc.displayName = "Scarecrow" + " " + RandomUsernames.Get((int)npc.userID.Get());
                if (SpawnScarecrow == false)
                {
                    npc.inventory.containerWear.Clear();
                    npc.displayName = "Murderer" + " " + RandomUsernames.Get((int)npc.userID.Get());
                }

                npcCreated.Add(npc.net.ID.Value, check.Key);
                NextTick(() =>
                {
                    if (npc == null) return;

                    var mono = npc.gameObject.AddComponent<Zombies>();
                    mono.SpawnPoint = pos;
                    npc.startHealth = _Health;
                    npc.InitializeHealth(_Health, _Health);
                    if (Kits && configData.NPCCFG.KitName.Count > 0)
                    {
                        object checkKit = Kits?.CallHook("GetKitInfo", configData.NPCCFG.KitName[new System.Random().Next(configData.NPCCFG.KitName.Count())]);
                        if (checkKit == null)
                        {
                            if (Debug) Puts("Kit does not exist!");
                        }
                        else
                        {
                            npc.inventory.containerWear.Clear();
                            Kits?.Call($"GiveKit", npc, configData.NPCCFG.KitName[new System.Random().Next(configData.NPCCFG.KitName.Count())]);
                        }
                    }
                    npc.SendNetworkUpdate();
                });
            }
            if (Debug) Puts(msg("EndSpawn"));
        }

        void Kill()
        {
            foreach (var check in npcCreated)
            {
                BaseNetworkable.serverEntities.Find(new NetworkableId(check.Key)).Kill();
            }
        }

        private void Reload()
        {
            Interface.Oxide.ReloadPlugin("Murderers");
            startSpawn();
        }

        #endregion

        #region Console Commands
        [ConsoleCommand("m.spawn")]
        void CmdSpawn(ConsoleSystem.Arg arg)
        {
            Zombies[] zombies = UnityEngine.Object.FindObjectsOfType<Zombies>();
            if (zombies != null)
            {
                foreach (Zombies zombie in zombies)
                    UnityEngine.Object.Destroy(zombie);
            }
            startSpawn();
        }

        [ConsoleCommand("m.kill")]
        void CmdBotKill(ConsoleSystem.Arg arg)
        {
            Kill();
        }
        #endregion

        #region chat commands
        [ChatCommand("m.spawn")]
        void npcSpawn(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, _PermSpawn)) return;
            Zombies[] zombies = UnityEngine.Object.FindObjectsOfType<Zombies>();
            if (zombies != null)
            {
                foreach (Zombies zombie in zombies)
                    UnityEngine.Object.Destroy(zombie);
            }
            startSpawn();
            {
                Player.Message(player, prefix + string.Format(msg("StartSpawn", player.UserIDString)), chaticon);
            }
        }

        [ChatCommand("m.point")]
        void npcMain(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _PermAddLoc)) return;
            {
                int amount = 1;

                if (args.Length == 1 && Int32.Parse(args[0]) > 0)
                {
                    amount = Int32.Parse(args[0]);
                }
                var location = player.transform.position.ToString();
                murdersPoint.Add(location, amount);
                Interface.Oxide.DataFileSystem.WriteObject("murdersPoint", murdersPoint);
                if (Debug) Puts(msg($"{player} created a new spawn location at {location}"));
                Player.Message(player, prefix + string.Format(msg("SavePoint", player.UserIDString), location), chaticon);
                timer.Once(3.0f, () =>
                {
                    startSpawn();
                    if (Debug) Puts(msg("StartSpawn"));
                    Player.Message(player, prefix + string.Format(msg("StartSpawn", player.UserIDString), location), chaticon);

                });
            }
        }

        [ChatCommand("m.kill")]
        void npcKill(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, _PermKill)) return;
            {
                Kill();
                Player.Message(player, prefix + string.Format(msg("Remove", player.UserIDString)), chaticon);
            }
        }
        #endregion

        #region msg helper
        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);
        #endregion
    }
}