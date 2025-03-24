using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Boombox Durability", "WhiteThunder", "2.0.1")]
    [Description("Allows configuring deployable boomboxes to decay while playing.")]
    internal class BoomboxDurability : CovalencePlugin
    {
        #region Fields

        private const string PermissionProfilePrefix = "boomboxdurability";
        private const float VanillaConditionLossRate = 0;

        private Configuration _pluginConfig;

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginConfig.Init(this);

            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var boomBox = entity as DeployableBoomBox;
                if (boomBox == null)
                    continue;

                OnEntitySpawned(boomBox);
            }

            Subscribe(nameof(OnEntitySpawned));
        }

        private void OnEntitySpawned(DeployableBoomBox boomBox)
        {
            if (boomBox.IsStatic)
                return;

            boomBox.BoxController.ConditionLossRate = GetPlayerDecayRate(boomBox.OwnerID);
        }

        private void Unload()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var boomBox = entity as DeployableBoomBox;
                if (boomBox == null || boomBox.IsStatic)
                    continue;

                boomBox.BoxController.ConditionLossRate = VanillaConditionLossRate;
            }
        }

        #endregion

        #region Configuration

        public float GetPlayerDecayRate(ulong userId)
        {
            if (userId == 0 || (_pluginConfig.ProfilesRequiringPermission?.Length ?? 0) == 0)
                return _pluginConfig.DefaultDecayRate;

            var userIdString = userId.ToString();

            for (var i = _pluginConfig.ProfilesRequiringPermission.Length - 1; i >= 0; i--)
            {
                var profile = _pluginConfig.ProfilesRequiringPermission[i];
                if (profile.Permission != null && permission.UserHasPermission(userIdString, profile.Permission))
                    return profile.DecayRate;
            }

            return _pluginConfig.DefaultDecayRate;
        }

        private class DurabilityProfile
        {
            [JsonProperty("PermissionSuffix")]
            public string PermissionSuffix;

            [JsonProperty("DecayRate")]
            public float DecayRate;

            [JsonIgnore]
            public string Permission;
        }

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("DefaultDecayRate")]
            public float DefaultDecayRate = 0.025f;

            [JsonProperty("ProfilesRequiringPermission")]
            public DurabilityProfile[] ProfilesRequiringPermission = new DurabilityProfile[]
            {
                new DurabilityProfile()
                {
                    PermissionSuffix = "fastdecay",
                    DecayRate = 0.111f,
                },
                new DurabilityProfile()
                {
                    PermissionSuffix = "slowdecay",
                    DecayRate = 0.007f,
                },
                new DurabilityProfile()
                {
                    PermissionSuffix = "nodecay",
                    DecayRate = 0,
                },
            };

            public void Init(BoomboxDurability pluginInstance)
            {
                if (ProfilesRequiringPermission == null)
                    return;

                foreach (var profile in ProfilesRequiringPermission)
                {
                    if (!string.IsNullOrEmpty(profile.PermissionSuffix))
                    {
                        profile.Permission = $"{PermissionProfilePrefix}.{profile.PermissionSuffix}";
                        pluginInstance.permission.RegisterPermission(profile.Permission, pluginInstance);
                    }
                }
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
