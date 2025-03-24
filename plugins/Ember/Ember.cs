using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries;

namespace Oxide.Plugins {
	[Info ("Ember", "Mkekala", "1.4.0")]
	[Description ("Integrates Ember store & ban management with Rust")]
	public class Ember : RustPlugin {
		#region Configuration
		private class PluginConfig {
			public string Host = "http://127.0.0.1";
			public string Token;
		}

		private PluginConfig config;
		private bool banLog;
		private Dictionary<string, bool> roleSync;

		private Dictionary<string, string> headers;

		protected override void LoadDefaultConfig () {
			Config.WriteObject (new PluginConfig (), true);
		}
		#endregion

		#region Variables
		private bool connected;
		private Dictionary<string, JToken> userData;
		private Dictionary<string, string> usersToPost;
		private List<string> usersProcessed;
		#endregion

		#region Methods
		void QueueUserToPost (string steamid, string name = "") {
			if (!usersToPost.ContainsKey (steamid))
				usersToPost.Add (steamid, name);
		}

		void PostUsers (Dictionary<string, string> users) {
			if (users.Count == 0)
				return;

			Puts (string.Format (lang.GetMessage ("ConsolePostingUsers", this), users.Count));

			List<string> names = new List<string> ();
			foreach (string name in users.Values)
				names.Add (name.Replace (",", ""));

			webrequest.Enqueue (config.Host + "/api/server/users", "steamids=" + string.Join (",", users.Keys) + "&names=" + string.Join (",", names), (code, response) => {
				if ((code != 200 && code != 204) || response == null) {
					Puts (string.Format (lang.GetMessage ("ConsoleApiRequestFailed", this), code, response));
					return;
				}

				JObject json = JObject.Parse (response);

				foreach (var userobj in (JObject) json["users"]) {
					userData[userobj.Key] = userobj.Value;
					BasePlayer player = GetPlayerBySteamID (userobj.Key);
					if (player != null)
						ProcessUser (player);
				}

			}, this, RequestMethod.POST, headers);
		}

