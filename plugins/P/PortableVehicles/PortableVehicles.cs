/*
 ########### README ####################################################
                                                                             
  !!! DON'T EDIT THIS FILE !!!
  Orange is the original developer of this plugin
                                                                     
 ########### CHANGES ###################################################

 1.1.2
    - Rewrited config
    - Added chat command
    - Added chat icon option
    - Added more localization messages
    - Added option blacklist pickupable vehicles
    - Added item name vehicle
    - Fixed Pickupable Hot Air Balloon
    - Added submarinesolo
    - Added submarineduo
    - Added snowmobile
    - Added option what item you will be placing
 1.1.3
    - Changed skinId icons
    - Added tugboat
    - Added tomaha snowmobile
    - Added option item for big models
    - Added option pickup any vehicles
    - Added option pickup own vehicles
    - Added option require building priviledge
 1.1.4
    - Fixed minicopter skinid
 1.1.5
    - Added Attack Helicopter
 1.1.6
    - Rust Update Fix
 1.1.7
    - Added motorbike
    - Added bicykle
    - Added trike
    - Fixed pickup conflict with repair
 1.1.8
    - Changed ridable horse prefab
    - Remove blacklist vehicles from config
    - Added option check storages before pickup
    - Fixed pickup
    - Added option auto fuel to vehicle
 1.1.9
    - Added siegeweapons
    - Added option ignore repair vehicles

 #######################################################################
*/

