using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Vehicle BBQ's", "Judess69er/Shady14u", "1.3.6")]
	[Description("Puts Barbeque's on specific Vehicles")]
	public class VehicleBBQ : RustPlugin
	{
		#region Fields
		public const string BBQ_PREFAB = "assets/bundled/prefabs/static/bbq.static.prefab";
		public PluginConfig _config;
		#endregion Fields
		#region Initialization
		void OnServerInitialized(bool initialBoot)
		{
			LoadConfig();
			foreach (var vehicle in UnityEngine.Object.FindObjectsOfType<BaseVehicle>())
			{
				AddOven(vehicle);
			}
		}
        #endregion
		#region Config
		protected override void LoadDefaultConfig()
		{
			Config.WriteObject(GetDefaultConfig(), true);
		}
		public PluginConfig GetDefaultConfig()
		{
			return new PluginConfig
			{
				EnableRHIB = false,
				EnableWorkcart = false,
				EnableScrapHeli = false
			};
		}
		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<PluginConfig>() ?? GetDefaultConfig();
				SaveConfig();
			}
			catch
			{
				PrintWarning("Creating new config file.");
				LoadDefaultConfig();
			}
		} 
		protected override void SaveConfig() => Config.WriteObject(_config);
		public class PluginConfig
		{
			[JsonProperty(PropertyName = "Add BBQ to Rigid Hull Inflatable Boat ( True/False )")]
			public bool EnableRHIB { get; set; }
			
			[JsonProperty(PropertyName = "Add BBQ to Workcart ( True/False )")]
			public bool EnableWorkcart { get; set; }
			
			[JsonProperty(PropertyName = "Add BBQ to Scrap Transport Helicopter ( True/False )")]
			public bool EnableScrapHeli { get; set; }
		}
		#endregion Config
		#region Oxide Hooks
		private static void RemoveColliderProtection(BaseEntity colliderEntity)
		{
			foreach (var meshCollider in colliderEntity.GetComponentsInChildren<MeshCollider>())
			{
			UnityEngine.Object.DestroyImmediate(meshCollider);
			}
			UnityEngine.Object.DestroyImmediate(colliderEntity.GetComponent<GroundWatch>());
		}
		void OnEntitySpawned(BaseVehicle entity)
		{
			if (entity == null) return;
			NextTick(() =>
			{
				AddOven(entity);
			});
		}
		#endregion Oxide Hooks
		#region Component
		void AddOven(BaseVehicle entity)
		{
			if (entity == null || entity.GetComponentsInChildren<BaseOven>(true).Any(child => child.name == BBQ_PREFAB))
			{
				return;
			}
			// Spawn BBQ on Rigid Hull Inflatable Boat
			if (entity.ShortPrefabName.Contains("rhib") && _config.EnableRHIB)
			{
				SpawnOven(entity, new Vector3(0.0f, 0.95f, 3.5f), new Vector3(0.0f, 90.0f, 0.0f));
				return;
			}
			// Spawn BBQ on Workcart
			if (entity.ShortPrefabName.Contains("workcart") &&  _config.EnableWorkcart)
			{
				SpawnOven(entity, new Vector3(0.85f, 1.4f, -0.6f), new Vector3(0.0f, 90.0f, 0.0f));
				return;
			}
			// Spawn BBQ on Scrap Transport Helicopter
			if (entity.ShortPrefabName.Contains("scraptransporthelicopter") && _config.EnableScrapHeli)
			{
				SpawnOven(entity, new Vector3(-0.85f, 0.6f, -2.6f), new Vector3(0.0f, 0.0f, 0.0f));
				return;
			}
		}
		private void SpawnOven(BaseVehicle entity, Vector3 localPosition, Vector3 rotate)
		{
			var oven = GameManager.server?.CreateEntity(BBQ_PREFAB, entity.transform.position) as BaseOven;
			if (oven == null) return;
			RemoveColliderProtection(oven);
			oven.Spawn();
			oven.SetParent(entity);
			oven.pickup.enabled = false;
			oven.dropsLoot = true;
			oven.transform.localPosition = localPosition;
			oven.transform.Rotate(rotate);
			oven.SendNetworkUpdateImmediate(true);
		}
		// drop items when entity is killed (don't want salty tears)
		private void OnEntityKill(BaseVehicle entity)
		{
			if (entity == null) return;
			foreach (var child in entity.GetComponentsInChildren<BaseOven>(true))
			{
				if (child.name != BBQ_PREFAB) continue;
				child.DropItems();
				child.Kill();
			}
		}
		#endregion Component
		#region Console Command
		[ConsoleCommand("killbbqs")]
		private void cmdKillBBQs(ConsoleSystem.Arg arg)
		{
			foreach (var vehicle in UnityEngine.Object.FindObjectsOfType<BaseVehicle>())
			{
				foreach (var child in vehicle.GetComponentsInChildren<BaseOven>(true).Where(x=>x.name==BBQ_PREFAB))
				{
					child.DropItems();
					child.Kill();
				}
			}
		}
		#endregion Console Command
	}
}