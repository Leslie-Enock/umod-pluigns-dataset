using System.Collections.Generic;
using System;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Electric Generator Tweaker", "FastBurst", "1.0.1")]
    [Description("Change Electric Generator Attributes")]

    public class ElectricGeneratorTweaker : RustPlugin
    {
        private const string PERMS = "electricgeneratortweaker.tweak";

        #region Hooks
        private void Init()
        {
            permission.RegisterPermission(PERMS, this);
        }

        private void OnServerInitialized()
        {
            if (configData.General.ElectricGeneratorWorld)
                SetAnElectricGeneratorWorld();
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            ElectricGenerator generator = entity.GetComponent<ElectricGenerator>();
            if (generator != null)
            {
                if (configData.General.enableDebug)
                    Puts($"ELECTRIC GENERATOR SPAWN!");

                bool istweaker = permission.UserHasPermission(generator.OwnerID.ToString(), PERMS);

                if (configData.General.ElectricGeneratorWorld || istweaker)
                    ElectricGeneratorTweakerizer(generator);
            }
        }
        #endregion

        #region Functions
        private void SetAnElectricGeneratorWorld()
        {
            foreach (var generator in UnityEngine.Object.FindObjectsOfType<ElectricGenerator>())
                ElectricGeneratorTweakerizer(generator);
        }

        private void ElectricGeneratorTweakerizer(ElectricGenerator generator)
        {
            if (generator.OwnerID == 0)
                return;

            generator.electricAmount = configData.Output.electricAmount;

            if (configData.General.enableDebug)
                Puts($"electricAmount {generator.electricAmount}");
        }
        #endregion

        #region Config        
        private static ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Electric Generator")]
            public GeneralSettings General { get; set; }
            [JsonProperty(PropertyName = "Electric Generator Attributes")]
            public OutputSettings Output { get; set; }

            public class GeneralSettings
            {
                [JsonProperty(PropertyName = "Setting for all World")]
                public bool ElectricGeneratorWorld { get; set; }
                [JsonProperty(PropertyName = "Enable Debug option to console output (default false)")]
                public bool enableDebug { get; set; }
            }

            public class OutputSettings
            {
                [JsonProperty(PropertyName = "Amount of electricity (100 by default)")]
                public float electricAmount { get; set; }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                General = new ConfigData.GeneralSettings
                {
                    ElectricGeneratorWorld = true,
                    enableDebug = false
                },
                Output = new ConfigData.OutputSettings
                {
                    electricAmount = 100f
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();
            if (configData.Version < new Core.VersionNumber(1, 0, 0))
                configData = baseConfig;

            if (configData.Version < new Core.VersionNumber(1, 0, 1))
                configData.General.enableDebug = false;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

    }
}
