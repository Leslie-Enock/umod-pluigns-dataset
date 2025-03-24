using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Plugins;
using System;

namespace Oxide.Plugins
{
    [Info("Private Messages GUI", "Tricky/TurtleInBlack", "1.1.0")]
    [Description("Shows the sent messages of the PrivateMessages plugin as game tips.")]

    public class PrivateMessagesGUI : CovalencePlugin
    {
        #region References

        // Required dependency.
        [PluginReference]
        private Plugin PrivateMessages;

        #endregion

        #region Fields

        // Configuration.
        private static Configuration config;

        #endregion

        #region Configuration

        /// <summary>
        /// Provides the configuration options.
        /// </summary>
        private class Configuration
        {
            [JsonProperty(PropertyName = "Game Tip Duration In Seconds")]
            public float GameTipDuration { get; set; }

            [JsonProperty(PropertyName = "Show To Sender")]
            public bool ShowToSender { get; set; }

            [JsonProperty(PropertyName = "Show To Receiver")]
            public bool ShowToReceiver { get; set; }
        }

        /// <summary>
        /// Provides the default values for each configuration key.
        /// </summary>
        private Configuration DefaultConfig()
        {
            return new Configuration
            {
                GameTipDuration = 10f,
                ShowToSender = false,
                ShowToReceiver = true,
            };
        }

        /// <summary>
        /// Loads the configuration file.
        /// </summary>
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch (Exception exception)
            {
                PrintError(exception.ToString());
                PrintWarning("Failed to load the configuration file.");
                return;
            }
            SaveConfig();
        }

        /// <summary>
        /// Creates a configuration file and populates it with the required default values.
        /// </summary>
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file.");
            config = DefaultConfig();
        }

        /// <summary>
        /// Saves the configuration file.
        /// </summary>
        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }

        #endregion

        #region Initialization and Quitting

        /// <summary>
        /// Called after the server startup has been completed and is awaiting connections or when a plugin is hot loaded.
        /// </summary>
        private void OnServerInitialized()
        {
            // Send a warning if the dependency is not present or not loaded.
            if (PrivateMessages == null || !PrivateMessages.IsLoaded)
                PrintWarning("PrivateMessages is not installed and is required to use this plugin. Get it at http://umod.org.");
        }

        /// <summary>
        /// Called when a plugin is being unloaded.
        /// </summary>
        private void Unload()
        {
            // Nullify the configuration to leave no traces behind.
            config = null;
        }

        #endregion

        #region Custom Hooks

        private void OnPMProcessed(IPlayer sender, IPlayer receiver, string message)
        {
            // Show the game tip to the sender if enabled.
            if (config.ShowToSender)
                SendGameTip(sender, GetMessage("PMTo", sender.Id, receiver.Name, message));

            // Show the game tip to the receiver if enabled.
            if (config.ShowToReceiver)
                SendGameTip(receiver, GetMessage("PMFrom", receiver.Id, sender.Name, message));
        }

        #endregion

        #region Functions

        /// <summary>
        /// Creates a game tip and sends it to the target player.
        /// </summary> 
        /// <param name="player"> The player to whom the game tip is to be sent. </param>
        /// <param name="message"> The given message in the game tip. </param>
        private void SendGameTip(IPlayer player, string message)
        {
            // Strip the formatting tags off the message and convert it into plain text. Note: This doesn't affect the text size tag.
            message = Formatter.ToPlaintext(message);

            // Send the game tip.
            player.Command("gametip.showgametip", message);
            // If the player is still alive, hide the game tip after a set interval of time in seconds.
            timer.Once(config.GameTipDuration, () => player?.Command("gametip.hidegametip"));
        }

        #endregion

        #region Helper Functions

        /// <summary>
        /// Gets the localized and formatted message from the localization file.
        /// </summary>
        /// <param name="messageKey"> The message key. </param>
        /// <param name="playerId"> The player to whom the message is to be sent. </param>
        /// <param name="args"> Any additional arguments required in the message. </param>
        /// <returns> The localized message for the stated key. </returns>
        private string GetMessage(string messageKey, string playerId = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(messageKey, PrivateMessages, playerId), args);
            }
            catch (Exception exception)
            {
                PrintError(exception.ToString());
                throw;
            }
        }

        #endregion
    }
}