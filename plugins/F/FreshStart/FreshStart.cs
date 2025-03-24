using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using System.Linq;
using System.Collections;

namespace Oxide.Plugins
{
	[Info("FreshStart", "Yoshi", "1.1.1")]
	[Description("Removes all entities when killed by another player")]
	//shoutout to Vice for cleaning up the plugin

	class FreshStart : RustPlugin
	{
		private const string PermissionName = "freshstart.excluded";


		#region config
		private Configuration config;
		public class Configuration
		{
			[JsonProperty(PropertyName = "remove corpses on death (true/false)")]
			public bool NoCorpses = false;

			[JsonProperty(PropertyName = "kill everything belonging to the player (true/false)")]
			public bool KillAllEntitiesOnDeath = true;

			[JsonProperty(PropertyName = "only run if the player is killed by another player (true/false)")]
			public bool LimitToPVPDeath = true;

			[JsonProperty(PropertyName = "wipes all their boxes on death (true/false)")]
			public bool DespawnLoot = true;

		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				config = Config.ReadObject<Configuration>();
				if (config == null) throw new Exception();
				SaveConfig();
			}
			catch
			{
				PrintError("Your configuration file contains an error. Using default configuration values.");
				LoadDefaultConfig();
			}
		}

		protected override void SaveConfig() => Config.WriteObject(config);

		protected override void LoadDefaultConfig() => config = new Configuration();

		#endregion

		#region Hooks
		void Init()
		{
			permission.RegisterPermission(PermissionName, this);
		}

		object OnPlayerDeath(BasePlayer player, HitInfo info)
		{
			if (player.IPlayer.HasPermission(PermissionName)) return null;

			if (config.LimitToPVPDeath && !(info.Initiator is BasePlayer) || info.InitiatorPlayer.userID == player.userID)
				return null;


			ServerMgr.Instance.StartCoroutine(ClearEntities(player));


			return null;
		}

		public IEnumerator ClearEntities(BasePlayer player)
		{
			int batch = 0;
			foreach (var ent in BaseNetworkable.serverEntities.Where(x => (x as BaseEntity).OwnerID == player.userID).ToList())
			{
				if (config.DespawnLoot)
				{
					if (ent is StorageContainer)
					{
						var container = ent.GetComponent<StorageContainer>();

						container.inventory.Clear();
					}
				}

				if (config.KillAllEntitiesOnDeath)
				{
					ent.Kill();
				}


				if (++batch % 20 == 0)
				{
					yield return null;
				}
			}

		}


		void OnEntitySpawned(PlayerCorpse entity)
		{
			if (!config.NoCorpses) return;
			entity.Kill();
		}
		#endregion
	}
}