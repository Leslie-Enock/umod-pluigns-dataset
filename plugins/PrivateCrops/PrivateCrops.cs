using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;

/*
 * This update 1.1.5
 * Updated old methods
 * Added new oxide hook
 *
 * This update 1.2.0
 * Added Clans, Rustio Clans, Friends and team support.
 * Added Plant death prevention option
 * Added More Config Options
 * Added 2 new hooks OnGrowableStateChange, OnEntityBuilt
 */

namespace Oxide.Plugins
{
	[Info("Private Crops", "Khan", "1.2.0")]
	[Description("Protects player's crops from being stolen!")]
	class PrivateCrops : CovalencePlugin
	{
		#region Refrences

		[PluginReference]
		private Plugin Clans, Friends;

		#endregion
		
		#region Variables
		
		private PluginConfig _config;
		
		private const string messagebypass = "privatecrops.message.bypass";
		private const string protectionbypass = "privatecrops.protection.bypass";
		private const string instant = "privatecrops.speed";

		#endregion

		#region Config
		
		private void Init()
		{
			permission.RegisterPermission(messagebypass, this);
			permission.RegisterPermission(protectionbypass, this);
			permission.RegisterPermission(instant, this);
		}

		protected override void LoadDefaultMessages()
		{
			lang.RegisterMessages(new Dictionary<string, string>
			{
				["message"] = "<color={0}>This crop is not yours! Do not steal from other players!</color>",
			}, this);
		}
		private class PluginConfig
		{
			[JsonProperty("Use Tool Cupboard Protection")]
			public bool EnableTC = true;
			
			[JsonProperty("Set Max Seasons the plant regrows")]
			public int MaxSeasons = 1;
			
			[JsonProperty("Set Max Harvests")]
			public int MaxHarvests = 1;
			
			[JsonProperty("Prevent Plants from dying")]
			public bool Dying = false;

			[JsonProperty("Warning Message Color")]
			public string MessageColor = "#ff0000";
			
			[JsonProperty("Use Clan Protection")]
			public bool EnableClans = false;
			
			[JsonProperty("Use Teams Protection")]
			public bool EnableTeams = false;
			
			[JsonProperty("Use Friends only Protection")]
			public bool EnableFriends = false;

			public string ToJson() => JsonConvert.SerializeObject(this);

			public Dictionary<string, object> ToDictionary() =>
				JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
		}

		protected override void LoadConfig()
		{
			base.LoadConfig();

			try
			{
				_config = Config.ReadObject<PluginConfig>();

				if (_config == null)
				{
					throw new JsonException();
				}

				if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
				{
					PrintWarning($"Configuration file {Name}.json was Updated");
					SaveConfig();
				}
			}
			catch
			{
				PrintError("Configuration file is corrupt! Loading Default Config");
				LoadDefaultConfig();
			}
		}
		protected override void SaveConfig() => Config.WriteObject(_config, true);
		protected override void LoadDefaultConfig() => _config = new PluginConfig();
		
		#endregion

		#region Hooks
		
		private void OnEntityBuilt(Planner planner, GameObject seed)
		{
			var player = planner.GetOwnerPlayer();
			var plant = seed.GetComponent<GrowableEntity>();

			NextTick(() =>
			{
				if (plant == null || plant.planter == null) return;
				if (player != null && permission.UserHasPermission(player.UserIDString, instant))
				{
					plant.ChangeState(PlantProperties.State.Fruiting, false);
				}
				if (_config.MaxSeasons != 1)
				{
					plant.Properties.MaxSeasons = _config.MaxSeasons;
				}
				if (_config.MaxHarvests != 1)
				{
					plant.Properties.maxHarvests = _config.MaxHarvests;
				}
			});

		}
		
		private object CanTakeCutting(BasePlayer player, GrowableEntity growable)
		{
			if (player == null || growable == null) return null;
			return CropsProtected(player, growable);
		}

		private object OnGrowableGather(GrowableEntity growable, BasePlayer player)
		{
			if (player == null || growable == null) return null;
			return CropsProtected(player, growable);
		}

		private object OnGrowableGathered(GrowableEntity growable, Item item, BasePlayer player)
		{
			if (player == null || growable == null || item == null) return null;
			return CropsProtected(player, growable);
		}

		private bool? OnGrowableStateChange(GrowableEntity growableEntity, PlantProperties.State state)
		{
			if (growableEntity == null) return null;
			if (_config.Dying && growableEntity.currentStage.nextState == PlantProperties.State.Dying)
			{
				return true;
			}
			return null;
		}

		#endregion

		#region Helpers
		private object CropsProtected(BasePlayer player, GrowableEntity growable)
		{
			if (player.IPlayer.HasPermission(protectionbypass))
			{
				return null;
			}

			if (_config.EnableTC)
			{
				if (player.IsBuildingBlocked())
				{
					WarnPlayer(player);
					return true;
				}
			}
			else if (!IsOwner(player.userID, growable.OwnerID))
			{
				WarnPlayer(player);
				return true;
			}

			return null;
		}
		public void WarnPlayer(BasePlayer player)
		{
			if (player.IPlayer.HasPermission(messagebypass) == false)
			{
				player.IPlayer.Message(string.Format(lang.GetMessage("message", this, player.IPlayer.Id), Config["MessageColor"]));
			}
		}
		private bool IsOwner(ulong userID, ulong owner)
        {
            if (userID == owner)
            {
                return true;
            }
            
            if (_config.EnableClans && SameClan(userID, owner))
            {
	            return true;
            }
            
            if (_config.EnableTeams && SameTeam(userID, owner))
            {
                return true;
            }

            if (_config.EnableFriends && AreFriends(userID, owner))
            {
                return true;
            }

            return false;
        }
        private bool SameTeam(ulong playerID, ulong friendID)
        {
            if (!RelationshipManager.TeamsEnabled()) return false;
            var playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerID);
            if (playerTeam == null) return false;
            var friendTeam = RelationshipManager.ServerInstance.FindPlayersTeam(friendID);
            if (friendTeam == null) return false;
            return playerTeam == friendTeam;
        }
        private bool AreFriends(ulong playerID, ulong friendID)
        {
            if (Friends == null) return false;

            return (bool)Friends.Call("AreFriends", playerID, friendID);
        }
        private bool SameClan(ulong playerID, ulong friendID)
        {
            if (Clans == null) return false;
            //Clans
            var isMember = Clans.Call("IsClanMember", playerID.ToString(), friendID.ToString());
            if (isMember != null) return (bool)isMember;
            //Rust:IO Clans
            var playerClan = Clans.Call("GetClanOf", playerID);
            if (playerClan == null) return false;
            var friendClan = Clans.Call("GetClanOf", friendID);
            if (friendClan == null) return false;
            return (string)playerClan == (string)friendClan;
        }

        #endregion Helpers

	}
}
