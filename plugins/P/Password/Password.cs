//#define DEBUG

using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Password", "Wulf", "3.0.2")]
    [Description("Provides name and chat command password protection for the server")]
    class Password : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        class Configuration
        {
            [JsonProperty("Server password")]
            public string ServerPassword = "umod";

            [JsonProperty("Maxium password attempts")]
            public int MaxAttempts = 3;

            [JsonProperty("Grace period (seconds)")]
            public int GracePeriod = 60;

            [JsonProperty("Always check for password on join")]
            public bool AlwaysCheck = true;

            [JsonProperty("Ask for password in chat")]
            public bool PromptInChat = true;

            [JsonProperty("Check for password in names")]
            public bool CheckNames = true;

            [JsonProperty("Freeze unauthorized players")]
            public bool FreezeUnauthorized = true;

            [JsonProperty("Mute unauthorized players")]
            public bool MuteUnauthorized = true;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
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
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandPassword"] = "password",
                ["MaximumAttempts"] = "You've exhausted the maximum password attempts ({0})",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NotFastEnough"] = "You did not enter a password fast enough ({0} seconds)",
                ["PasswordAccepted"] = "Server password accepted, welcome!",
                ["PasswordChanged"] = "Server password has been changed to: {0}",
                ["PasswordCurrently"] = "Server password is currently set to: {0}",
                ["PasswordInvalid"] = "Invalid server password or not provided",
                ["PasswordPrompt"] = "Please enter the server password with /{0} PASSWORD"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private readonly Dictionary<string, Timer> freezeTimers = new Dictionary<string, Timer>();
        private readonly Dictionary<string, int> attempts = new Dictionary<string, int>();
        private readonly HashSet<string> authorized = new HashSet<string>();

        private const string permBypass = "password.bypass";

        private void Init()
        {
            permission.RegisterPermission(permBypass, this);

            AddCovalenceCommand("server.password", nameof(CommandServerPassword));
            AddLocalizedCommand(nameof(CommandPassword));

            if (!config.PromptInChat)
            {
                Unsubscribe(nameof(OnUserConnected));
            }
            if (!config.MuteUnauthorized)
            {
                Unsubscribe(nameof(OnBetterChat));
                Unsubscribe(nameof(OnUserChat));
            }
        }

        private void OnServerInitialized()
        {
            foreach (IPlayer player in players.Connected)
            {
                authorized.Add(player.Id);
            }
        }

        #endregion Initialization

        #region Login Checking

        private object CanUserLogin(string playerName, string playerId)
        {
#if DEBUG
            LogWarning($"{playerName} authorized? {authorized.Contains(playerId)}");
#endif

            if (permission.UserHasPermission(playerId, permBypass) || config.CheckNames && playerName.Contains(config.ServerPassword)
                || !config.AlwaysCheck && authorized.Contains(playerId))
            {
                if (!authorized.Contains(playerId))
                {
                    authorized.Add(playerId);
                }
                Interface.Oxide.CallHook("OnPasswordAccepted", playerName, playerId);
                return true;
            }

            if (!config.PromptInChat)
            {
                return GetLang("PasswordInvalid", playerId);
            }

            return null;
        }

        #endregion Login Checking

        #region Auth Handling

        private void OnUserConnected(IPlayer player)
        {
            if (!player.HasPermission(permBypass) && !authorized.Contains(player.Id))
            {
                Message(player, "PasswordPrompt", GetLang("CommandPassword", player.Id));

                timer.Once(config.GracePeriod, () =>
                {
                    if (!authorized.Contains(player.Id))
                    {
                        player.Kick(GetLang("NotFastEnough", player.Id, config.GracePeriod));
                    }
                });

                if (config.FreezeUnauthorized)
                {
                    GenericPosition originalPosition = player.Position();
                    freezeTimers[player.Id] = timer.Every(0.01f, () =>
                    {
                        if (!player.IsConnected || authorized.Contains(player.Id))
                        {
                            freezeTimers[player.Id].Destroy();
                        }
                        else
                        {
                            player.Teleport(originalPosition);
                        }
                    });
                }
            }
        }

        private void OnUserDisconnected(IPlayer player)
        {
            if (!config.AlwaysCheck && authorized.Contains(player.Id))
            {
                authorized.Remove(player.Id);
            }
        }

        #endregion Auth Handling

        #region Chat Handling

        private object OnUserChat(IPlayer player)
        {
            if (!authorized.Contains(player.Id))
            {
                Message(player, "PasswordPrompt", GetLang("CommandPassword", player.Id));
                return true;
            }

            return null;
        }

        private object OnBetterChat(Dictionary<string, object> data)
        {
            if (!authorized.Contains((data["Player"] as IPlayer).Id))
            {
                return true;
            }

            return null;
        }

        #endregion Chat Handling

        #region Commands

        private void CommandPassword(IPlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                Message(player, "PasswordPrompt", command);
                return;
            }

            if (args[0] != config.ServerPassword)
            {
                if (attempts.ContainsKey(player.Id) && attempts[player.Id] + 1 >= config.MaxAttempts)
                {
                    player.Kick(GetLang("MaximumAttempts", player.Id, config.MaxAttempts));
                    return;
                }

                Message(player, "PasswordInvalid");
                if (attempts.ContainsKey(player.Id))
                {
                    attempts[player.Id] += 1;
                }
                else
                {
                    attempts.Add(player.Id, 1);
                }
                return;
            }

            authorized.Add(player.Id);
            Message(player, "PasswordAccepted");
            Interface.Oxide.CallHook("OnPasswordAccepted", player.Name, player.Id);
        }

        private void CommandServerPassword(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length < 1)
            {
                Message(player, "PasswordCurrently", config.ServerPassword);
                return;
            }

            config.ServerPassword = args[0].Sanitize(); // TODO: Combine all args to allow spaces?
            Message(player, "PasswordChanged", config.ServerPassword);
            SaveConfig();
        }

        #endregion Commands

        #region Helpers

        private void AddLocalizedCommand(string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages)
                {
                    if (message.Key.Equals(command))
                    {
                        if (!string.IsNullOrEmpty(message.Value))
                        {
                            AddCovalenceCommand(message.Value, command);
                        }
                    }
                }
            }
        }

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }

        private void Message(IPlayer player, string textOrLang, params object[] args)
        {
            if (player.IsConnected)
            {
                string message = GetLang(textOrLang, player.Id, args);
                player.Reply(message != textOrLang ? message : textOrLang);
            }
        }

        #endregion Helpers
    }
}
