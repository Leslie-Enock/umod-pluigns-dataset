using ConVar;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Chat Head", "Ujiou", "1.4.2")]
    [Description("Displays chat messages above player to other players in range.")]
    class ChatHead : RustPlugin
    {
        #region Config
        ChatHeadConfig config;

        protected override void LoadDefaultConfig() => config = LoadBaseConfig();

        protected override void SaveConfig() => Config.WriteObject(config, true);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ChatHeadConfig>();

                if (config == null)
                    throw new JsonException();

                if (config.Version < Version || config.Version > Version)
                    LoadDefaultConfig();
                    SaveConfig();
            }
            catch
            {
                Puts("Config was created!");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        private ChatHeadConfig LoadBaseConfig()
        {
            return new ChatHeadConfig
            {
                settings = new ChatHeadConfig.Settings
                {
                    textColor = "#ffffff",
                    textHeight = 2.5f,
                    textSize = 25,
                    hideTeamChat = true,
                },
                vanishS = new ChatHeadConfig.VanishSettings
                {
                    vanishHideAdmins = true
                },
                Version = Version
            };
        }

        public class ChatHeadConfig
        {
            [JsonProperty(PropertyName = "Settings: ")]
            public Settings settings { get; set; }

            [JsonProperty(PropertyName = "Vanish Settings: ")]
            public VanishSettings vanishS { get; set; }


            public class Settings
            {
                [JsonProperty(PropertyName = "Text Color")]
                public string textColor { get; set; }

                [JsonProperty(PropertyName = "Text Height")]
                public float textHeight { get; set; }

                [JsonProperty(PropertyName = "Text Size")]
                public int textSize { get; set; }

                [JsonProperty(PropertyName = "Hide Team Chat")]
                public bool hideTeamChat { get; set; }
            }

            public class VanishSettings
            {
                [JsonProperty(PropertyName = "Hide Text")]
                public bool vanishHideAdmins { get; set; }
            }

            [JsonProperty(PropertyName = "Version: ")]
            public Core.VersionNumber Version { get; set; }
        }
        #endregion

        #region Chat Handling
        readonly Dictionary<string, string> lastMessage = new Dictionary<string, string>();

        void OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (lastMessage.ContainsKey(player.UserIDString))
                lastMessage[player.UserIDString] = message;
            else
                lastMessage.Add(player.UserIDString, message);

            if (channel == Chat.ChatChannel.Team && config.settings.hideTeamChat)
                return;

            if (Vanish != null)
                if (isInvisible(player) && config.vanishS.vanishHideAdmins)
                    return;

            foreach (var target in BasePlayer.activePlayerList) DrawChat(target, player);
        }

        void DrawChat(BasePlayer target, BasePlayer player)
        {
            var distance = Vector3.Distance(target.transform.position, player.transform.position);

            if (!target.IsConnected || !player.IsConnected || distance >= 20) return;

            var color = config.settings.textColor.Contains("#") ? ColorTranslator.FromHtml(config.settings.textColor) : ColorTranslator.FromHtml("#{textColor}");

            timer.Repeat(0.1f, 80, () =>
            {
                if (!target.IsConnected || !player.IsConnected || !Equals(lastMessage[player.UserIDString], lastMessage[player.UserIDString])) return;

                var format = $"<size={config.settings.textSize}>{lastMessage[player.UserIDString]}</size>";
                if (target.IsAdmin)
                    target.SendConsoleCommand("ddraw.text", 0.1f, color, player.transform.position + (Vector3.up * config.settings.textHeight), format);
                else 
                {
                    GiveFlag(target, BasePlayer.PlayerFlags.IsAdmin, true);
                    target.SendConsoleCommand("ddraw.text", 0.1f, color, player.transform.position + (Vector3.up * config.settings.textHeight), format);
                    GiveFlag(target, BasePlayer.PlayerFlags.IsAdmin, false);
                }
            });

        }
        #endregion

        #region Vanish Plugin
        [PluginReference]
        private Plugin Vanish;

        private void Loaded()
        {
            if (Vanish != null)
                Vanish.Load();
        }

        public bool isInvisible(BasePlayer player) =>
            Vanish.Call<bool>("IsInvisible", player);
        #endregion

        #region Helpers
        private void GiveFlag(BasePlayer player, BasePlayer.PlayerFlags flag, bool value)
        {
            if (player == null) 
                return;

            player.SetPlayerFlag(flag, value);
            player.SendNetworkUpdateImmediate();
        }
        #endregion
    }
}