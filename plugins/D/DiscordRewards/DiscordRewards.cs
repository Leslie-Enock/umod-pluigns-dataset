using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord;
using Oxide.Ext.Discord.Attributes;
using Oxide.Ext.Discord.Constants;
using Oxide.Ext.Discord.Entities;
using Oxide.Ext.Discord.Entities.Applications;
using Oxide.Ext.Discord.Entities.Channels;
using Oxide.Ext.Discord.Entities.Gatway;
using Oxide.Ext.Discord.Entities.Gatway.Events;
using Oxide.Ext.Discord.Entities.Guilds;
using Oxide.Ext.Discord.Entities.Messages;
using Oxide.Ext.Discord.Entities.Permissions;
using Oxide.Ext.Discord.Logging;

namespace Oxide.Plugins
{
    [Info("Discord Rewards", "birthdates", "1.4.0")]
    [Description("Get rewards for joining a discord!")]
    public class DiscordRewards : CovalencePlugin
    {
        #region Variables
        [DiscordClient] private DiscordClient Client;

        private readonly DiscordSettings _settings = new DiscordSettings
        {
            Intents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.DirectMessages
        };
        private DiscordRole role;
        private DiscordGuild _guild;
        private const string Perm = "discordrewards.use";
        private Data data;
        #endregion

        #region Hooks
        private void Init()
        {
            LoadConfig();
            data = Interface.Oxide.DataFileSystem.ReadObject<Data>("Discord Rewards");
            permission.RegisterPermission(Perm, this);
            AddCovalenceCommand(_config.command, "ChatCMD");
            if (!_config.wipeData)
            {
                Unsubscribe(nameof(OnNewSave));
            }
        }

        private void OnServerInitialized()
        {
            _settings.ApiToken = _config.botKey;
            _settings.LogLevel = _config.ExtensionDebugging;
            Client.Connect(_settings);
        }
        
        private void OnNewSave()
        {
            data = new Data();
            SaveData();
            PrintWarning("Wiped all verification data");
        }

        [HookMethod(DiscordExtHooks.OnDiscordGuildMembersLoaded)]
        private void OnDiscordGuildMembersLoaded()
        {
            role = _guild.GetRole(_config.role);
            if (role == null)
            {
                Snowflake roleId;
                if (Snowflake.TryParse(_config.role, out roleId))
                {
                    role = _guild.Roles[roleId];
                }
            }
                
            if (role == null)
            {
                PrintError($"ERROR: The \"{_config.role}\" role couldn't be found (try role ID instead)! Expect further errors!");
                return;
            }
            foreach (Snowflake id in data.verified2)
            {
                GuildMember member = _guild.Members[id];
                if (member == null || member.Roles.Contains(role.Id))
                {
                    continue;
                }
                _guild.AddGuildMemberRole(Client, member.User, role);
            }
        }

        private void ChatCMD(IPlayer player)
        {
            if (!permission.UserHasPermission(player.Id, Perm))
            {
                player.Message(lang.GetMessage("NoPermission", this, player.Id));
                return;
            }

            if (data.verified.Contains(player.Id))
            {
                player.Message(lang.GetMessage("AlreadyVerified", this, player.Id));
                return;
            }
            if (data.codes.ContainsValue(player.Id))
            {
                player.Message(string.Format(lang.GetMessage("YouAlreadyHaveACodeOut", this, player.Id), data.codes.First(x => x.Value == player.Id).Key));
                return;
            }
            var code = RandomString(_config.codeLength);
            data.codes.Add(code, player.Id);
            player.Message(string.Format(lang.GetMessage("Verify", this, player.Id), code));
        }
        private readonly System.Random random = new System.Random();

