/***********************************************************************************************************************/
/*** DO NOT edit this file! Edit the files under `oxide/config` and/or `oxide/lang`, created once plugin has loaded. ***/
/*** Please note, support cannot be provided if the plugin has been modified. Please use a fresh copy if modified.   ***/
/***********************************************************************************************************************/

//#define DEBUG

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Keywords", "Wulf", "1.3.1")]
    [Description("Sends notifications when a keyword is used in the chat")]
    public class Keywords : CovalencePlugin
    {
        #region Configuration

        private Configuration _config;

        public class Configuration
        {
            [JsonProperty("Permission required to trigger keywords")]
            public bool UsePermissions = false;

            [JsonProperty("Include original message with notification")]
            public bool IncludeOriginal = true;

            [JsonProperty("Match only exact keywords")]
            public bool MatchExact = true;

            [JsonProperty("Auto-reply for triggered keywords")]
            public bool AutoReply = false;

            [JsonProperty("Notify configured players in chat")]
            public bool NotifyPlayers = false;

            [JsonProperty("Players to notify in chat", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> PlayersToNotify = new List<string> { "PLAYER_ID", "PLAYER_ID_2" };

            [JsonProperty("Notify configured groups in chat")]
            public bool NotifyGroups = true;

            [JsonProperty("Notify configured group in chat")]
            private bool NotifyGroupsOld { set { NotifyGroups = value; } } // TODO: From version 1.2.4; remove eventually

            [JsonProperty("Groups to notify in chat", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> GroupsToNotify = new List<string> { "admin", "moderator" };
#if RUST
            [JsonProperty("Notify using GUI Announcements")]
            public bool NotifyGuiAnnouncements = false;

            [JsonProperty("Banner color to use for GUI (RGBA or color name)")]
            public string GuiBannerColor = "0.1 0.1 0.1 0.7";

            [JsonProperty("Text color to use for GUI (RGB or color name)")]
            public string GuiTextColor = "1 1 1";

            [JsonProperty("Notify using UI Notify")]
            public bool NotifyUiNotify = false;

            [JsonProperty("Notification type for UI Notify")]
            public bool UiNotifyType = false;
#endif
            [JsonProperty("Notify in Discord channel")]
            public bool NotifyInDiscord = false;

            [JsonProperty("Discord embed color (decimal color code)")]
            public string DiscordEmbedColor = "16538684";

            [JsonProperty("Role IDs to mention on Discord", ObjectCreationHandling = ObjectCreationHandling.Replace)] // <@&ROLE_ID> for roles
            public List<ulong> RolesToMention = new List<ulong> { 305751989176762388 };

            [JsonProperty("Roles to mention on Discord",
            ObjectCreationHandling = ObjectCreationHandling.Replace)]
            private List<string> RolesToMentionOld // TODO: From version 1.2.4; remove eventually
            {
                set
                {
                    List<ulong> roles = new List<ulong>();
                    for (int i = 0; i < value.Count; i++)
                    {
                        ulong roleId;
                        if (ulong.TryParse(value[i], out roleId))
                        {
                            roles.Add(roleId);
                        }
                    }
                    RolesToMention = roles;
                }
            }

            [JsonProperty("User IDs to mention on Discord", ObjectCreationHandling = ObjectCreationHandling.Replace)] // <@USER_ID> for users
            public List<ulong> UsersToMention = new List<ulong> { 97031326011506688 };

            [JsonProperty("Users to mention on Discord", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            private List<string> UsersToMentionOld // TODO: From version 1.2.4; remove eventually
            {
                set
                {
                    List<ulong> users = new List<ulong>();
                    for (int i = 0; i < value.Count; i++)
                    {
                        ulong userId;
                        if (ulong.TryParse(value[i], out userId))
                        {
                            users.Add(userId);
                        }
                    }
                    UsersToMention = users;
                }
            }

            [JsonProperty("Discord webhook URL")]
            public string DiscordWebhook = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

            [JsonProperty("Keywords to listen for in chat", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Keywords = new List<string> { "admin", "crash", "bug" };

            private string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    throw new JsonException();
                }

                if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            LogWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #endregion Configuration

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["AutoReply"] = "Your message has triggered a notification to admin",
                ["KeywordsChat"] = "{0} ({1}) has used the keywords: {2}",
                ["DiscordKeywordsFieldName"] = "Keywords",
                ["DiscordMentionsFieldName"] = "Mentions",
                ["DiscordMessageFieldName"] = "Message",
                ["DiscordPlayerFieldName"] = "Player"
            }, this);
        }

        #endregion Localization

        #region Initialization

        [PluginReference]
        private Plugin BetterChat, GUIAnnouncements, UINotify;

        private readonly Regex _keywordRegex = new Regex("\\w+(?:'(?![aeiou])\\w+)?", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private const string DiscordJson = @"{
            ""embeds"":[{
                    ""color"": ""${discord.embed.color}"",
                    ""fields"": [
                    {
                        ""name"": ""${player.field.name}"",
                        ""value"": ""${player}""
                    },
                    {
                        ""name"": ""${keywords.field.name}"",
                        ""value"": ""${keywords}""
                    },
                    {
                        ""name"": ""${message.field.name}"",
                        ""value"": ""${message}""
                    },
                    {
                        ""name"": ""${mentions.field.name}"",
                        ""value"": ""${mentions}""
                    }
                ]
            }]
        }";
        private const string PermissionUse = "keywords.use";
        private const string PermissionBypass = "keywords.bypass";

        private string _roleMentions;
        private string _userMentions;

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionBypass, this);

            if (_config.RolesToMention.Count > 0)
            {
                for (int i = 0; i < _config.RolesToMention.Count; i++)
                {
                    _roleMentions += $"<@&{_config.RolesToMention[i]}>";
                }
            }
            if (_config.UsersToMention.Count > 0)
            {
                for (int i = 0; i < _config.UsersToMention.Count; i++)
                {
                    _userMentions += $"<@{_config.UsersToMention[i]}>";
                }
            }

            if (BetterChat != null && BetterChat.IsLoaded)
            {
                Unsubscribe(nameof(OnUserChat));
            }
        }

        #endregion Initialization

        #region Chat Handling

        private void HandleChat(IPlayer player, string message)
        {
            if (player == null || string.IsNullOrEmpty(message))
            {
                return;
            }

            if (_config.UsePermissions && (!player.HasPermission(PermissionUse) || player.HasPermission(PermissionBypass)))
            {
                return;
            }

#if DEBUG
            LogWarning($"DEBUG: Message from {player.Name} ({player.Id}): {message}");
#endif
            List<string> matches = new List<string>();
            MatchCollection keywordMatches = _keywordRegex.Matches(message);
            if (_config.MatchExact)
            {
#if DEBUG
                LogWarning("DEBUG: Looking for exact matches...");
                LogWarning($"DEBUG: Regex matches: {keywordMatches.Count}");
#endif
                // These are exact matches
                matches = keywordMatches.Cast<Match>().Select(m => m.Value).Intersect(_config.Keywords, StringComparer.OrdinalIgnoreCase).ToList(); // TODO: Find alternative to Linq
            }
            else
            {
#if DEBUG
                LogWarning("DEBUG: Looking for partial matches...");
#endif
                // These are partial matches
                for (int i = 0; i < keywordMatches.Count; i++)
                {
                    if (_config.Keywords.Contains(keywordMatches[i].Value))
                    {
                        matches.Add(keywordMatches[i].Value);
                    }
                }
            }

#if DEBUG
            LogWarning($"DEBUG: Matches found: {matches.Count}");
#endif
            if (matches.Count > 0)
            {
                string[] triggers = matches.Distinct().ToArray(); // TODO: Find alternative to Linq
#if DEBUG
                LogWarning($"DEBUG: Keywords triggered by {player.Name} ({player.Id})! {string.Join(", ", triggers)}");
#endif
                if (_config.NotifyPlayers)
                {
                    foreach (string targetId in _config.PlayersToNotify)
                    {
                        IPlayer target = players.FindPlayer(targetId);
                        if (target != null && target.IsConnected)
                        {
                            string notification = GetLang("KeywordsChat", target.Id, player.Name, player.Id, string.Join(", ", triggers));
                            target.Message(_config.IncludeOriginal ? notification + $" | {message}" : notification);
                            SendGuiNotification(target, notification);
                        }
                    }
                }

                if (_config.NotifyGroups)
                {
                    foreach (IPlayer target in players.Connected)
                    {
                        foreach (string group in _config.GroupsToNotify)
                        {
                            if (target.BelongsToGroup(group.ToLower()) && _config.NotifyPlayers && !_config.PlayersToNotify.Contains(target.Id) && target.IsConnected)
                            {
                                string notification = GetLang("KeywordsChat", target.Id, player.Name, player.Id, string.Join(", ", triggers));
                                target.Message(_config.IncludeOriginal ? notification + $" | {message}" : notification);
                                SendGuiNotification(target, notification);
                            }
                        }
                    }
                }

                if (_config.NotifyInDiscord && _config.DiscordWebhook.Contains("/api/webhooks"))
                {
                    for (int i = 0; i < triggers.Length; i++)
                    {
                        message = message.Replace(triggers[i], $"**{triggers[i]}**");
                    }
                    string content = DiscordJson
                        .Replace("${discord.embed.color}", _config.DiscordEmbedColor)
                        .Replace("${player.field.name}", GetLang("DiscordPlayerFieldName"))
                        .Replace("${player}", $"{player.Name.Sanitize()} ({player.Id})")
                        .Replace("${keywords.field.name}", GetLang("DiscordKeywordsFieldName"))
                        .Replace("${keywords}", string.Join(", ", triggers))
                        .Replace("${message.field.name}", GetLang("DiscordMessageFieldName"))
                        .Replace("${message}", message.Sanitize())
                        .Replace("${mentions.field.name}", GetLang("DiscordMentionsFieldName"))
                        .Replace("${mentions}", _roleMentions + _userMentions);
#if DEBUG
                    LogWarning($"DEBUG: {content}");
#endif

                    webrequest.Enqueue(_config.DiscordWebhook, content, (code, response) =>
                    {
#if DEBUG
                        LogWarning($"DEBUG: {_config.DiscordWebhook}");
                        if (!string.IsNullOrEmpty(response))
                        {
                            LogWarning($"DEBUG: {response}");
                        }
#endif
                        if (code != 204)
                        {
                            LogWarning($"Discord.com responded with code {code}");
                        }
                    }, this, RequestMethod.POST, new Dictionary<string, string> { ["Content-Type"] = "application/json" });
                }

                if (_config.AutoReply)
                {
                    player.Reply(GetLang("AutoReply", player.Id));
                }
            }
        }
        private void OnBetterChat(Dictionary<string, object> data)
        {
            HandleChat(data["Player"] as IPlayer, data["Message"] as string);
        }

        private void OnUserChat(IPlayer player, string message) => HandleChat(player, message);

        #endregion Chat Handling

        #region Helpers

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }

        private void SendGuiNotification(IPlayer target, string notification)
        {
#if RUST
            if (_config.NotifyGuiAnnouncements && GUIAnnouncements != null && GUIAnnouncements.IsLoaded)
            {
                GUIAnnouncements.Call("CreateAnnouncement", notification, _config.GuiBannerColor, _config.GuiTextColor, target);
            }
            if (_config.NotifyUiNotify && UINotify != null && UINotify.IsLoaded)
            {
                UINotify.Call("SendNotify", target.Id, _config.UiNotifyType, notification);
            }
#endif
        }

        #endregion Helpers
    }
}