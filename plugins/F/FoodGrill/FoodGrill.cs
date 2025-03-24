using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Configuration;
using UnityEngine;
namespace Oxide.Plugins
{
    [Info("Food Grill", "Dana", "1.0.5")]
    [Description("Displays meat models on the barbeque.")]
    class FoodGrill : RustPlugin
    {
        List<FOG> FOGLIST = new List<FOG>();
        private PluginConfig _pluginConfig;
        void OnFindBurnable(BaseOven oven)
        {
            if (!_pluginConfig.Config.IsEnabled)
                return;
            if (oven.GetComponent<BaseEntity>() == null)
                return;
            if (oven.GetComponent<BaseEntity>().GetComponent<FOG>() == null)
                return;
            oven.GetComponent<BaseEntity>().GetComponent<FOG>().AddFood(_pluginConfig.Config);
        }
        void OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (!_pluginConfig.Config.IsEnabled)
                return;

            if (oven.GetComponent<BaseEntity>().ShortPrefabName != "bbq.deployed")
                return;
            if (oven.GetComponent<FOG>() != null)
                return;
            oven.GetComponent<BaseEntity>().gameObject.AddComponent<FOG>();
            FOGLIST.Add(oven.GetComponent<BaseEntity>().GetComponent<FOG>());
        }
        void init()
        {
            LoadDefaultConfig();
        }
        void Unload()
        {
            if (FOGLIST == null)
                return;

            for (var i = 0; i < FOGLIST.Count; i++)
            {
                FOGLIST[i]?.Delete();
            }
        }
        protected override void LoadConfig()
        {
            var configPath = $"{Manager.ConfigPath}/{Name}.json";
            var newConfig = new DynamicConfigFile(configPath);
            if (!newConfig.Exists())
            {
                LoadDefaultConfig();
                newConfig.Save();
            }
            try
            {
                newConfig.Load();
            }
            catch (Exception ex)
            {
                RaiseError("Failed to load config file (is the config file corrupt?) (" + ex.Message + ")");
                return;
            }

            newConfig.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = newConfig.ReadObject<PluginConfig>();
            if (_pluginConfig.Config == null)
            {
                _pluginConfig.Config = new GrillConfig
                {
                    IsEnabled = true,
                    ShowDefaultFood = true,
                    DefaultRawFood = "chicken.raw",
                    DefaultCookedFood = "chicken.cooked",
                    DefaultBurntFood = "chicken.burned",
                    DefaultSpoiledFood = "chicken.spoiled",
                    RawFoods = new List<string>
                    {
                        "bearmeat",
                        "chicken.raw",
                        "deermeat.raw",
                        "fish.raw",
                        "fish.minnows",
                        "fish.troutsmall",
                        "horsemeat.raw",
                        "humanmeat.raw",
                        "meat.boar",
                        "wolfmeat.raw"
                    },
                    CookedFoods = new List<string>
                    {
                        "bearmeat.cooked",
                        "chicken.cooked",
                        "deermeat.cooked",
                        "fish.cooked",
                        "horsemeat.cooked",
                        "humanmeat.cooked",
                        "meat.pork.cooked",
                        "wolfmeat.cooked"
                    },
                    BurntFoods = new List<string>
                    {
                        "bearmeat.burned",
                        "chicken.burned",
                        "deermeat.burned",
                        "horsemeat.burned",
                        "humanmeat.burned",
                        "meat.pork.burned",
                        "wolfmeat.burned"
                    },
                    SpoiledFoods = new List<string>
                    {
                        "chicken.spoiled",
                        "humanmeat.spoiled",
                        "wolfmeat.spoiled"
                    }
                };
            }

            newConfig.WriteObject(_pluginConfig);
            PrintWarning("Config Loaded");
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }
        class FOG : MonoBehaviour
        {
            BaseEntity entity;
            BaseOven oven;
            public List<Item> ItemList = new List<Item>();
            private IEnumerator coroutine;
            bool wait;

            private IEnumerator Wait(float waitTime)
            {
                while (true)
                {
                    yield return new WaitForSeconds(waitTime);
                    wait = false;
                }
            }

