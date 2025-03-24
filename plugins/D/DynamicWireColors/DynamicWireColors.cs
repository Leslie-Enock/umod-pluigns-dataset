using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static WireTool;
using static IOEntity;

namespace Oxide.Plugins
{
    [Info("Dynamic Wire Colors", "WhiteThunder", "1.1.2")]
    [Description("Temporarily changes the color of wires and hoses while they are providing insufficient power or fluid.")]
    internal class DynamicWireColors : CovalencePlugin
    {
        #region Fields

        private Configuration _config;

        private const string PermissionUse = "dynamicwirecolors.use";

        private WaitWhile WaitWhileSaving = new(() => SaveRestore.IsSaving);

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
        }

        private void OnServerInitialized()
        {
            SetupAllEntities(networkUpdate: true, fixDestinationColor: true);
        }

        private void Unload()
        {
            ResetAllEntities(networkUpdate: true);
        }

        private void OnOutputUpdate(IOEntity sourceEntity)
        {
            NextTick(() =>
            {
                if (sourceEntity == null)
                    return;

                ProcessSourceEntity(sourceEntity);
            });
        }

        private void OnServerSave()
        {
            ServerMgr.Instance.StartCoroutine(ResetColorsWhileSaving());
        }

        #endregion

        #region Exposed Hooks

        private bool ChangeColorWasBlocked(IOEntity ioEntity, IOSlot slot, WireColour color)
        {
            return Interface.CallHook("OnDynamicWireColorChange", ioEntity, slot, color) is false;
        }

        #endregion

        #region Helper Methods

        private IOSlot GetConnectedSourceSlot(IOSlot destinationSlot, out IOEntity sourceEntity)
        {
            sourceEntity = destinationSlot.connectedTo.Get();
            if (sourceEntity == null)
                return null;

            return sourceEntity.outputs[destinationSlot.connectedToSlot];
        }

        private IOSlot GetConnectedDestinationSlot(IOSlot sourceSlot, out IOEntity destinationEntity)
        {
            destinationEntity = sourceSlot.connectedTo.Get();
            if (destinationEntity == null)
                return null;

            return destinationEntity.inputs[sourceSlot.connectedToSlot];
        }

        private void CopySourceSlotColorsToDestinationSlots(IOEntity sourceEntity)
        {
            // This fixes an issue where loading a save does not restore the input slot colors.
            // This workaround updates the input slot colors to match the output colors when the plugin loads.
            // Without this workaround, we can't use the input colors to know which color to revert back to.
            foreach (var sourceSlot in sourceEntity.outputs)
            {
                var destinationSlot = GetConnectedDestinationSlot(sourceSlot, out _);
                if (destinationSlot == null)
                    continue;

                // Note: This intentionally does not check permissions or call a hook because:
                //   a) It shouldn't be necessary.
                //   b) It would require that other parts of the plugin do the same checks.
                // This can be changed if it turns out that some other plugin is using the destination slot color for special reasons.
                if (destinationSlot.wireColour == WireColour.Gray)
                {
                    destinationSlot.wireColour = sourceSlot.wireColour;
                }
            }
        }

        private bool EntityHasPermission(IOEntity ioEntity)
        {
            if (ioEntity.OwnerID == 0)
                return _config.AppliesToUnownedEntities;

            if (!_config.RequiresPermission)
                return true;

            return permission.UserHasPermission(ioEntity.OwnerID.ToString(), PermissionUse);
        }

        private bool EitherEntityHasPermission(IOEntity ioEntity1, IOEntity ioEntity2, string perm)
        {
            return EntityHasPermission(ioEntity1) || EntityHasPermission(ioEntity2);
        }

        private void ChangeSlotColor(IOEntity ioEntity, IOSlot slot, WireColour color, bool networkUpdate)
        {
            if (slot.wireColour == color || ChangeColorWasBlocked(ioEntity, slot, color))
                return;

            slot.wireColour = color;

            if (networkUpdate)
            {
                ioEntity.SendNetworkUpdate();
            }
        }

        private void SetupAllEntities(bool networkUpdate, bool fixDestinationColor)
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var ioEntity = entity as IOEntity;
                if (ioEntity == null)
                    continue;

