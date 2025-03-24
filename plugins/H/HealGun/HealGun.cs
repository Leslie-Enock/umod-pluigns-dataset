using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using ConVar;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{

	[Info("Heal Gun", "Wolfleader101", "1.3.7")]
	[Description("A customizable heal gun")]
	class HealGun : RustPlugin
	{
		#region Variables

		private PluginConfig config;
		public const string HealgunPerms = "healgun.use";
		

		#endregion
		#region Hooks
		private void Init()
		{
			config = Config.ReadObject<PluginConfig>();
			
			permission.RegisterPermission(HealgunPerms, this);
		}
		
		void OnPlayerAttack(BasePlayer attacker, HitInfo info)
		{
			if (info == null) return;
			if (attacker == null) return;
			if (!permission.UserHasPermission(attacker.UserIDString, HealgunPerms)) return;
			var healgun = info.Weapon.ShortPrefabName;
			if (healgun != config.Healgun) return;
			if (!(info.HitEntity is BasePlayer)) return;
			
			info.damageTypes.ScaleAll(0); // disable damage
			var player = info.HitEntity as BasePlayer;

			if (player.IsWounded() && config.CanRevive)
			{
				ServerMgr.Instance.StartCoroutine(WoundTimer(player));
			}
			player.Heal(config.HealAmount);
			player.metabolism.pending_health.value += config.PendingHealAmount;
			
			info.ProjectilePrefab.remainInWorld = false;

		}
		#endregion

		#region Custom Methods

		IEnumerator WoundTimer(BasePlayer player)
		{
			yield return new WaitForSeconds(config.ReviveTime);
			player.StopWounded();

		}

		#endregion

		#region Config
		private class PluginConfig
		{
			[JsonProperty("Healgun")] public string Healgun { get; set; }
			[JsonProperty("Heal Amount")] public float HealAmount { get; set; }
			[JsonProperty("Pending Health Amount")] public float PendingHealAmount { get; set; }
			[JsonProperty("Can Revive")] public bool CanRevive { get; set; }
			[JsonProperty("Revive Time")] public float ReviveTime { get; set; }
		}

		private PluginConfig GetDefaultConfig()
		{
			return new PluginConfig
			{
				Healgun = "nailgun.entity",
				HealAmount = 5f,
				PendingHealAmount  = 10f,
				CanRevive = true,
				ReviveTime = 3f
			};
		}

		protected override void LoadDefaultConfig()
		{
			Config.WriteObject(GetDefaultConfig(), true);
		}

		

		#endregion
	}
}
