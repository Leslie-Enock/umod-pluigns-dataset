using System.Collections.Generic;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Oxide.Core.Plugins;
using System.Linq;
using System;

namespace Oxide.Plugins
{
	[Info("Map My AirDrop", "ColdUnwanted", "0.1.0")]
	[Description("Display a pop-up on Cargo Plane spawn, and a marker on in-game map at drop position.")]

	public class MapMyAirDrop : RustPlugin
	{
		// Configuration
		private bool ConfigChanged;

		// Configuration Variables
		// Supply Drop Marker Configuration
		private float mapmarkerradius;
		private float mapMarkerAlpha;
		private string mapMarkerColor;
		private float mapMarkerLootedAlpha;
		private string mapMarkerLootedColor;

		// Banner Configuration
		private bool displayHudForAll;
		private bool displayGuiForAll;
		private int bannerHUDLimit;
		private Dictionary<string, string> bannerGUIData;
		private Dictionary<string, string> bannerHUDData;

		// Variable Declaration
		// Supply Drop Variables
		private Color markerColor;
		private Color markerLootedColor;
		private Dictionary<BaseEntity, MapMarkerGenericRadius> dropradius = new Dictionary<BaseEntity, MapMarkerGenericRadius>();
		private Dictionary<BaseEntity, bool> lootedornot = new Dictionary<BaseEntity, bool>();
		private Dictionary<BaseEntity, SupplyDrop> entsupply = new Dictionary<BaseEntity, SupplyDrop>();
		private Dictionary<BaseEntity, Vector3> dropposition = new Dictionary<BaseEntity, Vector3>();
		private List<Vector3> supplySignalPosition = new List<Vector3>();

		// Permission Variables
		private const string MapMyAirdropHUD = "mapmyairdrop.hud";
		private const string MapMyAirdropBanner = "mapmyairdrop.banner";

		// HUD Variables
		private Dictionary<BasePlayer, List<string>> HUDlist = new Dictionary<BasePlayer, List<string>>();
		private string hudAnchorMin;
		private string hudAnchorMax;
		private string hudOffsetMin;
		private string hudOffsetMax;
		private bool alreadyUpdating;

		// Banner Variables
		private Dictionary<BasePlayer, List<string>> bannerlist = new Dictionary<BasePlayer, List<string>>();
		private string bannerAnchorMin;
		private string bannerAnchorMax;
		private string bannerOffsetMin;
		private string bannerOffsetMax;

		#region Configuration
		private static Dictionary<string, object> defaultBannerGUI()
		{
			Dictionary<string, object> thisDictGui = new Dictionary<string, object>();
			thisDictGui.Add("Anchor Min", "0.0 0.85");
			thisDictGui.Add("Anchor Max", "1.0 0.90");
			thisDictGui.Add("Offset Min", "0 0");
			thisDictGui.Add("Offset Max", "0 0");

			return thisDictGui;
		}

		private static Dictionary<string, object> defaultBannerHUD()
		{
			Dictionary<string, object> thisDictHud = new Dictionary<string, object>();
			thisDictHud.Add("Anchor Min", "0.1 0.91");
			thisDictHud.Add("Anchor Max", "0.17 0.96");
			thisDictHud.Add("Offset Min", "0 0");
			thisDictHud.Add("Offset Max", "0 0");

			return thisDictHud;
		}

		protected override void LoadDefaultConfig()
		{
			LoadVariables();
		}

