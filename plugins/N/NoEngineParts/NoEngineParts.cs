using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using Rust.Modular;

namespace Oxide.Plugins
{
    [Info("No Engine Parts", "WhiteThunder", "1.0.1")]
    [Description("Allows modular cars to be driven without engine parts.")]
    internal class NoEngineParts : CovalencePlugin
    {
        #region Fields

        private const string PermissionPresetPrefix = "noengineparts.preset";

        private Configuration pluginConfig;

        #endregion

        #region Hooks

        private void Init()
        {
            foreach (var preset in pluginConfig.presetsRequiringPermission)
                if (!string.IsNullOrWhiteSpace(preset.name))
                    permission.RegisterPermission(GetPresetPermission(preset.name), this);
        }

        private void OnEntityMounted(ModularCarSeat seat)
        {
            var car = seat.associatedSeatingModule?.Vehicle as ModularCar;
            if (car == null)
                return;

            // Only refresh engine loadout if engine cannot be started, else handle in OnEngineStarted
            if (car.HasDriver() && !car.HasAnyWorkingEngines())
                RefreshCarEngineLoadouts(car);
        }

        private void OnEngineStarted(ModularCar car) =>
            RefreshCarEngineLoadouts(car);

        private object OnEngineLoadoutRefresh(EngineStorage engineStorage)
        {
            var enginePreset = DetermineEnginePresetForStorage(engineStorage);
            if (enginePreset == null)
                return null;

            if (TryRefreshEngineLoadout(engineStorage, enginePreset))
                return false;

            return null;
        }

        #endregion

        #region Helper Methods

        private bool OverrideLoadoutWasBlocked(EngineStorage engineStorage)
        {
            object hookResult = Interface.CallHook("OnEngineLoadoutOverride", engineStorage);
            return hookResult is bool && (bool)hookResult == false;
        }

        private string GetPresetPermission(string presetName) => $"{PermissionPresetPrefix}.{presetName}";

        private void RefreshCarEngineLoadouts(ModularCar car)
        {
            var enginePreset = DetermineEnginePresetForOwner(car.OwnerID);
            if (enginePreset == null)
                return;

            foreach (var module in car.AttachedModuleEntities)
            {
                var engineModule = module as VehicleModuleEngine;
                if (engineModule == null)
                    continue;

                var engineStorage = engineModule.GetContainer() as EngineStorage;
                if (engineStorage == null)
                    continue;

                TryRefreshEngineLoadout(engineStorage, enginePreset);
            }
        }

        private bool TryRefreshEngineLoadout(EngineStorage engineStorage, EnginePreset enginePreset)
        {
            if (OverrideLoadoutWasBlocked(engineStorage))
                return false;

            var acceleration = 0f;
            var topSpeed = 0f;
            var fuelEconomy = 0f;

            for (var slot = 0; slot < engineStorage.inventory.capacity; slot++)
            {
                var engineItemType = engineStorage.slotTypes[slot];

                var item = engineStorage.inventory.GetSlot(slot);
                var itemValue = 0f;
                if (item != null && !item.isBroken)
                {
                    var component = item.info.GetComponent<ItemModEngineItem>();
                    if (component != null)
                        itemValue = item.amount * engineStorage.GetTierValue(component.tier);
                }

                if (engineItemType.BoostsAcceleration())
                    acceleration += Math.Max(itemValue, enginePreset.acceleration);

                if (engineItemType.BoostsFuelEconomy())
                    fuelEconomy += Math.Max(itemValue, enginePreset.fuelEconomy);

                if (engineItemType.BoostsTopSpeed())
                    topSpeed += Math.Max(itemValue, enginePreset.topSpeed);
            }

            engineStorage.isUsable = acceleration > 0 && topSpeed > 0 && fuelEconomy > 0;
            engineStorage.accelerationBoostPercent = acceleration / engineStorage.accelerationBoostSlots;
            engineStorage.fuelEconomyBoostPercent = fuelEconomy / engineStorage.fuelEconomyBoostSlots;
            engineStorage.topSpeedBoostPercent = topSpeed / engineStorage.topSpeedBoostSlots;
            engineStorage.SendNetworkUpdate();
            engineStorage.GetEngineModule()?.RefreshPerformanceStats(engineStorage);

            return true;
        }

        #endregion

        #region Configuration

        private EnginePreset DetermineEnginePresetForStorage(EngineStorage engineStorage)
        {
            var car = engineStorage.GetEngineModule()?.Vehicle as ModularCar;
            if (car == null)
                return pluginConfig.defaultPreset;

            return DetermineEnginePresetForOwner(car.OwnerID);
        }

        private EnginePreset DetermineEnginePresetForOwner(ulong ownerId)
        {
            if (ownerId == 0 || pluginConfig.presetsRequiringPermission == null)
                return pluginConfig.defaultPreset;

            var ownerIdString = ownerId.ToString();
            for (var i = pluginConfig.presetsRequiringPermission.Length - 1; i >= 0; i--)
            {
                var preset = pluginConfig.presetsRequiringPermission[i];
                if (!string.IsNullOrWhiteSpace(preset.name) &&
                    permission.UserHasPermission(ownerIdString, GetPresetPermission(preset.name)))
                {
                    return preset;
                }
            }

            return pluginConfig.defaultPreset;
        }

        private Configuration GetDefaultConfig() => new Configuration();

        internal class Configuration : SerializableConfiguration
        {
            [JsonProperty("DefaultPreset")]
            public EnginePreset defaultPreset = new EnginePreset()
            {
                acceleration = 0.3f,
                topSpeed = 0.3f,
                fuelEconomy = 0.3f,
            };

            [JsonProperty("PresetsRequiringPermission")]
            public EnginePreset[] presetsRequiringPermission = new EnginePreset[]
            {
                new EnginePreset ()
                {
                    name = "tier1",
                    acceleration = 0.6f,
                    topSpeed = 0.6f,
                    fuelEconomy = 0.6f,
                },
                new EnginePreset ()
                {
                    name = "tier2",
                    acceleration = 0.8f,
                    topSpeed = 0.8f,
                    fuelEconomy = 0.8f,
                },
                new EnginePreset ()
                {
                    name = "tier3",
                    acceleration = 1,
                    topSpeed = 1,
                    fuelEconomy = 1,
                },
                new EnginePreset ()
                {
                    name = "tier4",
                    acceleration = 2,
                    topSpeed = 2,
                    fuelEconomy = 2,
                },
                new EnginePreset()
                {
                    name = "tier5",
                    acceleration = 3,
                    topSpeed = 3,
                    fuelEconomy = 3,
                },
                new EnginePreset()
                {
                    name = "tier6",
                    acceleration = 4,
                    topSpeed = 4,
                    fuelEconomy = 4,
                },
            };
        }

        internal class EnginePreset
        {
            [JsonProperty("Name", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string name;

            [JsonProperty("Acceleration")]
            public float acceleration;

            [JsonProperty("TopSpeed")]
            public float topSpeed;

            [JsonProperty("FuelEconomy")]
            public float fuelEconomy;
        }

        #endregion

        #region Configuration Boilerplate

        internal class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        internal static class JsonHelper
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

        protected override void LoadDefaultConfig() => pluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                pluginConfig = Config.ReadObject<Configuration>();
                if (pluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(pluginConfig))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(pluginConfig, true);
        }

        #endregion
    }
}
