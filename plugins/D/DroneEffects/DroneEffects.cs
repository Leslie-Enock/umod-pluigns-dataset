using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("Drone Effects", "WhiteThunder", "1.0.3")]
    [Description("Adds collision effects and propeller animations to RC drones.")]
    internal class DroneEffects : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin BetterDroneCollision;

        private static DroneEffects _pluginInstance;
        private static Configuration _pluginConfig;

        private const string DeliveryDronePrefab = "assets/prefabs/misc/marketplace/drone.delivery.prefab";

        private const float CollisionDistanceFraction = 0.25f;

        private bool _usingCustomCollisionListener = false;

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginInstance = this;

            if (!_pluginConfig.DeathEffect.Enabled
                || string.IsNullOrEmpty(_pluginConfig.DeathEffect.EffectPrefab))
            {
                Unsubscribe(nameof(OnEntityDeath));
            }

            if (!_pluginConfig.Animation.Enabled)
            {
                Unsubscribe(nameof(OnBookmarkControlStarted));
                Unsubscribe(nameof(OnBookmarkControlEnded));
                Unsubscribe(nameof(OnDroneControlStarted));
                Unsubscribe(nameof(OnDroneControlEnded));
            }

            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnDroneCollisionImpact));
        }

        private void OnServerInitialized()
        {
            if (_pluginConfig.CollisionEffect.Enabled
                && !string.IsNullOrEmpty(_pluginConfig.CollisionEffect.EffectPrefab))
            {
                if (BetterDroneCollision != null)
                {
                    Subscribe(nameof(OnDroneCollisionImpact));
                }
                else
                {
                    Subscribe(nameof(OnEntitySpawned));
                    _usingCustomCollisionListener = true;
                }
            }

            // Delay this in case Drone Hover needs a moment to set the drones to being controlled.
            NextTick(() =>
            {
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    var drone = entity as Drone;
                    if (drone == null || !IsDroneEligible(drone))
                        continue;

                    if (_pluginConfig.Animation.Enabled)
                        MaybeStartAnimating(drone);

                    if (_usingCustomCollisionListener)
                        drone.GetOrAddComponent<DroneCollisionListener>();
                }
            });
        }

        private void Unload()
        {
            if (_pluginConfig.Animation.Enabled)
            {
                foreach (var deliveryDrone in BaseNetworkable.serverEntities.OfType<DeliveryDrone>().ToArray())
                {
                    if (deliveryDrone != null && deliveryDrone.GetParentEntity() is Drone)
                        deliveryDrone.Kill();
                }
            }

            if (_usingCustomCollisionListener)
                DroneCollisionListener.DestroyAll();

            _pluginConfig = null;
            _pluginInstance = null;
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin == BetterDroneCollision && _usingCustomCollisionListener)
            {
                Unsubscribe(nameof(OnEntitySpawned));
                DroneCollisionListener.DestroyAll();
                _usingCustomCollisionListener = false;

                Subscribe(nameof(OnDroneCollisionImpact));
            }
        }

        private void OnBookmarkControlStarted(ComputerStation station, BasePlayer player, string bookmarkName, Drone drone)
        {
            OnDroneControlStarted(drone);
        }

        private void OnBookmarkControlEnded(ComputerStation station, BasePlayer player, Drone drone)
        {
            OnDroneControlEnded(drone);
        }

        private void OnEntitySpawned(Drone drone)
        {
            if (!IsDroneEligible(drone))
                return;

            drone.GetOrAddComponent<DroneCollisionListener>();
        }

        private void OnEntityDeath(Drone drone)
        {
            if (!IsDroneEligible(drone))
                return;

            Effect.server.Run(_pluginConfig.DeathEffect.EffectPrefab, drone.transform.position, Vector3.up);
        }

        // This hook is exposed by plugin: Better Drone Collision (BetterDroneCollision).
        private void OnDroneCollisionImpact(Drone drone, Collision collision)
        {
            ShowCollisionEffect(drone, collision);
        }

        // This hook is exposed by plugin: Ridable Drones (RidableDrones).
        private void OnDroneControlStarted(Drone drone)
        {
            MaybeStartAnimating(drone);
        }

        // This hook is exposed by plugin: Ridable Drones (RidableDrones).
        private void OnDroneControlEnded(Drone drone)
        {
            // Delay in case Drone Hover is going to keep the drone in the controlled state.
            NextTick(() =>
            {
                if (drone == null || drone.IsBeingControlled)
                    return;

                StopAnimating(drone);
            });
        }

        #endregion

        #region API

        private void API_StopAnimating(Drone drone)
        {
            StopAnimating(drone);
        }

        #endregion

        #region Helper Methods

        private static bool AnimateWasBlocked(Drone drone)
        {
            object hookResult = Interface.CallHook("OnDroneAnimationStart", drone);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool CollisionEffectWasBlocked(Drone drone, Collision collision)
        {
            object hookResult = Interface.CallHook("OnDroneCollisionEffect", drone);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool IsDroneEligible(Drone drone) =>
            !(drone is DeliveryDrone);

        private static Drone GetControlledDrone(ComputerStation computerStation) =>
            computerStation.currentlyControllingEnt.Get(serverside: true) as Drone;

        private static T GetChildOfType<T>(BaseEntity entity) where T : BaseEntity
        {
            foreach (var child in entity.children)
            {
                var childOfType = child as T;
                if (childOfType != null)
                    return childOfType;
            }
            return null;
        }

        private void ShowCollisionEffect(Drone drone, Collision collision)
        {
            if (CollisionEffectWasBlocked(drone, collision))
                return;

            var transform = drone.transform;
            var collisionPoint = collision.GetContact(0).point;
            collisionPoint += (transform.position - collisionPoint) * CollisionDistanceFraction;
            Effect.server.Run(_pluginConfig.CollisionEffect.EffectPrefab, collisionPoint, transform.up);
        }

        #endregion

        #region Helper Methods - Animation

        private static DeliveryDrone GetChildDeliveryDrone(Drone drone) =>
            GetChildOfType<DeliveryDrone>(drone);

        private static void SetupDeliveryDrone(DeliveryDrone deliveryDrone)
        {
            deliveryDrone.EnableSaving(false);
            deliveryDrone.EnableGlobalBroadcast(false);

            // Disable delivery drone AI.
            deliveryDrone.CancelInvoke(deliveryDrone.Think);

            // Prevent the Update() method from running.
            deliveryDrone.IsBeingControlled = true;

            // Prevent the FixedUpdate() method from running.
            deliveryDrone.lifestate = BaseCombatEntity.LifeState.Dead;

            // Remove physics.
            UnityEngine.Object.Destroy(deliveryDrone.body);

            if (deliveryDrone._mapMarkerInstance != null)
                deliveryDrone._mapMarkerInstance.Kill();
        }

        private static void StartAnimationg(Drone drone)
        {
            var deliveryDrone = GameManager.server.CreateEntity(DeliveryDronePrefab) as DeliveryDrone;
            if (deliveryDrone == null)
                return;

            deliveryDrone.SetParent(drone);
            deliveryDrone.CancelInvoke(deliveryDrone.Think);
            deliveryDrone.Spawn();
            SetupDeliveryDrone(deliveryDrone);
        }

        private static void MaybeStartAnimating(Drone drone)
        {
            if (!drone.IsBeingControlled)
                return;

            var deliveryDrone = GetChildDeliveryDrone(drone);
            if (deliveryDrone != null)
                return;

            if (AnimateWasBlocked(drone))
                return;

            StartAnimationg(drone);
        }

        private static void StopAnimating(Drone drone)
        {
            var deliveryDrone = GetChildDeliveryDrone(drone);
            if (deliveryDrone == null)
                return;

            deliveryDrone.Kill();
        }

        #endregion

        #region Collision Detection

        private class DroneCollisionListener : EntityComponent<Drone>
        {
            public static void DestroyAll()
            {
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    var drone = entity as Drone;
                    if (drone == null || !IsDroneEligible(drone))
                        continue;

                    var component = drone.GetComponent<DroneCollisionListener>();
                    if (component == null)
                        continue;

                    DestroyImmediate(component);
                }
            }

            private const float DelayBetweenCollisions = 0.25f;

            private float _nextCollisionFXTime;

            private void OnCollisionEnter(Collision collision)
            {
                var forceMagnitude = collision.impulse.magnitude / Time.fixedDeltaTime;
                if (forceMagnitude < _pluginConfig.CollisionEffect.RequiredMagnitude)
                    return;

                ShowCollisionFX(collision);
            }

            private void ShowCollisionFX(Collision collision)
            {
                if (Time.time < _nextCollisionFXTime)
                    return;

                _pluginInstance.ShowCollisionEffect(baseEntity, collision);
                _nextCollisionFXTime = Time.time + DelayBetweenCollisions;
            }
        }

        #endregion

        #region Configuration

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("Animation")]
            public AnimationSettings Animation = new AnimationSettings();

            [JsonProperty("CollisionEffect")]
            public CollisionSettings CollisionEffect = new CollisionSettings();

            [JsonProperty("DeathEffect")]
            public DeathSettings DeathEffect = new DeathSettings();
        }

        private class AnimationSettings
        {
            [JsonProperty("Enabled")]
            public bool Enabled = true;
        }

        private class CollisionSettings
        {
            [JsonProperty("Enabled")]
            public bool Enabled = true;

            [JsonProperty("RequiredMagnitude")]
            public int RequiredMagnitude = 40;

            [JsonProperty("EffectPrefab")]
            public string EffectPrefab = "assets/content/vehicles/modularcar/carcollisioneffect.prefab";
        }

        private class DeathSettings
        {
            [JsonProperty("Enabled")]
            public bool Enabled = true;

            [JsonProperty("EffectPrefab")]
            public string EffectPrefab = "assets/prefabs/ammo/40mmgrenade/effects/40mm_he_explosion.prefab";
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
