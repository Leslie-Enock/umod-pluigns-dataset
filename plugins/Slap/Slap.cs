using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using Random = System.Random;

// TODO: Add distance/radius restriction for slapping

namespace Oxide.Plugins
{
    [Info("Slap", "Wulf", "2.0.2")]
    [Description("Sometimes players just need to be slapped around a bit")]
    class Slap : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        class Configuration
        {
            [JsonProperty("Command cooldown in seconds (0 to disable)")]
            public int CommandCooldown = 30;

            [JsonProperty("Default damage per slap")]
            public int DefaultDamage = 10;

            [JsonProperty("Default intensity per slap")]
            public int DefaultIntensity = 5;

            [JsonProperty("Default amount of slaps")]
            public int DefaultAmount = 1;

            [JsonProperty("Show players who slapped them")]
            public bool ShowWhoSlapped = true;

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
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandCooldown"] = "Wait a bit before attempting to slap again",
                ["CommandSlap"] = "slap",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["NoPlayersFound"] = "No players found with name or ID '{0}'",
                ["PlayerNotFound"] = "Player '{0}' was not found",
                ["PlayersFound"] = "Multiple players were found, please specify: {0}",
                ["PlayerSlapped"] = "{0} got slapped!",
                ["UsageSlap"] = "Usage: {0} <name or id> [damage] [intensity] [amount]",
                ["YouGotSlapped"] = "You got slapped!",
                ["YouGotSlappedBy"] = "You got slapped by {0}!"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private readonly Hash<string, float> cooldowns = new Hash<string, float>();
        private static readonly Random random = new Random();

        private const string permUse = "slap.use";

        private void Init()
        {
            AddLocalizedCommand(nameof(CommandSlap));

            permission.RegisterPermission(permUse, this);
        }

        #endregion Initialization

        #region Commands

        private void CommandSlap(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length == 0)
            {
                Message(player, "UsageSlap", command);
                return;
            }

            if (!player.IsServer)
            {
                if (!cooldowns.ContainsKey(player.Id))
                {
                    cooldowns.Add(player.Id, 0f);
                }

                if (config.CommandCooldown > 0 && cooldowns[player.Id] + config.CommandCooldown > Interface.Oxide.Now)
                {
                    Message(player, "CommandCooldown");
                    return;
                }
            }

            IPlayer target = FindPlayer(args[0], player);
            if (target == null)
            {
                return;
            }

            int damage = args.Length >= 2 ? int.Parse(args[1]) : config.DefaultDamage;
            int intensity = args.Length >= 3 ? int.Parse(args[2]) : config.DefaultIntensity;
            int amount = args.Length >= 4 ? int.Parse(args[3]) : config.DefaultAmount;
            SlapPlayer(target, damage, intensity, amount);

            if (!target.Equals(player))
            {
                Message(player, "PlayerSlapped", target.Name);
            }

            if (config.ShowWhoSlapped)
            {
                Message(target, "YouGotSlappedBy", player.Name);
            }
            else
            {
                Message(target, "YouGotSlapped");
            }
            cooldowns[player.Id] = Interface.Oxide.Now;
        }

        #endregion Commands

        #region Slapping

        private void SlapPlayer(IPlayer player, float damage = 0f, int intensity = 0, int amount = 0)
        {
            damage = damage > 0f ? damage : config.DefaultDamage;
            intensity = intensity > 0 ? intensity : config.DefaultIntensity;
            amount = amount > 0 ? amount : config.DefaultAmount;

            timer.Repeat(0.6f, amount, () => {
                if (player != null && player.IsConnected)
                {
                    player.Hurt(damage * intensity);                    
                }});

#if RUST

            BasePlayer basePlayer = player.Object as BasePlayer;
            BaseEntity.Signal[] flinches = new[]
            {
                BaseEntity.Signal.Flinch_Chest,
                BaseEntity.Signal.Flinch_Head,
                BaseEntity.Signal.Flinch_Stomach
            };
            BaseEntity.Signal flinch = flinches[random.Next(flinches.Length)];
            basePlayer.SignalBroadcast(flinch, string.Empty, null);
            string[] effects = new[] // TODO: Move to configuration
            {
                "headshot",
                "headshot_2d",
                "impacts/slash/clothflesh/clothflesh1",
                "impacts/stab/clothflesh/clothflesh1"
            };
            string effect = effects[random.Next(effects.Length)];
            Effect.server.Run($"assets/bundled/prefabs/fx/{effect}.prefab", basePlayer.transform.position, UnityEngine.Vector3.zero);

#endif

            GenericPosition pos = player.Position();
            player.Teleport(pos.X + random.Next(1, intensity), pos.Y + random.Next(1, intensity), pos.Z + random.Next(1, intensity));
        }

        #endregion Slapping

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