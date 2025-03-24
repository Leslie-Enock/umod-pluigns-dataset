using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
	[Info("WipeAssistant", "S0N_0F_BISCUIT", "1.0.0")]
	[Description("Wipes player deployables, players(active and sleeping), and several mod data files.")]
    class WipeAssistant : RustPlugin
	{
		#region Variables
		const string version = "1.0.0";
		#region Plugin References
		[PluginReference("Economics")]
		Plugin Economics;
		[PluginReference("Backpacks")]
		Plugin Backpacks;
		[PluginReference("PlayerChallenges")]
		Plugin PlayerChallenges;
		[PluginReference("ZLevelsRemastered")]
		Plugin ZLevelsRemastered;
		[PluginReference("GUIShop")]
		Plugin GUIShop;
		[PluginReference("HeliControl")]
		Plugin HeliControl;
		[PluginReference("Kits")]
		Plugin Kits;
		[PluginReference("Pets")]
		Plugin Pets;
		[PluginReference("Replenish")]
		Plugin Replenish;
		[PluginReference("SignTracker")]
		Plugin SignTracker;
		[PluginReference("TargetPractice")]
		Plugin TargetPractice;
		[PluginReference("AutoPurge")]
		Plugin AutoPurge;
		[PluginReference("Airstrike")]
		Plugin Airstrike;
		[PluginReference("RotatingBillboards")]
		Plugin RotatingBillboards;
		[PluginReference("NTeleportation")]
		Plugin NTeleportation;
		[PluginReference("Bounty")]
		Plugin Bounty;
		#endregion

		#region Backpack Classes
		private class EmptyBackpack
		{
			public BackpackInventory Inventory = new BackpackInventory();
			public ulong ownerID;

			private BaseEntity entity;
			private BaseEntity visualEntity;
			private StorageContainer container => entity.GetComponent<StorageContainer>();
			public bool IsOpen => entity != null;
		}

		private class BackpackInventory
		{
			public List<BackpackItem> Items = new List<BackpackItem>();

			public class BackpackItem
			{
				public int ID;
				public int Amount;
				public ulong Skin;
				public float Fuel;
				public int FlameFuel;
				public float Condition;
				public int Ammo;
				public int AmmoType;

				public List<BackpackItem> Contents = new List<BackpackItem>();
			}
		}
		#endregion

		#region Configuration Classes
		class ConfigData
		{
			public bool WipeBackpackOnPlayerJoin { get; set; } = true;
			public ExampleWipe ExampleWipeOptions { get; set; } = new ExampleWipe();
			public HardWipe HardWipeOptions { get; set; } = new HardWipe();
			public SoftWipe SoftWipeOptions { get; set; } = new SoftWipe();
		}

		class ExampleWipe
		{
			public bool WipeAirstrike { get; set; } = false;
			public bool WipeAutoPurge { get; set; } = false;
			public bool WipeBackpacks { get; set; } = false;
			public bool WipeBounty { get; set; } = false;
			public bool WipeEconomics { get; set; } = false;
			public bool WipeGUIShop { get; set; } = false;
			public bool WipeHeliControl { get; set; } = false;
			public bool WipeKits { get; set; } = false;
			public bool WipeNTeleportation { get; set; } = false;
			public bool WipePets { get; set; } = false;
			public bool WipePlayerChallenges { get; set; } = false;
			public bool WipeReplenish { get; set; } = false;
			public bool WipeRotatingBillboards { get; set; } = false;
			public bool WipeSignTracker { get; set; } = false;
			public bool WipeTargetPractice { get; set; } = false;
			public bool WipeZLevelsRemastered { get; set; } = false;
			public List<string> CustomConsoleCommands { get; set; } = new List<string>();
		}

		class HardWipe
		{
			public bool WipeAirstrike { get; set; } = true;
			public bool WipeAutoPurge { get; set; } = true;
			public bool WipeBackpacks { get; set; } = true;
			public bool WipeBounty { get; set; } = true;
			public bool WipeEconomics { get; set; } = true;
			public bool WipeGUIShop { get; set; } = true;
			public bool WipeHeliControl { get; set; } = true;
			public bool WipeKits { get; set; } = true;
			public bool WipeNTeleportation { get; set; } = true;
			public bool WipePets { get; set; } = true;
			public bool WipePlayerChallenges { get; set; } = true;
			public bool WipeReplenish { get; set; } = true;
			public bool WipeRotatingBillboards { get; set; } = true;
			public bool WipeSignTracker { get; set; } = true;
			public bool WipeTargetPractice { get; set; } = true;
			public bool WipeZLevelsRemastered { get; set; } = true;
			public List<string> CustomConsoleCommands { get; set; } = new List<string>();
		}

		class SoftWipe
		{
			public bool WipeAirstrike { get; set; } = true;
			public bool WipeAutoPurge { get; set; } = true;
			public bool WipeBackpacks { get; set; } = true;
			public bool WipeBounty { get; set; } = true;
			public bool WipeEconomics { get; set; } = false;
			public bool WipeGUIShop { get; set; } = true;
			public bool WipeHeliControl { get; set; } = true;
			public bool WipeKits { get; set; } = true;
			public bool WipeNTeleportation { get; set; } = true;
			public bool WipePets { get; set; } = true;
			public bool WipePlayerChallenges { get; set; } = false;
			public bool WipeReplenish { get; set; } = false;
			public bool WipeRotatingBillboards { get; set; } = false;
			public bool WipeSignTracker { get; set; } = true;
			public bool WipeTargetPractice { get; set; } = false;
			public bool WipeZLevelsRemastered { get; set; } = false;
			public List<string> CustomConsoleCommands { get; set; } = new List<string>();
		}
		#endregion

		private Dictionary<ulong, string> PlayerData;
		private ConfigData config = new ConfigData();
		#endregion

		#region Plugin Initialization
		//
		// Load default config file
		//
		protected override void LoadDefaultConfig()
		{
			Config.Clear();
			var config = new ConfigData();
			Config.WriteObject(config, true);
		}

		private void LoadConfig()
		{
			config = Config.ReadObject<ConfigData>();
			config.ExampleWipeOptions.CustomConsoleCommands.Clear();
			config.ExampleWipeOptions.CustomConsoleCommands.Add("say SERVER WIPED BY WIPE ASSISTANT");
			config.ExampleWipeOptions.CustomConsoleCommands.Add("Command 2");
			Config.WriteObject(config, true);
		}

		private void LoadData()
		{
			try
			{
				PlayerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, string>>("WipeAssistant");
			}
			catch
			{
				PlayerData = new Dictionary<ulong, string>();
			}
		}

		private void Init()
		{
			LoadConfig();
			LoadData();
		}

		private void Loaded()
		{
			try
			{
				foreach (BasePlayer current in BasePlayer.activePlayerList)
				{
					string value;
					ulong userID = current.userID;
					string userName = current.displayName;
					if (!PlayerData.TryGetValue(userID, out value))
					{
						PlayerData.Add(userID, userName);
						SaveData();
					}
				}
			}
			catch { }
			try
			{
				foreach (BasePlayer current in BasePlayer.sleepingPlayerList)
				{
					string value;
					ulong userID = current.userID;
					string userName = current.displayName;
					if (!PlayerData.TryGetValue(userID, out value))
					{
						PlayerData.Add(userID, userName);
						SaveData();
					}
				}
			}
			catch { }
		}
		#endregion

		#region Player Hooks
		void OnPlayerConnected(Network.Message packet)
		{
			ulong userID = packet.connection.userid;
			string userName = packet.connection.username;
			if (userID == null)
				return;
			if (Backpacks && config.WipeBackpackOnPlayerJoin)
			{
				string value;
				if (!PlayerData.TryGetValue(userID, out value))
				{
					wipeBackpack(userID, userName);
				}
			}
		}
		#endregion

		#region Data Handling
		//
		// Save PlayerData
		//
		private void SaveData()
		{
			Interface.Oxide.DataFileSystem.WriteObject("WipeAssistant", PlayerData);
		}
		//
		// Clear PlayerData
		//
		private void ClearData()
		{
			PlayerData.Clear();
			Interface.Oxide.DataFileSystem.WriteObject("WipeAssistant", PlayerData);
		}
		#endregion

		#region Functionality
		//
		// Create a backup of the servers files
		//
		void CreateBackup()
		{
			string location = $"backup/WipeAssistant/" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + "/" + ConVar.Server.identity;
			DirectoryEx.Backup(location);
			DirectoryEx.CopyAll(ConVar.Server.rootFolder, location);
			Puts($"Backup added to \"" + location + "\"");
		}
		//
		// Create a backup of a players backpack
		//
		void BackupBackpack(string playerName, ulong playerID)
		{
			string location = $"backup/WipeAssistant/Backpacks/{playerName}/{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}";
			DirectoryEx.Backup(location);
			DirectoryEx.CopyAll(ConVar.Server.rootFolder + "/oxide/data/Backpacks", location);
			Puts($"Backpack backup added to \"" + location + "\"");
		}
		//
		// Kills all active and sleeping players
		//
		void killPlayers(string type)
		{
			int active = 0, sleeping = 0;
			bool unloadedBackpacks = false;
			if (Backpacks && (type == "hard" && config.HardWipeOptions.WipeBackpacks) || (type == "soft" && config.SoftWipeOptions.WipeBackpacks))
			{
				Interface.Oxide.UnloadPlugin("Backpacks");
				unloadedBackpacks = true;
			}
			try
			{
				foreach (BasePlayer current in BasePlayer.activePlayerList)
				{
					current.Hurt(10000);
					active++;
					if (unloadedBackpacks)
						wipeBackpack(current.userID, current.displayName);
				}
			}
			catch { }
			try
			{
				foreach (BasePlayer current in BasePlayer.sleepingPlayerList)
				{
					current.Hurt(10000);
					sleeping++;
					if (unloadedBackpacks)
						wipeBackpack(current.userID, current.displayName);
				}
			}
			catch { }
			Puts($"Killed {active} active players.");
			Puts($"Killed {sleeping} sleeping players.");
			if (unloadedBackpacks)
				Interface.Oxide.LoadPlugin("Backpacks");
		}
		//
		// Removes all corpses from the world
		//
		void removeCorpses()
		{
			int corpses = 0;
			foreach (BaseCorpse entity in BaseNetworkable.serverEntities.Where(p => (p as BaseCorpse) != null).ToList())
			{
				entity.RemoveCorpse();
				corpses++;
			}
			Puts($"Removed {corpses} corpses.");
		}
		//
		// Wipe players from the world (Kill and remove corpses)
		//
		void wipePlayers(string type)
		{
			killPlayers(type);
			removeCorpses();
		}
		//
		// Wipe a players backpack
		//
		void wipeBackpack(ulong userID, string userName)
		{
			EmptyBackpack eb = new EmptyBackpack();
			eb.ownerID = userID;
			BackupBackpack(userName, userID);
			Core.Interface.Oxide.DataFileSystem.WriteObject("Backpacks/" + userID, eb);
			Puts($"Cleared {userName}'s backpack.");
			PlayerData.Add(userID, userName);
			SaveData();
		}
		//
		// Wipe all player deployables from the map
		//
		void wipeDeployables()
		{
			int entities = 0;
			foreach (BaseNetworkable entity in BaseNetworkable.serverEntities.Where(p => (p as BaseEntity).OwnerID != 0).ToList())
			{
				entity.Kill();
				entities++;
			}
			Puts($"Removed {entities} entities from the server.");
		}
		#endregion

		#region Console Commands
		[ConsoleCommand("softwipe")]
        void softWipe(ConsoleSystem.Arg arg)
        {
			if (arg.Connection != null && arg.Connection.authLevel < 2)
				return;
			
			// Create server backup
			CreateBackup();

			#region Clear other mod data
			if (Airstrike && config.SoftWipeOptions.WipeAirstrike)
			{
				Interface.Oxide.UnloadPlugin("Airstrike");
				Core.Interface.Oxide.DataFileSystem.WriteObject("airstrike_data", new Dictionary<object, object>());
				Puts($"Cleared airstrike_data");
				Interface.Oxide.LoadPlugin("Airstrike");
			}
			if (AutoPurge && config.SoftWipeOptions.WipeAutoPurge)
			{
				Interface.Oxide.UnloadPlugin("AutoPurge");
				Core.Interface.Oxide.DataFileSystem.WriteObject("autopurge", new Dictionary<object, object>());
				Puts($"Cleared autopurge");
				Interface.Oxide.LoadPlugin("AutoPurge");
			}
			if (Bounty && config.SoftWipeOptions.WipeBounty)
			{
				Bounty.Call("ccmdbWipe", arg);
			}
			if (Economics && config.SoftWipeOptions.WipeEconomics)
			{
				string[] args = { "wipe" };
				ConsoleSystem.Arg econArg = arg;
				econArg.Args = args;
				Economics.Call("ccmdEco", econArg);
			}
			if (GUIShop && config.SoftWipeOptions.WipeGUIShop)
			{
				Interface.Oxide.UnloadPlugin("GUIShop");
				Core.Interface.Oxide.DataFileSystem.WriteObject("GUIShop", new Dictionary<object, object>());
				Puts($"Cleared GUIShop");
				Interface.Oxide.LoadPlugin("GUIShop");
			}
			if (HeliControl && config.SoftWipeOptions.WipeHeliControl)
			{
				Interface.Oxide.UnloadPlugin("HeliControl");
				Core.Interface.Oxide.DataFileSystem.WriteObject("HeliControlCooldowns", new Dictionary<object, object>());
				Puts($"Cleared HeliControlCooldowns");
				Interface.Oxide.LoadPlugin("HeliControl");
			}
			if (Kits && config.SoftWipeOptions.WipeKits)
			{
				Interface.Oxide.UnloadPlugin("Kits");
				Core.Interface.Oxide.DataFileSystem.WriteObject("Kits_Data", new Dictionary<object, object>());
				Puts($"Cleared Kits_Data");
				Interface.Oxide.LoadPlugin("Kits");
			}
			if (NTeleportation && config.SoftWipeOptions.WipeNTeleportation)
			{
				Interface.Oxide.UnloadPlugin("NTeleportation");
				Core.Interface.Oxide.DataFileSystem.WriteObject("NTeleportationHome", new Dictionary<object, object>());
				Puts($"Cleared NTeleportationHome");
				Interface.Oxide.LoadPlugin("NTeleportation");
			}
			if (Pets && config.SoftWipeOptions.WipePets)
			{
				Interface.Oxide.UnloadPlugin("Pets");
				Core.Interface.Oxide.DataFileSystem.WriteObject("Pets", new Dictionary<object, object>());
				Puts($"Cleared Pets");
				Interface.Oxide.LoadPlugin("Pets");
			}
			if (PlayerChallenges && config.SoftWipeOptions.WipePlayerChallenges)
			{
				PlayerChallenges.Call("ccmdPCWipe", arg);
			}
			if (Replenish && config.SoftWipeOptions.WipeReplenish)
			{
				Interface.Oxide.UnloadPlugin("Replenish");
				Core.Interface.Oxide.DataFileSystem.WriteObject("ReplenishData", new Dictionary<object, object>());
				Puts($"Cleared ReplenishData");
				Interface.Oxide.LoadPlugin("Replenish");
			}
			if (RotatingBillboards && config.SoftWipeOptions.WipeRotatingBillboards)
			{
				Interface.Oxide.UnloadPlugin("RotatingBillboards");
				Core.Interface.Oxide.DataFileSystem.WriteObject("billboard_data", new Dictionary<object, object>());
				Puts($"Cleared billboard_data");
				Interface.Oxide.LoadPlugin("RotatingBillboards");
			}
			if (SignTracker && config.SoftWipeOptions.WipeSignTracker)
			{
				Interface.Oxide.UnloadPlugin("SignTracker");
				Core.Interface.Oxide.DataFileSystem.WriteObject("SignTracker", new Dictionary<object, object>());
				Puts($"Cleared SignTracker");
				Interface.Oxide.LoadPlugin("SignTracker");
			}
			if (TargetPractice && config.SoftWipeOptions.WipeTargetPractice)
			{
				Interface.Oxide.UnloadPlugin("TargetPractice");
				Core.Interface.Oxide.DataFileSystem.WriteObject("targetpractice_scores", new Dictionary<object, object>());
				Puts($"Cleared targetpractice_scores");
				Interface.Oxide.LoadPlugin("TargetPractice");
			}
			if (ZLevelsRemastered && config.SoftWipeOptions.WipeZLevelsRemastered)
			{
				string[] args = { "**", "*", "/2" };
				ConsoleSystem.Arg econArg = arg;
				econArg.Args = args;
				ZLevelsRemastered.Call("ZlvlCommand", econArg);
			}
			#endregion

			foreach (string command in config.SoftWipeOptions.CustomConsoleCommands)
				ConsoleSystem.Run(ConsoleSystem.Option.Server, command);

			ClearData();

			wipePlayers("soft");

			wipeDeployables();
			return;
        }

		[ConsoleCommand("hardwipe")]
		void hardWipe(ConsoleSystem.Arg arg)
		{
			if (arg.Connection != null && arg.Connection.authLevel < 2)
				return;

			// Create server backup
			CreateBackup();

			#region Clear other mod data
			if (Airstrike && config.HardWipeOptions.WipeAirstrike)
			{
				Interface.Oxide.UnloadPlugin("Airstrike");
				Core.Interface.Oxide.DataFileSystem.WriteObject("airstrike_data", new Dictionary<object, object>());
				Puts($"Cleared airstrike_data");
				Interface.Oxide.LoadPlugin("Airstrike");
			}
			if (AutoPurge && config.HardWipeOptions.WipeAutoPurge)
			{
				Interface.Oxide.UnloadPlugin("AutoPurge");
				Core.Interface.Oxide.DataFileSystem.WriteObject("autopurge", new Dictionary<object, object>());
				Puts($"Cleared autopurge");
				Interface.Oxide.LoadPlugin("AutoPurge");
			}
			if (Bounty && config.HardWipeOptions.WipeBounty)
			{
				Bounty.Call("ccmdbWipe", arg);
			}
			if (Economics && config.HardWipeOptions.WipeEconomics)
			{
				string[] args = { "wipe" };
				ConsoleSystem.Arg econArg = arg;
				econArg.Args = args;
				Economics.Call("ccmdEco", econArg);
			}
			if (GUIShop && config.HardWipeOptions.WipeGUIShop)
			{
				Interface.Oxide.UnloadPlugin("GUIShop");
				Core.Interface.Oxide.DataFileSystem.WriteObject("GUIShop", new Dictionary<object, object>());
				Puts($"Cleared GUIShop");
				Interface.Oxide.LoadPlugin("GUIShop");
			}
			if (HeliControl && config.HardWipeOptions.WipeHeliControl)
			{
				Interface.Oxide.UnloadPlugin("HeliControl");
				Core.Interface.Oxide.DataFileSystem.WriteObject("HeliControlCooldowns", new Dictionary<object, object>());
				Puts($"Cleared HeliControlCooldowns");
				Interface.Oxide.LoadPlugin("HeliControl");
			}
			if (Kits && config.HardWipeOptions.WipeKits)
			{
				Interface.Oxide.UnloadPlugin("Kits");
				Core.Interface.Oxide.DataFileSystem.WriteObject("Kits_Data", new Dictionary<object, object>());
				Puts($"Cleared Kits_Data");
				Interface.Oxide.LoadPlugin("Kits");
			}
			if (NTeleportation && config.HardWipeOptions.WipeNTeleportation)
			{
				Interface.Oxide.UnloadPlugin("NTeleportation");
				Core.Interface.Oxide.DataFileSystem.WriteObject("NTeleportationHome", new Dictionary<object, object>());
				Puts($"Cleared NTeleportationHome");
				Interface.Oxide.LoadPlugin("NTeleportation");
			}
			if (Pets && config.HardWipeOptions.WipePets)
			{
				Interface.Oxide.UnloadPlugin("Pets");
				Core.Interface.Oxide.DataFileSystem.WriteObject("Pets", new Dictionary<object, object>());
				Puts($"Cleared Pets");
				Interface.Oxide.LoadPlugin("Pets");
			}
			if (PlayerChallenges && config.HardWipeOptions.WipePlayerChallenges)
			{
				PlayerChallenges.Call("ccmdPCWipe", arg);
			}
			if (Replenish && config.HardWipeOptions.WipeReplenish)
			{
				Interface.Oxide.UnloadPlugin("Replenish");
				Core.Interface.Oxide.DataFileSystem.WriteObject("ReplenishData", new Dictionary<object, object>());
				Puts($"Cleared ReplenishData");
				Interface.Oxide.LoadPlugin("Replenish");
			}
			if (RotatingBillboards && config.HardWipeOptions.WipeRotatingBillboards)
			{
				Interface.Oxide.UnloadPlugin("RotatingBillboards");
				Core.Interface.Oxide.DataFileSystem.WriteObject("billboard_data", new Dictionary<object, object>());
				Puts($"Cleared billboard_data");
				Interface.Oxide.LoadPlugin("RotatingBillboards");
			}
			if (SignTracker && config.HardWipeOptions.WipeSignTracker)
			{
				Interface.Oxide.UnloadPlugin("SignTracker");
				Core.Interface.Oxide.DataFileSystem.WriteObject("SignTracker", new Dictionary<object, object>());
				Puts($"Cleared SignTracker");
				Interface.Oxide.LoadPlugin("SignTracker");
			}
			if (TargetPractice && config.HardWipeOptions.WipeTargetPractice)
			{
				Interface.Oxide.UnloadPlugin("TargetPractice");
				Core.Interface.Oxide.DataFileSystem.WriteObject("targetpractice_scores", new Dictionary<object, object>());
				Puts($"Cleared targetpractice_scores");
				Interface.Oxide.LoadPlugin("TargetPractice");
			}
			if (ZLevelsRemastered && config.HardWipeOptions.WipeZLevelsRemastered)
			{
				string[] args = { "**", "*", "/2" };
				ConsoleSystem.Arg econArg = arg;
				econArg.Args = args;
				ZLevelsRemastered.Call("ZlvlCommand", econArg);
			}
			#endregion

			foreach (string command in config.HardWipeOptions.CustomConsoleCommands)
				ConsoleSystem.Run(ConsoleSystem.Option.Server, command);

			ClearData();

			wipePlayers("hard");

			wipeDeployables();
			return;
		}

		[ConsoleCommand("entitycount")]
		void entityCount(ConsoleSystem.Arg args)
		{
			int entityCount = 0;
			int ownerCount = 0;
			List<ulong> ownerIDs = new List<ulong>();
			if (args.Connection != null && args.Connection.authLevel < 2)
				return;

			Puts($"Owner List:");
			Puts($"--------------------------------------------");
			foreach (var entity in BaseNetworkable.serverEntities.ToList())
			{
				if (!ownerIDs.Contains((entity as BaseEntity).OwnerID))
				{
					ownerIDs.Add((entity as BaseEntity).OwnerID);
					ownerCount++;
					Puts($"" + ownerCount.ToString() + ": " + (entity as BaseEntity).OwnerID.ToString());
				}
				entityCount++;
			}

			Puts($"--------------------------------------------");
			Puts($"Entity count: " + entityCount.ToString());
			return;
		}
		#endregion
	}
}