using Rust;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Rock Block", "Author Nogrod, Maintainer nivex", "1.1.4")]
    [Description("Blocks players from building in rocks")]
    class RockBlock : RustPlugin
    {
        private ConfigData config;
        private RaycastHit _hit;
        private const string permBypass = "rockblock.bypass";
        private readonly int worldLayer = LayerMask.GetMask("World", "Default");
        private Dictionary<string, string> _displayNames = new Dictionary<string, string>();

        private class ConfigData
        {
            public bool AllowCave { get; set; }
            public bool Logging { get; set; }
            public int MaxHeight { get; set; }
            public bool Kill { get; set; } = true;
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData
            {
                AllowCave = false,
                Logging = true,
                MaxHeight = -1,
                Kill = true
            };
            Config.WriteObject(config, true);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["DistanceTooHigh"] = "Distance to ground too high: {0}",
                ["PlayerSuspected"] = "{0} is suspected of building {1} inside a rock at {2}!"
            }, this);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try { config = Config.ReadObject<ConfigData>(); } catch { }
            if (config == null) LoadDefaultConfig();
            Config.WriteObject(config, true);
        }

        private void Init()
        {            
            permission.RegisterPermission(permBypass, this);
            if (!config.Logging) Unsubscribe(nameof(OnServerInitialized));
        }

        private void OnServerInitialized()
        {
            foreach (var def in ItemManager.GetItemDefinitions())
            {
                var imd = def.GetComponent<ItemModDeployable>();
                if (imd == null || _displayNames.ContainsKey(imd.entityPrefab.resourcePath)) continue;
                _displayNames.Add(imd.entityPrefab.resourcePath, def.displayName.english);
            }
        }

        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            BasePlayer player = planner.GetOwnerPlayer();
            if (player == null || permission.UserHasPermission(player.UserIDString, permBypass))
            {
                return;
            }
            
            BaseEntity entity = gameObject.GetComponent<BaseEntity>();

            if (config.MaxHeight > 0)
            {
                RaycastHit hit;
                if (Physics.Raycast(new Ray(entity.transform.position, Vector3.down), out hit, float.PositiveInfinity, Rust.Layers.Terrain))
                {
                    if (hit.distance > config.MaxHeight)
                    {
                        SendReply(player, string.Format(lang.GetMessage("DistanceTooHigh", this, player.UserIDString), hit.distance));
                        entity.Invoke(entity.KillMessage, 0.1f);
                        return;
                    }
                }
            }

            CheckEntity(entity, player);
        }

        private void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            BasePlayer player = deployer.GetOwnerPlayer();

            if (player != null && !permission.UserHasPermission(player.UserIDString, permBypass))
            {
                CheckEntity(entity, player);
            }
        }

        private void CheckEntity(BaseEntity entity, BasePlayer player)
        {
            if (entity == null)
            {
                return;
            }

            RaycastHit[] targets = Physics.RaycastAll(new Ray(entity.transform.position + Vector3.up * 200f, Vector3.down), 250, worldLayer);

            foreach (RaycastHit hit in targets)
            {
                if (hit.collider == null || !hit.collider.name.Contains("rock_") || !IsInside(hit.collider, entity) && !IsInCave(entity))
                {
                    continue;
                }

                if (config.Logging)
                {
                    string name;
                    if (!_displayNames.TryGetValue(entity.gameObject.name, out name))
                    {
                        name = entity.ShortPrefabName;
                    }

                    Puts(lang.GetMessage("PlayerSuspected", this), player.displayName, name, entity.transform.position);
                }

                if (config.Kill) entity.Invoke(entity.KillMessage, 0.1f);
                
                break;
            }
        }

        private bool IsInCave(BaseEntity entity)
        {
            if (!config.AllowCave)
            {
                return false;
            }

            RaycastHit[] targets = Physics.RaycastAll(new Ray(entity.transform.position, Vector3.up), 250, worldLayer);

            foreach (RaycastHit hit in targets)
            {
                if (hit.collider.name.Contains("rock_"))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsInside(Collider collider, BaseEntity entity)
        {
            var center = entity.WorldSpaceBounds().ToBounds().center;
            var rotation = entity.transform.rotation;
            var size = entity.bounds.extents;

            var points = new List<Vector3> // credits ZoneManger/k1lly0u
            {
                RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z + size.z), center, rotation),
                RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z + size.z), center, rotation),
                RotatePointAroundPivot(new Vector3(center.x + size.x, center.y + size.y, center.z - size.z), center, rotation),
                RotatePointAroundPivot(new Vector3(center.x + size.x, center.y - size.y, center.z - size.z), center, rotation),
                RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z + size.z), center, rotation),
                RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z + size.z), center, rotation),
                RotatePointAroundPivot(new Vector3(center.x - size.x, center.y + size.y, center.z - size.z), center, rotation),
                RotatePointAroundPivot(new Vector3(center.x - size.x, center.y - size.y, center.z - size.z), center, rotation)
            };

            Physics.queriesHitBackfaces = true;

            bool isInside = points.TrueForAll(point => IsInside(point));

            Physics.queriesHitBackfaces = false;

            return isInside;
        }

        private bool IsInside(Vector3 point)
        {
            if (Physics.Raycast(point, Vector3.up, out _hit, 20f, Layers.Mask.World, QueryTriggerInteraction.Ignore) && _hit.collider.name.Contains("rock_"))
            {
                var hits = Physics.RaycastAll(point + new Vector3(0f, 0.1f, 0f), Vector3.down, 50f, Layers.Mask.World | Layers.Mask.Terrain, QueryTriggerInteraction.Ignore);

                if (hits != null && hits.Length > 0)
                {
                    return hits.Last().collider.name.Contains("rock_");
                }

                return true;
            }

            return false;
        }

        private Vector3 RotatePointAroundPivot(Vector3 point, Vector3 pivot, Quaternion rotation) => rotation * (point - pivot) + pivot; // credits ZoneManger/k1lly0u
    }
}
