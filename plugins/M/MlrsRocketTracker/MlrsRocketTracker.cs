using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("MLRS Rocket Tracker", "MadKingCraig", "1.1.0")]
    [Description("Track where the MLRS is fired at")]
    class MlrsRocketTracker : RustPlugin
    {
        #region Fields

        [PluginReference]
        private Plugin DiscordMessages;

        #endregion Fields

        #region Hooks

        private void Init()
        {
            _config = Config.ReadObject<PluginConfig>();
        }

        private void OnMlrsFired(MLRS mlrs, BasePlayer player)
        {
            var lastSentTargetHitPos = CalculateGridPosition(mlrs.lastSentTargetHitPos);
            var lastSentTrueHitPos = CalculateGridPosition(mlrs.lastSentTrueHitPos);

            if (_config.LogToConsole)
                Puts(lang.GetMessage("FiredMessage", this, player.UserIDString), player.displayName, lastSentTargetHitPos, lastSentTrueHitPos);

            if (_config.UseDiscord)
                SendDiscordMessage(player.displayName, player.UserIDString, string.Format(lang.GetMessage("DiscordMessage", this, player.UserIDString), lastSentTargetHitPos, lastSentTrueHitPos));
        }

        #endregion Hooks

        #region Functions

        private string CalculateGridPosition(Vector3 position)
        {
            int maxGridSize = Mathf.FloorToInt(World.Size / 146.3f) - 1;
            int xGrid = Mathf.Clamp(Mathf.FloorToInt((position.x + (World.Size / 2f)) / 146.3f), 0, maxGridSize);
            string extraA = string.Empty;
            if (xGrid > 26) extraA = $"{(char)('A' + (xGrid / 26 - 1))}";
            return $"{extraA}{(char)('A' + xGrid % 26)}{Mathf.Clamp(maxGridSize - Mathf.FloorToInt((position.z + (World.Size / 2f)) / 146.3f), 0, maxGridSize).ToString()}";
        }

        private void SendDiscordMessage(string name, string playerId, string text)
        {
            object fields = new object[]
            {
                new
                {
                    name = "Player", value = $"[{name}](https://steamcommunity.com/profiles/{playerId})", inline = true
                },
                new
                {
                    name = "MLRS Info", value = text, inline = false
                }
            };
            string json = JsonConvert.SerializeObject(fields);
            DiscordMessages?.Call("API_SendFancyMessage", _config.WebhookUrl, "MLRS Rocket Tracker", 1, json);
        }

        #endregion Functions

        #region Config
        private PluginConfig _config;

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                LogToConsole = true,
                UseDiscord = false,
                WebhookUrl = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks"
            };
        }

        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Log to Console")]
            public bool LogToConsole;
            [JsonProperty(PropertyName = "Use Discord Webhook")]
            public bool UseDiscord;
            [JsonProperty(PropertyName = "Discord Webhook URL")]
            public string WebhookUrl;
        }

        #endregion Config

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["FiredMessage"] = "{0} fired the MLRS at {1} and missiles hit {2}!",
                ["DiscordMessage"] = "MLRS fired at {0} and hit {1}"
            }, this);
        }

        #endregion Lang
    }
}
