using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("Vending Machine Logs", "Ryz0r", "1.0.5"), Description("Logs vending machine transactions to Discord.")]
    public class VendingMachineLogs : RustPlugin
    {
        private const string BypassPerm = "vendingmachinelogs.bypass";
        #region Configuration
        private Configuration _config;
        private class Configuration
        {
            [JsonProperty(PropertyName = "Discord Webhook URL")]
            public string WebhookURL = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

            [JsonProperty(PropertyName = "Ignore Non-Player Vending Machines")]
            public bool IgnoreNonPlayer = false;
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

        private void OnVendingTransaction(VendingMachine machine, BasePlayer player, int sellOrderId, int numberOfTransactions)
        {
            if (permission.UserHasPermission(player.UserIDString, BypassPerm)) return;
            if (_config.IgnoreNonPlayer && machine.OwnerID == 0) return;
           
            var sellOrder = machine.sellOrders.sellOrders[sellOrderId];
           
            var currency = sellOrder.currencyID;
            var currencyAmount = sellOrder.currencyAmountPerItem * numberOfTransactions;
            var currencyName = ItemManager.FindItemDefinition(currency).displayName.english;
                       
            var item = sellOrder.itemToSellID;
            var itemAmount = sellOrder.itemToSellAmount * numberOfTransactions;
            var itemName = ItemManager.FindItemDefinition(item).displayName.english;
                       
            SendDiscordMessage(player.displayName, player.UserIDString, itemName, item, currencyName, currency, machine.shopName, itemAmount, currencyAmount, machine.OwnerID.ToString());

        }

        #endregion
        #region Functions
        private void SendDiscordMessage(string customerName, string customerID, string soldItem, int soldItemID, string currencyItem, int currencyItemID, string shopName, int soldQuantity, int currencyQuantity, string machineOwner)
        {
            var embed = new Embed()
                .AddField("Customer:", customerName + " (" + customerID + ")", true)
                .AddField("Shop Name:", shopName, true)
                .AddField("Shop Owner ID:", machineOwner, true)
                
                .AddField("Sold Item:", soldItem, true)
                .AddField("Sold Item ID:", soldItemID.ToString(), true)
                .AddField("Amount Sold:", soldQuantity.ToString(), true)
                
                .AddField("Currency Item:", currencyItem, true)
                .AddField("Currency Item ID:", currencyItemID.ToString(), true)
                .AddField("Amount Spent:", currencyQuantity.ToString(), true)
                .SetColor("#00FFFF");
            
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