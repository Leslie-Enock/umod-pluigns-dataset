using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

namespace Oxide.Plugins
{
	[Info("Sleeper Count", "NubbbZ", "1.2.1")]
	[Description("Returns the total number of sleepers!")]
	class SleeperCount : CovalencePlugin
	{
		private void Init()
		{
			permission.RegisterPermission("sleepercount.use", this);
		}

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["denied"] = "You don't have permission to use that command!",
				["none"] = "There are currently no sleepers on the server!",
				["single"] = "There is {0} sleeping player!",
				["multiple"] = "There are {0} sleeping players!"
			}, this);
		}

		[Command("sleepers")]
		private void TestCommand(IPlayer player, string command, string[] args)
		{
			int Sleeper_Count = BasePlayer.sleepingPlayerList.Count;

			if (player.HasPermission("sleeperlist.use"))
			{
				if (BasePlayer.sleepingPlayerList.Count == 0)
				{
					player.Reply(lang.GetMessage("none", this, player.Id));
				} 
				else
				{
					if (BasePlayer.sleepingPlayerList.Count == 1) 
					{
						player.Reply(string.Format(lang.GetMessage("single", this, player.Id), Sleeper_Count.ToString()));
					} 
					else 
					{
						player.Reply(string.Format(lang.GetMessage("multiple", this, player.Id), Sleeper_Count.ToString()));
					}
				}
			}
			else
			{
				player.Reply(lang.GetMessage("denied", this, player.Id));
			}
		}
	}
}