using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord.Clients;
using Oxide.Ext.Discord.Connections;
using Oxide.Ext.Discord.Constants;
using Oxide.Ext.Discord.Entities;
using Oxide.Ext.Discord.Interfaces;
using Oxide.Ext.Discord.Libraries;
using Oxide.Ext.Discord.Logging;

namespace Oxide.Plugins
{
    [Info("Discord Commands", "MJSU", "2.1.1")]
    [Description("Allows using discord to execute commands")]
    internal class DiscordCommands : CovalencePlugin, IDiscordPlugin
    {
        #region Class Fields
        public DiscordClient Client { get; set; }

        private PluginConfig _pluginConfig; //Plugin Config

        private const string UsePermission = "discordcommands.use";

        private readonly DiscordCommand _dcCommands = Interface.Oxide.GetLibrary<DiscordCommand>();

        private readonly BotConnection _discordSettings = new BotConnection
        {
            Intents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.MessageContent
        };

        private readonly Hash<Snowflake, StringBuilder> _playerLogs = new();
        private DiscordGuild _guild;

        private bool _logActive;
        
        private enum RestrictionMode {Blacklist, Whitelist}
        #endregion

        #region Setup & Loading
        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
            
            _discordSettings.ApiToken = _pluginConfig.DiscordApiKey;
            _discordSettings.LogLevel = _pluginConfig.ExtensionDebugging;
            
            if (_pluginConfig.CommandSettings.AllowInDm)
            {
                _discordSettings.Intents |= GatewayIntents.DirectMessages;
            }
            
            if (_pluginConfig.CommandSettings.AllowInGuild)
            {
                _discordSettings.Intents |= GatewayIntents.GuildMessages;
            }
            
            if (_pluginConfig.CommandSettings.Restrictions.EnableRestrictions)
            {
                foreach (string command in _pluginConfig.CommandSettings.Restrictions.Restrictions.Keys.ToList())
                {
                    _pluginConfig.CommandSettings.Restrictions.Restrictions[command.ToLower()] = _pluginConfig.CommandSettings.Restrictions.Restrictions[command];
                }
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.NoPermission] = "You do not have permission to use this command",
                [LangKeys.Blacklisted] = "This command is blacklisted and you do not have permission to use it.",
                [LangKeys.WhiteListedNotAdded] = "This command is not added to the command whitelist and cannot be used.",
                [LangKeys.WhiteListedNoPermission] = "You do not have the whitelisted permission to use this command.",
                [LangKeys.CommandInfoText] = "To execute a command on the server",
                [LangKeys.RanCommand] = "Ran Command: {0}",
                [LangKeys.ExecCommand] = "exec",
                [LangKeys.CommandLogging] = "{0} ran command '{1}'",
                [LangKeys.CommandHelpText] = "Send commands to the rust server:\n" +
                                             "Type /{0} {{command}} - to execute that command on the server\n" +
                                             "Example: /{0} o.reload DiscordCommand"
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            config.CommandSettings = new CommandSettings(config.CommandSettings);
            config.LogSettings = new LogSettings(config.LogSettings);
            return config;
        }

        private void OnServerInitialized()
        {
            if (string.IsNullOrEmpty(_pluginConfig.DiscordApiKey))
            {
                PrintWarning("Please set the Discord Bot Token and reload the plugin");
                return;
            }
            
            RegisterDiscordLangCommand(nameof(ExecCommand), LangKeys.ExecCommand, _pluginConfig.CommandSettings.AllowInDm, _pluginConfig.CommandSettings.AllowInGuild, _pluginConfig.CommandSettings.AllowedChannels);
            Client.Connect(_discordSettings);
        }

        private void Unload()
        {
            UnityEngine.Application.logMessageReceived -= HandleCommandLog;
        }
        
