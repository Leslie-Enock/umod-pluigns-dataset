using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust.Modular;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Car Turrets", "WhiteThunder", "1.6.3")]
    [Description("Allows players to deploy auto turrets onto modular cars.")]
    internal class CarTurrets : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private readonly Plugin VehicleDeployedLocks;

        private Configuration _config;

        private const string PermissionDeployCommand = "carturrets.deploy.command";
        private const string PermissionDeployInventory = "carturrets.deploy.inventory";
        private const string PermissionFree = "carturrets.free";
        private const string PermissionControl = "carturrets.control";
        private const string PermissionRemoveAll = "carturrets.removeall";

        private const string PermissionLimit2 = "carturrets.limit.2";
        private const string PermissionLimit3 = "carturrets.limit.3";
        private const string PermissionLimit4 = "carturrets.limit.4";

        private const string PermissionSpawnWithCar = "carturrets.spawnwithcar";

        private const string PermissionAllModules = "carturrets.allmodules";
        private const string PermissionModuleFormat = "carturrets.{0}";

        private const string PrefabEntityAutoTurret = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
        private const string PrefabEntityElectricSwitch = "assets/prefabs/deployable/playerioents/simpleswitch/switch.prefab";
        private const string PrefabEffectDeployAutoTurret = "assets/prefabs/npc/autoturret/effects/autoturret-deploy.prefab";
        private const string PrefabEffectCodeLockDenied = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";

        private const int ItemIdAutoTurret = -2139580305;

        private static readonly Vector3 TurretSwitchPosition = new Vector3(0, 0.36f, -0.32f);
        private static readonly Quaternion TurretBackwardRotation = Quaternion.Euler(0, 180, 0);
        private static readonly Quaternion TurretSwitchRotation = Quaternion.Euler(0, 180, 0);

        private readonly object False = false;

        private DynamicHookSubscriber<NetworkableId> _carTurretTracker;
        private ProtectionProperties ImmortalProtection;

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionDeployCommand, this);
            permission.RegisterPermission(PermissionDeployInventory, this);
            permission.RegisterPermission(PermissionFree, this);
            permission.RegisterPermission(PermissionControl, this);
            permission.RegisterPermission(PermissionRemoveAll, this);

            permission.RegisterPermission(PermissionLimit2, this);
            permission.RegisterPermission(PermissionLimit3, this);
            permission.RegisterPermission(PermissionLimit4, this);

            permission.RegisterPermission(PermissionSpawnWithCar, this);

            permission.RegisterPermission(PermissionAllModules, this);
            foreach (var moduleItemShortName in _config.ModulePositions.Keys)
            {
                permission.RegisterPermission(GetAutoTurretPermission(moduleItemShortName), this);
            }

            Unsubscribe(nameof(OnEntitySpawned));

            var dynamicHookNames = new List<string>()
            {
                nameof(OnItemDropped),
                nameof(OnEntityKill),
                nameof(OnSwitchToggle),
                nameof(OnSwitchToggled),
                nameof(OnTurretTarget),
            };

            if (_config.EnableTurretPickup)
            {
                Unsubscribe(nameof(CanPickupEntity));
                Unsubscribe(nameof(canRemove));
            }
            else
            {
                dynamicHookNames.Add(nameof(CanPickupEntity));
                dynamicHookNames.Add(nameof(canRemove));
            }

            if (!_config.OnlyPowerTurretsWhileEngineIsOn)
            {
                Unsubscribe(nameof(OnEngineStartFinished));
                Unsubscribe(nameof(OnEngineStopped));
                Unsubscribe(nameof(OnTurretStartup));
            }
            else
            {
                dynamicHookNames.Add(nameof(OnEngineStartFinished));
                dynamicHookNames.Add(nameof(OnEngineStopped));
                dynamicHookNames.Add(nameof(OnTurretStartup));
            }

            if (!_config.RequirePermissionToControl)
            {
                Unsubscribe(nameof(OnBookmarkControlStarted));
            }
            else
            {
                dynamicHookNames.Add(nameof(OnBookmarkControlStarted));
            }

            _carTurretTracker = new DynamicHookSubscriber<NetworkableId>(this, dynamicHookNames.ToArray());
            _carTurretTracker.UnsubscribeAll();
        }

        private void Unload()
        {
            UnityEngine.Object.Destroy(ImmortalProtection);
        }

        private void OnServerInitialized()
        {
            ImmortalProtection = ScriptableObject.CreateInstance<ProtectionProperties>();
            ImmortalProtection.name = "CarTurretsSwitchProtection";
            ImmortalProtection.Add(1);

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var car = entity as ModularCar;
                if (car == null)
                    continue;

                foreach (var module in car.AttachedModuleEntities)
                {
                    var turret = GetModuleAutoTurret(module);
                    if (turret == null)
                        continue;

                    RefreshCarTurret(turret);
                }

                if (_config.OnlyPowerTurretsWhileEngineIsOn)
                {
                    if (car.IsOn())
                    {
                        OnEngineStartFinished(car);
                    }
                    else
                    {
                        OnEngineStopped(car);
                    }
                }
            }

            if (_config.SpawnWithCarConfig.Enabled)
            {
                Subscribe(nameof(OnEntitySpawned));
            }
        }

        private void OnEntitySpawned(ModularCar car)
        {
            if (!ShouldSpawnTurretsWithCar(car))
                return;

            // Intentionally using both NextTick and Invoke.
            // Using NextTick to delay until the items have been added to the module inventory.
            // Using Invoke since that's what the game uses to delay spawning module entities.
            NextTick(() =>
            {
                if (car == null)
                    return;

                car.Invoke(() =>
                {
                    var ownerIdString = car.OwnerID != 0 ? car.OwnerID.ToString() : string.Empty;
                    var ownerPlayer = FindEntityOwner(car);

                    var allowedTurretsRemaining = GetCarAutoTurretLimit(car);
                    for (var i = 0; i < car.AttachedModuleEntities.Count && allowedTurretsRemaining > 0; i++)
                    {
                        var vehicleModule = car.AttachedModuleEntities[i];

                        Vector3 position;
                        if (!TryGetAutoTurretPositionForModule(vehicleModule, out position) ||
                            GetModuleAutoTurret(vehicleModule) != null ||
                            ownerIdString != string.Empty && !HasPermissionToVehicleModule(ownerIdString, vehicleModule) ||
                            UnityEngine.Random.Range(0, 100) >= GetAutoTurretChanceForModule(vehicleModule) ||
                            DeployWasBlocked(vehicleModule, ownerPlayer, automatedDeployment: true))
                            continue;

                        if (ownerPlayer == null)
                            DeployAutoTurret(car, vehicleModule, position);
                        else
                            DeployAutoTurretForPlayer(car, vehicleModule, position, ownerPlayer);

                        allowedTurretsRemaining--;
                    }
                }, 0);
            });
        }

        private object CanMoveItem(Item item, PlayerInventory playerInventory, ItemContainerId targetContainerId, int targetSlot, int amount)
        {
            if (item == null || playerInventory == null)
                return null;

            var basePlayer = playerInventory.baseEntity;
            if (basePlayer == null)
                return null;

            if (item.parent == null || item.parent.uid == targetContainerId)
                return null;

            if (playerInventory.loot.containers.Contains(item.parent))
            {
                // Player is moving an item from the loot panel.
                var fromCar = item.parent.entityOwner as ModularCar;
                if (fromCar == null)
                    return null;

                return HandleRemoveTurret(basePlayer, item, fromCar);
            }

            // Player is moving an item to the loot panel (module inventory is at position 1).
            var targetContainer = targetContainerId.Value != 0
                ? playerInventory.loot.FindContainer(targetContainerId)
                : playerInventory.loot.containers.ElementAtOrDefault(1);

            var toCar = targetContainer?.entityOwner as ModularCar;
            if ((object)toCar == null)
                return null;

            return HandleAddTurret(basePlayer, item, toCar, targetContainer, targetSlot);
        }

        private object HandleAddTurret(BasePlayer basePlayer, Item item, ModularCar car, ItemContainer targetContainer, int targetSlot)
        {
            var player = basePlayer.IPlayer;

            var itemid = item.info.itemid;
            if (itemid != ItemIdAutoTurret)
                return null;

            // In case a future update or a plugin adds another storage container to the car.
            if (car.Inventory.ModuleContainer != targetContainer)
                return null;

            if (!player.HasPermission(PermissionDeployInventory))
            {
                ChatMessage(basePlayer, Lang.GenericErrorNoPermission);
                return null;
            }

            if (!VerifyCarHasAutoTurretCapacity(player, car, replyInChat: true))
                return null;

            if (targetSlot == -1)
            {
                targetSlot = FindFirstSuitableSocketIndex(car, basePlayer);
            }

            if (targetSlot == -1)
            {
                ChatMessage(basePlayer, Lang.DeployErrorNoSuitableModule);
                return null;
            }

            var moduleItem = targetContainer.GetSlot(targetSlot);
            if (moduleItem == null)
                return null;

            var vehicleModule = car.GetModuleForItem(moduleItem);
            if (vehicleModule == null)
                return null;

            if (!HasPermissionToVehicleModule(player.Id, vehicleModule))
            {
                ChatMessage(basePlayer, Lang.DeployErrorNoPermissionToModule);
                return null;
            }

            if (GetModuleAutoTurret(vehicleModule) != null)
            {
                ChatMessage(basePlayer, Lang.DeployErrorModuleAlreadyHasTurret);
                return null;
            }

            Vector3 position;
            if (!TryGetAutoTurretPositionForModule(vehicleModule, out position)
                || DeployWasBlocked(vehicleModule, basePlayer))
                return null;

            if (DeployAutoTurretForPlayer(car, vehicleModule, position, basePlayer, GetItemConditionFraction(item)) == null)
                return null;

            if (!player.HasPermission(PermissionFree))
            {
                UseItem(basePlayer, item);
            }

            return False;
        }

        private object HandleRemoveTurret(BasePlayer basePlayer, Item moduleItem, ModularCar car, ItemContainer targetContainer = null)
        {
            if (car.Inventory.ModuleContainer != moduleItem.parent)
                return null;

            var vehicleModule = car.GetModuleForItem(moduleItem);
            if (vehicleModule == null)
                return null;

            var autoTurret = GetModuleAutoTurret(vehicleModule);
            if (autoTurret == null)
                return null;

            if (_config.EnableTurretPickup && autoTurret.pickup.enabled)
            {
                if (autoTurret.pickup.requireEmptyInv && !autoTurret.inventory.IsEmpty() && !autoTurret.inventory.IsLocked())
                {
                    ChatMessage(basePlayer, Lang.RemoveErrorTurretHasItems);
                    return False;
                }

                var turretItem = ItemManager.CreateByItemID(ItemIdAutoTurret);
                if (turretItem == null)
                    return null;

                if (turretItem.info.condition.enabled)
                {
                    turretItem.condition = autoTurret.healthFraction * 100;
                }

                if (targetContainer == null)
                {
                    if (!basePlayer.inventory.GiveItem(turretItem))
                    {
                        turretItem.Remove();
                        return False;
                    }
                }
                else if (!turretItem.MoveToContainer(targetContainer))
                {
                    turretItem.Remove();
                    return False;
                }

                basePlayer.Command("note.inv", ItemIdAutoTurret, 1);
            }

            autoTurret.Kill();
            return null;
        }

        private void OnItemDropped(Item item, BaseEntity itemEntity)
        {
            if (item?.parent == null)
                return;

            var car = item.parent.entityOwner as ModularCar;
            if (car == null)
                return;

            if (item.info.GetComponent<ItemModVehicleModule>() == null)
                return;

            var vehicleModule = car.GetModuleForItem(item);
            if (vehicleModule == null)
                return;

            var autoTurret = GetModuleAutoTurret(vehicleModule);
            if (autoTurret == null)
                return;

            if (_config.EnableTurretPickup && autoTurret.pickup.enabled)
            {
                var turretItem = CreateItemFromAutoTurret(autoTurret);
                if (turretItem == null)
                    return;

                var rigidBody = itemEntity.GetComponent<Rigidbody>();
                turretItem.Drop(itemEntity.transform.position, rigidBody?.velocity ?? Vector3.zero, itemEntity.transform.rotation);
            }
        }

        // Automatically move a deployed turret when a module moves.
        // This is not done in the CanMoveItem hook since we don't know if it's being moved yet.
        private void OnEntityKill(BaseVehicleModule vehicleModule)
        {
            var moduleItem = vehicleModule.AssociatedItemInstance;
            if (moduleItem == null)
                return;

            var car = vehicleModule.Vehicle as ModularCar;
            if (car == null)
                return;

            var autoTurret = GetModuleAutoTurret(vehicleModule);
            if (autoTurret == null)
                return;

            autoTurret.SetParent(null);

            var moduleItem2 = moduleItem;
            var car2 = car;
            var autoTurret2 = autoTurret;

            NextTick(() =>
            {
                if (car2 == null)
                {
                    autoTurret2.Kill();
                }
                else
                {
                    var newModule = car2.GetModuleForItem(moduleItem2);
                    if (newModule == null)
                    {
                        autoTurret2.Kill();
                    }
                    else
                    {
                        autoTurret2.SetParent(newModule);
                    }
                }
            });
        }

        private void OnEntityKill(AutoTurret turret)
        {
            _carTurretTracker.Remove(turret.net.ID);
        }

        private object OnSwitchToggle(ElectricSwitch electricSwitch, BasePlayer player)
        {
            var turret = GetParentTurret(electricSwitch);
            if (turret == null)
                return null;

            var vehicleModule = GetParentVehicleModule(turret);
            if (vehicleModule == null)
                return null;

            var car = vehicleModule.Vehicle as ModularCar;
            if (car == null)
                return null;

            if (!player.CanBuild())
            {
                // Disallow switching the turret on and off while building blocked.
                Effect.server.Run(PrefabEffectCodeLockDenied, electricSwitch, 0, Vector3.zero, Vector3.forward);
                return False;
            }

            return null;
        }

        private void OnSwitchToggled(ElectricSwitch electricSwitch, BasePlayer player)
        {
            var turret = GetParentTurret(electricSwitch);
            if (turret == null)
                return;

            var vehicleModule = GetParentVehicleModule(turret);
            if (vehicleModule == null)
                return;

            var car = vehicleModule.Vehicle as ModularCar;
            if (car == null)
                return;

            if (electricSwitch.IsOn())
            {
                if (_config.OnlyPowerTurretsWhileEngineIsOn && !car.IsOn())
                {
                    ChatMessage(player, Lang.InfoPowerRequiresEngine);
                }
                else
                {
                    turret.InitiateStartup();
                }
            }
            else
            {
                turret.InitiateShutdown();
            }
        }

        private object OnTurretTarget(AutoTurret turret, BaseCombatEntity target)
        {
            if (turret == null || target == null || GetParentVehicleModule(turret) == null)
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

        private void OnBookmarkControlStarted(ComputerStation station, BasePlayer player, string bookmarkName, AutoTurret turret)
        {
            var vehicleModule = GetParentVehicleModule(turret);
            if (vehicleModule == null)
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
            }
        }

        // This is only subscribed while config option EnableTurretPickup is false.
        private object CanPickupEntity(BasePlayer player, AutoTurret turret)
        {
            if (GetParentVehicleModule(turret) != null)
                return False;

            return null;
        }

        // This hook is exposed by plugin: Remover Tool (RemoverTool).
        // Only subscribed while config option EnableTurretPickup is false.
        private object canRemove(BasePlayer player, AutoTurret turret)
        {
            if (GetParentVehicleModule(turret) != null)
                return False;

            return null;
        }

        // This is only subscribed while OnlyPowerTurretsWhileEngineIsOn is true.
        private void OnEngineStartFinished(ModularCar car)
        {
            foreach (var module in car.AttachedModuleEntities)
            {
                var turret = GetModuleAutoTurret(module);
                if (turret == null || turret.booting || turret.IsOn())
                    continue;

                var electricSwitch = GetTurretSwitch(turret);
                if (electricSwitch == null || !electricSwitch.IsOn())
                    continue;

                turret.InitiateStartup();
            }
        }

        // This is only subscribed while OnlyPowerTurretsWhileEngineIsOn is true.
        private void OnEngineStopped(ModularCar car)
        {
            foreach (var module in car.AttachedModuleEntities)
            {
                var turret = GetModuleAutoTurret(module);
                if (turret == null || !turret.booting && !turret.IsOn())
                    continue;

                var electricSwitch = GetTurretSwitch(turret);
                if (electricSwitch == null)
                    continue;

                turret.InitiateShutdown();
            }
        }

        // This is only subscribed while OnlyPowerTurretsWhileEngineIsOn is true.
        private object OnTurretStartup(AutoTurret turret)
        {
            var module = GetParentVehicleModule(turret);
            if (module == null)
                return null;

            var car = module.Vehicle as ModularCar;
            if (car == null)
                return null;

            if (!car.IsOn())
                return False;

            return null;
        }

        #endregion

        #region API

        [HookMethod(nameof(API_DeployAutoTurret))]
        public AutoTurret API_DeployAutoTurret(BaseVehicleModule vehicleModule, BasePlayer basePlayer)
        {
            var car = vehicleModule.Vehicle as ModularCar;
            if (car == null)
                return null;

            Vector3 position;
            if (!TryGetAutoTurretPositionForModule(vehicleModule, out position) ||
                GetModuleAutoTurret(vehicleModule) != null ||
                DeployWasBlocked(vehicleModule, basePlayer))
                return null;

            return basePlayer == null
                ? DeployAutoTurret(car, vehicleModule, position)
                : DeployAutoTurretForPlayer(car, vehicleModule, position, basePlayer);
        }

        #endregion

        #region Commands

        [Command("carturret")]
        private void CommandDeploy(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer || !VerifyPermissionAny(player, PermissionDeployCommand))
                return;

            var basePlayer = player.Object as BasePlayer;
            ModularCar car;
            BaseVehicleModule vehicleModule;

            if (!VerifyCanBuild(player) ||
                !VerifyVehicleModuleFound(player, out car, out vehicleModule) ||
                !CanAccessVehicle(car, basePlayer) ||
                !VerifyCarHasAutoTurretCapacity(player, car) ||
                !VerifyPermissionToModule(player, vehicleModule))
                return;

            if (GetModuleAutoTurret(vehicleModule) != null)
            {
                ReplyToPlayer(player, Lang.DeployErrorModuleAlreadyHasTurret);
                return;
            }

            Vector3 position;
            if (!TryGetAutoTurretPositionForModule(vehicleModule, out position))
            {
                ReplyToPlayer(player, Lang.DeployErrorUnsupportedModule);
                return;
            }

            Item autoTurretItem = null;
            var conditionFraction = 1.0f;

            var isFree = player.HasPermission(PermissionFree);
            if (!isFree)
            {
                autoTurretItem = FindPlayerAutoTurretItem(basePlayer);
                if (autoTurretItem == null)
                {
                    ReplyToPlayer(player, Lang.DeployErrorNoTurret);
                    return;
                }

                conditionFraction = GetItemConditionFraction(autoTurretItem);
            }

            if (DeployWasBlocked(vehicleModule, basePlayer))
                return;

            if (DeployAutoTurretForPlayer(car, vehicleModule, position, basePlayer, conditionFraction) != null && !isFree && autoTurretItem != null)
            {
                UseItem(basePlayer, autoTurretItem);
            }
        }

        [Command("carturrets.removeall")]
        private void CommandRemoveAllCarTurrets(IPlayer player, string cmd, string[] args)
        {
            if (!player.IsServer && !VerifyPermissionAny(player, PermissionRemoveAll))
                return;

            var turretsRemoved = 0;
            foreach (var turret in BaseNetworkable.serverEntities.OfType<AutoTurret>().ToArray())
            {
                if (turret.GetParentEntity() is BaseVehicleModule)
                {
                    turret.Kill();
                    turretsRemoved++;
                }
            }

            ReplyToPlayer(player, Lang.RemoveAllSuccess, turretsRemoved);
        }

        #endregion

        #region Helper Methods - Command Checks

        private bool VerifyPermissionAny(IPlayer player, params string[] permissionNames)
        {
            foreach (var perm in permissionNames)
            {
                if (player.HasPermission(perm))
                    return true;
            }

            ReplyToPlayer(player, Lang.GenericErrorNoPermission);
            return false;
        }

        private bool VerifyCanBuild(IPlayer player)
        {
            if ((player.Object as BasePlayer).CanBuild())
                return true;

            ReplyToPlayer(player, Lang.GenericErrorBuildingBlocked);
            return false;
        }

        private bool VerifyVehicleModuleFound(IPlayer player, out ModularCar car, out BaseVehicleModule vehicleModule)
        {
            var basePlayer = player.Object as BasePlayer;
            var entity = GetLookEntity(basePlayer);

            vehicleModule = entity as BaseVehicleModule;
            if (vehicleModule != null)
            {
                car = vehicleModule.Vehicle as ModularCar;
                if (car != null)
                    return true;

                ReplyToPlayer(player, Lang.DeployErrorNoCarFound);
                return false;
            }

            car = entity as ModularCar;
            if (car == null)
            {
                var lift = entity as ModularCarGarage;
                car = lift?.carOccupant;
                if (car == null)
                {
                    ReplyToPlayer(player, Lang.DeployErrorNoCarFound);
                    return false;
                }
            }

            var closestModule = FindClosestModuleToAim(car, basePlayer);
            if (closestModule != null)
            {
                vehicleModule = closestModule;
                return true;
            }

            ReplyToPlayer(player, Lang.DeployErrorNoModules);
            return false;
        }

        private bool VerifyCarHasAutoTurretCapacity(IPlayer player, ModularCar car, bool replyInChat = false)
        {
            var limit = GetCarAutoTurretLimit(car);
            if (GetCarTurretCount(car) < limit)
                return true;

            if (replyInChat)
            {
                ChatMessage(player.Object as BasePlayer, Lang.DeployErrorTurretLimit, limit);
            }
            else
            {
                ReplyToPlayer(player, Lang.DeployErrorTurretLimit, limit);
            }

            return false;
        }

        private bool VerifyPermissionToModule(IPlayer player, BaseVehicleModule vehicleModule)
        {
            if (HasPermissionToVehicleModule(player.Id, vehicleModule))
                return true;

            ReplyToPlayer(player, Lang.DeployErrorNoPermissionToModule);
            return false;
        }

        #endregion

        #region Helpers

        private static class RCUtils
        {
            public static bool HasController(IRemoteControllable controllable, BasePlayer player)
            {
                return controllable.ControllingViewerId?.SteamId == player.userID;
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

            public static bool AddFakeViewer(IRemoteControllable controllable)
            {
                return controllable.InitializeControl(new CameraViewerId());
            }
        }

        private static bool DeployWasBlocked(BaseVehicleModule vehicleModule, BasePlayer basePlayer, bool automatedDeployment = false)
        {
            var hookResult = Interface.CallHook("OnCarAutoTurretDeploy", vehicleModule, basePlayer, automatedDeployment);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static BaseVehicleModule FindClosestModuleToAim(ModularCar car, BasePlayer basePlayer)
        {
            var headRay = basePlayer.eyes.HeadRay();

            BaseVehicleModule closestModule = null;
            float closestDistance = 0;

            for (var socketIndex = 0; socketIndex < car.TotalSockets; socketIndex++)
            {
                BaseVehicleModule currentModule;
                if (car.TryGetModuleAt(socketIndex, out currentModule) && currentModule.FirstSocketIndex == socketIndex)
                {
                    var currentDistance = Vector3.Cross(headRay.direction, currentModule.CenterPoint() - headRay.origin).magnitude;
                    if (ReferenceEquals(closestModule, null))
                    {
                        closestModule = currentModule;
                        closestDistance = currentDistance;
                    }
                    else if (currentDistance < closestDistance)
                    {
                        closestModule = currentModule;
                        closestDistance = currentDistance;
                    }
                }
            }

            return closestModule;
        }

        private static void UseItem(BasePlayer basePlayer, Item item, int amountToConsume = 1)
        {
            item.amount -= amountToConsume;
            if (item.amount <= 0)
            {
                item.RemoveFromContainer();
                item.Remove();
            }
            else
            {
                item.MarkDirty();
            }

            basePlayer.Command("note.inv", item.info.itemid, -amountToConsume);
        }

        private static float GetItemConditionFraction(Item item)
        {
            return item.hasCondition ? item.condition / item.info.condition.max : 1.0f;
        }

        private static Item FindPlayerAutoTurretItem(BasePlayer basePlayer)
        {
            return basePlayer.inventory.FindItemByItemID(ItemIdAutoTurret);
        }

        private static Item CreateItemFromAutoTurret(AutoTurret autoTurret)
        {
            var turretItem = ItemManager.CreateByItemID(ItemIdAutoTurret);
            if (turretItem == null)
                return null;

            if (turretItem.info.condition.enabled)
            {
                turretItem.condition = autoTurret.healthFraction * 100;
            }

            return turretItem;
        }

        private static string GetAutoTurretPermissionForModule(BaseVehicleModule vehicleModule)
        {
            return GetAutoTurretPermission(vehicleModule.AssociatedItemDef.shortname);
        }

        private static string GetAutoTurretPermission(string moduleItemShortName)
        {
            return string.Format(PermissionModuleFormat, moduleItemShortName);
        }

        private static int GetCarTurretCount(ModularCar car)
        {
            var numTurrets = 0;
            foreach (var module in car.AttachedModuleEntities)
            {
                var turret = GetModuleAutoTurret(module);
                if (turret != null)
                {
                    numTurrets++;
                }
            }

            return numTurrets;
        }

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

        private static AutoTurret GetModuleAutoTurret(BaseVehicleModule vehicleModule)
        {
            return GetChildOfType<AutoTurret>(vehicleModule);
        }

        private static ElectricSwitch GetTurretSwitch(AutoTurret turret)
        {
            return GetChildOfType<ElectricSwitch>(turret);
        }

        private static bool IsNaturalCarSpawn(ModularCar car)
        {
            var spawnable = car.GetComponent<Spawnable>();
            return spawnable != null && spawnable.Population != null;
        }

        private static BaseVehicleModule GetParentVehicleModule(BaseEntity entity)
        {
            return entity.GetParentEntity() as BaseVehicleModule;
        }

        private static AutoTurret GetParentTurret(BaseEntity entity)
        {
            return entity.GetParentEntity() as AutoTurret;
        }

        private static void RunOnEntityBuilt(Item turretItem, AutoTurret autoTurret)
        {
            Interface.CallHook("OnEntityBuilt", turretItem.GetHeldEntity(), autoTurret.gameObject);
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

        private static Quaternion GetIdealTurretRotation(ModularCar car, BaseVehicleModule vehicleModule)
        {
            var lastSocketIndex = vehicleModule.FirstSocketIndex + vehicleModule.GetNumSocketsTaken() - 1;

            var faceForward = car.TotalSockets == 2
                ? vehicleModule.FirstSocketIndex == 0
                : car.TotalSockets == 3
                ? lastSocketIndex <= 1
                : vehicleModule.FirstSocketIndex <= 1;

            return faceForward ? Quaternion.identity : TurretBackwardRotation;
        }

        private static void RemoveColliders<T>(BaseEntity entity) where T : Collider
        {
            foreach (var collider in entity.GetComponentsInChildren<T>())
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }
        }

        private static void RemoveGroundWatch(BaseEntity entity)
        {
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());
            UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());
        }

        private static BaseEntity GetLookEntity(BasePlayer basePlayer, float maxDistance = 3)
        {
            RaycastHit hit;
            return Physics.Raycast(basePlayer.eyes.HeadRay(), out hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.GetEntity()
                : null;
        }

        private static BasePlayer FindEntityOwner(BaseEntity entity)
        {
            return entity.OwnerID != 0 ? BasePlayer.FindByID(entity.OwnerID) : null;
        }

        private bool HasPermissionToControl(BasePlayer player)
        {
            if (!_config.RequirePermissionToControl)
                return true;

            return permission.UserHasPermission(player.UserIDString, PermissionControl);
        }

        private void SetupCarTurret(AutoTurret turret)
        {
            turret.gameObject.layer = (int)Rust.Layer.Vehicle_Detailed;
            RemoveColliders<MeshCollider>(turret);
            RemoveGroundWatch(turret);
            _carTurretTracker.Add(turret.net.ID);
        }

        private AutoTurret DeployAutoTurret(ModularCar car, BaseVehicleModule vehicleModule, Vector3 position, float conditionFraction = 1, ulong ownerId = 0)
        {
            var autoTurret = GameManager.server.CreateEntity(PrefabEntityAutoTurret, position, GetIdealTurretRotation(car, vehicleModule)) as AutoTurret;
            if (autoTurret == null)
                return null;

            autoTurret.SetFlag(IOEntity.Flag_HasPower, true);
            autoTurret.SetParent(vehicleModule);
            autoTurret.OwnerID = ownerId;
            autoTurret.Spawn();
            autoTurret.SetHealth(autoTurret.MaxHealth() * conditionFraction);

            SetupCarTurret(autoTurret);
            AttachTurretSwitch(autoTurret);

            Effect.server.Run(PrefabEffectDeployAutoTurret, autoTurret.transform.position);

            return autoTurret;
        }

        private void RefreshCarTurret(AutoTurret turret)
        {
            SetupCarTurret(turret);

            var turretSwitch = GetTurretSwitch(turret);
            if (turretSwitch != null)
            {
                SetupTurretSwitch(turretSwitch);
            }
        }

        private ElectricSwitch AttachTurretSwitch(AutoTurret autoTurret)
        {
            var turretSwitch = GameManager.server.CreateEntity(PrefabEntityElectricSwitch, autoTurret.transform.TransformPoint(TurretSwitchPosition), autoTurret.transform.rotation * TurretSwitchRotation) as ElectricSwitch;
            if (turretSwitch == null)
                return null;

            SetupTurretSwitch(turretSwitch);
            turretSwitch.Spawn();
            turretSwitch.SetParent(autoTurret, true);

            return turretSwitch;
        }

        private void SetupTurretSwitch(ElectricSwitch electricSwitch)
        {
            electricSwitch.pickup.enabled = false;
            electricSwitch.SetFlag(IOEntity.Flag_HasPower, true);
            electricSwitch.baseProtection = ImmortalProtection;
            RemoveColliders<Collider>(electricSwitch);
            RemoveGroundWatch(electricSwitch);
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

        private bool CanAccessVehicle(BaseVehicle vehicle, BasePlayer basePlayer, bool provideFeedback = true)
        {
            if (VehicleDeployedLocks == null)
                return true;

            var canAccess = VehicleDeployedLocks.Call("API_CanAccessVehicle", basePlayer, vehicle, provideFeedback);
            return !(canAccess is bool) || (bool)canAccess;
        }

        private int FindFirstSuitableSocketIndex(ModularCar car, BasePlayer basePlayer)
        {
            for (var socketIndex = 0; socketIndex < car.TotalSockets; socketIndex++)
            {
                BaseVehicleModule currentModule;
                if (car.TryGetModuleAt(socketIndex, out currentModule)
                    && currentModule.FirstSocketIndex == socketIndex
                    && HasPermissionToVehicleModule(basePlayer.UserIDString, currentModule)
                    && GetModuleAutoTurret(currentModule) == null)
                {
                    return socketIndex;
                }
            }

            return -1;
        }

        private int GetCarAutoTurretLimit(ModularCar car)
        {
            var defaultLimit = _config.DefaultLimitPerCar;

            if (car.OwnerID == 0)
                return defaultLimit;

            var ownerIdString = car.OwnerID.ToString();
            if (defaultLimit < 4 && permission.UserHasPermission(ownerIdString, PermissionLimit4))
                return 4;
            if (defaultLimit < 3 && permission.UserHasPermission(ownerIdString, PermissionLimit3))
                return 3;
            if (defaultLimit < 2 && permission.UserHasPermission(ownerIdString, PermissionLimit2))
                return 2;

            return defaultLimit;
        }

        private bool HasPermissionToVehicleModule(string userId, BaseVehicleModule vehicleModule)
        {
            return permission.UserHasPermission(userId, PermissionAllModules)
                || permission.UserHasPermission(userId, GetAutoTurretPermissionForModule(vehicleModule));
        }

        private bool ShouldSpawnTurretsWithCar(ModularCar car)
        {
            var spawnWithCarConfig = _config.SpawnWithCarConfig;
            if (!spawnWithCarConfig.Enabled)
                return false;

            if (IsNaturalCarSpawn(car))
                return spawnWithCarConfig.NaturalCarSpawns.Enabled;

            if (!spawnWithCarConfig.OtherCarSpawns.Enabled)
                return false;

            if (!spawnWithCarConfig.OtherCarSpawns.RequirePermission)
                return true;

            return car.OwnerID != 0 && permission.UserHasPermission(car.OwnerID.ToString(), PermissionSpawnWithCar);
        }

        private bool TryGetAutoTurretPositionForModule(BaseVehicleModule vehicleModule, out Vector3 position)
        {
            return _config.ModulePositions.TryGetValue(vehicleModule.AssociatedItemDef.shortname, out position);
        }

        private int GetAutoTurretChanceForModule(BaseVehicleModule vehicleModule)
        {
            int chance;
            return _config.SpawnWithCarConfig.SpawnChanceByModule.TryGetValue(vehicleModule.AssociatedItemDef.shortname, out chance)
                ? chance
                : 0;
        }

        private AutoTurret DeployAutoTurretForPlayer(ModularCar car, BaseVehicleModule vehicleModule, Vector3 position, BasePlayer basePlayer, float conditionFraction = 1)
        {
            var autoTurret = DeployAutoTurret(car, vehicleModule, position, conditionFraction, basePlayer.userID);
            if (autoTurret == null)
                return null;

            // Other plugins may have already automatically authorized the player.
            if (!autoTurret.IsAuthed(basePlayer))
            {
                autoTurret.authorizedPlayers.Add(new ProtoBuf.PlayerNameID
                {
                    userid = basePlayer.userID,
                    username = basePlayer.displayName
                });
                autoTurret.SendNetworkUpdate();
            }

            // Allow other plugins to detect the auto turret being deployed (e.g., to add a weapon automatically).
            var turretItem = FindPlayerAutoTurretItem(basePlayer);
            if (turretItem != null)
            {
                RunOnEntityBuilt(turretItem, autoTurret);
            }
            else
            {
                // Temporarily increase the player inventory capacity to ensure there is enough space.
                basePlayer.inventory.containerMain.capacity++;
                var temporaryTurretItem = ItemManager.CreateByItemID(ItemIdAutoTurret);
                if (basePlayer.inventory.GiveItem(temporaryTurretItem))
                {
                    RunOnEntityBuilt(temporaryTurretItem, autoTurret);
                    temporaryTurretItem.RemoveFromContainer();
                }

                temporaryTurretItem.Remove();
                basePlayer.inventory.containerMain.capacity--;
            }

            return autoTurret;
        }

        #endregion

        #region Dynamic Hook Subscriptions

        private class DynamicHookSubscriber<T>
        {
            private CarTurrets _plugin;
            private HashSet<T> _list = new HashSet<T>();
            private string[] _hookNames;

            public DynamicHookSubscriber(CarTurrets plugin, params string[] hookNames)
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

        #region Configuration

        [JsonObject(MemberSerialization.OptIn)]
        private class SpawnWithCarConfig
        {
            [JsonProperty("NaturalCarSpawns")]
            public NaturalCarSpawnsConfig NaturalCarSpawns = new NaturalCarSpawnsConfig();

            [JsonProperty("OtherCarSpawns")]
            public OtherCarSpawnsConfig OtherCarSpawns = new OtherCarSpawnsConfig();

            [JsonProperty("SpawnChanceByModule")]
            public Dictionary<string, int> SpawnChanceByModule = new Dictionary<string, int>()
            {
                ["vehicle.1mod.cockpit"] = 0,
                ["vehicle.1mod.cockpit.armored"] = 0,
                ["vehicle.1mod.cockpit.with.engine"] = 0,
                ["vehicle.1mod.engine"] = 0,
                ["vehicle.1mod.flatbed"] = 0,
                ["vehicle.1mod.passengers.armored"] = 0,
                ["vehicle.1mod.rear.seats"] = 0,
                ["vehicle.1mod.storage"] = 0,
                ["vehicle.1mod.taxi"] = 0,
                ["vehicle.2mod.flatbed"] = 0,
                ["vehicle.2mod.fuel.tank"] = 0,
                ["vehicle.2mod.passengers"] = 0,
                ["vehicle.2mod.camper"] = 0,
            };

            public bool Enabled => NaturalCarSpawns.Enabled || OtherCarSpawns.Enabled;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class NaturalCarSpawnsConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled = false;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class OtherCarSpawnsConfig
        {
            [JsonProperty("Enabled")]
            public bool Enabled = false;

            [JsonProperty("RequirePermission")]
            public bool RequirePermission = false;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class Configuration : BaseConfiguration
        {
            [JsonProperty("RequirePermissionToControlCarTurrets")]
            public bool RequirePermissionToControl;

            [JsonProperty("DefaultLimitPerCar")]
            public int DefaultLimitPerCar = 4;

            [JsonProperty("EnableTurretPickup")]
            public bool EnableTurretPickup = true;

            [JsonProperty("OnlyPowerTurretsWhileEngineIsOn")]
            public bool OnlyPowerTurretsWhileEngineIsOn = false;

            [JsonProperty("TargetPlayers")]
            public bool TargetPlayers = true;

            [JsonProperty("TargetNPCs")]
            public bool TargetNPCs = true;

            [JsonProperty("TargetAnimals")]
            public bool TargetAnimals = true;

            [JsonProperty("SpawnWithCar")]
            public SpawnWithCarConfig SpawnWithCarConfig = new SpawnWithCarConfig();

            [JsonProperty("AutoTurretPositionByModule")]
            public Dictionary<string, Vector3> ModulePositions = new Dictionary<string, Vector3>()
            {
                ["vehicle.1mod.cockpit"] = new Vector3(0, 1.39f, -0.3f),
                ["vehicle.1mod.cockpit.armored"] = new Vector3(0, 1.39f, -0.3f),
                ["vehicle.1mod.cockpit.with.engine"] = new Vector3(0, 1.39f, -0.85f),
                ["vehicle.1mod.engine"] = new Vector3(0, 0.4f, 0),
                ["vehicle.1mod.flatbed"] = new Vector3(0, 0.06f, 0),
                ["vehicle.1mod.passengers.armored"] = new Vector3(0, 1.38f, -0.31f),
                ["vehicle.1mod.rear.seats"] = new Vector3(0, 1.4f, -0.12f),
                ["vehicle.1mod.storage"] = new Vector3(0, 0.61f, 0),
                ["vehicle.1mod.taxi"] = new Vector3(0, 1.38f, -0.13f),
                ["vehicle.2mod.flatbed"] = new Vector3(0, 0.06f, -0.7f),
                ["vehicle.2mod.fuel.tank"] = new Vector3(0, 1.28f, -0.85f),
                ["vehicle.2mod.passengers"] = new Vector3(0, 1.4f, -0.9f),
                ["vehicle.2mod.camper"] = new Vector3(0, 1.4f, -1.6f),
            };
        }

        private Configuration GetDefaultConfig() => new Configuration();

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

        #endregion

        #region Localization

        private string GetMessage(string userId, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, userId);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        private string GetMessage(IPlayer player, string messageName, params object[] args) =>
            GetMessage(player.Id, messageName, args);

        private void ReplyToPlayer(IPlayer player, string messageName, params object[] args) =>
            player.Reply(string.Format(GetMessage(player, messageName), args));

        private void ChatMessage(BasePlayer basePlayer, string messageName, params object[] args) =>
            basePlayer.ChatMessage(string.Format(GetMessage(basePlayer.UserIDString, messageName), args));

        private class Lang
        {
            public const string GenericErrorNoPermission = "Generic.Error.NoPermission";
            public const string GenericErrorBuildingBlocked = "Generic.Error.BuildingBlocked";
            public const string DeployErrorNoCarFound = "Deploy.Error.NoCarFound";
            public const string DeployErrorNoModules = "Deploy.Error.NoModules";
            public const string DeployErrorNoPermissionToModule = "Deploy.Error.NoPermissionToModule";
            public const string DeployErrorModuleAlreadyHasTurret = "Deploy.Error.ModuleAlreadyHasTurret";
            public const string DeployErrorUnsupportedModule = "Deploy.Error.UnsupportedModule";
            public const string DeployErrorTurretLimit = "Deploy.Error.TurretLimit";
            public const string DeployErrorNoSuitableModule = "Deploy.Error.NoSuitableModule";
            public const string DeployErrorNoTurret = "Deploy.Error.NoTurret";
            public const string RemoveErrorTurretHasItems = "Remove.Error.TurretHasItems";
            public const string RemoveAllSuccess = "RemoveAll.Success";
            public const string InfoPowerRequiresEngine = "Info.PowerRequiresEngine";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.GenericErrorNoPermission] = "You don't have permission to do that.",
                [Lang.GenericErrorBuildingBlocked] = "Error: Cannot do that while building blocked.",
                [Lang.DeployErrorNoCarFound] = "Error: No car found.",
                [Lang.DeployErrorNoModules] = "Error: That car has no modules.",
                [Lang.DeployErrorNoPermissionToModule] = "You don't have permission to do that to that module type.",
                [Lang.DeployErrorModuleAlreadyHasTurret] = "Error: That module already has a turret.",
                [Lang.DeployErrorUnsupportedModule] = "Error: That module is not supported.",
                [Lang.DeployErrorTurretLimit] = "Error: That car may only have {0} turret(s).",
                [Lang.DeployErrorNoSuitableModule] = "Error: No suitable module found.",
                [Lang.DeployErrorNoTurret] = "Error: You need an auto turret to do that.",
                [Lang.RemoveErrorTurretHasItems] = "Error: That module's turret must be empty.",
                [Lang.RemoveAllSuccess] = "Removed all {0} car turrets.",
                [Lang.InfoPowerRequiresEngine] = "The turret will power on when the car engine starts."
            }, this, "en");

            //Adding translation in portuguese brazil
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.GenericErrorNoPermission] = "Você não tem permissão para fazer isso.",
                [Lang.GenericErrorBuildingBlocked] = "Erro: Não é possível fazer isso enquanto o prédio está bloqueado.",
                [Lang.DeployErrorNoCarFound] = "Erro: nenhum carro encontrado.",
                [Lang.DeployErrorNoModules] = "Erro: esse carro não tem módulos.",
                [Lang.DeployErrorNoPermissionToModule] = "Você não tem permissão para fazer isso com esse tipo de módulo.",
                [Lang.DeployErrorModuleAlreadyHasTurret] = "Erro: esse módulo já tem uma turret.",
                [Lang.DeployErrorUnsupportedModule] = "Erro: esse módulo não é compatível.",
                [Lang.DeployErrorTurretLimit] = "Erro: esse carro só pode ter {0} torreta(s).",
                [Lang.DeployErrorNoSuitableModule] = "Erro: Nenhum módulo adequado encontrado.",
                [Lang.DeployErrorNoTurret] = "Erro: você precisa de uma turret automática para fazer isso.",
                [Lang.RemoveErrorTurretHasItems] = "Erro: a torre desse módulo deve estar vazia.",
                [Lang.RemoveAllSuccess] = "Removidas todas as {0} turrets do carro.",
                [Lang.InfoPowerRequiresEngine] = "A torre será ligada quando o motor do carro ligar."
            }, this, "pt-BR");
        }

        #endregion
    }
}
