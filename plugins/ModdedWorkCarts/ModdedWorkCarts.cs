using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Modded Work Carts", "Kopter", "1.0.2")]
    [Description("Allows making modifications to Work Carts, such as adding Auto Turret, Storage")]

    public class ModdedWorkCarts : RustPlugin
    {
        #region Variables

        int AutoTurretID;
        List<ulong> PlayersOnCoolDown = new List<ulong>();

        private const string StoragePrefab = "assets/content/vehicles/boats/rhib/subents/rhib_storage.prefab";
        private const string ChairPrefab = "assets/prefabs/vehicle/seats/passengerchair.prefab";
        private const string InvisibleChairPrefab = "assets/bundled/prefabs/static/chair.invisible.static.prefab";
        private const string AutoTurretPrefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
        private const string ElectricSwitchPrefab = "assets/prefabs/deployable/playerioents/simpleswitch/switch.prefab";

        private const string AutoTurretSound = "assets/prefabs/npc/autoturret/effects/autoturret-deploy.prefab";

        private const string TurretPermission = "moddedworkcarts.turret";

        public List<Vector3> ChairPositions = new List<Vector3>()
        {
            new Vector3(-1.1f, 1.40f, -4.15f),
            new Vector3(-0.36f, 1.40f, -4.15f),
            new Vector3(0.36f, 1.40f, -4.15f),
            new Vector3(1.1f, 1.40f, -4.15f),
        };

        public List<Vector3> StoragePositions = new List<Vector3>()
        {
            new Vector3(0.85f, 2.63f, 0.3f),
            new Vector3(0.85f, 2.63f, 1.44f)
        };

        Vector3 AutoTurretPosition = new Vector3(0.7f, 3.8f, 3.7f);
        Vector3 ElectricSwitchPosition = new Vector3(-0.59f, -2, -1.9f);

        #endregion

        #region Oxide Hooks

        void Init()
        {
            permission.RegisterPermission(TurretPermission, this);

            AutoTurretID = ItemManager.FindItemDefinition("autoturret").itemid;

            PlayersOnCoolDown.Clear();
        }

        void OnEntitySpawned(TrainCar WorkCart)
        {
            if (Rust.Application.isLoadingSave) return;

            NextTick(() =>
            {
                if (WorkCart == null) return;

                if (config.Chairs) SpawnChairs(WorkCart);

                if (config.Storage) SpawnStorage(WorkCart);

                if (config.AutoTurret) SpawnAutoTurretAndSwitch(WorkCart);
            });
        }

        object OnSwitchToggled(ElectricSwitch ElectricSwitch)
        {
            var AutoTurret = ElectricSwitch.GetParentEntity() as AutoTurret;

            if (AutoTurret == null) return null;

            var WorkCart = AutoTurret.GetParentEntity() as TrainCar;

            if (WorkCart == null) return null;

            if (ElectricSwitch.IsOn()) AutoTurret.InitiateStartup();

            else AutoTurret.InitiateShutdown();

            return null;
        }

        object OnEntityTakeDamage(ElectricSwitch ElectricSwitch)
        {
            var AutoTurret = ElectricSwitch.GetParentEntity();

            if (AutoTurret == null) return null;

            var WorkCart = AutoTurret.GetParentEntity();

            if (WorkCart == null) return null;

            return true;
        }

        void OnEntityKill(StorageContainer Storage)
        {
            var WorkCart = Storage.GetParentEntity() as TrainCar;

            if (WorkCart == null) return;

            Storage.DropItems();
        }

        #endregion

        #region Commands

        [ChatCommand("workcartturret")]
        void WorkCartTurretCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, TurretPermission))
            {
                player.ChatMessage($"{Lang("NoPermission", player.UserIDString)}");
                return;
            }

            if (PlayersOnCoolDown.Contains(player.userID))
            {
                player.ChatMessage($"{Lang("OnCooldown", player.UserIDString, config.CommandCooldown)}");
                return;
            }

            TrainCar WorkCart;

            if (!VerifyWorkCartFound(player, out WorkCart))
            {
                player.ChatMessage($"{Lang("NoWorkCartFound", player.UserIDString)}");
                return;
            }

            if (GetWorkCartAutoTurret(WorkCart) != null)
            {
                player.ChatMessage($"{Lang("WorkCartHasTurret", player.UserIDString)}"); 
                return;
            }

            if (player.inventory.GetAmount(AutoTurretID) < 1)
            {
                player.ChatMessage($"{Lang("NoAutoTurret", player.UserIDString)}");
                return;
            }

            AutoTurret AutoTurret = (AutoTurret)SpawnAutoTurretAndSwitch(WorkCart);

            if (AutoTurret == null) return;

            Effect.server.Run(AutoTurretSound, AutoTurret.transform.position);

            player.inventory.Take(null, AutoTurretID, 1);

            AutoTurret.authorizedPlayers.Add(new ProtoBuf.PlayerNameID
            {
                userid = player.userID,
                username = player.displayName
            });

            AutoTurret.SendNetworkUpdate();

            player.ChatMessage($"{Lang("AutoTurretPlaced", player.UserIDString)}");

            if (config.CommandCooldown > 0)
            {
                PlayersOnCoolDown.Add(player.userID);

                timer.Once(config.CommandCooldown * 60, () =>
                {
                    PlayersOnCoolDown.Remove(player.userID);
                });
            }
        }

        #endregion

        #region Functions

        void SpawnChairs(TrainCar WorkCart)
        {
            foreach (Vector3 Position in ChairPositions)
            {
                BaseMountable Chair = GameManager.server.CreateEntity(ChairPrefab, WorkCart.transform.position) as BaseMountable;
                BaseMountable InvisibleChair = GameManager.server.CreateEntity(InvisibleChairPrefab, WorkCart.transform.position) as BaseMountable;

                if (Chair == null || InvisibleChair == null) return;

                Chair.Spawn();
                InvisibleChair.Spawn();

                Chair.SetParent(WorkCart);
                InvisibleChair.SetParent(WorkCart);

                Chair.transform.localPosition = Position;
                InvisibleChair.transform.localPosition = Position;

                RemoveColliderProtection(Chair);
                RemoveColliderProtection(InvisibleChair);

                Chair.SendNetworkUpdateImmediate();
                InvisibleChair.SendNetworkUpdateImmediate();
            }
        }

        void SpawnStorage(TrainCar WorkCart)
        {
            foreach (Vector3 Position in StoragePositions)
            {
                StorageContainer Storage = GameManager.server.CreateEntity(StoragePrefab, WorkCart.ServerPosition) as StorageContainer;

                if (Storage == null) return;

                Storage.Spawn();

                Storage.SetParent(WorkCart);

                Storage.transform.localPosition = Position;

                Storage.DropItems();

                RemoveColliderProtection(Storage);

                Storage.SendNetworkUpdateImmediate();
            }
        }

        object SpawnAutoTurretAndSwitch(TrainCar WorkCart)
        {
            Quaternion ElectricSwitchRotation = new Quaternion(0, WorkCart.transform.rotation.y, 0, WorkCart.transform.rotation.x);

            AutoTurret AutoTurret = GameManager.server.CreateEntity(AutoTurretPrefab, WorkCart.ServerPosition) as AutoTurret;
            ElectricSwitch ElectricSwitch = GameManager.server.CreateEntity(ElectricSwitchPrefab, WorkCart.ServerPosition, ElectricSwitchRotation) as ElectricSwitch;
            
            if (AutoTurret == null || ElectricSwitch == null) return null;

            AutoTurret.Spawn();
            ElectricSwitch.Spawn();

            AutoTurret.SetParent(WorkCart);
            ElectricSwitch.SetParent(AutoTurret);

            AutoTurret.transform.localPosition = AutoTurretPosition;
            ElectricSwitch.transform.localPosition = ElectricSwitchPosition;

            AutoTurret.pickup.enabled = false;
            ElectricSwitch.pickup.enabled = false;

            ElectricSwitch.SetFlag(IOEntity.Flag_HasPower, true);

            RemoveColliderProtection(AutoTurret);
            RemoveColliderProtection(ElectricSwitch);

            foreach (var Input in AutoTurret.inputs)
                Input.type = IOEntity.IOType.Generic;

            foreach (var Output in AutoTurret.outputs)
                Output.type = IOEntity.IOType.Generic;

            foreach (var Input in ElectricSwitch.inputs)
                Input.type = IOEntity.IOType.Generic;

            foreach (var Output in ElectricSwitch.outputs)
                Output.type = IOEntity.IOType.Generic;

            AutoTurret.SendNetworkUpdateImmediate();
            ElectricSwitch.SendNetworkUpdateImmediate();

            return AutoTurret;
        }

        void RemoveColliderProtection(BaseEntity Entity)
        {
            foreach (var MeshCollider in Entity.GetComponentsInChildren<MeshCollider>())
            {
                UnityEngine.Object.DestroyImmediate(MeshCollider);
            }

            UnityEngine.Object.DestroyImmediate(Entity.GetComponent<GroundWatch>());
            UnityEngine.Object.DestroyImmediate(Entity.GetComponent<DestroyOnGroundMissing>());
        }

        private bool VerifyWorkCartFound(BasePlayer player, out TrainCar WorkCart)
        {
            RaycastHit hit;

            var Entity = Physics.Raycast(player.eyes.HeadRay(), out hit, 3, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore) ? hit.GetEntity() : null;

            WorkCart = Entity as TrainCar;

            if (WorkCart != null) return true;

            return false;
        }

        private static AutoTurret GetWorkCartAutoTurret(TrainCar WorkCart)
        {
            foreach (var Child in WorkCart.children)
            {
                var AutoTurret = Child as AutoTurret;

                if (AutoTurret != null) return AutoTurret;
            }

            return null;
        }

        #endregion

        #region Config

        private ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Add a Auto Turret on top of the driver cabin")]
            public bool AutoTurret = false;

            [JsonProperty(PropertyName = "Add Storage Boxes on top of the fuel deposit")]
            public bool Storage = false;

            [JsonProperty(PropertyName = "Add chairs at the back of the Work Cart")]
            public bool Chairs = false;

            [JsonProperty(PropertyName = "Turret Command Cooldown in Minutes (If 0 there will be none)")]
            public float CommandCooldown = 0;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }

            catch
            {
                PrintError("Configuration file is corrupt, check your config file at https://jsonlint.com/!");
                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NoPermission", "You don't have permission to use this command!"},
                {"OnCooldown", "You are currently on a cooldown, you can use this command every {0} minutes!"},
                {"NoWorkCartFound", "A Work Cart was not found!"},
                {"WorkCartHasTurret", "This Work Cart already has a turret!"},
                {"NoAutoTurret", "You need a Auto Turret on your inventory to use this command!"},
                {"AutoTurretPlaced", "The Auto Turret was placed!"}
            }, this);
        }

        #endregion

        #region Helpers

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion

    }
}