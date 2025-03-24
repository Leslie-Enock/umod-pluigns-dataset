using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VLB;
using static SamSite;
using static BaseVehicle;

namespace Oxide.Plugins
{
    [Info("Heli Sams", "WhiteThunder & Whispers88", "2.1.3")]
    [Description("Allows Sam Sites to target CH47 and Patrol Helicopters")]
    internal class HeliSams : CovalencePlugin
    {
        #region Fields

        private const float DebugDrawDistance = 500;

        private const string PermissionCh47Npc = "helisams.ch47.npc";
        private const string PermissionCh47Player = "helisams.ch47.player";
        private const string PermissionPatrolHeli = "helisams.patrolheli";
        private const string CH47NpcPrefab = "assets/prefabs/npc/ch47/ch47scientists.entity.prefab";

        private readonly object False = false;

        private static Configuration _config;
        private static uint _ch47NpcPrefabId;

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionCh47Npc, this);
            permission.RegisterPermission(PermissionCh47Player, this);
            permission.RegisterPermission(PermissionPatrolHeli, this);
        }

        private void OnServerInitialized()
        {
            _ch47NpcPrefabId = StringPool.Get(CH47NpcPrefab);

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var ch47 = entity as CH47Helicopter;
                if (ch47 != null)
                {
                    OnEntitySpawned(ch47);
                    continue;
                }

                var patrolHeli = entity as PatrolHelicopter;
                if (patrolHeli != null)
                {
                    OnEntitySpawned(patrolHeli);
                    continue;
                }
            }
        }

        private void Unload()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var ch47 = entity as CH47Helicopter;
                if (ch47 != null)
                {
                    SAMTargetComponent.RemoveFromEntity(ch47);
                    continue;
                }

                var patrolHeli = entity as PatrolHelicopter;
                if (patrolHeli != null)
                {
                    SAMTargetComponent.RemoveFromEntity(patrolHeli);
                    continue;
                }
            }

            _config = null;
        }

        private void OnEntitySpawned(CH47Helicopter entity) => SAMTargetComponent.AddToEntity(entity);

        private void OnEntitySpawned(PatrolHelicopter entity) => SAMTargetComponent.AddToEntity(entity);

        private void OnEntityKill(CH47Helicopter entity) => SAMTargetComponent.RemoveFromEntity(entity);

        private void OnEntityKill(PatrolHelicopter entity) => SAMTargetComponent.RemoveFromEntity(entity);

        private object OnSamSiteTarget(SamSite samSite, SAMTargetComponent targetComponent)
        {
            var ch47 = targetComponent.Entity as CH47Helicopter;
            if ((object)ch47 != null)
            {
                if (IsNpcCH47(ch47))
                    return ShouldTargetNpcCH47(samSite, ch47) ? null : False;

                return ShouldTargetPlayerCH47(samSite, ch47) ? null : False;
            }

            var patrolHeli = targetComponent.Entity as PatrolHelicopter;
            if ((object)patrolHeli != null)
                return ShouldTargetPatrolHelicopter(samSite, patrolHeli) ? null : False;

            return null;
        }

        private void OnEntityTakeDamage(CH47Helicopter ch47, HitInfo info)
        {
            var samSite = info.Initiator as SamSite;
            if (samSite == null)
                return;

            var damageMultiplier = IsNpcCH47(ch47)
                ? _config.CH47Npc.RocketDamageMultiplier
                : _config.CH47Player.RocketDamageMultiplier;

            if (damageMultiplier > 1)
            {
                info.damageTypes.ScaleAll(damageMultiplier);
                if (_config.DebugRocketDamage)
                {
                    ShowRocketDamage(info.HitPositionWorld, info.damageTypes.Total());
                }
            }
        }

        private void OnEntityTakeDamage(PatrolHelicopter patrolHeli, HitInfo info)
        {
            var samSite = info.Initiator as SamSite;
            if (samSite == null)
                return;

            var damageMultiplier = _config.PatrolHeli.RocketDamageMultiplier;
            if (damageMultiplier > 1)
            {
                info.damageTypes.ScaleAll(damageMultiplier);
                if (_config.DebugRocketDamage)
                {
                    ShowRocketDamage(info.HitPositionWorld, info.damageTypes.Total());
                }
            }
        }

        #endregion

        #region Helper Methods

        private static bool IsNpcCH47(CH47Helicopter ch47)
        {
            return ch47.prefabID == _ch47NpcPrefabId;
        }

        private static bool IsOccupied(BaseCombatEntity entity, List<MountPointInfo> mountPoints)
        {
            if (mountPoints != null)
            {
                foreach (var mountPoint in mountPoints)
                {
                    var player = mountPoint.mountable.GetMounted();
                    if ((object)player != null)
                        return true;
                }
            }

            foreach (var child in entity.children)
            {
                if (child is BasePlayer)
                    return true;
            }

            return false;
        }

        private static bool IsAuthed(BuildingPrivlidge cupboard, ulong userId)
        {
            foreach (var entry in cupboard.authorizedPlayers)
            {
                if (entry.userid == userId)
                    return true;
            }

            return false;
        }

        private static BuildingPrivlidge GetSamSiteToolCupboard(SamSite samSite)
        {
            return samSite.GetBuildingPrivilege(samSite.WorldSpaceBounds());
        }

        private bool ShouldTargetNpcCH47(SamSite samSite, CH47Helicopter ch47)
        {
            // Always allow static sam sites since this will only be called if that is enabled in the config.
            if (samSite.staticRespawn)
                return true;

            if (!SamSiteHasPermission(samSite, PermissionCh47Npc))
                return false;

            return true;
        }

        private bool ShouldTargetPlayerCH47(SamSite samSite, CH47Helicopter ch47)
        {
            // Always allow static sam sites since this will only be called if that is enabled in the config.
            if (samSite.staticRespawn)
                return true;

            if (!SamSiteHasPermission(samSite, PermissionCh47Player))
                return false;

            var mountPoints = ch47.mountPoints;
            if (!IsOccupied(ch47, mountPoints))
                return false;

            if (!_config.CH47Player.CheckCupboardAuth)
                return true;

            var cupboard = GetSamSiteToolCupboard(samSite);
            if ((object)cupboard == null)
                return true;

            if (mountPoints != null)
            {
                foreach (var mountPoint in mountPoints)
                {
                    var player = mountPoint.mountable.GetMounted();
                    if ((object)player != null && IsAuthed(cupboard, player.userID))
                        return false;
                }
            }

            foreach (var child in ch47.children)
            {
                var player = child as BasePlayer;
                if ((object)player != null)
                {
                    if (IsAuthed(cupboard, player.userID))
                        return false;
                }
            }

            return true;
        }

        private bool ShouldTargetPatrolHelicopter(SamSite samSite, PatrolHelicopter patrolHeli)
        {
            // Don't allow static sam sites to target owned helicopters if cupboard auth is required.
            // Otherwise, allow static sam sites since this will only be called if that is enabled in the config.
            if (samSite.staticRespawn)
                return !_config.PatrolHeli.RequireCupboardAuth;

            if (!SamSiteHasPermission(samSite, PermissionPatrolHeli))
                return false;

            // Always allow unowned Patrol Helicopters to be targeted by Sam Sites that have permission.
            if (patrolHeli.OwnerID == 0)
                return true;

            if (_config.PatrolHeli.RequireCupboardAuth)
            {
                var cupboard = GetSamSiteToolCupboard(samSite);

                // Don't allow Sam Sites without a Tool Cupboard to target owned Patrol Helicopters.
                // This could possibly be changed in the future, to follow the Sam Site ownership,
                // if the config option is renamed for clarity.
                if ((object)cupboard == null)
                    return false;

                // Only target the Patrol Helicopter if owned by an authorized player.
                return IsAuthed(cupboard, patrolHeli.OwnerID);
            }

            return true;
        }

        private bool SamSiteHasPermission(SamSite samSite, string perm)
        {
            if (samSite.OwnerID == 0)
                return false;

            return permission.UserHasPermission(samSite.OwnerID.ToString(), perm);
        }

        private Vector3 PredictedPos(BaseEntity target, SamSite samSite, Vector3 targetVelocity, float projectileSpeedMultiplier)
        {
            Vector3 targetpos = target.transform.TransformPoint(target.transform.GetBounds().center);
            Vector3 displacement = targetpos - samSite.eyePoint.transform.position;
            float projectileSpeed = samSite.projectileTest.Get().GetComponent<ServerProjectile>().speed * projectileSpeedMultiplier;
            float targetMoveAngle = Vector3.Angle(-displacement, targetVelocity) * Mathf.Deg2Rad;
            if (targetVelocity.magnitude == 0 || targetVelocity.magnitude > projectileSpeed && Mathf.Sin(targetMoveAngle) / projectileSpeed > Mathf.Cos(targetMoveAngle) / targetVelocity.magnitude)
                return targetpos;

            float shootAngle = Mathf.Asin(Mathf.Sin(targetMoveAngle) * targetVelocity.magnitude / projectileSpeed);
            return targetpos + targetVelocity * displacement.magnitude / Mathf.Sin(Mathf.PI - targetMoveAngle - shootAngle) * Mathf.Sin(shootAngle) / targetVelocity.magnitude;
        }

        private void ShowRocketPath(Vector3 samSitePositon, Vector3 position)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsAdmin && (position - player.transform.position).sqrMagnitude <= DebugDrawDistance*DebugDrawDistance)
                {
                    player.SendConsoleCommand("ddraw.sphere", 5, Color.red, position, 1);
                    player.SendConsoleCommand("ddraw.arrow", 5, Color.red, samSitePositon, position, 1);
                }
            }
        }

        private void ShowRocketDamage(Vector3 position, float amount)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.IsAdmin && (position - player.transform.position).sqrMagnitude <= DebugDrawDistance*DebugDrawDistance)
                {
                    player.SendConsoleCommand("ddraw.text", 5, Color.red, position, amount.ToString());
                }
            }
        }

        #endregion

        #region Target Component

        private class SAMTargetComponent : FacepunchBehaviour, ISamSiteTarget
        {
            public static HashSet<SAMTargetComponent> SAMTargetComponents = new HashSet<SAMTargetComponent>();

            public static void AddToEntity(BaseCombatEntity entity) =>
                entity.GetOrAddComponent<SAMTargetComponent>();

            public static void RemoveFromEntity(BaseCombatEntity entity) =>
                DestroyImmediate(entity.GetComponent<SAMTargetComponent>());

            public BaseEntity Entity;
            public float TargetRangeSquared;
            private GameObject _child;
            private Transform _transform;
            private SamTargetType _targetType;

            private void Awake()
            {
                SAMTargetComponents.Add(this);

                Entity = GetComponent<BaseEntity>();
                _transform = Entity.transform;

                var ch47 = Entity as CH47Helicopter;
                if ((object)ch47 != null)
                {
                    if (IsNpcCH47(ch47))
                    {
                        TargetRangeSquared = Mathf.Pow(_config.CH47Npc.TargetRange, 2);
                        _targetType = _config.CH47Npc.TargetType;
                    }
                    else
                    {
                        TargetRangeSquared = Mathf.Pow(_config.CH47Player.TargetRange, 2);
                        _targetType = _config.CH47Player.TargetType;
                    }
                }

                if (Entity is PatrolHelicopter)
                {
                    TargetRangeSquared = Mathf.Pow(_config.PatrolHeli.TargetRange, 2);
                    _targetType = _config.PatrolHeli.TargetType;

                    _child = Entity.gameObject.CreateChild();
                    _child.gameObject.layer = (int)Rust.Layer.Vehicle_World;
                    _child.AddComponent<SphereCollider>();
                }
            }

            private void OnDestroy()
            {
                if (_child != null)
                {
                    DestroyImmediate(_child);
                }

                SAMTargetComponents.Remove(this);
            }

            public Vector3 Position => _transform.position;

            public SamTargetType SAMTargetType => _targetType;

            public bool isClient => false;

            public bool IsValidSAMTarget(bool isStaticSamSite)
            {
                if (!isStaticSamSite)
                {
                    // If not static, whether to target will be determined in OnSamSiteTarget,
                    // since that is when the sam site itself is available to check permissions.
                    return true;
                }

                var ch47 = Entity as CH47Helicopter;
                if ((object)ch47 != null)
                {
                    return IsNpcCH47(ch47)
                        ? _config.CH47Npc.CanBeTargetedByStaticSamSites
                        : _config.CH47Player.CanBeTargetedByStaticSamSites;
                }

                if (Entity is PatrolHelicopter)
                    return _config.PatrolHeli.CanBeTargetedByStaticSamSites;

                return false;
            }

            public Vector3 CenterPoint() => Entity.CenterPoint();

            public Vector3 GetWorldVelocity() => Entity.GetWorldVelocity();

            public bool IsVisible(Vector3 position, float distance) => Entity.IsVisible(position, distance);
        }

        private void OnSamSiteTargetScan(SamSite samSite, List<ISamSiteTarget> targetList)
        {
            if (samSite.IsInDefenderMode() || SAMTargetComponent.SAMTargetComponents.Count == 0)
                return;

            var samSitePosition = samSite.transform.position;

            foreach (var targetComponent in SAMTargetComponent.SAMTargetComponents)
            {
                if ((samSitePosition - targetComponent.Position).sqrMagnitude <= targetComponent.TargetRangeSquared)
                {
                    targetList.Add(targetComponent);
                }
            }
        }

        private void CanSamSiteShoot(SamSite samSite)
        {
            var targetComponent = samSite.currentTarget as SAMTargetComponent;
            if (targetComponent == null)
                return;

            var ch47 = targetComponent.Entity as CH47Helicopter;
            if (ch47 != null)
            {
                var targetVelocity = targetComponent.gameObject.GetComponent<Rigidbody>().velocity;
                var estimatedPoint = PredictedPos(ch47, samSite, targetVelocity, targetComponent.SAMTargetType.speedMultiplier);
                samSite.currentAimDir = (estimatedPoint - samSite.eyePoint.transform.position).normalized;
                if (_config.DebugRocketPrediction)
                {
                    ShowRocketPath(samSite.eyePoint.position, estimatedPoint);
                }

                return;
            }

            var patrolHeli = targetComponent.Entity as PatrolHelicopter;
            if (patrolHeli != null)
            {
                var patrolHeliAI = patrolHeli.myAI;
                var targetVelocity = patrolHeliAI.GetLastMoveDir() * patrolHeliAI.GetMoveSpeed() * 1.25f;
                var estimatedPoint = PredictedPos(patrolHeli, samSite, targetVelocity, targetComponent.SAMTargetType.speedMultiplier);
                samSite.currentAimDir = (estimatedPoint - samSite.eyePoint.transform.position).normalized;
                if (_config.DebugRocketPrediction)
                {
                    ShowRocketPath(samSite.transform.position, estimatedPoint);
                }

                return;
            }
        }

        #endregion

        #region Configuration

        [JsonObject(MemberSerialization.OptIn)]
        private class HeliSettings
        {
            [JsonProperty("Can be targeted by static SAM Sites")]
            public bool DeprecatedCanBeTargetedByStaticSamSites { set { CanBeTargetedByStaticSamSites = value; } }

            [JsonProperty("Can be targeted by static Sam Sites")]
            public bool CanBeTargetedByStaticSamSites = true;

            [JsonProperty("Targeting range")]
            public float TargetRange = 150;

            [JsonProperty("Rocket speed multiplier")]
            public float RocketSpeedMultiplier = 1;

            [JsonProperty("Rocket damage multiplier")]
            public float RocketDamageMultiplier = 1;

            [JsonProperty("Seconds between rocket bursts")]
            public float SecondsBetweenBursts = 5;

            private SamTargetType _targetType;
            public SamTargetType TargetType
            {
                get
                {
                    if (_targetType == null)
                    {
                        _targetType = new SamTargetType(TargetRange, RocketSpeedMultiplier, SecondsBetweenBursts);
                    }

                    return _targetType;
                }
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class PatrolHeliSettings : HeliSettings
        {
            [JsonProperty("Require cupboard auth for owned helicopters", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool RequireCupboardAuth = false;
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class PlayerCH47Settings : HeliSettings
        {
            [JsonProperty("Check cupboard auth")]
            public bool CheckCupboardAuth = false;
        }

        private class Configuration : BaseConfiguration
        {
            [JsonProperty("NPC CH47 Helicopter")]
            public HeliSettings CH47Npc = new HeliSettings
            {
                RocketDamageMultiplier = 4,
            };

            [JsonProperty("Player CH47 Helicopter")]
            public PlayerCH47Settings CH47Player = new PlayerCH47Settings
            {
                RocketDamageMultiplier = 2,
            };

            [JsonProperty("Patrol Helicopter")]
            public PatrolHeliSettings PatrolHeli = new PatrolHeliSettings
            {
                RocketDamageMultiplier = 4,
                RocketSpeedMultiplier = 1.5f,
            };

            [JsonProperty("Debug rocket prediction")]
            public bool DebugRocketPrediction;

            [JsonProperty("Debug rocket damage")]
            public bool DebugRocketDamage;
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
    }
}
