using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Facepunch;
using Rust.Ai.Gen2;

namespace Oxide.Plugins
{
    [Info("Sleeper Animal Protection", "Fujikura/Krungh Crow/Lorenzo", "1.0.10")]
	[Description("Protects sleeping players from being killed by animals")]
    class SleeperAnimalProtection : CovalencePlugin
    {

        private readonly int buildingLayer = LayerMask.GetMask("Terrain", "World", "Construction", "Deployed");

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Settings")]
            public Settings setting = new Settings();
        };

        private class Settings
        {
            [JsonProperty(PropertyName = "OnNpcTarget return code (debug warning issue with TruePVE)")]
            public bool OnNpcReturnCode = true;

            [JsonProperty(PropertyName = "Permission name")]
            public string permissionName = "sleeperanimalprotection.active";   // name of permission

            [JsonProperty(PropertyName = "Required to sleep ON foundation")]
            public bool checkForFoundation = false;

            [JsonProperty(PropertyName = "Use permissions")]
            public bool usePermission = false;      // use permission or grant access to every players

            [JsonProperty(PropertyName = "Animal ignore sleepers")]
            public bool AnimalIgnoreSleepers = true;

            [JsonProperty(PropertyName = "HumanNPC ignore sleepers")]
            public bool HumanNPCIgnoreSleepers = false;
        };

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (Config == null) throw new Exception();
            }
            catch (Exception ex)
            {
                PrintError(ex.Message);
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

		void Init()
		{
			permission.RegisterPermission(_config.setting.permissionName, this);
            if (_config.setting.AnimalIgnoreSleepers == false && _config.setting.AnimalIgnoreSleepers == false) Unsubscribe(nameof(OnNpcTargetSense));
        }

        private List<BuildingBlock> GetFoundation(Vector3 positionCoordinates)
        {
            var position = positionCoordinates;
            var entities = new List<BuildingBlock>();
            var hits = Pool.Get<List<BuildingBlock>>();
            Vis.Entities(position, 2.5f, hits, buildingLayer);
            for (var i = 0; i < hits.Count; i++)
            {
                var entity = hits[i];
                if (!entity.ShortPrefabName.Contains("foundation") || positionCoordinates.y < entity.WorldSpaceBounds().ToBounds().max.y) continue;
                entities.Add(entity);
            }
            Pool.FreeUnmanaged(ref hits);
            return entities;
        }	
	
   
        private object OnNpcTargetSense(BaseEntity attacker, BaseEntity target, AIBrainSenses brainSenses)
        {
            //BasePlayer attackerPlayer = attacker as BasePlayer;
            //BaseAnimalNPC attackerAnimal = attacker as BaseAnimalNPC;
            //BasePlayer targetPlayer = target as BasePlayer;
            if (target is BasePlayer targetPlayer)
            {
                if (attacker is BaseAnimalNPC attackerAnimal && !targetPlayer.IsNpc)
                {
                    if (_config.setting.AnimalIgnoreSleepers == true && targetPlayer.IsSleeping())
                    {
                        if (_config.setting.usePermission && !permission.UserHasPermission(targetPlayer.UserIDString, _config.setting.permissionName))
                            return null;

                        if (_config.setting.checkForFoundation && GetFoundation(target.transform.position).Count == 0)
                            return null;

                        try
                        {                            
                            if (brainSenses != null && brainSenses.brain != null)
                            {
                                if ((brainSenses.brain.CurrentState?.StateType == AIState.Attack) ||
                                    (brainSenses.brain.CurrentState?.StateType == AIState.Chase) ||
                                    (brainSenses.brain.CurrentState?.StateType == AIState.Combat)) brainSenses.brain.SwitchToState(AIState.Idle, 0);
                            }
                        }
                        catch (Exception ex)
                        {
                            PrintError(ex.Message);
                            PrintError($"Animal NRE {target.transform.position}");
                        }

                        return _config.setting.OnNpcReturnCode;
                    }
                }

                if (attacker is BasePlayer attackerPlayer && attackerPlayer.IsNpc && !targetPlayer.IsNpc)
                {
                    if (_config.setting.HumanNPCIgnoreSleepers == true && targetPlayer.IsSleeping())
                    {
                        if (_config.setting.usePermission && !permission.UserHasPermission(targetPlayer.UserIDString, _config.setting.permissionName))
                            return null;

                        if (_config.setting.checkForFoundation && GetFoundation(target.transform.position).Count == 0)
                            return null;

                        try
                        {
                            if (brainSenses != null && brainSenses.brain != null)
                            {
                                if ((brainSenses.brain.CurrentState?.StateType == AIState.Attack) ||
                                    (brainSenses.brain.CurrentState?.StateType == AIState.Chase) ||
                                    (brainSenses.brain.CurrentState?.StateType == AIState.Combat)) brainSenses.brain.SwitchToState(AIState.Idle, 0);
                            }
                        }
                        catch (Exception ex)
                        {
                            PrintError(ex.Message);
                            PrintError($"NPC NRE  {target.transform.position}");
                        }

                        return _config.setting.OnNpcReturnCode;
                    }
                }
            }

            return null;
        }
		
		object OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null) return null;
            var npc = info.Initiator as BaseNpc;
			
            if (player.IsSleeping() && npc != null) 
			{
				
				if(_config.setting.usePermission && !permission.UserHasPermission(player.userID.ToString(), _config.setting.permissionName))
					return null;
				if(_config.setting.checkForFoundation && GetFoundation(player.transform.position).Count == 0)
					return null;

                // To stop the attack, hurt animal until it flee. 
                // this method should still work if animal AI change again
                HitInfo newinfo = new HitInfo(player, npc, Rust.DamageType.Generic, npc.Health()*0.6f, player.transform.position);
                npc.Hurt(newinfo);
                return true;
			}

            var wolf2 = info.Initiator as BaseNPC2;
			
            if (player.IsSleeping() && wolf2 != null) 
			{
				
				if(_config.setting.usePermission && !permission.UserHasPermission(player.userID.ToString(), _config.setting.permissionName))
					return null;
				if(_config.setting.checkForFoundation && GetFoundation(player.transform.position).Count == 0)
					return null;

                // To stop the attack, hurt animal until it flee. 
                // this method should still work if animal AI change again
                HitInfo newinfo = new HitInfo(player, wolf2, Rust.DamageType.Generic, wolf2.Health()*0.6f, player.transform.position);
                wolf2.Hurt(newinfo);
                return true;
			}
			
			
			
            return null;
        }		

    }
}