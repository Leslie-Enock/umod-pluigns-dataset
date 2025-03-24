using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
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
using Oxide.Ext.Discord.Entities.Permissions;
using Oxide.Ext.Discord.Entities.Voice;
using Oxide.Ext.Discord.Libraries.Linking;
using Oxide.Ext.Discord.Logging;

namespace Oxide.Plugins
{
    [Info("Discord Team", "Owned", "2.0.0")]
    [Description("Creates a private voice channel in Discord when creating a team in-game")]
    class DiscordTeam : CovalencePlugin
    {
      #region Plugin variables
      [DiscordClient] private DiscordClient _client;
      private DiscordRole role;
      private Hash<string, DiscordChannel> listTeamChannels = new Hash<string, DiscordChannel>();
      private confData config;
      private bool _init;

      private readonly DiscordSettings _settings = new DiscordSettings
      {
        Intents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildVoiceStates
      };

      private readonly DiscordLink _link = GetLibrary<DiscordLink>();
      
      private DiscordGuild _guild;
      #endregion
      protected override void LoadDefaultConfig()
      {
        Config.WriteObject(new confData(),true);
      }
      public class confData
      {
        [JsonProperty("Discord Bot Token")]
        public string Token = string.Empty;
        
        [JsonProperty(PropertyName = "Discord Server ID (Optional if bot only in 1 guild)")]
        public Snowflake GuildId { get; set; }

        [JsonProperty("Change channel when user create the team")]
        public bool moveLeader = true;

        [JsonProperty("Discord users can see other team's private vocal channel")]
        public bool seeOtherTeam = false;

        [JsonProperty("Using roles")]
        public bool roleUsage = false;

        [JsonProperty("Name of the player's role on discord (not @everyone)")]
        public string rolePlayer = "Player";

        [JsonProperty("Max players in a voice channel")]
        public int maxPlayersChannel = 3;
        
        [JsonConverter(typeof(StringEnumConverter))]
        [DefaultValue(DiscordLogLevel.Info)]
        [JsonProperty(PropertyName = "Discord Extension Log Level (Verbose, Debug, Info, Warning, Error, Exception, Off)")]
        public DiscordLogLevel ExtensionDebugging { get; set; } = DiscordLogLevel.Info;
      }

      protected override void LoadDefaultMessages()
      {
        lang.RegisterMessages(new Dictionary<string, string>
        {
          ["messageChannelCreated"] = "A private voice channel was created on Discord for your team !",
          ["messageChannelDeleted"] = "Your private voice channel on Discord has been deleted !",
          ["messageMemberJoin"] = "You have been added to your team's private voice channel on Discord !",
          ["messageMemberLeft"] = "You have been removed from your team's private voice channel on Discord !",
          ["channelName"] = "{0}'s Team"

        }, this);

        lang.RegisterMessages(new Dictionary<string, string>
        {
          ["messageChannelCreated"] = "Un salon vocal privé a été crée sur Discord pour votre équipe !",
          ["messageChannelDeleted"] = "Votre salon vocal privé sur Discord a été supprimé !",
          ["messageMemberJoin"] = "Vous avez été ajouté au salon vocal privé de votre équipe sur Discord !",
          ["messageMemberLeft"] = "Vous avez été retiré du salon vocal privé de votre équipe sur Discord !",
          ["channelName"] = "L'équipe de {0}"
        }, this, "fr");
      }

      string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);
      private void Init()
      {
        config = Config.ReadObject<confData>();
      }
      private void OnServerInitialized()
      {
        StartDiscordTeam();
      }

      private void StartDiscordTeam()
      {
        if (!string.IsNullOrEmpty(config.Token))
        {
          _settings.ApiToken = config.Token;
          _settings.LogLevel = config.ExtensionDebugging;
          _client.Connect(_settings);
        }
        else
        {
          PrintError("Discord Bot Token (API key) in the configuration file is missing");
        }
      }
      
      [HookMethod(DiscordHooks.OnDiscordGatewayReady)]
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
                
        if (!_client.Bot.Application.Flags.HasValue || !_client.Bot.Application.Flags.Value.HasFlag(ApplicationFlags.GatewayGuildMembersLimited))
        {
          PrintError($"You need to enable \"Server Members Intent\" for {_client.Bot.BotUser.Username} @ https://discord.com/developers/applications\n" +
                     $"{Name} will not function correctly until that is fixed. Once updated please reload {Name}.");
          return;
        }
            
        _guild = guild;

