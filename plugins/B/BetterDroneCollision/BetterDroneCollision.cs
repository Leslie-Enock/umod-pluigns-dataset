using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using Network;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Better Drone Collision", "WhiteThunder", "1.1.0")]
    [Description("Overhauls drone collision damage so it's more intuitive.")]
    internal class BetterDroneCollision : CovalencePlugin
    {
        #region Fields

        private const float ReplacementHurtVelocityThreshold = float.MaxValue;

        [PluginReference]
        private readonly Plugin DroneSettings;

        private Configuration _config;
        private float? _vanillaHurtVelocityThreshold;

        #endregion

        #region Hooks

        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var drone = entity as Drone;
                if (drone == null)
                    continue;

                OnEntitySpawned(drone);
            }

            Subscribe(nameof(OnEntitySpawned));
        }

        private void Unload()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var drone = entity as Drone;
                if (drone == null || !IsDroneEligible(drone))
                    continue;

                ResetDrone(drone);
            }
        }

        private void OnEntitySpawned(Drone drone)
        {
            if (!IsDroneEligible(drone))
                return;

            _vanillaHurtVelocityThreshold ??= drone.hurtVelocityThreshold;

            var drone2 = drone;
            NextTick(() =>
            {
                if (drone2 != null)
                {
                    TryReplaceDroneCollision(drone2);
                }
            });
        }

        #endregion

        #region Helper Methods

        private static bool DroneCollisionReplaceWasBlocked(Drone drone)
        {
            return Interface.CallHook("OnDroneCollisionReplace", drone) is false;
        }

        private static bool IsDroneEligible(Drone drone)
        {
            return drone.skinID == 0 && drone is not DeliveryDrone;
        }

        private bool TryReplaceDroneCollision(Drone drone)
        {
            if (DroneCollisionReplaceWasBlocked(drone))
                return false;

            drone.hurtVelocityThreshold = ReplacementHurtVelocityThreshold;
            DroneCollisionReplacer.AddToDrone(this, drone);
            return true;
        }

        private void ResetDrone(Drone drone)
        {
            if (drone.hurtVelocityThreshold == ReplacementHurtVelocityThreshold
                && _vanillaHurtVelocityThreshold != null)
            {
                drone.hurtVelocityThreshold = (float)_vanillaHurtVelocityThreshold;
            }

            DroneCollisionReplacer.RemoveFromDrone(drone);
        }

        #endregion

        #region Collision Replacer

        private class DroneCollisionReplacer : FacepunchBehaviour
        {
            public static void AddToDrone(BetterDroneCollision plugin, Drone drone)
            {
                var component = drone.gameObject.AddComponent<DroneCollisionReplacer>();
                component._plugin = plugin;
                component._drone = drone;
            }

            public static void RemoveFromDrone(Drone drone)
            {
                Destroy(drone.gameObject.GetComponent<DroneCollisionReplacer>());
            }

            private BetterDroneCollision _plugin;
            private Drone _drone;
            private float _nextDamageTime;

            private Configuration _config => _plugin._config;

	        private void OnCollisionEnter(Collision collision)
            {
                if (collision == null || collision.gameObject == null)
                    return;

                if (Time.time < _nextDamageTime)
                    return;

                var magnitude = collision.relativeVelocity.magnitude;
                if (magnitude < _config.MinCollisionVelocity)
                    return;

                // Avoid damage when landing.
                if (Vector3.Dot(collision.relativeVelocity.normalized, _drone.transform.up) > 0.5f)
                    return;

                var otherEntity = collision.gameObject.ToBaseEntity();
                if (otherEntity is TimedExplosive)
                    return;

                if (otherEntity is DroppedItem)
                {
                    for (var i = 0; i < collision.contactCount; i++)
                    {
                        var contact = collision.GetContact(i);
                        Physics.IgnoreCollision(contact.thisCollider, contact.otherCollider);
                    }

                    // If the drone just had a collision, assume it was triggered by the Dropped Item and re-enable the drone.
                    if (Math.Abs(_drone.lastCollision - TimeEx.currentTimestamp) < 0.001f)
                    {
                        _drone.lastCollision = 0;
                    }

                    return;
                }

                var damage = magnitude * _config.CollisionDamageMultiplier;

                // If DroneSettings is not loaded, it's probably safe to assume that drones are using default protection properties.
                // Default protection properties make a drone immune to collision damage, so bypass protection.
                // Without this bypass, using this plugin standalone would make drones immune to collision which is not desirable.
                var useProtection = _plugin.DroneSettings != null;
                _drone.Hurt(damage, DamageType.Collision, useProtection: useProtection);

                Interface.CallHook("OnDroneCollisionImpact", _drone, collision);
                _nextDamageTime = Time.time + _config.MinTimeBetweenImpacts;
            }
        }

        #endregion

        #region Configuration

        [JsonObject(MemberSerialization.OptIn)]
        private class Configuration : BaseConfiguration
        {
            [JsonProperty("MinCollisionVelocity")]
            public float MinCollisionVelocity = 3;

            [JsonProperty("MinTimeBetweenImpacts")]
            public float MinTimeBetweenImpacts = 0.25f;

            [JsonProperty("CollisionDamageMultiplier")]
            public float CollisionDamageMultiplier = 1;
        }

        private Configuration GetDefaultConfig() => new();

        #region Configuration Helpers

        private class BaseConfiguration
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

        private bool MaybeUpdateConfig(BaseConfiguration config)
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
                if (currentRaw.TryGetValue(key, out var currentRawValue))
                {
                    var currentDictValue = currentRawValue as Dictionary<string, object>;
                    if (currentWithDefaults[key] is Dictionary<string, object> defaultDictValue)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                        {
                            changed = true;
                        }
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
