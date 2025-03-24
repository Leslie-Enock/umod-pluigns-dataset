
using Newtonsoft.Json;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Oxide.Plugins
{
	[Info("Global Progression", "ignignokt84", "0.2.1")]
	[Description("Global blueprint learning")]
	class GlobalProgression : RustPlugin
	{
		Timer tickTimer;
		
		DataWrapper data = new DataWrapper();

		const string CommandRefresh = "refresh";
		const string CommandIgnore = "ignore";
		const string CommandUnignore = "unignore";

		const string PermAdministrate = "globalprogression.admin";
		const string PermIgnore = "globalprogression.ignore";

		#region Lang

		// load default messages to Lang
		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				{ "Prefix", "<color=#FFA500>[ GP ]</color>" },
				
				{ "Notify_BlueprintUnlocked", "<color=#00AA11>Blueprint(s) globally unlocked</color>: {0}" },
				{ "Notify_BlockedPrivileged", "<color=#EE0303>Blueprint for {0} has been queued for unlock and cannot be studied</color>" },
				{ "Notify_QueuedBlueprints", "<color=#00FFFF>Blueprint(s) queued for unlock</color>: {0}" },

				{ "Notify_TotalBlueprintCount", "Non-default blueprints found: {0}" },
				{ "Notify_BlueprintMissingCount", "Non-default blueprints to unlock: {0}" },
				{ "Notify_MissingBlueprintsUnlocked", "Missing blueprints unlocked: {0}" },
				{ "Notify_NoBlueprintsToUnlock", "No blueprints to unlock" },
				
				{ "Notify_RefreshComplete", "Refresh completed; took {0}ms" },

				{ "Error_InvalidCommand", "Invalid command" },

				{ "Wrapper_QueuedBlueprint", "{0} <color=#FF5522>({1})</color>" }
			}, this);
		}

		// send message to player (chat)
		void SendMessage(BasePlayer player, string key, object[] options = null)
		{
			if (player == null) return;
			SendReply(player, GetMessage("Prefix", player.UserIDString) + " " + GetMessage(key, player.UserIDString), options);
		}

		// send message to player (console)
		void SendMessage(ConsoleSystem.Arg arg, string key, object[] options = null)
		{
			string userIDString = arg?.Connection?.userid.ToString();
			if (arg != null)
				SendReply(arg, GetMessage("Prefix", userIDString) + " " + GetMessage(key, userIDString), options ?? new object[] { });
			else
				Puts(string.Format(GetMessage("Prefix") + " " + GetMessage(key), options));
		}

		// get message from Lang
		string GetMessage(string key, string userId = null) => lang.GetMessage(key, this, userId);

		#endregion

		#region Loading/Unloading

		// init
		void Init()
		{
			// register console/chat commands
			cmd.AddConsoleCommand("gp." + CommandRefresh, this, "CommandDelegator");
			cmd.AddConsoleCommand("gp." + CommandIgnore, this, "CommandDelegator");
			cmd.AddConsoleCommand("gp." + CommandUnignore, this, "CommandDelegator");
			cmd.AddChatCommand("gpq", this, "CheckQueue");

			permission.RegisterPermission(PermAdministrate, this);
			permission.RegisterPermission(PermIgnore, this);
		}

		// server initialized
		void OnServerInitialized()
		{
			LoadConfiguration();
			if (data.pendingBlueprints.Count > 0)
				tickTimer = timer.Every(data.tickRate, LocalTick);
			if (data.firstLoad)
			{
				RefreshBlueprints();
				data.firstLoad = false;
				SaveData();
			}
		}

		// on unloaded
		void Unload()
		{
			if (tickTimer != null)
				tickTimer.Destroy();

			SaveData();
		}

		#endregion

		#region Configuration/Data

		// load config
		void LoadConfiguration()
		{
			Config.Settings.NullValueHandling = NullValueHandling.Include;
			try
			{
				data = Config.ReadObject<DataWrapper>() ?? null;
			}
			catch (Exception)
			{
				LoadDefaultConfig();
			}
			if (data == null)
				LoadDefaultConfig();
		}

		// default config creation
		protected override void LoadDefaultConfig()
		{
			data = new DataWrapper();
			SaveData();
		}

		// save data
		void SaveData() => Config.WriteObject(data);

		#endregion

		#region Command Handling
		
		// delegation method for console commands
		void CommandDelegator(ConsoleSystem.Arg arg)
		{
			// return if user doesn't have access to run console command
			if (arg == null) return;
			string userIDString = arg?.Connection?.userid.ToString();

			if (userIDString == null || !HasPermission(userIDString, PermAdministrate)) return;

			// refresh globally unlocked blueprints by polling active/sleeping users and grant missing blueprints

			if (arg.cmd.Name.Equals(CommandRefresh, StringComparison.InvariantCultureIgnoreCase))
			{
				bool verbose = false;
				if (arg.HasArgs())
				{
					if (arg.Args[0].Equals("v") || arg.Args[0].Equals("verbose"))
						verbose = true;
				}

				RefreshBlueprints(arg, verbose);
				SaveData();
			}
			else
				SendMessage(arg, "Error_InvalidCommand");
		}

		// refresh blueprints by getting blueprints from all active players and sleepers
		void RefreshBlueprints(ConsoleSystem.Arg arg = null, bool verbose = false)
		{
			Stopwatch sw = new Stopwatch();
			sw.Start();
			if (arg == null)
				verbose = false;

			HashSet<string> playerBlueprints = new HashSet<string>();

			// get all blueprints from active players and sleepers
			List<BasePlayer> playerList = new List<BasePlayer>(BasePlayer.activePlayerList);
			playerList.AddRange(BasePlayer.sleepingPlayerList);

			foreach (BasePlayer player in playerList)
			{
				if (HasPermission(player.UserIDString, PermIgnore)) continue;
				PersistantPlayer playerInfo = SingletonComponent<ServerMgr>.Instance.persistance.GetPlayerInfo(player.userID);
				playerBlueprints.UnionWith(playerInfo.unlockedItems.Select(i => ItemManager.FindItemDefinition(i)?.shortname));
			}

			// filter out default blueprints
			playerBlueprints.ExceptWith(ItemManager.defaultBlueprints.Select(i => ItemManager.FindItemDefinition(i)?.shortname));

			// couldn't hurt
			playerBlueprints.Remove(null);

			if (verbose)
				SendMessage(arg, "Notify_TotalBlueprintCount", new object[] { playerBlueprints.Count });

			// if empty, no blueprints to unlock
			if (playerBlueprints.Count == 0)
			{
				if (verbose)
					SendMessage(arg, "Notify_NoBlueprintsToUnlock");
				return;
			}

			if (verbose)
				SendMessage(arg, "Notify_BlueprintMissingCount", new object[] { data.unlockedBlueprints.Except(playerBlueprints).ToList().Count });

			// union global unlocked blueprints with the aggregated player blueprints
			data.unlockedBlueprints.UnionWith(playerBlueprints);

			int counter = 0;
			// force unlock for all players
			playerList = new List<BasePlayer>(BasePlayer.activePlayerList);

			foreach (BasePlayer player in playerList)
			{
				if (HasPermission(player.UserIDString, PermIgnore)) continue;
				counter += UnlockGlobalBlueprintsForPlayer(player, false);
			}

			// remove any pending blueprints that have been unlocked
			if(data.pendingBlueprints.Count > 0)
				foreach (string shortname in data.unlockedBlueprints)
					data.pendingBlueprints.Remove(shortname);

			if (verbose)
				SendMessage(arg, "Notify_MissingBlueprintsUnlocked", new object[] { counter });

			sw.Stop();
			SendMessage(arg, "Notify_RefreshComplete", new object[] { sw.ElapsedMilliseconds });
		}

		// handler for /gpq chat command (check bp queue)
		void CheckQueue(BasePlayer player, string command, string[] args)
		{
			List<ItemDefinition> pending = new List<ItemDefinition>();
			foreach (string shortname in data.pendingBlueprints.Keys)
			{
				ItemDefinition def = ItemManager.FindItemDefinition(shortname);
				if (def == null) continue;
				pending.Add(def);
			}
			DateTime now = DateTime.Now;
			string queuedString = "None";
			if(pending.Count > 0)
			{
				List<string> entries = new List<string>();
				foreach(ItemDefinition def in pending)
				{
					TimeSpan remainingTime = data.pendingBlueprints[def.shortname].unlockTime - now;
					int hours = remainingTime.Hours;
					int minutes = remainingTime.Minutes;
					int seconds = remainingTime.Seconds;
					string time = (hours > 0 ? hours + "h " : "") + (minutes > 0 ? minutes + "m " : "") + seconds + "s";
					entries.Add(string.Format(GetMessage("Wrapper_QueuedBlueprint", player.UserIDString), def.displayName.translated, time));
				}
				queuedString = string.Join(", ", entries);
			}
			SendMessage(player, "Notify_QueuedBlueprints", new object[] { queuedString });
		}

		#endregion

		#region Hooks/Methods

		// player joined - update with global blueprints
		void OnPlayerInit(BasePlayer player)
		{
			if (HasPermission(player.UserIDString, PermIgnore)) return;

			UnlockGlobalBlueprintsForPlayer(player, false);
			//if(data.sync)
			//	Sync(player);
		}

		// update unlocked blueprints for new player
		void Sync(BasePlayer player)
		{
			PersistantPlayer playerInfo = SingletonComponent<ServerMgr>.Instance.persistance.GetPlayerInfo(player.userID);
			
			// get player unlocked blueprints; remove unlocked/pending blueprints from list, and immediately unlock any outstanding blueprints
			List<string> blueprintsToUnlock = playerInfo.unlockedItems.Select(i => ItemManager.FindItemDefinition(i)?.shortname)
												.Except(data.unlockedBlueprints)
												.Except(data.pendingBlueprints.Keys).ToList();

			if (blueprintsToUnlock.Count == 0) return;

			foreach (string shortname in blueprintsToUnlock)
				UnlockBlueprintForAllPlayers(shortname);
		}

		// when blueprint studied, queue blueprint for global learning
		object OnItemAction(Item item, string action, BasePlayer player)
		{
			if (!action.Equals("study")) return null;
			if (HasPermission(player.UserIDString, PermIgnore)) return null;

			string shortname = item.blueprintTargetDef.shortname;
			if (string.IsNullOrEmpty(shortname)) return null;

			if (data.unlockedBlueprints.Contains(shortname))
				return null;

			if(IsBlocked(shortname))
			{
				SendMessage(player, "Notify_BlockedPrivileged", new object[] { item.blueprintTargetDef.displayName.translated });
				return 0; // return non-null to block studying
			}
			
			// increment counter if blueprint is not already counting down to unlock
			if(!IsBlueprintUnlocking(shortname))
				Increment(shortname);
			
			return null;
		}

		// local tick - runs at interval defined by tickRate; terminated when no pending blueprints to unlock
		void LocalTick()
		{
			DateTime now = DateTime.Now;

			bool dirty = false;
			List<string> blueprintsToUnlock = GetBlueprintsToUnlock();
			foreach(string shortname in blueprintsToUnlock)
			{
				UnlockBlueprintForAllPlayers(shortname);
				data.pendingBlueprints.Remove(shortname);
				dirty = true;
			}

			if (dirty)
				SaveData();

			if (GetPendingUnlockCount() == 0)  // only continue tick if there are pending blueprints
				DestroyTimer();
		}

		// destroy timer
		void DestroyTimer()
		{
			tickTimer.Destroy();
			tickTimer = null;
		}

		// unlocks the specified blueprint for all players
		void UnlockBlueprintForAllPlayers(string shortname)
		{
			ItemDefinition itemDef = ItemManager.FindItemDefinition(shortname);
			if (itemDef == null) return;

			data.unlockedBlueprints.Add(shortname);

			List<BasePlayer> playerList = new List<BasePlayer>(BasePlayer.activePlayerList);
			foreach (BasePlayer player in playerList)
				UnlockBlueprintforPlayer(itemDef, player);
		}

		// unlocks the specified blueprint for a specific player
		void UnlockBlueprintforPlayer(ItemDefinition itemDef, BasePlayer player, bool notify = true)
		{
			if (HasPermission(player.UserIDString, PermIgnore)) return;
			if (player.blueprints.HasUnlocked(itemDef)) return;

			player.blueprints.Unlock(itemDef);
			if (notify)
				SendMessage(player, "Notify_BlueprintUnlocked", new object[] { itemDef.displayName.translated });
		}

		// unlocks all "global" blueprints for a specific player
		int UnlockGlobalBlueprintsForPlayer(BasePlayer player, bool notify = true)
		{
			List<string> itemNames = new List<string>();
			foreach(string shortname in data.unlockedBlueprints)
			{
				ItemDefinition itemDef = ItemManager.FindItemDefinition(shortname);
				if (itemDef == null) continue;

				itemNames.Add(itemDef.displayName.translated);
				UnlockBlueprintforPlayer(itemDef, player, false);
			}

			if(notify && itemNames.Count > 0)
				SendMessage(player, "Notify_BlueprintUnlocked", new object[] { string.Join(", ", itemNames.ToArray()) } );

			return itemNames.Count;
		}

		#endregion

		#region Helper Methods

		// check if player has permission
		private bool HasPermission(string userIDString, string permname)
		{
			return permission.UserHasPermission(userIDString, permname);
		}

		bool IsBlueprintUnlocked(string shortname)
		{
			return data.unlockedBlueprints.Contains(shortname);
		}

		// item is currently unlocking (timer running)
		bool IsBlueprintUnlocking(string shortname)
		{
			Blueprint b;
			if(!data.pendingBlueprints.TryGetValue(shortname, out b))
				return false;

			return b.counter >= data.threshold && b.unlockTime.CompareTo(DateTime.Now) > 0;
		}

		bool ShouldUnlock(string shortname)
		{
			Blueprint b;
			if (!data.pendingBlueprints.TryGetValue(shortname, out b))
				return false;
			
			return b.counter >= data.threshold && b.unlockTime.CompareTo(DateTime.Now) <= 0;
		}

		bool IsBlocked(string shortname)
		{
			if (!data.blocking) return false;

			return IsBlueprintUnlocking(shortname);
		}

		int GetPendingUnlockCount()
		{
			if (data.pendingBlueprints.Count == 0) return 0;
			return data.pendingBlueprints.Values.Count(b => b.counter >= data.threshold && b.unlockTime.CompareTo(DateTime.Now) > 0);
		}

		List<string> GetBlueprintsToUnlock()
		{
			if (data.pendingBlueprints == null || data.pendingBlueprints.Count == 0)
				return new List<string>();
			return data.pendingBlueprints.Values.Where(b => b.counter >= data.threshold && b.unlockTime.CompareTo(DateTime.Now) <= 0).Select(b => b.shortname).ToList();
		}

		void Increment(string shortname)
		{
			Blueprint b;
			if (!data.pendingBlueprints.TryGetValue(shortname, out b))
				b = new Blueprint() { shortname = shortname, counter = 0, unlockTime = DateTime.MaxValue };

			if (++b.counter >= data.threshold)
			{
				if (data.delay <= 0)
				{
					UnlockBlueprintForAllPlayers(shortname);
					data.pendingBlueprints.Remove(shortname);

					if (GetPendingUnlockCount() == 0)  // destroy timer if no pending unlocks
						DestroyTimer();

					return;
				}
				else
					b.unlockTime = DateTime.Now.AddSeconds(data.delay);
			}

			data.pendingBlueprints[shortname] = b;
			if (tickTimer == null && GetPendingUnlockCount() > 0)
				tickTimer = timer.Every(data.tickRate, LocalTick);

			SaveData();
		}

		#endregion

		#region Subclasses

		// config data wrapper class
		class DataWrapper
		{
			[JsonProperty(PropertyName = "First Load")]
			public bool firstLoad = true;
			[JsonProperty(PropertyName = "Blocking")]
			public bool blocking = false; // block other players from learning the blueprint until the global unlock timer finishes
			//[JsonProperty(PropertyName = "Sync on Player Join")]
			//public bool sync = true;
			[JsonProperty(PropertyName = "Global Unlock Threshold")]
			public int threshold = 1; // how many players need to study to start the global unlock countdown
			[JsonProperty(PropertyName = "Learning Delay")]
			public int delay = 600; // delay in seconds (realtime), default 600s (10min)
			[JsonProperty(PropertyName = "Tick Rate")]
			public float tickRate = 1f; // tick rate for internal checking
			[JsonProperty(PropertyName = "Unlocked Blueprints")]
			public HashSet<string> unlockedBlueprints = new HashSet<string>(); // unlocked blueprints
			[JsonProperty(PropertyName = "Pending Blueprints")]
			public Dictionary<string, Blueprint> pendingBlueprints = new Dictionary<string, Blueprint>(); // pending unlocks
		}

		// blueprint unlock container
		[Serializable]
		struct Blueprint
		{
			public string shortname;
			public short counter;
			public DateTime unlockTime;
		}

		#endregion
	}
}