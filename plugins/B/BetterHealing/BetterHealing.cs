using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Better Healing", "Default", "2.0.0")]
    [Description("Customization of healing and food items.")]
    public class BetterHealing : RustPlugin
    {
        // Declares the permission to use this plugin
        private static string medicalPermission = "betterhealing.medical";
        private static string foodPermission = "betterhealing.food";

        private void Init()
        {
            permission.RegisterPermission(medicalPermission, this);
            permission.RegisterPermission(foodPermission, this);
        }

        private object OnHealingItemUse(MedicalTool tool, BasePlayer player)
        {
            var toolName = tool.GetItem()?.info?.shortname;
            if (toolName != null && permission.UserHasPermission(player.UserIDString, medicalPermission))
            {
                if (_config.healingItemSettings.medicalItems.ContainsKey(toolName))
                {
                    var HealAmount = _config.healingItemSettings.medicalItems[toolName].HealAmount;
                    player.health = player.health + HealAmount;
                    if (_config.healingItemSettings.medicalItems[toolName].HealOverTimeAmount > 0)
                    {
                        var HealOverTime = _config.healingItemSettings.medicalItems[toolName].HealOverTimeAmount;
                        player.metabolism.ApplyChange(MetabolismAttribute.Type.HealthOverTime, HealOverTime, 1f);
                    }
                    player.metabolism.poison.Subtract(Math.Abs(_config.healingItemSettings.medicalItems[toolName].Poison));
                    player.metabolism.bleeding.Subtract(Math.Abs(_config.healingItemSettings.medicalItems[toolName].Bleed));
                    player.metabolism.radiation_poison.Subtract(Math.Abs(_config.healingItemSettings.medicalItems[toolName].Radiation));
                    return true;
                }
            }
            return null;
        }

        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (action == "consume" && permission.UserHasPermission(player.UserIDString, foodPermission))
            {
                var name = item.info.shortname;
                if (_config.healingItemSettings.foodItems.ContainsKey(name))
                {
                    item.UseItem(1);
                    var chance = UnityEngine.Random.Range(_config.healingItemSettings.pickleChanceMin, _config.healingItemSettings.pickleChanceMax);
                    if (name.Contains("can"))
                    {
                        switch (name)
                        {
                            case "can.tuna":
                                player.GiveItem(ItemManager.CreateByPartialName("can.tuna.empty"));
                                break;
                            case "can.beans":
                                player.GiveItem(ItemManager.CreateByPartialName("can.beans.empty"));
                                break;
                        }
                    }
                    else if (name == "jar.pickle" && chance == _config.healingItemSettings.pickleEffect)
                    {
                        player.metabolism.poison.Add(8);
                        player.metabolism.hydration.Subtract(50);
                        player.metabolism.calories.Subtract(50);
                    }
                    player.health += (_config.healingItemSettings.foodItems[name].HealAmount);
                    player.metabolism.ApplyChange(MetabolismAttribute.Type.HealthOverTime, (_config.healingItemSettings.foodItems[name].HealOverTimeAmount), 1f);
                    player.metabolism.calories.Add(_config.healingItemSettings.foodItems[name].Calories);
                    if (_config.healingItemSettings.foodItems[name].Hydration > 0)
                    {
                        player.metabolism.hydration.Add(_config.healingItemSettings.foodItems[name].Hydration);
                    }
                    else
                    {
                        player.metabolism.hydration.Subtract(Math.Abs(_config.healingItemSettings.foodItems[name].Hydration));
                    }
                    if (_config.healingItemSettings.foodItems[name].Poison > 0)
                    {
                        player.metabolism.poison.Add(_config.healingItemSettings.foodItems[name].Poison);
                    }
                    else if (_config.healingItemSettings.foodItems[name].Poison < 0)
                    {
                        player.metabolism.poison.Subtract(Math.Abs(_config.healingItemSettings.foodItems[name].Poison));
                    }
                    if (name.Contains("raw") && !name.Contains("fish") || (name == "jar.pickle" && chance == 2))
                    {
                        Effect.server.Run("assets/bundled/prefabs/fx/gestures/drink_vomit.prefab", player.ServerPosition + new Vector3(0, 1, 0));
                    }
                    else
                    {
                        Effect.server.Run("assets/bundled/prefabs/fx/gestures/eat_generic.prefab", player.ServerPosition + new Vector3(0, 1, 0));
                    }
                    return true;
                }
            }
            return null;
        }


        #region Config handling

        public ConfigFile _config;

        public class ConfigFile
        {
            [JsonProperty("Health and metabolism settings (Healing items & food)")]
            public HealingItemSettings healingItemSettings = new HealingItemSettings();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null)
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = new ConfigFile();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
    }

    #region Healing/Metabolism class

    public class HealingItemSettings
    {
        [JsonProperty("Medical")]
        public Dictionary<string, MedicalItem> medicalItems = new Dictionary<string, MedicalItem>
        {
            {"bandage", new MedicalItem(){HealAmount = 5, HealOverTimeAmount = 0, Bleed = -50, Poison = -2, Radiation = 0} },
            {"syringe.medical", new MedicalItem(){HealAmount = 15, HealOverTimeAmount = 20, Bleed = 0, Poison = -5, Radiation = -10}},
            {"largemedkit", new MedicalItem(){HealAmount = 0, HealOverTimeAmount = 100, Bleed = -100, Poison = -10, Radiation = 0}}
        };

        [JsonProperty("Food")]
        public Dictionary<string, FoodItem> foodItems = new Dictionary<string, FoodItem>
        {
            {"chicken.cooked", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 10f, Calories = 40f, Hydration = 3f, Poison = 0f}},
            {"chicken.raw", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 0f, Calories = 20f, Hydration = 0f, Poison = 10f}},
            {"meat.pork.burned", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 1f, Calories = 15f, Hydration = 0f, Poison = 0f}},
            {"wolfmeat.burned", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 1f, Calories = 15f, Hydration = 0f, Poison = 0f}},
            {"humanmeat.burned", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 0f, Calories = 6f, Hydration = -30f, Poison = 0f}},
            {"horsemeat.burned", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 2f, Calories = 10f, Hydration = 0f, Poison = 0f}},
            {"deermeat.burned", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 2f, Calories = 10f, Hydration = 0f, Poison = 0f}},
            {"chicken.burned", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 2f, Calories = 10f, Hydration = 0f, Poison = 0f}},
            {"bearmeat.burned", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 1f, Calories = 25f, Hydration = 0f, Poison = 0f}},
            {"cactusflesh", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 3f, Calories = 5f, Hydration = 20f, Poison = 0f}},
            {"bearmeat.cooked", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 5f, Calories = 100f, Hydration = 1f, Poison = 0f}},
            {"deermeat.cooked", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 10f, Calories = 40f, Hydration = 3f, Poison = 0f}},
            {"fish.cooked", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 5f, Calories = 60f, Hydration = 15f, Poison = 0f}},
            {"horsemeat.cooked", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 8f, Calories = 45f, Hydration = 3f, Poison = 0f}},
            {"blueberries", new FoodItem(){HealAmount = 10f, HealOverTimeAmount = 0f, Calories = 30f, Hydration = 20f, Poison = -5f}},
            {"black.raspberries", new FoodItem(){HealAmount = 10f, HealOverTimeAmount = 0f, Calories = 40f, Hydration = 20f, Poison = -5f}},
            {"apple", new FoodItem(){HealAmount = 2f, HealOverTimeAmount = 0f, Calories = 30f, Hydration = 15f, Poison = 0f}},
            {"corn", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 6f, Calories = 75f, Hydration = 10f, Poison = 0f}},
            {"pumpkin", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 10f, Calories = 100f, Hydration = 30f, Poison = 0f}},
            {"mushroom", new FoodItem(){HealAmount = 3f, HealOverTimeAmount = 0f, Calories = 15f, Hydration = 5f, Poison = 0f}},
            {"can.tuna", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 2f, Calories = 50f, Hydration = 15f, Poison = 0f}},
            {"jar.pickle", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 5f, Calories = 50f, Hydration = 20f, Poison = 0f}},
            {"granolabar", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 5f, Calories = 60f, Hydration = 0f, Poison = 0f}},
            {"can.beans", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 4f, Calories = 100f, Hydration = 25f, Poison = 0f}},
            {"chocholate", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 2f, Calories = 30f, Hydration = 1f, Poison = 0f}},
            {"humanmeat.cooked", new FoodItem(){HealAmount = 1f, HealOverTimeAmount = 1f, Calories = 100f, Hydration = 1f, Poison = 0f}},
            {"meat.pork.cooked", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 5f, Calories = 60f, Hydration = 1f, Poison = 0f}},
            {"wolfmeat.cooked", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 5f, Calories = 60f, Hydration = 1f, Poison = 0f}},
            {"fish.minnows", new FoodItem(){HealAmount = 1f, HealOverTimeAmount = 0f, Calories = 10f, Hydration = 1f, Poison = 0f}},
            {"apple.spoiled", new FoodItem(){HealAmount = 2f, HealOverTimeAmount = 0f, Calories = 15f, Hydration = 2f, Poison = 0f}},
            {"fish.raw", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 0f, Calories = 5f, Hydration = 1f, Poison = 0f}},
            {"bearmeat", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 0f, Calories = 10f, Hydration = 3f, Poison = 5f}},
            {"deermeat.raw", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 0f, Calories = 20f, Hydration = 0f, Poison = 10f}},
            {"horsemeat.raw", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 0f, Calories = 20f, Hydration = 0f, Poison = 10f}},
            {"humanmeat.raw", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 0f, Calories = 5f, Hydration = -3f, Poison = 10f}},
            {"wolfmeat.raw", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 0f, Calories = 20f, Hydration = 0f, Poison = 10f}},
            {"meat.boar", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 0f, Calories = 60f, Hydration = 0f, Poison = 5f}},
            {"grub", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 0f, Calories = 3f, Hydration = -15f, Poison = 0f}},
            {"worm", new FoodItem(){HealAmount = 0f, HealOverTimeAmount = 0f, Calories = 1f, Hydration = -10f, Poison = 0f}}
        };

        [JsonProperty("Pickle chance min")]
        public int pickleChanceMin = 1;

        [JsonProperty("Pickle chance max")]
        public int pickleChanceMax = 3;

        [JsonProperty("Pickle effect (Must be inbetween min and max")]
        public int pickleEffect = 2;

        public class FoodItem
        {
            [JsonProperty("Instant Heal")]
            public float HealAmount;
            [JsonProperty("Heal Over Time")]
            public float HealOverTimeAmount;
            [JsonProperty("Food")]
            public float Calories;
            [JsonProperty("Water")]
            public float Hydration;
            [JsonProperty("Poison")]
            public float Poison;
        }

        public class MedicalItem
        {
            [JsonProperty("Instant Heal")]
            public float HealAmount;
            [JsonProperty("Heal Over Time")]
            public float HealOverTimeAmount;
            [JsonProperty("Poison")]
            public float Poison;
            [JsonProperty("Bleed")]
            public float Bleed;
            [JsonProperty("Radiation")]
            public float Radiation;
        }
    }
    #endregion
}