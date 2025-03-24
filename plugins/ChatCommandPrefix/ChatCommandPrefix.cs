using System.Collections.Generic;
using ConVar;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Chat Command Prefix", "Clearshot", "1.0.0")]
    [Description("Add prefix for all chat commands")]
    class ChatCommandPrefix : CovalencePlugin
    {
        private PluginConfig _config;

        private object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (player != null && !string.IsNullOrEmpty(message) && _config.prefixes.Contains(message[0]))
            {
                Interface.CallHook("IOnPlayerCommand", player, "/" + message.Remove(0, 1));
                return false;
            }
            return null;
        }

        private Dictionary<string, object> OnBetterChat(Dictionary<string, object> data)
        {
            string message = (string)data?["Message"];
            if (!string.IsNullOrEmpty(message) && _config.prefixes.Contains(message[0]))
            {
                data["CancelOption"] = 1;
            }
            return data;
        }

        #region Config

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        private class PluginConfig
        {
            public char[] prefixes = { '!' };
        }

        #endregion
    }
}
