using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using Network;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Limited Drone Range", "WhiteThunder", "1.1.1")]
    [Description("Limits how far RC drones can be controlled from computer stations.")]
    internal class LimitedDroneRange : CovalencePlugin
    {
        #region Fields

        private readonly object False = false;
        private const float VanillaStaticDistanceFraction = 0.73f;
        private const int ForcedMaxRange = 100000;
        private const string DroneMaxControlRangeConVar = "drone.maxcontrolrange";
        private readonly string ForcedMaxRangeString = ForcedMaxRange.ToString();

        private Configuration _config;
        private UIManager _uiManager = new UIManager();

        #endregion

        #region Hooks

        private void Init()
        {
            _config.Init(this);
        }

        private void OnServerInitialized()
        {
            if (Drone.maxControlRange < ForcedMaxRange)
            {
                Drone.maxControlRange = ForcedMaxRange;
                LogWarning($"Updated {DroneMaxControlRangeConVar} ConVar to {ForcedMaxRange} so that the plugin can control drone max range.");
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
        }

        private void Unload()
        {
            RangeLimiter.DestroyAll();
        }

        private object OnBookmarkControl(ComputerStation station, BasePlayer player, string bookmarkName, Drone drone)
        {
            int maxRange;
            if (!ShouldLimitRange(drone, station, player, out maxRange)
                || IsWithinRange(drone, station, maxRange))
                return null;

            _uiManager.CreateOutOfRangeUI(this, player);
            return False;
        }

        private void OnBookmarkControlStarted(ComputerStation station, BasePlayer player, string bookmarkName, Drone drone)
        {
            int maxRange;
            if (!ShouldLimitRange(drone, station, player, out maxRange))
            {
                SendMaxRangeConVar(player, ForcedMaxRangeString);
                return;
            }

            RangeLimiter.AddToPlayer(this, player, station, drone, maxRange);
        }

        private void OnBookmarkControlEnded(ComputerStation station, BasePlayer player, Drone drone)
        {
            RangeLimiter.RemoveFromPlayer(player);
        }

        #endregion

        #region Helper Methods

        private static bool LimitRangeWasBlocked(Drone drone, ComputerStation station, BasePlayer player)
        {
            var hookResult = Interface.CallHook("OnDroneRangeLimit", drone, station, player);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool IsWithinRange(Drone drone, ComputerStation station, float range)
        {
            return (station.transform.position - drone.transform.position).sqrMagnitude < range * range;
        }

        private static void SendReplicatedVar(Connection connection, string fullName, string value)
        {
            var netWrite = Net.sv.StartWrite();
            netWrite.PacketID(Message.Type.ConsoleReplicatedVars);
            netWrite.Int32(1);
            netWrite.String(fullName);
            netWrite.String(value);
            netWrite.Send(new SendInfo(connection));
        }

        private static void SendMaxRangeConVar(BasePlayer player, string value)
        {
            SendReplicatedVar(player.Connection, DroneMaxControlRangeConVar, value);
        }

        private bool ShouldLimitRange(Drone drone, ComputerStation station, BasePlayer player, out int maxRange)
        {
            maxRange = _config.GetMaxRangeForPlayer(this, player.UserIDString);
            if (maxRange <= 0)
                return false;

            return !LimitRangeWasBlocked(drone, station, player);
        }

        #endregion

        private class RangeLimiter : FacepunchBehaviour
        {
            public static RangeLimiter AddToPlayer(LimitedDroneRange plugin, BasePlayer player, ComputerStation station, Drone drone, int maxDistance)
            {
                var component = player.gameObject.AddComponent<RangeLimiter>();

                component._plugin = plugin;
                component._player = player;
                component._station = station;
                component._stationTransform = station.transform;
                component._droneTransform = drone.transform;
                component._maxDistance = maxDistance;

                var secondsBetweenUpdates = plugin._config.UISettings.SecondsBetweenUpdates;

                component.InvokeRandomized(() =>
                {
                    plugin.TrackStart();
                    component.CheckRange();
                    plugin.TrackEnd();
                }, 0, secondsBetweenUpdates, secondsBetweenUpdates * 0.1f);

                // Show 25% of vanilla static.
                var staticFraction = Mathf.Lerp(VanillaStaticDistanceFraction, 1, 0.25f);
                SendMaxRangeConVar(player, Mathf.CeilToInt(maxDistance / staticFraction).ToString());

                return component;
            }

            public static void RemoveFromPlayer(BasePlayer player)
            {
                var component = player.GetComponent<RangeLimiter>();
                if (component != null)
                {
                    DestroyImmediate(component);
                }
            }

            public static void DestroyAll()
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    RemoveFromPlayer(player);
                }
            }

            private LimitedDroneRange _plugin;
            private BasePlayer _player;
            private ComputerStation _station;
            private Transform _stationTransform;
            private Transform _droneTransform;
            private int _maxDistance;
            private int _previousDisplayedDistance;

            private void CheckRange()
            {
                var sqrDistance = (_stationTransform.position - _droneTransform.position).sqrMagnitude;
                if (sqrDistance > _maxDistance * _maxDistance)
                {
                    _station.StopControl(_player);
                    _plugin._uiManager.CreateOutOfRangeUI(_plugin, _player);
                    return;
                }

                var distance = Mathf.CeilToInt(Mathf.Sqrt(sqrDistance));
                if (distance == _previousDisplayedDistance)
                    return;

                _plugin._uiManager.CreateDistanceUI(_plugin, _player, distance, _maxDistance);
                _previousDisplayedDistance = distance;
            }

            public void OnDestroy() => UIManager.Destroy(_player);
        }

        #region UI

        private class UIManager
        {
            private const string UIName = "LimitedDroneRange";

            private const string PlaceholderText = "__TEXT__";
            private const string PlaceholderColor = "__COLOR__";

            private string _cachedJson;

            public static void Destroy(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, UIName);
            }

            private string GetJsonWithPlaceholders(LimitedDroneRange plugin)
            {
                if (_cachedJson == null)
                {
                    var uiSettings = plugin._config.UISettings;

                    var cuiElements = new CuiElementContainer
                    {
                        new CuiElement
                        {
                            Parent = "Overlay",
                            Name = UIName,
                            DestroyUi = UIName,
                            Components =
                            {
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = uiSettings.AnchorMin,
                                    AnchorMax = uiSettings.AnchorMax,
                                    OffsetMin = uiSettings.OffsetMin,
                                    OffsetMax = uiSettings.OffsetMax,
                                }
                            },
                        },
                        new CuiElement
                        {
                            Parent = UIName,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = PlaceholderText,
                                    Align = TextAnchor.MiddleCenter,
                                    Color = PlaceholderColor,
                                    FontSize = uiSettings.TextSize,
                                },
                                new CuiOutlineComponent
                                {
                                    Color = "0 0 0 1",
                                    Distance = "0.75 0.75"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "0 0",
                                    OffsetMin = $"{uiSettings.TextSize * -4} 0",
                                    OffsetMax = $"{uiSettings.TextSize * 4} {uiSettings.TextSize * 1.5f}",
                                },
                            },
                        },
                    };

                    _cachedJson = CuiHelper.ToJson(cuiElements);
                }

                return _cachedJson;
            }

            public void CreateDistanceUI(LimitedDroneRange plugin, BasePlayer player, int distance, int maxDistance)
            {
                CreateLabel(
                    plugin,
                    player,
                    plugin.GetMessage(player.UserIDString, LangEntry.UIDistance, distance.ToString(), maxDistance.ToString()),
                    plugin._config.UISettings.GetDynamicColor(distance, maxDistance)
                );
            }

            public void CreateOutOfRangeUI(LimitedDroneRange plugin, BasePlayer player)
            {
                CreateLabel(
                    plugin,
                    player,
                    plugin.GetMessage(player.UserIDString, LangEntry.UIOutOfRange),
                    plugin._config.UISettings.OutOfRangeColor
                );

                plugin.timer.Once(1, () => Destroy(player));
            }

            private void CreateLabel(LimitedDroneRange plugin, BasePlayer player, string text, string color)
            {
                var json = GetJsonWithPlaceholders(plugin)
                    .Replace(PlaceholderText, text)
                    .Replace(PlaceholderColor, color);

                CuiHelper.AddUi(player, json);
            }
        }

        #endregion

        #region Configuration

        private class RangeProfile
        {
            [JsonProperty("PermissionSuffix")]
            public string PermissionSuffix;

            [JsonProperty("MaxRange")]
            public int MaxRange;

            [JsonIgnore]
            public string Permission;

            public void Init(LimitedDroneRange plugin)
            {
                if (string.IsNullOrWhiteSpace(PermissionSuffix))
                    return;

                Permission = $"{nameof(LimitedDroneRange)}.{PermissionSuffix}".ToLower();
                plugin.permission.RegisterPermission(Permission, plugin);
            }
        }

        private class ColorConfig
        {
            [JsonProperty("DistanceRemaining")]
            public int DistanceRemaining;

            [JsonProperty("Color")]
            public string Color;
        }

        private class UISettings
        {
            [JsonProperty("AnchorMin")]
            public string AnchorMin = "0.5 0";

            [JsonProperty("AnchorMax")]
            public string AnchorMax = "0.5 0";

            [JsonProperty("OffsetMin")]
            public string OffsetMin = "0 75";

            [JsonProperty("OffsetMax")]
            public string OffsetMax = "0 75";

            [JsonProperty("TextSize")]
            public int TextSize = 24;

            [JsonProperty("DefaultColor")]
            public string DefaultColor = "0.75 0.75 0.75 1";

            [JsonProperty("OutOfRangeColor")]
            public string OutOfRangeColor = "1 0.2 0.2 1";

            [JsonProperty("DynamicColors")]
            public ColorConfig[] DynamicColors =
            {
                new ColorConfig
                {
                    DistanceRemaining = 100,
                    Color = "1 0.5 0 1",
                },
                new ColorConfig
                {
                    DistanceRemaining = 50,
                    Color = "1 0.2 0.2 1",
                },
            };

            [JsonProperty("SecondsBetweenUpdates")]
            public float SecondsBetweenUpdates = 0.5f;

            public string GetDynamicColor(int distance, int maxDistance)
            {
                var distanceFromMax = maxDistance - distance;

                foreach (var colorConfig in DynamicColors)
                {
                    if (distanceFromMax <= colorConfig.DistanceRemaining)
                        return colorConfig.Color;
                }

                return DefaultColor;
            }

            public void Init()
            {
                var colorConfigs = DynamicColors.ToList();
                colorConfigs.Sort((config1, config2) => config1.DistanceRemaining.CompareTo(config2.DistanceRemaining));
                DynamicColors = colorConfigs.ToArray();
            }
        }

        private class Configuration : BaseConfiguration
        {
            [JsonProperty("DefaultMaxRange")]
            public int DefaultMaxRange = 500;

            [JsonProperty("ProfilesRequiringPermission")]
            public RangeProfile[] ProfilesRequiringPermission =
            {
                new RangeProfile
                {
                    PermissionSuffix = "short",
                    MaxRange = 250,
                },
                new RangeProfile
                {
                    PermissionSuffix = "medium",
                    MaxRange = 500,
                },
                new RangeProfile
                {
                    PermissionSuffix = "long",
                    MaxRange = 1000,
                },
                new RangeProfile
                {
                    PermissionSuffix = "unlimited",
                    MaxRange = 0,
                },
            };

            [JsonProperty("UISettings")]
            public UISettings UISettings = new UISettings();

            public void Init(LimitedDroneRange pluginInstance)
            {
                foreach (var profile in ProfilesRequiringPermission)
                    profile.Init(pluginInstance);

                UISettings.Init();
            }

            public int GetMaxRangeForPlayer(LimitedDroneRange plugin, string userId)
            {
                if (ProfilesRequiringPermission == null)
                    return DefaultMaxRange;

                for (var i = ProfilesRequiringPermission.Length - 1; i >= 0; i--)
                {
                    var profile = ProfilesRequiringPermission[i];
                    if (profile.Permission != null && plugin.permission.UserHasPermission(userId, profile.Permission))
                        return profile.MaxRange;
                }

                return DefaultMaxRange;
            }
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #endregion

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

                var changed = MaybeUpdateConfig(_config);

                if (_config.UISettings.AnchorMin == "0.5 0"
                    && _config.UISettings.AnchorMax == "0.5 0"
                    && _config.UISettings.OffsetMin == "0 75"
                    && _config.UISettings.OffsetMax == "0 75")
                {
                    _config.UISettings.OffsetMin = "0 47";
                    _config.UISettings.OffsetMax = "0 47";
                    changed = true;
                }

                if (changed)
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

        #region Localization

        private class LangEntry
        {
            public static readonly List<LangEntry> AllLangEntries = new List<LangEntry>();

            public static readonly LangEntry UIOutOfRange = new LangEntry("UI.OutOfRange", "{0}m / {1}m");
            public static readonly LangEntry UIDistance = new LangEntry("UI.Distance", "OUT OF RANGE");

            public string Name;
            public string English;

            public LangEntry(string name, string english)
            {
                Name = name;
                English = english;

                AllLangEntries.Add(this);
            }
        }

        private string GetMessage(string playerId, LangEntry langEntry) =>
            lang.GetMessage(langEntry.Name, this, playerId);

        private string GetMessage(string playerId, LangEntry langEntry, object arg1, object arg2) =>
            string.Format(GetMessage(playerId, langEntry), arg1, arg2);

        protected override void LoadDefaultMessages()
        {
            var englishLangKeys = new Dictionary<string, string>();

            foreach (var langEntry in LangEntry.AllLangEntries)
            {
                englishLangKeys[langEntry.Name] = langEntry.English;
            }

            lang.RegisterMessages(englishLangKeys, this, "en");
        }

        #endregion
    }
}
