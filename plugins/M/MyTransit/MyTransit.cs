using Facepunch;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("My Transit", "Lincoln & Unboxing", "1.5.4")]
    [Description("Spawns work carts and makes them go fast")]
    class MyTransit : RustPlugin
    {
        #region Varariables

        private readonly Dictionary<ulong, string> _playerTransit = new Dictionary<ulong, string>();
        //private readonly Hash<string, float> _cooldowns = new Hash<string, float>();

        private const string PermUse = "mytransit.use";
        private const string PermAdmin = "mytransit.admin";
        private const string WorkcartEntity = "assets/content/vehicles/trains/workcart/workcart.entity.prefab";
        private const string TrainEntity = "assets/content/vehicles/trains/workcart/workcart_aboveground.entity.prefab";
        private const string TrainEntity2 = "assets/content/vehicles/trains/workcart/workcart_aboveground2.entity.prefab";
        private const string TrainEntity3 = "assets/content/vehicles/trains/locomotive/locomotive.entity.prefab";
        private const string TrainEntity4 = "assets/content/vehicles/trains/caboose/traincaboose.entity.prefab";
        private const int ItemFuelID = -946369541;

        private List<string> allTrains = new List<string>()
        {
            "workcart.entity",
            "workcart_aboveground.entity",
            "workcart_aboveground2.entity",
            "locomotive.entity",
            "traincaboose.entity"
        };


        //private float _cooldownTime = 5f;
        private Timer _speedTimer;

        #endregion

        #region Configuration

        private PluginConfig _config;

        private class PluginConfig
        {
            public int forwardSpeed;
            public int reverseSpeed;
            public bool useFuel;
        }

        private void Init()
        {
            _config = Config.ReadObject<PluginConfig>();

            permission.RegisterPermission(PermUse, this);
            permission.RegisterPermission(PermAdmin, this);
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                forwardSpeed = 200,
                reverseSpeed = -200,
                useFuel = true
            };
        }

        #endregion

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            foreach (var entity in BaseNetworkable.serverEntities.OfType<TrainCar>().ToArray())
            {
                if (entity.OwnerID == 0uL) continue;

                BasePlayer player = BasePlayer.FindByID(entity.OwnerID);
                if (player != null && allTrains.Contains(entity.ShortPrefabName))
                {
                    _playerTransit.Add(player.userID, player.displayName);
                }
            }
        }
        private void OnEntityKill(BaseEntity entity)
        {
            if (!allTrains.Contains(entity.ShortPrefabName)) return;

            BasePlayer player = BasePlayer.FindByID(entity.OwnerID);
            if (player == null) return;

            _playerTransit.Remove(entity.OwnerID);
        }

        private void Unload()
        {
            TrainCar.TRAINCAR_MAX_SPEED = 25f;
        }

        #endregion

        #region Custom Functions

        private void SpawnTransit(BasePlayer player, string command, string[] args)
        {
            TrainCar transit;

            Puts(args[0]);

            if (args[0] == "cart")
            {
                transit = GameManager.server.CreateEntity(WorkcartEntity, player.transform.position) as TrainCar;
            }
            else if (args[0] == "train2")
            {
                transit = GameManager.server.CreateEntity(TrainEntity2, player.transform.position) as TrainCar;
            }
            else if (args[0] == "loco")
            {
                transit = GameManager.server.CreateEntity(TrainEntity3, player.transform.position) as TrainCar;
            }
            else if (args[0] == "caboose")
            {
                transit = GameManager.server.CreateEntity(TrainEntity4, player.transform.position) as TrainCar;
            }
            else
            {
                transit = GameManager.server.CreateEntity(TrainEntity, player.transform.position) as TrainCar;
            }

            if (transit == null) return;

            transit.OwnerID = player.userID;
            _playerTransit.Add(player.userID, player.displayName);
            foreach (KeyValuePair<ulong, string> kvp in _playerTransit) Puts("Key: {0}, Value: {1}", kvp.Key, kvp.Value);
            SendReply(player, GetLang("Spawned", player.UserIDString));

            transit.Spawn();

            //

            if (!_config.useFuel)
            {
                transit.GetFuelSystem().AddFuel(1);
                Puts("Trying to add fuel");
                TrainEngine trainEngine = transit.GetComponent<TrainEngine>();
                //access EntityFuelSystem of transit and get the fuel container and lock it
                StorageContainer transitInventory = (transit.GetFuelSystem() as EntityFuelSystem)?.GetFuelContainer() as StorageContainer;

                if (trainEngine != null)
                {
                    trainEngine.maxFuelPerSec = 0f;
                    trainEngine.idleFuelPerSec = 0f;
                    transitInventory.SetFlag(BaseEntity.Flags.Locked, true);


                    Puts("Set maxFuelPerSec and idleFuelPerSec to 0");
                }
                else
                {
                    Puts("Failed to get TrainEngine component");
                }
            }


            if (player.transform.position == transit.transform.position)
            {
                //transform player position to behind the train
                player.transform.position = transit.transform.position + new Vector3(-5, 0, 0);

            }
            transit.SendNetworkUpdateImmediate();
        }

        private bool PositionIsInWater(Vector3 position)
        {
            List<Collider> colliders = Pool.GetList<Collider>();
            Vis.Colliders(position, 0.5f, colliders);
            bool flag = colliders.Any(x => x.gameObject?.layer == (int)Rust.Layer.Water);
            Pool.FreeList(ref colliders);
            return flag;
        }

        private void RecallTransit(BasePlayer player, BaseEntity entity)
        {
            entity.transform.position = player.transform.position;
            entity.SendNetworkUpdateImmediate();
            entity.UpdateNetworkGroup();
            SendReply(player, GetLang("TransitRecall", player.UserIDString));
        }

        private void ChangeSpeed(BasePlayer player, int currentSpeed)
        {
            TrainCar transit = GetActiveTransit(player);
            _speedTimer?.Destroy();
            _speedTimer = timer.Every(1f, () =>
            {
                if (transit == null) _speedTimer.Destroy();
                //if currentSpeed is less than 0, it is a reverse speed
                if (currentSpeed < 0)
                {
                    transit.completeTrain.trackSpeed = 0;
                    transit.completeTrain.trackSpeed = currentSpeed - 1;
                    //set the trainengine maxfuelpersec to 0
                    TrainCar.TRAINCAR_MAX_SPEED = -(transit.completeTrain.trackSpeed - 1);

                }
                else
                {
                    transit.completeTrain.trackSpeed = 0;
                    transit.completeTrain.trackSpeed = currentSpeed + 1;
                    TrainCar.TRAINCAR_MAX_SPEED = transit.completeTrain.trackSpeed + 1;
                }

            });
        }

        private TrainCar GetActiveTransit(BasePlayer player)
        {
            // Get all transits on the map and check ownership
            foreach (TrainCar train in BaseNetworkable.serverEntities.OfType<TrainCar>().ToArray())
            {
                if (train.OwnerID == player.userID)
                {
                    return train;
                }
            }

            return null;
        }

        public bool TryFindTrackNearby(Vector3 pos, float maxDist, out TrainTrackSpline splineResult, out float distResult)
        {
            splineResult = null;
            distResult = 0f;
            List<Collider> list = Pool.GetList<Collider>();
            GamePhysics.OverlapSphere(pos, maxDist, list, 65536);

            if (list.Count > 0)
            {
                List<TrainTrackSpline> trainTrackSplines = Pool.GetList<TrainTrackSpline>();
                float single;

                foreach (Collider collider in list)
                {
                    collider.GetComponentsInParent(false, trainTrackSplines);
                    if (trainTrackSplines.Count <= 0)
                    {
                        continue;
                    }

                    foreach (TrainTrackSpline trainTrackSpline in trainTrackSplines)
                    {
                        float distance = trainTrackSpline.GetDistance(pos, 1f, out single);
                        if (!(distance >= single))
                        {
                            single = distance;
                            splineResult = trainTrackSpline;
                        }
                    }
                }
                if (splineResult != null)
                {
                    distResult = splineResult.GetDistance(pos, 0.25f, out single);
                }
                Pool.FreeList(ref trainTrackSplines);
            }
            Pool.FreeList(ref list);
            return splineResult != null;
        }

        private bool ValidationChecks(BasePlayer player)
        {
            Vector3 pos = player.transform.position;
            const float maxDist = 2f;
            TrainTrackSpline splineResult;
            float distResult;

            if (player.IsDead()) return true;

            if (!permission.UserHasPermission(player.UserIDString, PermUse))
            {
                SendReply(player, GetLang("NoPerm", player.UserIDString));
                return true;
            }

            if (player.IsBuildingBlocked())
            {
                SendReply(player, GetLang("BuildingBlocked", player.UserIDString));
                return true;
            }

            if (player.isMounted)
            {
                SendReply(player, GetLang("Mounted", player.UserIDString));
                return true;
            }

            if (player.IsFlying || !player.IsOnGround())
            {
                SendReply(player, GetLang("Flying", player.UserIDString));
                return true;
            }

            if (player.IsWounded())
            {
                SendReply(player, GetLang("Wounded", player.UserIDString));
                return true;
            }

            if (!TryFindTrackNearby(pos, maxDist, out splineResult, out distResult))
            {
                SendReply(player, GetLang("NotNearTrack", player.UserIDString));
                return true;
            }

            return false;
        }

        #endregion

        #region Commands

        //chat command mtlist
        [ChatCommand("mtlist")]
        private void ListCommand(BasePlayer player, string command, string[] args)
        {
            foreach (KeyValuePair<ulong, string> kvp in _playerTransit) Puts("Key: {0}, Value: {1}", kvp.Key, kvp.Value);
        }

        [ChatCommand("mtcheck")]
        private void CheckCommand(BasePlayer player, string command, string[] args)
        {
            TrainCar transit = GetActiveTransit(player);

            if (transit == null)
            {
                SendReply(player, GetLang("TransitNotOut", player.UserIDString));
                return;
            }

            if (transit.OwnerID == player.userID)
            {
                int speed = (int)transit.completeTrain.trackSpeed;
                SendReply(player, GetLang("TransitSpeed", player.UserIDString), speed);
            }
        }

        [ChatCommand("mtspeed")]
        private void SpeedCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermAdmin))
            {
                SendReply(player, GetLang("NoPerm", player.UserIDString));
                return;
            }

            if (args.IsNullOrEmpty() || args[0].Length == 0)
            {
                SendReply(player, GetLang("NoArgs", player.UserIDString));
                return;
            }

            int currentSpeed;
            int.TryParse(args[0], out currentSpeed);

            if (currentSpeed > _config.forwardSpeed || currentSpeed < _config.reverseSpeed || currentSpeed == 0)
            {
                SendReply(player, GetLang("OutOfBounds", player.UserIDString), _config.reverseSpeed, _config.forwardSpeed);
                return;
            }

            if (!_playerTransit.ContainsKey(player.userID))
            {
                SendReply(player, GetLang("TransitNotOut", player.UserIDString));
                return;
            }

            TrainCar transit = GetActiveTransit(player);
            if (transit.GetDriver() == null)
            {
                SendReply(player, GetLang("NotMounted", player.UserIDString));
                return;
            }

            ChangeSpeed(player, currentSpeed);
            SendReply(player, GetLang("TransitSpeedEnabled", player.UserIDString), currentSpeed);
        }

        [ChatCommand("mtspeed.off")]
        private void OffCommand(BasePlayer player, string command, string[] args)
        {
            if (_speedTimer == null) return;

            _speedTimer.Destroy();
            SendReply(player, GetLang("TransitSpeedDisabled", player.UserIDString));
        }

        [ChatCommand("mthelp")]
        private void HelpCommand(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, PermAdmin))
            {
                SendReply(player, GetLang("HelpAdmin", player.UserIDString));
                return;
            }

            SendReply(player, GetLang("Help", player.UserIDString));
        }

        [ChatCommand("mtspawn")]
        private void SpawnCommand(BasePlayer player, string command, string[] args)
        {
            if (args.IsNullOrEmpty() || args[0].Length == 0)
            {
                SendReply(player, GetLang("NoArgsTransit", player.UserIDString));
                return;
            }
            string type = args[0].ToLower();
            if (type != "cart" && type != "train" && type != "train2" && type != "loco" && type != "caboose")
            {
                SendReply(player, GetLang("NoArgsTransit", player.UserIDString));
                return;
            }

            if (_playerTransit.ContainsKey(player.userID))
            {
                SendReply(player, GetLang("TransitExists", player.UserIDString));
                return;
            }

            if (!ValidationChecks(player))
            {
                SpawnTransit(player, command, args);
            }
        }

        [ChatCommand("mtkill")]
        private void KillCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "MyTransit.use"))
            {
                SendReply(player, GetLang("NoPerm", player.UserIDString));
                return;
            }

            if (!_playerTransit.ContainsKey(player.userID))
            {
                SendReply(player, GetLang("TransitNotOut", player.UserIDString));
                return;
            }

            TrainCar transit = GetActiveTransit(player);
            DestroyTransit(player, transit);
            SendReply(player, GetLang("TransitKilled", player.UserIDString));
            //remove player from list
            _playerTransit.Remove(player.userID);
        }

        private void OnEntityTakeDamage(TrainCar trainCar, HitInfo info)
        {
            if (trainCar.OwnerID != 0uL)
            {
                info.damageTypes.ScaleAll(0f);
            }
        }

        private void DestroyTransit(BasePlayer player, TrainCar transit)
        {
            if (player == null || transit == null || transit.OwnerID != player.userID) return;

            _playerTransit.Remove(player.userID);
            transit.Kill();
        }

        #endregion

        #region Localization

        private string GetLang(string langKey, string playerId = null) => lang.GetMessage(langKey, this, playerId);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Spawned"] = "<size=18><color=#ffc34d>MyTransit</color></size>\nYou have spawned your <color=#9999ff>transit</color>.",
                ["BuildingBlocked"] = "<color=#ffc34d>MyTransit</color>: Can't spawn/recall a transit while building blocked.",
                ["Mounted"] = "<color=#ffc34d>MyTransit</color>: Can't spawn/recall a transit while mounted.",
                ["Help"] = "<size=18><color=#ffc34d>MyTransit</color></size>\n<color=#9999ff>/mtspawn</color> to spawn your transit.\n<color=#9999ff>/mtkill</color> to kill your transit.\n<color=#9999ff>/mthelp</color> to open this help menu.",
                ["HelpAdmin"] = "<size=18><color=#ffc34d>MyTransit</color></size>\n<color=#9999ff>/mtspawn</color> to spawn your transit.\n<color=#9999ff>/mtkill</color> to kill your transit.\n<color=#9999ff>/mthelp</color> to open this help menu.\n\n<size=13><color=#ff6666>Admin Commands</color></size>\n<color=#9999ff>/mtspeed</color> to set the speed of your transit.\n</size><color=#9999ff>/mtcheck</color> to check the speed of your transit.",
                ["Flying"] = "<color=#ffc34d>MyTransit</color>: Can't spawn while jumping, flying, or falling",
                ["NoArgs"] = "<color=#ffc34d>MyTransit</color>: Please specify a speed.",
                ["NoArgsTransit"] = "<color=#ffc34d>MyTransit</color>: You must specify a type of transit!\n<color=#ffc34d>/mtspawn cart</color> workcart (without couplings)\n<color=#ffc34d>/mtspawn train</color> above ground train (with couplings)\n<color=#ffc34d>/mtspawn train2</color> above ground covered train (with couplings)\n<color=#ffc34d>/mtspawn loco</color> to spawn a locomotive.\n<color=#ffc34d>/mtspawn caboose</color> to spawn a caboose.",
                ["OutOfBounds"] = "<color=#ffc34d>MyTransit</color>: Speed out of bounds. Please specify a speed between {0} and {1}",
                ["NotMounted"] = "<color=#ffc34d>MyTransit</color>: You need to be seated in the train.",
                ["NotNearTrack"] = "<color=#ffc34d>MyTransit</color>: Please stand next to a track to spawn.",
                ["Cooldown"] = "<color=#ffc34d>MyTransit</color>: You are on a 5 second cooldown.",
                ["TransitKilled"] = "<color=#ffc34d>MyTransit</color>: Your transit has been killed.",
                ["TransitSpeed"] = "<color=#ffc34d>MyTransit</color>: The transit speed is <color=#b0fa66>{0}</color>.",
                ["TransitSpeedDisabled"] = "<color=#ffc34d>MyTransit</color>: Custom speed <color=#ff6666>disabled</color>.",
                ["TransitSpeedEnabled"] = "<color=#ffc34d>MyTransit</color>: Custom speed set to <color=#b0fa66>{0}</color>.",
                ["TransitNotOut"] = "<color=#ffc34d>MyTransit</color>: You do not have a transit out.",
                ["TransitExists"] = "<color=#ffc34d>MyTransit</color>: You already have a transit out. Type <color=#9999ff>/mtkill</color> to kill it. ",
                ["Wounded"] = "<color=#ffc34d>MyTransit</color>: You cannot spawn a transit while wounded.",
                ["NoPerm"] = "<color=#ffc34d>MyTransit</color>: You don't have permission to use that.",
                ["Killed"] = "<color=#ffc34d>MyTransit</color>: You just removed your transit."
            }, this);
        }

        #endregion
    }
}