            void Awake()
            {
                entity = GetComponent<BaseEntity>();
                oven = GetComponent<BaseOven>();
            }
            public void AddFood(GrillConfig grillConfig)
            {
                if (wait)
                    return;
                wait = true;
                for (int i = 0; i < ItemList.Count; i++)
                {
                    if (ItemList[i] != null)
                    {
                        var removeitem = ItemList[i];
                        ItemList.Remove(removeitem);
                        removeitem.DoRemove();
                    }
                }

                for (int u = 0; u < oven.inventory.itemList.Count; u++)
                {
                    if (oven.inventory.itemList[u] == null)
                        continue;
                    if (oven.inventory.itemList[u].info == null)
                        continue;

                    var itemId = oven.inventory.itemList[u].info.itemid;
                    var shortName = oven.inventory.itemList[u].info.shortname;

                    if (grillConfig.RawFoods.Contains(shortName))
                    {
                        if (grillConfig.ShowDefaultFood && !string.IsNullOrWhiteSpace(grillConfig.DefaultRawFood))
                        {
                            var itemDefinition = ItemManager.FindItemDefinition(grillConfig.DefaultRawFood);
                            if (itemDefinition != null)
                                itemId = itemDefinition.itemid;
                        }
                    }
                    else if (grillConfig.CookedFoods.Contains(shortName))
                    {
                        if (grillConfig.ShowDefaultFood && !string.IsNullOrWhiteSpace(grillConfig.DefaultCookedFood))
                        {
                            var itemDefinition = ItemManager.FindItemDefinition(grillConfig.DefaultCookedFood);
                            if (itemDefinition != null)
                                itemId = itemDefinition.itemid;
                        }
                    }
                    else if (grillConfig.BurntFoods.Contains(shortName))
                    {
                        if (grillConfig.ShowDefaultFood && !string.IsNullOrWhiteSpace(grillConfig.DefaultBurntFood))
                        {
                            var itemDefinition = ItemManager.FindItemDefinition(grillConfig.DefaultBurntFood);
                            if (itemDefinition != null)
                                itemId = itemDefinition.itemid;
                        }
                    }
                    else if (grillConfig.SpoiledFoods.Contains(shortName))
                    {
                        if (grillConfig.ShowDefaultFood && !string.IsNullOrWhiteSpace(grillConfig.DefaultSpoiledFood))
                        {
                            var itemDefinition = ItemManager.FindItemDefinition(grillConfig.DefaultSpoiledFood);
                            if (itemDefinition != null)
                                itemId = itemDefinition.itemid;
                        }
                    }
                    else
                    {
                        continue;
                    }

                    var pos = entity.transform.position + new Vector3(0, 0.8f, 0);
                    var ang = entity.transform.eulerAngles;

                    var item = ItemManager.CreateByItemID(itemId, 1, (ulong)0);
                    ItemList.Add(item);
                    DroppedItem food = item.Drop(pos, Vector3.zero).GetComponent<DroppedItem>();
                    food.SetParent(entity);
                    var offset = 0f;
                    var offset2 = 0f;
                    if (u >= 6) { offset = 0.2f; offset2 = 6; }
                    food.transform.localPosition = new Vector3(-0.1f + offset, 0.8f, -0.38f + (u - offset2) * 0.15f);
                    food.transform.eulerAngles = ang;
                    food.transform.hasChanged = true;
                    food.SendNetworkUpdateImmediate();
                    food.GetComponent<Rigidbody>().isKinematic = true;
                    food.GetComponent<Rigidbody>().useGravity = false;
                    food.allowPickup = false;
                    food.GetComponent<Rigidbody>().detectCollisions = true;
                    food.CancelInvoke((Action)Delegate.CreateDelegate(typeof(Action), food, "IdleDestroy"));

                }

                coroutine = Wait(5);
                StartCoroutine(coroutine);
            }
            public void Delete()
            {
                Destroy();
            }
            void Destroy()
            {
                try
                {
                    for (int i = 0; i < ItemList.Count; i++)
                    {
                        if (ItemList[i] != null)
                        {
                            ItemList[i].DoRemove();
                        }
                    }
                    enabled = false;
                    CancelInvoke();
                    Destroy(this);
                }
                catch (Exception)
                {
                    // ignored
                }
            }
        }

        private class PluginConfig
        {
            public GrillConfig Config { get; set; }
        }

        private class GrillConfig
        {
            [JsonProperty(PropertyName = "Plugin - Enabled")]
            public bool IsEnabled { get; set; }

            [JsonProperty(PropertyName = "Show Default Food")]
            public bool ShowDefaultFood { get; set; }

            [JsonProperty(PropertyName = "Default Raw Food")]
            public string DefaultRawFood { get; set; }

            [JsonProperty(PropertyName = "Default Cooked Food")]
            public string DefaultCookedFood { get; set; }

            [JsonProperty(PropertyName = "Default Burnt Food")]
            public string DefaultBurntFood { get; set; }

            [JsonProperty(PropertyName = "Default Spoiled Food")]
            public string DefaultSpoiledFood { get; set; }

            [JsonProperty(PropertyName = "Raw Food List")]
            public List<string> RawFoods { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Cooked Food List")]
            public List<string> CookedFoods { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Burnt Food List")]
            public List<string> BurntFoods { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Spoiled Food List")]
            public List<string> SpoiledFoods { get; set; } = new List<string>();
        }
    }
}