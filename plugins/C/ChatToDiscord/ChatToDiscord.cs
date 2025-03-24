using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System;
using ConVar;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Chat to Discord Relay", "Psystec", "1.0.9")]
    [Description("Relay chat to Discord")]

    public class ChatToDiscord : CovalencePlugin
    {
        Dictionary<string, SteamPlayerInfo> _steamPlayerInfoCache = new Dictionary<string, SteamPlayerInfo>();
        public const string AdminPermission = "chattodiscord.admin";

        #region Configuration

        private Configuration _configuration;
        private class Configuration
        {
            public string SteamApiKey { get; set; } = "https://steamcommunity.com/dev/apikey";
            public string GlobalChatWebhook { get; set; } = "";
            public string TeamChatWebhook { get; set; } = "";
            public string ConnectionWebhook { get; set; } = "";
            public bool AllowMentions { get; set; } = false;
            public bool AllowSpecialCharacters { get; set; } = true;
            public string GlobalChatFormat { get; set; } = "[{time}] [**GLOBAL**] **{username}**: `{message}`";
            public string TeamChatFormat { get; set; } = "[{time}] [**TEAM**] **{username}**: `{message}`";
            public string ConnectionFormat { get; set; } = "[{time}] **{username}**: {connectionstatus}";
            public string DateFormat { get; set; } = "yyyy-MM-dd HH:mm:ss";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Connected"] = "Connected.",
                ["Disconnected"] = "Disconnected.",
                ["NoPermission"] = "You do not have permission to use this command.",
                ["FileLoaded"] = "File loaded.",
                ["cmdCommand"] = "COMMAND",
                ["cmdDescription"] = "DESCRIPTION",
                ["cmdReload"] = "Reads the config file."
            }, this);
        }

        protected override void SaveConfig() => Config.WriteObject(_configuration);
        private void LoadNewConfig() => _configuration = Config.ReadObject<Configuration>();
        protected override void LoadDefaultConfig() => _configuration = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _configuration = Config.ReadObject<Configuration>();
        }

        #endregion Configuration

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(AdminPermission, this);
        }

        private void Loaded()
        {
            CheckSubscribsions();
        }

        #endregion Hooks

        #region User Connection Hooks

        private void OnUserConnected(IPlayer player)
        {
            string message = _configuration.ConnectionFormat
                .Replace("{time}", DateTime.Now.ToString(_configuration.DateFormat))
                .Replace("{username}", player.Name)
                .Replace("{userid}", player.Id)
                .Replace("{connectionstatus}", Lang("Connected"));

            SendToDiscord(_configuration.ConnectionWebhook, player.Name, player.Id, message);
        }

        private void OnUserDisconnected(IPlayer player)
        {
            string message = _configuration.ConnectionFormat
                .Replace("{time}", DateTime.Now.ToString(_configuration.DateFormat))
                .Replace("{username}", player.Name)
                .Replace("{userid}", player.Id)
                .Replace("{connectionstatus}", Lang("Disconnected"));

            SendToDiscord(_configuration.ConnectionWebhook, player.Name, player.Id, message);
        }

        #endregion User Connection Hooks

        #region User Chat Hooks

        private void OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            //Allow Metions
            if (!_configuration.AllowMentions)
            {
                message = message.Replace("@here", "@.here")
                .Replace("@everyone", "@.everyone");
            }

            //Allow Special Characters
            if (!_configuration.AllowSpecialCharacters)
            {
                message = RemoveSpecialCharacters(message);
            }

            switch (channel)
            {
                case Chat.ChatChannel.Global:
                    if (!string.IsNullOrEmpty(_configuration.GlobalChatWebhook))
                    {
                        message = _configuration.GlobalChatFormat
                            .Replace("{time}", DateTime.Now.ToString(_configuration.DateFormat))
                            .Replace("{username}", player.displayName)
                            .Replace("{userid}", player.UserIDString)
                            .Replace("{message}", message);

                        SendToDiscord(_configuration.GlobalChatWebhook, player.displayName, player.UserIDString, message);
                    }
                    break;

                case Chat.ChatChannel.Team:
                    if (!string.IsNullOrEmpty(_configuration.TeamChatWebhook))
                    {
                        message = _configuration.TeamChatFormat
                            .Replace("{time}", DateTime.Now.ToString(_configuration.DateFormat))
                            .Replace("{username}", player.displayName)
                            .Replace("{userid}", player.UserIDString)
                            .Replace("{message}", message);

                        SendToDiscord(_configuration.TeamChatWebhook, player.displayName, player.UserIDString, message);
                    }
                    break;
            }
        }

        #endregion User Chat Hooks

        #region Commands

        [Command("chattodiscord")]
        private void ChatToDiscordCommands(IPlayer player, string command, string[] args)
        {
            if (player == null)
                return;

            if (!HasPermission(player, AdminPermission))
                return;

            if (args.IsNullOrEmpty())
            {
                player.Reply(Lang("cmdCommand").PadRight(30) + Lang("cmdDescription"));
                player.Reply(("chattodiscord loadconfig").PadRight(30) + Lang("cmdReload"));
                return;
            }

            if (args[0] == "loadconfig")
            {
                player.Reply(Lang("FileLoaded"));
                LoadNewConfig();
                Loaded();
            }
        }

        #endregion Commands

        #region Helpers

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        private bool HasPermission(IPlayer player, string permission)
        {
            if (!player.HasPermission(permission))
            {
                player.Reply(Lang("NoPermission"));
                PrintWarning("UserID: " + player.Id + " | UserName: " + player.Name + " | " + Lang("NoPermission"));
                return false;
            }
            return true;
        }
        public static string RemoveSpecialCharacters(string message)
        {
            string pattern = "[^a-zA-Z0-9._@\\[\\] ]";

            // Replace matched characters (those NOT allowed) with an empty string
            string cleanedMessage = Regex.Replace(message, pattern, "");

            return cleanedMessage;
        }
        private void SendToDiscord(string Webhook, string PlayerName, string PlayerID, string message)
        {
            string steamUrl = $"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={_configuration.SteamApiKey}&steamids={PlayerID}";
            string defaultImage = "https://images.sftcdn.net/images/t_app-logo-l,f_auto,dpr_auto/p/e8326516-9b2c-11e6-9634-00163ec9f5fa/3905415571/rust-logo.png";
            //string defaultImage = "https://files.facepunch.com/lewis/1b2911b1/rust-marque.svg";
            Dictionary<string, string> headers = new Dictionary<string, string>
            {
                { "Content-Type", "application/json" }
            };


            if (_steamPlayerInfoCache.ContainsKey(PlayerID))
            {
                //User cached Data from SteamPlayerInfo
                foreach (Player p in _steamPlayerInfoCache[PlayerID].response.players)
                {
                    defaultImage = p.avatarfull;
                    DiscordMessage dm = new DiscordMessage();
                    dm.username = PlayerName;
                    dm.avatar_url = defaultImage;
                    dm.content = message;
                    string payload = JsonConvert.SerializeObject(dm);

                    webrequest.Enqueue(Webhook, payload, (dcode, dresponse) =>
                    {
                        if (dcode != 200 && dcode != 204)
                        {
                            if (dresponse == null)
                            {
                                PrintWarning($"Discord didn't respond (down?) Code: {dcode}");
                            }
                        }
                    }, this, Core.Libraries.RequestMethod.POST, headers);
                }
            }
            else
            {
                webrequest.Enqueue(steamUrl, null, (code, response) =>
                {
                    if (code != 200)
                    {
                        PrintWarning($"ERROR: {response}");
                        PrintWarning($"steamUrl: {steamUrl}");
                        PrintWarning($"code: {code}");
                        return;
                    }

                    SteamPlayerInfo steamPlayers = JsonConvert.DeserializeObject<SteamPlayerInfo>(response);

                    _steamPlayerInfoCache.Add(PlayerID, steamPlayers); //Cache SteamPlayerInfo Data from Player

                    foreach (Player p in steamPlayers.response.players)
                    {
                        defaultImage = p.avatarfull;
                        DiscordMessage dm = new DiscordMessage();
                        dm.username = PlayerName;
                        dm.avatar_url = defaultImage;
                        dm.content = message;
                        string payload = JsonConvert.SerializeObject(dm);

                        webrequest.Enqueue(Webhook, payload, (dcode, dresponse) =>
                        {
                            if (dcode != 200 && dcode != 204)
                            {
                                if (dresponse == null)
                                {
                                    PrintWarning($"Discord didn't respond (down?) Code: {dcode}");
                                }
                            }
                        }, this, Core.Libraries.RequestMethod.POST, headers);

                    }
                }, this, Core.Libraries.RequestMethod.GET);
            }
        }
        private void CheckSubscribsions()
        {
            if (string.IsNullOrEmpty(_configuration.GlobalChatWebhook) && string.IsNullOrEmpty(_configuration.TeamChatWebhook))
                Unsubscribe(nameof(OnPlayerChat));
            else
                Subscribe(nameof(OnPlayerChat));

            if (string.IsNullOrEmpty(_configuration.ConnectionWebhook))
            {
                Unsubscribe(nameof(OnUserConnected));
                Unsubscribe(nameof(OnUserDisconnected));
            }
            else
            {
                Subscribe(nameof(OnUserConnected));
                Subscribe(nameof(OnUserDisconnected));
            }
        }

        #endregion Helpers

        #region Classes

        #region Discord Messages
        public class DiscordMessage
        {
            /// <summary>
            /// if used, it overrides the default username of the webhook
            /// </summary>
            public string username { get; set; }
            /// <summary>
            /// if used, it overrides the default avatar of the webhook
            /// </summary>
            public string avatar_url { get; set; }
            /// <summary>
            /// simple message, the message contains (up to 2000 characters)
            /// </summary>
            public string content { get; set; }
            /// <summary>
            /// array of embed objects. That means, you can use more than one in the same body
            /// </summary>
            public Embed[] embeds { get; set; }
        }
        public class Embed
        {
            /// <summary>
            /// embed author object
            /// </summary>
            public Author author { get; set; }
            /// <summary>
            /// title of embed
            /// </summary>
            public string title { get; set; }
            /// <summary>
            /// url of embed. If title was used, it becomes hyperlink
            /// </summary>
            public string url { get; set; }
            /// <summary>
            /// description text
            /// </summary>
            public string description { get; set; }
            /// <summary>
            /// color code of the embed. You have to use Decimal numeral system, not Hexadecimal. Use color picker and converter: https://htmlcolorcodes.com/color-picker/ and https://www.binaryhexconverter.com/hex-to-decimal-converter
            /// </summary>
            public int color { get; set; }
            /// <summary>
            /// rray of embed field objects
            /// </summary>
            public Field[] fields { get; set; }
            /// <summary>
            /// embed thumbnail object
            /// </summary>
            public Thumbnail thumbnail { get; set; }
            /// <summary>
            /// embed image object
            /// </summary>
            public Image image { get; set; }
            /// <summary>
            /// embed footer object
            /// </summary>
            public Footer footer { get; set; }
        }
        public class Author
        {
            /// <summary>
            /// name of author
            /// </summary>
            public string name { get; set; }
            /// <summary>
            /// url of author. If name was used, it becomes a hyperlink
            /// </summary>
            public string url { get; set; }
            /// <summary>
            /// url of author icon
            /// </summary>
            public string icon_url { get; set; }
        }
        public class Thumbnail
        {
            /// <summary>
            /// url of thumbnail
            /// </summary>
            public string url { get; set; }
        }
        public class Image
        {
            /// <summary>
            /// url of image
            /// </summary>
            public string url { get; set; }
        }
        public class Footer
        {
            /// <summary>
            /// footer text, doesn't support Markdown
            /// </summary>
            public string text { get; set; }
            /// <summary>
            /// url of footer icon
            /// </summary>
            public string icon_url { get; set; }
        }
        public class Field
        {
            /// <summary>
            /// name of the field
            /// </summary>
            public string name { get; set; }
            /// <summary>
            /// alue of the field
            /// </summary>
            public string value { get; set; }
            /// <summary>
            /// if true, fields will be displayed in same line, but there can only be 3 max in same line or 2 max if you used thumbnail
            /// </summary>
            public bool inline { get; set; }
        }

        #endregion
        #region Steam Player Info

        public class SteamPlayerInfo
        {
            public Response response { get; set; }
        }

        public class Response
        {
            public Player[] players { get; set; }
        }

        public class Player
        {
            public string steamid { get; set; }
            public int communityvisibilitystate { get; set; }
            public int profilestate { get; set; }
            public string personaname { get; set; }
            public string profileurl { get; set; }
            public string avatar { get; set; }
            public string avatarmedium { get; set; }
            public string avatarfull { get; set; }
            public string avatarhash { get; set; }
            public int lastlogoff { get; set; }
            public int personastate { get; set; }
            public string primaryclanid { get; set; }
            public int timecreated { get; set; }
            public int personastateflags { get; set; }
            public string gameserverip { get; set; }
            public string gameserversteamid { get; set; }
            public string gameextrainfo { get; set; }
            public string gameid { get; set; }
            public string loccountrycode { get; set; }
        }



        #endregion

        #endregion Classes
    }
}
