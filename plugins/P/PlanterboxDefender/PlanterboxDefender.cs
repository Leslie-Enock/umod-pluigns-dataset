//#define Debug
using HarmonyLib;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using System;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Planterbox Defender", "kaucsenta", "3.0.0")]
    [Description("Only owner, owner team or if clans plugin present, clan can harvest grownable entities")]

    public class PlanterboxDefender : RustPlugin
    {
        [PluginReference]
        private Plugin Clans, Friends;
        private static PlanterboxDefender _instance;

        [AutoPatch]
        [HarmonyPatch(typeof(GrowableEntity), "TakeClones", new Type[]
        {
            typeof(BasePlayer)
        })]
        public class Patch_TakeClones
        {
            public static bool Prefix(GrowableEntity __instance, BasePlayer player)
            {
                if (_instance.CanLootGrowableEntity(__instance, player) == null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        [AutoPatch]
        [HarmonyPatch(typeof(GrowableEntity), "PickFruit", new Type[]
        {
            typeof(BasePlayer),
            typeof(bool)
        })]
        public class Patch_PickFruit
        {
            public static bool Prefix(GrowableEntity __instance, BasePlayer player, bool eat)
            {
                if(_instance.CanLootGrowableEntity(__instance, player) == null)
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }
        private void OnServerInitialized()
        {
            _instance = this;
            permission.RegisterPermission("planterboxdefender.admin", this);
        }
        private void OnEntityBuilt(Planner plan, GameObject seed)
        {
            var player = plan.GetOwnerPlayer();
            var isSeed = seed.GetComponent<GrowableEntity>();
            if (player == null || isSeed == null)
            {
                return;
            }

            var held = player.GetActiveItem();

            NextTick(() =>
            {
                if (isSeed.GetParentEntity() == null || !(isSeed.GetParentEntity() is PlanterBox))
                { 
                    return; 
                }
                else
                {   
                    if(!(isSeed.GetParentEntity() is PlanterBox))
                    {
                        return;
                    }
                    else
                    {

                        if (held == null)
                        {
                            return;
                        }
                        
                        PlanterBox temp = (PlanterBox)isSeed.GetParentEntity();
                        ulong plantowner = temp.OwnerID;
                        if (plantowner == player.userID)
                        {
#if Debug
            PrintToChat("You can harvest0");
#endif
                            return;
                        }

                        if (SameTeam(plantowner, player.userID))
                        {
#if Debug
            PrintToChat("You can harvest1");
#endif
                            return;
                        }

                        if (SameClan(plantowner, player.userID))
                        {
#if Debug
            PrintToChat("You can harvest2");
#endif
                            return;
                        }
                        if (HasFriend(plantowner, player.userID))
                        {
#if Debug
                    PrintToChat("You can harvest3");
#endif
                            return;
                        }
                        BuildingPrivlidge PrivlidgeToHarvest = temp.GetBuildingPrivilege();
                        if (PrivlidgeToHarvest?.IsAuthed(player) == true)
                        {
#if Debug
                    PrintToChat("You can harvest4");
#endif
                            return ;
                        }
                        player.ChatMessage(lang.GetMessage("Noharvest", this, player.UserIDString));
#if Debug
                PrintToChat(player.userID.ToString());
# endif
                        var refund = ItemManager.CreateByName(held.info.shortname, 1);

                        if (refund != null)
                        {
                            player.inventory.GiveItem(refund);
                        }
                        return; /* This is the case, when the player can't harvest the crops*/
                    }               
                }
            });

        }
                
        object CanLootGrowableEntity(GrowableEntity plant, BasePlayer player)
        {
            if (player == null)
            {
                Puts("Player error");
                return true; /* This is the case, when the player can't harvest the crops*/
            }
            if (plant == null)
            {
                Puts("Plant error");
#if Debug
                PrintToChat(player.userID.ToString());
#endif
                return true; /* This is the case, when the player can't harvest the crops*/
            }
            if (player != null && permission.UserHasPermission(player.UserIDString, "planterboxdefender.admin"))
                return null;
            if (plant.GetPlanter() != null)
            {
                ulong plantowner = plant.GetPlanter().OwnerID;

                if (plantowner == player.userID)
                {
#if Debug
            PrintToChat("You can harvest0");
#endif
                    return null;
                }

                if (SameTeam(plantowner, player.userID))
                {
#if Debug
            PrintToChat("You can harvest1");
#endif
                    return null;
                }

                if (SameClan(plantowner, player.userID))
                {
#if Debug
            PrintToChat("You can harvest2");
#endif
                    return null;
                }
                if(HasFriend(plantowner, player.userID))
                {
#if Debug
                    PrintToChat("You can harvest3");
#endif
                    return null;
                }
                BuildingPrivlidge PrivlidgeToHarvest =  plant.GetPlanter().GetBuildingPrivilege();
                if (PrivlidgeToHarvest?.IsAuthed(player) == true)
                {
#if Debug
                    PrintToChat("You can harvest4");
#endif
                    return null;
                }
                player.ChatMessage(lang.GetMessage("Noharvest", this, player.UserIDString));
#if Debug
                PrintToChat(player.userID.ToString());
#endif
                return true; /* This is the case, when the player can't harvest the crops*/
            }
#if Debug
            PrintToChat("missing planter");
#endif

            return null;
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

        private bool HasFriend(ulong playerID, ulong friendID)
        {
            if (Friends == null) return false;
            return (bool)Friends.Call("HasFriend", playerID, friendID);
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

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Noharvest"] = "You can't harvest this."
            }, this);
        }

    }
}
