using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Pills Here", "Wulf", "3.2.1")]
    [Description("Recovers health, hunger, and/or hydration by set amounts on item use")]
    class PillsHere : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Calories amount (0.0 - 500.0)")]
            public float CaloriesAmount = 50f;

            [JsonProperty("Health amount (0.0 - 100.0)")]
            public float HealthAmount = 10f;

            [JsonProperty("Hydration amount (0.0 - 250.0)")]
            public float HydrationAmount = 25f;

            [JsonProperty("Item ID or short name to use")]
            public string ItemIdOrShortName = "antiradpills";

            [JsonProperty("Use permission system")]
            public bool UsePermissions = true;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            LogWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Initialization

        private const string permUse = "pillshere.use";

        private void Init()
        {
            permission.RegisterPermission(permUse, this);

            // Check if item name or ID used is old or not set
            if (string.IsNullOrEmpty(config.ItemIdOrShortName) || config.ItemIdOrShortName == "1685058759")
            {
                LogWarning("Old or no item configured, using default item: antiradpills");
                config.ItemIdOrShortName = "antiradpills";
                SaveConfig();
            }
        }

        #endregion Initialization

        #region Item Handling

        private void OnItemUse(Item item)
        {
            // Check if item name or ID used matches what is configured
            if (item.info.itemid.ToString() != config.ItemIdOrShortName && item.info.shortname != config.ItemIdOrShortName) // -1432674913 or antiradpills
            {
                return;
            }

            // Check of item was used by a real player
            BasePlayer player = item.GetOwnerPlayer();
            if (player == null)
            {
                return;
            }

            // Check if player has permission to use this
            if (config.UsePermissions && !permission.UserHasPermission(player.UserIDString, permUse))
            {
                return;
            }

            // Heal player and restore calories and hydration
            float targetHydration = player.metabolism.hydration.lastValue + config.HydrationAmount;
            player.metabolism.hydration.value = Mathf.Clamp(targetHydration, player.metabolism.hydration.min, player.metabolism.hydration.max); // Max: 250
            float targetCalories = player.metabolism.calories.lastValue + config.CaloriesAmount;
            player.metabolism.calories.value = Mathf.Clamp(targetCalories, player.metabolism.calories.min, player.metabolism.calories.max); // Max: 500
            player.Heal(config.HealthAmount);
        }

        #endregion Item Handling
    }
}
