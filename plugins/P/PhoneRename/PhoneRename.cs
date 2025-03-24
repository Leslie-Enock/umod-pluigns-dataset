using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Oxide.Core.Libraries;

namespace Oxide.Plugins
{
    [Info("Phone Rename", "Bazz3l", "0.0.4")]
    [Description("Ability to rename naughty named phones and changes to discord.")]
    public class PhoneRename : CovalencePlugin
    {
        [PluginReference] private Plugin DiscordMessages;
        
        #region Fields

        private const string PermUse = "phonerename.use";

        private readonly Dictionary<string, string> _headers = new Dictionary<string, string> { { "Content-Type", "application/json" } };
        
        private PluginConfig _pluginConfig;

        #endregion
        
        #region Config

        protected override void LoadDefaultConfig() => _pluginConfig = new PluginConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _pluginConfig = Config.ReadObject<PluginConfig>();

                if (_pluginConfig == null)
                {
                    throw new JsonException();
                }
            }
            catch
            {
                LoadDefaultConfig();

                PrintError("Config file contains an error and has been replaced with the default file.");
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_pluginConfig);
        
        private class PluginConfig
        {
            [JsonProperty("DiscordWebhook (discord webhook url here)")]
            public string DiscordWebhook = "https://discord.com/api/webhooks/webhook-here";

            [JsonProperty("DiscordColor (discord embed color)")]
            public int DiscordColor = 65535;

            [JsonProperty("DiscordAuthor (discord embed author name)")]
            public string DiscordAuthor = "Phone Rename";
            
            [JsonProperty("DiscordAuthorImageUrl (discord embed author image url)")]
            public string DiscordAuthorImageUrl = "https://assets.umod.org/images/icons/plugin/5fa92b3f428d1.png";
            
            [JsonProperty("DiscordAuthorUrl (discord embed author url)")]
            public string DiscordAuthorUrl = "https://umod.org/plugins/phone-rename";
            
            [JsonProperty("LogToDiscord (log updated phone names to a discord channel)")]
            public bool LogToDiscord;

            [JsonProperty("WordList (list of ad words)")]
            public List<string> WordList = new List<string>
            {
                "fucker",
                "fuck",
                "cunt",
                "twat",
                "wanker",
                "bastard"
            };
        }

        #endregion
        
        #region Oxide

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> {
                { "NoPermission", "No permission" },
                { "InvalidSyntax", "Invalid syntax, renamephone <phone-number> <new-name>" },
                { "NotFound", "No telephone found by that phone number." },
                { "Updated", "Phone was updated to {0}." },
                { "PhoneNumber", "Phone Number" },
                { "PhoneName", "Phone Name" },
                { "Connect", "Connect" },
                { "Server", "Server" },
                { "Profile", "Profile" }
            }, this);
        }
        
        private void Init()
        {
            permission.RegisterPermission(PermUse, this);
        }

        private object OnPhoneNameUpdate(PhoneController phoneController, string phoneName, BasePlayer player) => UpdatePhoneName(player.IPlayer, phoneController, phoneName);

        #endregion
        
        #region Core

        private object UpdatePhoneName(IPlayer player, PhoneController phoneController, string phoneName)
        {
            phoneController.PhoneName = FilterWord(phoneName);
            phoneController._baseEntity.SendNetworkUpdate();
            
            if (_pluginConfig.LogToDiscord)
            {
                SendDiscordMessage(player, phoneName, phoneController.PhoneNumber.ToString());
            }

            return false;
        }
        
        private string FilterWord(string phoneName)
        {
            foreach (string filteredWord in _pluginConfig.WordList)
            {
                string strReplace = "";
                
                for (int i = 0; i <= filteredWord.Length; i++)
                {
                    strReplace += "*";
                }
                
                phoneName = Regex.Replace(phoneName, filteredWord, strReplace, RegexOptions.IgnoreCase);
            }
            
            return phoneName;
        }
        
        private Telephone FindByPhoneNumber(int phoneNumber)
        {
            foreach (Telephone telephone in BaseNetworkable.serverEntities.OfType<Telephone>())
            {
                if (telephone.Controller.PhoneNumber == phoneNumber)
                {
                    return telephone;
                }
            }

            return null;
        }

        #endregion

        #region Command

        [Command("renamephone")]
        private void RenamePhoneCommand(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PermUse))
            {
                player.Message(Lang("NoPermission", player.Id));
                return;
            }
            
            if (args.Length < 2)
            {
                player.Message(Lang("InvalidSyntax", player.Id));
                return;
            }
            
            int phoneNumber;
            if (!int.TryParse(args[0], out phoneNumber))
            {
                player.Message(Lang("InvalidSyntax", player.Id));
                return;
            }
            
            Telephone telephone = FindByPhoneNumber(phoneNumber);
            if (telephone == null)
            {
                player.Message(Lang("NotFound", player.Id));
                return;
            }

            string phoneName = string.Join(" ", args.Skip(1).ToArray());

            UpdatePhoneName(player, telephone.Controller, phoneName);
            
            player.Message(Lang("Updated", player.Id, phoneName));
        }

        #endregion

        #region Discord

        private void SendDiscordMessage(IPlayer player, string phoneName, string phoneNumber)
        {
            webrequest.Enqueue(_pluginConfig.DiscordWebhook, new DiscordMessage("", new List<Embed>
            {
                new Embed
                {
                    Color = _pluginConfig.DiscordColor,
                    Author = new Author
                    {
                        Name = _pluginConfig.DiscordAuthor,
                        Url = _pluginConfig.DiscordAuthorUrl,
                        IconUrl = _pluginConfig.DiscordAuthorImageUrl,
                    },
                    Fields = new List<Field>
                    {
                        new Field(Lang("Server"), ConVar.Server.hostname, false),
                        new Field(Lang("PhoneNumber"), phoneNumber, false),
                        new Field(Lang("PhoneName"), phoneName, false),
                        new Field(Lang("Profile"), $"[{player.Name}](https://steamcommunity.com/profiles/{player.Id})", false),
                        new Field(Lang("Connect"), $"steam://connect/{covalence.Server.Address}:{covalence.Server.Port}", false),
                    }
                }
            }).ToJson(), ( code, response ) => {}, this, RequestMethod.POST, _headers);
        }

        private class DiscordMessage
        {
            [JsonProperty("content")]
            public string Content { get; set; }

            [JsonProperty("embeds")]
            public List<Embed> Embeds { get; set; } = new List<Embed>();
            
            public DiscordMessage(string content, List<Embed> embeds)
            {
                Content = content;
                Embeds = embeds;
            }

            public string ToJson()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        private class Embed
        {
            [JsonProperty("author")]
            public Author Author;
            
            [JsonProperty("title")]
            public string Title { get; set; }

            [JsonProperty("description")]
            public string Description { get; set; }

            [JsonProperty("color")]
            public int Color { get; set; }

            [JsonProperty("fields")]
            public List<Field> Fields { get; set; } = new List<Field>();
        }

        private class Author
        {
            [JsonProperty("icon_url")]
            public string IconUrl;
            
            [JsonProperty("name")]
            public string Name;
            
            [JsonProperty("url")]
            public string Url;
        }

        private class Field
        {
            public Field(string name, string value, bool inline)
            {
                Name = name;
                Value = value;
                Inline = inline;
            }

            [JsonProperty("name")] 
            public string Name { get; set; }

            [JsonProperty("value")] 
            public string Value { get; set; }

            [JsonProperty("inline")] 
            public bool Inline { get; set; }
        }

        #endregion
        
        #region Helpers

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}