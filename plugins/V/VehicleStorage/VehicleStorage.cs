using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Vehicle Storage", "WhiteThunder", "3.5.1")]
    [Description("Allows adding storage containers to vehicles and increasing built-in storage capacity.")]
    internal class VehicleStorage : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin CargoTrainEvent;

        private Configuration _config;

        private const string BasePermissionPrefix = "vehiclestorage";

        private const string RhibStoragePrefab = "assets/content/vehicles/boats/rhib/subents/rhib_storage.prefab";
        private const string HabStoragePrefab = "assets/prefabs/deployable/hot air balloon/subents/hab_storage.prefab";

        private const string StashDeployEffectPrefab = "assets/prefabs/deployable/small stash/effects/small-stash-deploy.prefab";
        private const string BoxDeployEffectPrefab = "assets/prefabs/deployable/woodenbox/effects/wooden-box-deploy.prefab";

        private const string ResizableLootPanelName = "generic_resizable";
        private const int MaxCapacity = 48;

        private readonly VehicleTracker _vehicleTracker = new VehicleTracker();
        private readonly ReskinEventManager _reskinEventManager = new ReskinEventManager();
        private readonly ContainerBoundsManager _containerBoundsManager = new ContainerBoundsManager();

        #endregion

        #region Hooks

        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            _config.Init(this);
            _containerBoundsManager.OnServerInitialized(this);

            foreach (var networkable in BaseNetworkable.serverEntities)
            {
                var baseEntity = networkable as BaseEntity;
                if (baseEntity == null)
                    continue;

                OnEntitySpawned(baseEntity);
            }

            Subscribe(nameof(OnEntitySpawned));
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            var vehicleConfig = _config.GetVehicleConfig(entity);
            if (vehicleConfig == null)
                return;

            _vehicleTracker.AddVehicle(vehicleConfig, entity);

            // Wait 2 ticks to give Vehicle Vendor Options an opportunity to set ownership.
            NextTick(() => NextTick(() =>
            {
                if (entity == null)
                    return;

                RefreshVehicleStorage(entity, vehicleConfig);
            }));
        }

        private void OnEntityKill(BaseEntity entity)
        {
            var vehicleConfig = _config.GetVehicleConfig(entity);
            if (vehicleConfig == null || vehicleConfig.ContainerPresets == null)
                return;

            if (!_vehicleTracker.RemoveVehicle(vehicleConfig, entity))
                return;

            foreach (var child in entity.children)
            {
                var container = child as StorageContainer;
                if (container == null)
                    continue;

                if (!vehicleConfig.ContainerPresets.ContainsKey(container.name))
                    continue;

                container.DropItems();
            }
        }

        private void OnUserPermissionGranted(string userId, string perm)
        {
            if (perm.StartsWith(BasePermissionPrefix))
            {
                HandlePermissionChanged(userId);
            }
        }

        private void OnGroupPermissionGranted(string group, string perm)
        {
            if (perm.StartsWith(BasePermissionPrefix))
            {
                HandlePermissionChanged();
            }
        }

        private void OnUserGroupAdded(string userId, string groupName)
        {
            var permList = permission.GetGroupPermissions(groupName, parents: true);
            foreach (var perm in permList)
            {
                if (perm.StartsWith(BasePermissionPrefix))
                {
                    HandlePermissionChanged(userId);
                    return;
                }
            }
        }

        private void OnRidableAnimalClaimed(RidableHorse horse, BasePlayer player)
        {
            RefreshVehicleStorage(horse);
        }

        private void OnEntityReskin(Snowmobile snowmobile, ItemSkinDirectory.Skin skin, BasePlayer player)
        {
            var vehicleConfig = _config.GetVehicleConfig(snowmobile);
            if (vehicleConfig?.ContainerPresets == null)
                return;

            var reskinEvent = _reskinEventManager.GetEvent()
                .WithParent(snowmobile)
                .WithVehicleConfig(vehicleConfig);

            for (var i = snowmobile.children.Count - 1; i >= 0; i--)
            {
                var container = snowmobile.children[i] as StorageContainer;
                if (container == null || container.IsDestroyed)
                    continue;

                if (!vehicleConfig.ContainerPresets.ContainsKey(container.name))
                    continue;

                reskinEvent.AddContainer(container);

                // Unparent the container to prevent it from being destroyed.
                // It will later be parented to the newly spawned entity.
                container.SetParent(null);
            }

            if (reskinEvent.Containers.Count == 0)
            {
                _reskinEventManager.CancelEvent(reskinEvent);
            }
            else
            {
                _reskinEventManager.RecordEvent(reskinEvent);

                // In case another plugin blocks the pre-hook, reparent or kill the containers.
                NextTick(_reskinEventManager.CleanupAction);
            }
        }

        private void OnEntityReskinned(Snowmobile snowmobile, ItemSkinDirectory.Skin skin, BasePlayer player)
        {
            var transform = snowmobile.transform;
            var reskinEvent = _reskinEventManager.FindEvent(transform.position);
            if (reskinEvent == null)
                return;

            var newVehicleConfig = _config.GetVehicleConfig(snowmobile);
            if (newVehicleConfig?.ContainerPresets == null)
            {
                // New vehicle has no container presets, so kill the containers.
                foreach (var container in reskinEvent.Containers)
                {
                    container.DropItems();
                    container.Kill();
                }

                return;
            }

            foreach (var container in reskinEvent.Containers)
            {
                var containerName = container.name;
                var newContainerPreset = newVehicleConfig.FindContainerPreset(containerName);
                if (newContainerPreset == null || container.PrefabName != newContainerPreset.Prefab)
                {
                    container.DropItems();
                    container.Kill();
                    continue;
                }

                newContainerPreset.MoveContainerToParent(container, snowmobile);
            }

            _reskinEventManager.CompleteEvent(reskinEvent);
        }

        // Compatibility with plugin: Claim Vehicle Ownership
        private void OnVehicleOwnershipChanged(BaseEntity vehicle) => RefreshVehicleStorage(vehicle);

        #endregion

        #region Dependencies

        private bool IsCargoTrain(BaseEntity entity)
        {
            var workcart = entity as TrainEngine;
            if (workcart == null)
                return false;

            var result = CargoTrainEvent?.Call("IsTrainSpecial", workcart.net.ID);
            return result is bool && (bool)result;
        }

        #endregion

        #region API

        private void API_RefreshVehicleStorage(BaseEntity vehicle)
        {
            RefreshVehicleStorage(vehicle);
        }

        #endregion

        #region Exposed Hooks

        private bool AlterStorageWasBlocked(BaseEntity vehicle)
        {
            var hookResult = Interface.CallHook("OnVehicleStorageUpdate", vehicle);
            if (hookResult is bool && (bool)hookResult == false)
                return true;

            if (IsCargoTrain(vehicle))
                return true;

            return false;
        }

        private bool SpawnStorageWasBlocked(BaseEntity vehicle)
        {
            var hookResult = Interface.CallHook("OnVehicleStorageSpawn", vehicle);
            return hookResult is bool && (bool)hookResult == false;
        }

        private void CallHookVehicleStorageSpawned(BaseEntity vehicle, StorageContainer container)
        {
            Interface.CallHook("OnVehicleStorageSpawned", vehicle, container);
        }

        #endregion

        #region Helper Methods

        private void RemoveProblemComponents(BaseEntity entity)
        {
            foreach (var meshCollider in entity.GetComponentsInChildren<MeshCollider>())
            {
                UnityEngine.Object.DestroyImmediate(meshCollider);
            }

            UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());
        }

        private void SetupStorage(BaseEntity vehicle, StorageContainer container, ContainerPreset preset)
        {
            if (container.IsFullySpawned())
            {
                MoveOverlappingDismountPositions(vehicle, container);
            }

            container.name = preset.Name;
            container.pickup.enabled = false;
            container.dropsLoot = true;
            RemoveProblemComponents(container);
        }

        private void MoveOverlappingDismountPositions(BaseEntity vehicle, StorageContainer container)
        {
            var vehicleEntity = vehicle as BaseVehicle;
            if (vehicleEntity == null)
                return;

            var containerTransform = container.transform;
            var containerLocalPosition = containerTransform.localPosition;
            var containerBounds = _containerBoundsManager.GetBounds(container);
            var containerWorldBounds = new OBB(containerTransform.position, containerTransform.lossyScale, containerTransform.rotation, containerBounds);

            foreach (var dismountTransform in vehicleEntity.dismountPositions)
            {
                if (containerWorldBounds.Contains(dismountTransform.position))
                {
                    var newDismountLocalPosition = dismountTransform.localPosition;
                    newDismountLocalPosition.y = containerLocalPosition.y + containerBounds.center.y + containerBounds.extents.y + 0.4f;
                    dismountTransform.localPosition = newDismountLocalPosition;
                }
            }
        }

        private StorageContainer SpawnStorage(BaseEntity vehicle, ContainerPreset preset, int capacity)
        {
            if (SpawnStorageWasBlocked(vehicle))
                return null;

            Vector3 position;
            Quaternion rotation;

            var parentAfterSpawn = vehicle is HotAirBalloon && preset.Prefab == HabStoragePrefab;
            if (parentAfterSpawn)
            {
                var vehicleTransform = vehicle.transform;
                position = vehicleTransform.TransformPoint(preset.Position);
                rotation = vehicleTransform.rotation * preset.Rotation;
            }
            else
            {
                position = preset.Position;
                rotation = preset.Rotation;
            }

            var createdEntity = GameManager.server.CreateEntity(preset.Prefab, position, rotation);
            if (createdEntity == null)
                return null;

            var container = createdEntity as StorageContainer;
            if (container == null)
            {
                UnityEngine.Object.Destroy(createdEntity);
                return null;
            }

            container.name = preset.Name;
            SetupStorage(vehicle, container, preset);
            if (parentAfterSpawn)
            {
                container.Spawn();
                container.SetParent(vehicle, preset.ParentBone, worldPositionStays: true, sendImmediate: true);
            }
            else
            {
                container.SetParent(vehicle, preset.ParentBone);
                container.Spawn();
            }

            MoveOverlappingDismountPositions(vehicle, container);
            MaybeIncreaseCapacity(container, capacity);
            CallHookVehicleStorageSpawned(vehicle, container);

            var deployEffect = preset.Prefab == RhibStoragePrefab
                ? BoxDeployEffectPrefab
                : preset.Prefab == HabStoragePrefab
                ? StashDeployEffectPrefab
                : null;

            if (deployEffect != null)
            {
                Effect.server.Run(deployEffect, container.transform.position);
            }

            return container;
        }

        private StorageContainer FindStorageContainerForPreset(BaseEntity entity, ContainerPreset preset)
        {
            foreach (var child in entity.children)
            {
                var container = child as StorageContainer;
                if (container == null)
                    continue;

                if (container.PrefabName == preset.Prefab
                    && (container.transform.localPosition == preset.Position || container.name == preset.Name))
                    return container;
            }

            return null;
        }

        private void MaybeIncreaseCapacity(StorageContainer container, int capacity)
        {
            container.panelName = ResizableLootPanelName;

            // Don't decrease capacity, in case there are items in those slots.
            // It's possible to handle that better, but not a priority as of writing this.
            if (capacity != -1 && container.inventory.capacity < capacity)
            {
                container.inventory.capacity = capacity;
            }
        }

        private void AddOrUpdateExtraContainers(BaseEntity vehicle, VehicleProfile vehicleProfile)
        {
            foreach (var entry in vehicleProfile.ValidAdditionalStorage)
            {
                var containerPreset = entry.Key;
                var capacity = entry.Value;

                var container = FindStorageContainerForPreset(vehicle, containerPreset);
                if (container == null)
                {
                    SpawnStorage(vehicle, containerPreset, capacity);
                    continue;
                }

                SetupStorage(vehicle, container, containerPreset);
                MaybeIncreaseCapacity(container, capacity);
                var transform = container.transform;
                if (transform.localPosition != containerPreset.Position || transform.localRotation != containerPreset.Rotation)
                {
                    transform.localPosition = containerPreset.Position;
                    transform.localRotation = containerPreset.Rotation;
                    container.InvalidateNetworkCache();
                    container.SendNetworkUpdate_Position();
                }
            }
        }

        private void RefreshVehicleStorage(BaseEntity vehicle, VehicleConfig vehicleConfig)
        {
            var vehicleProfile = vehicleConfig.GetProfileForVehicle(permission, vehicle);
            if (vehicleProfile == null)
            {
                // Probably not a supported vehicle.
                return;
            }

            if (AlterStorageWasBlocked(vehicle))
            {
                // Another plugin blocked altering storage.
                return;
            }

            var defaultContainer = vehicleConfig.GetDefaultContainer(vehicle);
            if (defaultContainer != null)
            {
                MaybeIncreaseCapacity(defaultContainer, vehicleProfile.BuiltInStorageCapacity);
            }

            if (vehicleProfile.ValidAdditionalStorage != null)
            {
                AddOrUpdateExtraContainers(vehicle, vehicleProfile);
            }
        }

        private void RefreshVehicleStorage(BaseEntity vehicle)
        {
            if (vehicle is ModularCar)
            {
                foreach (var child in vehicle.children)
                {
                    var module = child as BaseVehicleModule;
                    if (module != null)
                    {
                        RefreshVehicleStorage(module);
                    }
                }
                return;
            }

            var vehicleConfig = _config.GetVehicleConfig(vehicle);
            if (vehicleConfig == null)
                return;

            RefreshVehicleStorage(vehicle, vehicleConfig);
        }

        private void HandlePermissionChanged(string userIdString = "")
        {
            foreach (var entry in _vehicleTracker.AllSupportedVehicles)
            {
                var vehicleConfig = entry.Key;
                var vehicleList = entry.Value;

                foreach (var vehicle in vehicleList)
                {
                    var ownerId = vehicleConfig.GetOwnerId(vehicle);
                    if (ownerId == 0)
                    {
                        // Unowned vehicles cannot be affected by permissions so there's nothing to do.
                        continue;
                    }

                    if (userIdString != string.Empty && userIdString != vehicle.OwnerID.ToString())
                    {
                        // Permissions changed for a specific player, but they don't own the vehicle, so nothing to do.
                        continue;
                    }

                    RefreshVehicleStorage(vehicle, vehicleConfig);
                }
            }
        }

        #endregion

        #region Container Bounds Manager

        private class ContainerBoundsManager
        {
            private readonly Dictionary<string, string> _boundsReplacements = new Dictionary<string, string>
            {
                // The RHIB storage bounds are small, so use the normal wooden box bounds.
                [RhibStoragePrefab] = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab",
            };

            private readonly Dictionary<uint, Bounds> _boundsReplacementsByPrefabId = new Dictionary<uint, Bounds>();

            public void OnServerInitialized(VehicleStorage plugin)
            {
                foreach (var entry in _boundsReplacements)
                {
                    var destinationEntity = GameManager.server.FindPrefab(entry.Key)?.GetComponent<BaseEntity>();
                    if (destinationEntity == null)
                    {
                        plugin.LogWarning($"Prefab not found: {entry.Key}");
                        continue;
                    }

                    var sourceEntity = GameManager.server.FindPrefab(entry.Value)?.GetComponent<BaseEntity>();
                    if (sourceEntity == null)
                    {
                        plugin.LogWarning($"Prefab not found: {entry.Value}");
                        continue;
                    }

                    _boundsReplacementsByPrefabId[destinationEntity.prefabID] = sourceEntity.bounds;
                }
            }

            public Bounds GetBounds(BaseEntity entity)
            {
                Bounds bounds;
                return _boundsReplacementsByPrefabId.TryGetValue(entity.prefabID, out bounds)
                    ? bounds
                    : entity.bounds;
            }
        }

        #endregion

        #region Supported Vehicle Tracker

        private class VehicleTracker
        {
            public readonly Dictionary<VehicleConfig, HashSet<BaseEntity>> AllSupportedVehicles = new Dictionary<VehicleConfig, HashSet<BaseEntity>>();

            public HashSet<BaseEntity> GetVehicles(VehicleConfig vehicleConfig)
            {
                HashSet<BaseEntity> vehicles;
                return AllSupportedVehicles.TryGetValue(vehicleConfig, out vehicles)
                    ? vehicles
                    : null;
            }

            public void AddVehicle(VehicleConfig vehicleConfig, BaseEntity vehicle)
            {
                var vehicles = GetVehicles(vehicleConfig);
                if (vehicles == null)
                {
                    vehicles = new HashSet<BaseEntity>();
                    AllSupportedVehicles[vehicleConfig] = vehicles;
                }

                vehicles.Add(vehicle);
            }

            public bool RemoveVehicle(VehicleConfig vehicleConfig, BaseEntity vehicle)
            {
                return GetVehicles(vehicleConfig)?.Remove(vehicle) ?? false;
            }
        }

        #endregion

        #region Reskin Manager

        private class ReskinEvent
        {
            public BaseEntity Parent;
            public VehicleConfig VehicleConfig;
            public List<StorageContainer> Containers = new List<StorageContainer>();
            public Vector3 Position;

            public ReskinEvent WithParent(BaseEntity parent)
            {
                Parent = parent;
                Position = parent?.transform.position ?? Vector3.zero;
                return this;
            }

            public ReskinEvent WithVehicleConfig(VehicleConfig vehicleConfig)
            {
                VehicleConfig = vehicleConfig;
                return this;
            }

            public void AddContainer(StorageContainer container)
            {
                Containers.Add(container);
            }

            public void Reset()
            {
                Parent = null;
                Position = Vector3.zero;
                Containers.Clear();
            }
        }

        private class ReskinEventManager
        {
            // Pool only a single reskin event since usually there will be at most a single event per frame.
            private ReskinEvent _pooledReskinEvent;

            // Keep track of all reskin events happening in a frame, in case there are multiple.
            private List<ReskinEvent> _reskinEvents = new List<ReskinEvent>();

            public readonly Action CleanupAction;

            public ReskinEventManager()
            {
                CleanupAction = CleanupEvents;
            }

            public ReskinEvent GetEvent()
            {
                if (_pooledReskinEvent == null)
                {
                    _pooledReskinEvent = new ReskinEvent();
                }

                return ReferenceEquals(_pooledReskinEvent.Parent, null)
                    ? _pooledReskinEvent
                    : new ReskinEvent();
            }

            public void RecordEvent(ReskinEvent reskinEvent)
            {
                _reskinEvents.Add(reskinEvent);
            }

            public void CancelEvent(ReskinEvent reskinEvent)
            {
                reskinEvent.Reset();
            }

            public ReskinEvent FindEvent(Vector3 position)
            {
                foreach (var reskinEvent in _reskinEvents)
                {
                    if (reskinEvent.Position == position)
                        return reskinEvent;
                }

                return null;
            }

            public void CompleteEvent(ReskinEvent reskinEvent)
            {
                reskinEvent.Reset();
                _reskinEvents.Remove(reskinEvent);
            }

            private void CleanupEvents()
            {
                if (_reskinEvents.Count == 0)
                    return;

                foreach (var reskinEvent in _reskinEvents)
                {
                    if (reskinEvent.Parent == null || reskinEvent.Parent.IsDestroyed)
                    {
                        // The post event wasn't called, and the original parent is gone, so kill the containers.
                        foreach (var container in reskinEvent.Containers)
                        {
                            if (container == null || container.IsDestroyed)
                                continue;

                            container.DropItems();
                            container.Kill();
                        }

                        continue;
                    }

                    // The reskin event must have been blocked, so reparent the containers to the original parent.
                    foreach (var container in reskinEvent.Containers)
                    {
                        var containerPreset = reskinEvent.VehicleConfig.FindContainerPreset(container.name);
                        if (containerPreset != null)
                        {
                            containerPreset.MoveContainerToParent(container, reskinEvent.Parent);
                        }
                        else
                        {
                            container.SetParent(reskinEvent.Parent);
                        }
                    }
                }

                _pooledReskinEvent.Reset();
                _reskinEvents.Clear();
            }
        }

        #endregion

        #region Configuration

        private class ContainerPreset
        {
            [JsonProperty("Prefab")]
            public string Prefab;

            [JsonProperty("Position")]
            public Vector3 Position;

            [JsonProperty("RotationAngles", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public Vector3 RotationAngles;

            [JsonProperty("ParentBone", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string ParentBone;

            [JsonIgnore]
            public Quaternion Rotation => Quaternion.Euler(RotationAngles);

            [JsonIgnore]
            public string Name;

            public void MoveContainerToParent(StorageContainer container, BaseEntity parent)
            {
                var transform = container.transform;
                transform.localPosition = Position;
                transform.localRotation = Rotation;

                container.SetParent(parent, ParentBone);
                container.SendNetworkUpdate_Position();
            }
        }

        private class VehicleProfile
        {
            [JsonProperty("PermissionSuffix", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string PermissionSuffix;

            [JsonProperty("BuiltInStorageCapacity", DefaultValueHandling = DefaultValueHandling.Ignore)]
            [DefaultValue(-1)]
            public int BuiltInStorageCapacity = -1;

            [JsonProperty("AdditionalStorage", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public Dictionary<string, int> AdditionalStorage;

            [JsonIgnore]
            public Dictionary<ContainerPreset, int> ValidAdditionalStorage { get; private set; }

            [JsonIgnore]
            public string Permission { get; private set; }

            public void Init(VehicleStorage pluginInstance, VehicleConfig vehicleConfig)
            {
                if (PermissionSuffix != null)
                {
                    Permission = $"{BasePermissionPrefix}.{vehicleConfig.VehicleType}.{PermissionSuffix}";
                    pluginInstance.permission.RegisterPermission(Permission, pluginInstance);
                }

                if (AdditionalStorage != null && AdditionalStorage.Count != 0)
                {
                    ValidAdditionalStorage = new Dictionary<ContainerPreset, int>(AdditionalStorage.Count);

                    foreach (var presetEntry in AdditionalStorage)
                    {
                        var presetName = presetEntry.Key;
                        var capacity = presetEntry.Value;

                        ContainerPreset preset;
                        if (!vehicleConfig.ContainerPresets.TryGetValue(presetName, out preset))
                        {
                            pluginInstance.LogError($"Storage preset {vehicleConfig.VehicleType} -> \"{presetName}\" does not exist.");
                            continue;
                        }

                        if (string.IsNullOrEmpty(preset.Prefab))
                        {
                            pluginInstance.LogError($"Missing prefab for preset {vehicleConfig.VehicleType} -> \"{presetName}\".");
                            continue;
                        }

                        preset.Name = presetName;
                        ValidAdditionalStorage[preset] = capacity;
                    }
                }
            }
        }

        private abstract class VehicleConfig
        {
            [JsonProperty("DefaultProfile")]
            public VehicleProfile DefaultProfile = new VehicleProfile();

            [JsonProperty("ProfilesRequiringPermission")]
            public VehicleProfile[] ProfilesRequiringPermission;

            [JsonProperty("ContainerPresets", DefaultValueHandling = DefaultValueHandling.Ignore, ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, ContainerPreset> ContainerPresets;

            [JsonIgnore]
            public abstract string VehicleType { get; }

            [JsonIgnore]
            public abstract string PrefabPath { get; }

            public virtual StorageContainer GetDefaultContainer(BaseEntity enitty) => null;

            public virtual ulong GetOwnerId(BaseEntity entity) => entity.OwnerID;

            public void Init(VehicleStorage pluginInstance)
            {
                if (ProfilesRequiringPermission == null)
                    return;

                DefaultProfile.Init(pluginInstance, this);

                foreach (var profile in ProfilesRequiringPermission)
                {
                    profile.Init(pluginInstance, this);
                }
            }

            public VehicleProfile GetProfileForVehicle(Permission permissionSystem, BaseEntity vehicle)
            {
                var ownerId = GetOwnerId(vehicle);
                if (ownerId == 0 || (ProfilesRequiringPermission?.Length ?? 0) == 0)
                    return DefaultProfile;

                var ownerIdString = ownerId.ToString();

                for (var i = ProfilesRequiringPermission.Length - 1; i >= 0; i--)
                {
                    var profile = ProfilesRequiringPermission[i];
                    if (profile.Permission != null && permissionSystem.UserHasPermission(ownerIdString, profile.Permission))
                        return profile;
                }

                return DefaultProfile;
            }

            public ContainerPreset FindContainerPreset(string name)
            {
                ContainerPreset preset;
                return ContainerPresets.TryGetValue(name, out preset)
                    ? preset
                    : null;
            }
        }

        private class AttackHelicopterConfig : VehicleConfig
        {
            public override string VehicleType => "attackhelicopter";
            public override string PrefabPath => "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab";
        }

        private class ChinookConfig : VehicleConfig
        {
            public override string VehicleType => "chinook";
            public override string PrefabPath => "assets/prefabs/npc/ch47/ch47.entity.prefab";
        }

        private class DuoSubmarineConfig : SoloSubmarineConfig
        {
            public override string VehicleType => "duosub";
            public override string PrefabPath => "assets/content/vehicles/submarine/submarineduo.entity.prefab";
        }

        private class HotAirBalloonConfig : VehicleConfig
        {
            public override string VehicleType => "hotairballoon";
            public override string PrefabPath => "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab";

            public override StorageContainer GetDefaultContainer(BaseEntity entity) =>
                (entity as HotAirBalloon)?.storageUnitInstance.Get(serverside: true);
        }

        private class KayakConfig : VehicleConfig
        {
            public override string VehicleType => "kayak";
            public override string PrefabPath => "assets/content/vehicles/boats/kayak/kayak.prefab";
        }

        private class LocomotiveConfig : VehicleConfig
        {
            public override string VehicleType => "locomotive";
            public override string PrefabPath => "assets/content/vehicles/trains/locomotive/locomotive.entity.prefab";
        }

        private class MagnetCraneConfig : VehicleConfig
        {
            public override string VehicleType => "magnetcrane";
            public override string PrefabPath => "assets/content/vehicles/crane_magnet/magnetcrane.entity.prefab";
        }

        private class MinicopterConfig : VehicleConfig
        {
            public override string VehicleType => "minicopter";
            public override string PrefabPath => "assets/content/vehicles/minicopter/minicopter.entity.prefab";
        }

        private class ModularCarStorageModuleConfig : VehicleConfig
        {
            public override string VehicleType => "modularcarstorage";
            public override string PrefabPath => "assets/content/vehicles/modularcar/module_entities/1module_storage.prefab";

            public override StorageContainer GetDefaultContainer(BaseEntity entity) =>
                (entity as VehicleModuleStorage)?.GetContainer() as StorageContainer;

            public override ulong GetOwnerId(BaseEntity entity) =>
                (entity as VehicleModuleStorage)?.Vehicle?.OwnerID ?? 0;
        }

        private class ModularCarCamperModuleConfig : VehicleConfig
        {
            public override string VehicleType => "modularcarcamper";
            public override string PrefabPath => "assets/content/vehicles/modularcar/module_entities/2module_camper.prefab";

            public override StorageContainer GetDefaultContainer(BaseEntity entity) =>
                (entity as VehicleModuleCamper)?.activeStorage.Get(serverside: true);

            public override ulong GetOwnerId(BaseEntity entity) =>
                (entity as VehicleModuleCamper)?.Vehicle?.OwnerID ?? 0;
        }

        private class MotorBikeConfig : VehicleConfig
        {
            public override string VehicleType => "motorbike";
            public override string PrefabPath => "assets/content/vehicles/bikes/motorbike.prefab";
        }

        private class MotorBikeSideCarConfig : VehicleConfig
        {
            public override string VehicleType => "motorbike.sidecar";
            public override string PrefabPath => "assets/content/vehicles/bikes/motorbike_sidecar.prefab";
        }

        private class PedalBikeConfig : VehicleConfig
        {
            public override string VehicleType => "pedalbike";
            public override string PrefabPath => "assets/content/vehicles/bikes/pedalbike.prefab";
        }

        private class PedalTrikeConfig : VehicleConfig
        {
            public override string VehicleType => "pedaltrike";
            public override string PrefabPath => "assets/content/vehicles/bikes/pedaltrike.prefab";
        }

        private class RHIBConfig : RowboatConfig
        {
            public override string VehicleType => "rhib";
            public override string PrefabPath => "assets/content/vehicles/boats/rhib/rhib.prefab";
        }

        private class RidableHorseConfig : VehicleConfig
        {
            public override string VehicleType => "ridablehorse";
            public override string PrefabPath => "assets/content/vehicles/horse/ridablehorse2.prefab";
        }

        private class RowboatConfig : VehicleConfig
        {
            public override string VehicleType => "rowboat";
            public override string PrefabPath => "assets/content/vehicles/boats/rowboat/rowboat.prefab";

            public override StorageContainer GetDefaultContainer(BaseEntity entity) =>
                (entity as MotorRowboat)?.storageUnitInstance.Get(serverside: true);
        }

        private class ScrapTransportHelicopterConfig : VehicleConfig
        {
            public override string VehicleType => "scraptransport";
            public override string PrefabPath => "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab";
        }

        private class SedanConfig : VehicleConfig
        {
            public override string VehicleType => "sedan";
            public override string PrefabPath => "assets/content/vehicles/sedan_a/sedantest.entity.prefab";
        }

        private class SedanRailConfig : VehicleConfig
        {
            public override string VehicleType => "sedanrail";
            public override string PrefabPath => "assets/content/vehicles/sedan_a/sedanrail.entity.prefab";
        }

        private class SnowmobileConfig : VehicleConfig
        {
            public override string VehicleType => "snowmobile";
            public override string PrefabPath => "assets/content/vehicles/snowmobiles/snowmobile.prefab";

            public override StorageContainer GetDefaultContainer(BaseEntity entity) =>
                (entity as Snowmobile)?.GetItemContainer();
        }

        private class SoloSubmarineConfig : VehicleConfig
        {
            public override string VehicleType => "solosub";
            public override string PrefabPath => "assets/content/vehicles/submarine/submarinesolo.entity.prefab";

            public override StorageContainer GetDefaultContainer(BaseEntity entity) =>
                (entity as BaseSubmarine)?.GetItemContainer();
        }

        private class TomahaConfig : VehicleConfig
        {
            public override string VehicleType => "tomaha";
            public override string PrefabPath => "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab";

            public override StorageContainer GetDefaultContainer(BaseEntity entity) =>
                (entity as Snowmobile)?.GetItemContainer();
        }

        private class WagonAConfig : VehicleConfig
        {
            public override string VehicleType => "wagona";
            public override string PrefabPath => "assets/content/vehicles/trains/wagons/trainwagona.entity.prefab";
        }

        private class WagonBConfig : VehicleConfig
        {
            public override string VehicleType => "wagonb";
            public override string PrefabPath => "assets/content/vehicles/trains/wagons/trainwagonb.entity.prefab";
        }

        private class WagonCConfig : VehicleConfig
        {
            public override string VehicleType => "wagonc";
            public override string PrefabPath => "assets/content/vehicles/trains/wagons/trainwagonc.entity.prefab";
        }

        private class WorkcartConfig : VehicleConfig
        {
            public override string VehicleType => "workcart";
            public override string PrefabPath => "assets/content/vehicles/trains/workcart/workcart.entity.prefab";
        }

        private class WorkcartAbovegroundConfig : VehicleConfig
        {
            public override string VehicleType => "workcartaboveground";
            public override string PrefabPath => "assets/content/vehicles/trains/workcart/workcart_aboveground.entity.prefab";
        }

        private class WorkcartCoveredConfig : VehicleConfig
        {
            public override string VehicleType => "workcartcovered";
            public override string PrefabPath => "assets/content/vehicles/trains/workcart/workcart_aboveground2.entity.prefab";
        }

        private class Configuration : BaseConfiguration
        {
            [JsonProperty("AttackHelicopter")]
            public AttackHelicopterConfig AttackHelicopter = new AttackHelicopterConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1stash",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Left Stash"] = 48,
                        }
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2stashes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Left Stash"] = 48,
                            ["Right Stash"] = 48,
                        }
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Left Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-0.63f, 1.07f, 0.68f),
                        RotationAngles = new Vector3(0, 270, 0),
                    },
                    ["Right Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0.63f, 1.07f, 0.68f),
                        RotationAngles = new Vector3(0, 90, 0),
                    },
                },
            };

            [JsonProperty("Chinook")]
            public ChinookConfig Chinook = new ChinookConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "2boxes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Left Box"] = MaxCapacity,
                            ["Front Right Box"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "4boxes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Left Box"] = MaxCapacity,
                            ["Front Right Box"] = MaxCapacity,
                            ["Front Upper Left Box"] = MaxCapacity,
                            ["Front Upper Right Box"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Front Left Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(-0.91f, 1.259f, 2.845f),
                        RotationAngles = new Vector3(0, 90, 0),
                    },
                    ["Front Right Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0.91f, 1.259f, 2.845f),
                        RotationAngles = new Vector3(0, 270, 0),
                    },
                    ["Front Upper Left Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(-0.91f, 1.806f, 2.845f),
                        RotationAngles = new Vector3(0, 90, 0),
                    },
                    ["Front Upper Right Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0.91f, 1.806f, 2.845f),
                        RotationAngles = new Vector3(0, 270, 0),
                    },
                },
            };

            [JsonProperty("DuoSubmarine")]
            public DuoSubmarineConfig DuoSubmarine = new DuoSubmarineConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    BuiltInStorageCapacity = 18,
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "4rows",
                        BuiltInStorageCapacity = 24,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "5rows",
                        BuiltInStorageCapacity = 30,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "6rows",
                        BuiltInStorageCapacity = 36,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "7rows",
                        BuiltInStorageCapacity = 42,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "8rows",
                        BuiltInStorageCapacity = 48,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "1stash",
                        BuiltInStorageCapacity = MaxCapacity,
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Stash"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2stashes",
                        BuiltInStorageCapacity = MaxCapacity,
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Stash"] = MaxCapacity,
                            ["Back Stash"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Front Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0, 1.56f, 0.55f),
                        RotationAngles = new Vector3(270, 0, 0),
                    },
                    ["Back Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0, 1.56f, -1.18f),
                        RotationAngles = new Vector3(270, 180, 0),
                    },
                },
            };

            [JsonProperty("HotAirBalloon")]
            public HotAirBalloonConfig HotAirBalloon = new HotAirBalloonConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    BuiltInStorageCapacity = 18,
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "4rows",
                        BuiltInStorageCapacity = 24,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "5rows",
                        BuiltInStorageCapacity = 30,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "6rows",
                        BuiltInStorageCapacity = 36,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "7rows",
                        BuiltInStorageCapacity = 42,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "8rows",
                        BuiltInStorageCapacity = 48,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2stashes",
                        BuiltInStorageCapacity = MaxCapacity,
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Left Stash"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "3stashes",
                        BuiltInStorageCapacity = MaxCapacity,
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Left Stash"] = MaxCapacity,
                            ["Front Right Stash"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "4stashes",
                        BuiltInStorageCapacity = MaxCapacity,
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Left Stash"] = MaxCapacity,
                            ["Front Right Stash"] = MaxCapacity,
                            ["Back Right Stash"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Front Left Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(1.2f, 0.6f, 1.2f),
                        RotationAngles = new Vector3(330f, 225f, 0),
                    },
                    ["Front Right Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(1.2f, 0.6f, -1.2f),
                        RotationAngles = new Vector3(330f, 315, 0),
                    },
                    ["Back Right Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-1.2f, 0.6f, -1.2f),
                        RotationAngles = new Vector3(330f, 45f, 0),
                    },
                },
            };

            [JsonProperty("Kayak")]
            public KayakConfig Kayak = new KayakConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1stash",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Back Middle Stash"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2stashes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Back Left Stash"] = MaxCapacity,
                            ["Back Right Stash"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Back Left Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-0.28f, 0.23f, -1.18f),
                        RotationAngles = new Vector3(270, 180, 0),
                    },
                    ["Back Middle Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-0.075f, 0.23f, -1.18f),
                        RotationAngles = new Vector3(270, 180, 0),
                    },
                    ["Back Right Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0.17f, 0.23f, -1.18f),
                        RotationAngles = new Vector3(270, 180, 0),
                    },
                },
            };

            [JsonProperty("Locomotive")]
            public LocomotiveConfig Locomotive = new LocomotiveConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1stash",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Stash"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Front Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-0.43f, 2.89f, 5.69f),
                        RotationAngles = new Vector3(0, 180, 0),
                    },
                },
            };

            [JsonProperty("MagnetCrane")]
            public MagnetCraneConfig MagnetCrane = new MagnetCraneConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1stash",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Stash"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Front Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-0.78f, -1.445f, 0f),
                        RotationAngles = new Vector3(90, 0, 90),
                        ParentBone = "Top",
                    },
                },
            };

            [JsonProperty("Minicopter")]
            public MinicopterConfig Minicopter = new MinicopterConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1stash",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Stash Below Pilot Seat"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2stashes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Stash Below Pilot Seat"] = MaxCapacity,
                            ["Stash Below Front Seat"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "3stashes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Stash Below Pilot Seat"] = MaxCapacity,
                            ["Stash Below Front Seat"] = MaxCapacity,
                            ["Stash Behind Fuel Tank"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "4stashes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Stash Below Pilot Seat"] = MaxCapacity,
                            ["Stash Below Front Seat"] = MaxCapacity,
                            ["Stash Below Left Seat"] = MaxCapacity,
                            ["Stash Below Right Seat"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Stash Below Pilot Seat"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0.01f, 0.33f, 0.21f),
                        RotationAngles = new Vector3(270, 90, 0),
                    },
                    ["Stash Below Front Seat"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0.01f, 0.22f, 1.32f),
                        RotationAngles = new Vector3(270, 90, 0),
                    },
                    ["Stash Below Left Seat"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0.6f, 0.32f, -0.41f),
                        RotationAngles = new Vector3(270, 0, 0),
                    },
                    ["Stash Below Right Seat"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-0.6f, 0.32f, -0.41f),
                        RotationAngles = new Vector3(270, 0, 0),
                    },
                    ["Stash Behind Fuel Tank"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0, 1.025f, -0.63f),
                        RotationAngles = new Vector3(0, 180, 0),
                    },
                    ["Box Below Fuel Tank"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0, 0.31f, -0.57f),
                        RotationAngles = new Vector3(0, 90, 0),
                    },
                    ["Back Middle Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0.0f, 0.07f, -1.05f),
                        RotationAngles = new Vector3(0, 180, 0),
                    },
                    ["Back Left Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(-0.48f, 0.07f, -1.05f),
                        RotationAngles = new Vector3(0, 180, 0),
                    },
                    ["Back Right Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0.48f, 0.07f, -1.05f),
                        RotationAngles = new Vector3(0, 180, 0),
                    },
                },
            };

            [JsonProperty("ModularCarCamperModule")]
            public ModularCarCamperModuleConfig ModularCarCamperModule = new ModularCarCamperModuleConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    BuiltInStorageCapacity = 12,
                },

                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "3rows",
                        BuiltInStorageCapacity = 18,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "4rows",
                        BuiltInStorageCapacity = 24,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "5rows",
                        BuiltInStorageCapacity = 30,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "6rows",
                        BuiltInStorageCapacity = 36,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "7rows",
                        BuiltInStorageCapacity = 42,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "8rows",
                        BuiltInStorageCapacity = 48,
                    },
                },
            };
            [JsonProperty("MotorBike")]
            public MotorBikeConfig MotorBike = new MotorBikeConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1stash",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Left Stash"] = 48,
                        }
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2stashes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Left Stash"] = 48,
                            ["Right Stash"] = 48,
                        }
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Left Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-0.09f, 0.8f, -0.6f),
                        RotationAngles = new Vector3(-7, 265, 0),
                    },
                    ["Right Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0.09f, 0.8f, -0.6f),
                        RotationAngles = new Vector3(-7, 95, 0),
                    },
                },
            };

            [JsonProperty("MotorBikeSideCar")]
            public MotorBikeSideCarConfig MotorBikeSideCar = new MotorBikeSideCarConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1stash",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Left Stash"] = 48,
                        }
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2stashes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Left Stash"] = 48,
                            ["Right Stash"] = 48,
                        }
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "3stashes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Left Stash"] = 48,
                            ["Right Stash"] = 48,
                            ["Back Stash"] = 48
                        }
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Left Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-0.09f, 0.8f, -0.6f),
                        RotationAngles = new Vector3(-7, 265, 0),
                    },
                    ["Right Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0.09f, 0.8f, -0.6f),
                        RotationAngles = new Vector3(-7, 95, 0),
                    },
                    ["Back Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0.72f, 0.5f, -0.65f),
                        RotationAngles = new Vector3(185, 0, 90),
                    },
                },
            };

            [JsonProperty("PedalBike")]
            public PedalBikeConfig PedalBike = new PedalBikeConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1stash",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Stash"] = 48,
                        }
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Front Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0, 0.93f, 0.48f),
                        RotationAngles = new Vector3(0, 0, 0),
                    }
                },
            };

            [JsonProperty("PedalTrike")]
            public PedalTrikeConfig PedalTrike = new PedalTrikeConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1stash",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Left Stash"] = 48,
                        }
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2stashes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Left Stash"] = 48,
                            ["Right Stash"] = 48,
                        }
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Left Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-0.28f, 0.67f, -0.7f),
                        RotationAngles = new Vector3(0, 265, 278),
                    },
                    ["Right Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0.28f, 0.67f, -0.7f),
                        RotationAngles = new Vector3(0, -265, -278),
                    },
                },
            };
            [JsonProperty("ModularCarStorageModule")]
            public ModularCarStorageModuleConfig ModularCarStorageModule = new ModularCarStorageModuleConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    BuiltInStorageCapacity = 48,
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1box",
                        BuiltInStorageCapacity = 48,
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Middle Box"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Middle Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0, 0.61f, 0),
                    },
                },
            };

            [JsonProperty("RHIB")]
            public RHIBConfig RHIB = new RHIBConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    BuiltInStorageCapacity = 36,
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "7rows",
                        BuiltInStorageCapacity = 42,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "8rows",
                        BuiltInStorageCapacity = 48,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "3boxes",
                        BuiltInStorageCapacity = MaxCapacity,
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Back Left Box"] = MaxCapacity,
                            ["Back Right Box"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Back Left Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(-0.4f, 1.255f, -2.25f),
                        RotationAngles = new Vector3(0, 270, 0),
                    },
                    ["Back Right Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0.4f, 1.255f, -2.25f),
                        RotationAngles = new Vector3(0, 90, 0),
                    },
                },
            };

            [JsonProperty("RidableHorse")]
            public RidableHorseConfig RidableHorse = new RidableHorseConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1stash",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Back Left Stash"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2stashes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Back Left Stash"] = MaxCapacity,
                            ["Back Right Stash"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Back Left Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-0.1f, 0.1f, 0),
                        RotationAngles = new Vector3(270, 285, 0),
                        ParentBone = "L_Hip",
                    },
                    ["Back Right Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-0.1f, -0.1f, 0),
                        RotationAngles = new Vector3(90, 105, 0),
                        ParentBone = "R_Hip",
                    },
                },
            };

            [JsonProperty("Rowboat")]
            public RowboatConfig Rowboat = new RowboatConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    BuiltInStorageCapacity = 18,
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "4rows",
                        BuiltInStorageCapacity = 24,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "5rows",
                        BuiltInStorageCapacity = 30,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "6rows",
                        BuiltInStorageCapacity = 36,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "7rows",
                        BuiltInStorageCapacity = 42,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "8rows",
                        BuiltInStorageCapacity = 48,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2stashes",
                        BuiltInStorageCapacity = MaxCapacity,
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Back Left Stash"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Back Left Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-0.42f, 0.52f, -1.7f),
                        RotationAngles = new Vector3(270, 180, 0),
                    },
                },
            };

            [JsonProperty("ScrapTransportHelicopter")]
            public ScrapTransportHelicopterConfig ScrapTransportHelicopter = new ScrapTransportHelicopterConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1box",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["LeftBox"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2boxes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["LeftBox"] = MaxCapacity,
                            ["RightBox"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["LeftBox"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(-0.5f, 0.85f, 1.75f),
                    },
                    ["RightBox"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0.5f, 0.85f, 1.75f),
                    },
                },
            };

            [JsonProperty("Sedan")]
            public SedanConfig Sedan = new SedanConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1stash",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Middle Stash"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Middle Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0, 0.83f, 0.55f),
                        RotationAngles = new Vector3(270, 180, 0),
                    },
                },
            };

            [JsonProperty("SedanRail")]
            public SedanRailConfig SedanRail = new SedanRailConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1stash",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Middle Stash"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Middle Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0, 1.065f, -0.21f),
                        RotationAngles = new Vector3(270, 180, 0),
                    },
                },
            };

            [JsonProperty("Snowmobile")]
            public SnowmobileConfig Snowmobile = new SnowmobileConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    BuiltInStorageCapacity = 12,
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "3rows",
                        BuiltInStorageCapacity = 18,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "4rows",
                        BuiltInStorageCapacity = 24,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "5rows",
                        BuiltInStorageCapacity = 30,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "6rows",
                        BuiltInStorageCapacity = 36,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "7rows",
                        BuiltInStorageCapacity = 42,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "8rows",
                        BuiltInStorageCapacity = 48,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2stashes",
                        BuiltInStorageCapacity = MaxCapacity,
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Back Left Stash"] = MaxCapacity,
                            ["Back Right Stash"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Back Left Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-0.21f, 0.555f, -1.08f),
                        RotationAngles = new Vector3(0, 270, 270),
                    },
                    ["Back Right Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0.21f, 0.555f, -1.08f),
                        RotationAngles = new Vector3(0, 90, 90),
                    },
                },
            };

            [JsonProperty("SoloSubmarine")]
            public SoloSubmarineConfig SoloSubmarine = new SoloSubmarineConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    BuiltInStorageCapacity = 18,
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "4rows",
                        BuiltInStorageCapacity = 24,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "5rows",
                        BuiltInStorageCapacity = 30,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "6rows",
                        BuiltInStorageCapacity = 36,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "7rows",
                        BuiltInStorageCapacity = 42,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "8rows",
                        BuiltInStorageCapacity = 48,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "1stash",
                        BuiltInStorageCapacity = MaxCapacity,
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Back Left Stash"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Back Left Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-0.34f, 1.16f, -0.7f),
                        RotationAngles = new Vector3(300, 270, 270),
                    },
                },
            };

            [JsonProperty("Tomaha")]
            public TomahaConfig Tomaha = new TomahaConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    BuiltInStorageCapacity = 12,
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "3rows",
                        BuiltInStorageCapacity = 18,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "4rows",
                        BuiltInStorageCapacity = 24,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "5rows",
                        BuiltInStorageCapacity = 30,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "6rows",
                        BuiltInStorageCapacity = 36,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "7rows",
                        BuiltInStorageCapacity = 42,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "8rows",
                        BuiltInStorageCapacity = 48,
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2stashes",
                        BuiltInStorageCapacity = MaxCapacity,
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Back Left Stash"] = MaxCapacity,
                            ["Back Right Stash"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Back Left Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(-0.21f, 0.37f, -1.08f),
                        RotationAngles = new Vector3(0, 270, 270),
                    },
                    ["Back Right Stash"] = new ContainerPreset
                    {
                        Prefab = HabStoragePrefab,
                        Position = new Vector3(0.21f, 0.37f, -1.08f),
                        RotationAngles = new Vector3(0, 90, 90),
                    },
                },
            };

            [JsonProperty("WagonA")]
            public WagonAConfig WagonA = new WagonAConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "2boxes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Left Box"] = MaxCapacity,
                            ["Front Right Box"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "4boxes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Left Box"] = MaxCapacity,
                            ["Front Right Box"] = MaxCapacity,
                            ["Back Left Box"] = MaxCapacity,
                            ["Back Right Box"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Front Left Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(-1.1f, 1.55f, 1.545f),
                        RotationAngles = new Vector3(0, 90, 0),
                    },
                    ["Front Right Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(1.10f, 1.55f, 1.545f),
                        RotationAngles = new Vector3(0, 270, 0),
                    },
                    ["Back Left Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(-1.1f, 1.55f, -0.5f),
                        RotationAngles = new Vector3(0, 90, 0),
                    },
                    ["Back Right Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(1.10f, 1.55f, -0.5f),
                        RotationAngles = new Vector3(0, 270, 0),
                    },
                },
            };

            [JsonProperty("WagonB")]
            public WagonBConfig WagonB = new WagonBConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "2boxes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Left Box"] = MaxCapacity,
                            ["Front Right Box"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "4boxes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Left Box"] = MaxCapacity,
                            ["Front Right Box"] = MaxCapacity,
                            ["Back Left Box"] = MaxCapacity,
                            ["Back Right Box"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Front Left Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(-1.1f, 1.55f, 1.545f),
                        RotationAngles = new Vector3(0, 90, 0),
                    },
                    ["Front Right Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(1.10f, 1.55f, 1.545f),
                        RotationAngles = new Vector3(0, 270, 0),
                    },
                    ["Back Left Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(-1.1f, 1.55f, -0.5f),
                        RotationAngles = new Vector3(0, 90, 0),
                    },
                    ["Back Right Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(1.10f, 1.55f, -0.5f),
                        RotationAngles = new Vector3(0, 270, 0),
                    },
                },
            };

            [JsonProperty("WagonC")]
            public WagonCConfig WagonC = new WagonCConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "2boxes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Middle Box 1"] = MaxCapacity,
                            ["Middle Box 2"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "4boxes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Middle Box 1"] = MaxCapacity,
                            ["Middle Box 2"] = MaxCapacity,
                            ["Middle Box 3"] = MaxCapacity,
                            ["Middle Box 4"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Middle Box 1"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0f, 1.51f, 1.5f),
                        RotationAngles = new Vector3(0, 90, 0),
                    },
                    ["Middle Box 2"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0f, 1.51f, 0.5f),
                        RotationAngles = new Vector3(0, 270, 0),
                    },
                    ["Middle Box 3"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0f, 1.51f, -0.5f),
                        RotationAngles = new Vector3(0, 90, 0),
                    },
                    ["Middle Box 4"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0f, 1.51f, -1.5f),
                        RotationAngles = new Vector3(0, 270, 0),
                    },
                },
            };

            [JsonProperty("Workcart")]
            public WorkcartConfig Workcart = new WorkcartConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1box",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Box"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2boxes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Box"] = MaxCapacity,
                            ["Back Box"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Front Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0.85f, 2.595f, 1.43f),
                        RotationAngles = new Vector3(0, 180, 0),
                    },
                    ["Back Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0.85f, 2.595f, 0.7f),
                        RotationAngles = new Vector3(0, 180, 0),
                    },
                },
            };

            [JsonProperty("WorkcartAboveground")]
            public WorkcartAbovegroundConfig WorkcartAboveground = new WorkcartAbovegroundConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1box",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Box"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2boxes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Box"] = MaxCapacity,
                            ["Back Box"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Front Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0.85f, 2.595f, 1.43f),
                        RotationAngles = new Vector3(0, 180, 0),
                    },
                    ["Back Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0.85f, 2.595f, 0.7f),
                        RotationAngles = new Vector3(0, 180, 0),
                    },
                },
            };

            [JsonProperty("WorkcartCovered")]
            public WorkcartCoveredConfig WorkcartCovered = new WorkcartCoveredConfig
            {
                DefaultProfile = new VehicleProfile
                {
                    AdditionalStorage = new Dictionary<string, int>(),
                },
                ProfilesRequiringPermission = new[]
                {
                    new VehicleProfile
                    {
                        PermissionSuffix = "1box",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Box"] = MaxCapacity,
                        },
                    },
                    new VehicleProfile
                    {
                        PermissionSuffix = "2boxes",
                        AdditionalStorage = new Dictionary<string, int>
                        {
                            ["Front Box"] = MaxCapacity,
                            ["Back Box"] = MaxCapacity,
                        },
                    },
                },
                ContainerPresets = new Dictionary<string, ContainerPreset>
                {
                    ["Front Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0.85f, 2.595f, 1f),
                        RotationAngles = new Vector3(0, 180, 0),
                    },
                    ["Back Box"] = new ContainerPreset
                    {
                        Prefab = RhibStoragePrefab,
                        Position = new Vector3(0.85f, 2.595f, 0.27f),
                        RotationAngles = new Vector3(0, 180, 0),
                    },
                },
            };

            [JsonIgnore]
            private readonly Dictionary<uint, VehicleConfig> _vehicleConfigsByPrefabId = new Dictionary<uint, VehicleConfig>();

            public void Init(VehicleStorage pluginInstance)
            {
                var allVehicleConfigs = new VehicleConfig[]
                {
                    AttackHelicopter,
                    Chinook,
                    DuoSubmarine,
                    HotAirBalloon,
                    Kayak,
                    Locomotive,
                    MagnetCrane,
                    Minicopter,
                    ModularCarCamperModule,
                    ModularCarStorageModule,
                    MotorBike,
                    MotorBikeSideCar,
                    PedalBike,
                    PedalTrike,
                    RHIB,
                    RidableHorse,
                    Rowboat,
                    ScrapTransportHelicopter,
                    Sedan,
                    SedanRail,
                    Snowmobile,
                    SoloSubmarine,
                    Tomaha,
                    WagonA,
                    WagonB,
                    WagonC,
                    Workcart,
                    WorkcartAboveground,
                    WorkcartCovered,
                };

                foreach (var vehicleConfig in allVehicleConfigs)
                {
                    // Map the configs by prefab id for fast lookup.
                    var prefabId = StringPool.Get(vehicleConfig.PrefabPath);
                    _vehicleConfigsByPrefabId[prefabId] = vehicleConfig;

                    // Validate correctness, and register permissions.
                    vehicleConfig.Init(pluginInstance);
                }
            }

            public VehicleConfig GetVehicleConfig(BaseEntity entity)
            {
                VehicleConfig vehicleConfig;
                return _vehicleConfigsByPrefabId.TryGetValue(entity.prefabID, out vehicleConfig)
                    ? vehicleConfig
                    : null;
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