		private void LoadVariables()
		{
			// Marker settings
			mapmarkerradius = Convert.ToSingle(GetConfig("Map Marker settings", "The Map Marker Radius On The Map", "1"));
			mapMarkerAlpha = Convert.ToSingle(GetConfig("Map Marker settings", "The Map Marker Alpha On The Map (0 to 1)", "0.4"));
			mapMarkerColor = Convert.ToString(GetConfig("Map Marker settings", "The Map Marker Color On The Map (Hex Code)", "#FF00FF"));
			mapMarkerLootedAlpha = Convert.ToSingle(GetConfig("Map Marker settings", "The Map Marker Looted Alpha On The Map (0 to 1)", "0.4"));
			mapMarkerLootedColor = Convert.ToString(GetConfig("Map Marker settings", "The Map Marker Looted Color On The Map (Hex Code)", "#00FFFF"));

			// Banner settings
			displayHudForAll = Convert.ToBoolean(GetConfig("Banner Settings", "Display Drop HUD For All Users (Overrides 'mapmyairdrop.hud')", "false"));
			displayGuiForAll = Convert.ToBoolean(GetConfig("Banner Settings", "Display Drop Banner For All Users (Overrides 'mapmyairdrop.banner')", "false"));
			Dictionary<string, object> bannerGUIObject = (Dictionary<string, object>)GetConfig("Banner Settings", "Banner GUI Settings", defaultBannerGUI());
			bannerGUIData = new Dictionary<string, string>();
			foreach (KeyValuePair<string, object> obj in bannerGUIObject)
			{
				bannerGUIData.Add(obj.Key, Convert.ToString(obj.Value));
			}
			Dictionary<string, object> bannerHUDObject = (Dictionary<string, object>)GetConfig("Banner Settings", "Banner HUD Settings", defaultBannerHUD());
			bannerHUDData = new Dictionary<string, string>();
			foreach (KeyValuePair<string, object> obj in bannerHUDObject)
			{
				bannerHUDData.Add(obj.Key, Convert.ToString(obj.Value));
			}
			bannerHUDLimit = Convert.ToInt32(GetConfig("Banner Settings", "HUD Banner Display Limit", "5"));

			if (!ConfigChanged)
			{
				return;
			}

			SaveConfig();
			ConfigChanged = false;
		}

		private object GetConfig(string menu, string datavalue, object defaultValue)
		{
			var data = Config[menu] as Dictionary<string, object>;
			if (data == null)
			{
				data = new Dictionary<string, object>();
				Config[menu] = data;
				ConfigChanged = true;
			}
			object value;
			if (!data.TryGetValue(datavalue, out value))
			{
				value = defaultValue;
				data[datavalue] = value;
				ConfigChanged = true;
			}
			return value;
		}
		#endregion

		#region Messages
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				// General
				["ColorError"] = "Hex Conversion Error For {0}, Unable To Convert The Hex: {1}. Using Default Hex Color Instead!",

