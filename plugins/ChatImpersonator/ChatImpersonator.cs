using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Chat Impersonator", "LaserHydra", "3.0.0")]
    [Description("Allows you to impersonate another player in chat")]
    public class ChatImpersonator : CovalencePlugin
    {
        private const string Permission = "chatimpersonator.use";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Syntax"] = "Syntax: /ci <player> <message>",
                ["Player Not Found"] = "The player could not be found."
            }, this);
        }

        [Command("ci"), Permission(Permission)]
        private void Cmd_Impersonate(IPlayer player, string command, string[] args)
        {
            if (args.Length < 2)
            {
                SendLocalizedReply(player, "Syntax");
                return;
            }
            
            var targetPlayer = FindPlayer(args[0]);

            if (targetPlayer == null)
            {
                SendLocalizedReply(player, "Player Not Found");
                return;
            }

            ForcePlayerChat(targetPlayer, string.Join(" ", args.Skip(1).ToArray()));
        }

        private void ForcePlayerChat(BasePlayer target, string message) =>
            target.SendConsoleCommand($"chat.say \"{message}\"");

        private void SendLocalizedReply(IPlayer player, string key) => 
            player.Reply(lang.GetMessage(key, this, player.Id));

        private BasePlayer FindPlayer(string nameOrId) =>
            covalence.Players.FindPlayer(nameOrId)?.Object as BasePlayer;
    }
}