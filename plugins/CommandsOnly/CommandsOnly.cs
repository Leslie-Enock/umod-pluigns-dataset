using System.Collections.Generic;
using ConVar;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Commands Only", "varygoode", "1.0.2")]
    [Description("Only allow chat messages that are commands")]
    class CommandsOnly : CovalencePlugin
    {
        #region Fields

        private const string PermBypass = "commandsonly.bypass";

        #endregion Fields

        #region Init

        private void Init()
        {
            permission.RegisterPermission(PermBypass, this);
        }

        #endregion Init

        #region Hooks

        private object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (!player.IPlayer.HasPermission(PermBypass))
            {
                player.ChatMessage(lang.GetMessage("CommandsOnly", this, player.IPlayer.Id));
                return false;
            }

            return null;
        }

        private Dictionary<string, object> OnBetterChat(Dictionary<string, object> data)
        {
            string message = (string)data?["Message"];
            if (!string.IsNullOrEmpty(message) && message[0] != '/')
            {
                data["CancelOption"] = 1;
            }
            return data;
        }

        #endregion Hooks

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandsOnly"] = "You may only use commands in the chat."
            }, this);
        }

        #endregion Localization
    }
}
