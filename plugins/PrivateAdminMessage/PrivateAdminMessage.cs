using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Private Admin Message", "waayne", "0.2.0")]
    [Description("Allows admins to send private messages to players via console/chat")]
    internal class PrivateAdminMessage : CovalencePlugin
    {
        private const string PRIVATE_ADMIN_MESSAGE = "privateadminmessage.use";
        private const string PRIVATE_ADMIN_MESSAGE_RECEIVE = "privateadminmessage.receive";

        private void Init()
        {
            permission.RegisterPermission(PRIVATE_ADMIN_MESSAGE_RECEIVE, this);

            AddCovalenceCommand("privateadminmessage.pam", nameof(AdminToPlayerMessageCommand), PRIVATE_ADMIN_MESSAGE);
            AddCovalenceCommand("pam", nameof(AdminToPlayerMessageCommand), PRIVATE_ADMIN_MESSAGE);
            AddCovalenceCommand("am", nameof(PlayerToAdminMessageCommand));
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SentYouToUser"] = "You to {0}: {1}",
                ["SentAdminToYou"] = "Admin to you: {0}\n(reply via /am <message>)",
                ["SentYouToAdmin"] = "You to Admin: {0}",
                ["SentUserToAdmin"] = "{0} to Admin: {1}",
                ["NoUserFoundOrOnline"] = "User not found or is currently not online",
                ["NoMessageProvided"] = "No message was provided",
                ["NoAdminOnline"] = "There is currently no admin online",
                ["Help"] = "Use /pam <name> <message> to write a message to an admin"
            }, this);
        }

        private void AdminToPlayerMessageCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length <= 0)
            {
                player.Reply(lang.GetMessage("Help", this, player.Id));
            }
            else if (args.Length == 1)
            {
                player.Reply(lang.GetMessage("NoMessageProvided", this, player.Id));
            }
            else if (args.Length > 1)
            {
                string receiverName = args[0];

                string message = args[1];
                for (var i = 2; i < args.Length; i++)
                    message += " " + args[i];

                IPlayer receiver = null;
                foreach (IPlayer connectedPlayer in covalence.Players.Connected)
                {
                    if (connectedPlayer.Name == receiverName)
                        receiver = connectedPlayer;
                }

                if (receiver == null)
                {
                    player.Reply(lang.GetMessage("NoUserFoundOrOnline", this, player.Id));
                    return;
                }

                if (!receiver.IsConnected)
                {
                    player.Reply(lang.GetMessage("NoUserFoundOrOnline", this, player.Id));
                    return;
                }

                receiver.Reply(string.Format(lang.GetMessage("SentAdminToYou", this, receiver.Id), message));
                player.Reply(string.Format(lang.GetMessage("SentYouToUser", this, player.Id), receiverName, message));
            }
        }

        private void PlayerToAdminMessageCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length <= 0)
            {
                player.Reply(lang.GetMessage("NoMessageProvided", this, player.Id));
                return;
            }
            
            string message = args[0];
            for (var i = 1; i < args.Length; i++)
                message += " " + args[i];

            var wasSent = false;

            foreach (IPlayer connectedPlayer in covalence.Players.Connected)
            {
                if (!connectedPlayer.HasPermission(PRIVATE_ADMIN_MESSAGE_RECEIVE))
                    continue;

                connectedPlayer.Reply(string.Format(lang.GetMessage("SentUserToAdmin", this, connectedPlayer.Id),
                    player.Name, message));

                wasSent = true;
            }

            player.Reply(wasSent
                ? string.Format(lang.GetMessage("SentYouToAdmin", this, player.Id), message)
                : string.Format(lang.GetMessage("NoAdminOnline", this, player.Id)));

            Log(string.Format(lang.GetMessage("SentUserToAdmin", this), player.Name, message));
        }
    }
}