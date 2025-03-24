using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BuildRevert", "nivex", "1.2.2")]
    [Description("Prevent building in blocked area.")]
    public class BuildRevert : RustPlugin
    {
        [PluginReference] Plugin RaidableBases, AbandonedBases, ZoneManager;

        private List<ZoneInfo> excludedZones { get; set; } = new();
        private List<Construction> blocked = new();

        public class ZoneInfo
        {
            internal Vector3 origin;
            internal Vector3 extents;
            internal float distance;

            public ZoneInfo(object origin, object radius, object size)
            {
                this.origin = (Vector3)origin;

                if (radius is float r)
                {
                    distance = r;
                }

                if (size is Vector3 v && v != Vector3.zero)
                {
                    extents = v * 0.5f;
                }
            }

            public bool IsPositionInZone(Vector3 point)
            {
                if (extents != Vector3.zero)
                {
                    var v = Quaternion.Inverse(Quaternion.identity) * (point - origin);

                    return v.x <= extents.x && v.x > -extents.x && v.y <= extents.y && v.y > -extents.y && v.z <= extents.z && v.z > -extents.z;
                }
                return InRange2D(origin, point, distance);
            }

            private bool InRange2D(Vector3 a, Vector3 b, float distance)
            {
                return (new Vector3(a.x, 0f, a.z) - new Vector3(b.x, 0f, b.z)).sqrMagnitude <= distance * distance;
            }
        }

        private void Init()
        {
            permission.RegisterPermission("buildrevert.bypass", this);
            Unsubscribe(nameof(CanBuild));
        }

        private void OnServerInitialized(bool isStartup)
        {
            LoadVariables();
            SetupExcludedZones(true);
            Subscribe(nameof(CanBuild));
        }

        private object CanBuild(Planner planner, Construction construction, Construction.Target target)
        {
            if (!blocked.Contains(construction) || permission.UserHasPermission(target.player.UserIDString, "buildrevert.bypass"))
            {
                return null;
            }

            var buildPos = target.entity && target.entity.transform && target.socket ? target.GetWorldPosition() : target.position;

            if (!IsExcluded(buildPos) && target.player.IsBuildingBlocked(new OBB(buildPos, Quaternion.identity, target.player.bounds), cached: true) && !EventTerritory(buildPos))
            {
                if (useToasts) target.player.ShowToast(toastStyle, GetLang("Building is blocked: Toast", target.player.UserIDString));
                else Player.Message(target.player, GetLang("Building is blocked!", target.player.UserIDString));
                return false;
            }

            return null;
        }

        private bool EventTerritory(Vector3 buildPos)
        {
            return RaidableBases != null && Convert.ToBoolean(RaidableBases?.Call("EventTerritory", buildPos)) || AbandonedBases != null && Convert.ToBoolean(AbandonedBases?.Call("EventTerritory", buildPos));
        }

        private void SetupExcludedZones(bool message)
        {
            if (ZoneManager == null || !ZoneManager.IsLoaded)
            {
                return;
            }

            timer.Once(60f, () => SetupExcludedZones(false));

            var zoneIds = ZoneManager?.Call("GetZoneIDs") as string[];

            if (zoneIds == null)
            {
                return;
            }

            excludedZones.Clear();

            foreach (string zoneId in zoneIds)
            {
                var zoneLoc = ZoneManager.Call("GetZoneLocation", zoneId);

                if (!(zoneLoc is Vector3))
                {
                    continue;
                }

                var zoneName = Convert.ToString(ZoneManager.Call("GetZoneName", zoneId));

                if (!allowedZones.Exists(zone => !string.IsNullOrEmpty(zone) && zone == zoneId || !string.IsNullOrEmpty(zoneName) && zoneName.Equals(zone, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                var radius = ZoneManager.Call("GetZoneRadius", zoneId);
                var size = ZoneManager.Call("GetZoneSize", zoneId);

                excludedZones.Add(new ZoneInfo(zoneLoc, radius, size));
            }

            if (message && excludedZones.Count > 0)
            {
                Puts("{0} zones have been excluded", excludedZones.Count);
            }
        }

        private bool IsExcluded(Vector3 position)
        {
            foreach (var zone in excludedZones)
            {
                if (zone.IsPositionInZone(position))
                {
                    return true;
                }
            }
            return false;
        }

        #region Config
        private bool Changed;
        private List<string> allowedZones = new();

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Building is blocked!"] = "<color=#FF0000>Building is blocked!</color>",
                ["Building is blocked: Toast"] = "You don't have permission to build here"
            }, this);
        }

        private bool useToasts;
        private GameTip.Styles toastStyle;
        
        private void LoadVariables()
        {
            useToasts = Convert.ToBoolean(GetConfig("Messages", "Use Toasts", true));
            toastStyle = (GameTip.Styles)GetConfig("Messages", "Toast Style (0 = blue normal, 1 = red normal, 2 = blue long, 3 = blue short, 4 = server event, 5 = error)", GameTip.Styles.Error);

            //PrefabAttribute.server.Find<Construction>(2150203378).canBypassBuildingPermission = false;
            Dictionary<string, uint> pooledStrings = new();
            foreach (var p in GameManifest.Current.pooledStrings)
            {
                pooledStrings[p.str.ToLower()] = p.hash;
            }

            Dictionary<Construction, bool> constructions = new();

            foreach (string str in GameManifest.Current.entities)
            {
                if (!pooledStrings.ContainsKey(str.ToLower())) continue;

                var construction = PrefabAttribute.server.Find<Construction>(pooledStrings[str.ToLower()]);
                
                if (construction != null)
                {
                    string value = string.Format("Allow {0}", construction.hierachyName.Replace("PrefabPreProcess - Server/", string.Empty));
                    constructions[construction] = Convert.ToBoolean(GetConfig("Constructions", value, true));
                    if (!constructions[construction]) blocked.Add(construction);
                }
            }

            foreach (var zone in GetConfig("Zone Manager", "Excluded Zones", new List<object> { "pvp", "99999999" }) as List<object>)
            {
                allowedZones.Add(zone.ToString());
            }

            if (Changed)
            {
                SaveConfig();
                Changed = false;
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();
        }

        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        private string GetLang(string key, string id = null) => lang.GetMessage(key, this, id);
        #endregion
    }
}