using System;
using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Legacy Nodes", "xdEggie", "1.0.2")]
    [Description("Restore legacy nodes to current rust")]
    class LegacyNodes : CovalencePlugin
    {

        #region References

        [PluginReference]
        private Plugin GatherRates;

        #endregion

        #region initialize

        private string permPrefix = "LegacyNodes.enabled";
        private List<BasePlayer> quedLastHit = new List<BasePlayer>();
        private List<ResourceDispenser> blacklisted = new List<ResourceDispenser>();
        private List<ResourceDispenser> nonbonusmetal = new List<ResourceDispenser>();
        private Dictionary<ResourceDispenser, NodeData> nodes = new Dictionary<ResourceDispenser, NodeData>();

        private int stone = -2099697608;
        private int sulfur = -1157596551;
        private int metal = -4031221;
        private int highqual = -1982036270;

        public class NodeData
        {
            public int rewardedstone { get; set; } = 750;
            public int rewardedmetal { get; set; } = 500;
            public int rewardedsulfur { get; set; } = 200;

            public bool sulfurComplete { get; set; } = false;
            public bool stoneComplete { get; set; } = false;
            public bool metalComplete { get; set; } = false;
        }

        private void Init() { permission.RegisterPermission(permPrefix, this); }

        private void OnServerInitialized()
        {
            if (GatherRates != null) LoadGatherRatesConfig();
            if (!legacyConfig.General.bonusEnabled) { Unsubscribe("OnDispenserBonus"); Unsubscribe("OnEntityDeath"); }

            if (legacyConfig.General.bonusEnabled) timer.Every(1f, () =>
            {
                List<BasePlayer> qued = new List<BasePlayer>();
                foreach (BasePlayer player in quedLastHit)
                {
                    if (player.IsConnected && player.IsAlive())
                    {
                        qued.Add(player);
                        int highQualAmount = 2; if (GatherRates != null) highQualAmount = highQualAmount * (int)ItemModifier(player.UserIDString, highqual.ToString());
                        DoItemAdd(player, highQualAmount, highqual.ToString());
                    }
                }

                foreach (BasePlayer player in qued) quedLastHit.Remove(player);
                qued.Clear();
            });
        }

        #endregion

        #region Config

        private LegacyNodeConfig legacyConfig;

        public class LegacyNodeConfig
        {
            [JsonProperty(PropertyName = "General Settings")]
            public GeneralSettings General { get; set; }

            [JsonProperty(PropertyName = "Chat Settings")]
            public ChatSettings Chat { get; set; }

            public class GeneralSettings
            {
                [JsonProperty("Bonus Enabled")]
                public bool bonusEnabled { get; set; }

                [JsonProperty("Permission Only Mode")]
                public bool permissionMode { get; set; }
            }

            public class ChatSettings
            {
                [JsonProperty("Announce Plugin Loaded In Game")]
                public bool AnnouncePluginLoaded { get; set; }

                [JsonProperty("Chat Prefix Enabled")]
                public bool PrefixEnabled { get; set; }

                [JsonProperty(PropertyName = "Chat Prefix Color")]
                public string PrefixColor { get; set; }

                [JsonProperty(PropertyName = "Chat Message Color")]
                public string MessageColor { get; set; }
            }

            [JsonProperty(PropertyName = "Version: ")]
            public Oxide.Core.VersionNumber Version { get; set; }
        }

        private void LoadDefaultConfig() => legacyConfig = LoadBaseConfig();

        private void SaveConfig() => Config.WriteObject(legacyConfig, true);

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                legacyConfig = Config.ReadObject<LegacyNodeConfig>();

                if (legacyConfig == null)
                    throw new JsonException();

                if (legacyConfig.Version < Version || legacyConfig.Version > Version)
                {
                    LogWarning(GetLang("configUpdated"));
                    LoadDefaultConfig();
                }

                SaveConfig();
            }
            catch
            {
                LoadDefaultConfig();
                LogWarning(GetLang("confCorrupt"));
                SaveConfig();
            }
        }

        private LegacyNodeConfig LoadBaseConfig()
        {
            return new LegacyNodeConfig
            {
                General = new LegacyNodeConfig.GeneralSettings
                {
                    bonusEnabled = true,
                    permissionMode = false
                },

                Chat = new LegacyNodeConfig.ChatSettings
                {
                    AnnouncePluginLoaded = true,
                    PrefixEnabled = true,
                    PrefixColor = "4A95CC",
                    MessageColor = "C57039"
                },

                Version = Version
            };
        }

        #endregion

        #region Hooks

        private void Unload()
        {
            quedLastHit.Clear();
            blacklisted.Clear();
            nonbonusmetal.Clear();
            nodes.Clear();
            legacyConfig = null;
        }

        private void OnEntityDeath(ResourceEntity entity, HitInfo info)
        {
            if (entity == null || info == null || info.Initiator == null) return;

            ResourceDispenser resourceEntity = entity.resourceDispenser;

            if (resourceEntity == null || resourceEntity.gatherType != ResourceDispenser.GatherType.Ore) return;

            BasePlayer initiatorPlayer = info.InitiatorPlayer; if (initiatorPlayer == null) return;

            if (legacyConfig.General.permissionMode && !initiatorPlayer.IPlayer.HasPermission(permPrefix)) return;

            NextTick(() =>
            {
                if (initiatorPlayer != null) { Interface.CallHook("OnNodeLastHit", resourceEntity, initiatorPlayer); }
            });
        }

        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer basePlayer, Item item)
        {
            if (dispenser == null || basePlayer == null || item == null) return;

            if (dispenser.gatherType != ResourceDispenser.GatherType.Ore) return;
            
            if (legacyConfig.General.permissionMode && !basePlayer.IPlayer.HasPermission(permPrefix)) return;

            if (!nodes.ContainsKey(dispenser)) nodes.Add(dispenser, new NodeData());
                
            double amount = (double)item.amount;

            if (GatherRates != null) { if (ItemModifier(basePlayer.UserIDString, item.info.itemid.ToString()) != 1) amount = amount / ItemModifier(basePlayer.UserIDString, item.info.itemid.ToString()); }

            if (item.info.itemid == stone)
            {

                var data = nodes[dispenser]; 

                int sulfurAmount = (int)Math.Round(amount / 3.75);
                sulfurAmount = ValidateAmount(dispenser, sulfurAmount, "sulfur");

                if (GatherRates != null) { if (ItemModifier(basePlayer.UserIDString, sulfur.ToString()) != 1) sulfurAmount = sulfurAmount * (int)ItemModifier(basePlayer.UserIDString, sulfur.ToString()); }

                DoItemAdd(basePlayer, sulfurAmount, sulfur.ToString());
                
                int metalAmount = (int) Math.Round(amount / 1.505);
                metalAmount = ValidateAmount(dispenser, metalAmount, "metal");

                if (GatherRates != null) { if (ItemModifier(basePlayer.UserIDString, metal.ToString()) != 1) metalAmount = metalAmount * (int)ItemModifier(basePlayer.UserIDString, metal.ToString()); }

                DoItemAdd(basePlayer, metalAmount, metal.ToString());

            }

            if(item.info.itemid == sulfur)
            {

                int stoneAmount = (int)Math.Round(amount * 3.75);
                stoneAmount = ValidateAmount(dispenser, stoneAmount, "stone");
               
                if (GatherRates != null) { if (ItemModifier(basePlayer.UserIDString, stone.ToString()) != 1) stoneAmount = stoneAmount * (int)ItemModifier(basePlayer.UserIDString, stone.ToString()); }

                DoItemAdd(basePlayer, stoneAmount, stone.ToString());

                int metalAmount = (int)Math.Round(amount * 2.5);
                metalAmount = ValidateAmount(dispenser, metalAmount, "metal");

                if (GatherRates != null) { if (ItemModifier(basePlayer.UserIDString, metal.ToString()) != 1) metalAmount = metalAmount * (int)ItemModifier(basePlayer.UserIDString, metal.ToString()); }

                DoItemAdd(basePlayer, metalAmount, metal.ToString());

            }

            if(item.info.itemid == metal)
            {

                int stoneAmount = (int)Math.Round(amount * 1.5);
                stoneAmount = ValidateAmount(dispenser, stoneAmount, "stone");

                if (GatherRates != null) { if (ItemModifier(basePlayer.UserIDString, stone.ToString()) != 1) stoneAmount = stoneAmount * (int)ItemModifier(basePlayer.UserIDString, stone.ToString()); }

                DoItemAdd(basePlayer, stoneAmount, stone.ToString());

                int sulfurAmount = (int)Math.Round(amount / 2.5);
                sulfurAmount = ValidateAmount(dispenser, sulfurAmount, "sulfur");

                if (GatherRates != null) { if (ItemModifier(basePlayer.UserIDString, sulfur.ToString()) != 1) sulfurAmount = sulfurAmount * (int)ItemModifier(basePlayer.UserIDString, sulfur.ToString()); }

                DoItemAdd(basePlayer, sulfurAmount, sulfur.ToString());

                if (legacyConfig.General.bonusEnabled && !blacklisted.Contains(dispenser)) blacklisted.Add(dispenser);
                if (legacyConfig.General.bonusEnabled && !nonbonusmetal.Contains(dispenser)) nonbonusmetal.Add(dispenser);

            }

            if (legacyConfig.General.bonusEnabled) timer.Once(0.3f, () =>
            {
                if (quedLastHit.Contains(basePlayer))
                {
                    quedLastHit.Remove(basePlayer);
                    int highQualAmount = 2; if (GatherRates != null) highQualAmount = highQualAmount * (int) ItemModifier(basePlayer.UserIDString, highqual.ToString());
                    DoItemAdd(basePlayer, highQualAmount, highqual.ToString());
                }
            });
        }

        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer basePlayer, Item item)
        {
            if (!legacyConfig.General.bonusEnabled) return;

            if (dispenser == null || basePlayer == null || item == null) return;
            if (dispenser.gatherType != ResourceDispenser.GatherType.Ore) return;

            if (legacyConfig.General.permissionMode && !basePlayer.IPlayer.HasPermission(permPrefix)) return;

            double amount = (double)item.amount;
            if (GatherRates != null) { if (ItemModifier(basePlayer.UserIDString, item.info.itemid.ToString()) != 1) amount = amount / ItemModifier(basePlayer.UserIDString, item.info.itemid.ToString()); }

            if (item.info.itemid == stone)
            {

                int sulfurAmount = (int)Math.Round(amount / 5 * 2);
                if (GatherRates != null) { if (ItemModifier(basePlayer.UserIDString, sulfur.ToString()) != 1) sulfurAmount = sulfurAmount * (int)ItemModifier(basePlayer.UserIDString, sulfur.ToString()); }

                DoItemAdd(basePlayer, sulfurAmount, sulfur.ToString());

                int metalAmount = (int)Math.Round(amount / 5 * 2.01);
                if (GatherRates != null) { if (ItemModifier(basePlayer.UserIDString, metal.ToString()) != 1) metalAmount = metalAmount * (int)ItemModifier(basePlayer.UserIDString, metal.ToString()); }

                DoItemAdd(basePlayer, metalAmount, metal.ToString());

            }

            if (item.info.itemid == sulfur)
            {

                int stoneAmount = (int)Math.Round(amount * 2.5);
                if (GatherRates != null) { if (ItemModifier(basePlayer.UserIDString, stone.ToString()) != 1) stoneAmount = stoneAmount * (int)ItemModifier(basePlayer.UserIDString, stone.ToString()); }

                DoItemAdd(basePlayer, stoneAmount, stone.ToString());

                int metalAmount = (int)Math.Round(amount);
                if (GatherRates != null) { if (ItemModifier(basePlayer.UserIDString, metal.ToString()) != 1) metalAmount = metalAmount * (int)ItemModifier(basePlayer.UserIDString, metal.ToString()); }

                DoItemAdd(basePlayer, metalAmount, metal.ToString());

            }

            if (item.info.itemid == metal)
            {

                int stoneAmount = (int)Math.Round(amount * 2.495);
                if (GatherRates != null) { if (ItemModifier(basePlayer.UserIDString, stone.ToString()) != 1) stoneAmount = stoneAmount * (int)ItemModifier(basePlayer.UserIDString, stone.ToString()); }

                DoItemAdd(basePlayer, stoneAmount, stone.ToString());

                int sulfurAmount = (int)Math.Round(amount);
                if (GatherRates != null) { if (ItemModifier(basePlayer.UserIDString, sulfur.ToString()) != 1) sulfurAmount = sulfurAmount * (int)ItemModifier(basePlayer.UserIDString, sulfur.ToString()); }

                DoItemAdd(basePlayer, sulfurAmount, sulfur.ToString());
                if (nonbonusmetal.Contains(dispenser)) nonbonusmetal.Remove(dispenser);

            }
        }
        
        //Notice to Developers: OnNodeLastHit always passes a null dispenser, it is simply used as a key.
        private void OnNodeLastHit(ResourceDispenser dispenser, BasePlayer basePlayer)
        {
            if (basePlayer == null) return;

            if (legacyConfig.General.permissionMode && !basePlayer.IPlayer.HasPermission(permPrefix)) return;

            if (blacklisted.Contains(dispenser) && !nonbonusmetal.Contains(dispenser))
            {
                blacklisted.Remove(dispenser);
                nonbonusmetal.Remove(dispenser);
                return;
            }

            if (!legacyConfig.General.bonusEnabled) return;

            if (quedLastHit.Contains(basePlayer))
                quedLastHit.Remove(basePlayer);

            quedLastHit.Add(basePlayer);
        }

        #endregion

        #region Helpers

        private void DoItemAdd(BasePlayer player, int quantity, string itemid)
        {
            if (quantity < 1) return;

            Item resourceItem = ItemManager.Create(FindItemDef(itemid), quantity, 0UL); if (resourceItem == null) return;
            ItemContainer itemContainer = null;
            itemContainer = player.inventory.containerMain;

            player.GiveItem(resourceItem, BaseEntity.GiveItemReason.ResourceHarvested);
        }

        private ItemDefinition FindItemDef(string idOrName)
        {
            ItemDefinition itemDef = ItemManager.FindItemDefinition(idOrName.ToLower());
            if (itemDef == null)
            {
                int itemId;
                if (int.TryParse(idOrName, out itemId))
                {
                    itemDef = ItemManager.FindItemDefinition(itemId);
                }
            }
            return itemDef;
        }

        private int ValidateAmount(ResourceDispenser dispenser, int amount, string nodetype)
        {
            var data = nodes[dispenser];
           
            switch (nodetype)
            {
                case "metal":

                    if (data.metalComplete) break;

                    if (data.rewardedmetal - amount < 5)
                    {
                        int amm = (int)FindDifference(data.rewardedmetal, 0);
                        data.rewardedmetal = data.rewardedmetal - amm;
                        return amm;
                    }

                    int newmetalamount = data.rewardedmetal - amount;

                    if (dispenser.baseEntity.Health() >= 175f && dispenser.baseEntity.Health() <= 260f && newmetalamount <= 355 && data.rewardedmetal >= 355)
                    {
                        data.metalComplete = true;
                        int finalmetal = (int)FindDifference(data.rewardedmetal, 250);
                        data.rewardedmetal = data.rewardedmetal - finalmetal;

                        return finalmetal;
                    }

                    data.rewardedmetal = data.rewardedmetal - amount;
                    return amount;

                case "stone":

                    if (data.stoneComplete) break;

                    if (data.rewardedstone - amount <= 0)
                    {
                        int amm = (int)FindDifference(data.rewardedstone, 0);
                        data.rewardedstone = data.rewardedstone - amm;
                        return amm;
                    }

                    int newstoneamount = data.rewardedstone - amount;

                    if (dispenser.baseEntity.Health() >= 20f && dispenser.baseEntity.Health() <= 65f && newstoneamount <= 415 && data.rewardedstone >= 375)
                    {
                        data.stoneComplete = true;
                        int finalstone = (int)FindDifference(data.rewardedstone, 375);
                        data.rewardedstone = data.rewardedstone - finalstone;
                        return finalstone;
                    }

                    data.rewardedstone = data.rewardedstone - amount;
                    return amount;
                 
                case "sulfur":

                    if (data.sulfurComplete) break;

                    if (data.rewardedsulfur - amount < 0)
                    {
                        int amm = (int)FindDifference(data.rewardedsulfur, 0);
                        data.rewardedsulfur = data.rewardedsulfur - amm;
                        return amm;
                    }

                    int newsulfuramount = data.rewardedsulfur - amount;
                    if (dispenser.baseEntity.Health() >= 20f && dispenser.baseEntity.Health() <= 65f && newsulfuramount <= 105 && data.rewardedsulfur >= 100)
                    {
                        data.sulfurComplete = true;
                        int finalsulfur = (int)FindDifference(data.rewardedsulfur, 100);
                        data.rewardedstone = data.rewardedstone - finalsulfur;
                        return finalsulfur;
                    }

                    data.rewardedsulfur = data.rewardedsulfur - amount;
                    return amount;

                default:
                    break;
            }
            return 0;
        }

        private float ItemModifier(string userId, string itemid)
        {
            Item resourceItem = ItemManager.Create(FindItemDef(itemid), 1, 0UL); if (resourceItem == null) return 1f;

            var ruleset = GetPlayerRuleset(userId);
            if (ruleset == null)
                return 1f;

            var rate = ruleset.GetGatherRate(resourceItem);
            return rate;
        }

        private decimal FindDifference(decimal amountone, decimal amounttwo)
        {
            return Math.Abs(amountone - amounttwo);
        }

        #endregion

        #region Localization

        private string GetLang(string message) => lang.GetMessage(message, this);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["announcement"] = "Legacy Nodes Enabled",
                ["confCorrupt"] = "Config Is Corrupt",
                ["configUpdated"] = "Version Change Detected, Updating Config",
                ["pmNull"] = "Error: Private Message Null",
                ["announceNull"] = "Error: Announcement Null",
                ["invalidGatherRatesConfig"] = "Configuration file is invalid, Gather scaling will not function"

            }, this);
        }

        #endregion

        #region GatherRatesByWhiteThunder

        private const string permFormat = "gatherrates.ruleset.{0}";

        private static string GetPermission(string name) => string.Format(permFormat, name);

        private GatherRatesConfig grconfig;

        private class GatherRatesConfig
        {
            [JsonProperty("GatherRateRulesets")]
            public GatherRateRuleset[] GatherRateRulesets = new GatherRateRuleset[]
            {
                new GatherRateRuleset()
                {
                    Name = "2x",
                    DefaultRate = 2,
                },
                new GatherRateRuleset()
                {
                    Name = "5x",
                    DefaultRate = 5,
                },
                new GatherRateRuleset()
                {
                    Name = "10x",
                    DefaultRate = 10,
                },
                new GatherRateRuleset()
                {
                    Name = "100x",
                    DefaultRate = 100,
                },
                new GatherRateRuleset()
                {
                    Name = "1000x",
                    DefaultRate = 1000,
                }
            };
        }

        private GatherRateRuleset GetPlayerRuleset(string userId)
        {
            var rulesets = grconfig.GatherRateRulesets;

            if (userId == string.Empty || rulesets == null)
                return null;

            for (var i = rulesets.Length - 1; i >= 0; i--)
            {
                var ruleset = rulesets[i];
                if (!string.IsNullOrEmpty(ruleset.Name)
                    && permission.UserHasPermission(userId, GetPermission(ruleset.Name)))
                    return ruleset;
            }

            return null;
        }

        private class GatherRateRuleset
        {
            [JsonProperty("Name")]
            public string Name;

            [JsonProperty("DefaultRate")]
            public float DefaultRate = 1;

            [JsonProperty("ItemRateOverrides", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public Dictionary<string, float> ItemRateOverrides = null;

            [JsonProperty("DispenserRateOverrides", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public Dictionary<string, Dictionary<string, float>> DispenserRateOverrides = null;

            public float GetGatherRate(Item item)
            {
                Dictionary<string, float> itemRates;
                float rate;
                if (DispenserRateOverrides != null
                    && DispenserRateOverrides.TryGetValue(item.info.displayName.english, out itemRates)
                    && itemRates.TryGetValue(item.info.shortname, out rate))
                    return rate;

                if (ItemRateOverrides != null
                    && ItemRateOverrides.TryGetValue(item.info.shortname, out rate))
                    return rate;

                return DefaultRate;
            }
        }

        private void LoadGatherRatesConfig()
        {
            try
            {
                grconfig = Interface.Oxide.DataFileSystem.ReadObject<GatherRatesConfig>("GatherRates");
                if (grconfig == null)
                {
                    throw new JsonException();
                }
            }
            catch
            {
                LogWarning(GetLang("invalidGatherRatesConfig"));
                GatherRates = null;
            }
        }

        #endregion

    }
}