using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Drone Lights", "WhiteThunder", "2.0.2")]
    [Description("Adds controllable search lights to RC drones.")]
    internal class DroneLights : CovalencePlugin
    {
        #region Fields

        private const string PermissionAutoDeploy = "dronelights.searchlight.autodeploy";
        private const string PermissionMoveLight = "dronelights.searchlight.move";

        private const string SpherePrefab = "assets/prefabs/visualization/sphere.prefab";
        private const string SearchLightPrefab = "assets/prefabs/deployable/search light/searchlight.deployed.prefab";

        private const float SearchLightYAxisRotation = 180;
        private const float SearchLightScale = 0.1f;

        private static readonly Vector3 SphereEntityLocalPosition = new Vector3(0, -0.075f, 0.25f);
        private static readonly Vector3 SearchLightLocalPosition = new Vector3(0, -1.25f, -0.25f);

        private static readonly FieldInfo DronePitchField = typeof(Drone).GetField("pitch", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private Configuration _config;
        private ProtectionProperties _immortalProtection;

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionAutoDeploy, this);
            permission.RegisterPermission(PermissionMoveLight, this);
            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void Unload()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var drone = entity as Drone;
                if (drone == null || !IsDroneEligible(drone))
                    continue;

                SearchLightUpdater.RemoveFromDrone(drone);
            }

            UnityEngine.Object.Destroy(_immortalProtection);
        }

        private void OnServerInitialized()
        {
            _immortalProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
            _immortalProtection.name = "DroneLightsProtection";
            _immortalProtection.Add(1);

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var drone = entity as Drone;
                if (drone == null || !IsDroneEligible(drone))
                    continue;

                AddOrUpdateSearchLight(drone);
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                var station = player.GetMounted() as ComputerStation;
                if (station == null)
                    continue;

                var drone = station.currentlyControllingEnt.Get(serverside: true) as Drone;
                if (drone == null)
                    continue;

                OnBookmarkControlStarted(station, player, string.Empty, drone);
            }

            Subscribe(nameof(OnEntitySpawned));
        }

        private void OnEntitySpawned(Drone drone)
        {
            if (!IsDroneEligible(drone))
                return;

            var drone2 = drone;
            NextTick(() =>
            {
                if (drone2 == null || drone2.IsDestroyed)
                    return;

                MaybeAutoDeploySearchLight(drone2);
            });
        }

        private void OnBookmarkControlStarted(ComputerStation station, BasePlayer player, string bookmarkName, Drone drone)
        {
            var controllerSteamId = drone.ControllingViewerId?.SteamId;
            if (controllerSteamId != player.userID)
                return;

            SphereEntity sphereEntity;
            var searchLight = GetDroneSearchLight(drone, out sphereEntity);
            if (searchLight == null)
                return;

            var hasMovePermission = permission.UserHasPermission(player.UserIDString, PermissionMoveLight);
            if (!hasMovePermission)
            {
                var defaultAngle = _config.SearchLight.DefaultAngle - 90 % 360;
                SetLightAngle(drone, sphereEntity, sphereEntity.transform, defaultAngle);
            }

            SearchLightUpdater.AddOrUpdateForDrone(this, drone, sphereEntity, searchLight, player, hasMovePermission);
        }

        #endregion

        #region Helper Methods

        private static bool DeployLightWasBlocked(Drone drone)
        {
            var hookResult = Interface.CallHook("OnDroneSearchLightDeploy", drone);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool IsDroneEligible(Drone drone)
        {
            return drone.skinID == 0 && !(drone is DeliveryDrone);
        }

        private static Drone GetControlledDrone(ComputerStation station)
        {
            return station.currentlyControllingEnt.Get(serverside: true) as Drone;
        }

        private static Drone GetControlledDrone(BasePlayer player)
        {
            var computerStation = player.GetMounted() as ComputerStation;
            if (computerStation == null)
                return null;

            return GetControlledDrone(computerStation);
        }

        private static T2 GetGrandChildOfType<T1, T2>(BaseEntity entity, out T1 childOfType) where T1 : BaseEntity where T2 : BaseEntity
        {
            foreach (var child in entity.children)
            {
                childOfType = child as T1;
                if (childOfType == null)
                    continue;

                foreach (var grandChild in childOfType.children)
                {
                    var grandChildOfType = grandChild as T2;
                    if (grandChildOfType != null)
                        return grandChildOfType;
                }
            }

            childOfType = null;
            return null;
        }

        private static SearchLight GetDroneSearchLight(Drone drone, out SphereEntity parentSphere)
        {
            return GetGrandChildOfType<SphereEntity, SearchLight>(drone, out parentSphere);
        }

        private static SearchLight GetControlledSearchLight(BasePlayer player, out SphereEntity parentSphere, out Drone drone)
        {
            drone = GetControlledDrone(player);
            if (drone == null)
            {
                parentSphere = null;
                return null;
            }

            return GetDroneSearchLight(drone, out parentSphere);
        }

        private static SearchLight GetControlledSearchLight(BasePlayer player)
        {
            Drone drone;
            SphereEntity parentSphere;
            return GetControlledSearchLight(player, out parentSphere, out drone);
        }

        private static void RemoveProblemComponents(BaseEntity entity)
        {
            foreach (var collider in entity.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());
        }

        private static void HideInputsAndOutputs(IOEntity ioEntity)
        {
            // Trick to hide the inputs and outputs on the client.
            foreach (var input in ioEntity.inputs)
            {
                input.type = IOEntity.IOType.Generic;
            }

            foreach (var output in ioEntity.outputs)
            {
                output.type = IOEntity.IOType.Generic;
            }
        }

        private static void SetLightAngle(Drone drone, SphereEntity sphere, Transform transform, float overrideAngle = 0)
        {
            float desiredPitch;

            if (overrideAngle != 0)
            {
                desiredPitch = overrideAngle;
            }
            else
            {
                if (DronePitchField == null)
                    return;

                desiredPitch = (float)DronePitchField.GetValue(drone);
                desiredPitch = (360 - desiredPitch) % 360;

            }

            var currentPitch = transform.localEulerAngles.x;
            if (Math.Abs(currentPitch - desiredPitch) < 0.1f)
                return;

            transform.localEulerAngles = new Vector3(desiredPitch, SearchLightYAxisRotation, 0);
            sphere.InvalidateNetworkCache();

            // This is the most expensive line in terms of performance.
            sphere.SendNetworkUpdate_Position();
        }

        private SearchLight TryDeploySearchLight(Drone drone)
        {
            if (DeployLightWasBlocked(drone))
                return null;

            var defaultAngle = _config.SearchLight.DefaultAngle - 90 % 360;
            var localRotation = Quaternion.Euler(defaultAngle, SearchLightYAxisRotation, 0);
            var sphereEntity = GameManager.server.CreateEntity(SpherePrefab, SphereEntityLocalPosition, localRotation) as SphereEntity;
            if (sphereEntity == null)
                return null;

            SetupSphereEntity(sphereEntity);

            sphereEntity.currentRadius = SearchLightScale;
            sphereEntity.lerpRadius = SearchLightScale;

            sphereEntity.SetParent(drone);
            sphereEntity.Spawn();

            var searchLight = GameManager.server.CreateEntity(SearchLightPrefab, SearchLightLocalPosition) as SearchLight;
            if (searchLight == null)
                return null;

            SetupSearchLight(searchLight);

            searchLight.SetFlag(BaseEntity.Flags.Disabled, true);
            searchLight.SetParent(sphereEntity);
            searchLight.Spawn();
            Interface.CallHook("OnDroneSearchLightDeployed", drone, searchLight);

            searchLight.Invoke(() =>
            {
                searchLight.SetFlag(BaseEntity.Flags.Disabled, false);
            }, 5f);

            return searchLight;
        }

        private void SetupSphereEntity(SphereEntity sphereEntity)
        {
            sphereEntity.transform.localPosition = SphereEntityLocalPosition;
            sphereEntity.EnableSaving(true);
            sphereEntity.EnableGlobalBroadcast(false);
        }

        private void SetupSearchLight(SearchLight searchLight)
        {
            RemoveProblemComponents(searchLight);
            HideInputsAndOutputs(searchLight);
            searchLight.EnableSaving(true);
            searchLight.SetFlag(BaseEntity.Flags.Busy, true);
            searchLight.baseProtection = _immortalProtection;
            searchLight.pickup.enabled = false;
        }

        private void AddOrUpdateSearchLight(Drone drone)
        {
            SphereEntity sphereEntity;
            var searchLight = GetDroneSearchLight(drone, out sphereEntity);
            if (searchLight == null)
            {
                MaybeAutoDeploySearchLight(drone);
                return;
            }

            SetupSphereEntity(sphereEntity);
            SetupSearchLight(searchLight);
        }

        private void MaybeAutoDeploySearchLight(Drone drone)
        {
            if (!permission.UserHasPermission(drone.OwnerID.ToString(), PermissionAutoDeploy))
                return;

            TryDeploySearchLight(drone);
        }

        #endregion

        #region Classes

        private class SearchLightUpdater : FacepunchBehaviour
        {
            public static void AddOrUpdateForDrone(DroneLights plugin, Drone drone, SphereEntity sphereEntity, SearchLight searchLight, BasePlayer controller, bool canMove)
            {
                var component = GetForDrone(drone);
                if (component == null)
                {
                    component = drone.gameObject.AddComponent<SearchLightUpdater>();
                    component._plugin = plugin;
                    component._drone = drone;
                    component._sphereEntity = sphereEntity;
                    component._sphereTransform = sphereEntity.transform;
                    component._searchLight = searchLight;
                }

                component._controller = controller;
                component._canMove = canMove;
                component.enabled = true;
            }

            public static void RemoveFromDrone(Drone drone)
            {
                DestroyImmediate(GetForDrone(drone));
            }

            private static SearchLightUpdater GetForDrone(Drone drone)
            {
                return drone.gameObject.GetComponent<SearchLightUpdater>();
            }

            private DroneLights _plugin;
            private Drone _drone;
            private SphereEntity _sphereEntity;
            private Transform _sphereTransform;
            private SearchLight _searchLight;
            private BasePlayer _controller;
            private bool _canMove;

            private void Update()
            {
                var controllerSteamId = _drone.ControllingViewerId?.SteamId ?? 0;
                if (controllerSteamId == 0)
                {
                    enabled = false;
                    return;
                }

                _plugin.TrackStart();

                if (_controller.lastTickTime == Time.time && _controller.serverInput.WasJustPressed(BUTTON.FIRE_SECONDARY))
                {
                    _searchLight.SetFlag(IOEntity.Flag_HasPower, !_searchLight.IsPowered());
                }

                if (_canMove && !_drone.isGrounded && _searchLight.IsPowered())
                {
                    SetLightAngle(_drone, _sphereEntity, _sphereTransform);
                }

                _plugin.TrackEnd();
            }
        }

        #endregion

        #region Configuration

        [JsonObject(MemberSerialization.OptIn)]
        private class Configuration : BaseConfiguration
        {
            [JsonProperty("SearchLight")]
            public SearchLightSettings SearchLight = new SearchLightSettings();
        }

        private class SearchLightSettings
        {
            [JsonProperty("DefaultAngle")]
            public int DefaultAngle = 75;
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #region Configuration Helpers

        [JsonObject(MemberSerialization.OptIn)]
        private class BaseConfiguration
        {
            private string ToJson() => JsonConvert.SerializeObject(this);

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

        private bool MaybeUpdateConfig(BaseConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigSection(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigSection(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
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
                        else if (MaybeUpdateConfigSection(defaultDictValue, currentDictValue))
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
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #endregion

        #endregion
    }
}
