using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("Drone Turrets", "WhiteThunder", "1.3.3")]
    [Description("Allows players to deploy auto turrets to RC drones.")]
    internal class DroneTurrets : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private readonly Plugin DroneSettings, EntityScaleManager;

        private Configuration _config;

        private const float TurretScale = 0.6f;

        private const string PermissionDeploy = "droneturrets.deploy";
        private const string PermissionDeployNpc = "droneturrets.deploynpc";
        private const string PermissionDeployFree = "droneturrets.deploy.free";
        private const string PermissionAutoDeploy = "droneturrets.autodeploy";
        private const string PermissionControl = "droneturrets.control";

        private const string SpherePrefab = "assets/prefabs/visualization/sphere.prefab";
        private const string AutoTurretPrefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
        private const string NpcAutoTurretPrefab = "assets/content/props/sentry_scientists/sentry.bandit.static.prefab";
        private const string ElectricSwitchPrefab = "assets/prefabs/deployable/playerioents/simpleswitch/switch.prefab";
        // private const string AlarmPrefab = "assets/prefabs/io/electric/other/alarmsound.prefab";
        private const string AlarmPrefab = "assets/prefabs/deployable/playerioents/alarms/audioalarm.prefab";
        private const string SirenLightPrefab = "assets/prefabs/io/electric/lights/sirenlightorange.prefab";
        private const string DeployEffectPrefab = "assets/prefabs/npc/autoturret/effects/autoturret-deploy.prefab";
        private const string CodeLockDeniedEffectPrefab = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";

        private const int AutoTurretItemId = -2139580305;

        private const BaseEntity.Slot TurretSlot = BaseEntity.Slot.UpperModifier;

        private static readonly Vector3 SphereEntityLocalPosition = new(0, -0.14f, 0);
        private static readonly Vector3 TurretSwitchPosition = new Vector3(0, 0.36f, -0.32f);
        private static readonly Quaternion TurretSwitchRotation = Quaternion.Euler(0, 180, 0);

        private static readonly Vector3 SphereTransformScale = new(TurretScale, TurretScale, TurretScale);
        private static readonly Vector3 TurretTransformScale = new(1 / TurretScale, 1 / TurretScale, 1 / TurretScale);

        private readonly object True = true;
        private readonly object False = false;

        private DynamicHookSubscriber<NetworkableId> _turretDroneTracker;

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionDeploy, this);
            permission.RegisterPermission(PermissionDeployNpc, this);
            permission.RegisterPermission(PermissionDeployFree, this);
            permission.RegisterPermission(PermissionAutoDeploy, this);
            permission.RegisterPermission(PermissionControl, this);

            var dynamicHookNames = new List<string>
            {
                nameof(OnServerCommand),
                nameof(OnSwitchToggle),
                nameof(OnSwitchToggled),
                nameof(OnTurretTarget),
                nameof(OnEntityTakeDamage),
                nameof(OnEntityKill),
                nameof(OnEntityDeath),
                nameof(CanPickupEntity),
                nameof(OnBookmarkControlStarted),
                nameof(OnBookmarkControlEnded),
                nameof(canRemove),
            };

            _turretDroneTracker = new DynamicHookSubscriber<NetworkableId>(this, dynamicHookNames.ToArray());
            _turretDroneTracker.UnsubscribeAll();
        }

        private void OnServerInitialized()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var drone = entity as Drone;
                if (drone == null || !RCUtils.IsRCDrone(drone))
                    continue;

                var turret = GetDroneTurret(drone);
                if (turret == null)
                    continue;

                RefreshDroneTurret(drone, turret);
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                var turret = RCUtils.GetControlledEntity<AutoTurret>(player, out var station);
                if (turret == null)
                    continue;

                OnBookmarkControlStarted(station, player, turret.GetIdentifier(), turret);
            }
        }

        private void Unload()
        {
            DroneController.DestroyAll();
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
                return null;

            var player = arg.Player();
            if (player == null)
                return null;

            if (arg.cmd.FullName == "vehicle.swapseats")
            {
                HandleSwapSeats(player);
                return null;
            }

            if (arg.cmd.FullName == "inventory.lighttoggle")
                return HandleLightToggle(player);

            return null;
        }

        private void OnEntityBuilt(Planner planner, GameObject go)
        {
            if (planner == null || go == null)
                return;

            var drone = go.ToBaseEntity() as Drone;
            if (drone == null)
                return;

            var player = planner.GetOwnerPlayer();
            if (player == null)
                return;

            var drone2 = drone;

            NextTick(() =>
            {
                // Delay this check to allow time for other plugins to deploy an entity to this slot.
                if (drone2 == null || player == null || drone2.GetSlot(TurretSlot) != null)
                    return;

                if (permission.UserHasPermission(player.UserIDString, PermissionAutoDeploy))
                {
                    DeployAutoTurret(drone2, player, 1);
                }
                else if (permission.UserHasPermission(player.UserIDString, PermissionDeploy)
                    && UnityEngine.Random.Range(0, 100) < _config.TipChance)
                {
                    ChatMessage(player, Lang.TipDeployCommand);
                }
            });
        }

        private object OnSwitchToggle(ElectricSwitch electricSwitch, BasePlayer player)
        {
            var turret = GetParentTurret(electricSwitch);
            if (turret == null)
                return null;

            var drone = GetParentDrone(turret);
            if (drone == null)
                return null;

            if (!player.CanBuild())
            {
                // Disallow switching the turret on and off while building blocked.
                Effect.server.Run(CodeLockDeniedEffectPrefab, electricSwitch, 0, Vector3.zero, Vector3.forward);
                return False;
            }

            return null;
        }

        private void OnSwitchToggled(ElectricSwitch electricSwitch)
        {
            var turret = GetParentTurret(electricSwitch);
            if (turret == null)
                return;

            var drone = GetParentDrone(turret);
            if (drone == null)
                return;

            if (electricSwitch.IsOn())
            {
                turret.InitiateStartup();
            }
            else
            {
                turret.InitiateShutdown();
            }

            RefreshAlarmState(drone, turret);
        }

        private object OnTurretTarget(AutoTurret turret, BaseCombatEntity target)
        {
            if (turret == null || target == null || GetParentDrone(turret) == null)
                return null;

            if (!_config.TargetAnimals && target is BaseAnimalNPC)
                return False;

            var basePlayer = target as BasePlayer;
            if (basePlayer != null)
            {
                if (!_config.TargetNPCs && basePlayer.IsNpc)
                    return False;

                if (!_config.TargetPlayers && basePlayer.userID.IsSteamId())
                    return False;

                // Don't target human or NPC players in safe zones, unless they are hostile.
                if (basePlayer.InSafeZone() && (basePlayer.IsNpc || !basePlayer.IsHostile()))
                    return False;

                return null;
            }

            return null;
        }

        // Redirect damage from the turret to the drone.
        private object OnEntityTakeDamage(AutoTurret turret, HitInfo info)
        {
            var drone = GetParentDrone(turret);
            if (drone == null)
                return null;

            drone.Hurt(info);
            HitNotify(drone, info);

            return True;
        }

        // Redirect damage from the turret switch to the drone.
        private object OnEntityTakeDamage(ElectricSwitch electricSwitch, HitInfo info)
        {
            var autoTurret = GetParentTurret(electricSwitch);
            if (autoTurret == null)
                return null;

            var drone = GetParentDrone(autoTurret);
            if (drone == null)
                return null;

            drone.Hurt(info);
            HitNotify(drone, info);

            return True;
        }

        private void OnEntityKill(Drone drone)
        {
            if (GetDroneTurret(drone))
                return;

            _turretDroneTracker.Remove(drone.net.ID);
        }

        private void OnEntityKill(AutoTurret turret)
        {
            SphereEntity parentSphere;
            var drone = GetParentDrone(turret, out parentSphere);
            if (drone == null)
                return;

            var parentSphere2 = parentSphere;
            parentSphere.Invoke(() =>
            {
                // EntityScaleManager may have already destroyed the sphere in the same frame.
                if (!parentSphere2.IsDestroyed)
                {
                    parentSphere2.Kill();
                }
            }, 0);

            _turretDroneTracker.Remove(drone.net.ID);

            var drone2 = drone;
            drone.Invoke(() => RefreshDroneSettingsProfile(drone2), 0);
        }

        private void OnEntityDeath(Drone drone)
        {
            if (!RCUtils.IsRCDrone(drone))
                return;

            var turret = GetDroneTurret(drone);
            if (turret != null)
            {
                // Causing the turret to die allows its inventory to potentially be dropped.
                // This approach is intentionally used, as opposed to dropping the inventory
                // directly, in order to respect vanilla behavior around `turret.dropChance`, and
                // to allow other plugins to intercept the OnEntityDeath(AutoTurret) hook to kill
                // the inventory before vanilla logic drops it.
                turret.Die();
            }
        }

        private object CanPickupEntity(BasePlayer player, Drone drone)
        {
            if (CanPickupInternal(drone))
                return null;

            ChatMessage(player, Lang.ErrorCannotPickupWithTurret);
            return False;
        }

        private void OnBookmarkControlStarted(ComputerStation station, BasePlayer player, string bookmarkName, Drone drone)
        {
            var turret = GetDroneTurret(drone);
            if (turret == null)
                return;

            RefreshAlarmState(drone, turret);
        }

        private void OnBookmarkControlStarted(ComputerStation station, BasePlayer player, string bookmarkName, AutoTurret turret)
        {
            var drone = GetParentDrone(turret);
            if (drone == null)
                return;

            if (!RCUtils.HasController(turret, player))
                return;

            if (!HasPermissionToControl(player))
            {
                RCUtils.RemoveController(turret);
                RCUtils.AddFakeViewer(turret);
                RCUtils.AddViewer(turret, player);
                RCUtils.RemoveController(turret);
                station.SetFlag(ComputerStation.Flag_HasFullControl, false);
                return;
            }

            if (RCUtils.CanControl(player, drone))
            {
                RCUtils.RemoveController(drone);
                RCUtils.AddViewer(drone, player);
                DroneController.AddToDrone(drone, turret, player);
                ExposedHooks.OnBookmarkControlStarted(station, player, drone.GetIdentifier(), drone);
            }
        }

        private void OnBookmarkControlEnded(ComputerStation station, BasePlayer player, Drone drone)
        {
            if (drone == null)
                return;

            var turret = GetDroneTurret(drone);
            if (turret != null)
            {
                var drone2 = drone;
                var turret2 = turret;

                // Delay in case the drone is hovering.
                NextTick(() =>
                {
                    if (drone2 == null || turret2 == null)
                        return;

                    RefreshAlarmState(drone2, turret2);
                });
            }
        }

        private void OnBookmarkControlEnded(ComputerStation station, BasePlayer player, AutoTurret turret)
        {
            if (turret == null)
                return;

            var drone = GetParentDrone(turret);
            if (drone == null)
                return;

            if (!RCUtils.HasController(drone, player))
                return;

            RCUtils.RemoveController(drone);

            // Notify other plugins such as Drone Hover, Limited Drone Range and Limited Drone Height.
            ExposedHooks.OnBookmarkControlEnded(station, player, drone);
        }

        // This hook is exposed by plugin: Remover Tool (RemoverTool).
        private string canRemove(BasePlayer player, Drone drone)
        {
            if (CanPickupInternal(drone))
                return null;

            return GetMessage(player, Lang.ErrorCannotPickupWithTurret);
        }

        // This hook is exposed by plugin: Drone Settings (DroneSettings).
        private string OnDroneTypeDetermine(Drone drone)
        {
            return GetDroneTurret(drone) != null ? Name : null;
        }

        #endregion

        #region API

        private AutoTurret API_DeployAutoTurret(Drone drone, BasePlayer player)
        {
            if (GetDroneTurret(drone) != null
                || drone.GetSlot(TurretSlot) != null
                || DeployTurretWasBlocked(drone, player))
                return null;

            return DeployAutoTurret(drone, player);
        }

        private NPCAutoTurret API_DeployNpcAutoTurret(Drone drone, BasePlayer player)
        {
            if (GetDroneTurret(drone) != null
                || drone.GetSlot(TurretSlot) != null
                || DeployNpcTurretWasBlocked(drone))
                return null;

            return DeployNpcAutoTurret(drone, player);
        }

        #endregion

        #region Exposed Hooks

        private static class ExposedHooks
        {
            // Oxide hook.
            public static void OnBookmarkControlStarted(ComputerStation station, BasePlayer player, string name, IRemoteControllable controllable)
            {
                Interface.CallHook("OnBookmarkControlStarted", station, player, name, controllable);
            }

            // Oxide hook.
            public static void OnBookmarkControlEnded(ComputerStation station, BasePlayer player, IRemoteControllable controllable)
            {
                Interface.CallHook("OnBookmarkControlEnded", station, player, controllable);
            }

            // Oxide hook.
            public static void OnEntityBuilt(BaseEntity heldEntity, GameObject gameObject)
            {
                Interface.CallHook("OnEntityBuilt", heldEntity, gameObject);
            }

            public static object OnDroneTurretDeploy(Drone drone, BasePlayer deployer)
            {
                return Interface.CallHook("OnDroneTurretDeploy", drone, deployer);
            }

            public static void OnDroneTurretDeployed(Drone drone, AutoTurret turret, BasePlayer deployer)
            {
                Interface.CallHook("OnDroneTurretDeployed", drone, turret, deployer);
            }

            public static object OnDroneNpcTurretDeploy(Drone drone, BasePlayer deployer)
            {
                return Interface.CallHook("OnDroneNpcTurretDeploy", drone, deployer);
            }

            public static void OnDroneNpcTurretDeployed(Drone drone, NPCAutoTurret turret, BasePlayer deployer)
            {
                Interface.CallHook("OnDroneNpcTurretDeployed", drone, turret, deployer);
            }
        }

        #endregion

        #region Commands

        [Command("droneturret")]
        private void DroneTurretCommand(IPlayer player)
        {
            if (player.IsServer)
                return;

            var basePlayer = player.Object as BasePlayer;
            if (!basePlayer.CanInteract())
                return;

            if (!VerifyPermission(player, PermissionDeploy)
                || !VerifyDroneFound(player, out var drone)
                || !VerifyCanBuild(player, drone)
                || !VerifyDroneHasNoTurret(player, drone)
                || !VerifyDroneHasSlotVacant(player, drone))
                return;

            Item autoTurretPaymentItem = null;
            var conditionFraction = 1f;

            if (!player.HasPermission(PermissionDeployFree))
            {
                autoTurretPaymentItem = FindPlayerAutoTurretItem(basePlayer);
                if (autoTurretPaymentItem == null)
                {
                    ReplyToPlayer(player, Lang.ErrorNoTurretItem);
                    return;
                }
                conditionFraction = GetItemConditionFraction(autoTurretPaymentItem);
            }

            if (DeployTurretWasBlocked(drone, basePlayer))
                return;

            if (DeployAutoTurret(drone, basePlayer, conditionFraction) == null)
            {
                ReplyToPlayer(player, Lang.ErrorDeployFailed);
                return;
            }

            if (autoTurretPaymentItem != null)
            {
                UseItem(basePlayer, autoTurretPaymentItem);
            }
        }

        [Command("dronenpcturret")]
        private void DroneNpcTurretCommand(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer)
                return;

            var basePlayer = player.Object as BasePlayer;
            if (!basePlayer.CanInteract())
                return;

            if (!VerifyPermission(player, PermissionDeployNpc)
                || !VerifyDroneFound(player, out var drone)
                || !VerifyCanBuild(player, drone)
                || !VerifyDroneHasNoTurret(player, drone)
                || !VerifyDroneHasSlotVacant(player, drone)
                || DeployNpcTurretWasBlocked(drone, basePlayer))
                return;

            if (DeployNpcAutoTurret(drone, basePlayer) == null)
            {
                ReplyToPlayer(player, Lang.ErrorDeployFailed);
            }
        }

        #endregion

        #region Helper Methods - Command Checks

        private bool VerifyPermission(IPlayer player, string perm)
        {
            if (player.HasPermission(perm))
                return true;

            ReplyToPlayer(player, Lang.ErrorNoPermission);
            return false;
        }

        private bool VerifyDroneFound(IPlayer player, out Drone drone)
        {
            var basePlayer = player.Object as BasePlayer;
            drone = GetLookEntity(basePlayer, 3) as Drone;
            if (drone != null && RCUtils.IsRCDrone(drone))
                return true;

            ReplyToPlayer(player, Lang.ErrorNoDroneFound);
            return false;
        }

        private bool VerifyCanBuild(IPlayer player, Drone drone)
        {
            var basePlayer = player.Object as BasePlayer;
            if (basePlayer.CanBuild() && basePlayer.CanBuild(drone.WorldSpaceBounds()))
                return true;

            ReplyToPlayer(player, Lang.ErrorBuildingBlocked);
            return false;
        }

        private bool VerifyDroneHasNoTurret(IPlayer player, Drone drone)
        {
            if (GetDroneTurret(drone) == null)
                return true;

            ReplyToPlayer(player, Lang.ErrorAlreadyHasTurret);
            return false;
        }

        private bool VerifyDroneHasSlotVacant(IPlayer player, Drone drone)
        {
            if (drone.GetSlot(TurretSlot) == null)
                return true;

            ReplyToPlayer(player, Lang.ErrorIncompatibleAttachment);
            return false;
        }

        #endregion

        #region Helpers

        private static class RCUtils
        {
            public static bool IsRCDrone(Drone drone)
            {
                return drone is not DeliveryDrone;
            }

            public static bool HasController(IRemoteControllable controllable)
            {
                return controllable.ControllingViewerId.HasValue;
            }

            public static bool HasController(IRemoteControllable controllable, BasePlayer player)
            {
                return controllable.ControllingViewerId?.SteamId == player.userID;
            }

            public static bool HasFakeController(IRemoteControllable controllable)
            {
                return controllable.ControllingViewerId?.SteamId == 0;
            }

            public static bool HasRealController(IRemoteControllable controllable)
            {
                return controllable.ControllingViewerId.GetValueOrDefault().SteamId != 0;
            }

            public static bool CanControl(BasePlayer player, IRemoteControllable controllable)
            {
                return !HasRealController(controllable) || HasController(controllable, player);
            }

            public static void RemoveController(IRemoteControllable controllable)
            {
                var controllerId = controllable.ControllingViewerId;
                if (controllerId.HasValue)
                {
                    controllable.StopControl(controllerId.Value);
                }
            }

            public static bool AddViewer(IRemoteControllable controllable, BasePlayer player)
            {
                return controllable.InitializeControl(new CameraViewerId(player.userID, 0));
            }

            public static void RemoveViewer(IRemoteControllable controllable, BasePlayer player)
            {
                controllable.StopControl(new CameraViewerId(player.userID, 0));
            }

            public static bool AddFakeViewer(IRemoteControllable controllable)
            {
                return controllable.InitializeControl(new CameraViewerId());
            }

            public static T GetControlledEntity<T>(BasePlayer player, out ComputerStation station) where T : class
            {
                station = player.GetMounted() as ComputerStation;
                if ((object)station == null)
                    return null;

                return station.currentlyControllingEnt.Get(serverside: true) as T;
            }

            public static T GetControlledEntity<T>(BasePlayer player) where T : class
            {
                ComputerStation station;
                return GetControlledEntity<T>(player, out station);
            }
        }

        private static bool DeployTurretWasBlocked(Drone drone, BasePlayer deployer)
        {
            var hookResult = ExposedHooks.OnDroneTurretDeploy(drone, deployer);
            return hookResult is false;
        }

        private static bool DeployNpcTurretWasBlocked(Drone drone, BasePlayer deployer = null)
        {
            var hookResult = ExposedHooks.OnDroneNpcTurretDeploy(drone, deployer);
            return hookResult is false;
        }

        private static Drone GetParentDrone(BaseEntity entity, out SphereEntity parentSphere)
        {
            parentSphere = entity.GetParentEntity() as SphereEntity;
            return parentSphere != null
                ? parentSphere.GetParentEntity() as Drone
                : null;
        }

        private static Drone GetParentDrone(BaseEntity entity)
        {
            return GetParentDrone(entity, out _);
        }

        private static AutoTurret GetDroneTurret(Drone drone)
        {
            return drone.GetSlot(TurretSlot) as AutoTurret;
        }

        private static T GetChildOfType<T>(BaseEntity entity, string prefabName = null) where T : BaseEntity
        {
            foreach (var child in entity.children)
            {
                var childOfType = child as T;
                if (childOfType != null && (prefabName == null || child.PrefabName == prefabName))
                    return childOfType;
            }

            return null;
        }

        private static IOEntity GetTurretAlarm(AutoTurret turret)
        {
            return GetChildOfType<IOEntity>(turret, AlarmPrefab);
        }

        private static IOEntity GetTurretLight(AutoTurret turret)
        {
            return GetChildOfType<IOEntity>(turret, SirenLightPrefab);
        }

        private static bool ShouldPowerAlarm(Drone drone, AutoTurret turret)
        {
            return drone.IsBeingControlled && (turret.booting || turret.IsOn());
        }

        private static bool CanPickupInternal(Drone drone)
        {
            if (!RCUtils.IsRCDrone(drone))
                return true;

            var turret = GetDroneTurret(drone);
            if (turret == null)
                return true;

            // Prevent drone pickup while it has a turret (the turret must be removed first).
            // Ignores NPC turrets since they can't be picked up.
            return turret == null || turret is NPCAutoTurret;
        }

        private static void HitNotify(BaseEntity entity, HitInfo info)
        {
            var player = info.Initiator as BasePlayer;
            if (player == null)
                return;

            entity.ClientRPCPlayer(null, player, "HitNotify");
        }

        private static SphereEntity SpawnSphereEntity(Drone drone)
        {
            var sphereEntity = GameManager.server.CreateEntity(SpherePrefab, SphereEntityLocalPosition) as SphereEntity;
            if (sphereEntity == null)
                return null;

            SetupSphereEntity(sphereEntity);
            sphereEntity.currentRadius = TurretScale;
            sphereEntity.lerpRadius = TurretScale;
            sphereEntity.SetParent(drone);
            sphereEntity.Spawn();

            return sphereEntity;
        }

        private static void RemoveProblemComponents(BaseEntity entity)
        {
            foreach (var collider in entity.GetComponentsInChildren<Collider>())
            {
                if (!collider.isTrigger)
                {
                    UnityEngine.Object.DestroyImmediate(collider);
                }
            }

            UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());
        }

        private static void AddRigidBodyToTriggerCollider(AutoTurret turret)
        {
            // Without this hack, the drone's sweep test can collide with other entities using the
            // turret trigger collider, causing the drone to occasionally reduce altitude like when
            // it's close to the ground.
            turret.targetTrigger.GetOrAddComponent<Rigidbody>().isKinematic = true;
        }

        private static IOEntity AttachTurretAlarm(Drone drone, AutoTurret turret)
        {
            var turretAlarm = GameManager.server.CreateEntity(AlarmPrefab, new Vector3(0, 0.185f, 0)) as IOEntity;
            if (turretAlarm == null)
                return null;

            turretAlarm.pickup.enabled = false;
            turretAlarm.SetFlag(IOEntity.Flag_HasPower, ShouldPowerAlarm(drone, turret));
            RemoveProblemComponents(turretAlarm);
            HideInputsAndOutputs(turretAlarm);

            turretAlarm.SetParent(turret);
            turretAlarm.Spawn();

            return turretAlarm;
        }

        private static IOEntity AttachTurretLight(Drone drone, AutoTurret turret)
        {
            var turretLight = GameManager.server.CreateEntity(SirenLightPrefab, new Vector3(0, 0.3f, 0), Quaternion.Euler(180, 0, 0)) as IOEntity;
            if (turretLight == null)
                return null;

            turretLight.SetFlag(IOEntity.Flag_HasPower, ShouldPowerAlarm(drone, turret));
            RemoveProblemComponents(turretLight);
            HideInputsAndOutputs(turretLight);

            turretLight.SetParent(turret);
            turretLight.Spawn();

            return turretLight;
        }

        private static ElectricSwitch AttachTurretSwitch(AutoTurret autoTurret)
        {
            var position = autoTurret.transform.TransformPoint(TurretSwitchPosition);
            var rotation = autoTurret.transform.rotation * TurretSwitchRotation;
            var electricSwitch = GameManager.server.CreateEntity(ElectricSwitchPrefab, position, rotation) as ElectricSwitch;
            if (electricSwitch == null)
                return null;

            SetupTurretSwitch(electricSwitch);
            electricSwitch.Spawn();
            electricSwitch.SetParent(autoTurret, true);

            return electricSwitch;
        }

        private static void HideInputsAndOutputs(IOEntity ioEntity)
        {
            // Hide the inputs and outputs on the client.
            foreach (var input in ioEntity.inputs)
            {
                input.type = IOEntity.IOType.Generic;
            }

            foreach (var output in ioEntity.outputs)
            {
                output.type = IOEntity.IOType.Generic;
            }
        }

        private static void SetupTurretSwitch(ElectricSwitch electricSwitch)
        {
            // Damage will be processed by the drone.
            electricSwitch.baseProtection = null;

            electricSwitch.pickup.enabled = false;
            electricSwitch.SetFlag(IOEntity.Flag_HasPower, true);
            RemoveProblemComponents(electricSwitch);
            HideInputsAndOutputs(electricSwitch);

            if (electricSwitch.HasParent())
            {
                var transform = electricSwitch.transform;
                if (transform.localPosition != TurretSwitchPosition)
                {
                    transform.localPosition = TurretSwitchPosition;
                    electricSwitch.InvalidateNetworkCache();
                    electricSwitch.SendNetworkUpdate_Position();
                }
            }
        }

        private static void SetupSphereEntity(SphereEntity sphereEntity)
        {
            sphereEntity.EnableSaving(true);

            // Fix the issue where leaving the area and returning would not recreate the sphere and its children on clients.
            sphereEntity.EnableGlobalBroadcast(false);

            // Needs to be called since we aren't using server side lerping.
            sphereEntity.transform.localScale = SphereTransformScale;
        }

        private static BaseEntity GetLookEntity(BasePlayer basePlayer, float maxDistance = 3)
        {
            return Physics.Raycast(basePlayer.eyes.HeadRay(), out var hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.GetEntity()
                : null;
        }

        private static AutoTurret GetParentTurret(BaseEntity entity)
        {
            return entity.GetParentEntity() as AutoTurret;
        }

        private static void RunOnEntityBuilt(Item turretItem, AutoTurret autoTurret)
        {
            ExposedHooks.OnEntityBuilt(turretItem.GetHeldEntity(), autoTurret.gameObject);
        }

        private static void UseItem(BasePlayer basePlayer, Item item, int amountToConsume = 1)
        {
            item.UseItem(amountToConsume);
            basePlayer.Command("note.inv", item.info.itemid, -amountToConsume);
        }

        private static float GetItemConditionFraction(Item item)
        {
            return item.hasCondition ? item.condition / item.info.condition.max : 1.0f;
        }

        private static Item FindPlayerAutoTurretItem(BasePlayer basePlayer)
        {
            return basePlayer.inventory.FindItemByItemID(AutoTurretItemId);
        }

        private void RefreshDroneSettingsProfile(Drone drone)
        {
            DroneSettings?.Call("API_RefreshDroneProfile", drone);
        }

        private void SwitchControl(BasePlayer player, ComputerStation station, IRemoteControllable previous, IRemoteControllable next)
        {
            var nextEnt = next.GetEnt();
            if (nextEnt == null)
                return;

            station.StopControl(player);
            station.SendNetworkUpdateImmediate();

            var forcedDroneHover = false;
            var drone = previous as Drone ?? next as Drone;

            // Check for a controller, in case Drone Hover has already added a fake one.
            if ((object)drone != null && !RCUtils.HasController(drone))
            {
                RCUtils.AddFakeViewer(drone);
                forcedDroneHover = true;
            }

            NextTick(() =>
            {
                if (player == null || player.IsDestroyed
                    || station == null || station.IsDestroyed
                    || ((previous as BaseEntity)?.IsDestroyed ?? false)
                    || ((next as BaseEntity)?.IsDestroyed ?? false)
                    || station.currentlyControllingEnt.IsValid(serverside: true))
                {
                    if (forcedDroneHover && RCUtils.HasFakeController(drone))
                    {
                        RCUtils.RemoveController(drone);
                    }

                    return;
                }

                RCUtils.RemoveController(next);

                station.currentlyControllingEnt.uid = nextEnt.net.ID;
                station.currentPlayerID = player.userID;
                var isControlling = RCUtils.AddViewer(next, player);
                station.SetFlag(ComputerStation.Flag_HasFullControl, isControlling, networkupdate: false);
                station.SendNetworkUpdateImmediate();
                // station.SendControlBookmarks(player);
                station.InvokeRepeating(station.ControlCheck, 0f, 0f);
                ExposedHooks.OnBookmarkControlStarted(station, player, next.GetIdentifier(), next);
            });
        }

        private bool HasPermissionToControl(BasePlayer player)
        {
            if (!_config.RequirePermission)
                return true;

            return permission.UserHasPermission(player.UserIDString, PermissionControl);
        }

        private void RegisterWithEntityScaleManager(BaseEntity entity)
        {
            EntityScaleManager?.Call("API_RegisterScaledEntity", entity);
        }

        private void RefreshAlarmState(Drone drone, AutoTurret turret)
        {
            if (_config.EnableAudioAlarm)
            {
                var turretAlarm = GetTurretAlarm(turret);
                if (turretAlarm != null)
                {
                    turretAlarm.SetFlag(IOEntity.Flag_HasPower, ShouldPowerAlarm(drone, turret));
                }
            }

            if (_config.EnableSirenLight)
            {
                var turretLight = GetTurretLight(turret);
                if (turretLight != null)
                {
                    turretLight.SetFlag(IOEntity.Flag_HasPower, ShouldPowerAlarm(drone, turret));
                }
            }
        }

        private void SetupDroneTurret(Drone drone, AutoTurret turret)
        {
            // Damage will be processed by the drone.
            turret.baseProtection = null;

            turret.sightRange = _config.TurretRange;
            turret.targetTrigger.GetComponent<SphereCollider>().radius = _config.TurretRange;

            RemoveProblemComponents(turret);
            HideInputsAndOutputs(turret);
            AddRigidBodyToTriggerCollider(turret);

            if (_config.EnableAudioAlarm)
            {
                var turretAlarm = GetTurretAlarm(turret);
                if (turretAlarm != null)
                {
                    turretAlarm.SetFlag(IOEntity.Flag_HasPower, ShouldPowerAlarm(drone, turret));
                }
                else
                {
                    AttachTurretAlarm(drone, turret);
                }
            }

            if (_config.EnableSirenLight)
            {
                var turretLight = GetTurretLight(turret);
                if (turretLight != null)
                {
                    turretLight.SetFlag(IOEntity.Flag_HasPower, ShouldPowerAlarm(drone, turret));
                }
                else
                {
                    AttachTurretLight(drone, turret);
                }
            }

            if (_config.EnableAudioAlarm || _config.EnableSirenLight)
            {
                var droneForClosure = drone;
                var turretForClosure = turret;

                // Delay refreshing the alarm state in case the turret is being automatically powered on.
                NextTick(() =>
                {
                    if (droneForClosure == null || turretForClosure == null)
                        return;

                    RefreshAlarmState(droneForClosure, turretForClosure);
                });
            }

            // Invert the localScale of the turret to compensate for the sphereEntity localScale being increased.
            // Without doing this, the range of the turret corresponds to the sphere scale.
            // This works fine for now because the only colliders remaining are triggers.
            // This will require a different approach if the non-trigger colliders are reintroduced.
            turret.transform.localScale = TurretTransformScale;

            RegisterWithEntityScaleManager(turret);
            RefreshDroneSettingsProfile(drone);
            _turretDroneTracker.Add(drone.net.ID);
        }

        private void RefreshDroneTurret(Drone drone, AutoTurret turret)
        {
            var sphereEntity = turret.GetParentEntity() as SphereEntity;
            if (sphereEntity == null)
                return;

            SetupSphereEntity(sphereEntity);
            SetupDroneTurret(drone, turret);

            var electricSwitch = turret.GetComponentInChildren<ElectricSwitch>();
            if (electricSwitch != null)
            {
                SetupTurretSwitch(electricSwitch);
            }
        }

        private NPCAutoTurret DeployNpcAutoTurret(Drone drone, BasePlayer deployer)
        {
            var sphereEntity = SpawnSphereEntity(drone);
            if (sphereEntity == null)
                return null;

            var turret = GameManager.server.CreateEntity(NpcAutoTurretPrefab) as NPCAutoTurret;
            if (turret == null)
            {
                sphereEntity.Kill();
                return null;
            }

            turret.SetParent(sphereEntity);
            turret.Spawn();

            drone.SetSlot(TurretSlot, turret);
            SetupDroneTurret(drone, turret);

            Effect.server.Run(DeployEffectPrefab, turret.transform.position);
            ExposedHooks.OnDroneNpcTurretDeployed(drone, turret, deployer);

            return turret;
        }

        private AutoTurret DeployAutoTurret(Drone drone, BasePlayer basePlayer, float conditionFraction = 1)
        {
            var sphereEntity = SpawnSphereEntity(drone);
            if (sphereEntity == null)
                return null;

            var turret = GameManager.server.CreateEntity(AutoTurretPrefab) as AutoTurret;
            if (turret == null)
            {
                sphereEntity.Kill();
                return null;
            }

            if (basePlayer != null)
            {
                turret.OwnerID = basePlayer.userID;
            }

            turret.SetFlag(IOEntity.Flag_HasPower, true);
            turret.SetParent(sphereEntity);
            turret.Spawn();
            turret.SetHealth(turret.MaxHealth() * conditionFraction);
            AttachTurretSwitch(turret);

            drone.SetSlot(TurretSlot, turret);
            SetupDroneTurret(drone, turret);

            Effect.server.Run(DeployEffectPrefab, turret.transform.position);
            ExposedHooks.OnDroneTurretDeployed(drone, turret, basePlayer);

            if (basePlayer == null)
                return turret;

            if (!turret.IsAuthed(basePlayer))
            {
                turret.authorizedPlayers.Add(new ProtoBuf.PlayerNameID
                {
                    userid = basePlayer.userID,
                    username = basePlayer.displayName
                });
                turret.SendNetworkUpdate();
            }

            // Allow other plugins to detect the auto turret being deployed (e.g., to add a weapon automatically).
            var turretItem = FindPlayerAutoTurretItem(basePlayer);
            if (turretItem != null)
            {
                RunOnEntityBuilt(turretItem, turret);
            }
            else
            {
                // Temporarily increase the player inventory capacity to ensure there is enough space.
                basePlayer.inventory.containerMain.capacity++;
                var temporaryTurretItem = ItemManager.CreateByItemID(AutoTurretItemId);
                if (basePlayer.inventory.GiveItem(temporaryTurretItem))
                {
                    RunOnEntityBuilt(temporaryTurretItem, turret);
                    temporaryTurretItem.RemoveFromContainer();
                }

                temporaryTurretItem.Remove();
                basePlayer.inventory.containerMain.capacity--;
            }

            return turret;
        }

        private object HandleLightToggle(BasePlayer player)
        {
            var turret = RCUtils.GetControlledEntity<AutoTurret>(player, out _);
            if (turret == null || !RCUtils.HasController(turret, player))
                return null;

            var weapon = turret.GetAttachedWeapon();
            if (weapon == null)
                return null;

            weapon.SetLightsOn(!weapon.LightsOn());
            return False;
        }

        private void HandleSwapSeats(BasePlayer player)
        {
            var controllable = RCUtils.GetControlledEntity<IRemoteControllable>(player, out var station);
            if (controllable == null)
                return;

            AutoTurret turret;
            var drone = controllable as Drone;
            if (drone != null)
            {
                turret = GetDroneTurret(drone);
                if (turret != null && turret.IsOn() && !turret.PeacekeeperMode() && turret is not NPCAutoTurret)
                {
                    SwitchControl(player, station, drone, turret);
                }

                return;
            }

            turret = controllable as AutoTurret;
            if (turret != null)
            {
                drone = GetParentDrone(turret);
                if (drone != null)
                {
                    SwitchControl(player, station, turret, drone);
                }

                return;
            }
        }

        #endregion

        #region Dynamic Hook Subscriptions

        private class DynamicHookSubscriber<T>
        {
            private DroneTurrets _plugin;
            private HashSet<T> _list = new();
            private string[] _hookNames;

            public DynamicHookSubscriber(DroneTurrets plugin, params string[] hookNames)
            {
                _plugin = plugin;
                _hookNames = hookNames;
            }

            public void Add(T item)
            {
                if (_list.Add(item) && _list.Count == 1)
                {
                    SubscribeAll();
                }
            }

            public void Remove(T item)
            {
                if (_list.Remove(item) && _list.Count == 0)
                {
                    UnsubscribeAll();
                }
            }

            public void SubscribeAll()
            {
                foreach (var hookName in _hookNames)
                {
                    _plugin.Subscribe(hookName);
                }
            }

            public void UnsubscribeAll()
            {
                foreach (var hookName in _hookNames)
                {
                    _plugin.Unsubscribe(hookName);
                }
            }
        }

        #endregion

        #region Drone Controller

        private class DroneController : ListComponent<DroneController>
        {
            public static void AddToDrone(Drone drone, AutoTurret turret, BasePlayer player)
            {
                var component = drone.gameObject.GetComponent<DroneController>();
                if (component == null)
                {
                    component = drone.gameObject.AddComponent<DroneController>();
                    component._drone = drone;
                    component._turret = turret;
                }
                else
                {
                    component.enabled = true;
                }

                component._controller = player;
                component._viewerId = new CameraViewerId(player.userID, 0);
            }

            public static void DestroyAll()
            {
                foreach (var component in InstanceList.ToArray())
                {
                    DestroyImmediate(component);
                }
            }

            private Drone _drone;
            private AutoTurret _turret;
            private BasePlayer _controller;
            private CameraViewerId _viewerId;

            private void Update()
            {
                if (!RCUtils.HasController(_turret, _controller)
                    || RCUtils.GetControlledEntity<AutoTurret>(_controller) != _turret)
                {
                    enabled = false;
                    return;
                }

                // Optimization: Skip if there was no user input this frame.
                if (_controller.lastTickTime < Time.time)
                    return;

                // Send only keyboard inputs to the drone.
                var input = _controller.serverInput;
                var originalMouseDelta = input.current.mouseDelta;
                input.current.mouseDelta = Vector3.zero;
                _drone.UserInput(input, _viewerId);
                input.current.mouseDelta = originalMouseDelta;

                if (_drone.currentInput.movement == Vector3.zero)
                    return;

                // Rotate the drone movement direction by the turret aim direction.
                var worldDirection = _drone.transform.InverseTransformVector(_drone.currentInput.movement);
                var turretRotation = Quaternion.Euler(0, Quaternion.LookRotation(_turret.aimDir).eulerAngles.y, 0);
                _drone.currentInput.movement = turretRotation * worldDirection;
            }

            private void OnDestroy()
            {
                if (RCUtils.HasController(_drone, _controller))
                {
                    RCUtils.RemoveController(_drone);
                }
            }
        }

        #endregion

        #region Configuration

        private class Configuration : BaseConfiguration
        {
            [JsonProperty("RequirePermissionToControlDroneTurrets")]
            public bool RequirePermission;

            [JsonProperty("TargetPlayers")]
            public bool TargetPlayers = true;

            [JsonProperty("TargetNPCs")]
            public bool TargetNPCs = true;

            [JsonProperty("TargetAnimals")]
            public bool TargetAnimals = true;

            [JsonProperty("EnableAudioAlarm")]
            public bool EnableAudioAlarm = false;

            [JsonProperty("EnableSirenLight")]
            public bool EnableSirenLight = false;

            [JsonProperty("TurretRange")]
            public float TurretRange = 30f;

            [JsonProperty("TipChance")]
            public int TipChance = 25;
        }

        private Configuration GetDefaultConfig() => new();

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

        #region Localization

        private void ReplyToPlayer(IPlayer player, string messageName, params object[] args) =>
            player.Reply(string.Format(GetMessage(player, messageName), args));

        private void ChatMessage(BasePlayer player, string messageName, params object[] args) =>
            player.ChatMessage(string.Format(GetMessage(player.IPlayer, messageName), args));

        private string GetMessage(IPlayer player, string messageName, params object[] args) =>
            GetMessage(player.Id, messageName, args);

        private string GetMessage(BasePlayer player, string messageName, params object[] args) =>
            GetMessage(player.UserIDString, messageName, args);

        private string GetMessage(string playerId, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, playerId);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        private class Lang
        {
            public const string TipDeployCommand = "Tip.DeployCommand";
            public const string ErrorNoPermission = "Error.NoPermission";
            public const string ErrorNoDroneFound = "Error.NoDroneFound";
            public const string ErrorBuildingBlocked = "Error.BuildingBlocked";
            public const string ErrorNoTurretItem = "Error.NoTurretItem";
            public const string ErrorAlreadyHasTurret = "Error.AlreadyHasTurret";
            public const string ErrorIncompatibleAttachment = "Error.IncompatibleAttachment";
            public const string ErrorDeployFailed = "Error.DeployFailed";
            public const string ErrorCannotPickupWithTurret = "Error.CannotPickupWithTurret";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.TipDeployCommand] = "Tip: Look at the drone and run <color=yellow>/droneturret</color> to deploy a turret.",
                [Lang.ErrorNoPermission] = "You don't have permission to do that.",
                [Lang.ErrorNoDroneFound] = "Error: No drone found.",
                [Lang.ErrorBuildingBlocked] = "Error: Cannot do that while building blocked.",
                [Lang.ErrorNoTurretItem] = "Error: You need an auto turret to do that.",
                [Lang.ErrorAlreadyHasTurret] = "Error: That drone already has a turret.",
                [Lang.ErrorIncompatibleAttachment] = "Error: That drone has an incompatible attachment.",
                [Lang.ErrorDeployFailed] = "Error: Failed to deploy turret.",
                [Lang.ErrorCannotPickupWithTurret] = "Cannot pick up that drone while it has a turret.",
            }, this, "en");
            //Add pt-BR
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.TipDeployCommand] = "Dica: olhe para o drone e execute <color=yellow>/droneturret</color> para implantar uma torre.",
                [Lang.ErrorNoPermission] = "Você não tem permissão para fazer isso.",
                [Lang.ErrorNoDroneFound] = "Erro: Nenhum drone encontrado.",
                [Lang.ErrorBuildingBlocked] = "Erro: Não é possível fazer isso enquanto o edifício está bloqueado.",
                [Lang.ErrorNoTurretItem] = "Erro: você precisa de uma torre automática para fazer isso.",
                [Lang.ErrorAlreadyHasTurret] = "Erro: esse drone já tem uma torre.",
                [Lang.ErrorIncompatibleAttachment] = "Erro: esse drone tem um anexo incompatível.",
                [Lang.ErrorDeployFailed] = "Erro: falha ao implantar a torre.",
                [Lang.ErrorCannotPickupWithTurret] = "Não é possível pegar aquele drone enquanto ele tiver uma torre.",
            }, this, "pt-BR");
        }

        #endregion
    }
}
