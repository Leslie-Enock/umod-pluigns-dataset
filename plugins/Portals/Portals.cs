using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Collections;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
	[Info("Portals", "LaserHydra", "3.1.0")]
	[Description("Allows to place Portals which you can step into to teleport")]
	public class Portals : RustPlugin
	{
		private const int PortalLayer = 3;

		private const string UsagePermission = "portals.use";
		private const string AdminPermission = "portals.admin";

		private Configuration _config;
		private static Portals _instance;

		#region Hooks

		private void OnServerInitialized()
		{
			_instance = this;

			permission.RegisterPermission(UsagePermission, this);
			permission.RegisterPermission(AdminPermission, this);

			LoadConfig();

			foreach (Portal portal in _config.Portals)
			{
				if (portal.RequiresIndiviualPermission)
					permission.RegisterPermission(portal.GetUsagePermission(), this);

				portal.Spawn();
			}
		}

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList)
				player.GetComponent<PlayerTeleporter>()?.Remove();

			foreach (Portal portal in _config.Portals)
				portal.Destroy();
		}

		private void OnPlayerDisconnected(BasePlayer player, string reason) =>
			player.GetComponent<PlayerTeleporter>()?.Remove();

		#endregion

		#region Commands

		[ChatCommand("portal")]
		private void PortalCommand(BasePlayer player, string cmd, string[] args)
		{
			if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
			{
				SendMessage(player, "No Command Permission");
				return;
			}

			if (args.Length == 0)
			{
				SendMessage(player, "Command Syntax");
				return;
			}

			switch (args[0].ToLower())
			{
				case "list":
					{
						if (_config.Portals.Count == 0)
						{
							SendMessage(player, "No Existing Portals");
							return;
						}

						string[] portalNames = _config.Portals
							.Select(p => p.Name)
							.ToArray();

						SendMessage(player, "Portal List", string.Join(", ", portalNames));
					}

					break;

				case "remove":
					{
						if (args.Length == 2)
						{
							string name = args[1];

							Portal portal = _config.Portals.FirstOrDefault(p => p.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));

							if (portal == null)
							{
								SendMessage(player, "Portal Doesn't Exist", name);
								return;
							}

							portal.Destroy();

							_config.Portals.Remove(portal);
							SaveConfig();

							SendMessage(player, "Portal Removed", name);
						}
						else
						{
							PortalPointBehaviour portalPoint = GetPortalPointInView(player);

							if (portalPoint == null)
							{
								SendMessage(player, "Not Looking At Portal (Remove)");
								return;
							}

							portalPoint.Portal.RemovePoint(portalPoint.Position);

							SaveConfig();

							SendMessage(player, "Portal Point Removed");
						}
					}

					break;

				case "info":
					{
						PortalPointBehaviour portalPoint = GetPortalPointInView(player);

						if (portalPoint == null)
						{
							SendMessage(player, "Not Looking At Portal (Info)");
							return;
						}

						SendMessage(player, "Portal Point Info", portalPoint.Portal.Name, portalPoint.PointType);
					}

					break;

				case "entr":
				case "entrance":
				case "exit":
					{
						if (args.Length < 2 || args.Length > 3)
						{
							SendMessage(player, "Command Syntax");
							return;
						}

						string name = args[1];
						bool removeOthers = false;

						if (args.Length == 3)
						{
							string tag = args[1];
							if (!tag.Equals("-r", StringComparison.InvariantCultureIgnoreCase))
							{
								SendMessage(player, "Invalid Tag", tag);
								return;
							}

							name = args[2];
							removeOthers = true;
						}

						Portal portal = _config.Portals.FirstOrDefault(p => p.Name.Equals(name, StringComparison.InvariantCultureIgnoreCase));

						if (portal == null)
						{
							portal = new Portal { Name = name };
							_config.Portals.Add(portal);

							SendMessage(player, "Portal Created", name);
						}

						if (args[0].Equals("exit", StringComparison.InvariantCultureIgnoreCase))
						{
							if (removeOthers)
							{
								portal.RemovePointsOfType(Portal.PointType.Exit);
								SendMessage(player, "Exits Cleared", name);
							}

							portal.Exits.Add(player.transform.position);
							SendMessage(player, "Exit Added", name);
						}
						else
						{
							if (removeOthers)
							{
								portal.RemovePointsOfType(Portal.PointType.Entrance);
								SendMessage(player, "Entrances Cleared", name);
							}

							portal.Entrances.Add(player.transform.position);
							SendMessage(player, "Entrance Added", name);
						}

						portal.Spawn();

						SaveConfig();
					}

					break;

                default:
                    SendMessage(player, "Command Syntax");
                    break;
            }
		}

		#endregion

		#region Helper Methods

		private static void SendMessage(BasePlayer player, string key, params object[] args) =>
			_instance.PrintToChat(player, _instance.lang.GetMessage(key, _instance, player.UserIDString), args);

		private static PortalPointBehaviour GetPortalPointInView(BasePlayer player)
		{
			RaycastHit hit;

			if (Physics.Raycast(player.eyes.HeadRay(), out hit, 10))
			{
				var portalPoint = hit.collider.GetComponent<PortalPointBehaviour>();

				if (portalPoint != null)
					return portalPoint;
			}

			return null;
		}

		private static void Teleport(BasePlayer player, Vector3 destination)
		{
			if (player.net?.connection != null)
				player.ClientRPCPlayer(null, player, "StartLoading");

			player.StartSleeping();
			player.MovePosition(destination);

			if (player.net?.connection != null)
				player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);

			player.UpdateNetworkGroup();
			player.SendNetworkUpdateImmediate(false);

			if (player.net?.connection == null)
				return;

			try
			{
				player.ClearEntityQueue(null);
			}
			catch
			{ }

			player.SendFullSnapshot();
		}

		#endregion

		#region Localization

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				// Commands
				["No Command Permission"] = "You don't have permission to use this command.",
				["Command Syntax"] = string.Join(Environment.NewLine, new [] {
					"/portal list -- <i><color=#c0c0c0> list existing portal names</color></i>",
					"/portal entrance|entr|exit [-r] <portal> -- <i><color=#c0c0c0>add new entrance or exit to a portal; with '-r' all other entrances/exits are removed</color></i>",
					"/portal info -- <i><color=#c0c0c0>show information about the portal entrance/exit you are looking at</color></i>",
					"/portal remove -- <i><color=#c0c0c0>remove the portal entrance/exit you are looking at</color></i>",
					"/portal remove <portal> -- <i><color=#c0c0c0>remove the entire portal with the specified name</color></i>"
				}),
				["Invalid Tag"] = "Invalid tag '{0}', please check the command syntax again.",

				["No Existing Portals"] = "There aren't any portals.",
				["Portal List"] = "Portal names: {0}",

				["Not Looking At Portal (Remove)"] = "You are not looking at a portal entrance or exit. If you want to remove an entire portal, use '/portal remove <portal>'",
				["Not Looking At Portal (Info)"] = "You are not looking at a portal entrance or exit. If you want to list portals use '/portal list'",
				["Portal Doesn't Exist"] = "Portal with the name '{0}' does not exist.",

				["Portal Point Removed"] = "Successfully removed the entrance/exit you were looking at.",
				["Portal Removed"] = "Successfully removed portal '{0}'.",

				["Portal Point Info"] = string.Join(Environment.NewLine, new[] {
					"Portal Name: {0}",
					"Type: {1}",
				}),

				["Portal Created"] = "Successfully created a new portal with the name '{0}'.",
				["Entrances Cleared"] = "All entrances for portal '{0}' were removed.",
				["Exits Cleared"] = "All exits for portal '{0}' were removed.",
				["Entrance Added"] = "A new entrance was added to portal '{0}'.",
				["Exit Added"] = "A new exit was added to portal '{0}'.",

				// Usage
				["No Usage Permission"] = "You don't have permission to enter this portal.",
				["Teleporting Shortly"] = "You will be teleported in {0} seconds.",
				["Teleportation Cancelled"] = "The teleportation was cancelled."
			}, this);
		}

		#endregion

		#region Configuration

		protected override void LoadConfig()
		{
			base.LoadConfig();
			_config = Config.ReadObject<Configuration>();
			SaveConfig();
		}

		protected override void LoadDefaultConfig() => _config = new Configuration();

		protected override void SaveConfig() => Config.WriteObject(_config);

		private class Configuration
		{
			[JsonProperty("Sphere Radius")]
			public float SphereRadius { get; set; } = 1f;

			[JsonProperty("Sphere Density (Enterable)")]
			public ushort SphereEntityCount { get; set; } = 3;

			[JsonProperty("Sphere Density (Not Enterable)")]
			public ushort ExitSphereEntityCount { get; set; } = 1;

			public HashSet<Portal> Portals { get; set; } = new HashSet<Portal>();
		}

		#endregion

		#region Portals and teleportation

		private class PlayerTeleporter : MonoBehaviour
		{
			public BasePlayer Player { get; private set; }

			public bool IsRunning { get; private set; }

			public Vector3 Destination { get; set; }
			public int Seconds { get; set; }

			public bool ReachedDestination { get; set; }

			public void Start()
			{
				if (IsRunning)
					return;

				StartCoroutine(nameof(Teleport));
				IsRunning = true;
				ReachedDestination = false;
			}

			public void Stop()
			{
				StopCoroutine(nameof(Teleport));
				IsRunning = false;
			}

			public void Remove() => Destroy(this);

			private void Awake() => Player = GetComponent<BasePlayer>();

			private IEnumerator Teleport()
			{
				yield return new WaitForSecondsRealtime(Seconds);

				IsRunning = false;

				Portals.Teleport(Player, Destination);
			}
		}

		private class PortalPointBehaviour : MonoBehaviour
		{
			private SphereEntity[] _sphereEntities = null;

			public Portal Portal { get; private set; }
			public Portal.PointType PointType { get; private set; }
			public Vector3 Position { get; private set; }

			private bool _isEnterable;
			private Portal.PointType _opposingPointType;
			private float _radius;
			private ushort _sphereEntityCount;

			private void Awake()
			{
				gameObject.layer = Portals.PortalLayer;

				var rigidbody = gameObject.AddComponent<Rigidbody>();
				rigidbody.useGravity = false;
				rigidbody.isKinematic = true;
				rigidbody.detectCollisions = true;
				rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

				var collider = gameObject.AddComponent<SphereCollider>();
				collider.radius = _radius;
				collider.isTrigger = true;
				collider.enabled = true;

				SpawnSphereEntities();
			}

			private void SpawnSphereEntities()
			{
				if (_sphereEntities != null)
					return;

				_sphereEntities = new SphereEntity[_sphereEntityCount];

				for (int i = 0; i < _sphereEntityCount; i++)
				{
					var sphere = GameManager.server.CreateEntity("assets/prefabs/visualization/sphere.prefab", transform.position)
						.GetComponent<SphereEntity>();

					sphere.currentRadius = _radius * 2;
					sphere.lerpRadius = _radius * 2;
					sphere.lerpSpeed = 1f;
					sphere.Spawn();

					_sphereEntities[i] = sphere;
				}
			}
			
			private void OnTriggerEnter(Collider collider)
			{
				var teleporter = collider.gameObject.GetComponent<PlayerTeleporter>();
				if (teleporter != null && !teleporter.ReachedDestination && !teleporter.Player.IsSleeping())
				{
					teleporter.ReachedDestination = true;
					return;
				}

				if (!_isEnterable)
					return;

				BasePlayer player = collider.gameObject.GetComponent<BasePlayer>();

				if (player != null && !player.IsSleeping())
				{
					if (Portal.HasPermission(player))
					{
						var destination = Portal.GetRandomPoint(_opposingPointType);

						if (destination != null)
							Teleport(player, (Vector3)destination);
					}
					else
					{
						Portals.SendMessage(player, "No Portal Permission");
					}
				}
			}

			private void OnTriggerExit(Collider collider)
			{
				if (!_isEnterable)
					return;

				var teleporter = collider.gameObject.GetComponent<PlayerTeleporter>();

				if (teleporter?.IsRunning ?? false)
				{
					teleporter.Stop();

					Portals.SendMessage(teleporter.Player, "Teleportation Cancelled");
				}
			}

			private void Teleport(BasePlayer player, Vector3 destination)
			{
				var teleporter = player.gameObject.GetComponent<PlayerTeleporter>() 
					?? player.gameObject.AddComponent<PlayerTeleporter>();

				teleporter.Destination = destination;
				teleporter.Seconds = Portal.TeleportationTime;

				teleporter.Start();

				if (Portal.TeleportationTime != 0)
				{
					Portals.SendMessage(player, "Teleporting Shortly", Portal.TeleportationTime);
				}
			}

			public void Destroy()
			{
				if (_sphereEntities != null)
				{
					foreach (var sphereEntity in _sphereEntities)
						sphereEntity.Kill();
				}

				_sphereEntities = null;

				Destroy(gameObject);
			}

			public static PortalPointBehaviour Create(Portal portal, Portal.PointType pointType, Vector3 position)
			{
				var gameObject = new GameObject();
				gameObject.transform.position = position + new Vector3(0, _instance._config.SphereRadius);

				// Deactivate gameObject temporarily to allow initializing of the portal point before Awake()
				gameObject.SetActive(false);

				var behaviour = gameObject.AddComponent<PortalPointBehaviour>();
				behaviour.Portal = portal;
				behaviour.Position = position;
				behaviour.PointType = pointType;
				behaviour._opposingPointType = pointType == Portal.PointType.Entrance
					? Portal.PointType.Exit
					: Portal.PointType.Entrance;
				behaviour._isEnterable = !portal.IsOneWay || pointType == Portal.PointType.Entrance;
				behaviour._radius = _instance._config.SphereRadius;
				behaviour._sphereEntityCount = behaviour._isEnterable
					? _instance._config.SphereEntityCount
					: _instance._config.ExitSphereEntityCount;

				gameObject.SetActive(true);

				return behaviour;
			}
		}

		private class Portal
		{
			private readonly List<PortalPointBehaviour> _portalPoints = new List<PortalPointBehaviour>();

			public string Name { get; set; }

			public bool IsOneWay { get; set; } = true;
			public bool RequiresIndiviualPermission { get; set; } = false;
			public int TeleportationTime { get; set; } = 0;

			public List<Vector3> Entrances { get; set; } = new List<Vector3>();
			public List<Vector3> Exits { get; set; } = new List<Vector3>();

			public bool HasPermission(BasePlayer player) => Portals._instance.permission.UserHasPermission(player.UserIDString, GetUsagePermission());

			public string GetUsagePermission() => RequiresIndiviualPermission
				? $"{Portals.UsagePermission}.{this.Name}"
				: Portals.UsagePermission;

			public void Spawn()
			{
				Destroy();

				foreach (var position in Entrances)
					_portalPoints.Add(PortalPointBehaviour.Create(this, PointType.Entrance, position));

				foreach (var position in Exits)
					_portalPoints.Add(PortalPointBehaviour.Create(this, PointType.Exit, position));
			}

			public void Destroy()
			{
				if (_portalPoints.Count == 0)
					return;

				foreach (var portalPoint in _portalPoints)
					portalPoint.Destroy();

				_portalPoints.Clear();
			}

			public void RemovePoint(Vector3 position)
			{
				if (Entrances.Contains(position))
				{
					DestroyPointBehaviour(position);
					Entrances.Remove(position);
				}
				else if (Exits.Contains(position))
				{
					DestroyPointBehaviour(position);
					Exits.Remove(position);
				}
			}

			public void RemovePointsOfType(PointType pointType)
			{
				if (pointType == PointType.Entrance)
				{
					for (int i = Entrances.Count - 1; i >= 0; i--)
					{
						RemovePoint(Entrances[i]);
					}
				}
				else if (pointType == PointType.Exit)
				{
					for (int i = Exits.Count - 1; i >= 0; i--)
					{
						RemovePoint(Exits[i]);
					}
				}
			}

			public Vector3? GetRandomPoint(PointType pointType)
			{
				if (pointType == PointType.Entrance)
				{
					if (Entrances.Count == 0)
						return null;

					return Entrances.GetRandom((uint)DateTime.UtcNow.Millisecond);
				}

				if (Exits.Count == 0)
					return null;

				return Exits.GetRandom((uint)DateTime.UtcNow.Millisecond);
			}

			private void DestroyPointBehaviour(Vector3 position)
			{
				for (int i = _portalPoints.Count - 1; i >= 0; i--)
				{
					PortalPointBehaviour point = _portalPoints[i];

					if (point.Position == position)
					{
						point.Destroy();
						_portalPoints.RemoveAt(i);
						return;
					}
				}
			}

			public enum PointType
			{
				Entrance,
				Exit
			}
		}

		#endregion
	}
}