		void ProcessUser (BasePlayer player) {
			string steamid = player.UserIDString;
			if (!userData.ContainsKey (steamid)) return;
			JToken userjson = userData[steamid];
			string name = player.displayName;

			Puts (string.Format (lang.GetMessage ("ConsoleProcessingUserData", this), name, steamid));
			bool banned = (bool) userjson["banned"];

			if (banned) {
				player.Kick (lang.GetMessage ("Banned", this, player.UserIDString));
				userData.Remove (steamid);
				return;
			}

			foreach (JObject purchase in userjson["store_package_purchases"]["expiring"]) {
				SendReply (player, string.Format (lang.GetMessage ("PackageExpired", this, player.UserIDString), purchase["store_package"]["name"]));
				if (purchase["store_package"]["role"].HasValues) {
					string groupToRevoke = (string) purchase["store_package"]["role"]["ingame_equivalent"];
					Puts (string.Format (lang.GetMessage ("ConsoleRevokingGroup", this), groupToRevoke, name, steamid));
					permission.RemoveUserGroup (steamid, groupToRevoke);
					SendReply (player, string.Format (lang.GetMessage ("GroupRevoked", this, player.UserIDString), groupToRevoke));
				}
				foreach (string command in purchase["store_package"]["expiry_commands"])
					rust.RunServerCommand (command);
			}

			foreach (JObject purchase in userjson["store_package_purchases"]["unredeemed"]) {
				SendReply (player, string.Format (lang.GetMessage ("RedeemingPackage", this, player.UserIDString), purchase["store_package"]["name"]));
				if (purchase["store_package"]["role"].HasValues) {
					string group = (string) purchase["store_package"]["role"]["ingame_equivalent"];
					if (!permission.GroupExists (group)) {
						Puts (string.Format (lang.GetMessage ("ConsoleCreatingGroup", this), group));
						permission.CreateGroup (group, group, 0);
					}
					Puts (string.Format (lang.GetMessage ("ConsoleGrantingGroup", this), group, name, steamid));
					permission.AddUserGroup (steamid, group);
					SendReply (player, string.Format (lang.GetMessage ("GroupGranted", this, player.UserIDString), group));
				}
				foreach (string command in purchase["store_package"]["commands"])
					rust.RunServerCommand (command);
			}

			if (roleSync["Receive"] == true || roleSync["Send"] == true) {
				Puts (string.Format (lang.GetMessage ("ConsoleSyncingRoles", this), name, steamid));

				if (roleSync["Receive"] == true) {
					foreach (string role in userjson["roles"]) {
						if (!permission.UserHasGroup (steamid, role)) {
							if (!permission.GroupExists (role)) {
								if (roleSync["Create"]) {
									Puts (string.Format (lang.GetMessage ("ConsoleCreatingGroup", this), role));
									permission.CreateGroup (role, role, 0);
								} else {
									continue;
								}
							}
							Puts (string.Format (lang.GetMessage ("ConsoleGrantingGroup", this), role, name, steamid));
							permission.AddUserGroup (steamid, role);
							SendReply (player, string.Format (lang.GetMessage ("GroupGranted", this, player.UserIDString), role));
						}
					}
					foreach (string role in userjson["revoked_roles"]) {
						if (permission.UserHasGroup (steamid, role)) {
							Puts (string.Format (lang.GetMessage ("ConsoleRevokingGroup", this), role, name, steamid));
							permission.RemoveUserGroup (steamid, role);
							SendReply (player, string.Format (lang.GetMessage ("GroupRevoked", this, player.UserIDString), role));
						}
					}
				}

				if (roleSync["Send"] == true) {
					string[] roles = userjson["roles"].ToObject<string[]> ();
					foreach (string group in permission.GetGroups ()) {
						if (permission.UserHasGroup (steamid, group)) {
							if (Array.IndexOf (roles, group) == -1) {
								PostRole (steamid, group);
							}
						}
					}
				}
			}

			usersProcessed.Add (steamid);
			userData.Remove (steamid);
		}

		void PostUsersProcessed (List<String> users) {
			if (users.Count == 0)
				return;

			webrequest.Enqueue (config.Host + "/api/server/usersprocessed", "steamids=" + string.Join (",", users), (code, response) => {
				if (code != 200 && code != 204)
					Puts (string.Format (lang.GetMessage ("ConsoleApiRequestFailed", this), code, response));
			}, this, RequestMethod.POST, headers);
		}

		private void PollUsers () {
			string steamids = "";

			foreach (BasePlayer ply in BasePlayer.activePlayerList) {
				if (!usersToPost.ContainsKey (ply.UserIDString) &&
					!usersProcessed.Contains (ply.UserIDString) &&
					!userData.ContainsKey (ply.UserIDString)
				)
					steamids += ply.UserIDString + ",";
			}

			if (!string.IsNullOrEmpty (steamids)) {
				webrequest.Enqueue (config.Host + "/api/server/users/poll?steamids=" + steamids, null, (code, response) => {
					if ((code != 200 && code != 204) || response == null) {
						Puts (string.Format (lang.GetMessage ("ConsoleApiRequestFailed", this), code, response));
						return;
					}

					JObject json = JObject.Parse (response);

					foreach (string steamid in json["users"])
						QueueUserToPost (steamid);
				}, this, RequestMethod.GET, headers);
			}
		}

		void Ban (string offenderSteamid, string expiryMinutes, string reason, bool global, string adminSteamid, BasePlayer caller) {
			string globalStr = global ? "global=true&" : "";
			string adminStr = (adminSteamid != null) ? "&admin_steamid=" + adminSteamid : "";

			webrequest.Enqueue (config.Host + "/api/server/users/" + offenderSteamid + "/bans", globalStr + "expiry_minutes=" + expiryMinutes + "&reason=" + reason + adminStr, (code, response) => {
				if (code != 200 && code != 204) {
					Puts (string.Format (lang.GetMessage ("ConsoleApiRequestFailedBan", this), code, response));
					return;
				}

				Puts (lang.GetMessage ("PlayerBanned", this));

				if (caller != null)
					SendReply (caller, lang.GetMessage ("PlayerBanned", this, caller.UserIDString));
			}, this, RequestMethod.POST, headers);
		}

