using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("Limited Drone Height", "WhiteThunder", "1.0.0")]
    [Description("Limits how high RC drones can be flown above terrain.")]
    internal class LimitedDroneHeight : CovalencePlugin
    {
        #region Fields

        private static LimitedDroneHeight _pluginInstance;
        private static Configuration _pluginConfig;

        private const string PermissionProfilePrefix = "limiteddroneheight";

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginConfig.Init(this);
            _pluginInstance = this;
        }

        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                var computerStation = player.GetMounted() as ComputerStation;
                if (computerStation == null)
                    continue;

                var drone = computerStation.currentlyControllingEnt.Get(serverside: true) as Drone;
                if (drone == null)
                    continue;

                OnDroneControlStarted(drone);
            }
        }

        private void Unload()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var drone = entity as Drone;
                if (drone == null)
                    continue;

                HeightLimiter.RemoveFromDrone(drone);
            }

            _pluginConfig = null;
            _pluginInstance = null;
        }

        private void OnBookmarkControlStarted(ComputerStation computerStation, BasePlayer player, string bookmarkName, Drone drone)
        {
            OnDroneControlStarted(drone);
        }

        private void OnBookmarkControlEnded(ComputerStation computerStation, BasePlayer player, Drone drone)
        {
            OnDroneControlEnded(drone);
        }

        // This hook is exposed by plugin: Ridable Drones (RidableDrones).
        private void OnDroneControlStarted(Drone drone)
        {
            var maxHeight = _pluginConfig.GetMaxHeightForDrone(drone);
            if (maxHeight == 0)
                return;

            if (LimitHeightWasBlocked(drone))
                return;

            HeightLimiter.StartControl(drone, maxHeight);
        }

        // This hook is exposed by plugin: Ridable Drones (RidableDrones).
        private void OnDroneControlEnded(Drone drone)
        {
            if (drone == null)
                return;

            HeightLimiter.StopControl(drone);
        }

        #endregion

        #region Helper Methods

        private static bool LimitHeightWasBlocked(Drone drone)
        {
            object hookResult = Interface.CallHook("OnDroneHeightLimit", drone);
            return hookResult is bool && (bool)hookResult == false;
        }

        private bool IsDroneEligible(Drone drone) => !(drone is DeliveryDrone);

        private static string GetProfilePermission(string profileSuffix) =>
            $"{PermissionProfilePrefix}.{profileSuffix}";

        #endregion

        private class HeightLimiter : EntityComponent<Drone>
        {
            private const float DistanceFromMaxHeightToSlow = 5;
            private const float DistanceToleranceAboveMaxHeight = 5;

            public static void StartControl(Drone drone, int maxHeight) =>
                drone.GetOrAddComponent<HeightLimiter>().OnControlStarted(maxHeight);

            public static void StopControl(Drone drone) =>
                drone.GetComponent<HeightLimiter>()?.OnControlStopped();

            public static void RemoveFromDrone(Drone drone) =>
                DestroyImmediate(drone.GetComponent<HeightLimiter>());

            private Transform _droneTransform;
            private int _maxHeight;
            private int _numControllers = 0;

            private void Awake()
            {
                _droneTransform = baseEntity.transform;
            }

            private void OnControlStarted(int maxHeight)
            {
                CancelInvoke(DelayedDestroy);

                _numControllers++;
                _maxHeight = maxHeight;
            }

            private void DelayedDestroy() => DestroyImmediate(this);

            private void OnControlStopped()
            {
                _numControllers--;
                if (_numControllers <= 0)
                    Invoke(DelayedDestroy, 0);
            }

            // Using LateUpdate since RidableDrones uses Update() to send player inputs.
            private void LateUpdate()
            {
                if (!baseEntity.IsBeingControlled)
                    return;

                // Optimization: Skip if there was no user input this frame (keep using previous input).
                // This may not be totally ideal if the player is lagging and not sending updates,
                // since that could mean the drone will keep moving downward.
                if (baseEntity.lastInputTime < Time.time)
                    return;

                var dronePosition = _droneTransform.position;
                var terrainOrWaterHeight = Math.Max(TerrainMeta.WaterMap.GetHeight(dronePosition), TerrainMeta.HeightMap.GetHeight(dronePosition));
                var currentHeight = dronePosition.y - terrainOrWaterHeight;

                var heightDiff = currentHeight - _maxHeight;
                var currentThrottle = baseEntity.currentInput.throttle;

                if (heightDiff > DistanceToleranceAboveMaxHeight && currentThrottle >= 0)
                {
                    // Drone is above max height, and the player is not attempting to move down, so set throttle to negative to make the drone move down.
                    baseEntity.currentInput.throttle = -heightDiff / baseEntity.altitudeAcceleration;
                }
                else if (heightDiff >= 0 && currentThrottle > 0)
                {
                    // Drone is within allowed distance above max height, and the player is attempting to move up, so set throttle to zero.
                    baseEntity.currentInput.throttle = 0;
                }
                else if (heightDiff < 0 && heightDiff >= -DistanceFromMaxHeightToSlow && currentThrottle > 0)
                {
                    // Close to max height, and the player is attempting to move up, so reduce throttle relative to the distance.
                    // For example: If 4 meters away, use 0.8 throttle. If 2 meters away, use 0.4 throttle.
                    baseEntity.currentInput.throttle = 1 / DistanceFromMaxHeightToSlow * heightDiff / baseEntity.altitudeAcceleration;
                }
            }
        }

        #region Configuration

        private class HeightProfile
        {
            [JsonProperty("PermissionSuffix")]
            public string PermissionSuffix;

            [JsonProperty("MaxHeight")]
            public int MaxHeight;

            [JsonIgnore]
            public string Permission;

            public void Init(LimitedDroneHeight pluginInstance)
            {
                if (string.IsNullOrWhiteSpace(PermissionSuffix))
                    return;

                Permission = GetProfilePermission(PermissionSuffix);
                pluginInstance.permission.RegisterPermission(Permission, pluginInstance);
            }
        }

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("DefaultMaxHeight")]
            public int DefaultMaxHeight = 75;

            [JsonProperty("ProfilesRequiringPermission")]
            public HeightProfile[] ProfilesRequiringPermission = new HeightProfile[]
            {
                new HeightProfile()
                {
                    PermissionSuffix = "low",
                    MaxHeight = 25,
                },
                new HeightProfile()
                {
                    PermissionSuffix = "medium",
                    MaxHeight = 75,
                },
                new HeightProfile()
                {
                    PermissionSuffix = "high",
                    MaxHeight = 125,
                },
                new HeightProfile()
                {
                    PermissionSuffix = "unlimited",
                    MaxHeight = 0,
                },
            };

            public void Init(LimitedDroneHeight pluginInstance)
            {
                foreach (var profile in ProfilesRequiringPermission)
                    profile.Init(pluginInstance);
            }

            public int GetMaxHeightForDrone(Drone drone)
            {
                if (drone.OwnerID == 0)
                    return DefaultMaxHeight;

                if (ProfilesRequiringPermission == null)
                    return DefaultMaxHeight;

                var ownerIdString = drone.OwnerID.ToString();

                for (var i = ProfilesRequiringPermission.Length - 1; i >= 0; i--)
                {
                    var profile = ProfilesRequiringPermission[i];
                    if (profile.Permission != null && _pluginInstance.permission.UserHasPermission(ownerIdString, profile.Permission))
                        return profile.MaxHeight;
                }

                return DefaultMaxHeight;
            }
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #endregion

        #region Configuration Boilerplate

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
            bool changed = false;

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

        protected override void LoadDefaultConfig() => _pluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _pluginConfig = Config.ReadObject<Configuration>();
                if (_pluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_pluginConfig))
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
            Config.WriteObject(_pluginConfig, true);
        }

        #endregion
    }
}
