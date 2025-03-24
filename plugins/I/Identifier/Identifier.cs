using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Identifier", "Wulf", "2.0.3")]
    [Description("Gets identification information for one or all connected players")]
    public class Identifier : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Use permission system")]
            public bool UsePermissions = true;

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
                ["CommandId"] = "id",
                ["CommandIdAll"] = "ids",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NoPermission"] = " You have no permissions to use that!",
                ["NoPlayersFound"] = "No players found with name or ID '{0}'",
                ["NoPlayersConnected"] = "No players connected at the moment",
                ["PlayerNotFound"] = "Player '{0}' was not found",
                ["PlayersFound"] = "Multiple players were found, please specify: {0}",
                ["PlayerName"] = "Player Name: {0}",
                ["PlayerId"] = "Player ID: {0}",
                ["PlayerIpAddress"] = "IP Address: {0}",
                ["PlayerListFormat"] = "{0} ({1})",
                ["PlayerListFormatIp"] = "{0} ({1}) - {2}",
                ["SelfId"] = "Your player ID is {0}",
                ["TooManyPlayers"] = "Too many players connected to list them all",
                ["Unknown"] = "Unknown",
                ["UsageId"] = "Usage: {0} <player name or id> - Show identification info for specified player",
                ["UsageIdAll"] = "Usage: {0} - Show identification info for all connected players"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private const string permId = "identifier.id";
        private const string permIpAddress = "identifier.ipaddress";

        private void Init()
        {
            AddLocalizedCommand(nameof(CommandId));
            AddLocalizedCommand(nameof(CommandIdAll));

            permission.RegisterPermission(permId, this);
            permission.RegisterPermission(permIpAddress, this);
        }

        #endregion Initialization

        #region Commands

        private void CommandId(IPlayer player, string command, string[] args)
        {
            bool hasPermission = player.HasPermission(permId);

            if (args.Length == 0)
            {
                StringBuilder output = new StringBuilder();
                if (!player.IsServer)
                {
                    output.Append(GetLang("SelfId", player.Id, player.Id)); // TODO: Find a universal way to handle line break
                }
                if (hasPermission)
                {
                    output.Append(GetLang("UsageId", player.Id, command)).Append("\n"); // TODO: Find a universal way to handle line break
                    output.Append(GetLang("UsageIdAll", player.Id, GetLang("CommandIdAll", player.Id)));
                }
                Message(player, output.ToString());
                return;
            }

            IPlayer target = FindPlayer(string.Join(" ", args.ToArray()), player);
            if (target != null)
            {
                StringBuilder output = new StringBuilder();
                if (!config.UsePermissions || hasPermission)
                {
                    output.Append(GetLang("PlayerName", player.Id, target.Name)).Append("\n"); // TODO: Find a universal way to handle line break
                    output.Append(GetLang("PlayerId", player.Id, target.Id)).Append("\n"); // TODO: Find a universal way to handle line break
                }
                if (!config.UsePermissions && player.IsAdmin || player.HasPermission(permIpAddress))
                {
                    output.Append(GetLang("PlayerIpAddress", player.Id, string.IsNullOrEmpty(target.Address) ? GetLang("Unknown", player.Id) : target.Address));
                }
                Message(player, output.ToString());
            }
        }

        private void CommandIdAll(IPlayer player, string command, string[] args)
        {
            if (config.UsePermissions && !player.HasPermission(permId))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            int playersConnected = players.Connected.Count();

            if (playersConnected == 0)
            {
                Message(player, "NoPlayersConnected");
                return;
            }

            int targetedCount = 0;
            StringBuilder output = new StringBuilder();
            foreach (IPlayer target in players.Connected)
            {
                // TODO: Add support for limiting output to X amount of results
                // TODO: Add support for limiting results based on partial name/ID matches
                // TODO: Add support for pagination to split up large amount of results

                if (!config.UsePermissions && player.IsAdmin || player.HasPermission(permIpAddress))
                {
                    output.Append(GetLang("PlayerListFormatIp", player.Id, target.Name, target.Id, target.Address));
                }
                else
                {
                    output.Append(GetLang("PlayerListFormat", player.Id, target.Name, target.Id));
                }
                if (playersConnected != targetedCount)
                {
                    output.Append("\n"); // TODO: Find a universal way to handle line break
                }

                targetedCount++;
            }
            Message(player, output.ToString());
        }

        #endregion Commands

        #region Helpers

        private IPlayer FindPlayer(string playerNameOrId, IPlayer player)
        {
            IPlayer[] foundPlayers = players.FindPlayers(playerNameOrId).ToArray();
            if (foundPlayers.Length > 1)
            {
                Message(player, "PlayersFound", string.Join(", ", foundPlayers.Select(p => p.Name).Take(10).ToArray()).Truncate(60));
                return null;
            }

            IPlayer target = foundPlayers.Length == 1 ? foundPlayers[0] : null;
            if (target == null)
            {
                Message(player, "NoPlayersFound", playerNameOrId);
                return null;
            }

            return target;
        }

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
