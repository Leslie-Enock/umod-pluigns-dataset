using System.Collections.Generic;
using System.Collections;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Core;
using System;

namespace Oxide.Plugins
{

	[Info("Scheduled Building", "5Dev24", "1.0.1")]
	[Description("Spawns prefabs on timers")]
	public class ScheduledBuilding : RustPlugin
	{

		private const string GetPositionPermission = "scheduledbuilding.getposition";
		private const string CreateNewPrefabPermission = "scheduledbuilding.createprefab";
		private Coroutine routine;
		private ConfigData data;

		#region Hooks

		private void Init()
		{
			this.permission.RegisterPermission(ScheduledBuilding.GetPositionPermission, this);
			this.permission.RegisterPermission(ScheduledBuilding.CreateNewPrefabPermission, this);
		}

		private void OnServerInitialized()
		{
			this.timer.Once(1f, this.StartRoutine);
		}

		private void Unload()
		{
			if (this.routine != null)
			{
				ServerMgr.Instance?.StopCoroutine(this.routine);

				foreach (ConfigData.PrefabData prefab in this.data.Prefabs)
					if (prefab.Name != null)
					{
						GameObject found = GameObject.Find(prefab.Name);
						if (found != null)
							GameObject.Destroy(found);
					}

				this.routine = null;
			}
		}

		#endregion

		#region Commands

		[ChatCommand("getposition")]
		private void GetPositionCommand(BasePlayer player, string cmd, string[] args)
		{
			if (!this.HasPermission(player, ScheduledBuilding.GetPositionPermission))
				SendReply(player, this.lang.GetMessage("No Permission", this, player.UserIDString));
			else
			{
				Vector3 position = player.transform.position;
				Quaternion rotation = player.eyes.rotation;
				SendReply(player, this.lang.GetMessage("Position Format", this, player.UserIDString)
					.Replace("{x1}", position.x.ToString()).Replace("{y1}", position.y.ToString())
					.Replace("{z1}", position.z.ToString()).Replace("{x2}", rotation.x.ToString())
					.Replace("{y2}", rotation.y.ToString()).Replace("{z2}", rotation.z.ToString())
					.Replace("{w2}", rotation.w.ToString())); // Nasty
			}
		}

		[ChatCommand("createprefab")]
		private void CreatePrefabCommand(BasePlayer player, string cmd, string[] args)
		{
			if (!this.HasPermission(player, ScheduledBuilding.CreateNewPrefabPermission))
				SendReply(player, this.lang.GetMessage("No Permission", this, player.UserIDString));
			else
			{
				Vector3 position = player.transform.position;
				Quaternion rotation = player.eyes.rotation;

				List<ConfigData.PrefabData> prefabsList = new List<ConfigData.PrefabData>(this.data.Prefabs);
				prefabsList.Add(new ConfigData.PrefabData
				{
					Location = position,
					Rotation = rotation
				});
				ConfigData.PrefabData[] prefabs = prefabsList.ToArray();
				prefabsList = null;

				Config.WriteObject(new ConfigData
				{
					Prefabs = prefabs
				}, true);

				SendReply(player, this.lang.GetMessage("Created Prefab", this, player.UserIDString)
					.Replace("{x}", position.x.ToString()).Replace("{y}", position.y.ToString())
					.Replace("{z}", position.y.ToString()));
			}
		}

		[ChatCommand("showprefabs")]
		private void ShowPrefabsCommand(BasePlayer player, string cmd, string[] args)
		{
			if (!player.IsAdmin)
				SendReply(player, this.lang.GetMessage("No Permission", this, player.UserIDString));
			else
			{
				foreach (ConfigData.PrefabData prefab in this.data.Prefabs)
					player.SendConsoleCommand("ddraw.sphere", 30, UnityEngine.Color.cyan, prefab.Location, 1f);

				SendReply(player, this.lang.GetMessage("Shown Prefabs", this, player.UserIDString));
			}
		}

		#endregion

		#region Routine

		private void StartRoutine()
		{
			if (this.routine == null)
				this.routine = ServerMgr.Instance?.StartCoroutine(Spawn());
		}

