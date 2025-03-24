using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Quantum Battery", "Colon Blow", "1.0.3")]
    [Description("Fully Charged and Endless Batteries")]

    public class QuantumBattery : CovalencePlugin
    {

        #region Load

        private const string permMax = "quantumbattery.allowed";
        private bool initComplete = false;

        private void Init()
        {
            permission.RegisterPermission(permMax, this);
        }

        private void OnServerInitialized()
        {
            ProcessExistingBatteries();
            initComplete = true;
        }

        #endregion

        #region Configuration

        private static PluginConfig config;

        private class PluginConfig
        {
            public QuantumSettings quantumSettings { get; set; }

            public class QuantumSettings
            {
                [JsonProperty(PropertyName = "Quantum Battery - Enable Max Connection change : ")] public bool enableMaxOutput { get; set; }
                [JsonProperty(PropertyName = "Quantum Battery - Reset Max Allowed Connections to : ")] public int maxQuantumOutput { get; set; }
            }

            public static PluginConfig DefaultConfig() => new PluginConfig()
            {
                quantumSettings = new PluginConfig.QuantumSettings
                {
                    enableMaxOutput = true,
                    maxQuantumOutput = 9999,
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

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SpawnedBattery"] = "Your Quantum Battery is Fully Charged and will never die !!"
            }, this);
        }

        #endregion

        #region Hooks

        private void OnEntitySpawned(ElectricBattery battery)
        {
            if (!initComplete) return;
            ProcessBattery(battery);
        }

        private void ProcessExistingBatteries()
        {
            var batteryList = UnityEngine.Object.FindObjectsOfType<ElectricBattery>();
            foreach (var battery in batteryList)
            {
                ProcessBattery(battery);
            }
        }

        private void ProcessBattery(ElectricBattery battery)
        {
            var iplayer = covalence.Players.FindPlayerById(battery.OwnerID.ToString());
            if (iplayer != null && iplayer.HasPermission(permMax))
            {
                var makeQuantum = battery.gameObject.AddComponent<BatteryAutoCharger>();
                if (initComplete && iplayer.IsConnected) iplayer.Message(lang.GetMessage("SpawnedBattery", this, iplayer.Id));
            }
        }

        #endregion

        #region Battery AutoCharger

        private class BatteryAutoCharger : MonoBehaviour
        {
            ElectricBattery quantumBattery;
            float maxCapacity;

            private void Awake()
            {
                quantumBattery = GetComponent<ElectricBattery>();
                maxCapacity = quantumBattery.maxCapactiySeconds;
                if (config.quantumSettings.enableMaxOutput) quantumBattery.maxOutput = config.quantumSettings.maxQuantumOutput;
            }

            private void FixedUpdate()
            {
                quantumBattery.rustWattSeconds = maxCapacity;
            }

            private void OnDestroy()
            {
                GameObject.Destroy(this);
            }

        }

        #endregion
    }
}