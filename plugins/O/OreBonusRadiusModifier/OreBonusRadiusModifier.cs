using System;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Ore Bonus Radius Modifier", "Ryz0r", "1.0.1")]
    [Description("Allows you to modify the size of an Ore Bonus (hotspot) radius")]
    public class OreBonusRadiusModifier : RustPlugin
    {
        private static Configuration _config;
        protected override void SaveConfig() => Config.WriteObject(_config);
        protected override void LoadDefaultConfig() => _config = new Configuration();

        private class Configuration
        {
            [JsonProperty(PropertyName = "Bonus Radius (Default 0.15)")]
            public float BonusRadius = 1f;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        private void OnEntitySpawned(OreHotSpot spot) => NextTick(() =>
        {
            spot.GetComponent<SphereCollider>().radius = _config.BonusRadius;
        });

        private void OnServerInitialized()
        {
            foreach (var spot in BaseNetworkable.serverEntities.OfType<OreHotSpot>())
            {
                OnEntitySpawned(spot);
            }
        }

        private void Unload()
        {
            foreach (var spot in BaseNetworkable.serverEntities.OfType<OreHotSpot>())
            {
                spot.GetComponent<SphereCollider>().radius = 0.15f;
            }
        }
    }
}