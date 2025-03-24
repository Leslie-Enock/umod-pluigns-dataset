using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Discord Messages Chat", "Slut", "1.0.4")]
    [Description("Relay global / team chat to Discord!")]
    public class DiscordMessagesChat  : RustPlugin
    {
        #region Variables
        [PluginReference]
        private Plugin DiscordMessages, BetterChatMute;

        private bool _teamChatEnabled;
        private bool _globalChatEnabled;
        private readonly object _falseObject = false;
        

        #endregion

        #region Configuration
        
        private Configuration _configuration;
        private class Configuration
        {
            public string GlobalChatWebhook;
            public string TeamChatWebhook;
            public bool AllowMutedPlayers;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _configuration = Config.ReadObject<Configuration>();
            
            CheckWebhook(_configuration.GlobalChatWebhook, success =>
            {
                _globalChatEnabled = success;
                if (!_globalChatEnabled)
                {
                    PrintWarning("Global Chat Webhook is not correct!");
                }
            });
            CheckWebhook(_configuration.TeamChatWebhook, success =>
            {
                _teamChatEnabled = success;
                if (!_teamChatEnabled)
                {
                    PrintWarning("Team Chat Webhook is not correct!");
                }
            });
            
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_configuration);
        }

        protected override void LoadDefaultConfig()
        {
            _configuration = new Configuration();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GlobalChatFormat"] = "[{time}] {username}: {message}",
                ["TeamChatFormat"] = "[TEAM] [{time}] {username}: {message}"
            }, this);
        }
        
        #endregion

        #region Hooks

        private void OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel)
        {
            if ((BetterChatMute?.Call<bool>("API_IsMuted", player.IPlayer) ?? false) && !_configuration.AllowMutedPlayers)
            {
                return;
            }
            if (channel == ConVar.Chat.ChatChannel.Team && !_teamChatEnabled)
            {
                return;
            }

            if (channel == ConVar.Chat.ChatChannel.Global && !_globalChatEnabled)
            {
                return;
            }

            message = message.Replace("@here", "@.here").Replace("@everyone", "@.everyone");
            string formattedMessage = lang.GetMessage(channel == ConVar.Chat.ChatChannel.Team ? "TeamChatFormat" : "GlobalChatFormat", this).Replace("{time}", DateTime.Now.ToShortTimeString()).Replace("{username}", player.displayName).Replace("{message}", message);
            
            DiscordMessages?.Call("API_SendTextMessage", channel == ConVar.Chat.ChatChannel.Team ? _configuration.TeamChatWebhook : _configuration.GlobalChatWebhook, formattedMessage, _falseObject, this);
        }        

        #endregion

        #region Functions

        private void CheckWebhook(string webhookUrl, Action<bool> success)
        {
            if (string.IsNullOrEmpty(webhookUrl))
            {
                success?.Invoke(false);
                return;
            }

            webrequest.Enqueue(webhookUrl, null, (code, response) =>
            {
                success?.Invoke(code == 200);
            }, this);
        }

        #endregion
    }
}