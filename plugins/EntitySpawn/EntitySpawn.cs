using Newtonsoft.Json;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Entity Spawn", "Wolfleader101", "1.0.0")]
	[Description("Throw a projectile and find a surprise!")]
	class EntitySpawn : RustPlugin
	{
		#region Variables
		private struct spawnItemConfig
		{
			public List<string> prefabs { get; set; }
			public bool enabled { get; set; }
			public bool random { get; set; }
			public string permission { get; set; }
		}

		private PluginConfig config;

		#endregion

		#region Hooks
		private void Init()
		{
			config = Config.ReadObject<PluginConfig>();

			permission.RegisterPermission("entityspawn.use", this);

			foreach (var item in config.NewItem.Values)
			{
				if (!string.IsNullOrEmpty(item.permission))
				{
					permission.RegisterPermission(item.permission, this);
				}
			}
		}

		void OnPlayerAttack(BasePlayer attacker, HitInfo info)
		{
			if (info.IsProjectile())
			{
				string EntName = info.ProjectilePrefab.name;
				foreach (var item in config.NewItem)
				{
					string projectileName = item.Key + ".projectile";

					if (!string.IsNullOrEmpty(item.Value.permission))
					{
						SpawnChecks(item.Value.permission, projectileName, EntName, item, attacker, info);
					}
					else
					{
						SpawnChecks("entityspawn.use", projectileName, EntName, item, attacker, info);
					}
				}
			}
		}
		#endregion

		#region Custom Methods

		void SpawnChecks(string perm, string projectileName, string EntName, KeyValuePair<string, spawnItemConfig> item, BasePlayer attacker, HitInfo info)
		{
			if (permission.UserHasPermission(attacker.UserIDString, perm))
			{
				if (EntName == projectileName && item.Value.enabled)
				{
					string PrefabAsset;

					if (item.Value.random)
					{
						System.Random random = new System.Random();
						int i = random.Next(0, item.Value.prefabs.Count);
						PrefabAsset = item.Value.prefabs[i];
					}
					else
					{
						PrefabAsset = item.Value.prefabs[0];
					}
					SpawnIn(attacker, info, PrefabAsset);
				}
			}
		}
		void SpawnIn(BasePlayer attacker, HitInfo info, string PrefName)
		{
			Vector3 EntLoc = info.HitPositionWorld;
			Quaternion playerRotQuat = attacker.GetNetworkRotation();
			float playerRotY = playerRotQuat.eulerAngles.y;


			BaseEntity NewEntity = GameManager.server.CreateEntity(PrefName, EntLoc, new Quaternion());
			NewEntity.ServerRotation = Quaternion.Euler(0, playerRotY, 0);
			NewEntity.Spawn();
			info.ProjectilePrefab.remainInWorld = false;
		}

		#endregion

		#region Config

		private class PluginConfig
		{
			[JsonProperty("Spawns")]
			public Dictionary<string, spawnItemConfig> NewItem { get; set; }
		}

		private PluginConfig GetDefaultConfig()
		{
			return new PluginConfig
			{
				NewItem = new Dictionary<string, spawnItemConfig>()
				{
					{
						"snowball",
						new spawnItemConfig {
							prefabs = new List<string>() {"assets/prefabs/npc/scientist/htn/scientist_full_any.prefab"},
							enabled = false,
							random = false,
							permission = "snowball.spawn"
						}
					}
				}
			};
		}

		protected override void LoadDefaultConfig()
		{
			Config.WriteObject(GetDefaultConfig(), true);
		}
		#endregion
	}
}