        private string RandomString(int length)
        {
            return new string(Enumerable.Repeat(_config.codeChars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        [HookMethod(DiscordExtHooks.OnDiscordDirectMessageCreated)]
        private void OnDiscordDirectMessageCreated(DiscordMessage message)
        {
            if (message.Author.Bot == true) return;
            DiscordChannel.GetChannel(Client, message.ChannelId, c =>
            {
                if (c.Type != ChannelType.Dm)
                    return;
                if (data.verified2.Contains(message.Author.Id))
                {
                    message.Reply(Client, lang.GetMessage("AlreadyVerified", this));
                    return;
                }
                if (!data.codes.ContainsKey(message.Content))
                {
                    message.Reply(Client, lang.GetMessage("NotAValidCode", this));
                    return;
                }
                var p = players.FindPlayer(data.codes[message.Content]);
                data.verified.Add(p.Id);
                data.verified2.Add(message.Author.Id);
                foreach (var s in _config.commands)
                {
                    server.Command(string.Format(s, p.Id));
                }
                message.Reply(Client, lang.GetMessage("Success", this));
                data.codes.Remove(message.Content);
                p.Message(lang.GetMessage("VerifiedInGame", this, p.Id));
                SaveData();
                if(role != null) _guild.AddGuildMemberRole(Client, message.Author, role);
            });
            
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
            if (ready.Guilds.Count == 1 && !_config.GuildId.IsValid())
            {
                guild = ready.Guilds.Values.FirstOrDefault();
            }

            if (guild == null)
            {
                guild = ready.Guilds[_config.GuildId];
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
            Puts($"Connected to bot: {Client.Bot.BotUser.Username}");
        }
        private void Unload()
        {
            SaveData();
        }

        #endregion

        #region API

        [HookMethod("IsAuthorized")]
        private bool IsAuthorized(string ID)
        {
            return data.verified.Contains(ID) || 
                   data.verified2.Contains(new Snowflake(ID));
        }

        [HookMethod("Deauthorize")]
        private bool Deauthorize(string ID)
        {
            return data.verified.Remove(ID) ||
                data.verified2.Remove(new Snowflake(ID));
        }

        #endregion

        #region Configuration & Language
        private ConfigFile _config;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NotAValidCode", "That is not a valid code!"},
                {"Success", "Success you are now verified, check for you rewards in game!"},
                {"AlreadyVerified", "You are already verified."},
                {"NoPermission", "You dont have permission to do this!"},
                {"YouAlreadyHaveACodeOut", "You already have a code out, it is {0}"},
                {"Verify", "Please message the bot on our discord with {0}"},
                {"VerifiedInGame", "Thank you for supporting the server, here are your rewards!"}
            }, this);
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Discord Rewards", data);
        }

        private class Data
        {
            public List<Snowflake> verified2 = new List<Snowflake>();
            public List<string> verified = new List<string>();
            public Dictionary<string, string> codes = new Dictionary<string, string>();
        }

        public class ConfigFile
        {
            [JsonProperty("Command")]
            public string command;
            [JsonProperty("Discord bot key (Look at documentation for how to get this)")]
            public string botKey;
            [JsonProperty(PropertyName = "Discord Server ID (Optional if bot only in 1 guild)")]
            public Snowflake GuildId { get; set; }
            [JsonProperty("Verification Role (role given when verified)")]
            public string role;
            [JsonProperty("Commands to execute when player is verified (use {0} for the player's steamid)")]
            public List<string> commands;
            [JsonProperty("Amount of characters in the code")]
            public int codeLength;
            [JsonProperty("Erase all verification data on wipe (new map save)?")]
            public bool wipeData;
            [JsonProperty("Characters used in the verification code")]
            public string codeChars;
            
            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(DiscordLogLevel.Info)]
            [JsonProperty(PropertyName = "Discord Extension Log Level (Verbose, Debug, Info, Warning, Error, Exception, Off)")]
            public DiscordLogLevel ExtensionDebugging { get; set; } = DiscordLogLevel.Info;
            
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    command = "verify",
                    botKey = "INSERT_BOT_KEY_HERE",
                    GuildId = default(Snowflake),
                    role = "enter_role_here",
                    commands = new List<string>
                    {
                        "inventory.giveto {0} stones 1000"
                    },
                    codeLength = 6,
                    wipeData = false,
                    codeChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz",
                    ExtensionDebugging = DiscordLogLevel.Info
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null)
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
        #endregion
    }
}
//Generated with birthdates' Plugin Maker