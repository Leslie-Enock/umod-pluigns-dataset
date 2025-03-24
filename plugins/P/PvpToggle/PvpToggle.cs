using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("PvP Toggle", "0x89A", "1.1.5")]
    [Description("Allows PvP to be toggled on and off ")]
    class PvpToggle : CovalencePlugin 
    {
        private bool pvpActive;

        private const string canuse = "pvptoggle.use";

        void Init()
        {
            permission.RegisterPermission(canuse, this);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> 
            { 
                ["SyntaxError"] = "Syntax error: '/pvp <true/false>'",
                ["SetActive"] = "Pvp is now enabled",
                ["SetInactive"] = "Pvp is now disabled",
                ["NoPermission"] = "You do not have permission to use this command"
            }, this);
        }

        [Command("pvp")]
        void ConsoleToggle(IPlayer player, string command, string[] args)
        {
            if (player.IsAdmin || permission.UserHasPermission(player.Id, canuse))
            {
                if (args.Length == 1)
                {
                    TogglePvp(player, args[0]);
                }
                else
                {
                    player.Message(lang.GetMessage("SyntaxError", this, player.Id));
                }

                return;
            }

            player.Message(lang.GetMessage("NoPermission", this, player.Id));
        }

        void TogglePvp(IPlayer player, string args)
        {
            bool set;
            if (!bool.TryParse(args, out set))
            {
                player.Message(lang.GetMessage("SyntaxError", this));
                return;
            }

            pvpActive = set;

            player.Message(lang.GetMessage(pvpActive ? "SetActive" : "SetInactive", this, player.Id));
        }

        object OnEntityTakeDamage(BasePlayer player, HitInfo hitInfo)
        {
            if (hitInfo != null && ((hitInfo.Initiator != null && hitInfo.Initiator.IsNpc) || (hitInfo.HitEntity != null && hitInfo.HitEntity.IsNpc)))
            {
                return null;
            }
            
            if (hitInfo == null || (!pvpActive && hitInfo.Initiator != null && !hitInfo.Initiator.IsNpc))
            {
                return true;
            }

            return null;
        }
    }
}