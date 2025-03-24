using System;
using UnityEngine.Networking;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Simple Bradley Kill Feed", "chrome", "1.0.2")]
    [Description("Logs who killed bradley to a webhook.")]
    public class SimpleBradleyKillFeed : RustPlugin
    {
        private DynamicConfigFile _config;
        private string _webhookurl;

        public class PluginConfig
        {
            [JsonProperty("webhookurl")]
            public string WebhookUrl { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new PluginConfig { WebhookUrl = "https://discord.com/api/webhooks/your_webhook_url" }, true);
        }

        private void Init()
        {
            Unsubscribe(nameof(OnEntityDeath));
        }

        private void OnServerInitialized()
        {
            var config = Config.ReadObject<PluginConfig>();
            _webhookurl = config.WebhookUrl;

            if (_webhookurl != "https://discord.com/api/webhooks/your_webhook_url")
            {
                Subscribe(nameof(OnEntityDeath));
            }
        }

        private void OnEntityDeath(BradleyAPC entity, HitInfo info)
        {
            var player = info?.InitiatorPlayer;
            {
                SendDiscordEmbed(player);
            }
        }

        private void SendDiscordEmbed(BasePlayer player)
        {
            if (player != null)
            {
                var json = $"{{\"content\":\"{player.displayName} ({player.UserIDString}) has just destroyed Bradley!\"}}";
                var request = new UnityWebRequest(_webhookurl, "POST");
                byte[] jsonToSend = new System.Text.UTF8Encoding().GetBytes(json);
                request.uploadHandler = new UploadHandlerRaw(jsonToSend);
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.SendWebRequest();
                if (request.isNetworkError || request.isHttpError)
                {
                    PrintError("Error sending webhook: " + request.error);
                }
            }
        }
    }
}
