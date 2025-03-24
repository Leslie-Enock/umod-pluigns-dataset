using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Troll System", "Grave", "0.6.0")]
    [Description("A easier way to troll someone in Rust! The commands available are banning and muting, /tmute, /tunmute, /tban, /tunban, and they all annouce in the chat. Example: /tmute Bill - Announces Grave muted Bill, as I sent the command. This works with unmuting and banning/unbanning.")]
    class TrollSystem : CovalencePlugin
    {
        private void Init()
        {
            permission.RegisterPermission("trollsystem.ban", this);
            permission.RegisterPermission("trollsystem.unban", this);
            permission.RegisterPermission("trollsystem.mute", this);
            permission.RegisterPermission("trollsystem.unmute", this);
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BanMessage"] = "{Banner} banned {Bannedname}",
                ["UnBanMessage"] = "{UnBanner} unbanned {Bannedname}",
                ["MuteMessage"] = "{Muter} muted {Mutedname}",
                ["UnMuteMessage"] = "{UnMuter} unmuted {Mutedname}"
            }, this);
        }
       

        [Command("tban"),Permission("trollsystem.ban")]
        private void Cmdtban(IPlayer player, string command, string[] args)
        {
            server.Broadcast($"{player.Name} banned {string.Join(" ", args)}");
            string message = lang.GetMessage("BanMessage", this, player.Id)
     .Replace("{Banner}", player.Name)
     .Replace("{Bannedname}", string.Join(" ", args));
            Puts(message);
        }

        [Command("tunban"),Permission("trollsystem.unban")]
        private void Cmdtunban(IPlayer player, string command, string[] args)
        {
            server.Broadcast($"{player.Name} unbanned {string.Join(" ", args)}");
            string message = lang.GetMessage("UnBanMessage", this, player.Id)
     .Replace("{UnBanner}", player.Name)
     .Replace("{Bannedname}", string.Join(" ", args));
            Puts(message);
        }

        [Command("tmute"),Permission("trollsystem.mute")]
        private void Cmdtmute(IPlayer player, string command, string[] args)
        {
            server.Broadcast($"{player.Name} muted {string.Join(" ", args)}");
            string message = lang.GetMessage("MuteMessage", this, player.Id)
                .Replace("{Muter}", player.Name)
                .Replace("{Mutedname}", string.Join(" ", args));
            Puts(message);

        }
        [Command("tunmute"), Permission("trollsystem.unmute")]
        private void Cmdtunmute(IPlayer player, string command, string[] args)
        {
            server.Broadcast($"{player.Name} unmuted {string.Join(" ", args)}");
            string message = lang.GetMessage("UnMuteMessage", this, player.Id)
                .Replace("{UnMuter}", player.Name)
                .Replace("{Mutedname}", string.Join(" ", args));
            Puts(message);

        }

    }
}
