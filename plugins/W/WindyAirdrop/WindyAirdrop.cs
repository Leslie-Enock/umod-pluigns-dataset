using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Windy Airdrop", "Colon Blow", "1.0.1")]
    [Description("Airdrops move some as if its windy")]

    public class WindyAirdrop : CovalencePlugin
    {

        //Rust update fixes

        #region Load

        private bool initComplete = false;

        private void OnServerInitialized()
        {
            initComplete = true;
        }

        #endregion

        #region Configuration

        private static PluginConfig config;

        private class PluginConfig
        {
            public WindyAirdropSettings windyAirdropSettings { get; set; }

            public class WindyAirdropSettings
            {
                [JsonProperty(PropertyName = "Wind Speed Max - Maximus wind speed : ")] public float windspeedMax { get; set; }
                [JsonProperty(PropertyName = "Wind Speed Min - Minimum wind speed : ")] public float windspeedMin { get; set; }
            }

            public static PluginConfig DefaultConfig() => new PluginConfig()
            {
                windyAirdropSettings = new PluginConfig.WindyAirdropSettings
                {
                    windspeedMax = 5f,
                    windspeedMin = 1f,
                }
            };
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created!!");
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            SaveConfig();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Hooks

        private void OnEntitySpawned(SupplyDrop supplyDrop)
        {
            if (!initComplete) return;
            var windModifier = supplyDrop.gameObject.AddComponent<SupplyDropModifier>();
        }

        #endregion

        #region SupplyDrop Wind

        private class SupplyDropModifier : MonoBehaviour
        {
            WindyAirdrop.PluginConfig.WindyAirdropSettings airdropSettings = new PluginConfig.WindyAirdropSettings();
            SupplyDrop supplyDrop;

            Vector3 windDir;
            Vector3 newDir;
            float windSpeed;
            int counter;
            int nextwind;
            bool dropinit = false;

            private void Awake()
            {
                airdropSettings = config.windyAirdropSettings;
                if (airdropSettings == null) { OnDestroy(); return; }
                supplyDrop = GetComponent<SupplyDrop>();
                if (supplyDrop == null) { OnDestroy(); return; }

                windDir = GetDirection();
                windSpeed = Random.Range(airdropSettings.windspeedMin, airdropSettings.windspeedMax);
                counter = 0;
                nextwind = GetRandomInt();
                dropinit = true;
            }

            private Vector3 GetDirection()
            {
                var direction = Random.insideUnitSphere * 5f;
                if (direction.y > -windSpeed) direction.y = -windSpeed;
                return direction;
            }

            private int GetRandomInt()
            {
                var ranInt = Random.Range(100, 1000);
                return ranInt;
            }

            private void FixedUpdate()
            {
                if (!dropinit) return;
                if (supplyDrop == null) { OnDestroy(); return; }
                newDir = Vector3.RotateTowards(transform.forward, windDir, 0.5f * Time.deltaTime, 0.0F);
                newDir.y = 0f;
                supplyDrop.transform.position = Vector3.MoveTowards(transform.position, transform.position + windDir, (windSpeed) * Time.deltaTime);
                supplyDrop.transform.rotation = Quaternion.LookRotation(newDir);
                if (counter == nextwind) { windDir = GetDirection(); counter = 0; nextwind = GetRandomInt(); }
                counter++;
            }

            private void OnDestroy()
            {
                GameObject.Destroy(this);
            }
        }

        #endregion
    }
}