                if (fixDestinationColor)
                {
                    CopySourceSlotColorsToDestinationSlots(ioEntity);
                }

                ProcessSourceEntity(ioEntity, networkUpdate);
            }
        }

        private void ResetAllEntities(bool networkUpdate)
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var destinationEntity = entity as IOEntity;
                if (destinationEntity == null)
                    continue;

                foreach (var destinationSlot in destinationEntity.inputs)
                {
                    var sourceSlot = GetConnectedSourceSlot(destinationSlot, out var sourceEntity);
                    if (sourceSlot == null)
                        continue;

                    ChangeSlotColor(sourceEntity, sourceSlot, destinationSlot.wireColour, networkUpdate);
                }
            }
        }

        private bool HasOtherMainInput(IOEntity ioEntity, IOSlot currentSlot)
        {
            foreach (var slot in ioEntity.inputs)
            {
                if (slot.type == currentSlot.type && slot.mainPowerSlot && slot != currentSlot)
                    return true;
            }

            return false;
        }

        private bool SufficientPowerOrFluid(IOEntity destinationEntity, IOSlot destinationSlot, int inputAmount)
        {
            if (inputAmount == 0)
                return false;

            // Only electrical entities have the concept of "sufficient" power (could be wrong).
            if (destinationSlot.type != IOType.Electric)
                return true;

            if (inputAmount >= destinationEntity.ConsumptionAmount())
                return true;

            // If not providing sufficient power, only change color if there are no other main power inputs.
            // This avoids dynamically coloring a toggle input, for example, unless that input is providing exactly 0.
            return HasOtherMainInput(destinationEntity, destinationSlot);
        }

        private void ProcessSourceEntity(IOEntity sourceEntity, bool networkUpdate = true)
        {
            foreach (var sourceSlot in sourceEntity.outputs)
            {
                if (sourceSlot.type != IOType.Electric && sourceSlot.type != IOType.Fluidic)
                    continue;

                var destinationSlot = GetConnectedDestinationSlot(sourceSlot, out var destinationEntity);
                if (destinationSlot == null)
                    continue;

                if (!EitherEntityHasPermission(sourceEntity, destinationEntity, PermissionUse))
                    continue;

                var inputAmount = sourceEntity.GetPassthroughAmount(destinationSlot.connectedToSlot);

                if (SufficientPowerOrFluid(destinationEntity, destinationSlot, inputAmount))
                {
                    // Don't check for permission here since we want to be able to reset the color even if the player lost permission.
                    ChangeSlotColor(sourceEntity, sourceSlot, destinationSlot.wireColour, networkUpdate);
                }
                else if (EitherEntityHasPermission(sourceEntity, destinationEntity, PermissionUse))
                {
                    ChangeSlotColor(sourceEntity, sourceSlot, _config.GetInsufficientColorForType(sourceSlot.type), networkUpdate);
                }
            }
        }

        private IEnumerator ResetColorsWhileSaving()
        {
            // Reset colors so they save without modification, in case an ungraceful shutdown does not invoke Unload() before saving.
            TrackStart();
            ResetAllEntities(networkUpdate: false);
            TrackEnd();

            yield return WaitWhileSaving;

            // Restore dynamic colors, as if the plugin had just loaded.
            TrackStart();
            SetupAllEntities(networkUpdate: false, fixDestinationColor: false);
            TrackEnd();
        }

        #endregion

        #region Configuration

        private class Configuration : BaseConfiguration
        {
            [JsonProperty("InsufficientPowerColor")]
            [JsonConverter(typeof(StringEnumConverter))]
            public WireColour InsufficientPowerColor = WireColour.Red;

            [JsonProperty("InsufficientFluidColor")]
            [JsonConverter(typeof(StringEnumConverter))]
            public WireColour InsufficientFluidColor = WireColour.Red;

            [JsonProperty("RequiresPermission")]
            public bool RequiresPermission = true;

            [JsonProperty("AppliesToUnownedEntities")]
            public bool AppliesToUnownedEntities = false;

            public WireColour GetInsufficientColorForType(IOType ioType)
            {
                return ioType == IOType.Fluidic
                    ? InsufficientFluidColor
                    : InsufficientPowerColor;
            }
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
