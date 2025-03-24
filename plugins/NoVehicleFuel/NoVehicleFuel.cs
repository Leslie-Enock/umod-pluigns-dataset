using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("No Vehicle Fuel", "birthdates", "2.0.3")]
    [Description("No fuel needed for boats and rhibs.")]
    public class NoVehicleFuel : RustPlugin
    {
        #region Variables
        private const string permission_boat = "novehiclefuel.boat";
        private const string permission_copter = "novehiclefuel.copter";
        public static NoVehicleFuel Ins;

        private readonly List<FuelVehicle> cachedVehicles = new List<FuelVehicle>();

        private readonly Dictionary<string, string> PrefabToPermission = new Dictionary<string, string>
        {
            {"assets/content/vehicles/boats/rowboat/rowboat.prefab", permission_boat},
            {"assets/content/vehicles/minicopter/minicopter.entity.prefab", permission_copter}
        };

        private class FuelVehicle : MonoBehaviour
        {

            private BaseEntity Vehicle;
            private StorageContainer FuelTank;

            private void Awake()
            {
                Vehicle = GetComponent<BaseEntity>();
                if (Vehicle == null) End();
                var Boat = Vehicle as MotorRowboat;
                var Copter = Vehicle as MiniCopter;
                if (Boat)
                {
                    FuelTank = Boat.GetFuelSystem()?.fuelStorageInstance.Get(true).GetComponent<StorageContainer>();
                }
                else if (Copter)
                {
                    FuelTank = Copter.GetFuelSystem()?.fuelStorageInstance.Get(true).GetComponent<StorageContainer>();
                }
                else End();
                Ins.cachedVehicles.Add(this);
                if (Ins.cachedVehicles.Count == 1)
                {
                    Ins.Subscribe("OnItemRemovedFromContainer");
                    Ins.Subscribe("OnEntityDismounted");
                    Ins.Subscribe("CanLootEntity");
                }
                if (FuelTank.inventory.GetSlot(0) == null) AddFuel();
            }

            public void AddFuel(int amount = 5)
            {
                ItemManager.CreateByName("lowgradefuel", amount)?.MoveToContainer(FuelTank.inventory);
            }

            public void End(bool remove = false)
            {
                FuelTank?.inventory?.Clear();
                if (remove)
                {
                    Ins.cachedVehicles.Remove(this);
                    if (Ins.cachedVehicles.Count < 1)
                    {
                        Ins.Unsubscribe("OnItemRemovedFromContainer");
                        Ins.Unsubscribe("OnEntityDismounted");
                        Ins.Unsubscribe("CanLootEntity");
                    }

                }
                Destroy(this);
            }

        }
        #endregion

        #region Hooks
        private void Init()
        {
            LoadConfig();
            permission.RegisterPermission(permission_boat, this);
            permission.RegisterPermission(permission_copter, this);
            Ins = this;
        }

        private void OnServerInitialized() => SetupMounted();

        private void SetupMounted()
        {
            foreach (var Entity in BaseNetworkable.serverEntities)
            {
                var Vehicle = Entity as BaseVehicle;
                if (Vehicle == null) continue;
                if (!Vehicle.HasDriver()) continue;
                var Mount = Vehicle.mountPoints[0].mountable;
                var Driver = Mount.GetMounted();
                if (Driver == null) continue;
                OnEntityMounted(Mount, Driver);
            }
        }

        private void Unload()
        {
            foreach (var Vehicle in cachedVehicles)
            {
                Vehicle.End();
            }
        }

        private void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            var Entity = entity.GetParentEntity();
            if (Entity == null) return;
            var Prefab = Entity.PrefabName;
            if (!PrefabToPermission.ContainsKey(Prefab)) return;

            if (_config.Disabled.Contains(Prefab)) return;
            var Permission = PrefabToPermission[Prefab];
            if (!permission.UserHasPermission(player.UserIDString, Permission)) return;
            Entity.gameObject.AddComponent<FuelVehicle>();
        }

        private void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            var FuelVehicle = entity.GetComponent<FuelVehicle>();
            if (!FuelVehicle) return;
            FuelVehicle.End(true);
        }

        private object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            var Parent = container.GetParentEntity();
            if (Parent == null) return null;

            if (Parent.GetComponent<FuelVehicle>()) return false;
            return null;
        }

        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            var Parent = container.entityOwner?.GetParentEntity();

            if (Parent == null) return;
            var FuelVehicle = Parent.GetComponent<FuelVehicle>();
            if (!FuelVehicle) return;

            FuelVehicle.AddFuel(item.amount == 0 ? 5 : item.amount);

        }
        #endregion

        #region Configuration & Language
        public ConfigFile _config;

        public class ConfigFile

        {
            [JsonProperty("Disabled Vehicles (Prefab)")]
            public List<string> Disabled;
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    Disabled = new List<string>
                    {
                        "assets/content/vehicles/minicopter/minicopter.entity.prefab"
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null)
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
        #endregion
    }
}
//Generated with birthdates' Plugin Maker