        [HookMethod(DiscordExtHooks.OnDiscordGatewayReady)]
        private void OnDiscordGatewayReady(GatewayReadyEvent ready)
        {
            _guild = null;
            if (ready.Guilds.Count == 1 && !_pluginConfig.GuildId.IsValid())
            {
                _guild = ready.Guilds.Values.FirstOrDefault();
            }

            if (_guild == null)
            {
                _guild = ready.Guilds[_pluginConfig.GuildId];
                if (_guild == null)
                {
                    PrintError("Failed to find a matching guild for the Discord Server Id. " +
                               "Please make sure your guild Id is correct and the bot is in the discord server.");
                    return;
                }
            }
            
            ApplicationFlags appFlags = Client.Bot.Application.Flags ?? ApplicationFlags.None;
            if (!appFlags.HasFlag(ApplicationFlags.GatewayMessageContentLimited) && !appFlags.HasFlag(ApplicationFlags.GatewayMessageContent))
            {
                PrintWarning($"You will need to enable \"Message Content Intent\" for {Client.Bot.BotUser.Username} @ https://discord.com/developers/applications\n by April 2022." +
                             $" {Name} will stop function correctly after that date until that is fixed. Once updated please reload {Name}.");
            }
            
            Puts("Discord Commands Ready");
        }
        #endregion

        #region Discord Chat Command
        private void ExecCommand(DiscordMessage message, string cmd, string[] args)
        {
            IPlayer player = message.Author.Player;
            GuildMember member = _guild.Members[message.Author.Id];
            if (!HasCommandPermissions(message))
            {
                message.Reply(Client, Lang(LangKeys.NoPermission, player));
                return;
            }

            if (args.Length == 0)
            {
                message.Reply(Client, Lang(LangKeys.CommandHelpText, player, Lang(LangKeys.ExecCommand, player)));
                return;
            }
            
            string command = args[0];
            string[] commandArgs = args.Skip(1).ToArray();
            string commandString = string.Join(" ", args);

            if (!CanRunCommand(message, command, player, member))
            {
                return;
            }
            
            RunCommand(message, command, commandArgs, player, member, commandString);
        }
        
