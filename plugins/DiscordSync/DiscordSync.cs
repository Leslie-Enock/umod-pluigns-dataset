using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord.Clients;
using Oxide.Ext.Discord.Connections;
using Oxide.Ext.Discord.Constants;
using Oxide.Ext.Discord.Entities;
using Oxide.Ext.Discord.Extensions;
using Oxide.Ext.Discord.Interfaces;
using Oxide.Ext.Discord.Libraries;
using Oxide.Ext.Discord.Logging;

namespace Oxide.Plugins
{
    [Info("Discord Sync", "Tricky & OuTSMoKE", "1.3.0")]
    [Description("Integrates players with the discord server")]

    public class DiscordSync : CovalencePlugin, IDiscordPlugin
    {
        #region Declared
        public DiscordClient Client { get; set; }
        
        private readonly BotConnection _settings = new BotConnection
        {
            Intents = GatewayIntents.Guilds | GatewayIntents.GuildMembers
        };
        
        private DiscordGuild _guild;

        private readonly DiscordLink _link = GetLibrary<DiscordLink>();
        #endregion

        #region Config
        Configuration config;

        class Configuration
        {
            [JsonProperty(PropertyName = "Discord Bot Token")]
            public string BotToken = string.Empty;

            [JsonProperty(PropertyName = "Discord Server ID (Optional if bot only in 1 guild)")]
            public Snowflake GuildId { get; set; } = new Snowflake();

            [JsonProperty(PropertyName = "Enable Nick Syncing")]
            public bool NickSync = false;

            [JsonProperty(PropertyName = "Enable Ban Syncing")]
            public bool BanSync = false;

            [JsonProperty(PropertyName = "Enable Role Syncing")]
            public bool RoleSync = true;

            // [JsonProperty(PropertyName = "Auto Reload Plugin")]
            // public bool AutoReloadPlugin { get; set; }

            // [JsonProperty(PropertyName = "Auto Reload Time (Seconds, Minimum 60)")]
            // public int AutoReloadTime { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(DiscordLogLevel.Info)]
            [JsonProperty(PropertyName = "Discord Extension Log Level (Verbose, Debug, Info, Warning, Error, Exception, Off)")]
            public DiscordLogLevel ExtensionDebugging { get; set; } = DiscordLogLevel.Info;

