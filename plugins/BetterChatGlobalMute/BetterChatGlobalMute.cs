// Requires: BetterChat

using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Better Chat Global Mute", "Whispers88", "1.0.3")]
    [Description("Allows players to toggle all Better Chat messages globally")]
    public class BetterChatGlobalMute : CovalencePlugin
    {
        [PluginReference]
        private Plugin BetterChat;

        private HashSet<string> GlobalChatMute = new HashSet<string>();

        void OnServerInitialized()
        {
            if (!BetterChat)
            {
                PrintWarning("BetterChat not detected");
            }

        }

        [Command("mutechat"), Permission("betterchatglobalmute.allowed")]
        private void MuteGlobalChat(IPlayer player, string command, string[] args)
        {
            GlobalChatMute.Add(player.Id);
            player.Reply(lang.GetMessage("Chat Muted", this, player.Id));
        }

        [Command("unmutechat"), Permission("betterchatglobalmute.allowed")]
        private void UnMuteGlobalChat(IPlayer player, string command, string[] args)
        {
            GlobalChatMute.Remove(player.Id);
            player.Reply(lang.GetMessage("Chat Unmuted", this, player.Id));
        }

        object OnBetterChat(Dictionary<string, object> messageData)
        {
            List<string> blockedReceivers = (List<string>)messageData["BlockedReceivers"];

            foreach (var player in covalence.Players.Connected)
            {
                if (GlobalChatMute.Contains(player.Id))
                {
                    blockedReceivers.Add(player.Id);
                }
            }

            messageData["BlockedReceivers"] = blockedReceivers;

            return messageData;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Chat Muted"] = "You have muted the Global Chat",
                ["Chat Unmuted"] = "You have unmuted the Global Chat"

            }, this, "en");
        }
    }
}
