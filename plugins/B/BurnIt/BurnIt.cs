using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("Burn It", "marcuzz", "1.1.2")]
    [Description("Makes all wooden items burnable.")]
    public class BurnIt : RustPlugin
    {
        private static PluginConfig _config;
        private static readonly ItemDefinition _wood = ItemManager.FindItemDefinition(-151838493);

        private void OnServerInitialized()
        {
            if (!_config.ConditionLoss)
            {
                Unsubscribe(nameof(OnOvenCooked));
                Unsubscribe(nameof(OnEntityBuilt));
            }

            foreach (CustomBurnable customBurnable in _config.SpecificBurnableItems)
                AddCustomBurnable(customBurnable);

            if (_config.WoodenItems.AllowWoodenItems)
                ModifyWoodenItems();

            foreach (var oven in UnityEngine.Object.FindObjectsOfType<BaseOven>()) 
            {
                if (oven is BaseFuelLightSource)
                    continue;

                oven.fuelType = null;
            }
        }

        private void OnEntitySpawned(BaseOven oven)
        {
            if (oven is BaseFuelLightSource)
                return;

            oven.fuelType = null;
        }

        private void Unload()
        {
            foreach (var definition in ItemManager.itemList)
                ClearItemModBurnable(definition);

            foreach (var oven in UnityEngine.Object.FindObjectsOfType<BaseOven>())
            {
                if (oven is BaseFuelLightSource)
                    continue;

                oven.fuelType = _wood;
            }

            _config = null;
            Config.Clear();
        }

        private Item OnFindBurnable(BaseOven oven)
        {
            if (oven.inventory == null)
                return null;

            foreach (Item item in oven.inventory.itemList)
            {
                var burnable = item.info.GetComponent<ItemModBurnable>();
                if (burnable == null)
                    continue;

                if (burnable is ItemModBurnIt)
                {
                    if (item.IsOnFire())
                        return item;

                    return SetItemFuel(item, burnable.fuelAmount);
                }

                if (!_config.DisableOvenFuelTypeCheck) 
                {
                    if (item.info.itemid == -946369541 && oven.temperature == BaseOven.TemperatureType.Fractioning)
                        continue;
                }

                return item;
            }

            return null;
        }

        private object OnFuelConsume(BaseOven oven, Item fuel, ItemModBurnable burnable)
        {
            if (oven.allowByproductCreation && burnable.byproductItem != null && UnityEngine.Random.Range(0f, 1f) > burnable.byproductChance)
            {
                Item item = ItemManager.Create(burnable.byproductItem, burnable.byproductAmount, 0uL);
                
                oven.fuelType = fuel.info;
                var moved = item.MoveToContainer(oven.inventory);
                oven.fuelType = null;

                if (!moved)
                {
                    oven.OvenFull();
                    item.Drop(oven.inventory.dropPosition, oven.inventory.dropVelocity);
                }
            }

            if (fuel.amount <= 1)
            {
                fuel.Remove();
                return true;
            }

            fuel.UseItem(1);
            fuel.fuel = burnable.fuelAmount;
            fuel.MarkDirty();
            Interface.CallHook("OnFuelConsumed", this, fuel, burnable);

            return true;
        }

        private object OnOvenCook(BaseOven oven, Item item)
        {
            if (oven.temperature == BaseOven.TemperatureType.Fractioning)
            {
                oven.fuelType = item.info;
                NextTick(() => { oven.fuelType = null; });
            }

            return null;
        }

        private void OnOvenCooked(BaseOven oven, Item fuel, BaseEntity slot)
        {
            if (fuel == null)
                return;

            var burnable = fuel.info.GetComponent<ItemModBurnIt>();
            if (burnable == null)
                return;

            if (!fuel.hasCondition)
                return;

            if (fuel.isBroken)
            {
                fuel.fuel = -1f;
                return;
            }

            if (fuel.condition < 1)
            {
                fuel.LoseCondition(1f);
                return;
            }

            var ratio = fuel.fuel / burnable.fuelAmount;
            var damage = fuel.condition - (fuel.maxCondition * ratio);

            if (damage > 0)
                fuel.LoseCondition(damage);
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            var item = plan.GetItem();
            if (item.hasCondition)
                return;

            if (item.fuel <= 0)
                return;

            var burnable = item.info.GetComponent<ItemModBurnIt>();
            if (burnable == null || burnable.fuelAmount == item.fuel)
                return;

            var combatEntity = (BaseCombatEntity)go.ToBaseEntity();
            if (combatEntity == null)
                return;

            if (combatEntity._maxHealth > 0)
            {
                var ratio = item.fuel / burnable.fuelAmount;
                var damage = combatEntity._maxHealth - (combatEntity._maxHealth * ratio);
                combatEntity.Hurt(damage * 10);
            }
        }

        private static void ModifyWoodenItems()
        {
            foreach (ItemDefinition definition in GenerateBurnableItemList().Values)
            {
                if (_config.WoodenItems.BlacklistedShortnames.Contains(definition.shortname))
                    continue;

                AddBurnableWoodenItem(definition);
            }
        }

        private static void ClearItemModBurnable(ItemDefinition definition)
        {
            var burnable = definition.GetComponent<ItemModBurnIt>();
            if (burnable != null)
                GameManager.DestroyImmediate(burnable);

            var mods = new List<ItemMod>();
            foreach (var mod in definition.itemMods) 
                if (!(mod is ItemModBurnIt))
                    mods.Add(mod);
            
            definition.itemMods = mods.ToArray();
        }

        private static Item SetItemFuel(Item item, float maxFuel)
        {
            if (item.hasCondition)
            {
                if (item.isBroken)
                {
                    item.fuel = 0;
                    return item;
                }

                if (item.condition < item.maxCondition)
                {
                    var ratio = item.condition / item.maxCondition;
                    item.fuel = maxFuel * ratio;
                }
            }

            if (item.fuel == 0)
                item.fuel = maxFuel;

            return item;
        }

        private static Dictionary<int, ItemDefinition> GenerateBurnableItemList()
        {
            var list = new Dictionary<int, ItemDefinition> { };

            foreach (var itemBlueprint in ItemManager.GetBlueprints())
            {
                if (ContainsModBurnable(itemBlueprint.targetItem))
                    continue;
                if (itemBlueprint.targetItem.GetComponent<ItemModBurnable>() != null)
                    continue;
                if (GetWoodAmount(itemBlueprint) == 0)
                    continue;
                if (itemBlueprint.targetItem.condition.enabled && !_config.ConditionLoss)
                    continue;

                list.Add(itemBlueprint.targetItem.itemid, itemBlueprint.targetItem);
            }

            return list;
        }

        private static int GetWoodAmount(ItemBlueprint blueprint)
        {
            foreach (var ingredient in blueprint.ingredients)
            {
                if (ingredient.itemDef == null)
                    continue;

                if (ingredient.itemDef.itemid == _wood.itemid)
                    return (int)ingredient.amount;
            }

            return 0;
        }

        private static void AddBurnableWoodenItem(ItemDefinition definition)
        {
            var woodBurnable = _wood.GetComponent<ItemModBurnable>();
            var woodAmount = (int)(GetWoodAmount(definition.Blueprint) * _config.WoodenItems.FuelAmountRatio);

            AddItemModBurnable(
                definition,
                woodBurnable.byproductItem,
                (int)(woodAmount * _config.WoodenItems.CharcoalRatio),
                -1f,
                woodAmount * woodBurnable.fuelAmount
                );
        }

        private static void AddCustomBurnable(CustomBurnable customBurnable)
        {
            var definition = ItemManager.FindItemDefinition(customBurnable.Shortname);
            if (definition == null)
                return;

            if (definition.condition.enabled && !_config.ConditionLoss)
                return;

            var byproduct = ItemManager.FindItemDefinition(customBurnable.ByproductShortname);
            if (byproduct == null)
                return;

            AddItemModBurnable(
                definition,
                byproduct,
                customBurnable.ByproductAmount,
                customBurnable.ByproductChance,
                customBurnable.FuelAmount
                );
        }

        private static bool ContainsModBurnable(ItemDefinition definition)
        {
            foreach (var mod in definition.itemMods)
                if (mod is ItemModBurnable)
                    return true;

            return false;
        }

        private static void AddItemModBurnable(
            ItemDefinition definition,
            ItemDefinition byproduct,
            int byproductAmount,
            float byproductChance,
            float fuelAmount
            )
        {
            if (definition.GetComponent<ItemModBurnable>() != null || ContainsModBurnable(definition))
                return;

            var burnable = definition.GetOrAddComponent<ItemModBurnIt>();
            burnable.byproductItem = byproduct;
            burnable.byproductAmount = byproductAmount;
            burnable.byproductChance = byproductChance;
            burnable.fuelAmount = fuelAmount;

            UpdateItemMods(definition, burnable);
        }

        private static void UpdateItemMods(ItemDefinition definition, ItemModBurnIt burnable)
        {
            var mods = new List<ItemMod>
            {
                burnable
            };

            foreach (var mod in definition.itemMods) 
            {
                if (mod is ItemModBurnIt)
                    return;

                mods.Add(mod);
            }
                
            definition.itemMods = mods.ToArray();
        }

        private class ItemModBurnIt : ItemModBurnable
        {
        }

        private static PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                ConditionLoss = true,
                WoodenItems = new WoodenItems
                {
                    AllowWoodenItems = true,
                    FuelAmountRatio = 0.2f,
                    CharcoalRatio = 0.5f,
                    BlacklistedShortnames = new List<string>
                    {
                        "small.oil.refinery",
                        "furnace.large",
                        "furnace",
                        "bbq",
                        "fireplace.stone",
                        "campfire",
                        "waterpump"
                    }
                },
                SpecificBurnableItems = new List<CustomBurnable>
                {
                    new CustomBurnable()
                    {
                        Shortname = "plantfiber",
                        ByproductShortname = "charcoal",
                        ByproductAmount = 1,
                        ByproductChance = 0.5f,
                        FuelAmount = 5f
                    },
                    new CustomBurnable()
                    {
                        Shortname = "horsedung",
                        ByproductShortname = "charcoal",
                        ByproductAmount = 1,
                        ByproductChance = 1f,
                        FuelAmount = 100f
                    }
                }
            };
        }

        private class WoodenItems
        {
            [JsonProperty("[a] Make wooden items burnable:")]
            public bool AllowWoodenItems;

            [JsonProperty("[b] Burnable wood ratio")]
            public float FuelAmountRatio;

            [JsonProperty("[c] Charcoal per burnable wood ratio")]
            public float CharcoalRatio;

            [JsonProperty("[d] Blacklisted shortnames")]
            public List<string> BlacklistedShortnames { get; set; }
        }

        private class CustomBurnable
        {
            [JsonProperty("[a] Item shortname")]
            public string Shortname;

            [JsonProperty("[b] Byproduct shortname")]
            public string ByproductShortname;

            [JsonProperty("[c] Byproduct amount")]
            public int ByproductAmount;

            [JsonProperty("[d] Byproduct loss chance")]
            public float ByproductChance;

            [JsonProperty("[e] Fuel amount (burn time)")]
            public float FuelAmount;
        }

        private class PluginConfig
        {
            [JsonProperty("[1] Condition loss feature (allow items with durability)")]
            public bool ConditionLoss { get; set; }

            [JsonProperty("[2] Allow lowgrade burning (disable oven fuel type check)")]
            public bool DisableOvenFuelTypeCheck { get; set; }

            [JsonProperty("[3] Wooden items")]
            public WoodenItems WoodenItems { get; set; }

            [JsonProperty("[4] Specified burnable items")]
            public List<CustomBurnable> SpecificBurnableItems { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config.WriteObject(GetDefaultConfig(), true);

            _config = Config.ReadObject<PluginConfig>();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            _config = Config.ReadObject<PluginConfig>();
        }
    }
}