		void Unban (string offenderSteamid, BasePlayer caller) {
			webrequest.Enqueue (config.Host + "/api/server/users/" + offenderSteamid + "/bans", null, (code, response) => {
				if (code != 200 && code != 204) {
					Puts (string.Format (lang.GetMessage ("ConsoleApiRequestFailedUnban", this), code, response));
					return;
				}

				Puts (lang.GetMessage ("PlayerUnbanned", this));

				if (caller != null)
					SendReply (caller, lang.GetMessage ("PlayerUnbanned", this, caller.UserIDString));
			}, this, RequestMethod.DELETE, headers);
		}

		void PostRole (string steamid, string role) {
			if (role == "default")
				return;

			webrequest.Enqueue (config.Host + "/api/server/users/" + steamid + "/roles", "role=" + role, (code, response) => {
				if (code != 200 && code != 204) {
					Puts (string.Format (lang.GetMessage ("ConsoleApiRequestFailedRoleAdd", this), role, code, response));
					return;
				}

				Puts (string.Format (lang.GetMessage ("ConsoleRoleAdded", this), role, steamid));
			}, this, RequestMethod.POST, headers);

		}

		void DeleteRole (string steamid, string role) {
			webrequest.Enqueue (config.Host + "/api/server/users/" + steamid + "/roles/" + role, null, (code, response) => {
				if (code != 200 && code != 204) {
					Puts (string.Format (lang.GetMessage ("ConsoleApiRequestFailedRoleRevoke", this), role, code, response));
					return;
				}

				Puts (string.Format (lang.GetMessage ("ConsoleRoleRevoked", this), role, steamid));
			}, this, RequestMethod.DELETE, headers);
		}

		static List<BasePlayer> GetPlayersByName (string name) {
			List<BasePlayer> matches = new List<BasePlayer> ();

			foreach (BasePlayer ply in BasePlayer.activePlayerList)
				if (ply.displayName.ToLower ().Contains (name.ToLower ()))
					matches.Add (ply);

			if (matches.Count () > 0)
				return matches;

			return null;
		}

		static BasePlayer GetPlayerBySteamID (string steamid) {
			foreach (BasePlayer ply in BasePlayer.activePlayerList)
				if (ply.UserIDString == steamid)
					return ply;

			return null;
		}

		protected void SendApiConnectionWarning (BasePlayer player) {
			SendReply (player, lang.GetMessage ("DebugChatPrefix", this) + "<color=orange>" + lang.GetMessage ("NoApiConnection", this, player.UserIDString) + "</color>");
		}
		#endregion

