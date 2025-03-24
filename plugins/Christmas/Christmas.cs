using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Christmas", "FastBurst", "2.0.6")]
    [Description("Christmas regardless of the month!")]

    public class Christmas : RustPlugin
    {
        private const string PLAYER_PERM = "christmas.use";
        private static Christmas Instance { get; set; }
        System.Random rand = new System.Random();

        #region Oxide
        private void OnServerInitialized()
        {
            lang.RegisterMessages(Messages, this);
            Instance = this;
            permission.RegisterPermission(PLAYER_PERM, this);
            if (!configData.Automation.enabled)
                return;

            ConVar.XMas.enabled = true;
            ConVar.XMas.spawnRange = configData.Automation.playerDistance;
            ConVar.XMas.giftsPerPlayer = configData.Automation.giftsPerPlayer;

            configData.Automation.RandomTimerMin = Mathf.Max(1, configData.Automation.RandomTimerMin);
            configData.Automation.RandomTimerMax = Mathf.Max(2, configData.Automation.RandomTimerMax);
            if (configData.Automation.EnableTimedEvents)
                StartTimedEvent();
        }

        private void Unload()
        {
            Puts("Disabling the Christmas event...");
            ConVar.XMas.enabled = false;
            Instance = null;
        }
        #endregion

        #region Functions
        private void StartTimedEvent()
        {
            timer.Repeat(rand.Next(configData.Automation.RandomTimerMin, configData.Automation.RandomTimerMax), 0, () =>
            {
                if (RefillPresents())
                    if (configData.Automation.messagesEnabled)
                        SendChatMessage("Christmas Message");
            });
        }

        public bool RefillPresents()
        {
            if (!configData.Automation.enabled)
            {
                PrintWarning("Plugin is disabled in config.");
                return false;
            }

            if (BasePlayer.activePlayerList.Count < configData.Automation.MinRequiredPlayers)
            {
                PrintWarning("Skipping Christmas : Not enough players.");
                return false;
            }

            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "xmas.refill");
            return true;
        }
        #endregion

        #region Commands
        [ChatCommand("gift")]
        private void GiftsCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.userID.ToString(), "christmas.use"))
                return;

            bool fail = false;

            if (player.IsAdmin && configData.ManualSettings.AdminBypass)
            {
                ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "xmas.refill");
                if (configData.Automation.messagesEnabled)
                    SendChatMessage("Christmas Message");
                return;
            }

            // Check for CCTV Amount Required
            if (configData.ManualSettings.cctv > 0)
            {
                int cctv_amount = player.inventory.GetAmount(634478325);
                if (cctv_amount < configData.ManualSettings.cctv)
                {
                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player.transform.position);
                    SendReply(player, string.Format(msg("RequireCCTV"), configData.ManualSettings.cctv));
                }
            }
            // Check for Targeting Computer amount required
            if (configData.ManualSettings.computer > 0)
            {
                int computer_amount = player.inventory.GetAmount(1523195708);
                if (computer_amount < configData.ManualSettings.computer)
                {
                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player.transform.position);
                    SendReply(player, string.Format(msg("RequireLaptop"), configData.ManualSettings.computer));
                    fail = true;
                }
            }
            // Check for Santa Hat is required
            if (configData.ManualSettings.santahat)
            {
                int santahat_amount = player.inventory.GetAmount(-575483084);
                if (santahat_amount < 1)
                {
                    Effect.server.Run("assets/prefabs/locks/keypad/effects/lock.code.denied.prefab", player.transform.position);
                    SendReply(player, msg("RequireSantaHat"));
                    fail = true;
                }
            }

            if (fail)
                return;
            else
            {
                if (RefillPresents())
                {
                    player.inventory.Take(null, 634478325, 1);
                    player.inventory.Take(null, 1523195708, 1);
                    player.inventory.Take(null, -575483084, 1);
                    if (configData.Automation.messagesEnabled)
                        SendChatMessage("Christmas Message");
                }
            }
        }

        [ConsoleCommand("gift")]
        private void GiftsConsole(ConsoleSystem.Arg arg)
        {
            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), "xmas.refill");
            if (configData.Automation.messagesEnabled)
                SendChatMessage("Christmas Message");
        }
        #endregion

        #region Config
        private static ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Event Automation Settings")]
            public AutomationOptions Automation { get; set; }
            [JsonProperty(PropertyName = "Authorized Calling Settings")]
            public ManualOptions ManualSettings { get; set; }

            public class AutomationOptions
            {
                [JsonProperty(PropertyName = "Enable Christmas Gifts Event Plugin")]
                public bool enabled { get; set; }
                [JsonProperty(PropertyName = "Enable random autospawn Christmas Gifts Events on random timer")]
                public bool EnableTimedEvents { get; set; }
                [JsonProperty(PropertyName = "Minimum required players to start events")]
                public int MinRequiredPlayers { get; set; }
                [JsonProperty(PropertyName = "Minimum time in-between presents and stocking refills (seconds)")]
                public int RandomTimerMin { get; set; }
                [JsonProperty(PropertyName = "Maximum time in-between presents and stocking refills (seconds)")]
                public int RandomTimerMax { get; set; }
                [JsonProperty(PropertyName = "Distance a player in which to spawn")]
                public int playerDistance { get; set; }
                [JsonProperty(PropertyName = "Gifts per player")]
                public int giftsPerPlayer { get; set; }
                [JsonProperty(PropertyName = "Broadcast Message enabled to players when gifts sent (true/false)")]
                public bool messagesEnabled { get; set; }
            }

            public class ManualOptions
            {
                [JsonProperty(PropertyName = "Allow Admin to bypass required items to call gift command")]
                public bool AdminBypass { get; set; }
                [JsonProperty(PropertyName = "How many CCTV's needed")]
                public int cctv { get; set; }
                [JsonProperty(PropertyName = "How many Targeting Computer's needed")]
                public int computer { get; set; }
                [JsonProperty(PropertyName = "Require Santa Hat (true/false)")]
                public bool santahat { get; set; }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Automation = new ConfigData.AutomationOptions
                {
                    enabled = true,
                    EnableTimedEvents = true,
                    MinRequiredPlayers = 5,
                    RandomTimerMin = 5400,
                    RandomTimerMax = 7200,
                    playerDistance = 50,
                    giftsPerPlayer = 5,
                    messagesEnabled = true
                },
                ManualSettings = new ConfigData.ManualOptions
                {
                    AdminBypass = true,
                    cctv = 1,
                    computer = 1,
                    santahat = true
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new Core.VersionNumber(2, 0, 0))
                configData = baseConfig;

            if (configData.Version < new Core.VersionNumber(2, 0, 5))
            {
                configData.Automation.enabled = true;
                configData.Automation.EnableTimedEvents = true;
                configData.Automation.MinRequiredPlayers = 5;
                configData.Automation.RandomTimerMin = 5400;
                configData.Automation.RandomTimerMax = 7200;
                configData.ManualSettings.AdminBypass = true;
            }

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
        #endregion

        #region Localization
        private static void SendChatMessage(string key, params object[] args)
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];
                player.ChatMessage(args != null ? string.Format(msg(key, player.UserIDString), args) : msg(key, player.UserIDString));
            }
        }

        private static string msg(string key, string playerId = null) => Instance.lang.GetMessage(key, Instance, playerId);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["Christmas Message"] = "This world has been blessed with Christmas Presents!",
            ["RequireCCTV"] = "<color=red>[WARNING]</color> You lack the required <color=orange>({0})</color> CCTV's",
            ["RequireLaptop"] = "<color=red>[WARNING]</color> You lack the required <color=orange>({0})</color> Targeting Computer's",
            ["RequireSantaHat"] = "<color=red>[WARNING]</color> You lack the required Santa Hat"
        };
        #endregion
    }
}