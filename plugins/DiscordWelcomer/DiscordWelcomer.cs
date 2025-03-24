using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord.Clients;
using Oxide.Ext.Discord.Connections;
using Oxide.Ext.Discord.Constants;
using Oxide.Ext.Discord.Entities;
using Oxide.Ext.Discord.Interfaces;
using Oxide.Ext.Discord.Logging;

namespace Oxide.Plugins
{
    [Info("Discord Welcomer", "Trey", "2.1.0")]
    [Description("Welcomes players when they join your Discord server.")]
    public class DiscordWelcomer : RustPlugin, IDiscordPlugin
    {
        #region Fields
        
        public DiscordClient Client { get; set; }

        private DiscordGuild _guild;

        private readonly BotConnection _settings = new BotConnection
        {
            Intents = GatewayIntents.Guilds | GatewayIntents.GuildMembers
        };
        #endregion

        #region Data

        Data _Data;
        public class Data
        {
            public List<string> ExistingData = new List<string>();
        }

        private void LoadData() => _Data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _Data);

        #endregion

        #region Configuration

        Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Discord Bot Token")]
            public string BotToken = string.Empty;
            
            [JsonProperty(PropertyName = "Discord Server ID (Optional if bot only in 1 guild)")]
            public Snowflake GuildId { get; set; }

            [JsonProperty(PropertyName = "Your Discord ID (For Testing)")]
            public Snowflake TestID;

            [JsonProperty(PropertyName = "Discord Embed Title")]
            public string EmbedTitle = "Welcome!";

            [JsonProperty(PropertyName = "Discord Embed Color (No '#')")]
            public string EmbedColor = "66B2FF";

            [JsonProperty(PropertyName = "Discord Embed Author Name (Leave Blank if Unwanted)")]
            public string EmbedAuthorName = "Server Administration";

            [JsonProperty(PropertyName = "Discord Embed Author Icon URL (Leave Blank if Unwanted)")]
            public string EmbedAuthorURL = "https://steamuserimages-a.akamaihd.net/ugc/687094810512264399/04BA8A55B390D1ED0389E561E95775BCF33A9857/";

            [JsonProperty(PropertyName = "Discord Embed Thumbnail Link (Leave Blank if Unwanted)")]
            public string EmbedThumbnailURL = "https://leganerd.com/wp-content/uploads/2014/05/Rust-logo.png";

            [JsonProperty(PropertyName = "Discord Embed Full Image URL (Leave Blank if Unwanted)")]
            public string EmbedFullImageURL = "https://leganerd.com/wp-content/uploads/2014/05/Rust-logo.png";

            [JsonProperty(PropertyName = "Discord Embed Footer Text (Leave Blank if Unwanted)")]
            public string EmbedFooterText = "Thanks for playing with us!";

