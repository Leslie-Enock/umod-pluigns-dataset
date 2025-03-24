using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Mass Genocide", "Ryz0r", "1.0.2")]
    [Description("Allows players with permission to kill all players at once.")]
    public class MassGenocide : RustPlugin
    {
        private const string NoKillPerm = "massgenocide.nokill";
        private const string UsePerm = "massgenocide.use";

        private void Init()
        {
            permission.RegisterPermission(NoKillPerm, this);
            permission.RegisterPermission(UsePerm, this);
            AddCovalenceCommand("genocide", nameof(GenocideCommand));
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GenocideComing"] = "The <color=red>genocide</color> is coming...",
                ["NoPerm"] = "You do not have the required permissions to use this command.",
                ["ConsoleStarts"] = "You have initiated the Genocide...",
                ["CountdownMessage"] = "The genocide is happening in {0} seconds!",
                ["NowHappening"] = "The mass genocide is now happening!"
            }, this);
        }

        private void GenocideCommand(IPlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.Id, UsePerm) || player.IsAdmin)
            {
                player.Message(lang.GetMessage("ConsoleStarts", this), "[Mass Genocide]");

                var countdown = 5;
                timer.Repeat(1f, countdown, () =>
                {
                    player.Message(string.Format(lang.GetMessage("CountdownMessage", this), countdown), "[Mass Genocide]");
                    countdown--;
                    if (countdown != 0) return;
                    Server.Broadcast(lang.GetMessage("GenocideComing", this, player.Id));
                    player.Message(lang.GetMessage("NowHappening", this),"[Mass Genocide]");
                    foreach (var b in BasePlayer.activePlayerList)
                    {
                        if (permission.UserHasPermission(b.UserIDString, NoKillPerm)) continue;
                        b.Die();
                    }
                });
            }
            else
            {
                player.Message(lang.GetMessage("NoPerm", this, player.Id));
            }
        }
    }
}