using System.Collections.Generic;
using System.Linq;
using ConVar;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("Portable Vehicles", "Paulsimik", "1.1.9")]
    [Description("Give vehicles as item to your players")]
    public class PortableVehicles : RustPlugin
    {
        #region [Fields]

        private static Configuration config;
        private const string permUse = "portablevehicles.use";
        private const string permAdmin = "portablevehicles.admin";
        private const string permPickup = "portablevehicles.pickup";
        private const string permFuel = "portablevehicles.fuel";
        private string[] chatCommands = { "pv", "portablevehicles", "portablevehicle" };

        #endregion

        #region [Oxide Hooks]

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permPickup, this);
            permission.RegisterPermission(permFuel, this);

            foreach (var command in chatCommands)
                cmd.AddChatCommand(command, this, nameof(cmdPortableVehicles));
        }

        private void OnEntityBuilt(Planner plan, GameObject go) => CheckPlacement(plan, go);

        private object OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (info.HitEntity is BaseVehicle)
                return CheckPickup(player, info.HitEntity as BaseVehicle);

            if (info.HitEntity is HotAirBalloon)
                return CheckPickupBalloon(player, info.HitEntity as HotAirBalloon);

            return null;
        }

        #endregion

        #region [Hooks]   

        private void CheckPlacement(Planner plan, GameObject go)
        {
            var entity = go.ToBaseEntity();
            if (entity == null)
                return;

            var player = plan.GetOwnerPlayer();
            var info = AllVehicles.FirstOrDefault(x => x.skinId == entity.skinID);
            if (info == null)
                return;

            var transform = entity.transform;
            var position = transform.position;
            var rotation = transform.rotation;
            var owner = entity.OwnerID;
            var skin = entity.skinID;

            transform.position = new Vector3();
            entity.TransformChanged();
            timer.Once(1f, () =>
            {
                if (entity.IsValid() && !entity.IsDestroyed)
                {
                    entity.Kill();
                }
            });

            var time = 1f;
            if (info.bigModel)
            {
                time = 3f;
                Message(player, "BigModel");
            }

            timer.Once(time, () =>
            {
                var vehicle = GameManager.server.CreateEntity(info.prefab, position, rotation);
                if (vehicle != null)
                {
                    vehicle.skinID = skin;
                    vehicle.OwnerID = owner;
                    vehicle.Spawn();

                    if (vehicle is HotAirBalloon)
                    {
                        var balloon = (HotAirBalloon)vehicle;
                        if (balloon != null)
                        {
                            AddFuelBalloon(player, balloon);
                        }

                        return;
                    }

                    if (vehicle is BaseVehicle)
                    {
                        var baseVehicle = (BaseVehicle)vehicle;
                        if (baseVehicle != null)
                        {
                            AddFuel(player, baseVehicle);

                            if (config.autoMount)
                            {
                                if (baseVehicle.mountPoints != null && baseVehicle.mountPoints.Count > 0)
                                {
                                    var driverSeat = baseVehicle.mountPoints.FirstOrDefault()?.mountable;
                                    if (driverSeat != null)
                                    {
                                        driverSeat.MountPlayer(player);
                                        player.SendNetworkUpdate();
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    SendMessage(player, "<size=12><color=#ff6666>Portable Vehicles: Failed to spawn vehicle!</color></size>");
                }
            });
        }

        private object CheckPickup(BasePlayer player, BaseVehicle entity)
        {
            if (entity == null)
                return null;

            if (!config.pickupAnyVehicles && entity.skinID == 0)
                return null;

            if (!permission.UserHasPermission(player.UserIDString, permPickup))
                return null;

            if (config.pickupableBlacklist.Contains(entity.ShortPrefabName))
            {
                Message(player, "Pickupable");
                return null;
            }

            if (config.pickupOwnVehicles && entity.skinID != 0 && entity.OwnerID != player.userID)
            {
                Message(player, "Pickup Ownership");
                return null;
            }

            if (config.requireBuildingPrivilege && !player.CanBuild())
            {
                Message(player, "Cupboard");
                return null;
            }

            var time = entity.SecondsSinceAttacked;
            if (time < 30)
            {
                Message(player, "Recently Attacked", (30 - time).ToString("0.0"));
                return true;
            }

            var diff = (Mathf.Abs(entity.MaxHealth() - entity.Health()));
            if (config.needRepair && diff > 5f)
            {
                Message(player, "Durability");
                return null;
            }

            if (config.checkStorages)
            {
                var containers = entity.GetComponentsInChildren<StorageContainer>();
                if (containers.Any(x => x.inventory.itemList.Count > 0))
                {
                    Message(player, "Not Empty");
                    return true;
                }

                var fs = entity.GetFuelSystem() as EntityFuelSystem;
                if (fs != null && !fs.fuelStorageInstance.Get(true).IsLocked() && fs.HasFuel())
                {
                    Message(player, "Fuel");
                    return true;
                }
            }

            var script = entity.GetOrAddComponent<PickupScript>();
            script.AddHit();
            var left = script.GetHitsLeft();
            if (left > 0)
            {
                Message(player, "Hits", script.GetHitsLeft());
                return true;
            }

            foreach (var vehicle in AllVehicles)
            {
                if (vehicle.prefab == entity.PrefabName)
                {
                    entity.Kill();
                    GiveItem(player, vehicle);
                    return true;
                }
            }

            return null;
        }

        private object CheckPickupBalloon(BasePlayer player, HotAirBalloon balloon)
        {
            if (balloon == null)
                return null;

            if (!config.pickupAnyVehicles && balloon.skinID == 0)
                return null;

            if (!permission.UserHasPermission(player.UserIDString, permPickup))
                return null;

            if (config.pickupableBlacklist.Contains(balloon.ShortPrefabName))
            {
                Message(player, "Pickupable");
                return null;
            }

            if (config.pickupOwnVehicles && balloon.OwnerID != player.userID)
            {
                Message(player, "Pickup Ownership");
                return null;
            }

            if (config.requireBuildingPrivilege && !player.CanBuild())
            {
                Message(player, "Cupboard");
                return null;
            }

            var time = balloon.SecondsSinceAttacked;
            if (time < 30)
            {
                Message(player, "Recently Attacked", (30 - time).ToString("0.0"));
                return true;
            }

            var diff = (Mathf.Abs(balloon.MaxHealth() - balloon.Health()));
            if (config.needRepair && diff > 5f)
            {
                Message(player, "Durability");
                return null;
            }

            if (config.checkStorages)
            {
                var containers = balloon.GetComponentsInChildren<StorageContainer>();
                if (containers.Any(x => x.inventory.itemList.Count > 0))
                {
                    Message(player, "Not Empty");
                    return true;
                }

                var fs = balloon.fuelSystem;
                if (fs != null && !fs.fuelStorageInstance.Get(true).IsLocked() && fs.HasFuel())
                {
                    Message(player, "Fuel");
                    return true;
                }
            }

            var script = balloon.GetOrAddComponent<PickupScript>();
            script.AddHit();
            var left = script.GetHitsLeft();
            if (left > 0)
            {
                Message(player, "Hits", script.GetHitsLeft());
                return true;
            }

            foreach (var vehicle in AllVehicles)
            {
                if (vehicle.prefab == balloon.PrefabName)
                {
                    balloon.Kill();
                    GiveItem(player, vehicle);
                    return true;
                }
            }

            return null;
        }

        private void GiveItem(BasePlayer player, ulong skin)
        {
            var vehicle = AllVehicles.FirstOrDefault(x => x.skinId == skin);
            if (vehicle == null)
                return;

            GiveItem(player, vehicle);
        }

        private void GiveItem(BasePlayer player, VehicleEntry vehicle)
        {
            var name = string.IsNullOrEmpty(vehicle.displayName) ? "Portable Vehicle" : vehicle.displayName;
            var shortname = "box.wooden.large";

            if (vehicle.bigModel)
            {
                shortname = config.bigModelEntityShortName;
            }
            else
            {
                shortname = vehicle.isWaterVehicle ? config.waterEntityShortName : config.groundEntityShortName;
            }

            Item item = ItemManager.CreateByName(shortname, 1, vehicle.skinId);
            if (item != null)
            {
                item.name = name;
                player.GiveItem(item);
                Message(player, "Received", name);
            }
            else
            {
                SendMessage(player, "<size=12><color=#ff6666>Portable Vehicles: Item not created. Wrong entity name in the configuration!</color></size>");
            }
        }

        private ulong GetSkin(string name)
        {
            switch (name.ToLower())
            {
                case "rhib":
                case "militaryboat":
                case "military":
                    return 2783365542;

                case "boat":
                case "rowboat":
                case "motorboat":
                    return 2783365250;

                case "copter":
                case "mini":
                case "minicopter":
                    return 2906148311;

                case "balloon":
                case "hotairballoon":
                    return 2783364912;

                case "ch":
                case "ch47":
                case "chinook":
                    return 2783365479;

                case "horse":
                case "unicorn":
                case "testridablehorse":
                    return 2783365408;

                case "scrap":
                case "scrapheli":
                case "scraphelicopter":
                case "helicopter":
                    return 2783365006;

                case "car":
                case "car1":
                case "sedan":
                    return 2783365060;

                case "car2":
                    return 2783364084;

                case "car3":
                    return 2783364660;

                case "car4":
                    return 2783364761;

                case "submarinesolo":
                    return 2783365665;

                case "submarineduo":
                    return 2783365593;

                case "snowmobile":
                case "snow":
                    return 2783366199;

                case "tomahasnowmobile":
                case "tsnowmobile":
                case "tsnow":
                    return 3000416835;

                case "tugboat":
                    return 3000418301;

                case "attackheli":
                case "attackhelicopter":
                case "attackcopter":
                    return 3284204081;

                case "motorbike":
                    return 3284204457;

                case "motorbikesidecar":
                case "motorsidecar":
                case "sidecar":
                    return 3284204759;

                case "bicycle":
                case "pedalbike":
                case "bike":
                    return 3284205070;

                case "trike":
                case "pedaltrike":
                    return 3284205351;

                case "catapult":
                    return 3446373078;

                case "siegetower":
                case "tower":
                    return 3446373165;

                case "batteringram":
                case "ram":
                    return 3446372968;

                case "ballista":
                    return 3446372639;

                default:
                    return 0;
            }
        }

        private void AddFuel(BasePlayer player, BaseVehicle baseVehicle)
        {
            if (!permission.UserHasPermission(player.UserIDString, permFuel) || config.vehicleFuel <= 0)
                return;

            NextTick(() =>
            {
                EntityFuelSystem entityFuelSystem = baseVehicle?.GetFuelSystem() as EntityFuelSystem;
                if (entityFuelSystem != null)
                {
                    entityFuelSystem.AddFuel(config.vehicleFuel);
                }
            });
        }

        private void AddFuelBalloon(BasePlayer player, HotAirBalloon hotAirBalloon)
        {
            if (!permission.UserHasPermission(player.UserIDString, permFuel) || config.vehicleFuel <= 0)
                return;

            NextTick(() =>
            {
                var fs = hotAirBalloon.fuelSystem;
                if (fs != null)
                {
                    fs.AddFuel(config.vehicleFuel);
                }
            });
        }

        #endregion

        #region [Chat Commands]

        private void cmdPortableVehicles(BasePlayer player, string command, string[] args)
        {
            var value = args.Length > 0 ? args[0] : null;
            var value2 = args.Length > 1 ? args[1] : null;
            if (value == null)
            {
                Message(player, "Invalid Syntax");
                return;
            }

            if (value2 == null)
            {
                if (!permission.UserHasPermission(player.UserIDString, permUse))
                {
                    Message(player, "Permission");
                    return;
                }

                var skin = GetSkin(value);
                if (skin == 0)
                {
                    Message(player, "Invalid Vehicle");
                    return;
                }

                GiveItem(player, skin);
            }
            else
            {
                if (!IsAdmin(player))
                {
                    Message(player, "Permission");
                    return;
                }

                var target = FindPlayer(player, value);
                if (target == null)
                    return;

                var skin = GetSkin(value2);
                if (skin == 0)
                {
                    Message(player, "Invalid Vehicle");
                    return;
                }

                GiveItem(target, skin);
            }
        }

        #endregion

        #region [Console Commands]

        [ConsoleCommand("portablevehicles.give")]
        private void cmdGiveConsole(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null && !IsAdmin(player))
            {
                Message(player, "Permission");
                return;
            }

            var args = arg.Args;
            if (args == null || args.Length < 2)
            {
                Message(arg, "Usage");
                return;
            }

            var target = FindPlayer(arg, args[0]);
            if (target == null)
                return;

            var skin = GetSkin(args[1]);
            if (skin == 0)
            {
                Message(arg, "Invalid Vehicle");
                return;
            }

            GiveItem(target, skin);
        }

        #endregion

        #region [Classes]

        private class Configuration
        {
            [JsonProperty(PropertyName = "Chat Icon")]
            public ulong chatIcon;

            [JsonProperty("Hits count to pickup vehicle")]
            public int hitsToPickup;

            [JsonProperty("Pickup any vehicles")]
            public bool pickupAnyVehicles;

            [JsonProperty("Pickup only your own vehicles")]
            public bool pickupOwnVehicles;

            [JsonProperty("Pickup require building priviledge")]
            public bool requireBuildingPrivilege;

            [JsonProperty("Automatically mount players")]
            public bool autoMount;

            [JsonProperty("Check storages before pickup")]
            public bool checkStorages;

            [JsonProperty("Need repair vehicles before pickup")]
            public bool needRepair;

            [JsonProperty(PropertyName = "Auto amount fuel to vehicle")]
            public int vehicleFuel;

            [JsonProperty(PropertyName = "Item shortname for water entity")]
            public string waterEntityShortName;

            [JsonProperty(PropertyName = "Item shortname for big models")]
            public string bigModelEntityShortName;

            [JsonProperty(PropertyName = "Item shortname for ground entity")]
            public string groundEntityShortName;

            [JsonProperty(PropertyName = "Blacklist pickupable vehicles shortname")]
            public List<string> pickupableBlacklist = new List<string>();

            public VersionNumber version;
        }

        private class VehicleEntry
        {
            public ulong skinId;
            public string displayName;
            public string prefab;
            public bool bigModel;
            public bool isWaterVehicle;
        }

        private VehicleEntry[] AllVehicles = new[]
        {
            new VehicleEntry
            {
                skinId = 2783365542,
                displayName = "Rhib",
                prefab = "assets/content/vehicles/boats/rhib/rhib.prefab",
                isWaterVehicle = true
            },
            new VehicleEntry
            {
                skinId = 2783365250,
                displayName = "Boat",
                prefab = "assets/content/vehicles/boats/rowboat/rowboat.prefab",
                isWaterVehicle = true
            },
            new VehicleEntry
            {
                skinId = 2906148311,
                displayName = "MiniCopter",
                prefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab"
            },
            new VehicleEntry
            {
                skinId = 2783365060,
                displayName = "Sedan",
                prefab = "assets/content/vehicles/sedan_a/sedantest.entity.prefab"
            },
            new VehicleEntry
            {
                skinId = 2783365479,
                displayName = "Chinook",
                prefab = "assets/prefabs/npc/ch47/ch47.entity.prefab",
                bigModel = true,
            },
            new VehicleEntry
            {
                skinId = 2783364912,
                displayName = "Hot Air Balloon",
                prefab = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab",
                bigModel = true,
            },
            new VehicleEntry
            {
                skinId = 2783365408,
                displayName = "Horse",
                prefab = "assets/content/vehicles/horse/ridablehorse2.prefab"
            },
            new VehicleEntry
            {
                skinId = 2783365006,
                displayName = "Scrap Transport Helicopter",
                prefab = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
                bigModel = true,
            },
            new VehicleEntry
            {
                skinId = 2783364084,
                displayName = "2 Module Car",
                prefab = "assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab"
            },
            new VehicleEntry
            {
                skinId = 2783364660,
                displayName = "3 Module Car",
                prefab = "assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab"
            },
            new VehicleEntry
            {
                skinId = 2783364761,
                displayName = "4 Module Car",
                prefab = "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab"
            },
            new VehicleEntry
            {
                skinId = 2783365665,
                displayName = "Submarine Solo",
                prefab = "assets/content/vehicles/submarine/submarinesolo.entity.prefab",
                isWaterVehicle = true
            },
            new VehicleEntry
            {
                skinId = 2783365593,
                displayName = "Submarine Duo",
                prefab = "assets/content/vehicles/submarine/submarineduo.entity.prefab",
                isWaterVehicle = true
            },
            new VehicleEntry
            {
                skinId = 2783366199,
                displayName = "Snowmobile",
                prefab = "assets/content/vehicles/snowmobiles/snowmobile.prefab"
            },
            new VehicleEntry
            {
                skinId = 3000416835,
                displayName = "Tomaha Snowmobile",
                prefab = "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab"
            },
            new VehicleEntry
            {
                skinId = 3000418301,
                displayName = "Tugboat",
                prefab = "assets/content/vehicles/boats/tugboat/tugboat.prefab",
                isWaterVehicle = true
            },
            new VehicleEntry
            {
                skinId = 3284204081,
                displayName = "Attack Helicopter",
                prefab = "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab"
            },
            new VehicleEntry
            {
                skinId = 3284204457,
                displayName = "Motorbike",
                prefab = "assets/content/vehicles/bikes/motorbike.prefab"
            },
            new VehicleEntry
            {
                skinId = 3284204759,
                displayName = "Motorbike With Sidecar",
                prefab = "assets/content/vehicles/bikes/motorbike_sidecar.prefab"
            },
            new VehicleEntry
            {
                skinId = 3284205070,
                displayName = "Bicycle",
                prefab = "assets/content/vehicles/bikes/pedalbike.prefab"
            },
            new VehicleEntry
            {
                skinId = 3284205351,
                displayName = "Trike",
                prefab = "assets/content/vehicles/bikes/pedaltrike.prefab"
            },
            new VehicleEntry
            {
                skinId = 3446373078,
                displayName = "Catapult",
                prefab = "assets/content/vehicles/siegeweapons/catapult/catapult.entity.prefab"
            },
            new VehicleEntry
            {
                skinId = 3446373165,
                displayName = "Siege Tower",
                prefab = "assets/content/vehicles/siegeweapons/siegetower/siegetower.entity.prefab"
            },
            new VehicleEntry
            {
                skinId = 3446372639,
                displayName = "Ballista",
                prefab = "assets/content/vehicles/siegeweapons/ballista/ballista.entity.prefab"
            },
            new VehicleEntry
            {
                skinId = 3446372968,
                displayName = "Battering Ram",
                prefab = "assets/content/vehicles/siegeweapons/batteringram/batteringram.entity.prefab",
                bigModel = true
            }
        };

        private class PickupScript : MonoBehaviour
        {
            private BaseEntity entity;
            private int hits;

            private void Awake()
            {
                entity = GetComponent<BaseEntity>();
            }

            public void AddHit()
            {
                hits++;
                CancelInvoke(nameof(ResetHits));
                Invoke(nameof(ResetHits), 60);
            }

            private void ResetHits()
            {
                hits = 0;
            }

            public int GetHitsLeft()
            {
                return config.hitsToPickup - hits;
            }
        }

        #endregion

        #region [Config]

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                chatIcon = 0,
                hitsToPickup = 5,
                pickupAnyVehicles = true,
                pickupOwnVehicles = true,
                requireBuildingPrivilege = true,
                autoMount = false,
                checkStorages = true,
                needRepair = false,
                vehicleFuel = 0,
                waterEntityShortName = "kayak",
                groundEntityShortName = "box.wooden.large",
                bigModelEntityShortName = "furnace.large",
                pickupableBlacklist = new List<string>() { },
                version = Version
            };
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();
            Puts("Generating new configuration file........");
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();

                if (config == null)
                    LoadDefaultConfig();

                if (config.version < Version)
                    UpdateConfig();
            }
            catch
            {
                PrintError("######### Configuration file is not valid! #########");
                return;
            }

            SaveConfig();
        }

        private void UpdateConfig()
        {
            Puts("Updating configuration values.....");

            if (config.version < new VersionNumber(1, 1, 8))
            {
                config = GetDefaultConfig();
                Config.WriteObject(config);
            };

            config.version = Version;
            Puts("Configuration updated");
        }

        #endregion

        #region [Localization]

        private string GetLang(string key, string playerID, params object[] args) => string.Format(lang.GetMessage(key, this, playerID), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Usage", "Usage: portablevehicles.give 'steamID/player name' 'vehicle name'\n"},
                {"Invalid Syntax", "Invalid Syntax!\n/pv 'vehicle name'\n/pv 'steamID/player name' 'vehicle name'"},
                {"Permission", "You don't have permission to use that!"},
                {"Received", "You received '{0}'!"},
                {"No Player", "There are no players with that Name or steamID!"},
                {"Multiple Players", "There are many players with that Name:\n{0}"},
                {"Pickup Ownership", "Only owner can pickup vehicles!"},
                {"Fuel", "You need to remove fuel from vehicle first!"},
                {"Recently Attacked", "Vehicle was recently attacked! {0}s left"},
                {"Durability", "You need to repair vehicles fully!"},
                {"Not Empty", "Vehicle is not empty! Check fuel or storages!"},
                {"Hits", "You need to do more {0} hits!"},
                {"Cupboard", "You need to have building privilege to do that!"},
                {"BigModel", "That vehicle have big model and can kill you. Run away! It will be spawned in 3 seconds"},
                {"Pickupable", "This vehicle cannot be picked up"},
                {"Invalid Vehicle", "Vehicle name is invalid!"},
            }, this);
        }

        #endregion

        #region [Helpers]

        private bool IsAdmin(BasePlayer player)
        {
            if (player.IsAdmin || permission.UserHasPermission(player.UserIDString, permAdmin))
                return true;

            return false;
        }

        private BasePlayer FindPlayer(ConsoleSystem.Arg arg, string nameOrID)
        {
            var targets = BasePlayer.activePlayerList.Where(x => x.UserIDString == nameOrID || x.displayName.ToLower().Contains(nameOrID.ToLower())).ToList();
            if (targets.Count == 0)
            {
                Message(arg, "No Player");
                return null;
            }

            if (targets.Count > 1)
            {
                Message(arg, "Multiple Players");
                return null;
            }

            return targets[0];
        }

        private BasePlayer FindPlayer(BasePlayer player, string nameOrID)
        {
            var targets = BasePlayer.activePlayerList.Where(x => x.UserIDString == nameOrID || x.displayName.ToLower().Contains(nameOrID.ToLower())).ToList();
            if (targets.Count == 0)
            {
                Message(player, "No Player");
                return null;
            }

            if (targets.Count > 1)
            {
                Message(player, "Multiple Players");
                return null;
            }

            return targets[0];
        }

        private void Message(ConsoleSystem.Arg arg, string messageKey, params object[] args)
        {
            var message = GetLang(messageKey, null, args);
            var player = arg.Player();
            if (player != null)
            {
                SendMessage(player, message);
            }
            else
            {
                SendReply(arg, message);
            }
        }

        private void Message(BasePlayer player, string messageKey, params object[] args)
        {
            if (player == null)
                return;

            var message = GetLang(messageKey, player.UserIDString, args);
            SendMessage(player, message);
        }

        private void SendMessage(BasePlayer player, string msg) => Player.Message(player, msg, config.chatIcon);

        #endregion
    }
}