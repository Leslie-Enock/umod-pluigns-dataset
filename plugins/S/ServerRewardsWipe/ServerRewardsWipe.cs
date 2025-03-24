using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
	[Info("Server Rewards Wipe", "ZEODE", "1.1.3")]
	[Description("Reset Server Rewards player RP on wipe or command.")]
	public class ServerRewardsWipe : CovalencePlugin
	{
		[PluginReference]
		private Plugin ServerRewards;

		private const string permAdmin = "serverrewardswipe.admin";

		private _PlayerData _playerData;
		private DynamicConfigFile _playerdata, _playerdatabackup;

		private class _PlayerData
		{
			public Dictionary<ulong, int> playerRP = new Dictionary<ulong, int>();
		}
		
		#region Oxide Hooks

		private void Init()
		{
			permission.RegisterPermission(permAdmin, this);
		}

		private void OnServerInitialized(bool initial)
		{
			_playerdata = Interface.Oxide.DataFileSystem.GetFile("ServerRewards/player_data");
			_playerdatabackup = Interface.Oxide.DataFileSystem.GetFile("ServerRewards/player_data_backup");

			timer.Once(5f, ()=>
			{
				if (!ServerRewards)
				{
					Puts("WARNING: ServerRewards plugin not found, unloading.");
					Interface.Oxide.UnloadPlugin(Name);
					return;
				}
				LoadRPData();
			});
		}

		private void OnNewSave(string filename)
		{
			if (config.options.ResetRPOnWipe)
			{
				// Make sure server startup is finished
				timer.Once(config.options.WipeDelay, ()=>
				{
					Puts($"INFO: Wipe detected, ServerRewards player RP reset in {config.options.WipeDelay} seconds...");
					ClearRPData();
				});
			}
		}

		#endregion

		#region Main

		private void LoadRPData()
		{
			try
			{
				_playerData = _playerdata.ReadObject<_PlayerData>();
				if (_playerData == null)
				{
					Puts($"ERROR: ServerRewards received data was null, aborted.");
					return;
				}
			}
			catch (Exception ex)
			{
				if (ex is JsonSerializationException || ex is NullReferenceException || ex is JsonReaderException)
				{
					Puts($"Exception Type: {ex.GetType()}");
					Puts($"INFO: {ex}");
					return;
				}
			}
		}

		private void WipeAndSaveRP()
		{
			if (_playerData == null)
			{
				Puts("ERROR: ServerRewards RP data not wiped, RP data was null.");
				return;
			}

			if (config.options.BackupRP)
			{
				Puts("INFO: ServerRewards RP data backed up to oxide/data/ServerRewards/player_data_backup.json...");
				_playerdatabackup.WriteObject(_playerData);
			}
			_playerData.playerRP.Clear();
			_playerdata.WriteObject(_playerData);
			Puts("INFO: ServerRewards RP data wiped...");
		}

		private void ClearRPData()
		{
			if (ServerRewards)
			{
				Puts("INFO: Unloading ServerRewards plugin...");
				Interface.Oxide.UnloadPlugin("ServerRewards");
				timer.Once(2f,()=>
				{
					WipeAndSaveRP();
					Puts("INFO: Finished, reloading ServerRewards.");
					Interface.Oxide.LoadPlugin("ServerRewards");
				});
			}
			else
			{
				Puts("ERROR: ServerRewards is not loaded, aborting.");
			}
		}

		[Command("clearrpdata")]
		private void cmdClearRPData(IPlayer player, string command, string[] args)
		{
			if (!player.HasPermission(permAdmin))
			{
				player.Reply(lang.GetMessage("NoPermission", this, player.Id));
				return;
			}
			ClearRPData();
		}

		#endregion
		
		#region Config & Language

		private ConfigData config;
		private class ConfigData
		{
			[JsonProperty(PropertyName = "Options")]
			public Options options;

			public class Options
			{
				[JsonProperty(PropertyName = "Reset ServerRewards player RP on wipe")]
				public bool ResetRPOnWipe { get; set; }
				[JsonProperty(PropertyName = "Backup player RP before wiping")]
				public bool BackupRP { get; set; }
				[JsonProperty(PropertyName = "Wipe delay (seconds) after server startup. Try increasing if RP wipe fails on startup")]
				public float WipeDelay { get; set; }
			}
			public VersionNumber Version { get; set; }
		}

		private ConfigData GetDefaultConfig()
		{
			return new ConfigData
			{
				options = new ConfigData.Options
				{
					ResetRPOnWipe = true,
					BackupRP = true,
					WipeDelay = 30f
				},
				Version = Version
			};
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();
			try
			{
				config = Config.ReadObject<ConfigData>();
				if (config == null)
				{
					LoadDefaultConfig();
				}
				else
				{
					UpdateConfigValues();
				}
			}
			catch (Exception ex)
			{
				if (ex is JsonSerializationException || ex is NullReferenceException || ex is JsonReaderException)
				{
					LoadDefaultConfig();
					return;
				}
				throw;
			}
		}

		protected override void LoadDefaultConfig()
		{
			Puts("Configuration file missing or corrupt, creating default config file. Ignore if this is first load.");
			config = GetDefaultConfig();
			SaveConfig();
		}

		protected override void SaveConfig()
		{
			Config.WriteObject(config);
		}

		private void UpdateConfigValues()
		{
			if (config.Version < Version)
			{
				ConfigData defaultConfig = GetDefaultConfig();

				Puts("Config update detected! Updating config file...");
				if (config.Version < new VersionNumber(1, 1, 0))
				{
					config = defaultConfig;
				}
				if (config.Version < new VersionNumber(1, 1, 3))
				{
					config.options.WipeDelay = defaultConfig.options.WipeDelay;
				}
				Puts("Config update completed!");
			}
			config.Version = Version;
			SaveConfig();
		}

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["NoPermission"] = "You do not have permission to use this command."
			}, this, "en");
		}

		#endregion
	}
}