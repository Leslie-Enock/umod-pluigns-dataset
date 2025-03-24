using Facepunch;
using Rust;
using System;
using System.Linq;
using System.Globalization;
using System.Collections.Generic;
using System.Text;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("Real Bear Traps", "XavierB", "0.0.8")]
	[Description("Makes bear traps actually trap bears")]
	
	public class RealBearTraps : RustPlugin
    {

		static RealBearTraps plugin;
		const string perms = "realbeartraps.auth";

		private bool ConfigChanged;
		private DynamicConfigFile data;
		private StoredData storedData;
		
		// Confiig defaults
		bool TrapTeam = true;
		float TrapDamage = 1000f;
		float TrapRadius = 1f;
		bool TrapBears = true;
		bool TrapChicken = true;
		bool TrapStag = true;
		bool TrapBoar = true;
		bool TrapWolf = true;
		bool Messages = true;

		// Dictionaries and Lists
		private List<ulong> BearTrapList = new List<ulong>();
		
		private class StoredData
        {
            public List<ulong> BearTrapList = new List<ulong>();
		}
		
		
		private void SaveData()
        {
            storedData.BearTrapList = BearTrapList;
			data.WriteObject(storedData);
			PrintWarning(lang.GetMessage("saving", this));
		}
		
		void LoadVariables()
		{
			Messages = Convert.ToBoolean(GetConfig("Chat Settings","Show chat messages?", "true"));
			TrapDamage = Convert.ToSingle(GetConfig("Settings", "Damage delt to animals", "1000"));
			TrapTeam = Convert.ToBoolean(GetConfig("Settings","Team members authed on trap?", "true"));
			TrapRadius = Convert.ToSingle(GetConfig("Settings", "Radius to trigger traps", "1"));
			TrapBears = Convert.ToBoolean(GetConfig("Settings","Can trap bears?", "true"));
			TrapChicken = Convert.ToBoolean(GetConfig("Settings","Can trap chickens?", "true"));
			TrapStag = Convert.ToBoolean(GetConfig("Settings","Can trap stags?", "true"));
			TrapWolf = Convert.ToBoolean(GetConfig("Settings","Can trap wolves?", "true"));
			TrapBoar = Convert.ToBoolean(GetConfig("Settings","Can trap boars?", "true"));
			
			if (ConfigChanged)
			{
				PrintWarning(lang.GetMessage("configchange", this));
				SaveConfig();
			}
			else
			{
				ConfigChanged = false;
				return;
			}
		}
		
		#region Config Reader
		
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
		
		protected override void LoadDefaultConfig()
		{
			LoadVariables();
		}
		
		void Init()
		{
			LoadVariables();
			permission.RegisterPermission(perms, this);
			
			plugin = this;
			
			data = Interface.Oxide.DataFileSystem.GetFile(Name);
			
			try
            {
                storedData = data.ReadObject<StoredData>();
				BearTrapList = storedData.BearTrapList;
				}
            catch
            {
                PrintWarning(lang.GetMessage("failedload", this));
                storedData = new StoredData();
            }
		}
		
		private void OnServerSave() => SaveData();
		
		void Unload()
        {
			foreach (var trap in UnityEngine.Object.FindObjectsOfType<BaseTrap>())
            {
				var bearTrap = trap.GetComponent<ColliderCheck>();
				if (bearTrap)
				{
					bearTrap.UnloadComponent();
				}
			}
			
			SaveData();	
			plugin = null;
		}
		
		void OnServerInitialized()
		{
			foreach (var trap in UnityEngine.Object.FindObjectsOfType<BaseTrap>())
            {
				if (BearTrapList.Contains(trap.net.ID))
				{
					trap.gameObject.AddComponent<ColliderCheck>();
				}
			}
		}
		
		private static BasePlayer FindOwner(string nameOrId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrId)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
            }
			return null;
		}
		
		#region Hooks
		private void OnEntitySpawned(BearTrap trap)
		{
			trap.gameObject.AddComponent<ColliderCheck>();
			var player = FindOwner(trap.OwnerID.ToString());
			if (player == null) return;
			BearTrapList.Add(trap.net.ID);
			if (Messages)
				Player.Message(player, $"{lang.GetMessage("TrapPlaced", this, player.UserIDString)}");
		}
		
		private object OnTrapTrigger(BearTrap trap, GameObject obj)
		{
			BasePlayer target = obj.GetComponent<BasePlayer>();
			
			if (target == null)
				return null;
			
			bool hasPermission = permission.UserHasPermission(target.UserIDString, perms);
			if (!hasPermission)
				return null;
			
			if (target.UserIDString == trap.OwnerID.ToString())
			{
				return true;
			}
			
			if (target.currentTeam != (long)0)
			{
				RelationshipManager.PlayerTeam trapTeam = RelationshipManager.ServerInstance.FindTeam(target.currentTeam);
				if (TrapTeam)
				{
					if (trapTeam.members.Contains(trap.OwnerID))
					{
						return true;
					}
				}
			}
			
			return null;
		}

		private void OnEntityKill(BearTrap trap)
		{
			trap.GetComponent<ColliderCheck>()?.OnKill(trap);
		}
		
		#endregion
		
		#region Behaviour
		
		class ColliderCheck : FacepunchBehaviour
		{
			private BaseTrap trap;
			private BaseCombatEntity targetEnt;
			private float radius;
			
			void Awake()
			{
				trap = GetComponent<BaseTrap>();
				radius = plugin.TrapRadius;
			}
			
			void FixedUpdate()
			{
				if (targetEnt == null && trap.HasFlag(BaseEntity.Flags.On))
				{
					List<BaseNpc> nearby = new List<BaseNpc>();
					Vis.Entities(transform.position, radius, nearby);
					foreach (var e in nearby)
					{
						if (e == null) continue;
						if (e is Bear && !plugin.TrapBears) continue;
						if (e is Stag && !plugin.TrapStag) continue;
						if (e is Wolf && !plugin.TrapWolf) continue;
						if (e is Boar && !plugin.TrapBoar) continue;
						if (e is Chicken && !plugin.TrapChicken) continue;
						targetEnt = e;
					}
				}
				
				if (targetEnt != null)
				{
					var distance = Vector3.Distance(transform.position, targetEnt.transform.position);
					if (distance > radius)
					{
						targetEnt = null;
					}
					else
					{
						trap.SetFlag(BaseEntity.Flags.On, false, false, true);
						trap.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
						targetEnt.Hurt(plugin.TrapDamage);
						targetEnt = null;
					}
				}
			}
	
			internal void OnKill(BearTrap trap)
			{
				if (plugin.BearTrapList.Contains(trap.net.ID))
					plugin.BearTrapList.Remove(trap.net.ID);
			}
			
			public void UnloadComponent()
			{
				Destroy(this);
			}
			
			public void Destroy()
			{
				if (plugin.BearTrapList.Contains(trap.net.ID))
					plugin.BearTrapList.Remove(trap.net.ID);
			}

		}
		
		#endregion
		
		
		#region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
				{"TrapPlaced", "You have placed a BearTrap."},
				{"saving", "Saving..."},
				{"configchange", "Config has changed."},
				{"failedload", "Falied to load, creating new config."},
            }, this, "en");
        }
        #endregion
	}
	
}