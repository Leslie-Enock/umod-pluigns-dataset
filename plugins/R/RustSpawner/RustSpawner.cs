using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Rust Spawner ", "Daano123", "3.0.1")]
    [Description("Rust Spawner Reworked is a plugin that you can spawn cars/helis/boats/animals/scarecrow. REWORKED Zoin plugin")]
    class RustSpawner : CovalencePlugin
    {
        #region Variables
        Dictionary<string, Timer> CoolDownsHorse = new Dictionary<string, Timer>();
        Dictionary<string, Timer> CoolDownsWolf = new Dictionary<string, Timer>();
        Dictionary<string, Timer> CoolDownsBear = new Dictionary<string, Timer>();
        Dictionary<string, Timer> CoolDownsMini = new Dictionary<string, Timer>();
        Dictionary<string, Timer> CoolDownsSedan = new Dictionary<string, Timer>();
        Dictionary<string, Timer> CoolDownsScrapHeli = new Dictionary<string, Timer>();
        Dictionary<string, Timer> CoolDownsChinook = new Dictionary<string, Timer>();
        Dictionary<string, Timer> CoolDownsRhib = new Dictionary<string, Timer>();
        Dictionary<string, Timer> CoolDownsBoat = new Dictionary<string, Timer>();

        const string Horse_Perm = "rustspawner.horse";
        const string Wolf_Perm = "rustspawner.wolf";
        const string Bear_Perm = "rustspawner.bear";
        const string Sedan_Perm = "rustspawner.sedan";
        const string Minicopter_Perm = "rustspawner.minicopter";
        const string ScrapHeli_Perm = "rustspawner.scrapheli";
        const string Chinook_Perm = "rustspawner.chinook";
        const string Rhib_Perm = "rustspawner.rhib";
        const string Boat_Perm = "rustspawner.boat";
        const string NoCooldown_Perm = "rustspawner.nocooldown";
        #endregion

        #region Configuaration
        protected override void LoadDefaultConfig()
        {
            LogWarning("Creating a new configuration file");
            Config["CooldownHorse"] = 3600;
            Config["CooldownWolf"] = 3600;
            Config["CooldownBear"] = 3600;
            Config["CooldownMini"] = 3600;
            Config["CooldownSedan"] = 3600;
            Config["CooldownScrapHeli"] = 14400;
            Config["CooldownChinook"] = 86400;
            Config["CooldownRhib"] = 14400;
            Config["CooldownBoat"] = 3600;
            Config["BuildingSpawn"] = false;
        }
        #endregion

        #region LanguageAPI
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You do not have access to that command!",
                ["InvalidInput"] = "Please enter a valid spawnable name!",
                ["IndoorsBlocked"] = "You cannot spawn indoors only outside!",
                ["Info"] = "You can spawn the following entitys\nhorse, wolf, bear, mini, scrapheli, chinook, sedan, rhib, boat\nIE /rspawn mini",
                ["Cooldown"] = "You are still on cooldown!",
                ["OutOfReach"] = "Please look closer to the ground!",
                ["Spawned"] = "Your {0} has been spawned!",
                ["Prefix"] = "[Rust Spawner] "
            }, this);
        }
        #endregion

        #region Hooks
        private void Init()
        {
            permission.RegisterPermission(NoCooldown_Perm, this);
            permission.RegisterPermission(Horse_Perm, this);
            permission.RegisterPermission(Wolf_Perm, this);
            permission.RegisterPermission(Bear_Perm, this);
            permission.RegisterPermission(Minicopter_Perm, this);
            permission.RegisterPermission(Sedan_Perm, this);
            permission.RegisterPermission(ScrapHeli_Perm, this);
            permission.RegisterPermission(Chinook_Perm, this);
            permission.RegisterPermission(Rhib_Perm, this);
            permission.RegisterPermission(Boat_Perm, this);
        }
        #endregion

        #region Commands
        [Command("rspawn")]
        private void Spawn(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                return;
            }

            string prefix = lang.GetMessage("Prefix", this, player.Id);

            if (args.Length < 1)
            {
                player.Reply(prefix + lang.GetMessage("InvalidInput", this, player.Id));
                return;
            }

            switch (args[0])
            {
                case "horse":
                    {
                        if (player.HasPermission(Horse_Perm))
                            SpawnEntity(player, "assets/rust.ai/nextai/testridablehorse.prefab", "CooldownHorse");
                        else
                            player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                        break;
                    }
                case "wolf":
                    {
                        if (player.HasPermission(Wolf_Perm))
                            SpawnEntity(player, "assets/rust.ai/agents/wolf/wolf.prefab", "CooldownWolf");
                        else
                            player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                        break;
                    }
                case "bear":
                    {
                        if (player.HasPermission(Bear_Perm))
                            SpawnEntity(player, "assets/rust.ai/agents/bear/bear.prefab", "CooldownBear");
                        else
                            player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                        break;
                    }
                case "mini":
                    {
                        if (player.HasPermission(Minicopter_Perm))
                            SpawnEntity(player, "assets/content/vehicles/minicopter/minicopter.entity.prefab", "CooldownMini");
                        else
                            player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                        break;
                    }
                case "scrapheli":
                    {
                        if (player.HasPermission(ScrapHeli_Perm))
                            SpawnEntity(player, "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab", "CooldownScrapHeli");
                        else
                            player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                        break;
                    }
                case "chinook":
                    {
                        if (player.HasPermission(Chinook_Perm))
                            SpawnEntity(player, "assets/prefabs/npc/ch47/ch47.entity.prefab", "CooldownChinook");
                        else
                            player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                        break;
                    }
                case "car":
                    {
                        if (player.HasPermission(Sedan_Perm))
                            SpawnEntity(player, "assets/content/vehicles/sedan_a/sedantest.entity.prefab", "CooldownSedan");
                        else
                            player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                        break;
                    }
                case "rhib":
                    {
                        if (player.HasPermission(Rhib_Perm))
                            SpawnEntity(player, "assets/content/vehicles/boats/rhib/rhib.prefab", "CooldownRhib");
                        else
                            player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                        break;
                    }
                case "boat":
                    {
                        if (player.HasPermission(Boat_Perm))
                            SpawnEntity(player, "assets/content/vehicles/boats/rowboat/rowboat.prefab", "CooldownBoat");
                        else
                            player.Reply(prefix + lang.GetMessage("NoPermission", this, player.Id));
                        break;
                    }
                case "info":
                    {
                            player.Reply(prefix + lang.GetMessage("Info", this, player.Id));
                        break;
                    }
                default:
                    player.Reply(prefix + lang.GetMessage("InvalidInput", this, player.Id));
                    break;
            }
        }
        #endregion

        #region Functions
        void SpawnEntity(IPlayer player, string entity_name, string cooldown)
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

            #region Cooldown
            if (!player.HasPermission(NoCooldown_Perm))
            {
                switch (cooldown)
                {
                    case "CooldownHorse":
                        {
                            if (CoolDownsHorse.ContainsKey(player.Id))
                            {
                                player.Reply(prefix + lang.GetMessage("Cooldown", this, player.Id));
                                return;
                            }
                            else {
                                object timerObj = Config["CooldownHorse"];
                                float timerTime = float.Parse(timerObj.ToString());
                                string id = player.Id;
                                Timer Cooldown = timer.Once(timerTime, () =>
                                {
                                    CoolDownsHorse.Remove(id);
                                });
                                CoolDownsHorse.Add(id, Cooldown);
                            }
                            break;
                        }
                    case "CooldownWolf":
                        {
                            if (CoolDownsWolf.ContainsKey(player.Id))
                            {
                                player.Reply(prefix + lang.GetMessage("Cooldown", this, player.Id));
                                return;
                            }
                            else
                            {
                                object timerObj = Config["CooldownWolf"];
                                float timerTime = float.Parse(timerObj.ToString());
                                string id = player.Id;
                                Timer Cooldown = timer.Once(timerTime, () =>
                                {
                                    CoolDownsWolf.Remove(id);
                                });
                                CoolDownsWolf.Add(id, Cooldown);
                            }
                            break;
                        }
                    case "CooldownBear":
                        {
                            if (CoolDownsBear.ContainsKey(player.Id))
                            {
                                player.Reply(prefix + lang.GetMessage("Cooldown", this, player.Id));
                                return;
                            }
                            else
                            {
                                object timerObj = Config["CooldownBear"];
                                float timerTime = float.Parse(timerObj.ToString());
                                string id = player.Id;
                                Timer Cooldown = timer.Once(timerTime, () =>
                                {
                                    CoolDownsBear.Remove(id);
                                });
                                CoolDownsBear.Add(id, Cooldown);
                            }
                            break;
                        }
                    case "CooldownMini":
                        {
                            if (CoolDownsMini.ContainsKey(player.Id))
                            {
                                player.Reply(prefix + lang.GetMessage("Cooldown", this, player.Id));
                                return;
                            }
                            else
                            {
                                object timerObj = Config["CooldownMini"];
                                float timerTime = float.Parse(timerObj.ToString());
                                string id = player.Id;
                                Timer Cooldown = timer.Once(timerTime, () =>
                                {
                                    CoolDownsMini.Remove(id);
                                });
                                CoolDownsMini.Add(id, Cooldown);
                            }
                            break;
                        }
                    case "CooldownSedan":
                        {
                            if (CoolDownsSedan.ContainsKey(player.Id))
                            {
                                player.Reply(prefix + lang.GetMessage("Cooldown", this, player.Id));
                                return;
                            }
                            else
                            {
                                object timerObj = Config["CooldownSedan"];
                                float timerTime = float.Parse(timerObj.ToString());
                                string id = player.Id;
                                Timer Cooldown = timer.Once(timerTime, () =>
                                {
                                    CoolDownsSedan.Remove(id);
                                });
                                CoolDownsSedan.Add(id, Cooldown);
                            }
                            break;
                        }
                    case "CooldownScrapHeli":
                        {
                            if (CoolDownsScrapHeli.ContainsKey(player.Id))
                            {
                                player.Reply(prefix + lang.GetMessage("Cooldown", this, player.Id) + CoolDownsScrapHeli[player.Id]);
                                return;
                            }
                            else
                            {
                                object timerObj = Config["CooldownScrapHeli"];
                                float timerTime = float.Parse(timerObj.ToString());
                                string id = player.Id;
                                Timer Cooldown = timer.Once(timerTime, () =>
                                {
                                    CoolDownsScrapHeli.Remove(id);
                                });
                                CoolDownsScrapHeli.Add(id, Cooldown);
                            }
                            break;
                        }
                    case "CooldownChinook":
                        {
                            if (CoolDownsChinook.ContainsKey(player.Id))
                            {
                                player.Reply(prefix + lang.GetMessage("Cooldown", this, player.Id));
                                return;
                            }
                            else
                            {
                                object timerObj = Config["CooldownChinook"];
                                float timerTime = float.Parse(timerObj.ToString());
                                string id = player.Id;
                                Timer Cooldown = timer.Once(timerTime, () =>
                                {
                                    CoolDownsChinook.Remove(id);
                                });
                                CoolDownsChinook.Add(id, Cooldown);
                            }
                            break;
                        }
                    case "CooldownRhib":
                        {
                            if (CoolDownsRhib.ContainsKey(player.Id))
                            {
                                player.Reply(prefix + lang.GetMessage("Cooldown", this, player.Id));
                                return;
                            }
                            else
                            {
                                object timerObj = Config["CooldownRhib"];
                                float timerTime = float.Parse(timerObj.ToString());
                                string id = player.Id;
                                Timer Cooldown = timer.Once(timerTime, () =>
                                {
                                    CoolDownsRhib.Remove(id);
                                });
                                CoolDownsRhib.Add(id, Cooldown);
                            }
                            break;
                        }
                    case "CooldownBoat":
                        {
                            if (CoolDownsBoat.ContainsKey(player.Id))
                            {
                                player.Reply(prefix + lang.GetMessage("Cooldown", this, player.Id));
                                return;
                            }
                            else
                            {
                                object timerObj = Config["CooldownBoat"];
                                float timerTime = float.Parse(timerObj.ToString());
                                string id = player.Id;
                                Timer Cooldown = timer.Once(timerTime, () =>
                                {
                                    CoolDownsBoat.Remove(id);
                                });
                                CoolDownsBoat.Add(id, Cooldown);
                            }
                            break;
                        }
                    default:
                        break;
                }
            }
            #endregion

            #region Building Check
            if (!(bool)Config["BuildingSpawn"] & !PlayerObject.IsOutside())
            {
                player.Reply(prefix + lang.GetMessage("IndoorsBlocked", this, player.Id));
                return;
            }
            #endregion


            #region Actual Spawning
            BaseEntity Entity = GameManager.server.CreateEntity(entity_name, hit.point);

            if (Entity)
            {
                Entity.Spawn();
                player.Reply(prefix + string.Format(lang.GetMessage("Spawned", this, player.Id), Entity.ShortPrefabName));
            }
            #endregion
        }
        #endregion
    }
}