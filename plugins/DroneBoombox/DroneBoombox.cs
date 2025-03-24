using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Drone Boombox", "WhiteThunder", "1.0.2")]
    [Description("Allows players to deploy boomboxes onto RC drones.")]
    internal class DroneBoombox : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin DroneSettings;

        private static DroneBoombox _pluginInstance;
        private Configuration _pluginConfig;

        private const string PermissionDeploy = "droneboombox.deploy";
        private const string PermissionDeployFree = "droneboombox.deploy.free";
        private const string PermissionAutoDeploy = "droneboombox.autodeploy";

        private const string BoomboxPrefab = "assets/prefabs/voiceaudio/boombox/boombox.static.prefab";
        private const string DeployEffectPrefab = "assets/prefabs/voiceaudio/boombox/effects/boombox-deploy.prefab";

        private const int PortableBoomboxItemId = 576509618;
        private const int DeployableBoomboxItemId = -1113501606;

        private const BaseEntity.Slot BoomboxSlot = BaseEntity.Slot.UpperModifier;

        private static readonly Vector3 BoomboxLocalPosition = new Vector3(0, 0.21f, 0.17f);
        private static readonly Quaternion BoomboxLocalRotation = Quaternion.Euler(270, 0, 0);

        // Subscribe to these hooks while there are boombox drones.
        private DynamicHookSubscriber<NetworkableId> _boomboxDroneTracker = new DynamicHookSubscriber<NetworkableId>(
            nameof(OnEntityKill),
            nameof(OnEntityTakeDamage),
            nameof(CanPickupEntity),
            nameof(canRemove)
        );

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginInstance = this;

            permission.RegisterPermission(PermissionDeploy, this);
            permission.RegisterPermission(PermissionDeployFree, this);
            permission.RegisterPermission(PermissionAutoDeploy, this);

            _boomboxDroneTracker.UnsubscribeAll();
        }

        private void Unload()
        {
            _pluginInstance = null;
        }

        private void OnServerInitialized()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var drone = entity as Drone;
                if (drone == null || !IsDroneEligible(drone))
                    continue;

                var droneBoombox = GetDroneBoombox(drone);
                if (droneBoombox == null)
                    continue;

                SetupDroneBoombox(drone, droneBoombox);
            }
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

            NextTick(() =>
            {
                // Delay this check to allow time for other plugins to deploy an entity to this slot.
                if (drone == null || player == null || drone.GetSlot(BoomboxSlot) != null)
                    return;

                if (permission.UserHasPermission(player.UserIDString, PermissionAutoDeploy))
                {
                    DeployBoombox(drone, player);
                }
                else if (permission.UserHasPermission(player.UserIDString, PermissionDeploy)
                    && UnityEngine.Random.Range(0, 100) < _pluginConfig.TipChance)
                {
                    ChatMessage(player, Lang.TipDeployCommand);
                }
            });
        }

        private void OnEntityKill(Drone drone)
        {
            _boomboxDroneTracker.Remove(drone.net.ID);
        }

        private void OnEntityKill(DeployableBoomBox boombox)
        {
            var drone = GetParentDrone(boombox);
            if (drone == null)
                return;

            _boomboxDroneTracker.Remove(drone.net.ID);
            drone.Invoke(() => RefreshDroneSettingsProfile(drone), 0);
        }

        // Redirect damage from the boombox to the drone.
        private bool? OnEntityTakeDamage(DeployableBoomBox boombox, HitInfo info)
        {
            var drone = GetParentDrone(boombox);
            if (drone == null)
                return null;

            drone.Hurt(info);
            HitNotify(drone, info);

            return true;
        }

        private bool? CanPickupEntity(BasePlayer player, Drone drone)
        {
            if (CanPickupInternal(player, drone))
                return null;

            ChatMessage(player, Lang.ErrorCannotPickupWithCassette);
            return false;
        }

        // This hook is exposed by plugin: Remover Tool (RemoverTool).
        private string canRemove(BasePlayer player, Drone drone)
        {
            if (CanPickupInternal(player, drone))
                return null;

            return GetMessage(player, Lang.ErrorCannotPickupWithCassette);
        }

        // This hook is exposed by plugin: Drone Settings (DroneSettings).
        private string OnDroneTypeDetermine(Drone drone)
        {
            return GetDroneBoombox(drone) != null ? Name : null;
        }

        #endregion

        #region API

        private DeployableBoomBox API_DeployBoombox(Drone drone, BasePlayer player)
        {
            if (GetDroneBoombox(drone) != null
                || drone.GetSlot(BoomboxSlot) != null
                || DeployBoomboxWasBlocked(drone, player))
                return null;

            return DeployBoombox(drone, player);
        }

        #endregion

        #region Commands

        [Command("droneboombox", "dronebb")]
        private void DroneBoomboxCommand(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer)
                return;

            var basePlayer = player.Object as BasePlayer;
            if (!basePlayer.CanInteract())
                return;

            Drone drone;
            if (!VerifyPermission(player, PermissionDeploy)
                || !VerifyDroneFound(player, out drone)
                || !VerifyCanBuild(player, drone)
                || !VerifyDroneHasNoBoombox(player, drone)
                || !VerifyDroneHasSlotVacant(player, drone))
                return;

            Item boomboxPaymentItem = null;

            if (!player.HasPermission(PermissionDeployFree))
            {
                boomboxPaymentItem = FindPlayerBoomboxItem(basePlayer);
                if (boomboxPaymentItem == null)
                {
                    ReplyToPlayer(player, Lang.ErrorNoBoomboxItem);
                    return;
                }
            }

            if (DeployBoomboxWasBlocked(drone, basePlayer))
                return;

            if (DeployBoombox(drone, basePlayer) == null)
            {
                ReplyToPlayer(player, Lang.ErrorDeployFailed);
                return;
            }

            if (boomboxPaymentItem != null)
                UseItem(basePlayer, boomboxPaymentItem);
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
            if (drone != null && IsDroneEligible(drone))
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

        private bool VerifyDroneHasNoBoombox(IPlayer player, Drone drone)
        {
            if (GetDroneBoombox(drone) == null)
                return true;

            ReplyToPlayer(player, Lang.ErrorAlreadyHasBoombox);
            return false;
        }

        private bool VerifyDroneHasSlotVacant(IPlayer player, Drone drone)
        {
            if (drone.GetSlot(BoomboxSlot) == null)
                return true;

            ReplyToPlayer(player, Lang.ErrorIncompatibleAttachment);
            return false;
        }

        #endregion

        #region Helper Method

        private static bool DeployBoomboxWasBlocked(Drone drone, BasePlayer deployer)
        {
            object hookResult = Interface.CallHook("OnDroneBoomboxDeploy", drone, deployer);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static bool IsDroneEligible(Drone drone) =>
            drone.skinID == 0 && !(drone is DeliveryDrone);

        private static BaseEntity GetLookEntity(BasePlayer basePlayer, float maxDistance = 3)
        {
            RaycastHit hit;
            return Physics.Raycast(basePlayer.eyes.HeadRay(), out hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.GetEntity()
                : null;
        }

        private static Drone GetParentDrone(BaseEntity entity) =>
            entity.GetParentEntity() as Drone;

        private static DeployableBoomBox GetDroneBoombox(Drone drone) =>
            drone.GetSlot(BoomboxSlot) as DeployableBoomBox;

        private static bool CanPickupInternal(BasePlayer player, Drone drone)
        {
            if (!IsDroneEligible(drone))
                return true;

            var boombox = GetDroneBoombox(drone);
            if (boombox == null)
                return true;

            // Prevent drone pickup while it has a boombox with a cassette in it (the cassette must be removed first).
            if (boombox != null && !boombox.inventory.IsEmpty() && !boombox.inventory.IsLocked())
                return false;

            return true;
        }

        private static void HitNotify(BaseEntity entity, HitInfo info)
        {
            var player = info.Initiator as BasePlayer;
            if (player == null)
                return;

            entity.ClientRPCPlayer(null, player, "HitNotify");
        }

        private static void RunOnEntityBuilt(Item boomboxItem, DeployableBoomBox boombox) =>
            Interface.CallHook("OnEntityBuilt", boomboxItem.GetHeldEntity(), boombox.gameObject);

        private static void UseItem(BasePlayer basePlayer, Item item, int amountToConsume = 1)
        {
            item.UseItem(amountToConsume);
            basePlayer.Command("note.inv", item.info.itemid, -amountToConsume);
        }

        private static Item FindPlayerBoomboxItem(BasePlayer basePlayer) =>
            basePlayer.inventory.FindItemByItemID(PortableBoomboxItemId) ??
            basePlayer.inventory.FindItemByItemID(DeployableBoomboxItemId);

        private void RefreshDroneSettingsProfile(Drone drone)
        {
            _pluginInstance.DroneSettings?.Call("API_RefreshDroneProfile", drone);
        }

        private void SetupDroneBoombox(Drone drone, DeployableBoomBox boombox)
        {
            // Damage will be processed by the drone.
            boombox.baseProtection = null;

            // These boomboxes should not decay while playing, since that would damage the drone.
            // Default is 0.025 for the static boomboxes, even though it's 0 for other deployable boomboxes.
            boombox.BoxController.ConditionLossRate = 0;

            RefreshDroneSettingsProfile(drone);
            _boomboxDroneTracker.Add(drone.net.ID);
        }

        private DeployableBoomBox DeployBoombox(Drone drone, BasePlayer basePlayer)
        {
            var boombox = GameManager.server.CreateEntity(BoomboxPrefab, BoomboxLocalPosition, BoomboxLocalRotation) as DeployableBoomBox;
            if (boombox == null)
                return null;

            if (basePlayer != null)
                boombox.OwnerID = basePlayer.userID;

            boombox.SetParent(drone);
            boombox.Spawn();

            drone.SetSlot(BoomboxSlot, boombox);
            SetupDroneBoombox(drone, boombox);

            Effect.server.Run(DeployEffectPrefab, boombox.transform.position);
            Interface.CallHook("OnDroneBoomboxDeployed", drone, boombox, basePlayer);

            if (basePlayer == null)
                return boombox;

            // Allow other plugins to detect the boombox being deployed (e.g., to set a default station).
            var boomboxItem = FindPlayerBoomboxItem(basePlayer);
            if (boomboxItem != null)
            {
                RunOnEntityBuilt(boomboxItem, boombox);
            }
            else
            {
                // Temporarily increase the player inventory capacity to ensure there is enough space.
                basePlayer.inventory.containerMain.capacity++;
                var temporaryItem = ItemManager.CreateByItemID(DeployableBoomboxItemId);
                if (basePlayer.inventory.GiveItem(temporaryItem))
                {
                    RunOnEntityBuilt(temporaryItem, boombox);
                    temporaryItem.RemoveFromContainer();
                }
                temporaryItem.Remove();
                basePlayer.inventory.containerMain.capacity--;
            }

            return boombox;
        }

        #endregion

        #region Dynamic Hook Subscriptions

        private class DynamicHookSubscriber<T>
        {
            private HashSet<T> _list = new HashSet<T>();
            private string[] _hookNames;

            public DynamicHookSubscriber(params string[] hookNames)
            {
                _hookNames = hookNames;
            }

            public void Add(T item)
            {
                if (_list.Add(item) && _list.Count == 1)
                    SubscribeAll();
            }

            public void Remove(T item)
            {
                if (_list.Remove(item) && _list.Count == 0)
                    UnsubscribeAll();
            }

            public void SubscribeAll()
            {
                foreach (var hookName in _hookNames)
                    _pluginInstance.Subscribe(hookName);
            }

            public void UnsubscribeAll()
            {
                foreach (var hookName in _hookNames)
                    _pluginInstance.Unsubscribe(hookName);
            }
        }

        #endregion

        #region Configuration

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("TipChance")]
            public int TipChance = 25;
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
            public const string ErrorNoBoomboxItem = "Error.NoBoomboxItem";
            public const string ErrorAlreadyHasBoombox = "Error.AlreadyHasBoombox";
            public const string ErrorIncompatibleAttachment = "Error.IncompatibleAttachment";
            public const string ErrorDeployFailed = "Error.DeployFailed";
            public const string ErrorCannotPickupWithCassette = "Error.CannotPickupWithCassette";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.TipDeployCommand] = "Tip: Look at the drone and run <color=yellow>/droneboombox</color> to deploy a boombox.",
                [Lang.ErrorNoPermission] = "You don't have permission to do that.",
                [Lang.ErrorNoDroneFound] = "Error: No drone found.",
                [Lang.ErrorBuildingBlocked] = "Error: Cannot do that while building blocked.",
                [Lang.ErrorNoBoomboxItem] = "Error: You need a boombox to do that.",
                [Lang.ErrorAlreadyHasBoombox] = "Error: That drone already has a boombox.",
                [Lang.ErrorIncompatibleAttachment] = "Error: That drone has an incompatible attachment.",
                [Lang.ErrorDeployFailed] = "Error: Failed to deploy boombox.",
                [Lang.ErrorCannotPickupWithCassette] = "Cannot pick up that drone while its boombox contains a cassette.",
            }, this, "en");
        }

        #endregion
    }
}
