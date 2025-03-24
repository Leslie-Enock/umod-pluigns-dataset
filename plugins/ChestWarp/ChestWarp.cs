using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Xml;
using Newtonsoft.Json;
using UnityEngine;
using Facepunch;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;


namespace Oxide.Plugins
{
    [Info("Chest Warp", "CEbbinghaus", "1.2.1")]
    [Description("Create warp between two chests")]
    class ChestWarp : RustPlugin
    {
        #region Variables
        public class Warp
        {
			[JsonProperty("User")]
			public ulong User = 0;
			[JsonProperty("First chest")]
             public uint FirstPoint = 0;
            [JsonProperty("Second chest")]
             public uint SecondPoint = 0;
			 public Warp(){}
			 public Warp(ulong uid){
				 User = uid;
			 }
        }

		 public class setup{
			 public bool isActive = false;
			 public string id = "";
			 public Warp warp = new Warp();
			 public setup(ulong id){
				 warp = new Warp(id);
			 }
		}

		[JsonProperty("Settings")]
		Dictionary<string, Dictionary<string, bool>> settings = new Dictionary<string, Dictionary<string, bool>>();

         public Dictionary<string, Warp> chestWarps = new Dictionary<string, Warp>();
		 Dictionary<ulong, setup> activeBinds = new Dictionary<ulong, setup>();
        #endregion

		#region OxideHooks
        void Unload() => Interface.Oxide.DataFileSystem.WriteObject("ChestWarp/Chests", chestWarps);

