using Newtonsoft.Json;
using Oxide.Core.Libraries;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Oil Rig Discord Notify", "Dana", "0.0.6")]
    [Description("Notifies when the hackable locked crate in both Oil Rigs gets activated.")]
    internal class OilRigDiscordNotify : RustPlugin
    {
        //[DiscordClient] DiscordClient Client;
        private Configuration config;
        private Timer LoginCheck;
        const float calgon = 0.0066666666666667f;
        public Dictionary<string, Vector3> GridInfo = new Dictionary<string, Vector3>();
        Vector3 SmallOilRigPos; // OilrigAI2 = monumentinfo.name
        Vector3 BigOilRigPos; // OilrigAI = monumentinfo.name

        public class Configuration
        {
            [JsonProperty(PropertyName = "Plugin - Enabled")]
            public bool Enabled { get; set; } = true;

            [JsonProperty(PropertyName = "Discord - Webhook URL")]
            public string DiscordWebHookUrl { get; set; }

            [JsonProperty(PropertyName = "Discord - Mention Roles (Roles IDs)")]
            public List<string> MentionRoles { get; set; } = new List<string>();

            [JsonProperty(PropertyName = "Discord - Message - Text")]
            public string MessageText { get; set; } = "Oil Rig Hackable Locked Crate has been activated by {0} {1} and 15 minutes timer count down has started.";

            [JsonProperty(PropertyName = "Discord - Embed - Image - URLs")]
            public List<string> EmbedImages { get; set; } = new List<string>() { "https://i.imgur.com/gqctTxU.png", "https://i.imgur.com/1V54pdo.png", "https://i.imgur.com/HFuYyC9.png" };

            [JsonProperty(PropertyName = "Discord - Embed - Image - Randomly Select (false = use the first, true = random)")]
            public bool SelectRandomImage { get; set; } = true;

            [JsonProperty(PropertyName = "Discord - Embed - Color")]
            public int EmbedColor { get; set; } = 3092790;

            [JsonProperty(PropertyName = "Discord - Embed - Field 1 Title")]
            public string EmbedField1Title { get; set; } = "Monument Type";

            [JsonProperty(PropertyName = "Discord - Embed - Field 2 Title")]
            public string EmbedField2Title { get; set; } = "Direction";
        }

        enum Direction { North, South, East, West };

        private void Init()
        {
            config = Config.ReadObject<Configuration>();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        private void OnServerInitialized()
        {
            var monuments = UnityEngine.Object.FindObjectsOfType<MonumentInfo>();
            foreach (var monument in monuments)
            {
                if (monument.name == "OilrigAI") // Small rig
                {
                    SmallOilRigPos = monument.transform.position;
                }
                else if (monument.name == "OilrigAI2") // Big rig
                {
                    BigOilRigPos = monument.transform.position;
                }
            }
        }

        Direction GetDirection(Vector3 location)
        {
            // The grid is split up with an X in the middle
            // location.y is up/down
            var x = location.x;
            var z = location.z;
            if (z > 0)
            {
                // Northern vicinity
                if (x > 0)
                {
                    // Eastern vicinity
                    if (x < z) return Direction.North;
                    else return Direction.East;
                }
                else
                {
                    // Western vicinity
                    x = Math.Abs(x);
                    if (x < z) return Direction.North;
                    else return Direction.West;
                }
            }
            else
            {
                // Southern vicinity
                if (x > 0)
                {
                    // Eastern vicinity
                    z = Math.Abs(z);
                    if (x < z) return Direction.South;
                    else return Direction.East;
                }
                else
                {
                    // Western vicinity
                    x = Math.Abs(x);
                    z = Math.Abs(z);
                    if (x < z) return Direction.South;
                    else return Direction.West;
                }
            }
        }

        bool isOnRig(Vector3 rigVector, Vector3 pos)
        {
            return Math.Sqrt(Math.Pow(Math.Abs(rigVector.x - pos.x), 2) + Math.Pow(Math.Abs(rigVector.z - pos.z), 2)) <= 30;
        }

        void CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (!config.Enabled)
                return;

            if (isOnRig(BigOilRigPos, crate.transform.position))
            {
                AccountForRoles(true, player);
            }
            else if (isOnRig(SmallOilRigPos, crate.transform.position))
            {
                AccountForRoles(false, player);
            }
        }

        void AccountForRoles(bool bigRig, BasePlayer hacker)
        {
            if (config.MentionRoles.Count == 0)
            {
                SendToDiscordBot(bigRig, hacker);
                return;
            }
            var mentions = "";
            foreach (var roleId in config.MentionRoles)
            {
                mentions += $"<@&{roleId}> ";
            }
            SendToDiscordBot(bigRig, hacker, mentions);
        }

        void SendToDiscordBot(bool bigRig, BasePlayer hacker, string mentions = null)
        {
            var imageURL = config.EmbedImages.Count == 0 ? "" : config.SelectRandomImage ? config.EmbedImages[UnityEngine.Random.Range(0, config.EmbedImages.Count)] : config.EmbedImages[0];
            var dir = GetDirection(bigRig ? BigOilRigPos : SmallOilRigPos);
            var embed = new WebHookEmbed
            {
                Image = new WebHookImage
                {
                    Url = imageURL
                },
                Color = config.EmbedColor,
                Fields = new List<WebHookField>
                {
                    new WebHookField
                    {
                        Name = config.EmbedField1Title,
                        Value = bigRig ? "Large Oil Rig" : "Small Oil Rig",
                        Inline = true
                    },
                    new WebHookField
                    {
                        Name = config.EmbedField2Title,
                        Value =  dir == Direction.North ? "North" : dir == Direction.South ? "South" : dir == Direction.East ? "East" : "West",
                        Inline = true
                    }
                }
            };

            var contentBody = new WebHookContentBody
            {
                Content = $"{mentions}{string.Format(config.MessageText, hacker.displayName, hacker.userID)}"
            };
            var embedBody = new WebHookEmbedBody
            {
                Embeds = new[]
                {
                    embed
                }
            };
            webrequest.Enqueue(config.DiscordWebHookUrl, JsonConvert.SerializeObject(contentBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                (headerCode, headerResult) =>
                {
                    if (headerCode >= 200 && headerCode <= 204)
                    {
                        webrequest.Enqueue(config.DiscordWebHookUrl, JsonConvert.SerializeObject(embedBody, new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }),
                            (code, result) => { }, this, RequestMethod.POST,
                            new Dictionary<string, string> { { "Content-Type", "application/json" } });
                    }
                }, this, RequestMethod.POST,
                new Dictionary<string, string> { { "Content-Type", "application/json" } });
        }

        private class WebHookEmbedBody
        {
            [JsonProperty(PropertyName = "embeds")]
            public WebHookEmbed[] Embeds;
        }

        private class WebHookContentBody
        {
            [JsonProperty(PropertyName = "content")]
            public string Content;
        }

        private class WebHookEmbed
        {
            [JsonProperty(PropertyName = "title")]
            public string Title;

            [JsonProperty(PropertyName = "type")]
            public string Type = "rich";

            [JsonProperty(PropertyName = "description")]
            public string Description;

            [JsonProperty(PropertyName = "color")]
            public int Color;

            [JsonProperty(PropertyName = "author")]
            public WebHookAuthor Author;

            [JsonProperty(PropertyName = "image")]
            public WebHookImage Image;

            [JsonProperty(PropertyName = "fields")]
            public List<WebHookField> Fields;

            [JsonProperty(PropertyName = "footer")]
            public WebHookFooter Footer;
        }
        private class WebHookAuthor
        {
            [JsonProperty(PropertyName = "name")]
            public string Name;

            [JsonProperty(PropertyName = "url")]
            public string AuthorUrl;

            [JsonProperty(PropertyName = "icon_url")]
            public string AuthorIconUrl;
        }
        private class WebHookImage
        {
            [JsonProperty(PropertyName = "proxy_url")]
            public string ProxyUrl;

            [JsonProperty(PropertyName = "url")]
            public string Url;

            [JsonProperty(PropertyName = "height")]
            public int? Height;

            [JsonProperty(PropertyName = "width")]
            public int? Width;
        }
        private class WebHookField
        {
            [JsonProperty(PropertyName = "name")]
            public string Name;

            [JsonProperty(PropertyName = "value")]
            public string Value;

            [JsonProperty(PropertyName = "inline")]
            public bool Inline;
        }
        private class WebHookFooter
        {
            [JsonProperty(PropertyName = "text")]
            public string Text;

            [JsonProperty(PropertyName = "icon_url")]
            public string IconUrl;

            [JsonProperty(PropertyName = "proxy_icon_url")]
            public string ProxyIconUrl;
        }
    }
}