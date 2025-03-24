using System;
using System.Collections.Generic;
using Oxide.Core;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Water Catcher Boost", "Substrata", "1.0.3")]
    [Description("Boosts the collection rate of water catchers & pumps")]

    class WaterCatcherBoost : RustPlugin
    {
        System.Random random = new System.Random();
        ItemDefinition freshWater = ItemManager.FindItemDefinition(-1779180711);

        void OnWaterCollect(WaterCatcher waterCatcher)
        {
            if (waterCatcher == null || freshWater == null || waterCatcher.IsFull()) return;

            int amount = 0;
            if (waterCatcher.ShortPrefabName.Contains("water_catcher_small"))
                amount = GetAmount(configData.smallWaterCatchers.minBoost, configData.smallWaterCatchers.maxBoost);
            else if (waterCatcher.ShortPrefabName == "water_catcher_large")
                amount = GetAmount(configData.largeWaterCatchers.minBoost, configData.largeWaterCatchers.maxBoost);

            if (amount > 0)
                waterCatcher.inventory.AddItem(freshWater, amount);
        }

        void OnWaterCollect(WaterPump waterPump, ItemDefinition water)
        {
            if (waterPump == null || water == null || waterPump.IsFull()) return;

            int amount = GetAmount(configData.waterPumps.minBoost, configData.waterPumps.maxBoost);

            if (amount > 0)
                waterPump.inventory.AddItem(water, amount);
        }

        int GetAmount(int min, int max)
        {
            if (min < 0) min = 0;
            if (max < 0) max = 0;
            if (min >= max) return max;
            return random.Next(min, max + 1);
        }

        #region Configuration
        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Small Water Catchers")]
            public SmallWaterCatchers smallWaterCatchers { get; set; }
            [JsonProperty(PropertyName = "Large Water Catchers")]
            public LargeWaterCatchers largeWaterCatchers { get; set; }
            [JsonProperty(PropertyName = "Water Pumps")]
            public WaterPumps waterPumps { get; set; }

            public class SmallWaterCatchers
            {
                [JsonProperty(PropertyName = "Minimum Boost")]
                public int minBoost { get; set; }
                [JsonProperty(PropertyName = "Maximum Boost")]
                public int maxBoost { get; set; }
            }

            public class LargeWaterCatchers
            {
                [JsonProperty(PropertyName = "Minimum Boost")]
                public int minBoost { get; set; }
                [JsonProperty(PropertyName = "Maximum Boost")]
                public int maxBoost { get; set; }
            }

            public class WaterPumps
            {
                [JsonProperty(PropertyName = "Minimum Boost")]
                public int minBoost { get; set; }
                [JsonProperty(PropertyName = "Maximum Boost")]
                public int maxBoost { get; set; }
            }

            [JsonProperty(PropertyName = "Version (Do not modify)")]
            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null) throw new Exception();

                if (configData.Version < Version)
                    UpdateConfigValues();

                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                smallWaterCatchers = new ConfigData.SmallWaterCatchers
                {
                    minBoost = 0,
                    maxBoost = 20
                },
                largeWaterCatchers = new ConfigData.LargeWaterCatchers
                {
                    minBoost = 0,
                    maxBoost = 60
                },
                waterPumps = new ConfigData.WaterPumps
                {
                    minBoost = 0,
                    maxBoost = 85
                },
                Version = Version
            };
        }

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");
            ConfigData baseConfig = GetBaseConfig();
            if (configData.Version < new Core.VersionNumber(1, 0, 3))
            {
                configData = baseConfig;
                configData.smallWaterCatchers.minBoost = Convert.ToInt32(GetConfig("Small Water Catchers", "Minimum Boost (per minute)", 0));
                configData.smallWaterCatchers.maxBoost = Convert.ToInt32(GetConfig("Small Water Catchers", "Maximum Boost (per minute)", 20));
                configData.largeWaterCatchers.minBoost = Convert.ToInt32(GetConfig("Large Water Catchers", "Minimum Boost (per minute)", 0));
                configData.largeWaterCatchers.maxBoost = Convert.ToInt32(GetConfig("Large Water Catchers", "Maximum Boost (per minute)", 60));
                int waterPumpMin = Convert.ToInt32(GetConfig("Water Pumps", "Minimum Boost (per minute)", 0));
                int waterPumpMax = Convert.ToInt32(GetConfig("Water Pumps", "Maximum Boost (per minute)", 85));
                configData.waterPumps.minBoost = waterPumpMin > 0 ? (int)Math.Round((double)waterPumpMin / 6) : waterPumpMin;
                configData.waterPumps.maxBoost = waterPumpMax > 0 ? (int)Math.Round((double)waterPumpMax / 6) : waterPumpMax;
            }
            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        private object GetConfig(string menu, string dataValue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
            }
            object value;
            if (!data.TryGetValue(dataValue, out value))
            {
                value = defaultValue;
                data[dataValue] = value;
            }
            return value;
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();
        protected override void SaveConfig() => Config.WriteObject(configData, true);
        #endregion
    }
}
