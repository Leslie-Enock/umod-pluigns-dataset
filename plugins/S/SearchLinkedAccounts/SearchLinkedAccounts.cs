using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord;
using Oxide.Ext.Discord.Attributes;
using Oxide.Ext.Discord.Builders;
using Oxide.Ext.Discord.Entities;
using Oxide.Ext.Discord.Entities.Activities;
using Oxide.Ext.Discord.Entities.Gatway;
using Oxide.Ext.Discord.Entities.Gatway.Commands;
using Oxide.Ext.Discord.Entities.Gatway.Events;
using Oxide.Ext.Discord.Entities.Guilds;
using Oxide.Ext.Discord.Entities.Messages;
using Oxide.Ext.Discord.Entities.Messages.Embeds;
using Oxide.Ext.Discord.Entities.Permissions;
using Oxide.Ext.Discord.Libraries.Command;
using Oxide.Ext.Discord.Libraries.Linking;
using System;
using System.Collections.Generic;
using System.Linq;
namespace Oxide.Plugins
{
    [Info("Search Linked Accounts", "Farkas", "1.0.1")]
    [Description("Search DiscordAuth or DiscordCore linked accounts trough discord.")]
    public class SearchLinkedAccounts : CovalencePlugin
    {
        #region Global Variables
        [PluginReference] private Plugin DiscordAuth, DiscordCore;
        [DiscordClient] private DiscordClient _client;
        private DiscordGuild _guild;
        private DiscordRole _role;
        private readonly DiscordLink _link = GetLibrary<DiscordLink>();
        private readonly DiscordCommand _dcCommands = Interface.Oxide.GetLibrary<DiscordCommand>();
        #endregion

