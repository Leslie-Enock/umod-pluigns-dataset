using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("BetterChatIgnore", "MisterPixie", "1.0.3")]
    [Description("Players can ignore chat messages from other players")]
    public class BetterChatIgnore : CovalencePlugin
    {
        [PluginReference]
        private Plugin BetterChat, Ignore;

        void OnServerInitialized()
        {
            if (!BetterChat)
            {
                PrintWarning("BetterChat not detected");
            }

            if (!Ignore)
            {
                PrintWarning("Ignore API not detected");
            }
        }

        object OnBetterChat(Dictionary<string, object> messageData)
        {
            if (!Ignore)
            {
                PrintWarning("Ignore API not detected");
                return messageData;
            }

            IPlayer playerSendingMessage = (IPlayer)messageData["Player"];

            List<string> blockedReceivers = (List<string>)messageData["BlockedReceivers"];

            foreach (var player in covalence.Players.Connected)
            {
                var hasIgnored = Ignore?.CallHook("HasIgnored", player.Id, playerSendingMessage.Id);
                if (hasIgnored != null && (bool)hasIgnored)
                {
                    blockedReceivers.Add(player.Id);
                }
            }

            messageData["BlockedReceivers"] = blockedReceivers;

            return messageData;
        }
    }
}