            [JsonProperty(PropertyName = "Discord Embed Footer Image URL (Leave Blank if Unwanted)")]
            public string EmbedFooterURL = "https://steamuserimages-a.akamaihd.net/ugc/687094810512264399/04BA8A55B390D1ED0389E561E95775BCF33A9857/";

            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty(PropertyName = "Discord Extension Log Level (Verbose, Debug, Info, Warning, Error, Exception, Off)")]
            public DiscordLogLevel ExtensionDebugging { get; set; } = DiscordLogLevel.Info;
            
            [JsonProperty(PropertyName = "Config Version")]
            public string ConfigVersion = "2.0.1";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Localization

        public class LangKeys
        {
            public const string Welcome_New = "Welcome_New";
            public const string Welcome_Existing = "Welcome_Existing";
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Welcome_New] = "Welcome to our Discord Server! Please read over our rules and regulations. We truly hope you enjoy your time with us!",
                [LangKeys.Welcome_Existing] = "Welcome back! Please read over our rules and regulations. We hope you stay with us this time!",
            }, this);
        }

        #endregion

        #region Core Methods
        private void OnServerInitialized()
        {
            LoadData();
            CheckConfigVersion(Version);

            if (config.BotToken != string.Empty)
            {
                _settings.ApiToken = config.BotToken;
                _settings.LogLevel = config.ExtensionDebugging;
                Client.Connect(_settings);
            }
            else
            {
                PrintWarning($"{Name} cannot function while your Discord Bot Token is empty!");
            }
        }

        private void Unload()
        {
            SaveData();
        }

        private void OnServerSave()
        {
            SaveData();
        }

        [HookMethod(DiscordExtHooks.OnDiscordGatewayReady)]
        private void OnDiscordGatewayReady(GatewayReadyEvent ready)
        {
            if (ready.Guilds.Count == 0)
            {
                PrintError("Your bot was not found in any discord servers. Please invite it to a server and reload the plugin.");
                return;
            }

            DiscordGuild guild = null;
            if (ready.Guilds.Count == 1 && !config.GuildId.IsValid())
            {
                guild = ready.Guilds.Values.FirstOrDefault();
            }

            if (guild == null)
            {
                guild = ready.Guilds[config.GuildId];
            }

            if (guild == null)
            {
                PrintError("Failed to find a matching guild for the Discord Server Id. " +
                           "Please make sure your guild Id is correct and the bot is in the discord server.");
                return;
            }
                
            if (Client.Bot.Application.Flags.HasValue && !Client.Bot.Application.Flags.Value.HasFlag(ApplicationFlags.GatewayGuildMembersLimited))
            {
                PrintError($"You need to enable \"Server Members Intent\" for {Client.Bot.BotUser.Username} @ https://discord.com/developers/applications\n" +
                           $"{Name} will not function correctly until that is fixed. Once updated please reload {Name}.");
                return;
            }

            _guild = guild;

            Puts($"{Title} ready!");
        }
        
        [HookMethod(DiscordExtHooks.OnDiscordGuildMemberAdded)]
        private void OnDiscordGuildMemberAdded(GuildMember member)
        {
            if (member == null) return;
            if (Client == null) return;

            member.User.CreateDirectMessageChannel(Client).Then(dm => 
            {
            
                if (_Data.ExistingData.Contains(member.User.Id))
                {
                    dm.CreateMessage(Client, CreateEmbed(Lang(LangKeys.Welcome_Existing, null)));
                }

                else
                {
                    dm.CreateMessage(Client, CreateEmbed(Lang(LangKeys.Welcome_New, null)));
                    _Data.ExistingData.Add(member.User.Id);
                }
            
            });
        }
        #endregion

        #region Command
        [ConsoleCommand("testwelcomemessage")]
        private void TestWelcomeCommand(ConsoleSystem.Arg args)
        {
            if (args.Args == null)
            {
                if (config.TestID == string.Empty)
                {
                    PrintError("We couldn't send you a test message because your Test ID is empty in your config.");
                    return;
                }

                GuildMember member = _guild?.Members[config.TestID];

                if (member == null) return;

                OnDiscordGuildMemberAdded(member);
            }
        }
        #endregion

        #region Helpers

        private DiscordEmbed CreateEmbed(string message)
        {
            DiscordEmbed embed = new DiscordEmbed
            {
                Title = config.EmbedTitle,
                Color = ConvertColorToDiscordColor(config.EmbedColor),
                Author = new EmbedAuthor
                {
                    Name = config.EmbedAuthorName,
                    IconUrl = config.EmbedAuthorURL
                },
                Thumbnail = new EmbedThumbnail
                {
                    Url = config.EmbedThumbnailURL
                },
                Description = message,
                Footer = new EmbedFooter
                {
                    Text = config.EmbedFooterText,
                    IconUrl = config.EmbedFooterURL
                },
                Image = new EmbedImage
                {
                    Url = config.EmbedFullImageURL
                },
                Timestamp = DateTime.UtcNow
            };

            return embed;
        }

        private DiscordColor ConvertColorToDiscordColor(string colorcode) => new DiscordColor(Convert.ToUInt32($"0x{colorcode}", 16));

        string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void CheckConfigVersion(VersionNumber version)
        {
            if (config.ConfigVersion != version.ToString())
            {
                Config.WriteObject(config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonOld");
                PrintError("Your configuration file is out of date, generating up to date one.\nThe old configuration file was saved in the .jsonOld extension");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        #endregion
    }
}