        #region Configuration
        private ConfigData _configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Bot token")]
            public string Token = "";
            [JsonProperty(PropertyName = "Discord Guild ID (optional if the bot is in one guild)")]
            public Snowflake GuildId { get; set; }
            [JsonProperty(PropertyName = "Discord Role ID that can use the command")]
            public Snowflake RoleId { get; set; }
            [JsonProperty(PropertyName = "Discord Channel ID where the command can be used")]
            public Snowflake ChannelID { get; set; }
            [JsonProperty(PropertyName = "Set custom status and activity for the discord bot")]
            public bool EnableCustomStatus = true;
            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty(PropertyName = "Bot's activity type: (Game, Listening, Watching, Competing)")]
            public ActivityType ActivityType = ActivityType.Watching;
            [JsonProperty(PropertyName = "Bot's Status")]
            public string Status = "Linked Accounts";
            [JsonProperty(PropertyName = "Embed's color")]
            public string Color = "#FFFFFF";
        }
        protected override void LoadConfig()
        {
            try
            {
                base.LoadConfig();
                _configData = Config.ReadObject<ConfigData>();
                SaveConfig(_configData);
            }
            catch (Exception)
            {
                PrintError(Lang("ConfigIssue"));
                return;
            }
        }
        void Init()
        {
            if (string.IsNullOrEmpty(_configData.Token))
            {
                PrintError(Lang("NoToken"));
                return;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts(Lang("NewConfig"));
            _configData = new ConfigData();
            SaveConfig(_configData);
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region Discord Bot Connection
        private void OnDiscordClientCreated()
        {
            DiscordSettings discordSettings = new DiscordSettings();
            discordSettings.ApiToken = _configData.Token;
            discordSettings.Intents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.DirectMessages | GatewayIntents.GuildMessages;
            //remove below for debugging
            //discordSettings.LogLevel = DiscordLogLevel.Verbose;
            _client.Connect(discordSettings);
        }
        private void OnDiscordGatewayReady(GatewayReadyEvent ready)
        {
            if (ready.Guilds.Count == 0)
            {
                PrintError(Lang("InviteBot"));
                _client.Disconnect();
                return;
            }

            DiscordGuild guild = null;

            if (ready.Guilds.Count == 1 && !ready.Guilds.Values.Contains(ready.Guilds[_configData.GuildId]))
            {
                guild = ready.Guilds.Values.FirstOrDefault();
            }

            if (guild == null)
            {
                guild = ready.Guilds[_configData.GuildId];
            }

            if (guild == null)
            {

                _client.Disconnect();
                return;
            }

            _guild = guild;

            if (_configData.EnableCustomStatus)
            {
                _client.Bot.UpdateStatus(new UpdatePresenceCommand
                {
                    Activities = new List<DiscordActivity> { new DiscordActivity { Name = _configData.Status, Type = _configData.ActivityType } }
                });
            }

            if (string.IsNullOrEmpty(_configData.ChannelID.ToString()))
            {
                _dcCommands.AddGuildCommand("search", this, null, nameof(SearchCommand));
            }
            else
            {
                _dcCommands.AddGuildCommand("search", this, new List<Snowflake> { _configData.ChannelID }, nameof(SearchCommand));
            }

            Puts(Lang("Connected"));
        }
        void OnDiscordGuildMembersLoaded(DiscordGuild guild)
        {
            foreach (DiscordRole role in guild.Roles.Values)
            {
                if (role.Id == _configData.RoleId)
                {
                    _role = role;
                    break;
                }
            }

            if (_role == null)
            {
                PrintError(Lang("RoleNotFound"));
                _client.Disconnect();
            }
        }
        #endregion

        #region Commands
        void SearchCommand(DiscordMessage message, string cmd, string[] args)
        {
            if (!DiscordAuth && !DiscordCore)
            {
                message.Reply(_client, CreateEmbed(Lang("NoLinkerPluginTitle"), Lang("NoLinkerPluginContent"), "", "", _configData.Color));
                return;
            }
            if (!message.Member.HasRole(_role) || message.Author.Bot == true)
            {
                message.Reply(_client, CreateEmbed(Lang("NotAllowedTitle"), Lang("NotAllowedContent"), "", "", _configData.Color));
                return;
            }

            if (args.Length == 0)
            {
                message.Reply(_client, CreateEmbed(Lang("InvalidIDTitle"), Lang("InvalidIDContent"), "", "", _configData.Color));
                return;
            }

            if (message.Mentions.Count > 0)
            {
                FindSteam(message.Mentions.Keys.First().Id.ToString(), message);
            }
            else if (args[0].StartsWith("7656119") && args[0].Length == 17)
            {
                FindDiscord(args[0], message);
            }
            else if (!args[0].StartsWith("7656119") && args[0].Length > 15)
            {
                FindSteam(args[0], message);
            }
            else
            {
                message.Reply(_client, CreateEmbed(Lang("InvalidIDTitle"), Lang("InvalidIDContent"), "", "", _configData.Color));
            }
        }
        void FindSteam(string discordID, DiscordMessage message)
        {
            string steamID;
            steamID = _link.GetSteamId((Snowflake)discordID);

            if (!string.IsNullOrEmpty(steamID) && steamID != "0")
            {
                message.Reply(_client, CreateEmbed(Lang("ResultTitle"), "", steamID, discordID, _configData.Color));
            }
            else
            {
                message.Reply(_client, CreateEmbed(Lang("ResultTitle"), Lang("NoResult"), "", "", _configData.Color));
            }
        }
        void FindDiscord(string steamID, DiscordMessage message)
        {
            string discordID;
            discordID = _link.GetDiscordId(steamID);
            if (!string.IsNullOrEmpty(discordID) && discordID != "0")
            {
                message.Reply(_client, CreateEmbed(Lang("ResultTitle"), "", steamID, discordID, _configData.Color));
            }
            else
            {
                message.Reply(_client, CreateEmbed(Lang("ResultTitle"), Lang("NoResult"), "", "", _configData.Color));
            }
        }
        #endregion

        #region Helper Methods
        private string Lang(string key, string id = null) => lang.GetMessage(key, this, id);
        private DiscordEmbed CreateEmbed(string title, string message, string steamID, string discordID, string color)
        {
            DiscordEmbedBuilder Embed = new DiscordEmbedBuilder();
            Embed.AddTitle(title);
            if (!string.IsNullOrEmpty(message))
            {
                Embed.AddDescription(message);
            }
            if (!string.IsNullOrEmpty(steamID))
            {
                Embed.AddField("SteamID", steamID, true);
            }
            if (!string.IsNullOrEmpty(discordID))
            {
                Embed.AddField("DiscordID", discordID, true);
            }
            Embed.AddColor(color);
            return Embed.Build();
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoLinkerPluginTitle"] = "Something went wrong..",
                ["NoLinkerPluginContent"] = "There is no linking system loaded at the moment.\nPlease install/load DiscordAuth or DiscordCore.",
                ["NotAllowedTitle"] = "Not allowed.",
                ["NotAllowedContent"] = "You are not allowed to use this command.",
                ["InvalidIDTitle"] = "The given ID is invalid.",
                ["InvalidIDContent"] = "Please use !search <steamID/discordID>",
                ["ResultTitle"] = "Linked Accounts",
                ["NoResult"] = "No users found.",
                ["Connected"] = "Discord bot connected.",
                ["NoToken"] = "Please set the discord bot token and reload the plugin to continue.",
                ["ConfigIssue"] = "Config file issue detected. Please delete file, or check syntax and fix.",
                ["NewConfig"] = "Creating new config file.",
                ["RoleNotFound"] = "The role with the given id can not be found. Please change the role in the config file."
            }, this, "en");
        }
        #endregion
    }
}