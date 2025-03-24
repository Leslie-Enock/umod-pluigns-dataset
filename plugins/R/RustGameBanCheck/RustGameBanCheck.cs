using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace Oxide.Plugins
{
    [Info("Rust Gameban Check", "Rogder Dodger", "1.1.2"),
     Description("Checks against GameBanDb for previous rust bans and alerts via discord")]
    public class RustGameBanCheck : RustPlugin
    {
        #region globals

        private const string DefaultWebhookURL = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        private const int WebTimeoutThreshold = 2000;
        private const string GameBanDbUrl = "https://rustbans.com/api";
        private const string DefaultBanMessage = "Previous Gameban Detected";

        #endregion

        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Log To Console")]
            public bool LogToConsole;

            [JsonProperty(PropertyName = "Log To Discord")]
            public bool LogToDiscord;

            [JsonProperty(PropertyName = "Discord Webhook URL")]
            public string DiscordWebhookUrl = DefaultWebhookURL;

            [JsonProperty(PropertyName = "Whitelist Steam IDs")]
            public List<ulong> WhitelistedSteamIds;

            [JsonProperty(PropertyName = "Automatically Ban Players with previous Rust Bans")]
            public bool AutomaticallyBan;

            [JsonProperty(PropertyName = "Ban Message")]
            public string BanMessage;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("A new configuration file is being generated.");
            _config = new Configuration
            {
                LogToConsole = true,
                LogToDiscord = true,
                DiscordWebhookUrl = DefaultWebhookURL,
                WhitelistedSteamIds = new List<ulong>(),
                AutomaticallyBan = false,
                BanMessage = "Previous Gameban Detected"
            };
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EmbedTitle"] = "GAMEBAN DETECTED!",
                ["EmbedDetailsWithBattlemetrics"] = "**Player:**\n{playerName}\n{playerId}\n[Steam Profile](https://steamcommunity.com/profiles/{playerId})\n\n**Links:**\n[Battlemetrics](https://www.battlemetrics.com/rcon/players?filter%5Bsearch%5D={playerId}&filter%5Bservers%5D=false&filter%5BplayerFlags%5D=&sort=score&showServers=true)",
                ["ConsoleMessage"] = "GAME BAN DETECTED on {playerName}/{playerId} - {twitterUrl}"
            }, this);
        }

        private string FormatMessage(string key, BasePlayer player, GameBannedDbResponse gameBanDetails)
        {
            return lang.GetMessage(key, this)
                .Replace("{playerName}", player.displayName)
                .Replace("{playerId}", player.UserIDString)
                .Replace("{serverName}", covalence.Server.Name);
        }

        #endregion

        #region Models

        private class GameBannedDbResponse
        {
            [JsonProperty("TweetLink")] public string? TweetLink { get; set; }

            [JsonProperty("BanDateMilliseconds")] public string? BanDateMilliseconds { get; set; }

            [JsonProperty("SteamID64")] public string? SteamID64 { get; set; }
            [JsonProperty("Banned")] public bool Banned { get; set; }

            [JsonProperty("TempBanCheck")] public string? TempBanCheck { get; set; }
        }

        #endregion'

        #region Hooks

        private void Init()
        {
            _config = Config.ReadObject<Configuration>();

            if (_config.BanMessage == null)
            {
                _config.BanMessage = DefaultBanMessage;
                SaveConfig();
            }

            if (_config.LogToDiscord && _config.DiscordWebhookUrl == DefaultWebhookURL)
            {
                PrintWarning($"Please set the discord webhook in the configuration file!)");
                Unsubscribe(nameof(OnPlayerConnected));
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (_config.WhitelistedSteamIds.Contains(player.userID)) return;
            CheckAndApplyGameBan(player.userID);
        }

        void CheckAndApplyGameBan(ulong userId)
        {
            string url = $"{GameBanDbUrl}/{userId}";
            webrequest.Enqueue(url, "", (httpCode, response) => HandleGameBan(httpCode, response, userId),
                this, Core.Libraries.RequestMethod.GET, null, WebTimeoutThreshold);
        }

        void HandleGameBan(int httpCode, string response, ulong userId)
        {
            if (httpCode == (int)StatusCode.Success)
            {
                var gameBanDetails = JsonConvert.DeserializeObject<IEnumerable<GameBannedDbResponse>>(response)
                    .FirstOrDefault();
                if (gameBanDetails.Banned)
                {
                    if (_config.LogToDiscord)
                    {
                        SendDiscordEmbed(gameBanDetails, BasePlayer.FindByID(userId));
                    }

                    if (_config.LogToConsole)
                    {
                        Puts(FormatMessage("ConsoleMessage", BasePlayer.FindByID(userId), gameBanDetails));
                    }

                    if (_config.AutomaticallyBan)
                    {
                        covalence.Players.FindPlayer(userId.ToString()).Ban(_config.BanMessage);
                    }
                }
            }
            else
            {
                PrintError($"Failed to check game ban for user {userId}. Status Code: {httpCode} Returned from API");
            }
        }

        #endregion

        #region Webhook

        private void SendDiscordEmbed(GameBannedDbResponse gameBanDetails, BasePlayer player)
        {
            var title = FormatMessage("EmbedTitle", player, gameBanDetails);
            var description = FormatMessage("EmbedDetailsWithBattlemetrics", player, gameBanDetails);
            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title,
                        description,
                        color = 10038562,
                        timestamp = DateTime.Now,
                    }
                }
            };

            var form = new WWWForm();
            form.AddField("payload_json", JsonConvert.SerializeObject(payload));
            ServerMgr.Instance.StartCoroutine(PostToDiscord(_config.DiscordWebhookUrl, form));
        }
      
        private IEnumerator PostToDiscord(string url, WWWForm data)
        {
            var www = UnityWebRequest.Post(url, data);
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Puts($"Failed to post to discord: {www.error}");
            }
        }

        #endregion

        #region enums

        private enum StatusCode
        {
            Success = 200,
            BadRequest = 400,
            Unauthorized = 401,
            Forbidden = 403,
            NotFound = 404,
            MethodNotAllowed = 405,
            TooManyRequests = 429,
            InternalError = 500,
            Unavailable = 503,
        }

        #endregion
    }
}