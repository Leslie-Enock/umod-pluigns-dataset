using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
	[Info("Permission Notifications", "klauz24", "1.0.5"), Description("Allows you to notify players when they get or lose some permission.")]
	internal class PermissionNotifications : CovalencePlugin
	{
		private Configuration _config;

		private class Configuration
		{
			[JsonProperty(PropertyName = "Granted (permission or group, lang key)")]
			public Dictionary<string, string> Granted = new Dictionary<string, string>()
			{
				{"permissionOrGroup", "GotExample"}
			};

			[JsonProperty(PropertyName = "Revoked (permission or group, lang key)")]
			public Dictionary<string, string> Revoked = new Dictionary<string, string>()
			{
				{"permissionOrGroup", "LostExample"}
			};

			public string ToJson() => JsonConvert.SerializeObject(this);

			public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
		}

		protected override void LoadDefaultConfig() => _config = new Configuration();

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				_config = Config.ReadObject<Configuration>();
				if (_config == null)
				{
					throw new JsonException();
				}

				if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
				{
					Puts("Configuration appears to be outdated; updating and saving");
					SaveConfig();
				}
			}
			catch
			{
				Puts($"Configuration file {Name}.json is invalid; using defaults");
				LoadDefaultConfig();
			}
		}

		protected override void SaveConfig()
		{
			Puts($"Configuration changes saved to {Name}.json");
			Config.WriteObject(_config, true);
		}

		// Borrowed from Wulf's Auto Broadcast plugin
		private void OnServerInitialized()
		{
			if (lang.GetLanguages(this).Length == 0 || lang.GetMessages(lang.GetServerLanguage(), this)?.Count == 0)
			{
				lang.RegisterMessages(new Dictionary<string, string>
				{
					{"GotExample", "Yo you just got a permission or a group!"},
					{"LostExample", "F you just lost a permission or a group!"}
				}, this, lang.GetServerLanguage());
			}
			else
			{
				foreach (var language in lang.GetLanguages(this))
				{
					var messages = new Dictionary<string, string>();
					foreach (var message in lang.GetMessages(language, this)) messages.Add(message.Key, message.Value);
					lang.RegisterMessages(messages, this, language);
				}
			}
		}

		private void OnUserPermissionGranted(string id, string permName) => NotifyPlayer(_config.Granted, id, permName, true);

		private void OnUserPermissionRevoked(string id, string permName) => NotifyPlayer(_config.Revoked, id, permName, true);

		private void OnUserGroupAdded(string id, string groupName) => NotifyPlayer(_config.Granted, id, groupName, false);

		private void OnUserGroupRemoved(string id, string groupName) => NotifyPlayer(_config.Revoked, id, groupName, false);

		private void NotifyPlayer(Dictionary<string, string> dict, string id, string str, bool isPerm)
		{
			if (dict.ContainsKey(str))
			{
				var exists = false;
				if (isPerm)
				{
					exists = permission.PermissionExists(str);
				}
				else
				{
					exists = permission.GroupExists(str);
				}
				if (exists)
				{
					var player = players.FindPlayer(id);
					if (player?.IsConnected == true)
					{
						var message = GetLang(player, dict[str]);
						if (message != null)
						{
							player.Message(message);
						}
						else
						{
							PrintError($"Failed to get a message for {str}.");
						}
					}
				}
				else
				{
					PrintError($"{str} doesn't exist?");
				}
			}
		}

		private string GetLang(IPlayer player, string langKey) => lang.GetMessage(langKey, this, player.Id);
	}
}