        if(config.roleUsage)
        {
          if(string.IsNullOrEmpty(config.rolePlayer))
          {
            PrintError("The role specified in the configuration file is missing");
            return;
          }
        }
        Puts("Discord Team initialized");
        _init = true;
        initializeTeam();
      }

      void Unload()
      {
        if(_init)
        {
          if(listTeamChannels.Count > 0)
          {
            foreach(var leaderGameId in listTeamChannels)
            {
              deleteChannel(listTeamChannels[leaderGameId.Key]);
            }
            listTeamChannels.Clear();
          }
        }
      }

      private void OnUserConnected(IPlayer player)
      {
        if(_init)
        {
          string info = GetDiscord(player.Id);
          if (info == null)
          {
            return;
          }
          else
          {
            BasePlayer basePlayer = player.Object as BasePlayer;
            var currentTeam = basePlayer.currentTeam;
            if(currentTeam != 0)
            {
              RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindTeam(currentTeam);
              string leaderId = team.teamLeader.ToString();
              if(listTeamChannels[leaderId] != null)
              {
                if(basePlayer.UserIDString != leaderId)
                {
                  addPlayerChannel(basePlayer, listTeamChannels[leaderId]);
                }
              }
              else
              {
                if(basePlayer.UserIDString == leaderId)
                {
                  CreateChannelGuild(basePlayer);
                }
              }
            }
          }
        }
      }

      object OnTeamCreate(BasePlayer player)
      {
        if(_init)
        {
          string info = GetDiscord(player.UserIDString);
          if(info != null)
          {
            CreateChannelGuild(player);
          }
        }
        return null;
      }

      object OnTeamPromote(RelationshipManager.PlayerTeam team, BasePlayer newLeader)
      {
        if(_init)
        {
          string info = GetDiscord(newLeader.UserIDString);
          if(info != null)
          {
            string newLeaderId = newLeader.UserIDString;
            string oldLeaderId = team.teamLeader.ToString();
            if(listTeamChannels[oldLeaderId] != null)
            {
              listTeamChannels[newLeaderId] = listTeamChannels[oldLeaderId];
              listTeamChannels.Remove(oldLeaderId);
              renameChannel(listTeamChannels[newLeaderId], newLeader);
            }
          }
        }
        return null;
      }

      object OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
      {
        if(_init)
        {
          string info = GetDiscord(player.UserIDString);
          if(info != null)
          {
            string leaderId = team.teamLeader.ToString();
            if(listTeamChannels[leaderId] != null)
            {
              if(leaderId != player.UserIDString)
              {
                removePlayerChannel(player, listTeamChannels[leaderId]);
              }
            }
          }
        }
        return null;
        //quit channel
      }

      object OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player)
      {
        if(_init)
        {
          string info = GetDiscord(player.UserIDString);
          if(info != null)
          {
            string leaderId = team.teamLeader.ToString();
            if(listTeamChannels[leaderId] != null)
            {
              removePlayerChannel(player, listTeamChannels[leaderId]);
            }
          }
        }
        return null;
      }

      object OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
      {
        if(_init)
        {
          string info = GetDiscord(player.UserIDString);
          if(info != null)
          {
            string leaderId = team.teamLeader.ToString();
            if(listTeamChannels[leaderId] != null)
            {
              addPlayerChannel(player, listTeamChannels[leaderId]);
            }
          }
        }
        return null;
      }

      void OnTeamDisbanded(RelationshipManager.PlayerTeam team)
      {
        if(_init)
        {
          string leaderId = team.teamLeader.ToString();
          var player = team.GetLeader();
          if(listTeamChannels[leaderId] != null)
          {
            deleteChannel(listTeamChannels[leaderId]);
            listTeamChannels.Remove(leaderId);
            if(player != null)
            {
              player.ChatMessage(GetMessage("messageChannelDeleted", leaderId));
            }
          }
        }
      }

      public void CreateChannelGuild(BasePlayer player)
      {
        Snowflake discordId = GetDiscord(player.UserIDString);
        if(discordId.IsValid())
        {
          string playerId = player.UserIDString;
          List<Overwrite> permissionList = new List<Overwrite>();
          DiscordRole rolePlayer;
          if(config.roleUsage)
          {
            rolePlayer = GetRoleByName(config.rolePlayer);
            DiscordRole roleEveryone = _guild.EveryoneRole;
            permissionList.Add(new Overwrite {Id = roleEveryone.Id,Type = PermissionType.Role ,Deny = (PermissionFlags)66061568});
          }
          else
          {
            rolePlayer = _guild.EveryoneRole;
          }
          if(config.seeOtherTeam)
          {
            permissionList.Add(new Overwrite {Id = rolePlayer.Id,Type = PermissionType.Role,Allow = (PermissionFlags)1024,Deny = (PermissionFlags)66060544});
          }
          else
          {
            permissionList.Add(new Overwrite {Id = rolePlayer.Id,Type = PermissionType.Role,Deny = (PermissionFlags)66060544});
          }
          
          permissionList.Add(new Overwrite {Id = discordId,Type =PermissionType.Member, Allow = (PermissionFlags)36701184});
          GuildMember guildMember = GetGuildMember(player.UserIDString);
          _guild.CreateGuildChannel(_client, new ChannelCreate
          {
            Name = string.Format(GetMessage("channelName", playerId), player.displayName),
            Type = ChannelType.GuildVoice,
            UserLimit = config.maxPlayersChannel,
            PermissionOverwrites = permissionList
            
          },
           channelCreated =>
          {
            listTeamChannels[playerId] = channelCreated;
            if(config.moveLeader)
            {
              _guild.ModifyGuildMember(_client, guildMember.Id, new GuildMemberUpdate
              {
                ChannelId = channelCreated.Id
              });
            }
            player.ChatMessage(GetMessage("messageChannelCreated", player.UserIDString));
          });
        }
      }

      public void addPlayerChannel(BasePlayer player, DiscordChannel channel)
      {
        Snowflake discordId = GetDiscord(player.UserIDString);
        if(discordId.IsValid())
        {
          channel.EditChannelPermissions(_client, discordId, (PermissionFlags)36701184, null, PermissionType.Member);
          player.ChatMessage(GetMessage("messageMemberJoin", player.UserIDString));
        }
      }

      public void removePlayerChannel(BasePlayer player, DiscordChannel channel)
      {
        Snowflake discordId = GetDiscord(player.UserIDString);
        if(discordId.IsValid())
        {
          string playerId = player.UserIDString;
          GuildMember guildMember = GetGuildMember(playerId);

          GuildMemberUpdate update = new GuildMemberUpdate();
          VoiceState playerVoice = _guild.VoiceStates[guildMember.Id];
          if (playerVoice?.ChannelId != null && playerVoice.ChannelId != channel.Id)
          {
            update.ChannelId = playerVoice.ChannelId;
          }
          
          _guild.ModifyGuildMember(_client, guildMember.Id, update);
          if(config.seeOtherTeam)
          {
            channel.EditChannelPermissions(_client, discordId, (PermissionFlags)1024, (PermissionFlags)66060544, PermissionType.Member);
          }
          else
          {
            channel.EditChannelPermissions(_client, discordId, null, (PermissionFlags)66060544, PermissionType.Member);
          }
          player.ChatMessage(GetMessage("messageMemberLeft", player.UserIDString));
        }
      }

      public void initializeTeam()
      {
        if(_init)
        {
          foreach (var player in BasePlayer.activePlayerList)
          {
            if(player.currentTeam != 0)
            {
              RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
              string leaderId = team.teamLeader.ToString();
              if(leaderId == player.UserIDString)
              {
                if(listTeamChannels[leaderId] != null)
                {
                  foreach (var teamMember in team.members)
                  {
                    if(teamMember.ToString() != leaderId)
                    {
                      string discordId = GetDiscord(player.UserIDString);
                      if(discordId != null)
                      {
                        BasePlayer member = RelationshipManager.FindByID(teamMember);
                        if(member != null)
                        {
                          addPlayerChannel(member, listTeamChannels[leaderId]);
                        }
                      }
                    }
                  }
                }
                else
                {
                  string discordId = GetDiscord(player.UserIDString);
                  if(discordId != null)
                  {
                    CreateChannelGuild(player);
                  }
                }
              }
            }
          }
        }
      }

      #region Discord functions
      public void deleteChannel(DiscordChannel channel)
      {
        channel.DeleteChannel(_client);
      }

      public void renameChannel(DiscordChannel channel, BasePlayer newLeader)
      {
        channel.ModifyGuildChannel(_client, new GuildChannelUpdate
        {
          Name = string.Format(GetMessage("channelName", newLeader.UserIDString), newLeader.displayName)
        });
      }

      public DiscordRole GetRoleByName(string roleName)
      {
        return _guild.GetRole(roleName);
      }

      private Snowflake GetDiscord(string steamId)
      {
        return _link.GetDiscordId(steamId) ?? default(Snowflake);
      }

      public GuildMember GetGuildMember(string steamId)
      {
        Snowflake discordId = GetDiscord(steamId);
        if(discordId.IsValid())
        {
          return _guild.Members[discordId];
        }
        return null;
      }
      #endregion
    }
}