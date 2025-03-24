using Oxide.Core;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("NoFuelRequirements", "k1lly0u", "1.8.0")]
    class NoFuelRequirements : RustPlugin
    {
        #region Fields 
        private readonly string[] ValidFuelTypes = new string[] { "wood", "lowgradefuel" };

        private readonly ConsumeType[] ObsoleteConsumeTypes = new ConsumeType[] 
        { ConsumeType.None, ConsumeType.CeilingLight, ConsumeType.Searchlight, ConsumeType.SmallCandle, ConsumeType.LargeCandle };
        #endregion

        #region Oxide Hooks        
        private void Loaded()
        {
            if (configData.UsePermissions)
            {
                foreach (ConsumeType consumeType in Enum.GetValues(typeof(ConsumeType)))
                {
                    if (!ObsoleteConsumeTypes.Contains(consumeType))
                        permission.RegisterPermission($"nofuelrequirements.{consumeType}", this);
                }
            }
        }

        private void OnFuelConsume(BaseOven baseOven, Item fuelItem, ItemModBurnable itemModBurnable)
        {
            if (!baseOven || fuelItem == null) 
                return;

            ConsumeType consumeType = ShortNameToConsumeType(baseOven.ShortPrefabName);
            if (consumeType == ConsumeType.None) 
                return;

            if (IsConsumeTypeEnabled(consumeType))
            {
                if (configData.UsePermissions && baseOven.OwnerID != 0U && !HasPermission(baseOven.OwnerID.ToString(), consumeType))                
                    return;
                
                fuelItem.amount += 1;

                baseOven.allowByproductCreation = false;
                
                NextTick(() =>
                {
                    if (baseOven)
                        baseOven.allowByproductCreation = true;
                });
            }
        }

        private void OnItemUse(Item item, int amount)
        {
            if (item == null || amount == 0 || !ValidFuelTypes.Contains(item.info.shortname))
                return;

            string shortPrefabName = string.Empty;
            ulong ownerId = 0UL;

            ItemContainer rootContainer = item.GetRootContainer();
            if (rootContainer != null && rootContainer.entityOwner)
            {
                if (rootContainer.entityOwner is BaseOven)
                    return;

                shortPrefabName = rootContainer.entityOwner.ShortPrefabName;
                ownerId = rootContainer.entityOwner.OwnerID;
            }
            else if (item.parent != null && item.parent.parent != null)
            {
                shortPrefabName = item.parent.parent.info.shortname;

                BasePlayer ownerPlayer = item.parent.GetOwnerPlayer();
                ownerId = ownerPlayer != null ? ownerPlayer.userID : 0UL;
            }
            else if (item.parent != null && item.parent.playerOwner != null)
            {                
                Item activeItem = item.parent.playerOwner.GetActiveItem();
                if (activeItem != null)
                {
                    HeldEntity heldEntity = activeItem.GetHeldEntity() as HeldEntity;
                    if (heldEntity)
                    {
                        if (heldEntity is FlameThrower || heldEntity is Chainsaw)
                            shortPrefabName = heldEntity.ShortPrefabName;
                    }
                }

                ownerId = item.parent.playerOwner.userID;
            }

            if (string.IsNullOrEmpty(shortPrefabName))
                return;

            ConsumeType consumeType = ShortNameToConsumeType(shortPrefabName);
            if (consumeType == ConsumeType.None)
                return;

            if (IsConsumeTypeEnabled(consumeType))
            {
                if (configData.UsePermissions && ownerId != 0UL && !HasPermission(ownerId, consumeType))
                    return;

                item.amount += amount;
            }
        }
        #endregion

        #region Functions        
        private object IgnoreFuelConsumption(string consumeTypeStr, ulong ownerId)
        {
            ConsumeType consumeType = ParseType(consumeTypeStr);
            if (consumeType != ConsumeType.None && configData.AffectedTypes[consumeType])
            {
                if (configData.UsePermissions && !HasPermission(ownerId.ToString(), consumeType))
                    return null;
                return true;
            }
            return null;
        }

        private bool HasPermission(string userId, ConsumeType consumeType) => permission.UserHasPermission(userId, $"nofuelrequirements.{consumeType}");

        private bool HasPermission(ulong userId, ConsumeType consumeType) => permission.UserHasPermission(userId.ToString(), $"nofuelrequirements.{consumeType}");

        private bool IsConsumeTypeEnabled(ConsumeType consumeType)
        {
            bool result = false;
            configData.AffectedTypes.TryGetValue(consumeType, out result);
            return result;
        }

        private ConsumeType ShortNameToConsumeType(string shortname)
        {
            switch (shortname)
            {
                case "campfire":
                    return ConsumeType.Campfire;
                case "skull_fire_pit":
                    return ConsumeType.Firepit;
                case "fireplace.deployed":
                    return ConsumeType.Fireplace;
                case "furnace":
                    return ConsumeType.Furnace;
                case "furnace.large":
                    return ConsumeType.LargeFurnace;
                case "refinery_small_deployed":
                    return ConsumeType.OilRefinery;                
                case "chainsaw.entity":
                    return ConsumeType.Chainsaw;
                case "flamethrower.entity":
                    return ConsumeType.FlameThrower;
                case "lantern.deployed":
                    return ConsumeType.Lanterns;
                case "hat.miner":
                    return ConsumeType.MinersHat;
                case "hat.candle":
                    return ConsumeType.CandleHat;
                case "fuelstorage":
                    return ConsumeType.Quarry;
                case "tunalight.deployed":
                    return ConsumeType.TunaLight;                
                case "fogmachine":
                    return ConsumeType.FogMachine;
                case "snowmachine":
                    return ConsumeType.SnowMachine;
                case "cursedcauldron.deployed":
                    return ConsumeType.CursedCauldren;
                case "chineselantern.deployed":
                    return ConsumeType.ChineseLantern;
                case "bbq.deployed":
                    return ConsumeType.Barbeque;
                case "hobobarrel.deployed":
                    return ConsumeType.HoboBarrel;
                case "small_fuel_generator.deployed":
                    return ConsumeType.SmallGenerator;
                default:
                    return ConsumeType.None;
            }
        }

        private ConsumeType ParseType(string type)
        {
            try
            {
                return (ConsumeType)Enum.Parse(typeof(ConsumeType), type, true);
            }
            catch
            {
                return ConsumeType.None;
            }
        }
        #endregion

        #region Config  
        private enum ConsumeType 
        {
            Barbeque,
            Campfire,
            CandleHat,
            CeilingLight, 
            Chainsaw,
            ChineseLantern, 
            CursedCauldren,
            Firepit, 
            Fireplace,
            FlameThrower,
            FogMachine,
            HoboBarrel,
            Furnace,
            Lanterns,
            LargeFurnace,
            MinersHat,
            OilRefinery,
            Quarry,            
            Searchlight,            
            SnowMachine,
            TunaLight,
            SmallCandle,
            LargeCandle,
            SmallGenerator,
            None 
        }
       
        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Entities that ignore fuel consumption")]
            public Dictionary<ConsumeType, bool> AffectedTypes { get; set; }

            [JsonProperty(PropertyName = "Require permission to ignore fuel consumption")]
            public bool UsePermissions { get; set; }

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
                AffectedTypes = new Dictionary<ConsumeType, bool>
                {
                    [ConsumeType.Barbeque] = true,
                    [ConsumeType.Campfire] = true,
                    [ConsumeType.CandleHat] = true,
                    [ConsumeType.Firepit] = true,
                    [ConsumeType.Fireplace] = true,
                    [ConsumeType.Furnace] = true,
                    [ConsumeType.Lanterns] = true,
                    [ConsumeType.LargeFurnace] = true,
                    [ConsumeType.MinersHat] = true,
                    [ConsumeType.OilRefinery] = true,
                    [ConsumeType.Quarry] = true,
                    [ConsumeType.TunaLight] = true,
                    [ConsumeType.FogMachine] = true,
                    [ConsumeType.SnowMachine] = true,
                    [ConsumeType.CursedCauldren] = true,
                    [ConsumeType.ChineseLantern] = true,
                    [ConsumeType.Chainsaw] = true,
                    [ConsumeType.FlameThrower] = true,
                    [ConsumeType.HoboBarrel] = true,
                    [ConsumeType.SmallGenerator] = true,
                },
                UsePermissions = false,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();
            if (configData.Version < new VersionNumber(1, 3, 6))
                configData = baseConfig;

            foreach (ConsumeType consumeType in Enum.GetValues(typeof(ConsumeType)))
            {
                if (!ObsoleteConsumeTypes.Contains(consumeType) && !configData.AffectedTypes.ContainsKey(consumeType))
                    configData.AffectedTypes.Add(consumeType, true);
            }

            for (int i = 0; i < ObsoleteConsumeTypes.Length; i++)
                configData.AffectedTypes.Remove(ObsoleteConsumeTypes[i]);

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion
    }
}