				["DroppedMsg"] = "Airdrop dropped! Check its position on your MAP (G).",
				["SpawnMsg"] = "A Cargo Plan has spawn. Airdrop will be notified on time.",
				["LootedMsg"] = "Someone is looting a SupplyDrop. Marker changed color.",
				["KilledMsg"] = "A SupplyDrop has despawn.",
				["HUDDistanceMsg"] = "<size=12><color=orange>{0}m.</color></size> away!",
				["HUDAirdropMsg"] = "<color=white>AIRDROP</color><color=black>#</color>{0}\n{1}",
			}, this, "en");
		}
		#endregion

		#region Player Disconnect
		void OnPlayerDisconnected(BasePlayer player, string reason)
		{
			// Destory their banner and hud
			DestroyOneBanner(player);
			DestroyOneHUD(player);
		}

		void DestroyOneBanner(BasePlayer player)
		{
			// Remove banner for that player
			List<string> todel = new List<string>();

			// Check if banner list is empty
			if (bannerlist != null)
			{
				foreach (var playerbanner in bannerlist)
				{
					if (playerbanner.Key == player)
					{
						todel = playerbanner.Value;
					}
				}

				foreach (var item in todel)
				{
					CuiHelper.DestroyUi(player, item);
				}
			}

			bannerlist.Remove(player);
		}

		void DestroyOneHUD(BasePlayer player)
		{
			List<string> todel = new List<string>();
			if (HUDlist != null)
			{
				foreach (var playerhud in HUDlist)
				{
					if (playerhud.Key == player)
					{
						todel = playerhud.Value;
					}
				}

				foreach (var item in todel)
				{
					CuiHelper.DestroyUi(player, item);
				}
			}
			HUDlist.Remove(player);
		}
		#endregion

		#region Player Connect
		void OnPlayerConnected(BasePlayer player)
		{
			// Generate the marker of the current drops in the map
			GenerateMarkers();
			DisplayDropHUD("update");
		}
		#endregion

		#region Unload
		void Unload()
		{
			foreach (var paire in dropradius)
			{
				if (paire.Value != null)
				{
					paire.Value.Kill();
					paire.Value.SendUpdate();
				}
			}

			DestoyAllUi();
		}

		void DestoyAllUi()
		{
			DestroyAllHUD();
			DestroyAllBanner();
		}

		void DestroyAllHUD()
		{
			List<string> todel = new List<string>();

			if (HUDlist != null)
			{
				foreach (var player in BasePlayer.activePlayerList.ToList())
				{
					todel = new List<string>();
					foreach (var playerhud in HUDlist)
					{
						if (playerhud.Key == player)
						{
							todel = playerhud.Value;

						}
					}
					foreach (var item in todel)
					{
						CuiHelper.DestroyUi(player, item);
					}
				}
			}
		}

		void DestroyAllBanner()
		{
			List<string> todel = new List<string>();

			if (HUDlist != null)
			{
				foreach (var player in BasePlayer.activePlayerList.ToList())
				{
					todel = new List<string>();
					foreach (var playerbanner in bannerlist)
					{
						if (playerbanner.Key == player)
						{
							todel = playerbanner.Value;
						}
					}
					foreach (var item in todel)
					{
						CuiHelper.DestroyUi(player, item);
					}
				}
			}
		}
		#endregion

		#region Initialization
		void Init()
		{
			// Load configuration
			LoadVariables();

			// Register Permission
			permission.RegisterPermission(MapMyAirdropHUD, this);
			permission.RegisterPermission(MapMyAirdropBanner, this);

			// Check if the config was setup correctly
			// Cap the range 
			mapMarkerAlpha = Mathf.Clamp(mapMarkerAlpha, 0, 1);
			mapMarkerLootedAlpha = Mathf.Clamp(mapMarkerLootedAlpha, 0, 1);

			// Check if the hex was a legit hex
			string defaultMapMarkerColor = "#FF00FF";
			string defaultMapMarkerLootedColor = "#00FFFF";

			// If it is not legit then just use the default
			if (!ColorUtility.TryParseHtmlString(mapMarkerColor, out markerColor))
			{
				// Use the default color and log it in console
				string message = lang.GetMessage("ColorError", this);
				PrintError(string.Format(message, "Map Marker Color", mapMarkerColor));

				ColorUtility.TryParseHtmlString(defaultMapMarkerColor, out markerColor);
			}

			if (!ColorUtility.TryParseHtmlString(mapMarkerLootedColor, out markerLootedColor))
			{
				// Use the default color and log it in console
				string message = lang.GetMessage("ColorError", this);
				PrintError(string.Format(message, "Map Marker Looted Color", mapMarkerColor));

				ColorUtility.TryParseHtmlString(defaultMapMarkerLootedColor, out markerLootedColor);
			}

			// Convert the dictionary for the banner gui to it's respective data
			if (!bannerGUIData.TryGetValue("Anchor Min", out bannerAnchorMin))
			{
				object thisObj;
				defaultBannerGUI().TryGetValue("Achor Min", out thisObj);
				bannerAnchorMin = Convert.ToString(thisObj);
			}

			if (!bannerGUIData.TryGetValue("Anchor Max", out bannerAnchorMax))
			{
				object thisObj;
				defaultBannerGUI().TryGetValue("Achor Max", out thisObj);
				bannerAnchorMax = Convert.ToString(thisObj);
			}

			if (!bannerGUIData.TryGetValue("Offset Min", out bannerOffsetMin))
			{
				object thisObj;
				defaultBannerGUI().TryGetValue("Offset Min", out thisObj);
				bannerOffsetMin = Convert.ToString(thisObj);
			}

			if (!bannerGUIData.TryGetValue("Offset Min", out bannerOffsetMax))
			{
				object thisObj;
				defaultBannerGUI().TryGetValue("Offset Min", out thisObj);
				bannerOffsetMax = Convert.ToString(thisObj);
			}

			// Convert the dictionary for the banner hud to it's respective data
			if (!bannerHUDData.TryGetValue("Anchor Min", out hudAnchorMin))
			{
				object thisObj;
				defaultBannerHUD().TryGetValue("Achor Min", out thisObj);
				hudAnchorMin = Convert.ToString(thisObj);
			}

			if (!bannerHUDData.TryGetValue("Anchor Max", out hudAnchorMax))
			{
				object thisObj;
				defaultBannerHUD().TryGetValue("Achor Max", out thisObj);
				hudAnchorMax = Convert.ToString(thisObj);
			}

			if (!bannerHUDData.TryGetValue("Offset Min", out hudOffsetMin))
			{
				object thisObj;
				defaultBannerHUD().TryGetValue("Offset Min", out thisObj);
				hudOffsetMin = Convert.ToString(thisObj);
			}

			if (!bannerHUDData.TryGetValue("Offset Min", out hudOffsetMax))
			{
				object thisObj;
				defaultBannerHUD().TryGetValue("Offset Min", out thisObj);
				hudOffsetMax = Convert.ToString(thisObj);
			}

			// Find all dropped supply drop after a delay of 1 minute if cant find
			try
			{
				FindAllDrops();
				DisplayDropHUD("update");
			}
			catch
			{
				timer.Once(60, () =>
				{
					FindAllDrops();
					DisplayDropHUD("update");
				});
			}
		}

		private void FindAllDrops()
		{
			// Find all supply drops object
			SupplyDrop[] allDrops = UnityEngine.Object.FindObjectsOfType<SupplyDrop>();

			// Add the supply drop location then generate marker
			foreach (SupplyDrop drops in allDrops)
			{
				Vector3 location = drops.transform.position;
				BaseEntity entity = drops.GetEntity();
				entsupply.Add(entity, drops);
				Vector3 positionOffset = location;
				positionOffset.x += UnityEngine.Random.Range(-80 * mapmarkerradius, 80 * mapmarkerradius);
				positionOffset.z += UnityEngine.Random.Range(-80 * mapmarkerradius, 80 * mapmarkerradius);
				dropposition.Add(entity, positionOffset);
			}

			GenerateMarkers();
		}
		#endregion

		#region Map Marker
		private void GenerateMarkers()
		{
			// Generate the map marker
			// Clear all the previously generated map markers
			if (dropradius != null)
			{
				foreach (var paire in dropradius)
				{
					MapMarkerGenericRadius MapMarkerDel = paire.Value;

					if (MapMarkerDel != null)
					{
						MapMarkerDel.Kill();
						MapMarkerDel.SendUpdate();
					}
				}
			}

			// For each of the drops, generate a marker on the map
			foreach (var paire in dropposition)
			{
				// Variables needed
				Vector3 position = paire.Value;
				bool looted;
				lootedornot.TryGetValue(paire.Key, out looted);
				MapMarkerGenericRadius MapMarker = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", position) as MapMarkerGenericRadius;

				// Check if map marker was generated, if not end this whole function because there was an error
				if (MapMarker == null)
				{
					return;
				}

				// Set the marker's color and alpha
				MapMarker.alpha = mapMarkerAlpha;
				MapMarker.color1 = markerColor;

				// Set the color to looted if the crate has already been looted
				if (looted)
				{
					MapMarker.color1 = markerLootedColor;
				}

				MapMarker.color2 = Color.black; // I honestly dont know why is this here

				// Set the marker's radius
				MapMarker.radius = mapmarkerradius;

				// Remove the previous marker data if there is then add the new one
				dropradius.Remove(paire.Key);
				dropradius.Add(paire.Key, MapMarker);
			}

			// Spawn the markers
			foreach (var markers in dropradius)
			{
				markers.Value.Spawn();
				markers.Value.SendUpdate();
			}
		}

		private void MarkerDisplayingDelete(BaseEntity Entity)
		{
			// Delete the marker
			MapMarkerGenericRadius delmarker;
			dropradius.TryGetValue(Entity, out delmarker);

			foreach (var paire in dropradius)
			{
				if (paire.Value == delmarker)
				{
					delmarker.Kill();
					delmarker.SendUpdate();
				}
			}
		}
		#endregion

		#region Hud
		private void DisplayDropHUD(string reason)
		{
			// Clear all the hud
			DestroyAllHUD();
			HUDlist.Clear();

			List<Vector3> positionlist = new List<Vector3>();
			List<BaseEntity> droplist = new List<BaseEntity>();
			List<string> HUDforplayers = new List<string>();

			/*if (reason == "spawn")
            {

            }*/
			if (reason == "dropped" || reason == "update" || reason == "killed" || reason == "looted")
            {
				// Remove the previous data if there is then add the new data
				foreach (var Suppliez in entsupply)
				{
					Vector3 supplyupdated = Suppliez.Key.transform.position;
					dropposition.Remove(Suppliez.Key);
					dropposition.Add(Suppliez.Key, supplyupdated);
				}

				// Add the data into the local list
				foreach (var pair in dropposition)
				{
					droplist.Add(pair.Key);
					positionlist.Add(pair.Value);
				}

				int round = 0;
				foreach (var player in BasePlayer.activePlayerList.ToList())
				{
					bool HUDview = permission.UserHasPermission(player.UserIDString, MapMyAirdropHUD);

					// Check if user has the permission
					if (!HUDview && !displayHudForAll)
                    {
						continue;
                    }

					// Storer for all the drops
					Dictionary<Vector3, int> allDropsAvailable = new Dictionary<Vector3, int>();

					// Loop for each drops
					foreach(Vector3 drop_posi in positionlist)
					{
						// Sort the data base on distance
						int dist = (int)Vector3.Distance(drop_posi, player.transform.position);
						allDropsAvailable.Add(drop_posi, dist);
					}

					// Only run if the list is has data
					if (allDropsAvailable.Count > 0)
                    {
						// Sort the list based on distance
						var dictList = allDropsAvailable.ToList();
						dictList.Sort((pair1, pair2) => pair1.Value.CompareTo(pair2.Value));

						int counter = 0;
						foreach (KeyValuePair<Vector3, int> theData in dictList)
                        {
							// Break it at 5, limit it to 5 on the screen
							if (counter == bannerHUDLimit)
                            {
								break;
                            }

							// Increment the counter
							counter++;

							// Offset between each gui element
							double columnOffset = 0.08 * round++;

							// Generate the gui
							var CuiElement = new CuiElementContainer();
							string CargoHUDBanner = CuiElement.Add(new CuiPanel
							{
								Image = {
								Color = "0.5 0.5 0.5 0.2",
							},
								RectTransform = {
								AnchorMin = Convert.ToDouble(hudAnchorMin.Split()[0]) + columnOffset + " " + Convert.ToDouble(hudAnchorMin.Split()[1]),
								AnchorMax = Convert.ToDouble(hudAnchorMax.Split()[0]) + columnOffset + " " + Convert.ToDouble(hudAnchorMax.Split()[1]),
								OffsetMin = hudOffsetMin,
								OffsetMax = hudOffsetMax,
							},
								CursorEnabled = false,
							});

							var closeButton = new CuiButton
							{
								Button = {
								Close = CargoHUDBanner,
								Color = "0.0 0.0 0.0 0.6",
							},
								RectTransform = {
								AnchorMin = "0.90 0.00",
								AnchorMax = "1.00 1.00",
							},
								Text = {
								Text = "X",
								FontSize = 8,
								Align = TextAnchor.MiddleCenter,
							},
							};

							// CuiElement.Add(closeButton, CargoHUDBanner);

							// Get the distance between the player
							int dist = theData.Value;
							string message = string.Format(lang.GetMessage("HUDDistanceMsg", this, player.UserIDString), dist.ToString());

							// Player distance GUI
							var playerdistance = CuiElement.Add(new CuiLabel
							{
								Text = {
								Text = string.Format(lang.GetMessage("HUDAirdropMsg", this, player.UserIDString), round, message),
								Color = "1.0 1.0 1.0 1.0",
								FontSize = 10,
								Align = TextAnchor.MiddleCenter
							},
								RectTransform = {
								AnchorMin = "0.0 0.0",
								AnchorMax = "0.85 1.0",
							},
							}, CargoHUDBanner);

							// Add The GUI
							CuiHelper.AddUi(player, CuiElement);
							HUDforplayers.Add(CargoHUDBanner);
						}
                    }

					HUDlist.Remove(player);
					HUDlist.Add(player, HUDforplayers);
				}

				if (!alreadyUpdating && HUDlist.Count > 0)
                {
					alreadyUpdating = true;
					timer.Once(10, () =>
					{
						DisplayDropHUD("update");
					});
                }
				else if (alreadyUpdating && HUDlist.Count == 0)
                {
					alreadyUpdating = false;
                }
				else if (alreadyUpdating && HUDlist.Count > 0)
                {
					alreadyUpdating = true;
					timer.Once(10, () =>
					{
						DisplayDropHUD("update");
					});
				}
            }
		}
		#endregion

		#region Spawn Detection
		private void OnSupplyDropLanded(SupplyDrop drop)
        {
			// Display banner to user
			DisplayBannerToAll("dropped");
			DisplayDropHUD("dropped");

			// Store the drop data
			BaseEntity entity = drop.GetEntity();
			Vector3 dropPosition = drop.transform.position;

			bool show = true;
			Vector3 thatPos = Vector3.zero;
			foreach (Vector3 position in supplySignalPosition)
            {
                if ((dropPosition.x - 10) <= position.x && (dropPosition.x + 10) >= position.x && (dropPosition.z - 10) <= position.z && (dropPosition.z + 10) >= position.z)
                {
					show = false;
					thatPos = position;
					break;
                }
            }

			if (show)
            {
				// Remove the previous data if there is then add the new data
				entsupply.Remove(entity);
				dropposition.Remove(entity);
				entsupply.Add(entity, drop);
				Vector3 positionOffset = dropPosition;
				positionOffset.x += UnityEngine.Random.Range(-80 * mapmarkerradius, 80 * mapmarkerradius);
				positionOffset.z += UnityEngine.Random.Range(-80 * mapmarkerradius, 80 * mapmarkerradius);
				dropposition.Add(entity, positionOffset);

				// Generate Marker
				GenerateMarkers();
			}
			else
            {
				supplySignalPosition.Remove(thatPos);
            }
        }

		private void OnExplosiveDropped(BasePlayer player, BaseEntity entity)
        {
			// Check if the entity is a supply signal
			if (entity.name == "assets/prefabs/tools/supply signal/grenade.supplysignal.deployed.prefab")
            {
				// Store the data
				supplySignalPosition.Add(entity.transform.position);
            }
        }

		private void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
		{
			// Check if the entity is a supply signal
			if (entity.name == "assets/prefabs/tools/supply signal/grenade.supplysignal.deployed.prefab")
			{
				// Store the data
				supplySignalPosition.Add(entity.transform.position);
			}
		}

		private void OnEntitySpawned(BaseEntity Entity)
		{
			// Check if entity does not exist
			if (Entity == null)
			{
				return;
			}

			// Check if it's a cargo plane entity
			if (Entity is CargoPlane)
			{
				DisplayBannerToAll("spawn");
			}
		}

		void OnEntityKill(BaseNetworkable entity)
		{
			// Get the base entity
			BaseEntity killed = entity as BaseEntity;

			// Check if the supply list contains the base entity
			if (entsupply.ContainsKey(killed))
			{
				// Delete the marker
				MarkerDisplayingDelete(killed);

				// Remove the data from the list
				entsupply.Remove(killed);
				dropposition.Remove(killed);
				dropradius.Remove(killed);
				lootedornot.Remove(killed);

				// Display the banner
				DisplayBannerToAll("killed");
				DisplayDropHUD("killed");
			}
		}

		void OnLootEntity(BasePlayer player, BaseEntity entity)
		{
			// Only care if it's supply drop
			if (entity is SupplyDrop)
			{
				// Check if there was already a key inside the lootedornot list 
				if (lootedornot.ContainsKey(entity))
				{
					// If has the key and is looted then end this function
					bool looted;
					lootedornot.TryGetValue(entity, out looted);

					if (looted)
					{
						return;
					}
				}

				// Check if the key is in the array
				foreach (var paire in entsupply)
				{
					if (paire.Key == entity)
					{
						// Remove the entity data if there is then add the new data
						lootedornot.Remove(entity);
						lootedornot.Add(entity, true);

						// Display banner
						DisplayBannerToAll("looted");
						DisplayDropHUD("looted");
					}
				}

				// Regenerate marker here
				GenerateMarkers();
			}
		}
		#endregion

		#region Banner
		void DisplayBannerToAll(string reason)
		{
			// Clear all the previous banner
			DestroyAllBanner();
			bannerlist.Clear();

			// Run it for all player
			foreach (var player in BasePlayer.activePlayerList.ToList())
			{
				// Check if user has the permission
				bool hasPermission = permission.UserHasPermission(player.UserIDString, MapMyAirdropBanner);

				if (!hasPermission && !displayGuiForAll)
                {
					continue;
                }

				// Here, user have permission so just create the banner
				List<string> bannerforplayers = new List<string>();

				// Generate the message
				string message = string.Empty;
				switch (reason)
				{
					case "spawn":
					{
						message = lang.GetMessage("SpawnMsg", this, player.UserIDString);
						break;
					}
					case "dropped":
					{
						message = lang.GetMessage("DroppedMsg", this, player.UserIDString);
						break;
					}
					case "looted":
					{
						message = lang.GetMessage("LootedMsg", this, player.UserIDString);
						break;
					}
					case "killed":
					{
						message = lang.GetMessage("KilledMsg", this, player.UserIDString);
						break;
					}
				}

				// Create the banner element
				CuiElementContainer CuiElement = new CuiElementContainer();
				CuiPanel bannerPanel = new CuiPanel
				{
					Image = {
						Color = "0.5 0.5 0.5 0.30",
					},
					RectTransform = {
						AnchorMin = bannerAnchorMin,
						AnchorMax = bannerAnchorMax,
						OffsetMin = bannerOffsetMin,
						OffsetMax = bannerOffsetMax,
					},
					CursorEnabled = false,
				};

				string CargoBanner = CuiElement.Add(bannerPanel);
				var closeButton = new CuiButton{
					Button = {
						Close = CargoBanner,
						Color = "0.0 0.0 0.0 0.6",
					},
					RectTransform = {
						AnchorMin = "0.90 0.01",
						AnchorMax = "0.99 0.99",
					},
					Text = {
						Text = "X",
						FontSize = 12,
						Align = TextAnchor.MiddleCenter
					},
				};

				// CuiElement.Add(closeButton, CargoBanner);

				CuiElement.Add(new CuiLabel{
					Text = {
						Text = message,
						FontSize = 14,
						FadeIn = 1.0f,
						Align = TextAnchor.MiddleCenter,
						Color = "1.0 1.0 1.0 1"
					},
					RectTransform = {
						AnchorMin = "0.10 0.10",
						AnchorMax = "0.90 0.90",
					},
				},
				CargoBanner);

				CuiHelper.AddUi(player, CuiElement);
				timer.Once(6, () =>
				{
					CuiHelper.DestroyUi(player, CargoBanner);
				});

				bannerforplayers.Add(CargoBanner);
				bannerlist.Remove(player);
				bannerlist.Add(player, bannerforplayers);
			}
		}
		#endregion
	}
}