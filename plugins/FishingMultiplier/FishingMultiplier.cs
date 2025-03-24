using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Fishing Multiplier", "Malmo", "1.0.3")]
    [Description("Multiplies the amount of fish caught with rod or traps")]
    class FishingMultiplier : RustPlugin
    {

        #region Fields

        private readonly Hash<ulong, int> _trapMultipliers = new Hash<ulong, int>();

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            _config = Config.ReadObject<ConfigData>();
        }

        private void OnFishCatch(Item item, BaseFishingRod rod, BasePlayer player)
        {
            var multiplier = GetMultiplier(item.info.shortname, CatchTypes.Rod);
            item.amount *= multiplier;
        }

        // private void OnWildlifeTrap(WildlifeTrap trap, TrappableWildlife trapped)
        // {
        //     var multiplier = GetMultiplier(trapped.inventoryObject.shortname, CatchTypes.FishTrap);

        //     if (multiplier > 1)
        //         _trapMultipliers.Add(trap.net.ID, multiplier);
        // }

        // void OnItemAddedToContainer(ItemContainer container, Item item)
        // {
        //     if (!(container.entityOwner is WildlifeTrap)) return;
        //     if (!_trapMultipliers.ContainsKey(container.entityOwner.net.ID)) return;

        //     item.amount *= _trapMultipliers[container.entityOwner.net.ID];
        //     _trapMultipliers.Remove(container.entityOwner.net.ID);
        // }

        #endregion

        #region Helper Methods

        private int GetMultiplier(Dictionary<string, int> dict, string shortname)
        {
            if (dict == null) return 1;

            if (dict.ContainsKey(shortname))
                return dict[shortname];

            if (dict.ContainsKey("*"))
                return dict["*"];

            return 1;
        }

        private int GetMultiplier(string shortname, CatchTypes type)
        {
            if (_config != null && _config.Multipliers != null)
            {
                switch (type)
                {
                    case CatchTypes.FishTrap:
                        return GetMultiplier(_config.Multipliers.FishTrapsMultipler, shortname);
                    case CatchTypes.Rod:
                        return GetMultiplier(_config.Multipliers.RodMultipler, shortname);
                }
            }

            return 1;
        }

        #endregion

        #region Config

        private ConfigData _config;

        class ConfigData
        {
            [JsonProperty("Multipliers")]
            public MultiplierConfig Multipliers { get; set; }
        }

        class MultiplierConfig
        {
            [JsonProperty("Fishing Rod Multiplier")]
            public Dictionary<string, int> RodMultipler { get; set; }

            [JsonProperty("Fish Traps Multiplier")]
            public Dictionary<string, int> FishTrapsMultipler { get; set; }
        }

        private enum CatchTypes
        {
            Rod,
            FishTrap
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData()
            {
                Multipliers = new MultiplierConfig
                {
                    RodMultipler = new Dictionary<string, int>()
                    {
                        ["*"] = 1,
                        ["shortname"] = 5
                    },
                    FishTrapsMultipler = new Dictionary<string, int>()
                    {
                        ["*"] = 1,
                        ["shortname"] = 5
                    }
                }
            };
        }

        #endregion

    }
}