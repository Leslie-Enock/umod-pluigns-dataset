using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;


namespace Oxide.Plugins
{
    [Info("NPC Vending Reset", "Whispers88", "1.0.9")]
    [Description("Reset all NPC vending machines")]
    public class NPCVendingReset : CovalencePlugin
    {
        private void vendingreset()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var vending = entity as NPCVendingMachine;
                if (vending != null && !vending.IsDestroyed)
                {
                    vending.ClearSellOrders();
                    vending.ClearPendingOrder();
                    vending.inventory.Clear();
                    vending.InstallFromVendingOrders();
                }
            }
        }

        [Command("vendingreset"), Permission("npcvendingreset.allowed")]
        private void Resetvendingmachines(IPlayer player, string command, string[] args)
        {
                vendingreset();
                player.Reply(lang.GetMessage("VendingReset", this, player.Id));
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["VendingReset"] = "Vending Machines have been reset."

            }, this, "en");
        }
    }
}
