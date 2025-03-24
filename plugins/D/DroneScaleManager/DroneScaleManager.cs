using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("Drone Scale Manager", "WhiteThunder", "1.0.1")]
    [Description("Utilities for resizing RC drones.")]
    internal class DroneScaleManager : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        Plugin DroneEffects, EntityScaleManager;

        private static DroneScaleManager _pluginInstance;
        private static StoredData _pluginData;

        private const string PermissionScaleUnrestricted = "dronescalemanager.unrestricted";

        private const string SpherePrefab = "assets/prefabs/visualization/sphere.prefab";

        private const float VanillaDroneGroundTraceDistance = 0.1f;
        private const float VanillaDroneYawSpeed = 2;
        private const float RootEntityLocalY = 0.2f;

        private static readonly Vector3 RootEntityPosition = new Vector3(0, RootEntityLocalY, 0);

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginInstance = this;
            _pluginData = StoredData.Load();

            permission.RegisterPermission(PermissionScaleUnrestricted, this);
        }

        private void OnServerInitialized()
        {
            if (!VerifyDependencies())
                return;

            RefreshAllScaledDrones();
        }

        private void Unload()
        {
            _pluginData.Save();

            DroneCollisionProxy.DestroyAll();

            _pluginData = null;
            _pluginInstance = null;
        }

        private void OnServerSave()
        {
            _pluginData.Save();
        }

        private void OnNewSave()
        {
            _pluginData = StoredData.Clear();
        }

        private void OnPluginLoaded(Plugin plugin)
        {
            if (plugin == EntityScaleManager)
                RefreshAllScaledDrones();
        }

        private void OnEntityKill(Drone drone)
        {
            if (!_pluginData.ScaledDrones.Remove(drone.net.ID.Value))
                return;

            var rootEntity = GetRootEntity(drone);
            if (rootEntity == null)
                return;

            rootEntity.Invoke(() =>
            {
                if (!rootEntity.IsDestroyed)
                    rootEntity.Kill();
            }, 0);
        }

        // This hook is exposed by plugin: Drone Effects (DroneEffects).
        private bool? OnDroneAnimationStart(Drone drone)
        {
            // Prevent the animated delivery drone from spawning, since it may be normal size.
            if (IsScaledDrone(drone))
                return false;

            return null;
        }

        #endregion

        #region API

        private bool API_ScaleDrone(Drone drone, float scale)
        {
            return TryScaleDrone(drone, scale);
        }

        private bool API_ParentEntity(Drone drone, BaseEntity childEntity)
        {
            if (!IsDroneEligible(drone) || !IsScaledDrone(drone))
                return false;

            var rootEntity = GetRootEntityOrParentSphere(drone);
            if (rootEntity == null)
                return false;

            PositionChildTransform(rootEntity.transform, drone.transform, childEntity.transform);
            childEntity.SetParent(rootEntity, worldPositionStays: false, sendImmediate: true);

            if (!childEntity.isSpawned)
                childEntity.Spawn();

            return true;
        }

        private bool API_ParentTransform(Drone drone, Transform childTransform)
        {
            if (!IsDroneEligible(drone) || !IsScaledDrone(drone))
                return false;

            var rootEntity = GetRootEntityOrParentSphere(drone);
            if (rootEntity == null)
                return false;

            var rootTransform = rootEntity.transform;
            PositionChildTransform(rootTransform, drone.transform, childTransform);
            childTransform.parent = rootTransform;

            return true;
        }

        private Drone API_GetParentDrone(BaseEntity entity)
        {
            var possibleRootEntity = GetParentSphere(entity);
            if (possibleRootEntity == null)
                return null;

            var drone = GetGrandChildOfType<Drone>(possibleRootEntity);
            if (drone == null || !IsScaledDrone(drone))
                return null;

            return drone;
        }

        private SphereEntity API_GetRootEntity(Drone drone)
        {
            return GetRootEntity(drone);
        }

        #endregion

        #region Commands

        [Command("scaledrone", "dronescale")]
        private void CommandDroneScale(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer)
                return;

            if (!player.HasPermission(PermissionScaleUnrestricted))
            {
                ReplyToPlayer(player, Lang.ErrorNoPermission);
                return;
            }

            float scale;
            if (args.Length == 0 || !float.TryParse(args[0], out scale))
            {
                ReplyToPlayer(player, Lang.ErrorSyntax, cmd);
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            var drone = GetLookEntity(basePlayer) as Drone;
            if (drone == null || !IsDroneEligible(drone))
            {
                ReplyToPlayer(player, Lang.ErrorNoDroneFound);
                return;
            }

            if (TryScaleDrone(drone, scale))
                ReplyToPlayer(player, Lang.ScaleSuccess, scale);
            else
                ReplyToPlayer(player, Lang.ScaleError);
        }

        #endregion

        #region Helper Methods

        private static bool DroneScaleWasBlocked(Drone drone, float scale)
        {
            object hookResult = Interface.CallHook("OnDroneScale", drone, scale);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool VerifyDependencies()
        {
            if (_pluginInstance.EntityScaleManager == null)
            {
                _pluginInstance.LogError("EntityScaleManager is not loaded, get it at https://umod.org");
                return false;
            }

            return true;
        }

        private static bool IsScaledDrone(Drone drone) =>
            _pluginData.ScaledDrones.Contains(drone.net.ID.Value);

        private static float GetDroneScale(Drone drone)
        {
            if (!VerifyDependencies())
                return 1;

            return Convert.ToSingle(_pluginInstance.EntityScaleManager.Call("API_GetScale", drone));
        }

        private static bool IsDroneEligible(Drone drone) => !(drone is DeliveryDrone);

        private static BaseEntity GetLookEntity(BasePlayer basePlayer, float maxDistance = 20)
        {
            RaycastHit hit;
            return Physics.Raycast(basePlayer.eyes.HeadRay(), out hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.GetEntity()
                : null;
        }

        private static T GetGrandChildOfType<T>(BaseEntity entity) where T : BaseEntity
        {
            foreach (var child in entity.children)
            {
                foreach (var grandChild in child.children)
                {
                    var grandChildOfType = grandChild as T;
                    if (grandChildOfType != null)
                        return grandChildOfType;
                }
            }
            return null;
        }

        private static void CopyRigidBodySettings(Rigidbody sourceBody, Rigidbody destinationBody)
        {
            // Copy everything except rotation since we are handling that at the transform level instead.
            destinationBody.angularDrag = sourceBody.angularDrag;
            destinationBody.centerOfMass = sourceBody.centerOfMass;
            destinationBody.collisionDetectionMode = sourceBody.collisionDetectionMode;
            destinationBody.constraints = sourceBody.constraints;
            destinationBody.detectCollisions = sourceBody.detectCollisions;
            destinationBody.drag = sourceBody.drag;
            destinationBody.freezeRotation = sourceBody.freezeRotation;
            destinationBody.inertiaTensorRotation = sourceBody.inertiaTensorRotation;
            destinationBody.interpolation = sourceBody.interpolation;
            destinationBody.isKinematic = sourceBody.isKinematic;
            destinationBody.mass = sourceBody.mass;
            destinationBody.maxAngularVelocity = sourceBody.maxAngularVelocity;
            destinationBody.maxDepenetrationVelocity = sourceBody.maxDepenetrationVelocity;
            destinationBody.sleepThreshold = sourceBody.sleepThreshold;
            destinationBody.solverIterations = sourceBody.solverIterations;
            destinationBody.solverVelocityIterations = sourceBody.solverVelocityIterations;
            destinationBody.useGravity = sourceBody.useGravity;
        }

        private static void MaybeSetupRootRigidBody(Drone scaledDrone, SphereEntity rootEntity)
        {
            var rootRigidBody = rootEntity.GetOrAddComponent<Rigidbody>();
            if (rootRigidBody == scaledDrone.body)
                return;

            CopyRigidBodySettings(scaledDrone.body, rootRigidBody);
            UnityEngine.Object.DestroyImmediate(scaledDrone.body);
            scaledDrone.body = rootRigidBody;
        }

        private static void RestoreRigidBody(Drone scaledDrone, SphereEntity rootEntity)
        {
            var rootRigidBody = rootEntity.GetComponent<Rigidbody>();
            if (rootRigidBody == null)
                return;

            scaledDrone.body = scaledDrone.GetOrAddComponent<Rigidbody>();
            CopyRigidBodySettings(rootRigidBody, scaledDrone.body);
        }

        private static void EnableGlobalBroadcastFixed(BaseEntity entity, bool wants)
        {
            entity.globalBroadcast = wants;

            if (wants)
            {
                entity.UpdateNetworkGroup();
            }
            else if (entity.net?.group?.ID == 0)
            {
                // Fix vanilla bug that prevents leaving the global network group.
                var group = entity.net.sv.visibility.GetGroup(entity.transform.position);
                entity.net.SwitchGroup(group);
            }
        }

        private static void SetupRootEntity(Drone drone, SphereEntity rootEntity)
        {
            rootEntity.gameObject.layer = drone.gameObject.layer;

            // SphereEntity has enableSaving off by default.
            // This fixes an issue where the resized child gets orphaned on restart and spams console errors every 2 seconds.
            rootEntity.EnableSaving(true);

            // SphereEntity has globalBroadcast on by default.
            // This fixes an issue where clients who resubscribe do not recreate the sphere or its children.
            EnableGlobalBroadcastFixed(rootEntity, false);

            // Move rigid body to the root entity.
            MaybeSetupRootRigidBody(drone, rootEntity);

            // Proxy collisions from the root entity to the drone.
            rootEntity.GetOrAddComponent<DroneCollisionProxy>().OwnerDrone = drone;
        }

        private static void SetupRootEntityAfterSpawn(Drone drone, SphereEntity rootEntity)
        {
            // Cancel the default position network updates since we will use fixed time updates instead.
            rootEntity.CancelInvoke(rootEntity.NetworkPositionTick);

            if (!rootEntity.IsInvokingFixedTime(rootEntity.NetworkPositionTick))
                rootEntity.InvokeRepeatingFixedTime(rootEntity.NetworkPositionTick);
        }

        private static void SetupScaledDrone(Drone drone, float scale)
        {
            // Without changing yaw speed, large drones turn faster and small drones turn slower.
            drone.yawSpeed = VanillaDroneYawSpeed / scale;
            drone.groundTraceDist = Math.Min(RootEntityLocalY, VanillaDroneGroundTraceDistance * scale + RootEntityLocalY);

            // Cancel position network updates since the root entity will be moved instead.
            if (scale != 1)
                drone.CancelInvokeFixedTime(drone.NetworkPositionTick);
        }

        private static void RefreshScaledDrone(Drone drone)
        {
            SphereEntity parentSphere;
            var rootEntity = GetRootEntity(drone, out parentSphere);
            if (rootEntity == null)
                return;

            SetupRootEntity(drone, rootEntity);
            SetupRootEntityAfterSpawn(drone, rootEntity);
            SetupScaledDrone(drone, parentSphere.currentRadius);
        }

        private void RefreshAllScaledDrones()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var drone = entity as Drone;
                if (drone == null || !IsDroneEligible(drone))
                    continue;

                if (IsScaledDrone(drone))
                    RefreshScaledDrone(drone);
            }
        }

        private static void PositionChildTransform(Transform rootTransform, Transform droneTransform, Transform childTransform)
        {
            var scaledWorldPosition = droneTransform.TransformPoint(childTransform.localPosition);
            childTransform.localPosition = rootTransform.InverseTransformPoint(scaledWorldPosition);
            childTransform.localRotation = droneTransform.localRotation * childTransform.localRotation;
        }

        private static SphereEntity GetParentSphere(BaseEntity entity) =>
            entity.GetParentEntity() as SphereEntity;

        private static SphereEntity GetRootEntity(Drone drone, out SphereEntity parentSphere)
        {
            parentSphere = GetParentSphere(drone);
            if (parentSphere == null)
                return null;

            return parentSphere.GetParentEntity() as SphereEntity;
        }

        private static SphereEntity GetRootEntity(Drone drone)
        {
            SphereEntity parentSphere;
            return GetRootEntity(drone, out parentSphere);
        }

        private static SphereEntity GetRootEntityOrParentSphere(Drone drone)
        {
            SphereEntity parentSphere;
            var rootEntity = GetRootEntity(drone, out parentSphere);
            if (rootEntity != null)
                return rootEntity;

            // Assume the parent sphere is the root entity, in case this was called during the OnDroneScaleBegin hook.
            if (parentSphere != null)
                return parentSphere;

            return null;
        }

        private static SphereEntity AddRootEntity(Drone drone)
        {
            var rootEntity = GameManager.server.CreateEntity(SpherePrefab, drone.transform.TransformPoint(RootEntityPosition)) as SphereEntity;
            if (rootEntity == null)
                return null;

            SetupRootEntity(drone, rootEntity);
            rootEntity.Spawn();
            SetupRootEntityAfterSpawn(drone, rootEntity);
            drone.SetParent(rootEntity, worldPositionStays: true, sendImmediate: true);

            return rootEntity;
        }

        private static void RemoveRootEntity(Drone scaledDrone)
        {
            SphereEntity parentSphere;
            var rootEntity = GetRootEntity(scaledDrone, out parentSphere);
            if (rootEntity == null)
                return;

            // Restore the movement updates since they were disabled while resized.
            if (!scaledDrone.IsInvokingFixedTime(scaledDrone.NetworkPositionTick))
                scaledDrone.InvokeRepeatingFixedTime(scaledDrone.NetworkPositionTick);

            parentSphere.SetParent(null, worldPositionStays: true, sendImmediate: true);

            RestoreRigidBody(scaledDrone, rootEntity);
            rootEntity.Kill();

            _pluginData.ScaledDrones.Remove(scaledDrone.net.ID.Value);
        }

        private static bool ScaleDrone(Drone drone, SphereEntity rootEntity, float scale, float currentScale)
        {
            SetupScaledDrone(drone, scale);

            // Notify other plugins before removing the root entity.
            // This allows plugins to move or remove attachments from the root entity if needed.
            Interface.CallHook("OnDroneScaleBegin", drone, rootEntity, scale, currentScale);

            if (scale == 1)
                RemoveRootEntity(drone);

            var result = _pluginInstance.EntityScaleManager.CallHook("API_ScaleEntity", drone, scale);
            var success = result is bool && (bool)result;

            // Reposition existing attachments if the drone is already resized.
            if (IsScaledDrone(drone))
            {
                var rootTransform = rootEntity.transform;
                var droneTransform = drone.transform;
                var droneParentSphere = GetParentSphere(drone);

                foreach (var child in rootEntity.children)
                {
                    if (child == droneParentSphere)
                        continue;

                    var childTransform = child.transform;
                    var baseLocalPosition = droneTransform.InverseTransformPoint(childTransform.position) / currentScale;
                    var newWorldPosition = droneTransform.TransformPoint(baseLocalPosition * scale);
                    childTransform.localPosition = rootTransform.InverseTransformPoint(newWorldPosition);

                    child.InvalidateNetworkCache();
                    child.SendNetworkUpdate_Position();
                }
            }

            return success;
        }

        private static bool TryScaleDrone(Drone drone, float desiredScale)
        {
            if (!VerifyDependencies())
                return false;

            if (DroneScaleWasBlocked(drone, desiredScale))
                return false;

            var currentScale = GetDroneScale(drone);
            var isCurrentlyScaled = currentScale != 1 && IsScaledDrone(drone);

            bool success = false;
            SphereEntity rootEntity;

            if (isCurrentlyScaled)
            {
                if (desiredScale == currentScale)
                    return true;

                rootEntity = GetRootEntity(drone);
                success = ScaleDrone(drone, rootEntity, desiredScale, currentScale);
            }
            else if (desiredScale == 1)
            {
                return true;
            }
            else
            {
                _pluginData.ScaledDrones.Add(drone.net.ID.Value);
                rootEntity = AddRootEntity(drone);
                success = ScaleDrone(drone, rootEntity, desiredScale, currentScale);
                _pluginInstance.DroneEffects?.Call("API_StopAnimating", drone);
            }

            Interface.CallHook("OnDroneScaled", drone, rootEntity, desiredScale, currentScale);
            return success;
        }

        #endregion

        #region Collision Detection

        private class DroneCollisionProxy : MonoBehaviour
        {
            private const string OnCollisionEnterMethodName = "OnCollisionEnter";
            private const string OnCollisionStayMethodName = "OnCollisionStay";

            public static void DestroyAll()
            {
                foreach (var entity in BaseNetworkable.serverEntities)
                {
                    var sphereEntity = entity as SphereEntity;
                    if (sphereEntity == null)
                        continue;

                    var component = sphereEntity.GetComponent<DroneCollisionProxy>();
                    if (component == null)
                        continue;

                    DestroyImmediate(component);
                }
            }

            public Drone OwnerDrone;

            private void OnCollisionEnter(Collision collision)
            {
                if (OwnerDrone != null)
                    OwnerDrone.BroadcastMessage(OnCollisionEnterMethodName, collision, SendMessageOptions.DontRequireReceiver);
            }

            private void OnCollisionStay()
            {
                if (OwnerDrone != null)
                    OwnerDrone.BroadcastMessage(OnCollisionStayMethodName, SendMessageOptions.DontRequireReceiver);
            }
        }

        #endregion

        #region Data

        private class StoredData
        {
            [JsonProperty("ScaledDrones")]
            public HashSet<ulong> ScaledDrones = new HashSet<ulong>();

            public static StoredData Load() =>
                Interface.Oxide.DataFileSystem.ReadObject<StoredData>(_pluginInstance.Name) ?? new StoredData();

            public static StoredData Clear() => new StoredData().Save();

            public StoredData Save()
            {
                Interface.Oxide.DataFileSystem.WriteObject(_pluginInstance.Name, this);
                return this;
            }
        }

        #endregion

        #region Localization

        private void ReplyToPlayer(IPlayer player, string messageName, params object[] args) =>
            player.Reply(string.Format(GetMessage(player, messageName), args));

        private string GetMessage(IPlayer player, string messageName, params object[] args) =>
            GetMessage(player.Id, messageName, args);

        private string GetMessage(string playerId, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, playerId);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        private class Lang
        {
            public const string ErrorNoPermission = "Error.NoPermission";
            public const string ErrorSyntax = "Error.Syntax";
            public const string ErrorNoDroneFound = "Error.NoDroneFound";
            public const string ScaleSuccess = "Scale.Success";
            public const string ScaleError = "Error.ScalePrevented";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.ErrorNoPermission] = "You don't have permission to do that.",
                [Lang.ErrorSyntax] = "Syntax: {0} <size>",
                [Lang.ErrorNoDroneFound] = "Error: No drone found.",
                [Lang.ScaleSuccess] = "Drone was scaled to: {0}",
                [Lang.ScaleError] = "An error occurred while attempting to resize that drone.",
            }, this, "en");
        }

        #endregion
    }
}