        public bool HasCommandPermissions(DiscordMessage message)
        {
            IPlayer player = message.Author.Player;
            if (player != null && player.HasPermission(UsePermission))
            {
                return true;
            }

            if (message.Member != null)
            {
                foreach (Snowflake role in _pluginConfig.CommandSettings.AllowedRoles)
                {
                    if (message.Member.Roles.Contains(role))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool CanRunCommand(DiscordMessage message, string command, IPlayer player, GuildMember member)
        {
            CommandRestrictions restrictions = _pluginConfig.CommandSettings.Restrictions;
            if (restrictions.EnableRestrictions)
            {
                if (restrictions.RestrictionMode == RestrictionMode.Blacklist && restrictions.Restrictions.ContainsKey(command.ToLower()))
                {
                    RestrictionSettings restriction = restrictions.Restrictions[command.ToLower()];
                    if (!IsPlayerAllowed(player, member, restriction))
                    {
                        message.Reply(Client, Lang(LangKeys.Blacklisted, player));
                        return false;
                    }
                }
                else if (restrictions.RestrictionMode == RestrictionMode.Whitelist)
                {
                    if (!restrictions.Restrictions.ContainsKey(command.ToLower()))
                    {
                        message.Reply(Client, Lang(LangKeys.WhiteListedNotAdded, player));
                        return false;
                    }

                    RestrictionSettings restriction = restrictions.Restrictions[command.ToLower()];
                    if (!IsPlayerAllowed(player, member, restriction))
                    {
                        message.Reply(Client, Lang(LangKeys.WhiteListedNoPermission, player));
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsPlayerAllowed(IPlayer player, GuildMember member, RestrictionSettings restriction)
        {
            bool allowed = false;
            if (player != null)
            {
                foreach (string group in restriction.AllowedGroups)
                {
                    if (player.BelongsToGroup(group))
                    {
                        allowed = true;
                        break;
                    }
                }
            }

            if (!allowed && member != null)
            {
                foreach (Snowflake roleId in member.Roles)
                {
                    if (restriction.AllowedRoles.Contains(roleId))
                    {
                        allowed = true;
                        break;
                    }
                }
            }

            return allowed;
        }

        private void RunCommand(DiscordMessage message, string command, string[] commandArgs, IPlayer player, GuildMember member, string commandString)
        {
            if (_pluginConfig.LogSettings.DisplayServerLog)
            {
                if (!_logActive)
                {
                    UnityEngine.Application.logMessageReceived += HandleCommandLog;
                    _logActive = true;
                }

                _playerLogs[message.Id] = new StringBuilder();

                timer.In(_pluginConfig.LogSettings.DisplayServerLogDuration, () =>
                {
                    StringBuilder sb = _playerLogs[message.Id];
                    //Message content length is 2k characters
                    if (sb.Length > 2000)
                    {
                        sb.Length = 2000;
                    }
                    message.Reply(Client, sb.ToString());
                    _playerLogs.Remove(message.Id);

                    if (_playerLogs.Count == 0)
                    {
                        UnityEngine.Application.logMessageReceived -= HandleCommandLog;
                        _logActive = false;
                    }
                });
            }

            server.Command(command, commandArgs);
            message.Reply(Client, Lang(LangKeys.RanCommand, player, commandString));

            string log = Lang(LangKeys.CommandLogging, player, player?.Name ?? member?.Nickname ?? $"{member?.User.Username}#{member?.User.Discriminator}", commandString);

            if (_pluginConfig.LogSettings.LoggingChannel.IsValid())
            {
                DiscordMessage.Create(Client, _pluginConfig.LogSettings.LoggingChannel, log);
            }

            if (_pluginConfig.LogSettings.LogToConsole)
            {
                Puts(log);
            }
        }

        #endregion

        #region Log Handler
        private void HandleCommandLog(string message, string stackTrace, UnityEngine.LogType type)
        {
            foreach (StringBuilder sb in _playerLogs.Values)
            {
                sb.AppendLine(message);
            }
        }
        #endregion

        #region Helper Methods
        public void RegisterDiscordLangCommand(string command, string langKey, bool direct, bool guild, List<Snowflake> allowedChannels)
        {
            if (direct)
            {
                _dcCommands.AddDirectMessageLocalizedCommand(langKey, this, command);
            }

            if (guild)
            {
                _dcCommands.AddGuildLocalizedCommand(langKey, this, allowedChannels, command);
            }
        }

        public string Lang(string key, IPlayer player = null)
        {
            return lang.GetMessage(key, this, player?.Id);
        }
        
        public string Lang(string key, IPlayer player = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, player?.Id), args);
            }
            catch (Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception\n:{ex}");
                throw;
            }
        }
        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue("")]
            [JsonProperty(PropertyName = "Discord Bot Token")]
            public string DiscordApiKey { get; set; }
            
            [JsonProperty(PropertyName = "Discord Server ID (Optional if bot only in 1 guild)")]
            public Snowflake GuildId { get; set; }

            [JsonProperty(PropertyName = "Command Settings")]
            public CommandSettings CommandSettings { get; set; }

            [JsonProperty(PropertyName = "Log Settings")]
            public LogSettings LogSettings { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(DiscordLogLevel.Info)]
            [JsonProperty(PropertyName = "Discord Extension Log Level (Verbose, Debug, Info, Warning, Error, Exception, Off)")]
            public DiscordLogLevel ExtensionDebugging { get; set; }
        }
        
        private class LogSettings 
        {
            [JsonProperty(PropertyName = "Log command usage in server console")]
            public bool LogToConsole { get; set; }
            
            [JsonProperty(PropertyName = "Command Usage Logging Channel ID")]
            public Snowflake LoggingChannel { get; set; }

            [JsonProperty(PropertyName = "Display Server Log Messages to user after running command")]
            public bool DisplayServerLog { get; set; }
            
            [JsonProperty(PropertyName = "Display Server Log Messages Duration (Seconds)")]
            public float DisplayServerLogDuration { get; set; }

            public LogSettings(LogSettings settings)
            {
                LogToConsole = settings?.LogToConsole ?? true;
                LoggingChannel = settings?.LoggingChannel ?? default(Snowflake);
                DisplayServerLog = settings?.DisplayServerLog ?? true;
                DisplayServerLogDuration = settings?.DisplayServerLogDuration ?? 1f;
            }
        }

        private class CommandSettings
        {
            [JsonProperty(PropertyName = "Allow Discord Commands In Direct Messages")]
            public bool AllowInDm { get; set; }
            
            [JsonProperty(PropertyName = "Allow Discord Commands In Guild")]
            public bool AllowInGuild { get; set; }

            [JsonProperty(PropertyName = "Allow Guild Commands Only In The Following Guild Channel Or Category (Channel ID Or Category ID)")]
            public List<Snowflake> AllowedChannels { get; set; }

            [JsonProperty(PropertyName = "Allow Commands for members having role (Role ID)")]
            public List<Snowflake> AllowedRoles { get; set; }

            public CommandRestrictions Restrictions { get; set; }

            public CommandSettings(CommandSettings settings)
            {
                AllowInDm = settings?.AllowInDm ?? true;
                AllowInGuild = settings?.AllowInGuild ?? false;
                AllowedChannels = settings?.AllowedChannels ?? new List<Snowflake>();
                AllowedRoles = settings?.AllowedRoles ??  new List<Snowflake>();
                Restrictions = new CommandRestrictions(settings?.Restrictions);
            }
        }

        private class CommandRestrictions
        {
            [JsonProperty(PropertyName = "Enable Command Restrictions")]
            public bool EnableRestrictions { get; set; }

            [JsonProperty(PropertyName = "Blacklist = listed commands cannot be used without permission, Whitelist = Cannot use any commands unless listed and have permission")]
            [JsonConverter(typeof(StringEnumConverter))]
            public RestrictionMode RestrictionMode { get; set; }

            [JsonProperty(PropertyName = "Command Restrictions")]
            public Hash<string, RestrictionSettings> Restrictions { get; set; }

            public CommandRestrictions(CommandRestrictions settings)
            {
                EnableRestrictions = settings?.EnableRestrictions ?? false;
                RestrictionMode = settings?.RestrictionMode ?? RestrictionMode.Blacklist;
                Restrictions = settings?.Restrictions ?? new Hash<string, RestrictionSettings>
                {
                    ["command"] = new RestrictionSettings
                    {
                        AllowedGroups = new List<string> { "admin" },
                        AllowedRoles = new List<Snowflake> { new Snowflake(1234512321) }
                    }
                };
            }
        }

        private class RestrictionSettings
        {
            [JsonProperty(PropertyName = "Allowed Discord Roles")]
            public List<Snowflake> AllowedRoles { get; set; }

            [JsonProperty(PropertyName = "Allowed Server Groups")]
            public List<string> AllowedGroups { get; set; }
        }

        private static class LangKeys
        {
            public const string CommandInfoText = nameof(CommandInfoText);
            public const string CommandHelpText = nameof(CommandHelpText) + "V2";
            public const string RanCommand = nameof(RanCommand);
            public const string CommandLogging = nameof(CommandLogging);
            public const string ExecCommand = nameof(ExecCommand);
            public const string NoPermission = nameof(NoPermission);
            public const string Blacklisted = nameof(Blacklisted);
            public const string WhiteListedNotAdded = nameof(WhiteListedNotAdded);
            public const string WhiteListedNoPermission = nameof(WhiteListedNoPermission);
        }
        #endregion
    }
}