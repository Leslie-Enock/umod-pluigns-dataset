using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
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
    [Info("Workcart Safe Zones", "WhiteThunder", "2.0.1")]
    [Description("Adds mobile safe zones and optional NPC auto turrets to workcarts.")]
    internal class WorkcartSafeZones : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin AutomatedWorkcarts, CargoTrainEvent;

        private static WorkcartSafeZones _pluginInstance;
        private static Configuration _pluginConfig;

        private const string PermissionUse = "workcartsafezones.use";
        private const string BanditSentryPrefab = "assets/content/props/sentry_scientists/sentry.bandit.static.prefab";

        private const float SafeZoneWarningCooldown = 10;

        private Dictionary<ulong, float> _playersLastWarnedTime = new Dictionary<ulong, float>();

        private SavedData _pluginData;

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginInstance = this;
            _pluginData = SavedData.Load();

            permission.RegisterPermission(PermissionUse, this);

            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnWorkcartAutomationStarted));
            Unsubscribe(nameof(OnWorkcartAutomationStopped));
        }

        private void OnServerInitialized()
        {
            CheckDependencies();

            _pluginData.CleanStaleData();

            if (_pluginConfig.AddToAllWorkcarts)
            {
                foreach (var workcart in BaseNetworkable.serverEntities.OfType<TrainEngine>().ToArray())
                    TryCreateSafeZone(workcart);

                Subscribe(nameof(OnEntitySpawned));
            }
            else if (_pluginConfig.AddToAutomatedWorkcarts)
            {
                var workcartList = GetAutomatedWorkcarts();
                if (workcartList != null)
                {
                    foreach (var workcart in workcartList)
                        TryCreateSafeZone(workcart);
                }

                Subscribe(nameof(OnWorkcartAutomationStarted));
                Subscribe(nameof(OnWorkcartAutomationStopped));
            }

            foreach (var workcartId in _pluginData.SafeWorkcarts)
            {
                var workcart = BaseNetworkable.serverEntities.Find(new NetworkableId(workcartId)) as TrainEngine;
                if (workcart != null)
                    TryCreateSafeZone(workcart);
            }
        }

        private void Unload()
        {
            foreach (var workcart in BaseNetworkable.serverEntities.OfType<TrainEngine>())
                SafeWorkcart.RemoveFromWorkcart(workcart);

            _pluginConfig = null;
            _pluginInstance = null;
        }

        private void OnEntitySpawned(TrainEngine workcart)
        {
            TryCreateSafeZone(workcart);
        }

        private bool? OnEntityTakeDamage(TrainEngine workcart)
        {
            if (workcart.GetComponent<SafeWorkcart>() != null)
            {
                // Return true (standard) to cancel default behavior (prevent damage).
                return true;
            }

            return null;
        }

        private void OnEntityEnter(TriggerSafeZone triggerSafeZone, BasePlayer player)
        {
            if (player.IsNpc
                || triggerSafeZone.GetComponentInParent<SafeWorkcart>() == null
                || !player.IsHostile())
                return;

            var hostileTimeRemaining = player.State.unHostileTimestamp - Network.TimeEx.currentTimestamp;
            if (hostileTimeRemaining < 0)
                return;

            float lastWarningTime;
            if (_playersLastWarnedTime.TryGetValue(player.userID, out lastWarningTime)
                && lastWarningTime + SafeZoneWarningCooldown > Time.realtimeSinceStartup)
                return;

            ChatMessage(player, "Warning.Hostile", TimeSpan.FromSeconds(Math.Ceiling(hostileTimeRemaining)).ToString("g"));
            _playersLastWarnedTime[player.userID] = Time.realtimeSinceStartup;
        }

        private void OnEntityEnter(TriggerParent triggerParent, BasePlayer player)
        {
            if (!_pluginConfig.DisarmOccupants
                || player.IsNpc
                || triggerParent.GetComponentInParent<SafeWorkcart>() == null)
                return;

            var activeItem = player.GetActiveItem();
            if (activeItem == null || !player.IsHostileItem(activeItem))
                return;

            var position = activeItem.position;
            activeItem.RemoveFromContainer();
            player.inventory.SendUpdatedInventory(PlayerInventory.Type.Belt, player.inventory.containerBelt);

            // Note: It's possible to leak an item here if the player is killed during the delay,
            // but the number of items that would be leaked this way is insignificant.
            player.Invoke(() =>
            {
                if (!activeItem.MoveToContainer(player.inventory.containerBelt, position))
                    player.inventory.GiveItem(activeItem);
            }, 0.2f);
        }

        // This hook is exposed by plugin: Automated Workcarts (AutomatedWorkcarts).
        private void OnWorkcartAutomationStarted(TrainEngine workcart)
        {
            TryCreateSafeZone(workcart);
        }

        // This hook is exposed by plugin: Automated Workcarts (AutomatedWorkcarts).
        private void OnWorkcartAutomationStopped(TrainEngine workcart)
        {
            SafeWorkcart.RemoveFromWorkcart(workcart);
        }

        // This hook is exposed by plugin: Cargo Train Event (CargoTrainEvent).
        private void OnTrainEventStarted(TrainEngine workcart)
        {
            SafeWorkcart.RemoveFromWorkcart(workcart);
        }

        #endregion

        #region Dependencies

        private void CheckDependencies()
        {
            if (_pluginConfig.AddToAllWorkcarts)
                return;

            if (_pluginConfig.AddToAutomatedWorkcarts && AutomatedWorkcarts == null)
                LogWarning("AutomatedWorkcarts is not loaded, get it at http://umod.org. If you don't intend to use this plugin with Automated Workcarts, then set \"AddToAutomatedWorkcarts\" to false in the config and you will no longer see this message.");
        }

        private TrainEngine[] GetAutomatedWorkcarts()
        {
            return AutomatedWorkcarts?.Call("API_GetAutomatedWorkcarts") as TrainEngine[];
        }

        private bool IsCargoTrain(TrainEngine workcart)
        {
            var result = CargoTrainEvent?.Call("IsTrainSpecial", workcart.net.ID);
            return result is bool && (bool)result;
        }

        #endregion

        #region API

        private bool API_CreateSafeZone(TrainEngine workcart)
        {
            if (workcart.GetComponent<SafeWorkcart>() != null)
                return true;

            return TryCreateSafeZone(workcart);
        }

        #endregion

        #region Exposed Hooks

        private static bool CreateSafeZoneWasBlocked(TrainEngine workcart)
        {
            object hookResult = Interface.CallHook("OnWorkcartSafeZoneCreate", workcart);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static void CallHookSafeZoneCreated(TrainEngine workcart)
        {
            Interface.CallHook("OnWorkcartSafeZoneCreated", workcart);
        }

        #endregion

        #region Commands

        [Command("safecart.add")]
        private void CommandAddSafeZone(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer)
                return;

            if (!player.HasPermission(PermissionUse))
            {
                ReplyToPlayer(player, "Error.NoPermission");
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            var workcart = GetLookEntity(basePlayer) as TrainEngine;

            if (workcart == null)
            {
                ReplyToPlayer(player, "Error.NoWorkcartFound");
                return;
            }

            if (workcart.GetComponent<SafeWorkcart>() != null)
            {
                ReplyToPlayer(player, "Error.SafeZonePresent");
                return;
            }

            if (TryCreateSafeZone(workcart))
            {
                _pluginData.AddWorkcart(workcart);
                ReplyToPlayer(player, "Add.Success");
            }
            else
            {
                ReplyToPlayer(player, "Add.Error");
            }
        }

        [Command("safecart.remove")]
        private void CommandRemoveSafeZone(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer)
                return;

            if (!player.HasPermission(PermissionUse))
            {
                ReplyToPlayer(player, "Error.NoPermission");
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            var workcart = GetLookEntity(basePlayer) as TrainEngine;

            if (workcart == null)
            {
                ReplyToPlayer(player, "Error.NoWorkcartFound");
                return;
            }

            var component = workcart.GetComponent<SafeWorkcart>();
            if (component == null)
            {
                ReplyToPlayer(player, "Error.NoSafeZone");
                return;
            }

            SafeWorkcart.RemoveFromWorkcart(workcart);
            _pluginData.RemoveWorkcart(workcart);
            ReplyToPlayer(player, "Remove.Success");
        }

        #endregion

        #region Helper Methods

        private static bool TryCreateSafeZone(TrainEngine workcart)
        {
            if (CreateSafeZoneWasBlocked(workcart))
                return false;

            if (_pluginInstance.IsCargoTrain(workcart))
                return false;

            SafeWorkcart.AddToWorkcart(workcart);
            CallHookSafeZoneCreated(workcart);

            return true;
        }

        private static NPCAutoTurret SpawnTurret(BaseEntity entity, Vector3 position, float rotationAngle)
        {
            var rotation = rotationAngle == 0 ? Quaternion.identity : Quaternion.Euler(0, rotationAngle, 0);

            var autoTurret = GameManager.server.CreateEntity(BanditSentryPrefab, position, rotation) as NPCAutoTurret;
            if (autoTurret == null)
                return null;

            autoTurret.enableSaving = false;
            autoTurret.SetParent(entity);
            autoTurret.Spawn();

            return autoTurret;
        }

        private static BaseEntity GetLookEntity(BasePlayer player, float maxDistance = 10)
        {
            RaycastHit hit;
            return Physics.Raycast(player.eyes.HeadRay(), out hit, maxDistance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                ? hit.GetEntity()
                : null;
        }

        #endregion

        #region Safe Zone

        private class SafeWorkcart : MonoBehaviour
        {
            public static void AddToWorkcart(TrainEngine workcart) =>
                workcart.GetOrAddComponent<SafeWorkcart>();

            public static void RemoveFromWorkcart(TrainEngine workcart) =>
                UnityEngine.Object.DestroyImmediate(workcart.GetComponent<SafeWorkcart>());

            private TrainEngine _workcart;
            private GameObject _child;
            private ProtectionProperties _originalProtection;
            private List<NPCAutoTurret> _autoTurrets = new List<NPCAutoTurret>();

            private void Awake()
            {
                _workcart = GetComponent<TrainEngine>();
                if (_workcart == null)
                    return;

                _workcart.SetHealth(_workcart.MaxHealth());

                AddVolumetricSafeZone();
                MaybeAddTurrets();
            }

            private void MaybeAddTurrets()
            {
                if (!_pluginConfig.EnableTurrets)
                    return;

                foreach (var turretConfig in _pluginConfig.TurretPositions)
                    _autoTurrets.Add(SpawnTurret(_workcart, turretConfig.Position, turretConfig.RotationAngle));
            }

            private void AddVolumetricSafeZone()
            {
                _child = gameObject.CreateChild();

                var safeZone = _child.AddComponent<TriggerSafeZone>();
                safeZone.interestLayers = Rust.Layers.Mask.Player_Server;
                safeZone.maxAltitude = 10;
                safeZone.maxDepth = 1;

                var radius = _pluginConfig.SafeZoneRadius;
                if (radius > 0)
                {
                    var collider = _child.AddComponent<SphereCollider>();
                    collider.isTrigger = true;
                    collider.gameObject.layer = 18;
                    collider.center = Vector3.zero;
                    collider.radius = radius;
                }
                else
                {
                    // Add a box collider for just the workcart area.
                    var collider = _child.AddComponent<BoxCollider>();
                    collider.isTrigger = true;
                    collider.gameObject.layer = 18;
                    collider.size = _workcart.bounds.extents * 2 + new Vector3(0, safeZone.maxAltitude, 0);
                }
            }

            private void OnDestroy()
            {
                if (_child != null)
                    Destroy(_child);

                foreach (var autoTurret in _autoTurrets)
                    if (autoTurret != null)
                        autoTurret.Kill();
            }
        }

        #endregion

        #region Saved Data

        private class SavedData
        {
            [JsonProperty("SafeWorkcartIds")]
            public List<ulong> SafeWorkcarts = new List<ulong>();

            public static SavedData Load() =>
                Interface.Oxide.DataFileSystem.ReadObject<SavedData>(_pluginInstance.Name) ?? new SavedData();

            public void Save() =>
                Interface.Oxide.DataFileSystem.WriteObject<SavedData>(_pluginInstance.Name, this);

            public void AddWorkcart(TrainEngine workcart)
            {
                SafeWorkcarts.Add(workcart.net.ID.Value);
                Save();
            }

            public void RemoveWorkcart(TrainEngine workcart)
            {
                SafeWorkcarts.Remove(workcart.net.ID.Value);
                Save();
            }

            public void CleanStaleData()
            {
                var cleanedCount = 0;

                for (var i = SafeWorkcarts.Count - 1; i >= 0; i--)
                {
                    var entity = BaseNetworkable.serverEntities.Find(new NetworkableId(SafeWorkcarts[i]));
                    if (entity == null)
                    {
                        SafeWorkcarts.RemoveAt(i);
                        cleanedCount++;
                    }
                }

                if (cleanedCount > 0)
                    Save();
            }
        }

        #endregion

        #region Configuration

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("AddToAllWorkcarts")]
            public bool AddToAllWorkcarts = false;

            [JsonProperty("AddToAutomatedWorkcarts")]
            public bool AddToAutomatedWorkcarts = true;

            [JsonProperty("SafeZoneRadius")]
            public float SafeZoneRadius = 0;

            [JsonProperty("DisarmOccupants")]
            public bool DisarmOccupants = false;

            [JsonProperty("EnableTurrets")]
            public bool EnableTurrets = false;

            [JsonProperty("TurretPositions")]
            public TurretConfig[] TurretPositions = new TurretConfig[]
            {
                new TurretConfig
                {
                    Position = new Vector3(0.85f, 2.62f, 1.25f),
                    RotationAngle = 180,
                },
                new TurretConfig
                {
                    Position = new Vector3(0.7f, 3.84f, 3.7f)
                }
            };
        }

        private class TurretConfig
        {
            [JsonProperty("Position")]
            public Vector3 Position;

            [JsonProperty("RotationAngle")]
            public float RotationAngle;
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

        private string GetMessage(string playerId, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, playerId);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Error.NoPermission"] = "You don't have permission to do that.",
                ["Error.NoWorkcartFound"] = "Error: No workcart found.",
                ["Error.SafeZonePresent"] = "That workcart already has a safe zone.",
                ["Error.NoSafeZone"] = "That workcart doesn't have a safe zone.",
                ["Add.Success"] = "Successfully added safe zone to the workcart.",
                ["Add.Error"] = "Error: Unable to add a safe zone to that workcart.",
                ["Remove.Success"] = "Successfully removed safe zone from the workcart.",
                ["Warning.Hostile"] = "You are <color=red>hostile</color> for <color=red>{0}</color>. No safe zone protection.",
            }, this, "en");
        }

        #endregion
    }
}
