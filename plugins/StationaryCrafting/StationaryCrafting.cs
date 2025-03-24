using System.Collections.Generic;

namespace Oxide.Plugins
{
	[Info("Stationary Crafting", "NubbbZ", "1.0.2")]
	[Description("Craft only when standing next to a workbench")]
	class StationaryCrafting : CovalencePlugin
	{
		#region Variables
		HashSet<ulong> InWorkbenchRadius = new HashSet<ulong>();
		#endregion

		#region Setup
		protected override void LoadDefaultConfig()
		{
			LogWarning("Creating a new configuration file");
			Config["ShowMessages"] = false;
		}

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["inofrange"] = "Stay in the workbench radius, or your crafting queue will be canceled!",
				["outofrange"] = "You cant craft if you arent in workbench radius!",
				["canceled"] = "You have left the workbench range so your crafting queue was canceled!"
			}, this);
		}
		#endregion

		#region Hooks
		private void OnEntityEnter(TriggerWorkbench triggerWorkbench, BasePlayer player)
		{
			InWorkbenchRadius.Add(player.userID);

			if ((bool)Config["ShowMessages"] == true)
			{
				player.IPlayer.Reply(lang.GetMessage("inofrange", this, player.IPlayer.Id));
			}
		}

		bool CanCraft(ItemCrafter itemCrafter, ItemBlueprint bp, int amount)
		{
			BasePlayer player = itemCrafter.GetComponent<BasePlayer>();

			if (InWorkbenchRadius.Contains(player.userID) == false)
			{
				if ((bool)Config["ShowMessages"] == true)
				{
					player.IPlayer.Reply(lang.GetMessage("outofrange", this, player.IPlayer.Id));
				}
				return false;
			}
			return true;
		}

		private void OnEntityLeave(TriggerWorkbench triggerWorkbench, BasePlayer player)
		{
			InWorkbenchRadius.Remove(player.userID);
			if ((bool)Config["ShowMessages"] == true)
			{
				player.IPlayer.Reply(lang.GetMessage("canceled", this, player.IPlayer.Id));
			}
			player.inventory.crafting.CancelAll(true);
		}
		#endregion
	}
}