            [JsonProperty(PropertyName = "Role Setup", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<RoleInfo> RoleSetup = new List<RoleInfo>
            {
                new RoleInfo
                {
                    OxideGroup = "default",
                    DiscordRole = "Member"
                },

                new RoleInfo
                {
                    OxideGroup = "vip",
                    DiscordRole = "Donator"
                }
            };

            public class RoleInfo
            {
                [JsonProperty(PropertyName = "Oxide Group")]
                public string OxideGroup;

                [JsonProperty(PropertyName = "Discord Role")]
                public string DiscordRole;
            }
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
                Config.WriteObject(config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Oxide / Discord Hooks
        [HookMethod(DiscordExtHooks.OnDiscordClientCreated)]
        private void OnDiscordClientCreated()
        {
            if (config.BotToken != string.Empty)
            {
                _settings.ApiToken = config.BotToken;
                _settings.LogLevel = config.ExtensionDebugging;
                Client.Connect(_settings);
            }
            else
            {
                PrintWarning("Please set the Discord Bot Token and reload the plugin");
            }
        }

        void OnUserConnected(IPlayer player)
        {
            if (config.NickSync)
                HandleNick(player);
            if (config.BanSync)
                HandleBan(player);
            if (config.RoleSync)
                HandleRole(player);
        }

        private void OnServerInitialized()
        {
            if (!config.RoleSync)
                Unsubscribe(nameof(OnUserGroupAdded));

            if (!config.RoleSync)
                Unsubscribe(nameof(OnUserGroupRemoved));

            if (!config.BanSync)
                Unsubscribe(nameof(OnUserBanned));

            if (!config.BanSync)
                Unsubscribe(nameof(OnUserUnbanned));

            // var reloadtime = config.AutoReloadTime;
            // if (config.AutoReloadPlugin && config.AutoReloadTime > 59)
            // {
            //     timer.Every(reloadtime, () => Reload());
            // }
        }
        
        // Called when the client is created, and the plugin can use it
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
            Puts($"Connected to bot: {Client.Bot.BotUser.Username}");
        }

        [HookMethod(DiscordExtHooks.OnDiscordPlayerLinked)]
        private void OnAuthenticate(IPlayer player, DiscordUser user)
        {
            if (config.NickSync)
                HandleNick(player);

            if (config.BanSync)
                HandleBan(player);

            if (config.RoleSync)
                HandleRole(player);
        }

        private void OnUserNameUpdated(string id)
        {
            var player = players.FindPlayerById(id);
            if (player == null)
                return;

            HandleNick(player);
        }

        private void OnUserBanned(string name, string id) => OnUserUnbanned(name, id);

        private void OnUserUnbanned(string name, string id)
        {
            var player = players.FindPlayerById(id);
            if (player == null)
                return;

            HandleBan(player);
        }

        private void OnUserGroupAdded(string id, string groupName) => OnUserGroupRemoved(id, groupName);

        private void OnUserGroupRemoved(string id, string groupName)
        {
            config.RoleSetup.ForEach(roleSetup =>
            {
                if (roleSetup.OxideGroup == groupName)
                    HandleRole(id, roleSetup.DiscordRole, groupName);
            });
        }
        #endregion

        #region Handle
        private void HandleNick(IPlayer player)
        {
            var discordId = player.GetDiscordUserId();
            if (!discordId.IsValid())
                return;

            var guildmember = GetGuildMember(discordId);
            if (guildmember == null)
                return;

            if (guildmember.Nickname == player.Name)
                return;

            _guild.EditMemberNick(Client, discordId, player.Name);
        }

        private void HandleBan(IPlayer player)
        {
            Snowflake discordId = player.GetDiscordUserId();
            if (!discordId.IsValid())
                return;

            if (GetGuildMember(discordId) == null)
                return;

            _guild.GetBans(Client).Then(bans =>
            {
                if ((bans.Any(ban => ban.User.Id != discordId) || bans.Count() == 0) && player.IsBanned)
                {
                    _guild.CreateBan(Client, discordId, new GuildBanCreate
                    {
                        DeleteMessageSeconds = 0
                    });
                }
                else if (bans.Any(ban => ban.User.Id == discordId) && !player.IsBanned)
                {
                    _guild.RemoveBan(Client, discordId);
                }
            });
        }

        private void HandleRole(string id, string roleName, string oxideGroup)
        {
            Snowflake discordId = _link.GetDiscordId(id);
            if (!discordId.IsValid())
                return;

            var guildmember = GetGuildMember(discordId);
            if (guildmember == null)
                return;

            var role = GetRoleByName(roleName);
            if (role == null)
            {
                Puts($"Unable to find '{roleName}' discord role!");
                return;
            }

            if (HasGroup(id, oxideGroup) && !UserHasRole(discordId, role.Id))
            {
                _guild.AddMemberRole(Client, guildmember.User, role);
            }
            else if (!HasGroup(id, oxideGroup) && UserHasRole(discordId, role.Id))
            {
                _guild.RemoveMemberRole(Client, guildmember.User, role);
            }
        }

        private void HandleRole(IPlayer player)
        {
            config.RoleSetup.ForEach(roleSetup =>
            {
                GetGroups(player.Id).ToList().ForEach(playerGroup =>
                {
                    if (roleSetup.OxideGroup == playerGroup)
                        HandleRole(player.Id, roleSetup.DiscordRole, playerGroup);
                });
            });
        }
        #endregion

        #region Helpers
        private bool HasGroup(string id, string groupName)
            => permission.UserHasGroup(id, groupName);

        private string[] GetGroups(string id)
            => permission.GetUserGroups(id);

        private DiscordRole GetRoleByName(string roleName) => _guild.GetRole(roleName);

        private GuildMember GetGuildMember(Snowflake discordId) => _guild.Members[discordId];

        private bool UserHasRole(Snowflake discordId, Snowflake roleId)
        {
            return GetGuildMember(discordId).HasRole(roleId);
        }
        #endregion
    }
}