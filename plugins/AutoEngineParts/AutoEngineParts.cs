using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust.Modular;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Auto Engine Parts", "WhiteThunder", "1.0.0")]
    [Description("Ensures modular car engines always have engine parts which players cannot remove.")]
    internal class AutoEngineParts : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin EnginePartsDurability;

        private const string PermissionTier1 = "autoengineparts.tier1";
        private const string PermissionTier2 = "autoengineparts.tier2";
        private const string PermissionTier3 = "autoengineparts.tier3";

        private const float VanillaInternalDamageMultiplier = 0.5f;

        private Configuration _pluginConfig;

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionTier1, this);
            permission.RegisterPermission(PermissionTier2, this);
            permission.RegisterPermission(PermissionTier3, this);

            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
                MaybeUpdateEngineModule(entity as BaseVehicleModule, dropExistingParts: true);

            Subscribe(nameof(OnEntitySpawned));
        }

        private void Unload()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var car = entity as ModularCar;
                if (car == null)
                    continue;

                foreach (var module in car.AttachedModuleEntities)
                {
                    var engineStorage = GetEngineStorage(module);
                    if (engineStorage == null)
                        continue;

                    MaybeUpdateEngineStorage(engineStorage, 0, dropExistingParts: false);
                }
            }
        }

        private object OnVehicleModuleMove(VehicleModuleEngine module)
        {
            var engineStorage = GetEngineStorage(module);
            if (engineStorage == null)
                return null;

            if (!IsLocked(engineStorage))
                return null;

            // Return true to force the module to be moved even though it probably has items.
            // The items will be removed in the OnEntityKill hook.
            return true;
        }

        private void OnEntityKill(VehicleModuleEngine module)
        {
            var engineStorage = GetEngineStorage(module);
            if (engineStorage == null)
                return;

            if (!IsLocked(engineStorage))
                return;

            // Kill the inventory to remove all items.
            engineStorage.inventory.Kill();
        }

        private void OnEntitySpawned(VehicleModuleEngine module)
        {
            // Delaying is necessary since the storage container is created immediately after this hook is called.
            NextTick(() => MaybeUpdateEngineModule(module, dropExistingParts: false));
        }

        private object OnItemLock(Item item)
        {
            var car = item.parent?.entityOwner as ModularCar;
            if (car == null)
                return null;

            var engineStorage = GetEngineStorage(car.GetModuleForItem(item));
            if (engineStorage == null)
                return null;

            if (!IsLocked(engineStorage))
                return null;

            // Return true (standard) to cancel default behavior.
            // This prevents the checkered appearance and allows the item to be dropped.
            return true;
        }

        // This hook is exposed by Engine Parts Durability (EnginePartsDurability).
        private object OnEngineDamageMultiplierChange(EngineStorage engineStorage, float desiredMultiplier)
        {
            // The inventory being locked indicates that this plugin is likely controlling the multiplier.
            if (engineStorage.inventory.IsLocked())
                return false;

            return null;
        }

        // This hook is exposed by plugin: Claim Vehicle Ownership (ClaimVehicle).
        private void OnVehicleOwnershipChanged(ModularCar car)
        {
            var enginePartsTier = GetOwnerEnginePartsTier(car.OwnerID);

            foreach (var module in car.AttachedModuleEntities)
            {
                var engineStorage = GetEngineStorage(module);
                if (engineStorage != null)
                    MaybeUpdateEngineStorage(engineStorage, enginePartsTier, dropExistingParts: true);
            }
        }

        #endregion

        #region Helper Methods

        private static bool UpdateEngineStorageWasBlocked(EngineStorage engineStorage, int tier)
        {
            object hookResult = Interface.CallHook("OnEngineStorageFill", engineStorage, tier);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static ulong GetCarOwnerId(BaseVehicleModule module)
        {
            var car = module.Vehicle as ModularCar;
            return car == null ? 0 : car.OwnerID;
        }

        private static EngineStorage GetEngineStorage(BaseVehicleModule module)
        {
            var engineModule = module as VehicleModuleEngine;
            if (engineModule == null)
                return null;

            return engineModule.GetContainer() as EngineStorage;
        }

        private static bool IsLocked(EngineStorage engineStorage) =>
            engineStorage.inventory.IsLocked();

        private static void RemoveAllEngineParts(EngineStorage engineStorage)
        {
            for (var slot = 0; slot < engineStorage.inventory.capacity; slot++)
            {
                var item = engineStorage.inventory.GetSlot(slot);
                if (item != null)
                {
                    item.RemoveFromContainer();
                    item.Remove();
                }
            }
        }

        private static bool TryAddEngineItem(EngineStorage engineStorage, int slot, int tier)
        {
            ItemModEngineItem output;
            if (!engineStorage.allEngineItems.TryGetItem(tier, engineStorage.slotTypes[slot], out output))
                return false;

            var component = output.GetComponent<ItemDefinition>();
            var item = ItemManager.Create(component);
            if (item == null)
                return false;

            if (!item.MoveToContainer(engineStorage.inventory, slot, allowStack: false))
            {
                item.Remove();
                return false;
            }

            return true;
        }

        private static int Clamp(int x, int min, int max) => Math.Max(min, Math.Min(x, max));

        private void MaybeUpdateEngineModule(BaseVehicleModule module, bool dropExistingParts)
        {
            var engineStorage = GetEngineStorage(module);
            if (engineStorage == null)
                return;

            var enginePartsTier = GetOwnerEnginePartsTier(GetCarOwnerId(module));
            MaybeUpdateEngineStorage(engineStorage, enginePartsTier, dropExistingParts);
        }

        private void MaybeUpdateEngineStorage(EngineStorage engineStorage, int enginePartsTier, bool dropExistingParts)
        {
            if (!UpdateEngineStorageWasBlocked(engineStorage, enginePartsTier))
                UpdateEngineStorage(engineStorage, enginePartsTier, dropExistingParts);
        }

        private void UpdateEngineStorage(EngineStorage engineStorage, int desiredTier, bool dropExistingParts)
        {
            var inventory = engineStorage.inventory;
            var wasLocked = inventory.IsLocked();

            var hasEngineParts = !inventory.IsEmpty();

            // If the storage was already locked, we assume the parts were free so we can safely delete them.
            // This may be less efficient than keeping/repairing the parts if they match the desired tier, but this keeps it simple.
            if (hasEngineParts && wasLocked)
                RemoveAllEngineParts(engineStorage);

            if (desiredTier <= 0)
            {
                if (wasLocked)
                    inventory.SetLocked(false);

                ResetInternalDamageMultiplier(engineStorage);
                return;
            }

            if (hasEngineParts && !wasLocked)
            {
                // The existing engine parts must be removed to add new ones.
                if (dropExistingParts)
                {
                    // Drop existing engine parts because players may have worked hard for them.
                    engineStorage.DropItems();
                }
                else
                    RemoveAllEngineParts(engineStorage);
            }

            // This must be set first because adding items will trigger the OnItemLock hook which needs to see this flag.
            inventory.SetLocked(true);

            var inventoryFilled = true;
            for (var slot = 0; slot < inventory.capacity; slot++)
            {
                if (!TryAddEngineItem(engineStorage, slot, desiredTier))
                    inventoryFilled = false;
            }

            if (!inventoryFilled)
            {
                // Something went wrong when filling the inventory, so unlock it to allow parts to be added.
                inventory.SetLocked(false);
                return;
            }

            // Prevent engine parts from taking damage since the player has no way to repair them while the inventory is locked.
            engineStorage.internalDamageMultiplier = 0;
        }

        private void ResetInternalDamageMultiplier(EngineStorage engineStorage)
        {
            if (EnginePartsDurability != null)
                EnginePartsDurability.Call("API_RefreshMultiplier", engineStorage);
            else
                engineStorage.internalDamageMultiplier = VanillaInternalDamageMultiplier;
        }

        private int GetOwnerEnginePartsTier(ulong ownerId)
        {
            var defaultTier = _pluginConfig.DefaultEnginePartsTier;

            if (ownerId == 0)
                return defaultTier;

            var ownerIdString = ownerId.ToString();

            if (permission.UserHasPermission(ownerIdString, PermissionTier3))
                return 3;
            else if (permission.UserHasPermission(ownerIdString, PermissionTier2))
                return Math.Max(2, defaultTier);
            else if (permission.UserHasPermission(ownerIdString, PermissionTier1))
                return Math.Max(1, defaultTier);
            else
                return defaultTier;
        }

        #endregion

        #region Configuration

        private class Configuration : SerializableConfiguration
        {
            private int _defaultEnginePartsTier = 0;

            [JsonProperty("DefaultEnginePartsTier")]
            public int DefaultEnginePartsTier
            {
                get { return _defaultEnginePartsTier; }
                set { _defaultEnginePartsTier = Clamp(value, 0, 3); }
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
            catch
            {
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
