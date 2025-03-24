using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Graffiti Restriction", "Turtle In Black", "1.1.0")]
    [Description("Restricts drawing graffiti on buildings where players don't have building privilege.")]
    public class GraffitiRestriction : RustPlugin
    {
        #region Fields

        // Declare the bypass permission.
        private const string permBypass = "graffitirestriction.bypass";

        #endregion

        #region Initialization

        private void Init()
        {
            // Register the permission.
            permission.RegisterPermission(permBypass, this);
        }

        #endregion

        #region Hooks

        private object OnSprayCreate(SprayCan spray, Vector3 position, Quaternion rotation)
        {
            // Store the player holding the spray tool inside a variable.
            BasePlayer player = spray.GetOwnerPlayer();

            // Skip if the player has the bypass permission.
            if (permission.UserHasPermission(player.UserIDString, permBypass))
                return null;

            // Prevent drawing graffiti if the player doesn't have building privilege.
            if (player.IsBuildingBlocked())
            {
                SendReply(player, GetLang(Lang.NoGraffiti, player.UserIDString));
                return false;
            }

            // Otherwise, allow drawing graffiti.
            return null;
        }

        #endregion

        #region Localization

        /// <summary>
        /// Provides the keys to the messages.
        /// </summary>
        private static class Lang
        {
            public const string NoGraffiti = "No Graffiti";
        }

        /// <summary>
        /// Registers and populates the language file with the default messages.
        /// </summary>
        protected override void LoadDefaultMessages()
        {
            // English language.
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [Lang.NoGraffiti] = "You don't have permission to spray here."
            }, this, "en");
        }

        /// <summary>
        /// Gets the localized and formatted message from the localization file.
        /// </summary>
        /// <param name="messageKey"> The message key. </param>
        /// <param name="playerId"> The player to whom the message is to be sent. </param>
        /// <param name="args"> Any additional arguments required in the message. </param>
        /// <returns> The localized message for the stated key. </returns>
        private string GetLang(string messageKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(messageKey, this, playerId), args);
        }

        #endregion
    }
}