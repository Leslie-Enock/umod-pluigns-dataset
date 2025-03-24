using System.Collections.Generic;
using System.Linq;
using Facepunch;
using Newtonsoft.Json;
using Rust;

namespace Oxide.Plugins
{
    [Info("Tool Cupboard Extender", "MJSU", "0.0.1")]
    [Description("Extends the range of the tool cupboard")]
    internal class TcExtender : RustPlugin
    {
        #region Class Fields
        private PluginConfig _pluginConfig; //Plugin Config

        private const string UsePermission = "tcextender.use";

        private float _maxRange;
        private readonly Hash<ulong, float> _playerRangeCache = new Hash<ulong, float>();
        #endregion

        #region Setup & Loading
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            config.Ranges = config.Ranges ?? new Hash<string, float>
            {
                [UsePermission] = 16f
            };
            return config;
        }
        
        private void OnServerInitialized()
        {
            _maxRange = _pluginConfig.Ranges.Values.Max();
            
            foreach (KeyValuePair<string,float> ranges in _pluginConfig.Ranges)
            {
                permission.RegisterPermission(ranges.Key, this);
                if (ranges.Value < 16f)
                {
                    PrintWarning($"{ranges.Key} permission has a range less than 16.0 which is not allowed!");
                }
            }
        }
        #endregion

        #region Permission Hooks

        private void OnUserPermissionGranted(string playerId, string permName)
        {
            _playerRangeCache.Remove(ulong.Parse(playerId));
        }
        
        private void OnUserPermissionRevoked(string playerId, string permName)
        {
            _playerRangeCache.Remove(ulong.Parse(playerId));
        }
        
        private void OnUserGroupAdded(string playerId, string groupName)
        {
            _playerRangeCache.Remove(ulong.Parse(playerId));
        }
        
        private void OnUserGroupRemoved(string playerId, string groupName)
        {
            _playerRangeCache.Remove(ulong.Parse(playerId));
        }

        private void OnGroupPermissionGranted(string groupName, string permName)
        {
           _playerRangeCache.Clear();
        }
        
        private void OnGroupPermissionRevoked(string groupName, string permName)
        {
            _playerRangeCache.Clear();
        }
        #endregion

        #region uMod Hook

        private BuildingPrivlidge OnBuildingPrivilege(BaseEntity entity, OBB obb)
        {
            BuildingBlock block = null;
            BuildingPrivlidge bp = null;
            List<BuildingBlock> blocks = Pool.GetList<BuildingBlock>();
            Vis.Entities(obb.position, _maxRange + obb.extents.magnitude, blocks, Layers.Construction);
            for (int i = 0; i < blocks.Count; i++)
            {
                BuildingBlock item = blocks[i];
                if (item.isServer == entity.isServer 
                    && item.IsOlderThan(block) 
                    && obb.Distance(item.WorldSpaceBounds()) <= GetPlayerRange(item.OwnerID))
                {
                    BuildingManager.Building building = item.GetBuilding();
                    if (building != null)
                    {
                        BuildingPrivlidge buildingTc = building.GetDominatingBuildingPrivilege();
                        if (buildingTc != null)
                        {
                            block = item;
                            bp = buildingTc;
                        }
                    }
                }
            }
            
            Pool.FreeList(ref blocks);
            return bp;
        }
        #endregion

        #region Helper Methods
        private float GetPlayerRange(ulong playerId)
        {
            if (_playerRangeCache.ContainsKey(playerId))
            {
                return _playerRangeCache[playerId];
            }
        
            foreach (KeyValuePair<string, float> rangePerm in _pluginConfig.Ranges.OrderByDescending(pr => pr.Value))
            {
                if (HasPermission(playerId, rangePerm.Key))
                {
                    _playerRangeCache[playerId] = rangePerm.Value;
                    return rangePerm.Value;
                }
            }

            _playerRangeCache[playerId] = 16f;
            return 16f;
        }
        
        private bool HasPermission(ulong playerId, string perm) => permission.UserHasPermission(playerId.ToString(), perm);
        #endregion

        #region Classes
        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Permission Ranges (Meters)")]
            public Hash<string, float> Ranges { get; set; }
        }
        #endregion
    }
}
