using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Underwater Vehicles", "WhiteThunder", "1.6.0")]
    [Description("Allows modular cars, snowmobiles, magnet cranes, and helicopters to be used underwater.")]
    internal class UnderwaterVehicles : CovalencePlugin
    {
        #region Fields

        private Configuration _config;
        private VehicleInfoManager _vehicleInfoManager;

        public UnderwaterVehicles()
        {
            _vehicleInfoManager = new VehicleInfoManager(this);
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            _vehicleInfoManager.OnServerInitialized();

            NextTick(() =>
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    var seat = player.GetMounted() as BaseVehicleSeat;
                    if (seat == null)
                        continue;

                    OnEntityMounted(seat);
                }
            });
        }

        private void Unload()
        {
            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                var vehicle = networkable as BaseVehicle;
                if ((object)vehicle == null)
                    continue;

                var vehicleInfo = _vehicleInfoManager.GetVehicleInfo(vehicle);
                if (vehicleInfo == null || !vehicleInfo.Config.Enabled)
                    continue;

                UnderwaterVehicleComponent.RemoveFromVehicle(vehicle);
            }
        }

        private void OnEntityMounted(BaseVehicleSeat seat)
        {
            HandleSeatMountedChanged(seat);
        }

        private void OnEntityDismounted(BaseVehicleSeat seat)
        {
            HandleSeatMountedChanged(seat);
        }

        #endregion

        #region Exposed Hooks

        private static class ExposedHooks
        {
            public static object OnVehicleUnderwaterEnable(BaseEntity vehicle)
            {
                return Interface.CallHook("OnVehicleUnderwaterEnable", vehicle);
            }
        }

        #endregion

        #region Helper Methods

        private static string[] FindPrefabsOfType<T>() where T : BaseEntity
        {
            var prefabList = new List<string>();

            foreach (var assetPath in GameManifest.Current.entities)
            {
                var entity = GameManager.server.FindPrefab(assetPath)?.GetComponent<T>();
                if (entity == null)
                    continue;

                prefabList.Add(entity.PrefabName);
            }

            return prefabList.ToArray();
        }

        private static BaseVehicle GetParentVehicle(BaseEntity entity)
        {
            var parent = entity.GetParentEntity();
            if (parent == null)
                return null;

            var vehicleModule = parent as BaseVehicleModule;
            if (vehicleModule != null)
                return vehicleModule.Vehicle;

            return parent as BaseVehicle;
        }

        private bool VehicleHasPermission(BaseVehicle vehicle, IVehicleInfo vehicleInfo)
        {
            if (!vehicleInfo.Config.RequireOccupantPermission)
                return true;

            if (!vehicle.AnyMounted())
                return false;

            foreach (var mountPointInfo in vehicle.allMountPoints)
            {
                var player = mountPointInfo.mountable?.GetMounted();
                if (player == null)
                    continue;

                if (permission.UserHasPermission(player.UserIDString, vehicleInfo.Permission))
                    return true;
            }

            return false;
        }

        private void RefreshUnderwaterCapability(BaseVehicle vehicle, IVehicleInfo vehicleInfo)
        {
            var vehicleHasPermission = VehicleHasPermission(vehicle, vehicleInfo);

            var component = UnderwaterVehicleComponent.GetForVehicle(vehicle);
            if (component == null)
            {
                if (!vehicleHasPermission)
                    return;

                component = UnderwaterVehicleComponent.AddToVehicle(vehicle, vehicleInfo);
            }

            if (vehicleHasPermission)
            {
                if (!component.IsUnderwaterCapable)
                {
                    component.EnableUnderwater();
                }
                else if (vehicle.AnyMounted())
                {
                    component.EnableCustomDrag();
                }
            }
            else if (component.IsUnderwaterCapable)
            {
                component.DisableUnderwater();
            }
        }

        private void HandleSeatMountedChanged(BaseVehicleSeat seat)
        {
            var vehicle = GetParentVehicle(seat);
            if (vehicle == null)
                return;

            var vehicleInfo = _vehicleInfoManager.GetVehicleInfo(vehicle);
            if (vehicleInfo == null || !vehicleInfo.Config.Enabled)
                return;

            RefreshUnderwaterCapability(vehicle, vehicleInfo);
        }

        #endregion

        #region Helper Classes

        private class UnderwaterVehicleComponent : FacepunchBehaviour
        {
            public static UnderwaterVehicleComponent AddToVehicle(BaseVehicle vehicle, IVehicleInfo vehicleInfo)
            {
                var component = vehicle.gameObject.AddComponent<UnderwaterVehicleComponent>();
                component._vehicleInfo = vehicleInfo;
                component._vehicle = vehicle;

                var waterLoggedPoint = vehicleInfo.GetWaterLoggedPoint(vehicle);
                component._waterLoggedPoint = waterLoggedPoint;
                component._waterLoggedPointParent = waterLoggedPoint.parent;
                component._waterLoggedPointLocalPosition = waterLoggedPoint.localPosition;

                var groundVehicle = vehicle as GroundVehicle;
                var groundVehicleConfig = vehicleInfo.Config as GroundVehicleConfig;
                if ((object)groundVehicle != null && groundVehicleConfig != null)
                {
                    var component2 = component;
                    var groundVehicle2 = groundVehicle;
                    var dragMultiplier = groundVehicleConfig.DragMultiplier;
                    component._customDragCheck = () => component2.CustomDragCheck(groundVehicle2, dragMultiplier);
                }

                return component;
            }

            public static UnderwaterVehicleComponent GetForVehicle(BaseEntity vehicle)
            {
                return vehicle.gameObject.GetComponent<UnderwaterVehicleComponent>();
            }

            public static void RemoveFromVehicle(BaseEntity vehicle)
            {
                DestroyImmediate(GetForVehicle(vehicle));
            }

            public bool IsUnderwaterCapable { get; private set; }
            private IVehicleInfo _vehicleInfo;
            private BaseVehicle _vehicle;
            private Transform _waterLoggedPoint;
            private Transform _waterLoggedPointParent;
            private Vector3 _waterLoggedPointLocalPosition;
            private Action _customDragCheck;

            public void EnableUnderwater()
            {
                if (IsUnderwaterCapable
                    || _waterLoggedPoint == null
                    || _waterLoggedPoint.parent == null)
                    return;

                var hookResult = ExposedHooks.OnVehicleUnderwaterEnable(_vehicle);
                if (hookResult is bool && !(bool)hookResult)
                    return;

                _waterLoggedPoint.SetParent(null);
                _waterLoggedPoint.position = new Vector3(0, 1000, 0);
                IsUnderwaterCapable = true;

                EnableCustomDrag();
            }

            public void DisableUnderwater()
            {
                if (!IsUnderwaterCapable
                    || _waterLoggedPoint == null
                    || _waterLoggedPoint.parent == _waterLoggedPointParent)
                    return;

                _waterLoggedPoint.SetParent(_waterLoggedPointParent);
                _waterLoggedPoint.transform.localPosition = _waterLoggedPointLocalPosition;
                IsUnderwaterCapable = false;

                DisableCustomDrag();
            }

            public void EnableCustomDrag()
            {
                if (_customDragCheck == null
                    || IsInvoking(_customDragCheck))
                    return;

                _vehicleInfo.SetTimeSinceWaterCheck(_vehicle, float.MinValue);
                InvokeRandomized(_customDragCheck, 0.25f, 0.25f, 0.05f);
            }

            private void DisableCustomDrag()
            {
                if (_customDragCheck == null
                    || !IsInvoking(_customDragCheck))
                    return;

                _vehicleInfo.SetTimeSinceWaterCheck(_vehicle, UnityEngine.Random.Range(0f, 0.25f));
                CancelInvoke(_customDragCheck);
            }

            private void CustomDragCheck(GroundVehicle groundVehicle, float dragMultiplier)
            {
                // Most of this code is identical to the vanilla drag computation
                var throttleInput = groundVehicle.IsOn() ? groundVehicle.GetThrottleInput() : 0;
                var waterFactor = groundVehicle.WaterFactor() * dragMultiplier;
                var drag = 0f;
                TriggerVehicleDrag triggerResult;
                if (groundVehicle.FindTrigger(out triggerResult))
                {
                    drag = triggerResult.vehicleDrag;
                }
                var throttleDrag = (throttleInput != 0) ? 0 : 0.25f;
                drag = Mathf.Max(waterFactor, drag);
                drag = Mathf.Max(drag, groundVehicle.GetModifiedDrag());
                groundVehicle.rigidBody.drag = Mathf.Max(throttleDrag, drag);
                groundVehicle.rigidBody.angularDrag = drag * 0.5f;
            }

            private void OnDestroy()
            {
                if (_vehicle != null && !_vehicle.IsDestroyed)
                {
                    DisableUnderwater();

                    var groundVehicle = _vehicle as GroundVehicle;
                    if ((object)groundVehicle != null)
                    {
                        DisableCustomDrag();
                    }
                }
                else if (_waterLoggedPoint != null)
                {
                    Destroy(_waterLoggedPoint.gameObject);
                }
            }
        }

        #endregion

        #region Vehicle Info

        private interface IVehicleInfo
        {
            VehicleConfig Config { get; }
            uint[] PrefabIds { get; }
            string Permission { get; }

            void OnServerInitialized(UnderwaterVehicles plugin);
            bool IsCorrectType(BaseEntity entity);
            Transform GetWaterLoggedPoint(BaseEntity entity);
            void SetTimeSinceWaterCheck(BaseEntity entity, float deltaTime);
        }

        private class VehicleInfo<T> : IVehicleInfo where T : BaseEntity
        {
            public VehicleConfig Config { get; set; }
            public uint[] PrefabIds { get; private set; }
            public string Permission { get; private set; }

            public string VehicleName { get; set; }
            public string[] PrefabPaths { get; set; }
            public Func<T, Transform> FindWaterLoggedPoint { private get; set; } = entity => null;
            public Action<T, float> ApplyTimeSinceWaterCheck { private get; set; } = (entity, deltaTime) => {};

            public void OnServerInitialized(UnderwaterVehicles plugin)
            {
                Permission = $"{nameof(UnderwaterVehicles)}.occupant.{VehicleName}".ToLower();
                plugin.permission.RegisterPermission(Permission, plugin);

                var prefabIds = new List<uint>(PrefabPaths.Length);

                foreach (var prefabName in PrefabPaths)
                {
                    var prefab = GameManager.server.FindPrefab(prefabName)?.GetComponent<T>();
                    if (prefab == null)
                    {
                        plugin.LogError($"Invalid or incorrect prefab. Please alert the plugin maintainer -- {prefabName}");
                        continue;
                    }

                    prefabIds.Add(prefab.prefabID);
                }

                PrefabIds = prefabIds.ToArray();
            }

            public bool IsCorrectType(BaseEntity entity)
            {
                return entity is T;
            }

            public Transform GetWaterLoggedPoint(BaseEntity entity)
            {
                var entityOfType = entity as T;
                if ((object)entityOfType == null)
                    return null;

                return FindWaterLoggedPoint(entityOfType);
            }

            public void SetTimeSinceWaterCheck(BaseEntity entity, float deltaTime)
            {
                var entityOfType = entity as T;
                if ((object)entityOfType == null)
                    return;

                ApplyTimeSinceWaterCheck(entityOfType, deltaTime);
            }
        }

        private class VehicleInfoManager
        {
            private static readonly FieldInfo BikeCarPhysicsField = typeof(Bike).GetField("carPhysics",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            private readonly UnderwaterVehicles _plugin;

            private readonly Dictionary<uint, IVehicleInfo> _prefabIdToVehicleInfo = new Dictionary<uint, IVehicleInfo>();
            private IVehicleInfo[] _allVehicles;

            private Configuration _config => _plugin._config;

            public VehicleInfoManager(UnderwaterVehicles plugin)
            {
                _plugin = plugin;
            }

            public void OnServerInitialized()
            {
                _allVehicles = new IVehicleInfo[]
                {
                    new VehicleInfo<ModularCar>
                    {
                        VehicleName = "modularcar",
                        PrefabPaths = FindPrefabsOfType<ModularCar>(),
                        Config = _config.ModularCar,
                        FindWaterLoggedPoint = vehicle => vehicle.waterloggedPoint,
                        ApplyTimeSinceWaterCheck = (car, deltaTime) => car.carPhysics.timeSinceWaterCheck = deltaTime,
                    },
                    new VehicleInfo<Snowmobile>
                    {
                        VehicleName = "snowmobile",
                        PrefabPaths = new[] { "assets/content/vehicles/snowmobiles/snowmobile.prefab" },
                        Config = _config.Snowmobile,
                        FindWaterLoggedPoint = vehicle => vehicle.waterloggedPoint,
                        ApplyTimeSinceWaterCheck = (vehicle, deltaTime) => vehicle.carPhysics.timeSinceWaterCheck = deltaTime,
                    },
                    new VehicleInfo<Snowmobile>
                    {
                        VehicleName = "tomaha",
                        PrefabPaths = new[] { "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab" },
                        Config = _config.Tomaha,
                        FindWaterLoggedPoint = vehicle => vehicle.waterloggedPoint,
                        ApplyTimeSinceWaterCheck = (vehicle, deltaTime) => vehicle.carPhysics.timeSinceWaterCheck = deltaTime,
                    },
                    new VehicleInfo<Bike>
                    {
                        VehicleName = "motorbike.sidecar",
                        PrefabPaths = new[] { "assets/content/vehicles/bikes/motorbike_sidecar.prefab" },
                        Config = _config.MotorBikeSidecar,
                        FindWaterLoggedPoint = vehicle => vehicle.waterloggedPoint,
                        ApplyTimeSinceWaterCheck = (vehicle, deltaTime) => ((CarPhysics<Bike>)BikeCarPhysicsField.GetValue(vehicle)).timeSinceWaterCheck = deltaTime,
                    },
                    new VehicleInfo<Bike>
                    {
                        VehicleName = "motorbike",
                        PrefabPaths = new[] { "assets/content/vehicles/bikes/motorbike.prefab" },
                        Config = _config.MotorBike,
                        FindWaterLoggedPoint = vehicle => vehicle.waterloggedPoint,
                        ApplyTimeSinceWaterCheck = (vehicle, deltaTime) => ((CarPhysics<Bike>)BikeCarPhysicsField.GetValue(vehicle)).timeSinceWaterCheck = deltaTime,
                    },
                    new VehicleInfo<Bike>
                    {
                        VehicleName = "pedalbike",
                        PrefabPaths = new[] { "assets/content/vehicles/bikes/pedalbike.prefab" },
                        Config = _config.PedalBike,
                        FindWaterLoggedPoint = vehicle => vehicle.waterloggedPoint,
                        ApplyTimeSinceWaterCheck = (vehicle, deltaTime) => ((CarPhysics<Bike>)BikeCarPhysicsField.GetValue(vehicle)).timeSinceWaterCheck = deltaTime,
                    },
                    new VehicleInfo<Bike>
                    {
                        VehicleName = "pedaltrike",
                        PrefabPaths = new[] { "assets/content/vehicles/bikes/pedaltrike.prefab" },
                        Config = _config.PedalTrike,
                        FindWaterLoggedPoint = vehicle => vehicle.waterloggedPoint,
                        ApplyTimeSinceWaterCheck = (vehicle, deltaTime) => ((CarPhysics<Bike>)BikeCarPhysicsField.GetValue(vehicle)).timeSinceWaterCheck = deltaTime,
                    },
                    new VehicleInfo<MagnetCrane>
                    {
                        VehicleName = "magnetcrane",
                        PrefabPaths = new[] { "assets/content/vehicles/crane_magnet/magnetcrane.entity.prefab" },
                        Config = _config.MagnetCrane,
                        FindWaterLoggedPoint = vehicle => vehicle.waterloggedPoint,
                        ApplyTimeSinceWaterCheck = (vehicle, deltaTime) => vehicle.carPhysics.timeSinceWaterCheck = deltaTime,
                    },
                    new VehicleInfo<Minicopter>
                    {
                        VehicleName = "minicopter",
                        PrefabPaths = new[] { "assets/content/vehicles/minicopter/minicopter.entity.prefab" },
                        Config = _config.Minicopter,
                        FindWaterLoggedPoint = vehicle => vehicle.engineController.waterloggedPoint,
                    },
                    new VehicleInfo<ScrapTransportHelicopter>
                    {
                        VehicleName = "scraptransport",
                        PrefabPaths = new[] { "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab" },
                        Config = _config.ScrapTransportHelicopter,
                        FindWaterLoggedPoint = vehicle => vehicle.engineController.waterloggedPoint,
                    },
                    new VehicleInfo<AttackHelicopter>
                    {
                        VehicleName = "attackhelicopter",
                        PrefabPaths = new[] { "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab" },
                        Config = _config.AttackHelicopter,
                        FindWaterLoggedPoint = vehicle => vehicle.engineController.waterloggedPoint,
                    },
                };

                foreach (var vehicleInfo in _allVehicles)
                {
                    vehicleInfo.OnServerInitialized(_plugin);

                    foreach (var prefabId in vehicleInfo.PrefabIds)
                    {
                        _prefabIdToVehicleInfo[prefabId] = vehicleInfo;
                    }
                }
            }

            public IVehicleInfo GetVehicleInfo(BaseEntity entity)
            {
                IVehicleInfo vehicleInfo;
                return _prefabIdToVehicleInfo.TryGetValue(entity.prefabID, out vehicleInfo) && vehicleInfo.IsCorrectType(entity)
                    ? vehicleInfo
                    : null;
            }
        }

        #endregion

        #region Configuration

        [JsonObject(MemberSerialization.OptIn)]
        private class VehicleConfig
        {
            [JsonProperty("Enabled", Order = -4)]
            public bool Enabled;

            [JsonProperty("RequireOccupantPermission", Order = -3)]
            public bool RequireOccupantPermission;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class GroundVehicleConfig : VehicleConfig
        {
            [JsonProperty("DragMultiplier", Order = -2)]
            public float DragMultiplier = 1;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("ModularCar")]
            public GroundVehicleConfig ModularCar = new GroundVehicleConfig();

            [JsonProperty("Snowmobile")]
            public GroundVehicleConfig Snowmobile = new GroundVehicleConfig();

            [JsonProperty("Tomaha")]
            public GroundVehicleConfig Tomaha = new GroundVehicleConfig();

            [JsonProperty("MotorBikeSidecar")]
            public GroundVehicleConfig MotorBikeSidecar = new GroundVehicleConfig();

            [JsonProperty("MotorBike")]
            public GroundVehicleConfig MotorBike = new GroundVehicleConfig();

            [JsonProperty("PedalBike")]
            public GroundVehicleConfig PedalBike = new GroundVehicleConfig();

            [JsonProperty("PedalTrike")]
            public GroundVehicleConfig PedalTrike = new GroundVehicleConfig();

            [JsonProperty("MagnetCrane")]
            public GroundVehicleConfig MagnetCrane = new GroundVehicleConfig();

            [JsonProperty("Minicopter")]
            public VehicleConfig Minicopter = new VehicleConfig();

            [JsonProperty("ScrapTransportHelicopter")]
            public VehicleConfig ScrapTransportHelicopter = new VehicleConfig();

            [JsonProperty("AttackHelicopter")]
            public VehicleConfig AttackHelicopter = new VehicleConfig();
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #region Configuration Helpers

        private class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            var changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_config))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #endregion

        #endregion
    }
}
