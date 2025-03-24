// Requires: Guardian
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Guardian ADR", "Rockioux", "1.0.2")]
    [Description("Starts demo recordings on Guardian Violations.")]
    class GuardianADR : CovalencePlugin
    {

        #region Config Classes

        private class GuardianADRConfig
        {
            [JsonProperty(PropertyName = "Gravity Violation Demo Config")]
            public HookConfig GravityHookConfig { get; set; } = new HookConfig(true);

            [JsonProperty(PropertyName = "Melee Rate Violation Demo Config")]
            public HookConfig MeleeRateConfig { get; set; } = new HookConfig(true);

            [JsonProperty(PropertyName = "NoClip Violation Demo Config")]
            public HookConfig NoClipConfig { get; set; } = new HookConfig(false);

            [JsonProperty(PropertyName = "InsideTerrain Violation Demo Config")]
            public HookConfig InsideTerrainConfig { get; set; } = new HookConfig(false);
        }

        private class HookConfig
        {
            [JsonProperty(PropertyName = "Enable Demo Recordings for this hook")]
            public bool HookEnabled { get; set; }

            [JsonProperty(PropertyName = "Demo Duration in minutes")]
            public int DemoDuration { get; set; } = 2;

            [JsonProperty(PropertyName = "Discord Webhook")]
            public string DiscordWebhook { get; set; } = "";

            public HookConfig()
            {
            }

            public HookConfig(bool enabled)
            {
                HookEnabled = enabled;
            }
        }

        #endregion // Config Classes

        #region Fields

        [PluginReference]
        private Plugin AutoDemoRecord;

        private GuardianADRConfig _config;

        #endregion // Fields

        #region Covalence Plugin Implemented Members

        private void Init()
        {
            if(!_config.GravityHookConfig.HookEnabled)
            {
                Unsubscribe(nameof(OnGuardianAntiCheatGravity));
            }

            if (!_config.MeleeRateConfig.HookEnabled)
            {
                Unsubscribe(nameof(OnGuardianAntiCheatMeleeRate));
            }

            if(!_config.NoClipConfig.HookEnabled && !_config.InsideTerrainConfig.HookEnabled)
            {
                Unsubscribe(nameof(OnGuardianServer));
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<GuardianADRConfig>();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig()
        {
            _config = new GuardianADRConfig();
        }

        #endregion // Covalence Plugin Implemented Members

        #region Guardian Hooks

        private void OnGuardianAntiCheatGravity(string playerID, Dictionary<string, string> details)
        {
            try
            {
                BasePlayer player;
                if (!GetBasePlayer(playerID, out player))
                {
                    return;
                }

                // Check if already recording
                if (player.Connection.IsRecording)
                {
                    return;
                }

                Vector3 pos = player.transform.position;
                string message = $"{player.displayName} ({playerID}) is defying gravity!\n\nElevation: {details["elevation"]}\nMovement speed: {details["movement_speed"]}\nViolation: {details["violation_id"]}\nPosition: teleportpos {pos.x}, {pos.y}, {pos.z}";
                AutoDemoRecord?.Call("API_StartRecording4", player, message, _config.GravityHookConfig.DemoDuration, _config.GravityHookConfig.DiscordWebhook);
            }
            catch(Exception ex)
            {
                LogError(ex.ToString());
            }
        }

        private void OnGuardianAntiCheatMeleeRate(string playerID, Dictionary<string, string> details)
        {
            try
            {
                BasePlayer player;
                if (!GetBasePlayer(playerID, out player))
                {
                    return;
                }

                // Check if already recording
                if (player.Connection.IsRecording)
                {
                    return;
                }

                string message = $"{player.displayName} ({playerID}) is meleeing too fast!\n\nWeapon Type: {details["weapon_type"]}\nMovement speed: {details["movement_speed"]}\nRate Percentage: {details["rate_percent"]}%\nViolation: {details["violation_id"]}";
                AutoDemoRecord?.Call("API_StartRecording4", player, message, _config.MeleeRateConfig.DemoDuration, _config.MeleeRateConfig.DiscordWebhook);
            }
            catch(Exception ex)
            {
                LogError(ex.ToString());
            }
        }

        private void OnGuardianServer(string playerID, Dictionary<string, string> details)
        {
            try
            {
                AntiHackType antihackType = (AntiHackType)Enum.Parse(typeof(AntiHackType), details["antihack_type"]);

                if ((antihackType != AntiHackType.NoClip || !_config.NoClipConfig.HookEnabled)
                    && (antihackType != AntiHackType.InsideTerrain || !_config.InsideTerrainConfig.HookEnabled))
                {
                    return;
                }

                BasePlayer player;
                if (!GetBasePlayer(playerID, out player))
                {
                    return;
                }

                // Check if already recording
                if (player.Connection.IsRecording)
                {
                    return;
                }

                Vector3 pos = player.transform.position;
                HookConfig config = antihackType == AntiHackType.NoClip ? _config.NoClipConfig : _config.InsideTerrainConfig;
                string actionStr = antihackType == AntiHackType.NoClip ? "no clipping" : "inside terrain";
                string message = $"{player.displayName} ({playerID}) is {actionStr}!\n\nViolation: {details["violation_id"]}\nPosition: teleportpos {pos.x}, {pos.y}, {pos.z}";

                AutoDemoRecord?.Call("API_StartRecording4", player, message, config.DemoDuration, config.DiscordWebhook);
            }
            catch(Exception ex)
            {
                LogError(ex.ToString());
            }
        }

        #endregion // Guardian Hooks

        #region Helper Methods

        private bool GetBasePlayer(string playerID, out BasePlayer player)
        {
            player = null;

            ulong steamID;
            if (!ulong.TryParse(playerID, out steamID))
            {
                LogError($"Unexpected error. Could not parse player steam id {playerID}.");
                return false;
            }

            player = BasePlayer.FindByID(steamID);
            if (player == null)
            {
                LogError($"Unexpected error. Player with steam id {playerID} was not found.");
                return false;
            }
            return true;
        }

        #endregion // Helper Methods

    }
}