		#region Hooks
		private void Init () {
			config = Config.ReadObject<PluginConfig> ();
			config.Host = config.Host.TrimEnd (new Char[] { '/' });
			Config.WriteObject (config);
			headers = new Dictionary<string, string> { { "Authorization", "Bearer " + config.Token }, { "Accept", "application/json" } };

			permission.RegisterPermission (this.Title.ToLower () + ".ban", this);
			permission.RegisterPermission (this.Title.ToLower () + ".unban", this);

			Puts (lang.GetMessage ("ConsoleApiConnectionChecking", this));

			webrequest.Enqueue (config.Host + "/api/server/connectioncheck", null, (code, response) => {
				if ((code != 200 && code != 204) || response == null) {
					Puts (string.Format (lang.GetMessage ("ConsoleApiRequestFailed", this), code, response));
					Unsubscribe ("OnUserBanned");
					Unsubscribe ("OnUserUnbanned");
					Unsubscribe ("OnUserGroupAdded");
					Unsubscribe ("OnUserGroupRemoved");
					Unsubscribe ("OnUserApproved");
					return;
				}

				JObject json = JObject.Parse (response);
				banLog = (bool) json["settings"]["ban_log"];
				roleSync = new Dictionary<string, bool> () {
					{ "Receive", (bool) json["settings"]["role_sync"]["receive"] },
					{ "Send", (bool) json["settings"]["role_sync"]["send"] },
					{ "Create", (bool) json["settings"]["role_sync"]["create"] }
				};

				int pollInterval = (int) json["settings"]["poll_interval"];
				if (pollInterval > 0) {
					timer.Every (pollInterval, () => {
						PollUsers ();
					});
				}

				usersToPost = new Dictionary<string, string> ();
				usersProcessed = new List<string> ();
				userData = new Dictionary<string, JToken> ();
				timer.Every (5, () => {
					PostUsers (usersToPost);
					usersToPost = new Dictionary<string, string> ();
					PostUsersProcessed (usersProcessed);
					usersProcessed = new List<string> ();
				});

				connected = true;
				Puts (lang.GetMessage ("ConsoleApiConnectionSuccess", this));
			}, this, RequestMethod.GET, headers);
		}

		void OnUserBanned (string name, string id, string ipAddress, string reason) {
			if (banLog)
				Ban (id, "0", reason, false, null, null);
		}

		void OnUserUnbanned (string name, string id, string ipAddress) {
			if (banLog)
				Unban (id, null);
		}

		void OnUserGroupAdded (string id, string groupName) {
			if (roleSync["Send"])
				PostRole (id, groupName);
		}

		void OnUserGroupRemoved (string id, string groupName) {
			if (roleSync["Send"])
				DeleteRole (id, groupName);
		}

		void OnUserApproved (string name, string id, string ipAddress) {
			QueueUserToPost (id, name);
		}

		void OnPlayerConnected (BasePlayer player) {
			if (!connected) {
				if (player.net.connection.authLevel >= 1)
					SendApiConnectionWarning (player);
				return;
			}

			ProcessUser (player);
		}
		#endregion

		#region Chat commands
		[ChatCommand ("ban")]
		void cmdBan (BasePlayer player, string command, string[] args) {
			string steamid = player.UserIDString;

			if (player.net.connection.authLevel != 2 && !permission.UserHasPermission (player.UserIDString, this.Title.ToLower () + ".ban")) {
				SendReply (player, lang.GetMessage ("NoPermission", this, player.UserIDString));
				return;
			} else if (args == null || args.Length < 3) {
				SendReply (player, lang.GetMessage ("BanCommandUsage", this, player.UserIDString));
				return;
			}

			string offenderSteamid = "";
			if (args[0].IsSteamId ()) {
				offenderSteamid = args[0];
			} else {
				List<BasePlayer> offenderMatches = GetPlayersByName (args[0]);
				if (offenderMatches != null) {
					if (offenderMatches.Count () == 1) {
						offenderSteamid = offenderMatches.First ().UserIDString;
					} else {
						SendReply (player, lang.GetMessage ("MultiplePlayersFound", this, player.UserIDString));
						return;
					}
				} else {
					SendReply (player, string.Format (lang.GetMessage ("NoPlayersFoundByName", this, player.UserIDString), args[0]));
					return;
				}
			}

			int n;
			if (!int.TryParse (args[1], out n)) {
				SendReply (player, lang.GetMessage ("InvalidTime", this, player.UserIDString));
				return;
			}

			BasePlayer offender = GetPlayerBySteamID (offenderSteamid);

			if (offender != null)
				offender.Kick (lang.GetMessage ("Banned", this, offender.UserIDString));

			bool global = args.Length == 4 && args[3] == "true";
			Ban (offenderSteamid, args[1], args[2], global, steamid, player);
		}

