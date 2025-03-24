// Requires: Coroutines

using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Name Changer", "birthdates", "1.0.1")]
    [Description("Listen for profile name changes")]
    public class NameChanger : CovalencePlugin
    {
        #region Hooks

        private void OnServerInitialized()
        {
            timer.Every(_config.CheckInterval, CheckProfiles);
        }

        #endregion

        #region Variables

        /// <summary>
        ///     The link for Steam profile's as XML
        /// </summary>
        private const string ProfileURL = "https://steamcommunity.com/profiles/{0}?xml=1";

        /// <summary>
        ///     The regex that captures the name of a XML profile
        /// </summary>
        private Regex NameRegex { get; } = new Regex("(?<=<steamID><!\\[CDATA\\[).*(?=]]>)");

        #endregion

        #region Core Logic

        /// <summary>
        ///     Asynchronously loop through all connected players and check for name changes
        /// </summary>
        private void CheckProfiles()
        {
            var coroutine = Coroutines.Instance.LoopListAsynchronously(this, new Action<IPlayer>(CheckProfile),
                new List<IPlayer>(covalence.Players.Connected), 0.05f, completePerTick: 3, id: "ProfileCheck");
            Coroutines.Instance.StartCoroutine(coroutine);
        }

        /// <summary>
        ///     Asynchronously enqueue a web request to check a player's Steam profile name
        /// </summary>
        /// <param name="player">Target name</param>
        private void CheckProfile(IPlayer player)
        {
            webrequest.Enqueue(string.Format(ProfileURL, player.Id), string.Empty,
                (code, response) => HandleProfile(player, code, response), this);
        }

        /// <summary>
        ///     Handle the web request's response from <see cref="CheckProfile" />
        /// </summary>
        /// <param name="player">Target player</param>
        /// <param name="code">Response code</param>
        /// <param name="response">Response data</param>
        private void HandleProfile(IPlayer player, int code, string response)
        {
            // See if Steam response was valid
            if (code != 200)
            {
                PrintError("Steam servers did not give code 200: {0} ({1})", response, code);
                return;
            }

            // Try match name
            var result = NameRegex.Match(response);
            if (!result.Success)
            {
                PrintError("Failed to retrieve name for: {0} ({1})", player.Name, player.Id);
                return;
            }
            
            // Rename
            var name = result.Value;
            if (player.Name.Equals(name)) return;
            // Notify
            foreach (var target in covalence.Players.Connected)
            {
                var msg = lang.GetMessage("UserNameChanged", this, target.Id);
                if (string.IsNullOrEmpty(msg)) break;
                target.Message(string.Format(msg, player.Name, name));
            }
            player.Rename(name);
            player.Name = name;
        }

        #endregion

        #region Configuration & Localization

        private ConfigFile _config;

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null) LoadDefaultConfig();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"UserNameChanged", "{0} has changed their name to {1}"}
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        private class ConfigFile
        {
            [JsonProperty("Check Interval (seconds)")]
            public float CheckInterval { get; set; }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    CheckInterval = 30f
                };
            }
        }

        #endregion
    }
}