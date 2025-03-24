using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Teleport", "Orf1", "1.0.1")]
    [Description("Lets players with permission teleport to any specified position.")]
    class Teleport : CovalencePlugin
    {
        private void Init()
        {
            permission.RegisterPermission("teleport.use", this);
            LoadDefaultMessages();
        }
        [Command("teleport")]
        private void SetCommand(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission("teleport.use"))
            {
                if (args.Length == 3)
                {
                    try
                    {
                        float x = Int32.Parse(args[0]);
                        float y = Int32.Parse(args[1]);
                        float z = Int32.Parse(args[2]);
                        
                        var newPos = new GenericPosition(x, y, z);
                        player.Teleport(newPos);

                        string message = lang.GetMessage("Success", this, player.Id);
                        player.Message(message);
                    }
                    catch (FormatException)
                    {
                        string message = lang.GetMessage("InvalidUsage", this, player.Id);
                        player.Message(message);
                    }
                }
                else
                {
                    string message = lang.GetMessage("InvalidUsage", this, player.Id);
                    player.Message(message);
                }
            }
            else
            {
                string message = lang.GetMessage("NoPermission", this, player.Id);
                player.Message(message);
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Success"] = "You have teleported successfully.",
                ["NoPermission"] = "You do not have permission to use this command!",
                ["InvalidUsage"] = "Invalid usage. /teleport [x] [y] [z]"
            }, this);
        }
    }
}