using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Oxide.Plugins
{
    [Info("Resource VIP Spawns", "Daano123", "1.0.0")]
    [Description("Resource VIP Spawns is a plugin that allows players with the correct permissions to set a spawn point for resources that respawn every X seconds")]
    class ResourceVIPSpawns : CovalencePlugin
    {
        #region Variables
        Dictionary<string, Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer>> SpawnPointsSet = new Dictionary<string, Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer>>();

        const string Admin_Perm = "resourcevipspawns.admin";
        const string Spawnable_Perm = "resourcevipspawns.spawnable";

        readonly string SulfurAssetLocation = "assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab";
        readonly string MetalAssetLocation = "assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab";
        readonly string StoneAssetLocation = "assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab";
        #endregion

        #region Configuaration
        protected override void LoadDefaultConfig()
        {
            LogWarning("Creating a new configuration file");
            Config["CooldownSulfurSpawn"] = 300;
            Config["CooldownMetalSpawn"] = 300;
            Config["CooldownStoneSpawn"] = 300;
            Config["AllowedOutsideBuildingPrivilege"] = false;
            Config["EnableSpawnablesWhenOffline"] = false;
            Config["DebugMode"] = false;
        }
        #endregion

        #region LanguageAPI
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You do not have access to that command!",
                ["InvalidInput"] = "Please enter valid command syntax!",
                ["OutdoorsBlocked"] = "You cannot set resource spawnpoints outside of building privelige areas",
                ["SpawnPointAlreadySet"] = "You already have a spawnpoint set for this resource!",
                ["RemoveAll"] = "Your resource spawn data has been wiped succesfully!",
                ["RemoveIndividual"] = "Your resource spawn data has been wiped succesfully for this ore type!",
                ["Info"] = "<br>Set up to 3 spawn points for resources with this plugin. <br>Commands: <br>/rs add/remove sulfur/metal/stone <br>Example: <br>/rs add sulfur - sets spawn point for sulfur" +
                " <br>/rs remove sulfur - removes spawn point for sulfur <br>/rs remove all - wipes all spawn points",
                ["AdminInfo"] = "<br>Currect respawn timers: <br>Sulfur - {0}<br>Metal - {1}<br>Stone - {2}",
                ["OutOfReach"] = "Please look closer to the ground!",
                ["Spawned"] = "The following ore spawnpoint has been set: ",
                ["Prefix"] = "<color=#ff6100>[Resource VIP] </color>"
            }, this);
        }
        #endregion

        #region Hooks
        private void Init()
        {
            permission.RegisterPermission(Admin_Perm, this);
            permission.RegisterPermission(Spawnable_Perm, this);
        }
        #endregion

        #region Commands
        [Command("rs")]
        private void Spawn(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                //SpawnHandler.Instance.EnforceLimits
                return;
            }

            string prefix = lang.GetMessage("Prefix", this, player.Id);

            if (args.Length < 1)
            {
                player.Reply(prefix + lang.GetMessage("Info", this, player.Id));
                return;
            }

            switch (args[0].ToLower())
            {
                case "add":
                    {
                        #region addSpawns
                        switch (args[1].ToLower())
                        {
                            case "sulfur":
                                {
                                    if (player.HasPermission(Spawnable_Perm))
                                        SpawnEntity(player, SulfurAssetLocation);
                                    else
                                        player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                                    break;
                                }
                            case "metal":
                                {
                                    if (player.HasPermission(Spawnable_Perm))
                                        SpawnEntity(player, MetalAssetLocation);
                                    else
                                        player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                                    break;
                                }
                            case "stone":
                                {
                                    if (player.HasPermission(Spawnable_Perm))
                                        SpawnEntity(player, StoneAssetLocation);
                                    else
                                        player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                                    break;
                                }
                            default:
                                player.Reply(prefix + lang.GetMessage("InvalidInput", this, player.Id));
                                break;
                        }
                        break;
                        #endregion
                    }
                case "remove":
                    {
                        #region removeSpawns
                        switch (args[1].ToLower())
                        {
                            case "all":
                                {
                                    if (player.HasPermission(Spawnable_Perm))
                                    {
                                        Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer> DestroyableTimers;

                                        SpawnPointsSet.TryGetValue(player.Id, out DestroyableTimers);
                                        if(DestroyableTimers.Item4 != null)
                                            DestroyableTimers.Item4.Destroy();
                                        if(DestroyableTimers.Item5 != null)
                                            DestroyableTimers.Item5.Destroy();
                                        if(DestroyableTimers.Item6 != null)
                                            DestroyableTimers.Item6.Destroy();
                                        SpawnPointsSet.Remove(player.Id);
                                        player.Reply(prefix + lang.GetMessage("RemoveAll", this, player.Id));
                                    }
                                    else 
                                        player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                                    break;
                                }
                            case "sulfur":
                                {
                                    if (player.HasPermission(Spawnable_Perm))
                                    {
                                        Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer> DestroyableTimers;

                                        SpawnPointsSet.TryGetValue(player.Id, out DestroyableTimers);
                                        DestroyableTimers.Item4.Destroy();
                                        SpawnPointsSet.Remove(player.Id);
                                        Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer> Data = new Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer>(new Vector3(0,0,0), DestroyableTimers.Item2,
                                            DestroyableTimers.Item3, null, DestroyableTimers.Item5, DestroyableTimers.Item6);
                                        SpawnPointsSet.Add(player.Id, Data);
                                        player.Reply(prefix + lang.GetMessage("RemoveIndividual", this, player.Id));
                                    }
                                    else
                                        player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                                    break;
                                }
                            case "metal":
                                {
                                    if (player.HasPermission(Spawnable_Perm))
                                    {
                                        Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer> DestroyableTimers;

                                        SpawnPointsSet.TryGetValue(player.Id, out DestroyableTimers);
                                        DestroyableTimers.Item5.Destroy();
                                        SpawnPointsSet.Remove(player.Id);
                                        Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer> Data = new Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer>(DestroyableTimers.Item1, new Vector3(0, 0, 0),
                                            DestroyableTimers.Item3, DestroyableTimers.Item4, null, DestroyableTimers.Item6);
                                        SpawnPointsSet.Add(player.Id, Data);
                                        player.Reply(prefix + lang.GetMessage("RemoveIndividual", this, player.Id));
                                    }
                                    else
                                        player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                                    break;
                                }
                            case "stone":
                                {
                                    if (player.HasPermission(Spawnable_Perm))
                                    {
                                        Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer> DestroyableTimers;

                                        SpawnPointsSet.TryGetValue(player.Id, out DestroyableTimers);
                                        DestroyableTimers.Item6.Destroy();
                                        SpawnPointsSet.Remove(player.Id);
                                        Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer> Data = new Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer>(DestroyableTimers.Item1, DestroyableTimers.Item2,
                                            new Vector3(0, 0, 0), DestroyableTimers.Item4, DestroyableTimers.Item5, null);
                                        SpawnPointsSet.Add(player.Id, Data);
                                        player.Reply(prefix + lang.GetMessage("RemoveIndividual", this, player.Id));
                                    }
                                    else
                                        player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                                    break;
                                }
                            default:
                                    player.Reply(prefix + lang.GetMessage("InvalidInput", this, player.Id));
                                    break;
                        }
                        break;
                        #endregion
                    }
                case "admin":
                    {
                        if (player.HasPermission(Admin_Perm))
                            player.Reply(prefix + string.Format(lang.GetMessage("AdminInfo", this, player.Id), float.Parse(Config["CooldownSulfurSpawn"].ToString()), float.Parse(Config["CooldownMetalSpawn"].ToString()), float.Parse(Config["CooldownStoneSpawn"].ToString())));
                        else
                            player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                        break;
                    }
                default:
                    player.Reply(prefix + lang.GetMessage("Info", this, player.Id));
                    break;
            }

        }
        #endregion

        #region Functions
        void SpawnEntity(IPlayer player, string entity_name)
        {
            BasePlayer PlayerObject = player.Object as BasePlayer;
            string prefix = lang.GetMessage("Prefix", this, player.Id);

            #region Raycast
            Vector3 ViewAdjust = new Vector3(0f, 1.5f, 0f);
            Vector3 position = PlayerObject.transform.position + ViewAdjust;
            Vector3 rotation = Quaternion.Euler(PlayerObject.serverInput.current.aimAngles) * Vector3.forward;
            int range = 10;

            RaycastHit hit;
            if (!Physics.Raycast(position, rotation, out hit, range))
            {
                player.Reply(prefix + lang.GetMessage("OutOfReach", this, player.Id));
                return;
            }
            #endregion

            #region Building Check
            if (!(bool)Config["AllowedOutsideBuildingPrivilege"] & !PlayerObject.IsBuildingAuthed())
            {
                player.Reply(prefix + lang.GetMessage("OutdoorsBlocked", this, player.Id));
                return;
            }
            #endregion

            #region Single Spawn Check
            switch (entity_name)
            {
                case "assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab":
                    {
                        if (SpawnPointsSet.ContainsKey(player.Id))
                        {
                            Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer> vectors3;
                            SpawnPointsSet.TryGetValue(player.Id, out vectors3);
                            if (vectors3.Item1.x != 0 & vectors3.Item1.y != 0 & vectors3.Item1.z != 0)
                            { 
                                player.Reply(prefix + lang.GetMessage("SpawnPointAlreadySet", this, player.Id));
                                return;
                            }
                            Vector3 i1 = hit.point;
                            Vector3 i2 = vectors3.Item2;
                            Vector3 i3 = vectors3.Item3;
                            Timer t2 = vectors3.Item5;
                            Timer t3 = vectors3.Item6;
                            SpawnPointsSet.Remove(player.Id);
                            object timerObj = Config["CooldownSulfurSpawn"];
                            float timerTime = float.Parse(timerObj.ToString());
                            Timer t1 = TimerHandler(player, timerTime, entity_name, hit.point);
                            SpawnPointsSet.Add(player.Id, new Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer>(i1, i2, i3, t1, t2, t3));
                        }
                        else
                        {
                            object timerObj = Config["CooldownSulfurSpawn"];
                            float timerTime = float.Parse(timerObj.ToString());
                            Timer timer = TimerHandler(player, timerTime, entity_name, hit.point);
                            SpawnPointsSet.Add(player.Id, new Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer>(hit.point, new Vector3(0, 0, 0), new Vector3(0, 0, 0), timer, null, null));
                        }
                        break;
                    }
                case "assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab":
                    {
                        if (SpawnPointsSet.ContainsKey(player.Id))
                        {
                            Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer> vectors3;
                            SpawnPointsSet.TryGetValue(player.Id, out vectors3);
                            if (vectors3.Item2.x != 0 & vectors3.Item2.y != 0 & vectors3.Item2.z != 0)
                            {
                                player.Reply(prefix + lang.GetMessage("SpawnPointAlreadySet", this, player.Id));
                                return;
                            }
                            Vector3 i1 = vectors3.Item1;
                            Vector3 i2 = hit.point;
                            Vector3 i3 = vectors3.Item3;
                            Timer t1 = vectors3.Item4;
                            Timer t3 = vectors3.Item6;

                            SpawnPointsSet.Remove(player.Id);
                            object timerObj = Config["CooldownMetalSpawn"];
                            float timerTime = float.Parse(timerObj.ToString());
                            Timer t2 = TimerHandler(player, timerTime, entity_name, hit.point);
                            SpawnPointsSet.Add(player.Id, new Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer>(i1, i2, i3, t1, t2, t3));
                        }
                        else
                        {
                            object timerObj = Config["CooldownMetalSpawn"];
                            float timerTime = float.Parse(timerObj.ToString());
                            Timer timer = TimerHandler(player, timerTime, entity_name, hit.point);
                            SpawnPointsSet.Add(player.Id, new Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer>(new Vector3(0, 0, 0), hit.point, new Vector3(0, 0, 0), null, timer, null));
                        }
                        break;
                    }
                case "assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab":
                    {
                        if (SpawnPointsSet.ContainsKey(player.Id))
                        {
                            Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer> vectors3;
                            SpawnPointsSet.TryGetValue(player.Id, out vectors3);
                            if (vectors3.Item3.x != 0 & vectors3.Item3.y != 0 & vectors3.Item3.z != 0)
                            {
                                player.Reply(prefix + lang.GetMessage("SpawnPointAlreadySet", this, player.Id));
                                return;
                            }
                            Vector3 i1 = vectors3.Item1;
                            Vector3 i2 = vectors3.Item2;
                            Vector3 i3 = hit.point;
                            Timer t1 = vectors3.Item4;
                            Timer t2 = vectors3.Item6;
                            SpawnPointsSet.Remove(player.Id);
                            object timerObj = Config["CooldownStoneSpawn"];
                            float timerTime = float.Parse(timerObj.ToString());
                            Timer t3 = TimerHandler(player, timerTime, entity_name, hit.point);
                            SpawnPointsSet.Add(player.Id, new Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer>(i1, i2, i3, t1, t2, t3));
                        }
                        else
                        {
                            object timerObj = Config["CooldownStoneSpawn"];
                            float timerTime = float.Parse(timerObj.ToString());
                            Timer timer = TimerHandler(player, timerTime, entity_name, hit.point);
                            SpawnPointsSet.Add(player.Id, new Tuple<Vector3, Vector3, Vector3, Timer, Timer, Timer>(new Vector3(0, 0, 0), new Vector3(0, 0, 0), hit.point, null, null, timer));
                        }
                        break;
                    }
                default:
                    break;
            }
            #endregion
        }

        #region Timer Handler
        private Timer TimerHandler(IPlayer player, float timerTime, string entity_name, Vector3 hit)
        {
            string prefix = lang.GetMessage("Prefix", this, player.Id);
            Timer Cooldown = timer.Every(timerTime, () =>
            {
                if((bool)Config["EnableSpawnablesWhenOffline"] | player.IsConnected)
                {
                    BaseEntity Entity = GameManager.server.CreateEntity(entity_name, hit);
                    if (Entity)
                    {
                        Entity.Spawn();
                        //Debug Mode Logging
                        if(player.HasPermission(Admin_Perm) & (bool)Config["DebugMode"])
                            player.Reply(prefix + "Resource spawned " + Entity.ShortPrefabName);
                    }
                }
            });
            player.Reply(prefix + lang.GetMessage("Spawned", this, player.Id) + GameManager.server.CreateEntity(entity_name).ShortPrefabName);
            return Cooldown;
        }
        #endregion

        #endregion
    }
}