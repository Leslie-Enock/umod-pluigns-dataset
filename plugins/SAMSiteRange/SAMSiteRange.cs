using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("SAM Site Range", "nivex", "1.2.8")]
    [Description("Modifies SAM site range.")]
    internal class SAMSiteRange : RustPlugin
    {
        #region Oxide Hooks

        private void Init()
        {
            foreach (var perm in config.permissions.Keys)
            {
                if (!permission.PermissionExists(perm))
                {
                    permission.RegisterPermission(perm, this);
                }
            }
        }

        private object OnSamSiteTargetScan(SamSite ss, List<SamSite.ISamSiteTarget> result)
        {
            if (GetSamSiteScanRange(ss, out var vehicleRange, out var missileRange))
            {
                if (!ss.IsInDefenderMode())
                {
                    AddVehicleTargetSet(ss, result, vehicleRange);
                }
                AddMLRSRockets(ss, result, missileRange);
                return true;
            }
            return null;
        }

        private void AddVehicleTargetSet(SamSite ss, List<SamSite.ISamSiteTarget> allTargets, float scanRadius)
        {
            if (SamSite.ISamSiteTarget.serverList.Count == 0)
            {
                return;
            }
            foreach (SamSite.ISamSiteTarget server in SamSite.ISamSiteTarget.serverList)
            {
                if (!(server is MLRSRocket) && server is BaseEntity entity && !entity.IsDestroyed && Vector3.Distance(entity.CenterPoint(), ss.eyePoint.transform.position) < scanRadius)
                {
                    allTargets.Add(server);
                }
            }
        }

        private void AddMLRSRockets(SamSite ss, List<SamSite.ISamSiteTarget> allTargets, float scanRadius)
        {
            if (MLRSRocket.serverList.Count == 0)
            {
                return;
            }
            foreach (MLRSRocket server in MLRSRocket.serverList)
            {
                if (server != null && !server.IsDestroyed && Vector3.Distance(server.transform.position, ss.transform.position) < scanRadius)
                {
                    allTargets.Add(server);
                }
            }
        }

        #endregion Oxide Hooks

        #region Methods

        [PluginReference] Core.Plugins.Plugin RaidableBases;

        private bool RaidableTerritory(BaseEntity entity) => RaidableBases != null && Convert.ToBoolean(RaidableBases?.Call("HasEventEntity", entity));

        private bool GetSamSiteScanRange(SamSite ss, out float vehicleRange, out float missileRange)
        {
            if (ss != null && !ss.IsDestroyed)
            {
                if (ss.OwnerID == 0 || ss.staticRespawn)
                {
                    vehicleRange = config.staticVehicleRange;
                    missileRange = config.staticMissileRange;
                    return !RaidableTerritory(ss);
                }
                if (ss.OwnerID.IsSteamId() && GetPermissionSettings(ss.OwnerID.ToString(), out var permissionSettings))
                {
                    vehicleRange = permissionSettings.vehicleScanRadius;
                    missileRange = permissionSettings.missileScanRadius;
                    return true;
                }
            }
            vehicleRange = missileRange = 0f;
            return false;
        }

        private bool GetPermissionSettings(string playerId, out PermissionSettings permissionSettings)
        {
            int priority = 0;
            permissionSettings = null;
            foreach (var (perm, settings) in config.permissions)
            {
                if (settings.priority >= priority && permission.UserHasPermission(playerId, perm))
                {
                    priority = settings.priority;
                    permissionSettings = settings;
                }
            }
            return permissionSettings != null;
        }

        #endregion Methods

        #region ConfigurationFile

        private ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Static SamSite Vehicle Scan Range")]
            public float staticVehicleRange = 150f;

            [JsonProperty(PropertyName = "Static SamSite Missile Scan Range")]
            public float staticMissileRange = 225f;

            [JsonProperty(PropertyName = "Permissions", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, PermissionSettings> permissions = new()
            {
                ["samsiterange.use"] = new()
                {
                    priority = 0,
                    vehicleScanRadius = 200f,
                    missileScanRadius = 275f,
                },
                ["samsiterange.vip"] = new()
                {
                    priority = 1,
                    vehicleScanRadius = 250f,
                    missileScanRadius = 325f,
                }
            };
        }

        private class PermissionSettings
        {
            public int priority;
            public float vehicleScanRadius;
            public float missileScanRadius;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch (Exception ex)
            {
                PrintError($"The configuration file is corrupted. \n{ex}");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            config = new();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion ConfigurationFile
    }
}