		private IEnumerator Spawn()
		{
			if (this.data == null || this.data.Prefabs == null || this.data.Prefabs.Length == 0)
				yield break;

			while (this.IsLoaded)
			{
				foreach (ConfigData.PrefabData prefab in this.data.Prefabs)
				{
					if (prefab.Prefab == null)
						continue;

					long now = Now();
					if (prefab.NextSpawnAt > now)
						continue;

					if (prefab.PreviousCheck && prefab.Name != null)
					{
						GameObject foundObject = GameObject.Find(prefab.Name);
						if (foundObject != null)
							continue;
					}

					object hook = Interface.CallHook("CanSpawnScheduledPrefab", prefab.Location, prefab.Rotation, prefab.Prefab, prefab.Interval, prefab.PreviousCheck, prefab.ShouldSave);
					if (hook is bool && !((bool) hook))
						continue;

					GameObject obj = GameManager.server.CreatePrefab(prefab.Prefab, prefab.Location, prefab.Rotation);
					if (obj == null)
					{
						yield return new WaitForEndOfFrame();
						continue;
					}

					string name = $"{obj.GetInstanceID()}-prefab";
					prefab.Name = name;
					obj.name = name;

					BaseEntity entity = obj.GetComponent<BaseEntity>();

					if (entity != null)
					{
						entity.EnableSaving(prefab.ShouldSave);
						entity.Spawn();
						entity.UpdateNetworkGroup();
						entity.SendNetworkUpdateImmediate(true);
					}

					Interface.CallHook("SpawnedScheduledPrefab", obj, entity);
	
					prefab.NextSpawnAt = now + prefab.Interval;
					yield return new WaitForEndOfFrame();
				}

				yield return new WaitForSeconds(0.25f);
			}
		}

		#endregion

		#region Configuration

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"No Permission", "You don't have permission to use this command"},
				{"Position Format", "You're at {x1} {y1} {z1} looking {x2} {y2} {z2} {w2}"},
				{"Created Prefab", "Added new, empty prefab to config at {x} {y} {z}"},
				{"Shown Prefabs", "All prefabs are now shown around the map for the next 30 seconds"}
			}, this, "en");

			lang.RegisterMessages(new Dictionary<string, string>
			{
				{"No Permission", "Vous n'êtes pas authorisé à utiliser cette commande"},
				{"Position Format", "Vous êtes à {x1} {y1} {z1} en regardant {x2} {y2} {z2} {w2}"},
				{"Created Prefab", "Ajout d'un nouveau préfabriqué vide à la configuration à {x} {y} {z}"},
				{"Shown Prefabs", "Tous les préfabriqués sont maintenant affichés sur la carte pendant les 30 prochaines seconds"}
			}, this, "fr");
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			this.data = Config.ReadObject<ConfigData>();
		}

		protected override void LoadDefaultConfig()
		{
			ConfigData cfg = new ConfigData();
			Config.WriteObject(cfg, true);
			this.data = cfg;
		}

		internal class ConfigData
		{
			internal class PrefabData
			{
				[JsonConverter(typeof(Converter))]
				public Vector3 Location = Vector3.zero;

				[JsonConverter(typeof(Converter))]
				public Quaternion Rotation = Quaternion.identity;

				public string Prefab = null;

				[JsonProperty("Spawn rate (in seconds)")]
				public uint Interval = 3600;

				[JsonProperty("Check for previous")]
				public bool PreviousCheck = true;

				[JsonProperty("Should save")]
				public bool ShouldSave = false;

				[JsonIgnore]
				public long NextSpawnAt = -1;

				[JsonIgnore]
				public string Name = null;
			}

			public PrefabData[] Prefabs = new PrefabData[0];

			public string Version = "1.0.0";
		}

		#endregion

		#region Converters

		private class Converter : JsonConverter
		{
			public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
			{
				if (reader.TokenType != JsonToken.String)
					return null;

				if (objectType == typeof(Vector3))
				{
					string[] splits = reader.Value.ToString().Split(' ');
					float[] xyz = new float[3];
					for (int i = 0; i < 3; i++)
						if (!float.TryParse(splits[i], out xyz[i]))
							return Vector3.zero;

					return new Vector3(xyz[0], xyz[1], xyz[2]);
				}
				else if (objectType == typeof(Quaternion))
				{
					string[] splits = reader.Value.ToString().Split(' ');
					float[] xyzw = new float[4];
					for (int i = 0; i < 4; i++)
						if (!float.TryParse(splits[i], out xyzw[i]))
							return Quaternion.identity;

					return new Quaternion(xyzw[0], xyzw[1], xyzw[2], xyzw[3]);
				}

				return null;
			}

			public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
			{
				if (value is Vector3)
				{
					Vector3 vector = (Vector3) value;
					writer.WriteValue($"{vector.x} {vector.y} {vector.z}");
				}
				else if (value is Quaternion)
				{
					Quaternion rotation = (Quaternion) value;
					writer.WriteValue($"{rotation.x} {rotation.y} {rotation.z} {rotation.w}");
				}
			}

			public override bool CanConvert(Type objectType) =>
				objectType == typeof(Vector3) || objectType == typeof(Quaternion);
		}

		#endregion

		#region Helpers

		private long Now() => DateTimeOffset.Now.ToUnixTimeSeconds();

		private bool HasPermission(BasePlayer player, string permission) =>
			player.IsAdmin || this.permission.UserHasPermission(player.UserIDString, permission);

		#endregion

	}
}