		void LoadDefaultMessages(){            
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PERMISSION"] = "You do not have the permissions to use this Command!",
                ["NOEXIST"] = "The Command you are looking for doesn't Exist!",
				["ENTITY"] = "You arent Looking At a valid Entity",
                ["GENERATED"] = "Generated new Warp with the id of: {0}",
                ["SUCCSESS"] = "Finished linking the chest to the warp id: {0}",
                ["SINGLEWARP"] = "You can only link a single warp to a box",
                ["SAMEBOX"] = "You cannot Link a Box to Itself",
				["OWNERSHIP"] = "This Warp Doesnt Belong to you",
				["REMOVED"] = "Removed Warp with the id of: {0}",
                ["NOWARP"] = "There is no Warp associated with this Box",
				["UNMATCHED"] = "Removed Unmatched Warp with the id of: {0}",
                ["CANCELERR"] = "There is Nothing to Cancel",
                ["CANCEL"] = "Cancelled Warp Linking with the id of: {0}",
				["HELP"] = "Commands Are:" +
                             "\n /cw add (or just /cw) - Adds a Chest to the current Wap Pairing" +
                             "\n /cw cancel - Cancels the current Warp Pairing" +
                             "\n /cw clear - Clears a Chest from a warp" +
                             "\n /cw help - Displays This",  
                ["RAY.NULL"] = "You are not looking at a Valid Entity!"
            }, this);
		}

		void Init(){
			if (Interface.Oxide.DataFileSystem.ExistsDatafile("ChestWarp/Chests"))
                chestWarps = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, Warp>>("ChestWarp/Chests");

			permission.RegisterPermission("chestwarp.use", this);
			permission.RegisterPermission("chestwarp.admin", this);
		}
		
		void OnServerSave() {
			Clean();
			Interface.Oxide.DataFileSystem.WriteObject("ChestWarp/Chests", chestWarps);
		}

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
			if (player.IsDucked()) {
				return;
			}

            Warp warp = null;
            if (chestWarps.Any(p => p.Value.FirstPoint == entity.net.ID || p.Value.SecondPoint == entity.net.ID))
                warp = chestWarps.First(p => p.Value.FirstPoint == entity.net.ID || p.Value.SecondPoint == entity.net.ID).Value;
            else
                return;


			BaseEntity teleportBox = null;
			if(warp.FirstPoint == entity.net.ID)
            	teleportBox = BaseNetworkable.serverEntities.Find(warp.SecondPoint) as BaseEntity;
			else
            	teleportBox = BaseNetworkable.serverEntities.Find(warp.FirstPoint) as BaseEntity;



            if (teleportBox == null){
				var id = chestWarps.Where(v => v.Value.FirstPoint == entity.net.ID || v.Value.SecondPoint == entity.net.ID).First().Key;
				chestWarps.Remove(id);
				SendReply(player, Lang("UNMATCHED", player.UserIDString, id));//lang.GetMessage("UNMATCHED", this, player.UserIDString) + id);
				return;
			}
            
            timer.Once(0.01f, player.EndLooting);

            
			Teleport(player, teleportBox.transform.position + new Vector3(0f, 2f, 0f));
        }

		void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
		{
			if (chestWarps.Any(v => v.Value.FirstPoint == entity.net.ID || v.Value.SecondPoint == entity.net.ID)) {
				var id = chestWarps.Where(v => v.Value.FirstPoint == entity.net.ID || v.Value.SecondPoint == entity.net.ID).First().Key;
				chestWarps.Remove(id);
				//Puts("Removed Warp with the id of: " + id);
			}
		}

		#endregion

		#region Commands

		[ChatCommand("cw")]
        void cmdWarp(BasePlayer player, string command, string[] args)
        {
			if (!permission.UserHasPermission(player.UserIDString, "chestwarp.use") && !permission.UserHasPermission(player.UserIDString, "chestwarp.admin")) {
				SendReply(player, Lang("PERMISSION",  player.UserIDString ));
				return;
			}

			if (!activeBinds.ContainsKey(player.userID)){
				activeBinds.Add(player.userID, new setup(player.userID));
			}
			
			RaycastHit hitInfo;

			if (args.Length > 0){
				switch (args[0]) {
					case "cancel":
						if (activeBinds[player.userID].isActive == false){
							SendReply(player, Lang("CANCELERR",  player.UserIDString ));
							return;
						}

						var currentBind = activeBinds[player.userID];
						SendReply(player, Lang("CANCEL",  player.UserIDString, currentBind.id));
						currentBind.isActive = false;
						return;
					break;
					case "help":
						SendReply(player, Lang("HELP",  player.UserIDString ));
						return;
					break;
				}
			}
			
			bool isLookingAtObject = Physics.Raycast(player.eyes.position, Quaternion.Euler(player.GetNetworkRotation().eulerAngles) * Vector3.forward, out hitInfo, 5f, LayerMask.GetMask(new string[] { "Deployed" }));
			
			if(!isLookingAtObject){
				SendReply(player, Lang("RAY.NULL",  player.UserIDString ));
				return;
			}

			if (!(hitInfo.GetEntity() is BoxStorage)){
				SendReply(player, Lang("RAY.NULL",  player.UserIDString ));
				return;
			}

			var boxid = hitInfo.GetEntity().net.ID;

			if (args.Length > 0 && args[0] == "clear"){
					
				if (!chestWarps.Any(v => v.Value.FirstPoint == boxid || v.Value.SecondPoint == boxid)){
					SendReply(player, Lang("NOWARP",  player.UserIDString ));
					return;
				}

				var id = chestWarps.First(v => v.Value.FirstPoint == boxid || v.Value.SecondPoint == boxid);
				if(id.Value.User != player.userID && !permission.UserHasPermission(player.UserIDString, "chestwarp.admin")){
					SendReply(player, Lang("OWNERSHIP",  player.UserIDString ));
					return;
				}

				chestWarps.Remove(id.Key);
				SendReply(player, Lang("REMOVED",  player.UserIDString, id.Key));

				return;
			}

			setup playerBind = activeBinds[player.userID];
			if (chestWarps.Any(i => i.Value.FirstPoint == boxid || i.Value.SecondPoint == boxid)) {
				SendReply(player, Lang("SINGLEWARP",  player.UserIDString ));
				return;
			}

			if (playerBind.isActive){
				if (boxid == playerBind.warp.FirstPoint){
					SendReply(player, Lang("SAMEBOX",  player.UserIDString ));
					return;
				}

				playerBind.warp.SecondPoint = hitInfo.GetEntity().net.ID;
				chestWarps.Add(playerBind.id, playerBind.warp);
				playerBind.isActive = false;
				activeBinds[player.userID].warp = new Warp();
				SendReply(player, Lang("SUCCSESS",  player.UserIDString, playerBind.id));
			}
			else{
				string id = Guid.NewGuid().ToString("N");
				playerBind.id = id;
				playerBind.warp.FirstPoint = hitInfo.GetEntity().net.ID;
				playerBind.isActive = true;
				SendReply(player, Lang("GENERATED",  player.UserIDString, id));
			}
			return;
        }

		#endregion

		#region function
		void Clean(){
			foreach(var i in chestWarps) {
				Warp warp = i.Value;
				BaseEntity firstBox = BaseNetworkable.serverEntities.Find(warp.FirstPoint) as BaseEntity;
				BaseEntity secondBox = BaseNetworkable.serverEntities.Find(warp.SecondPoint) as BaseEntity;
				if (firstBox == null || secondBox == null)
				{
					chestWarps.Remove(i.Key);
					//Puts("Removed: " + i.Key);
				}
			}
		}
		void Teleport(BasePlayer player, Vector3 pos) {
			if (player.net?.connection != null)
				player.ClientRPCPlayer(null, player, "StartLoading");
			player.StartSleeping();
			player.MovePosition(pos);
			if (player.net?.connection != null)
				player.ClientRPCPlayer(null, player, "ForcePositionTo", pos);
			if (player.net?.connection != null)
				player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
			player.UpdateNetworkGroup();
			player.SendFullSnapshot();
		}

		//Written by Wulf @https://umod.org/plugins/bed-rename-blocker
		private string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
		#endregion
	}
}