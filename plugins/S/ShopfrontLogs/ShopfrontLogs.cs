using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("Shopfront Logs", "Ryz0r", "1.0.0"), Description("Logs shopfront completed trades to Discord.")]
    public class ShopfrontLogs : RustPlugin
    {
        private const string BypassPerm = "shopfrontlogs.bypass";
        #region Configuration
        private Configuration _config;
        private class Configuration
        {
            [JsonProperty(PropertyName = "Discord Webhook URL")]
            public string WebhookURL = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("A new configuration file is being generated.");
            _config = new Configuration();
        }


        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion
        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(BypassPerm, this);
        }
        private object OnShopCompleteTrade(ShopFront entity)
        {
            if (permission.UserHasPermission(entity.customerPlayer.UserIDString, BypassPerm) ||
                permission.UserHasPermission(entity.vendorPlayer.UserIDString, BypassPerm)) return null;
            
            var vendorName = entity.vendorPlayer.displayName;
            var vendorId = entity.vendorPlayer.UserIDString;
            var cleanVendorItems = new List<string>();
            entity.vendorInventory.itemList.ToList().ForEach(item =>
                cleanVendorItems.Add($"{item.info.shortname.SentenceCase()} x {item.amount}"));


            var customerName = entity.customerPlayer.displayName;
            var customerId = entity.customerPlayer.UserIDString;
            var cleanCustomerItems = new List<string>();
            entity.customerInventory.itemList.ToList().ForEach(item =>
                cleanCustomerItems.Add($"{item.info.shortname.SentenceCase()} x {item.amount}"));

            SendDiscordMessage($"{vendorName} ({vendorId})", $"{customerName} ({customerId})", cleanVendorItems,
                cleanCustomerItems);
            return null;
        }
        #endregion
        #region Functions
        private void SendDiscordMessage(string v, string c, IReadOnlyCollection<string> vItems, IReadOnlyCollection<string> cItems)
        {
            var vItemString = vItems.Count < 1 ? "Null" : string.Join(", ", vItems);
            var cItemString = cItems.Count < 1 ? "Null" : string.Join(", ", cItems);
            
            var embed = new Embed()
                .AddField("Vendor:", v, true)
                .AddField("Customer:", c, true)
                .AddField("Vendor Items", vItemString, false)
                .AddField("Customer Items", cItemString, false)
                .SetColor("#FF0000");
            
            var headers = new Dictionary<string, string>() {{"Content-Type", "application/json"}};
            const float timeout = 500f;
            
            webrequest.Enqueue(_config.WebhookURL, new DiscordMessage("", embed).ToJson(),  GetCallback, this,
                RequestMethod.POST, headers, timeout);
        }
        
        private void GetCallback(int code, string response)
        {
            if (response != null && code == 204) return;
            
            Puts($"Error: {code} - Couldn't get an answer from server.");
        }
        #endregion
        #region Discord Stuff
        private class DiscordMessage
        {
            public DiscordMessage(string content, params Embed[] embeds)
            {
                Content = content;
                Embeds  = embeds.ToList();
            }

            [JsonProperty("content")] public string Content { get; set; }
            [JsonProperty("embeds")] public List<Embed> Embeds { get; set; }
            

            public string ToJson() => JsonConvert.SerializeObject(this);
        }

        private class Embed
        {
            [JsonProperty("fields")] public List<Field> Fields { get; set; } = new List<Field>();
            [JsonProperty("color")] public int Color { get; set; }

            public Embed AddField(string name, string value, bool inline)
            {
                Fields.Add(new Field(name, Regex.Replace(value, "<.*?>", string.Empty), inline));

                return this;
            }
            
            public Embed SetColor(string color)
            {
                var replace = color.Replace("#", "");
                var decValue = int.Parse(replace, System.Globalization.NumberStyles.HexNumber);
                Color = decValue;
                return this;
            }
        }

        private class Field
        {
            public Field(string name, string value, bool inline)
            {
                Name = name;
                Value = value;
                Inline = inline;
            }

            [JsonProperty("name")] public string Name { get; set; }
            [JsonProperty("value")] public string Value { get; set; }
            [JsonProperty("inline")] public bool Inline { get; set; }
        }
        #endregion
    }
}