using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using static TechTreeData;

namespace Oxide.Plugins
{
    [Info("Tech Tree Control", "WhiteThunder", "0.5.0")]
    [Description("Allows customizing Tech Tree research requirements.")]
    internal class TechTreeControl : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private readonly Plugin PopupNotifications;

        private const string PermissionAnyOrderLevel1 = "techtreecontrol.anyorder.level1";
        private const string PermissionAnyOrderLevel2 = "techtreecontrol.anyorder.level2";
        private const string PermissionAnyOrderLevel3 = "techtreecontrol.anyorder.level3";
        private const string PermissionAnyOrderIO = "techtreecontrol.anyorder.io";

        private readonly object True = true;
        private readonly object False = false;

        private Configuration _config;

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionAnyOrderLevel1, this);
            permission.RegisterPermission(PermissionAnyOrderLevel2, this);
            permission.RegisterPermission(PermissionAnyOrderLevel3, this);
            permission.RegisterPermission(PermissionAnyOrderIO, this);

            _config.Init(this);

            if (!_config.IsCustomCurrencyEnabledAndValid)
            {
                Unsubscribe(nameof(OnTechTreeNodeUnlock));
            }
        }

        private void OnServerInitialized()
        {
            if (_config.EnablePopupNotifications && PopupNotifications == null)
            {
                LogWarning("PopupNotifications integration is enabled in the config, but the PopupNotifications plugin isn't loaded.");
            }
        }

        private object OnTechTreeNodeUnlock(Workbench workbench, NodeInstance node, BasePlayer player)
        {
            var currencyAmountOverride = _config.GetResearchCostOverride(node.itemDef);
            if (currencyAmountOverride is int currencyAmount)
            {
                var taxRate = ConVar.Server.GetTaxRateForWorkbenchUnlock(workbench.Workbenchlevel);
                if (taxRate > 0)
                {
                    currencyAmount += Mathf.CeilToInt(currencyAmount * (taxRate / 100f));
                }
            }
            else
            {
                currencyAmount = Workbench.ScrapForResearch(node.itemDef, workbench.Workbenchlevel, out var tax);
                currencyAmount += tax;
            }

            var itemid = _config.CustomCurrency.ItemId;
            if (player.inventory.GetAmount(itemid) >= currencyAmount)
            {
                player.inventory.Take(null, itemid, currencyAmount);
                player.blueprints.Unlock(node.itemDef);
                Interface.CallHook("OnTechTreeNodeUnlocked", workbench, node, player);
            }

            return False;
        }

        private object CanUnlockTechTreeNode(BasePlayer player, NodeInstance node, TechTreeData techTree)
        {
            var blueprintRuleset = _config.GetPlayerBlueprintRuleset(this, player.UserIDString);
            if (blueprintRuleset == null)
                return null;

            if (node.itemDef != null && !blueprintRuleset.IsAllowed(node.itemDef))
            {
                var message = GetMessage(player.UserIDString,
                    blueprintRuleset.IsOptional(node.itemDef)
                        ? LangEntry.BlueprintDisallowedOptional
                        : LangEntry.BlueprintDisallowed);

                if (_config.EnablePopupNotifications)
                {
                    PopupNotifications?.Call("CreatePopupNotification", message, player);
                }

                if (_config.EnableChatFeedback)
                {
                    player.ChatMessage(message);
                }

                return False;
            }

            return null;
        }

        private object CanUnlockTechTreeNodePath(BasePlayer player, NodeInstance node, TechTreeData techTree)
        {
            if (HasPermissionToUnlockAny(player, techTree))
                return True;

            var blueprintRuleset = _config.GetPlayerBlueprintRuleset(this, player.UserIDString);
            if (blueprintRuleset == null)
                return null;

            if (node.itemDef != null && !blueprintRuleset.HasPrerequisites(node.itemDef))
                return True;

            if (HasUnlockPath(player, node, techTree, blueprintRuleset))
                return True;

            return null;
        }

        private object OnResearchCostDetermine(ItemDefinition itemDefinition)
        {
            return _config.GetResearchCostOverride(itemDefinition);
        }

        #endregion

        #region Helper Methods

        public static void LogError(string message) => Interface.Oxide.LogError($"[Tech Tree Control] {message}");
        public static void LogWarning(string message) => Interface.Oxide.LogWarning($"[Tech Tree Control] {message}");

        private static bool HasUnlockPath(BasePlayer player, NodeInstance node, TechTreeData techTree, BlueprintRuleset blueprintRuleset)
        {
            if (node.inputs.Count == 0)
                return true;

            var hasUnlockPath = false;

            foreach (var inputNodeId in node.inputs)
            {
                var inputNode = techTree.GetByID(inputNodeId);
                if (inputNode.itemDef == null)
                {
                    // This input node appears to be an entry node, so consider it unlocked.
                    return true;
                }

                if (!techTree.HasPlayerUnlocked(player, inputNode) && !blueprintRuleset.IsOptional(inputNode.itemDef))
                {
                    // This input node does not provide an unlock path.
                    // Continue iterating the other input nodes in case they provide an unlock path.
                    continue;
                }

                if (HasUnlockPath(player, inputNode, techTree, blueprintRuleset))
                {
                    hasUnlockPath = true;
                }
            }

            return hasUnlockPath;
        }

        private bool HasPermissionToUnlockAny(BasePlayer player, TechTreeData techTree)
        {
            if (techTree.name == "TechTreeT3")
                return permission.UserHasPermission(player.UserIDString, PermissionAnyOrderLevel3);

            if (techTree.name == "TechTreeT2")
                return permission.UserHasPermission(player.UserIDString, PermissionAnyOrderLevel2);

            if (techTree.name == "TechTreeT0")
                return permission.UserHasPermission(player.UserIDString, PermissionAnyOrderLevel1);

            if (techTree.name == "TechTreeIO")
                return permission.UserHasPermission(player.UserIDString, PermissionAnyOrderIO);

            return false;
        }

        #endregion

        #region Configuration

        [JsonObject(MemberSerialization.OptIn)]
        private class CustomCurrency
        {
            [JsonProperty("Enabled")]
            public bool Enabled;

            [JsonProperty("Item short name")]
            public string ItemShortName = "scrap";

            [JsonIgnore]
            public int ItemId;

            [JsonIgnore]
            public bool IsEnabledAndValid => Enabled && ItemId != 0;

            public void Init()
            {
                if (!Enabled)
                    return;

                var itemDefinition = ItemManager.FindItemDefinition(ItemShortName);
                if (itemDefinition == null)
                {
                    LogWarning($"Invalid item short name in config: {ItemShortName}");
                }
                else
                {
                    ItemId = itemDefinition.itemid;
                }
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class BlueprintRuleset
        {
            public static readonly BlueprintRuleset DefaultRuleset = new();

            [JsonProperty("Name")]
            private string Name;

            [JsonProperty("Optional blueprints")]
            private string[] OptionalBlueprints = Array.Empty<string>();

            [JsonProperty("OptionalBlueprints")]
            private string[] DeprecatedOptionalBlueprints { set => OptionalBlueprints = value; }

            [JsonProperty("Allowed blueprints", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private string[] AllowedBlueprints;

            [JsonProperty("AllowedBlueprints")]
            private string[] DeprecatedAllowedBlueprints { set => AllowedBlueprints = value; }

            [JsonProperty("Disallowed blueprints", DefaultValueHandling = DefaultValueHandling.Ignore)]
            private string[] DisallowedBlueprints;

            [JsonProperty("DisallowedBlueprints")]
            private string[] DeprecatedDisallowedBlueprints { set => DisallowedBlueprints = value; }

            [JsonProperty("Blueprints with no prerequisites")]
            private string[] BlueprintsWithNoPrerequisites = Array.Empty<string>();

            [JsonProperty("BlueprintsWithNoPrerequisites")]
            private string[] DeprecatedBlueprintsWithNoPrerequisites { set => BlueprintsWithNoPrerequisites = value; }

            public string Permission { get; private set; }
            private HashSet<int> _optionalBlueprints = new();
            private HashSet<int> _allowedBlueprints = new();
            private HashSet<int> _disallowedBlueprints = new();
            private HashSet<int> _blueprintsWithNoPrerequisites = new();

            public void Init(TechTreeControl plugin)
            {
                if (!string.IsNullOrWhiteSpace(Name))
                {
                    Permission = $"{nameof(TechTreeControl)}.ruleset.{Name}".ToLower();
                    plugin.permission.RegisterPermission(Permission, plugin);
                }

                CacheItemIds(OptionalBlueprints, _optionalBlueprints);
                CacheItemIds(AllowedBlueprints, _allowedBlueprints);
                CacheItemIds(DisallowedBlueprints, _disallowedBlueprints);
                CacheItemIds(BlueprintsWithNoPrerequisites, _blueprintsWithNoPrerequisites);
            }

            public bool HasPrerequisites(ItemDefinition itemDefinition)
            {
                return !_blueprintsWithNoPrerequisites.Contains(itemDefinition.itemid);
            }

            public bool IsAllowed(ItemDefinition itemDefinition)
            {
                if (AllowedBlueprints != null)
                    return _allowedBlueprints.Contains(itemDefinition.itemid);

                if (DisallowedBlueprints != null)
                    return !_disallowedBlueprints.Contains(itemDefinition.itemid);

                return true;
            }

            public bool IsOptional(ItemDefinition itemDefinition)
            {
                return _optionalBlueprints.Contains(itemDefinition.itemid);
            }

            private static void CacheItemIds(IEnumerable<string> shortNameList, HashSet<int> cachedItemIds)
            {
                if (shortNameList == null)
                    return;

                foreach (var itemShortName in shortNameList)
                {
                    var itemDefinition = ItemManager.FindItemDefinition(itemShortName);
                    if (itemDefinition == null)
                    {
                        LogError($"Invalid item short name in config: {itemShortName}");
                        continue;
                    }

                    cachedItemIds.Add(itemDefinition.itemid);
                }
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        private class Configuration : BaseConfiguration
        {
            [JsonProperty("Enable chat feedback")]
            public bool EnableChatFeedback = true;

            [JsonProperty("Enable PopupNotifications integration")]
            public bool EnablePopupNotifications;

            [JsonProperty("Research costs")]
            private Dictionary<string, int> ResearchCosts = new();

            [JsonProperty("Custom currency")]
            public CustomCurrency CustomCurrency = new();

            [JsonProperty("ResearchCosts")]
            private Dictionary<string, int> DeprecatedResearchCosts { set => ResearchCosts = value; }

            [JsonProperty("Blueprint rulesets")]
            private BlueprintRuleset[] BlueprintRulesets = Array.Empty<BlueprintRuleset>();

            [JsonProperty("BlueprintRulesets")]
            private BlueprintRuleset[] DeprecatedBlueprintRulesets { set => BlueprintRulesets = value; }

            private Dictionary<int, object> _researchCostByItemId = new();

            [JsonIgnore]
            public bool IsCustomCurrencyEnabledAndValid => CustomCurrency is { IsEnabledAndValid: true };

            public void Init(TechTreeControl plugin)
            {
                CustomCurrency?.Init();

                if (BlueprintRulesets != null)
                {
                    foreach (var ruleset in BlueprintRulesets)
                    {
                        ruleset.Init(plugin);
                    }
                }

                if (ResearchCosts != null)
                {
                    foreach (var entry in ResearchCosts)
                    {
                        var itemDefinition = ItemManager.FindItemDefinition(entry.Key);
                        if (itemDefinition == null)
                        {
                            LogError($"Invalid item short name in config: {entry.Key}");
                            continue;
                        }

                        _researchCostByItemId[itemDefinition.itemid] = entry.Value;
                    }
                }
            }

            public object GetResearchCostOverride(ItemDefinition itemDefinition)
            {
                return _researchCostByItemId.TryGetValue(itemDefinition.itemid, out var costOverride)
                    ? costOverride
                    : null;
            }

            public BlueprintRuleset GetPlayerBlueprintRuleset(TechTreeControl plugin, string userIdString)
            {
                if (BlueprintRulesets != null)
                {
                    for (var i = BlueprintRulesets.Length - 1; i >= 0; i--)
                    {
                        var ruleset = BlueprintRulesets[i];
                        if (ruleset.Permission != null
                            && plugin.permission.UserHasPermission(userIdString, ruleset.Permission))
                            return ruleset;
                    }
                }

                return BlueprintRuleset.DefaultRuleset;
            }
        }

        private Configuration GetDefaultConfig() => new();

        #region Configuration Helpers

        private class BaseConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(BaseConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            var changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                if (currentRaw.TryGetValue(key, out var currentRawValue))
                {
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (currentWithDefaults[key] is Dictionary<string, object> defaultDictValue)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                        {
                            changed = true;
                        }
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_config))
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
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #endregion

        #endregion

        #region Localization

        private class LangEntry
        {
            public static readonly List<LangEntry> AllLangEntries = new();

            public static readonly LangEntry BlueprintDisallowed = new("BlueprintDisallowed", "You don't have permission to unlock that blueprint.");
            public static readonly LangEntry BlueprintDisallowedOptional = new("BlueprintDisallowed.Optional", "You don't have permission to unlock that blueprint, but it can be skipped.");

            public string Name;
            public string English;

            public LangEntry(string name, string english)
            {
                Name = name;
                English = english;

                AllLangEntries.Add(this);
            }
        }

        private string GetMessage(string playerId, LangEntry langEntry) =>
            lang.GetMessage(langEntry.Name, this, playerId);

        private void ChatMessage(BasePlayer player, LangEntry langEntry) =>
            player.ChatMessage(GetMessage(player.UserIDString, langEntry));

        protected override void LoadDefaultMessages()
        {
            var englishLangKeys = new Dictionary<string, string>();

            foreach (var langEntry in LangEntry.AllLangEntries)
            {
                englishLangKeys[langEntry.Name] = langEntry.English;
            }

            lang.RegisterMessages(englishLangKeys, this, "en");
        }

        #endregion
    }
}
