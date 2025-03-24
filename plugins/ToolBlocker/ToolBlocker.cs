using System.Collections.Generic;

namespace Oxide.Plugins
{
	[Info("Tool Blocker", "NubbbZ", "1.0.0")]
	[Description("Blocks the use of certain tools on gather of flesh, trees, ore")]
	class ToolBlocker : CovalencePlugin
	{
		#region Variables
		List<object> FleshBlockedTools;
		List<object> TreeBlockedTools;
		List<object> NodeBlockedTools;

		private const string bypass = "toolblocker.bypass";
		#endregion

		#region Configuration
		protected override void LoadDefaultConfig()
		{
			LogWarning("Creating a new configuration file");

			Config["corpses"] = new List<string>() {
			"knife.bone",
			"knife.combat"
			};
			Config["trees"] = new List<string>() {
			"stonehatchet",
			"hatchet",
			"axe.salvaged"
			};
			Config["nodes"] = new List<string>() {
			"stone.pickaxe",
			"pickaxe",
			"icepick.salvaged"
			};
		}
		#endregion

		#region Localization
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["Blocked"] = "You are not allowed to use a {0} to gather {1}!"
			}, this);
		}
		#endregion

		#region Hook
		private void Init()
		{
			permission.RegisterPermission(bypass, this);
		}
		object OnMeleeAttack(BasePlayer player, HitInfo info)
		{
			FleshBlockedTools = (List<object>)Config["corpses"];
			TreeBlockedTools = (List<object>)Config["trees"];
			NodeBlockedTools = (List<object>)Config["nodes"];
			string tool = player.GetHeldEntity().GetItem().info.displayName.english;

			if (player.IPlayer.HasPermission(bypass) == false)
			{
				if (info.HitEntity != null)
				{
					if (info.HitEntity.HasTrait(BaseEntity.TraitFlag.Alive) == false)
					{
						if (info.HitEntity.GetType().FullName == "BaseCorpse")
						{
							if (FleshBlockedTools.Contains(player.GetHeldEntity().GetItem().info.shortname))
							{
								player.IPlayer.Reply(string.Format(lang.GetMessage("Blocked", this, player.IPlayer.Id), tool, "Corpses"));
								return true;
							}
						}
						if (info.HitEntity.GetType().FullName == "TreeEntity")
						{
							string Gather = info.HitEntity.GetComponent<ResourceDispenser>().gatherType.ToString();
							if (TreeBlockedTools.Contains(player.GetHeldEntity().GetItem().info.shortname))
							{
								player.IPlayer.Reply(string.Format(lang.GetMessage("Blocked", this, player.IPlayer.Id), tool, Gather));
								return true;
							}
						}
						if (info.HitEntity.GetType().FullName == "OreResourceEntity")
						{
							string Gather = info.HitEntity.GetComponent<ResourceDispenser>().gatherType.ToString();
							if (NodeBlockedTools.Contains(player.GetHeldEntity().GetItem().info.shortname))
							{
								player.IPlayer.Reply(string.Format(lang.GetMessage("Blocked", this, player.IPlayer.Id), tool, Gather));
								return true;
							}
						}
					}
				}
			}
			return null;
		}
		#endregion
	}
}