		[ChatCommand ("unban")]
		void cmdUnban (BasePlayer player, string command, string[] args) {
			if (player.net.connection.authLevel != 2 && !permission.UserHasPermission (player.UserIDString, this.Title.ToLower () + ".unban")) {
				SendReply (player, lang.GetMessage ("NoPermission", this, player.UserIDString));
				return;
			} else if (args == null || args.Length < 1) {
				SendReply (player, lang.GetMessage ("UnbanCommandUsage", this, player.UserIDString));
				return;
			} else if (!args[0].IsSteamId ()) {
				SendReply (player, string.Format (lang.GetMessage ("InvalidSteamid", this, player.UserIDString), args[0]));
				return;
			}

			Unban (args[0].ToString (), player);
		}

		[ChatCommand ("sync")]
		void cmdSync (BasePlayer player, string command, string[] args) {
			if (!connected) {
				if (player.net.connection.authLevel >= 1)
					SendApiConnectionWarning (player);
				return;
			}

			QueueUserToPost (player.UserIDString);
			SendReply (player, lang.GetMessage ("Syncing", this, player.UserIDString));
		}
		#endregion

		#region Localization
		protected override void LoadDefaultMessages () {
			lang.RegisterMessages (new Dictionary<string, string> {
				{ "BanCommandUsage", "Usage: /ban <(partial) name/SteamID64> <time in minutes (0 for permanent)> \"<reason>\" <global?>" },
				{ "Banned", "You've been banned from the server" },
				{ "ConsoleApiConnectionChecking", "Checking connection to web API" },
				{ "ConsoleApiConnectionSuccess", "Connection established and token validated successfully" },
				{ "ConsoleApiRequestFailed", "Web API request failed. Code: {0}. Response: {1}" },
				{ "ConsoleApiRequestFailedBan", "Failed to ban user. Code: {0}. Response: {1}" },
				{ "ConsoleApiRequestFailedRoleAdd", "Failed to add role \"{0}\". Code: {1}. Response: {2}" },
				{ "ConsoleApiRequestFailedRoleRevoke", "Failed to revoke role \"{0}\". Code: {1}. Response: {2}" },
				{ "ConsoleApiRequestFailedUnban", "Failed to unban user. Code: {0}. Response: {1}" },
				{ "ConsoleCreatingGroup", "Creating group \"{0}\"" },
				{ "ConsoleGrantingGroup", "Granting the \"{0}\" group to {1} ({2})" },
				{ "ConsolePostingUsers", "Posting {0} user(s)" },
				{ "ConsoleProcessingUserData", "Processing user data for {0} ({1})" },
				{ "ConsoleRevokingGroup", "Revoking the \"{0}\" group from {1} ({2})" },
				{ "ConsoleRoleAdded", "Role \"{0}\" added for {1}" },
				{ "ConsoleRoleRevoked", "Role \"{0}\" revoked from {1}" },
				{ "ConsoleSyncingRoles", "Syncing roles for {0} ({1})" },
				{ "DebugChatPrefix", "[Ember] " },
				{ "GroupGranted", "You've been granted the {0} group" },
				{ "GroupRevoked", "Your {0} group has been revoked" },
				{ "InvalidSteamid", "Invalid SteamID" },
				{ "InvalidTime", "Time must be a number, 0 for permanent" },
				{ "MultiplePlayersFound", "Multiple players found, please be more specific" },
				{ "NoApiConnection", "The plugin is not connected to the web API. Check the server console for details" },
				{ "NoPermission", "You don't have the required permissions to do that" },
				{ "NoPlayersFoundByName", "Player not found by name \"{0}\"" },
				{ "PackageExpired", "Your {0} package has expired" },
				{ "PlayerBanned", "Player banned" },
				{ "PlayerUnbanned", "Player unbanned" },
				{ "RedeemingPackage", "Redeeming the {0} package" },
				{ "Syncing", "Synchronizing groups & purchases" },
				{ "UnbanCommandUsage", "Usage: /unban <SteamID64>" },
			}, this, "en");
			Puts ("Default messages created");
		}
		#endregion
	}
}