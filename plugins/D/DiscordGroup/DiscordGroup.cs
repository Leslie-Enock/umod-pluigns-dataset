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
using Oxide.Ext.Discord.Extensions;
using Oxide.Ext.Discord.Interfaces;
using Oxide.Ext.Discord.Libraries;
using Oxide.Ext.Discord.Logging;

namespace Oxide.Plugins
{
    [Info("Discord Group", "MJSU", "2.1.0")]
    [Description("Grants players rewards for linking their game and discord accounts")]
    internal class DiscordGroup : CovalencePlugin, IDiscordPlugin
    {
        #region Class Fields
        public DiscordClient Client { get; set; }

        private PluginConfig _pluginConfig;
        private StoredData _storedData;
        private DiscordRole _role;
        private DiscordGuild _guild;
        
        private readonly Queue<SyncData> _processQueue = new();
        
        private readonly DiscordLink _link = Interface.Oxide.GetLibrary<DiscordLink>();

        private Action _processNext;
        private bool _initialized;
        #endregion

        #region Setup & Loading
        private void Init()
        {
            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            Unsubscribe(nameof(OnUserConnected));
            _processNext = ProcessNext;
        }
        
        private void OnServerInitialized()
        {
            if (string.IsNullOrEmpty(_pluginConfig.DiscordApiKey))
            {
                PrintWarning("Please set the Discord Bot Token and reload the plugin");
                return;
            }
            
            Client.Connect(new BotConnection
            {
                Intents = GatewayIntents.Guilds | GatewayIntents.GuildMembers,
                ApiToken = _pluginConfig.DiscordApiKey,
                LogLevel = _pluginConfig.ExtensionDebugging
            });
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
            config.Commands ??= new List<string>
            {
                "inventory.giveto {steamid} wood 100",
            };
            return config;
        }
        
        private void OnNewSave(string filename)
        {
            if (_pluginConfig.ResetRewardsOnWipe)
            {
                _storedData = new StoredData();
                SaveData();
            }
        }

        private void Unload()
        {
            SaveData();
        }
        #endregion

        #region Discord Hooks
        [HookMethod(DiscordExtHooks.OnDiscordGatewayReady)]
        private void OnDiscordGatewayReady(GatewayReadyEvent ready)
        {
            if (ready.Guilds.Count == 0)
            {
                PrintError("Your bot was not found in any discord servers. Please invite it to a server and reload the plugin.");
                return;
            }

            _guild = null;
            if (ready.Guilds.Count == 1 && !_pluginConfig.GuildId.IsValid())
            {
                _guild = ready.Guilds.Values.FirstOrDefault();
            }

            _guild ??= ready.Guilds[_pluginConfig.GuildId];
            if (_guild == null)
            {
                PrintError("Failed to find a matching guild for the Discord Server Id. " +
                           "Please make sure your guild Id is correct and the bot is in the discord server.");
            }
        }

        [HookMethod(DiscordExtHooks.OnDiscordBotFullyLoaded)]
        private void OnDiscordBotFullyLoaded()
        {
            if (_pluginConfig.DiscordRole.IsValid())
            {
                _role = _guild.Roles[_pluginConfig.DiscordRole];
                if (_role == null)
                {
                    PrintWarning($"Discord Role '{_pluginConfig.DiscordRole}' does not exist. Please set the role name or id in the config.");
                    return;
                }
            }
            
            if (!string.IsNullOrEmpty(_pluginConfig.OxideGroup) && !permission.GroupExists(_pluginConfig.OxideGroup))
            {
                PrintWarning($"Oxide group '{_pluginConfig.OxideGroup}' does not exist. Please add the oxide group or set the correct group in the config.");
                return;
            }
            
            if (!_initialized)
            {
                foreach (KeyValuePair<PlayerId, Snowflake> link in _link.PlayerToDiscordIds)
                {
                    IPlayer player = link.Key.Player;
                    if (player.IsDummyPlayer())
                    {
                       continue;
                    }
                    
                    DiscordUser user = _link.GetDiscordUser(link.Key);
                    _processQueue.Enqueue(new SyncData(player, user, SyncAction.Link));
                }
            
                Subscribe(nameof(OnUserConnected));
                timer.In(1f, ProcessNext);
                Puts($"{Title} Ready");
                _initialized = true;
            }
        }
        
        private void OnUserConnected(IPlayer player)
        {
            DiscordUser user = player.GetDiscordUser();
            if (user == null)
            {
                return;
            }

            if (_guild.Members.ContainsKey(user.Id))
            {
                HandlePlayerLinked(player, user);
            }
        }

        [HookMethod(DiscordExtHooks.OnDiscordPlayerLinked)]
        private void OnDiscordPlayerLinked(IPlayer player, DiscordUser user)
        {
            if (_processQueue.Count > 2)
            {
                _processQueue.Enqueue(new SyncData(player, user, SyncAction.Link));
            }
            else
            {
                HandlePlayerLinked(player, user);
            }
        }

        [HookMethod(DiscordExtHooks.OnDiscordPlayerUnlinked)]
        private void OnDiscordPlayerUnlinked(IPlayer player, DiscordUser user)
        {
            if (_processQueue.Count > 2)
            {
                _processQueue.Enqueue(new SyncData(player, user, SyncAction.Unlink));
            }
            else
            {
                HandlePlayerUnlinked(player, user);
            }
        }
        #endregion

