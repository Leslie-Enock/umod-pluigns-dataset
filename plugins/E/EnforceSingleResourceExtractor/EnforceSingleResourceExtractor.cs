/* --- Contributor information ---
 * Please follow the following set of guidelines when working on this plugin,
 * this to help others understand this file more easily.
 * 
 * NOTE: On Authors, new entries go BELOW the existing entries. As with any other software header comment.
 *
 * -- Authors --
 * Thimo (ThibmoRozier) <thibmorozier@live.nl> 2021-03-15 +
 *
 * -- Naming --
 * Avoid using non-alphabetic characters, eg: _
 * Avoid using numbers in method and class names (Upgrade methods are allowed to have these, for readability)
 * Private constants -------------------- SHOULD start with a uppercase "C" (PascalCase)
 * Private readonly fields -------------- SHOULD start with a uppercase "C" (PascalCase)
 * Private fields ----------------------- SHOULD start with a uppercase "F" (PascalCase)
 * Arguments/Parameters ----------------- SHOULD start with a lowercase "a" (camelCase)
 * Classes ------------------------------ SHOULD start with a uppercase character (PascalCase)
 * Methods ------------------------------ SHOULD start with a uppercase character (PascalCase)
 * Public properties (constants/fields) - SHOULD start with a uppercase character (PascalCase)
 * Variables ---------------------------- SHOULD start with a lowercase character (camelCase)
 *
 * -- Style --
 * Max-line-width ------- 160
 * Single-line comments - // Single-line comment
 * Multi-line comments -- Just like this comment block!
 */
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Enforce Single Resource Extractor", "ThibmoRozier", "1.0.5")]
    [Description("Enforce players only being able to use a single quarry and/or pump jack.")]
    public class EnforceSingleResourceExtractor : RustPlugin
    {
        #region Types
        private enum ExtractorType
        {
            PumpJack,
            Quarry
        }

        private struct QuarryState
        {
            public ulong PlayerId;
            public uint ExtractorId;
            public ExtractorType Type;
        }
        #endregion Types

        #region Constants
        // Not sure what the placable prefab is called, just playing safe
        private static readonly string[] CPumpJackPrefabs = { "pumpjack", "pump_jack", "pump-jack", "pumpjack-static" };
        private static readonly string[] CQuarryPrefabs = { "mining_quarry", "miningquarry_static" };
        private static readonly IEnumerable<string> CCombinedPrefabs = CPumpJackPrefabs.Concat(CQuarryPrefabs);

        // Permissions
        private const String CPermWhitelist = "enforcesingleresourceextractor.whitelist";
        #endregion Constants

        #region Variables
        private ConfigData FConfigData;
        private Timer FCleanupTimer;
        private readonly List<QuarryState> FPlayerExtractorList = new List<QuarryState>();
        #endregion Variables

        #region Config
        /// <summary>
        /// The config type class
        /// </summary>
        private class ConfigData
        {
            [DefaultValue(true)]
            [JsonProperty("Ignore Extractor Type", DefaultValueHandling = DefaultValueHandling.Populate)]
            public bool IgnoreExtractorType { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try {
                FConfigData = Config.ReadObject<ConfigData>();

                if (FConfigData == null)
                    LoadDefaultConfig();
            } catch (Exception) {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            FConfigData = new ConfigData {
                IgnoreExtractorType = true
            };
        }

        protected override void SaveConfig() => Config.WriteObject(FConfigData);
        #endregion Config

        #region Script Methods
        private void CheckExtractorIsOff()
        {
            List<uint> removeIds = new List<uint>();
            BaseNetworkable extractor;

            foreach (var item in FPlayerExtractorList) {
                // Check whitelist permission, just remove entry when whitelisted
                if (permission.UserHasPermission(item.PlayerId.ToString(), CPermWhitelist)) {
                    removeIds.Add(item.ExtractorId);
                    continue;
                }

                try {
                    // Check the prefab name, just to be sure, since we use the net.ID which could be reused after the entity is killed.
                    extractor = BaseNetworkable.serverEntities.First(x => x.net.ID == item.ExtractorId && CCombinedPrefabs.Contains(x.ShortPrefabName));

                    if ((extractor as MiningQuarry).IsEngineOn())
                        continue;
                } catch(ArgumentNullException) { }

                removeIds.Add(item.ExtractorId);
            }

            if (removeIds.Count > 0)
                FPlayerExtractorList.RemoveAll(x => removeIds.Contains(x.ExtractorId));
        }
        #endregion Script Methods

        #region Hooks
        void OnServerInitialized()
        { 
            LoadConfig();
            permission.RegisterPermission(CPermWhitelist, this);
            FCleanupTimer = timer.Every(1f, CheckExtractorIsOff);
        }

        void Unload()
        {
            if (FCleanupTimer != null && !FCleanupTimer.Destroyed)
                FCleanupTimer.Destroy();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(
                new Dictionary<string, string> {
                    { "Warning Message Text", "<color=#FF7900>You can only run a single resource extractor at any given time.</color>" }
                }, this, "en"
            );
        }

        void OnQuarryToggled(MiningQuarry aExtractor, BasePlayer aPlayer)
        {
            /* This check takes care of 2 simple cases:
             *   1> The player is whitelisted, just remove
             *   2> Extractor was turned off, don't care about player ID, just remove
             */
            if (permission.UserHasPermission(aPlayer.UserIDString, CPermWhitelist) || !aExtractor.IsEngineOn()) {
                FPlayerExtractorList.RemoveAll(x => aExtractor.net.ID == x.ExtractorId);
                return;
            }

            ExtractorType type;

            if (CPumpJackPrefabs.Contains(aExtractor.ShortPrefabName)) {
                type = ExtractorType.PumpJack;
            } else if (CQuarryPrefabs.Contains(aExtractor.ShortPrefabName)) {
                type = ExtractorType.Quarry;
            } else {
                // Skip anything we don't care about
                return;
            }

            if (FPlayerExtractorList.Count(x => aPlayer.userID == x.PlayerId && (FConfigData.IgnoreExtractorType || type == x.Type)) > 0) {
                // Turn engine OFF
                aExtractor.EngineSwitch(false);
                // Warn the player
                aPlayer.ChatMessage(lang.GetMessage("Warning Message Text", this, aPlayer.UserIDString));
                return;
            }

            FPlayerExtractorList.Add(new QuarryState { PlayerId = aPlayer.userID, ExtractorId = aExtractor.net.ID, Type = type });
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (CCombinedPrefabs.Contains(entity.ShortPrefabName))
                FPlayerExtractorList.RemoveAll(x => entity.net.ID == x.ExtractorId);
        }
        #endregion Hooks
    }
}
