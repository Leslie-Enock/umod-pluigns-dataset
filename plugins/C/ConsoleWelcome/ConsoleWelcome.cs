using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Console Welcome", "Ryz0r", "1.0.0"), Description("Sends a welcome message to the console when a player connects.")]
    public class ConsoleWelcome : RustPlugin
    {
        private static string consolePerm = "consolewelcome.receive";
        private void Init()
        {
            permission.RegisterPermission(consolePerm, this);
        }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null)
                return;
            if (!permission.UserHasPermission(player.UserIDString, consolePerm))
                return;
            
            player.ConsoleMessage(lang.GetMessage("WelcomeMsg", this, player.UserIDString));
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["WelcomeMsg"] = "<size=60><color=white>Your</color><color=blue> Server</color></size>\n<size=15>Welcome to the server! Have fun!</size>\n\n"
            }, this); 
        }
    }
}