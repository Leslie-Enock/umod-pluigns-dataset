using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("Drone Settings", "WhiteThunder", "1.3.0")]
    [Description("Allows changing speed, toughness and other properties of RC drones.")]
    internal class DroneSettings : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private readonly Plugin DroneScaleManager;

        private Configuration _config;

        private const string PermissionProfilePrefix = "dronesettings";

        private const string BaseDroneType = "BaseDrone";

        private DroneProperties _vanillaDroneProperties;
        private ProtectionProperties _vanillaDroneProtection;
        private readonly List<ProtectionProperties> _customProtectionProperties = new List<ProtectionProperties>();
        private readonly List<string> _reusableDroneTypeList = new List<string>();

        #endregion

        #region Hooks

        private void Init()
        {
            _config.Init(this);

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

            foreach (var player in BasePlayer.activePlayerList)
            {
                var station = player.GetMounted() as ComputerStation;
                if (station == null)
                    continue;

                var drone = GetControlledDrone(station);
                if (drone == null)
                    continue;

                OnBookmarkControlStarted(station, player, drone.GetIdentifier(), drone);
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

                DroneConnectionFixer.RemoveFromDrone(drone);

                if (!ApplySettingsWasBlocked(drone))
                {
                    RestoreVanillaSettings(drone);
                    Interface.CallHook("OnDroneSettingsChanged", drone);
                }
            }

            foreach (var protectionProperties in _customProtectionProperties)
            {
                UnityEngine.Object.Destroy(protectionProperties);
            }
        }

        private void OnEntitySpawned(Drone drone)
        {
            if (!IsDroneEligible(drone))
                return;

            if (_vanillaDroneProtection == null)
            {
                _vanillaDroneProtection = drone.baseProtection;
            }

            if (_vanillaDroneProperties == null)
            {
                _vanillaDroneProperties = DroneProperties.FromDrone(drone);
            }

            var drone2 = drone;

            // Delay to give other plugins a moment to cache the drone id so they can specify drone type or block this.
            NextTick(() =>
            {
                if (drone2 == null)
                    return;

                var profile = GetDroneProfile(drone2);
                if (profile == null)
                    return;

                TryApplyProfile(drone2, profile);
            });
        }

        private void OnBookmarkControlStarted(ComputerStation station, BasePlayer player, string bookmarkName, Drone drone)
        {
            DroneConnectionFixer.OnControlStarted(this, drone, player);
        }

        private void OnBookmarkControlEnded(ComputerStation station, BasePlayer player, Drone drone)
        {
            if (drone == null)
                return;

            DroneConnectionFixer.OnControlEnded(drone, player);
        }

        private void OnDroneScaled(Drone drone, BaseEntity rootEntity, float scale, float previousScale)
        {
            if (scale == 1)
            {
                DroneConnectionFixer.OnRootEntityChanged(drone, drone);
            }
            else if (previousScale == 1)
            {
                DroneConnectionFixer.OnRootEntityChanged(drone, rootEntity);
            }
        }

        #endregion

        #region API

        private void API_RefreshDroneProfile(Drone drone)
        {
            var profile = GetDroneProfile(drone);
            if (profile == null)
                return;

            TryApplyProfile(drone, profile, restoreVanilla: true);
        }

        #endregion

        #region Helper Methods

        private static bool ApplySettingsWasBlocked(Drone drone)
        {
            var hookResult = Interface.CallHook("OnDroneSettingsChange", drone);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool IsDroneEligible(Drone drone)
        {
            return drone.skinID == 0 && !(drone is DeliveryDrone);
        }

        private static string GetProfilePermission(string droneType, string profileSuffix)
        {
            return $"{PermissionProfilePrefix}.{droneType}.{profileSuffix}";
        }

        private static Drone GetControlledDrone(ComputerStation station)
        {
            return station.currentlyControllingEnt.Get(serverside: true) as Drone;
        }

        private string DetermineBestDroneType(List<string> droneTypeList)
        {
            if (droneTypeList.Count == 0)
                return null;

            if (droneTypeList.Count == 1)
                return droneTypeList[0];

            string bestDroneType = null;
            var bestDroneTypePriorityIndex = int.MaxValue;

            // Sort by priority, else sort alphabetically.
            foreach (var droneType in droneTypeList)
            {
                var priorityIndex = Array.IndexOf(_config.DroneTypePriority, droneType);
                priorityIndex = priorityIndex >= 0 ? priorityIndex : int.MaxValue;

                if (bestDroneType == null)
                {
                    bestDroneType = droneType;
                    bestDroneTypePriorityIndex = priorityIndex;
                    continue;
                }

                if (priorityIndex < bestDroneTypePriorityIndex
                    || priorityIndex == bestDroneTypePriorityIndex && string.Compare(droneType, bestDroneType, StringComparison.InvariantCultureIgnoreCase) < 0)
                {
                    bestDroneType = droneType;
                    bestDroneTypePriorityIndex = priorityIndex;
                }
            }

            return bestDroneType;
        }

        private string DetermineDroneType(Drone drone)
        {
            _reusableDroneTypeList.Clear();
            var hookResult = Interface.CallHook("OnDroneTypeDetermine", drone, _reusableDroneTypeList);
            return _reusableDroneTypeList.Count > 0
                ? DetermineBestDroneType(_reusableDroneTypeList)
                : hookResult as string;
        }

        private DroneProfile GetDroneProfile(Drone drone)
        {
            var droneType = DetermineDroneType(drone) ?? BaseDroneType;
            return _config.FindProfile(this, droneType, drone.OwnerID);
        }

        private BaseEntity GetRootEntity(Drone drone)
        {
            return DroneScaleManager?.Call("API_GetRootEntity", drone) as BaseEntity;
        }

        private BaseEntity GetDroneOrRootEntity(Drone drone)
        {
            var rootEntity = GetRootEntity(drone);
            return rootEntity != null ? rootEntity : drone;
        }

        private void RestoreVanillaSettings(Drone drone)
        {
            if (_vanillaDroneProtection != null && _customProtectionProperties.Contains(drone.baseProtection))
            {
                drone.baseProtection = _vanillaDroneProtection;
            }

            _vanillaDroneProperties?.ApplyToDrone(drone);
        }

        private bool TryApplyProfile(Drone drone, DroneProfile profile, bool restoreVanilla = false)
        {
            if (ApplySettingsWasBlocked(drone))
                return false;

            if (restoreVanilla)
            {
                RestoreVanillaSettings(drone);
            }

            profile.ApplyToDrone(drone);
            Interface.CallHook("OnDroneSettingsChanged", drone);
            return true;
        }

        private ProtectionProperties CreateProtectionProperties(Dictionary<string, float> damageMap)
        {
            var protectionProperties = ScriptableObject.CreateInstance<ProtectionProperties>();
            _customProtectionProperties.Add(protectionProperties);

            foreach (var entry in damageMap)
            {
                DamageType damageType;
                if (!Enum.TryParse(entry.Key, true, out damageType))
                {
                    LogError($"Invalid damage type: {entry.Key}");
                    continue;
                }

                protectionProperties.Add(damageType, 1 - Mathf.Clamp(entry.Value, 0, 1));
            }

            return protectionProperties;
        }

        #endregion

        #region Drone Network Fixer

        // Fixes issue where fast moving drones temporarily disconnect and reconnect.
        // This issue occurs because the drone's network group and the client's secondary network group cannot be changed at the same time.
        private class DroneConnectionFixer : FacepunchBehaviour
        {
            public static void OnControlStarted(DroneSettings plugin, Drone drone, BasePlayer player)
            {
                var component = drone.GetOrAddComponent<DroneConnectionFixer>();
                component.SetRootEntity(plugin.GetDroneOrRootEntity(drone));
                component._viewers.Add(player);
            }

            public static void OnControlEnded(Drone drone, BasePlayer player)
            {
                var component = drone.GetComponent<DroneConnectionFixer>();
                if (component == null)
                    return;

                component.RemoveController(player);
            }

            public static void OnRootEntityChanged(Drone drone, BaseEntity rootEntity)
            {
                var component = drone.GetComponent<DroneConnectionFixer>();
                if (component == null)
                    return;

                component.SetRootEntity(rootEntity);
            }

            public static void RemoveFromDrone(Drone drone)
            {
                DestroyImmediate(drone.GetComponent<DroneConnectionFixer>());
            }

            private BaseEntity _rootEntity;
            private List<BasePlayer> _viewers = new List<BasePlayer>();
            private bool _isCallingCustomUpdateNetworkGroup;
            private Action _updateNetworkGroup;
            private Action _customUpdateNetworkGroup;

            private DroneConnectionFixer()
            {
                _customUpdateNetworkGroup = CustomUpdateNetworkGroup;
            }

            private void SetRootEntity(BaseEntity rootEntity)
            {
                _rootEntity = rootEntity;
                _updateNetworkGroup = _rootEntity.UpdateNetworkGroup;
            }

            private void RemoveController(BasePlayer player)
            {
                _viewers.Remove(player);
                if (_viewers.Count == 0)
                {
                    DestroyImmediate(this);
                }
            }

            // Using LateUpdate since that's the soonest we can learn about a pending Invoke.
            private void LateUpdate()
            {
                // Detect when UpdateNetworkGroup has been scheduled, in order to schedule a custom one in its place.
                if (_rootEntity.isCallingUpdateNetworkGroup && !_isCallingCustomUpdateNetworkGroup)
                {
                    _rootEntity.CancelInvoke(_updateNetworkGroup);
                    Invoke(_customUpdateNetworkGroup, 5);
                    _isCallingCustomUpdateNetworkGroup = true;
                }
            }

            private void SendFakeUpdateNetworkGroup(BaseEntity entity, BasePlayer player, uint groupId)
            {
                var write = Net.sv.StartWrite();
                write.PacketID(Message.Type.GroupChange);
                write.EntityID(entity.net.ID);
                write.GroupID(groupId);
                write.Send(new SendInfo(player.net.connection));
            }

            private void CustomUpdateNetworkGroup()
            {
                foreach (var player in _viewers)
                {
                    // Temporarily tell the client that the drone is in the global network group.
                    SendFakeUpdateNetworkGroup(_rootEntity, player, BaseNetworkable.GlobalNetworkGroup.ID);

                    // Update the client secondary network group to the one that the drone will change to.
                    player.net.SwitchSecondaryGroup(Net.sv.visibility.GetGroup(_rootEntity.transform.position));
                }

                // Update the drone's network group based on its current position.
                // This will update clients to be aware that the drone is now in the new network group.
                _rootEntity.UpdateNetworkGroup();
                _isCallingCustomUpdateNetworkGroup = false;
            }

            private void OnDestroy()
            {
                if (_rootEntity == null || _rootEntity.IsDestroyed)
                    return;

                if (_rootEntity.isCallingUpdateNetworkGroup && !_rootEntity.IsInvoking(_updateNetworkGroup))
                {
                    _rootEntity.Invoke(_updateNetworkGroup, 5);
                }
            }
        }

        #endregion

        #region Configuration

        [JsonObject(MemberSerialization.OptIn)]
        private class DroneProfile
        {
            [JsonProperty("PermissionSuffix", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string PermissionSuffix;

            [JsonProperty("DroneProperties")]
            public DroneProperties DroneProperties = new DroneProperties();

            [JsonProperty("DamageScale")]
            public Dictionary<string, float> DamageScale;

            [JsonIgnore]
            public ProtectionProperties ProtectionProperties;

            [JsonIgnore]
            public string Permission;

            public void Init(DroneSettings plugin, string droneType, bool requiresPermission)
            {
                if (requiresPermission && !string.IsNullOrWhiteSpace(PermissionSuffix))
                {
                    Permission = GetProfilePermission(droneType, PermissionSuffix);
                    plugin.permission.RegisterPermission(Permission, plugin);
                }

                if (DamageScale != null)
                {
                    ProtectionProperties = plugin.CreateProtectionProperties(DamageScale);
                }
            }

            public void ApplyToDrone(Drone drone)
            {
                if (ProtectionProperties != null)
                {
                    drone.baseProtection = ProtectionProperties;
                }

                DroneProperties?.ApplyToDrone(drone);
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class DroneProperties
        {
            public static DroneProperties FromDrone(Drone drone)
            {
                return new DroneProperties
                {
                    KillInWater = drone.killInWater,
                    MovementAcceleration = drone.movementAcceleration,
                    AltitudeAcceleration = drone.altitudeAcceleration,
                    LeanWeight = drone.leanWeight,
                };
            }

            [JsonProperty("KillInWater")]
            public bool KillInWater = true;

            [JsonProperty("DisableWhenHurtChance")]
            public float DisableWhenHurtChance = 25;

            [JsonProperty("MovementAcceleration")]
            public float MovementAcceleration = 10;

            [JsonProperty("AltitudeAcceleration")]
            public float AltitudeAcceleration = 10;

            [JsonProperty("LeanWeight")]
            public float LeanWeight = 0.025f;

            public void ApplyToDrone(Drone drone)
            {
                drone.killInWater = KillInWater;
                drone.disableWhenHurt = DisableWhenHurtChance > 0;
                drone.disableWhenHurtChance = DisableWhenHurtChance / 100f;
                drone.movementAcceleration = MovementAcceleration;
                drone.altitudeAcceleration = AltitudeAcceleration;
                drone.leanWeight = LeanWeight;
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class DroneTypeConfig
        {
            [JsonProperty("DefaultProfile")]
            public DroneProfile DefaultProfile = new DroneProfile();

            [JsonProperty("ProfilesRequiringPermission")]
            public DroneProfile[] ProfilesRequiringPermission = Array.Empty<DroneProfile>();

            public void Init(DroneSettings plugin, string droneType)
            {
                DefaultProfile.Init(plugin, droneType, requiresPermission: false);

                foreach (var profile in ProfilesRequiringPermission)
                {
                    profile.Init(plugin, droneType, requiresPermission: true);
                }
            }

            public DroneProfile GetProfileForOwner(DroneSettings plugin, ulong ownerId)
            {
                if (ownerId == 0 || (ProfilesRequiringPermission?.Length ?? 0) == 0)
                    return DefaultProfile;

                var ownerIdString = ownerId.ToString();
                for (var i = ProfilesRequiringPermission.Length - 1; i >= 0; i--)
                {
                    var profile = ProfilesRequiringPermission[i];
                    if (profile.Permission != null && plugin.permission.UserHasPermission(ownerIdString, profile.Permission))
                        return profile;
                }

                return DefaultProfile;
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class Configuration : BaseConfiguration
        {
            public override bool Migrate()
            {
                var changed = false;

                foreach (var entry in _droneTypeAliases)
                {
                    var oldName = entry.Key;
                    var newName = entry.Value;

                    DroneTypeConfig droneTypeConfig;
                    if (SettingsByDroneType.TryGetValue(oldName, out droneTypeConfig))
                    {
                        SettingsByDroneType[newName] = droneTypeConfig;
                        SettingsByDroneType.Remove(oldName);
                        changed = true;
                    }
                }

                return changed;
            }

            [JsonIgnore]
            private readonly Dictionary<string, string> _droneTypeAliases = new Dictionary<string, string>
            {
                ["RidableDrones"] = "DroneChair",
            };

            [JsonProperty("DroneTypePriority")]
            public string[] DroneTypePriority =
            {
                "DroneTurrets",
                "DroneChair",
                "DroneStorage",
            };

            [JsonProperty("SettingsByDroneType")]
            public Dictionary<string, DroneTypeConfig> SettingsByDroneType = new Dictionary<string, DroneTypeConfig>
            {
                [BaseDroneType] = new DroneTypeConfig
                {
                    DefaultProfile = new DroneProfile
                    {
                        DamageScale = new Dictionary<string, float>
                        {
                            [DamageType.Generic.ToString()] = 0.1f,
                            [DamageType.Heat.ToString()] = 0.2f,
                            [DamageType.Bullet.ToString()] = 0.2f,
                            [DamageType.AntiVehicle.ToString()] = 0.25f,
                        },
                    },
                    ProfilesRequiringPermission = new[]
                    {
                        new DroneProfile
                        {
                            PermissionSuffix = "god",
                            DroneProperties = new DroneProperties
                            {
                                KillInWater = false,
                                DisableWhenHurtChance = 0,
                                MovementAcceleration = 30,
                                AltitudeAcceleration = 20,
                                LeanWeight = 0,
                            },
                            DamageScale = new Dictionary<string, float>
                            {
                                [DamageType.AntiVehicle.ToString()] = 0,
                                [DamageType.Arrow.ToString()] = 0,
                                [DamageType.Bite.ToString()] = 0,
                                [DamageType.Bleeding.ToString()] = 0,
                                [DamageType.Blunt.ToString()] = 0,
                                [DamageType.Bullet.ToString()] = 0,
                                [DamageType.Cold.ToString()] = 0,
                                [DamageType.ColdExposure.ToString()] = 0,
                                [DamageType.Collision.ToString()] = 0,
                                [DamageType.Decay.ToString()] = 0,
                                [DamageType.Drowned.ToString()] = 0,
                                [DamageType.ElectricShock.ToString()] = 0,
                                [DamageType.Explosion.ToString()] = 0,
                                [DamageType.Fall.ToString()] = 0,
                                [DamageType.Fun_Water.ToString()] = 0,
                                [DamageType.Generic.ToString()] = 0,
                                [DamageType.Heat.ToString()] = 0,
                                [DamageType.Hunger.ToString()] = 0,
                                [DamageType.Poison.ToString()] = 0,
                                [DamageType.Radiation.ToString()] = 0,
                                [DamageType.RadiationExposure.ToString()] = 0,
                                [DamageType.Slash.ToString()] = 0,
                                [DamageType.Stab.ToString()] = 0,
                                [DamageType.Suicide.ToString()] = 0,
                                [DamageType.Thirst.ToString()] = 0,
                            },
                        },
                    },
                },
                ["DroneBoombox"] = new DroneTypeConfig
                {
                    DefaultProfile = new DroneProfile
                    {
                        DroneProperties = new DroneProperties
                        {
                            MovementAcceleration = 7.5f,
                            AltitudeAcceleration = 7.5f,
                        },
                        DamageScale = new Dictionary<string, float>
                        {
                            [DamageType.Generic.ToString()] = 0.1f,
                            [DamageType.Heat.ToString()] = 0.1f,
                            [DamageType.Bullet.ToString()] = 0.1f,
                            [DamageType.AntiVehicle.ToString()] = 0.1f,
                        },
                    },
                },
                ["DroneChair"] = new DroneTypeConfig
                {
                    DefaultProfile = new DroneProfile
                    {
                        DroneProperties = new DroneProperties
                        {
                            MovementAcceleration = 7.5f,
                            AltitudeAcceleration = 7.5f,
                        },
                        DamageScale = new Dictionary<string, float>
                        {
                            [DamageType.Generic.ToString()] = 0.1f,
                            [DamageType.Heat.ToString()] = 0.1f,
                            [DamageType.Bullet.ToString()] = 0.1f,
                            [DamageType.AntiVehicle.ToString()] = 0.1f,
                        },
                    },
                },
                ["DroneSign"] = new DroneTypeConfig
                {
                    DefaultProfile = new DroneProfile
                    {
                        DroneProperties = new DroneProperties
                        {
                            MovementAcceleration = 7.5f,
                            AltitudeAcceleration = 7.5f,
                        },
                        DamageScale = new Dictionary<string, float>
                        {
                            [DamageType.Generic.ToString()] = 0.1f,
                            [DamageType.Heat.ToString()] = 0.1f,
                            [DamageType.Bullet.ToString()] = 0.1f,
                            [DamageType.AntiVehicle.ToString()] = 0.1f,
                        },
                    },
                },
                ["DroneStorage"] = new DroneTypeConfig
                {
                    DefaultProfile = new DroneProfile
                    {
                        DroneProperties = new DroneProperties
                        {
                            MovementAcceleration = 7.5f,
                            AltitudeAcceleration = 7.5f,
                        },
                        DamageScale = new Dictionary<string, float>
                        {
                            [DamageType.Generic.ToString()] = 0.1f,
                            [DamageType.Heat.ToString()] = 0.1f,
                            [DamageType.Bullet.ToString()] = 0.1f,
                            [DamageType.AntiVehicle.ToString()] = 0.1f,
                        },
                    },
                },
                ["DroneTurrets"] = new DroneTypeConfig
                {
                    DefaultProfile = new DroneProfile
                    {
                        DroneProperties = new DroneProperties
                        {
                            MovementAcceleration = 5,
                            AltitudeAcceleration = 5,
                        },
                        DamageScale = new Dictionary<string, float>
                        {
                            [DamageType.Generic.ToString()] = 0.1f,
                            [DamageType.Heat.ToString()] = 0.1f,
                            [DamageType.Bullet.ToString()] = 0.1f,
                            [DamageType.AntiVehicle.ToString()] = 0.1f,
                            [DamageType.Explosion.ToString()] = 0.75f,
                            [DamageType.Blunt.ToString()] = 0.75f,
                        },
                    },
                },
                ["MegaDrones"] = new DroneTypeConfig
                {
                    DefaultProfile = new DroneProfile
                    {
                        DroneProperties = new DroneProperties
                        {
                            DisableWhenHurtChance = 0,
                            MovementAcceleration = 20,
                            AltitudeAcceleration = 20,
                            KillInWater = false,
                            LeanWeight = 0.1f,
                        },
                        DamageScale = new Dictionary<string, float>
                        {
                            [DamageType.Generic.ToString()] = 0.1f,
                            [DamageType.Heat.ToString()] = 0.05f,
                            [DamageType.Bullet.ToString()] = 0.05f,
                            [DamageType.AntiVehicle.ToString()] = 0.1f,
                            [DamageType.Explosion.ToString()] = 0.1f,
                            [DamageType.Blunt.ToString()] = 0.25f,
                        },
                    },
                },
            };

            public void Init(DroneSettings plugin)
            {
                foreach (var entry in SettingsByDroneType)
                {
                    entry.Value.Init(plugin, entry.Key);
                }
            }

            public DroneProfile FindProfile(DroneSettings plugin, string droneType, ulong ownerId)
            {
                string alias;
                if (_droneTypeAliases.TryGetValue(droneType, out alias))
                {
                    droneType = alias;
                }

                DroneTypeConfig droneTypeConfig;
                return SettingsByDroneType.TryGetValue(droneType, out droneTypeConfig)
                    ? droneTypeConfig.GetProfileForOwner(plugin, ownerId)
                    : null;
            }
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #endregion

        #region Configuration Helpers

        private class BaseConfiguration
        {
            public virtual bool Migrate() => false;

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
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw) | config.Migrate();
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
    }
}