        #region Helpers
        public void ProcessNext()
        {
            if (_processQueue.Count == 0)
            {
                return;
            }

            SyncData link = _processQueue.Dequeue();
            
            try
            {
                switch (link.Action)
                {
                    case SyncAction.Link:
                        HandlePlayerLinked(link.Player, link.User);
                        break;
                    case SyncAction.Unlink:
                        HandlePlayerUnlinked(link.Player, link.User);
                        break;
                }
            }
            catch (Exception ex)
            {
                PrintError($"An error occured performing sync for: {link.Player?.Name}({link.Player?.Id}) User: {link.User?.GlobalName}({link.User?.Id}) Action: {link.Action}\n{ex}");
            }

            timer.In(_pluginConfig.UpdateRate, _processNext);
        }
        
        private void HandlePlayerLinked(IPlayer player, DiscordUser user)
        {
            if (!string.IsNullOrEmpty(_pluginConfig.OxideGroup))
            {
                AddToOxideGroup(player);
            }

            if (_role != null)
            {
                AddToDiscordRole(player, user);
            }

            if (_pluginConfig.RunCommands && player.IsConnected)
            {
                RunCommands(player);
            }
        }

        private void AddToOxideGroup(IPlayer player)
        {
            if (!permission.UserHasGroup(player.Id, _pluginConfig.OxideGroup))
            {
                Puts($"Adding player {player.Name}({player.Id}) to oxide group {_pluginConfig.OxideGroup}");
                permission.AddUserGroup(player.Id, _pluginConfig.OxideGroup);
            }
        }

        private void AddToDiscordRole(IPlayer player, DiscordUser user)
        {
            if (user == null || !user.Id.IsValid())
            {
                return;
            }

            _guild.GetMember(Client, user.Id).Then(member =>
            {
                if (_role != null && !member.Roles.Contains(_role.Id))
                {
                    _guild.AddMemberRole(Client, user.Id, _role.Id);
                    Puts($"Adding player {player.Name}({player.Id}) to discord role {_role.Name}");
                }
            }).Catch<ResponseError>(e => e.SuppressErrorMessage());
        }

        private void HandlePlayerUnlinked(IPlayer player, DiscordUser user)
        {
            if (!string.IsNullOrEmpty(_pluginConfig.OxideGroup))
            {
                RemoveFromOxide(player);
            }

            if (_role != null)
            {
                RemoveFromDiscord(player, user);
            }
        }

        private void RemoveFromOxide(IPlayer player)
        {
            Puts($"Removing player {player.Name}({player.Id}) from oxide group {_pluginConfig.OxideGroup}");
            permission.RemoveUserGroup(player.Id, _pluginConfig.OxideGroup);
        }

        private void RemoveFromDiscord(IPlayer player, DiscordUser user)
        {
            if (user == null || !user.Id.IsValid())
            {
                return;
            }

            if (_guild.Members.ContainsKey(user.Id))
            {
                _guild.RemoveMemberRole(Client, user.Id, _role.Id);
                Puts($"Removing player {player.Name}({player.Id}) from discord role {_role.Name}");
            }
        }

        private void RunCommands(IPlayer player)
        {
            if (_storedData.RewardedPlayers.Contains(player.Id))
            {
                return;
            }

            foreach (string command in _pluginConfig.Commands)
            {
                string execCommand = command.Replace("{steamid}", player.Id)
                    .Replace("{name}", player.Name);
                
                server.Command(execCommand);
            }

            _storedData.RewardedPlayers.Add(player.Id);
            NextTick(SaveData);
        }
        
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);
        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue("")]
            [JsonProperty(PropertyName = "Discord Bot Token")]
            public string DiscordApiKey { get; set; }
            
            [JsonProperty(PropertyName = "Discord Server ID (Optional if bot only in 1 guild)")]
            public Snowflake GuildId { get; set; }
            
            [JsonProperty("Add To Discord Role (Role ID)")]
            public Snowflake DiscordRole { get; set; }
            
            [DefaultValue("")]
            [JsonProperty("Add To Server Group")]
            public string OxideGroup { get; set; }
            
            [DefaultValue(2f)]
            [JsonProperty("Update Rate (Seconds)")]
            public float UpdateRate { get; set; }
            
            [DefaultValue(false)]
            [JsonProperty("Run Commands On Link")]
            public bool RunCommands { get; set; }
            
            [JsonProperty("Commands To Run")]
            public List<string> Commands { get; set; }

            [DefaultValue(false)]
            [JsonProperty("Reset Rewards On Wipe")]
            public bool ResetRewardsOnWipe { get; set; }
            
            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(DiscordLogLevel.Info)]
            [JsonProperty(PropertyName = "Discord Extension Log Level (Verbose, Debug, Info, Warning, Error, Exception, Off)")]
            public DiscordLogLevel ExtensionDebugging { get; set; }
        }

        private class StoredData
        {
            public HashSet<string> RewardedPlayers = new HashSet<string>();
        }
        
        public readonly struct SyncData
        {
            public readonly IPlayer Player;
            public readonly DiscordUser User;
            public readonly SyncAction Action;

            public SyncData(IPlayer player, DiscordUser user, SyncAction action)
            {
                Player = player;
                User = user;
                Action = action;
            }
        }

        public enum SyncAction : byte
        {
            Link,
            Unlink
        }
        #endregion
    }
}
