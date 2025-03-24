using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;
using Convert = System.Convert;
using System.Linq;
using Oxide.Game.Rust.Cui;
using System;
using System.Text.RegularExpressions;
using Oxide.Core.Plugins;
using Newtonsoft.Json.Linq;

namespace Oxide.Plugins
{
    [Info("CH47 And MiniCopter Spawner", "BuzZ[PHOQUE]", "1.0.4")]
    [Description("Spawn CH47 and/or MiniCopter at saved point(s)")]

/*=======================================================================================================================
*   THANKS TO THE OXIDE/UMOD TEAM for coding quality, ideas, and time spent for the community
*
*   10 february 2019
*
*
*   1.0.0   20190210        creation
*   1.0.1                   code
*   1.0.2                   code
*   1.0.3                   Patch for 2023 May 4th
*=======================================================================================================================*/

    public class CH47AndMiniCopterSpawner : RustPlugin
    {
        bool debug = false;
        bool loaded;
        string Prefix = "[Spawner] :";
        ulong SteamIDIcon = 76561198332562475;
        const string chprefab = "assets/prefabs/npc/ch47/ch47.entity.prefab";
        const string miniprefab = "assets/content/vehicles/minicopter/minicopter.entity.prefab";
        const string SpawnerAdmin = "ch47andminicopterspawner.admin";

//////////////////////////////////////////////////////////////////////////////////////////////////////////

    class StoredData
    {
        public Dictionary<string, Vector3> SpawnSpots = new Dictionary<string, Vector3>();
        public List<ulong> SpawnerCH47 = new List<ulong>();
        public List<ulong> SpawnerMinicopters = new List<ulong>();

        public StoredData()
        {
        }
    }
        private StoredData storedData;

////////////////////////////////////////////////////////////////////////////////
        void Init()
        {
            permission.RegisterPermission(SpawnerAdmin, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }

        void Loaded()
        {
            loaded = true;
        }

        void Unload()
        {
            	foreach (var entity in UnityEngine.Object.FindObjectsOfType<BaseEntity>())
				{
					if (entity.OwnerID == 998877665544)
					{
						if (debug) Puts ("REMOVING ONE COPTER from spawner");
						entity.Kill();
					}
				}
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }
/////////////////////////////////////////
#region MESSAGES

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NoDataMsg", "No spawn point data found."},
                {"WipeMsg", "Datas have been wiped. {0} spots were in datas."},
                {"AlreadySpotNameMsg", "Spot name already taken."},
                {"NotFoundSpotMsg", "Spot name not found."},
                {"RemovedOneSpotMsg", "Spot has been removed."},
                {"HelpMsg", "Commands are :\n/cms_add\n/cms_list\n/cms_allch\n/cms_ch\n/cms_allmini\n/cms_mini\n/cms_del\n/cms_wipe"},
                {"SetSpotNameDeleteMsg", "Please set a spot name to delete"},
                {"SetSpotNameMsg", "Please set a name for this spot.\nexample : /copterspawn_add airport"},
                {"SpotAddedMsg", "Spot {0} added to datas."},
                {"SpawnedMsg", "{0} Helicopter has spawned on {1} spot !."},
                {"KilledMsg", "A {0} Helicopter has been removed/killed."},
                {"NoPermMsg", "You are not allowed to do this."},

            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NoDataMsg", "Aucun point trouvé dans les données."},
                {"WipeMsg", "Données effacées. Il y avait {0} points enregistrés."},
                {"AlreadySpotNameMsg", "Ce nom est déjà pris."},
                {"NotFoundSpotMsg", "Le nom du spot n'a pas été trouvé."},
                {"RemovedOneSpotMsg", "Ces coordonnées ont été effacées."},
                {"HelpMsg", "Les commandes sont :\n/cms_add\n/cms_list\n/cms_allch\n/cms_ch\n/cms_allmini\n/cms_mini\n/cms_del\n/cms_wipe"},
                {"SetSpotNameDeleteMsg", "Veuillez préciser le nom d'un spot à effacer."},
                {"SetSpotNameMsg", "Veuillez saisir un nom pour ces coordonnées.\nexemple : /copterspawn_add chezmoi"},
                {"SpotAddedMsg", "Coordonnées pour {0} enregistrées dans les données."},
                {"SpawnedMsg", "{0} Helicoptère arrivé au point : {1}!"},
                {"KilledMsg", "Un Hélicoptère {0} a disparu du monde."},
                {"NoPermMsg", "Vous n'êtes pas autorisé."},

            }, this, "fr");
        }

#endregion
////////////////////////////////////////////////////
///////////////////
// CHAT /SPAWN
///////////
        [ChatCommand("cms")]
        private void CH47AndMiniCopterSpawnerNoUnderscore(BasePlayer player, string command, string[] args)
        {
            bool spawner = DoesThisPlayerHasPermission(player);
            if (!spawner) return;
            if (args.Length == 0 )
            {
                if (debug) Puts($"... command copterspawn... lonely");
                Player.Message(player, $"{lang.GetMessage("HelpMsg", this, player.UserIDString)}", Prefix, SteamIDIcon);
                return;
            }

            switch (args[0].ToLower())
            {
                case "add" :
                {
                    if (debug) Puts($"cms_add");
                    if (args.Length == 1 )
                    {
                        Player.Message(player, $"{lang.GetMessage("SetSpotNameMsg", this, player.UserIDString)}", Prefix, SteamIDIcon);
                        return;
                    }
                    string spotname = string.Empty;
                    int array = 1;
                    int Round = 1;
                    for (Round = 1; array <= args.Length - 1; Round++)
                    {
                        if (array == 1) spotname = args[array];
                        else spotname = spotname + " " + args[array];
                        array = array + 1;
                    }
                    if (storedData.SpawnSpots.ContainsKey(spotname))
                    {
                        Player.Message(player, $"{lang.GetMessage("AlreadySpotNameMsg", this, player.UserIDString)}", Prefix, SteamIDIcon);
                        return;
                    }
                    storedData.SpawnSpots.Add(spotname, player.transform.position + new Vector3(0f,1f,0f));
                    Player.Message(player, String.Format(lang.GetMessage("SpotAddedMsg", this, player.UserIDString), spotname), Prefix, SteamIDIcon);
                    break;
                }
                case "list" :
                {
                    if (debug) Puts($"cms_list");
                    string listinline = string.Empty;
                    if (storedData.SpawnSpots.Count() >= 1)
                    {
                        int array = 1;
                        foreach (var paire in storedData.SpawnSpots)
                        {
                            player.SendConsoleCommand("ddraw.box", 20f, Color.green, paire.Value, 1f);
                            player.SendConsoleCommand("ddraw.text", 20f, Color.green, paire.Value + new Vector3(0, 1f, 0), $"{paire.Key}");
                            listinline = listinline + $" <color=orange>[{array}]</color> " + paire.Key + " - " + paire.Value + " | ";
                            array = array + 1;
                        }
                        Player.Message(player, listinline, Prefix, SteamIDIcon);
                        return;
                    }
                    else
                    {
                        Player.Message(player, $"{lang.GetMessage("NoDataMsg", this, player.UserIDString)}", Prefix, SteamIDIcon);
                        return;
                    }
                    break;
                }
                case "allch" :
                {
                    if (debug) Puts($"cms_allch");
                    foreach (var paire in storedData.SpawnSpots)
                    {
                        SpawnOneHelicopterThere(paire.Value, "ch47");
                        Player.Message(player, String.Format(lang.GetMessage("SpawnedMsg", this, player.UserIDString), "CH47", paire.Key), Prefix, SteamIDIcon);
                    }
                    break;
                }
                case "ch" :
                {
                    if (debug) Puts($"cms_ch");
                    if (storedData.SpawnSpots == null)
                    {
                        Player.Message(player, $"{lang.GetMessage("NoDataMsg", this, player.UserIDString)}", Prefix, SteamIDIcon);
                        return;
                    }
                    if (args.Length == 1 )
                    {
                        Player.Message(player, $"{lang.GetMessage("SetSpotNameMsg", this, player.UserIDString)}", Prefix, SteamIDIcon);
                        return;
                    }
                    string spotname = string.Empty;
                    int array = 1;
                    int Round = 1;
                    for (Round = 1; array <= args.Length - 1; Round++)
                    {
                        if (array == 1) spotname = args[array];
                        else spotname = spotname + " " + args[array];
                        array = array + 1;
                    }
                    Vector3 there = new Vector3();
                    storedData.SpawnSpots.TryGetValue(spotname, out there);
                    if (there != null)
                    {
                        SpawnOneHelicopterThere(there, "ch47");
                        Player.Message(player, String.Format(lang.GetMessage("SpawnedMsg", this, player.UserIDString), "CH47", spotname), Prefix, SteamIDIcon);
                    }
                    break;
                }
                case "allmini" :
                {
                    if (debug) Puts($"cms_allmini");
                    foreach (var paire in storedData.SpawnSpots)
                    {
                        SpawnOneHelicopterThere(paire.Value, "mini");
                        Player.Message(player, String.Format(lang.GetMessage("SpawnedMsg", this, player.UserIDString), "Mini", paire.Key), Prefix, SteamIDIcon);
                    }
                    break;
                }
                case "mini" :
                {
                    if (debug) Puts($"cms_mini");
                    if (storedData.SpawnSpots == null)
                    {
                        Player.Message(player, $"{lang.GetMessage("NoDataMsg", this, player.UserIDString)}", Prefix, SteamIDIcon);
                        return;
                    }
                    if (args.Length == 1 )
                    {
                        Player.Message(player, $"{lang.GetMessage("SetSpotNameMsg", this, player.UserIDString)}", Prefix, SteamIDIcon);
                        return;
                    }
                    string spotname = string.Empty;
                    int array = 1;
                    int Round = 1;
                    for (Round = 1; array <= args.Length - 1; Round++)
                    {
                        if (array == 1) spotname = args[array];
                        else spotname = spotname + " " + args[array];
                        array = array + 1;
                    }
                    Vector3 there = new Vector3();
                    storedData.SpawnSpots.TryGetValue(spotname, out there);
                    if (there != null)
                    {
                        SpawnOneHelicopterThere(there, "mini");
                        Player.Message(player, String.Format(lang.GetMessage("SpawnedMsg", this, player.UserIDString), "Mini", spotname), Prefix, SteamIDIcon);
                    }
                    break;
                }
                case "del" :
                {
                    if (debug) Puts($"cms_del");
                    if (args.Length == 1 )
                    {
                        Player.Message(player, $"{lang.GetMessage("SetSpotNameDeleteMsg", this, player.UserIDString)}", Prefix, SteamIDIcon);
                        return;
                    }
                    bool found = false;
                    string spotname = string.Empty;
                    int array = 1;
                    int Round = 1;
                    for (Round = 1; array <= args.Length - 1; Round++)
                    {
                        if (array == 1) spotname = args[array];
                        else spotname = spotname + " " + args[array];
                        array = array + 1;
                    }
                    if (storedData.SpawnSpots.ContainsKey(spotname))
                    {
                            found = true;
                            Player.Message(player, $"{lang.GetMessage("RemovedOneSpotMsg", this, player.UserIDString)} - {spotname}", Prefix, SteamIDIcon);
                    }
                    if (found == false) Player.Message(player, $"{lang.GetMessage("NotFoundSpotMsg", this, player.UserIDString)}", Prefix, SteamIDIcon);
                    else storedData.SpawnSpots.Remove(spotname);
                    break;
                }
                case "wipe" :
                {
                    if (debug) Puts($"cms_wipe");
                    int count = storedData.SpawnSpots.Count();
                    storedData.SpawnSpots.Clear();
                    Player.Message(player, String.Format(lang.GetMessage("WipeMsg", this, player.UserIDString), count), Prefix, SteamIDIcon);

                    break;
                }
            }
        }

// code duplica on bool.perm
// onload check uint, remove non existing
// cms_chalive = list ch47 uint in world
// cms_minialive

// chat command spawn_random X

// chat command wipe

// config remove default rust CH47 on map (ownerid =0 ?)

// true/false ch47 mmapmarker

// timer auto kill/spawn = respawn timer
//console commands all in one
// kill heliz alive
///////////////////////////////////
// BOOL PERM ADMIN
/////////////////////////
        bool DoesThisPlayerHasPermission(BasePlayer player)
        {
            bool admin = permission.UserHasPermission(player.UserIDString, SpawnerAdmin);
            if (!admin)
            {
                Player.Message(player, $"{lang.GetMessage("NoPermMsg", this, player.UserIDString)}", Prefix, SteamIDIcon);
                return false;
            }
            else return true;
        }

/////////////////////////////
// SPAWN ON VECTOR3
///////////////////

        void SpawnOneHelicopterThere(Vector3 spot, string what)
        {
            if (spot == null) return;
            string prefab = chprefab;
            if (what == "mini") prefab = miniprefab;
            BaseVehicle vehicle = (BaseVehicle)GameManager.server.CreateEntity(prefab, spot, new Quaternion());
            if (vehicle == null) return;
            BaseEntity entity = vehicle as BaseEntity;
            entity.OwnerID = 998877665544;//for FUTURE
            vehicle.Spawn();
            NetworkableId copteruint = entity.net.ID;
            storedData.SpawnerCH47.Add(copteruint.Value);
            if (debug) Puts($"SPAWNED MINICOPTER {copteruint.ToString()} - OWNER {entity.OwnerID}");
        }

//////////////////////////
// KILL ONE ID CH47 - FUTURE
///////////////
        /*void KillThisSpawnerCH47HelicopterPlease(uint deluint)
        {
            if (debug) Puts($"KillThisSpawnerCH47HelicopterPlease");
            var tokill = BaseNetworkable.serverEntities.Find(deluint);
            if (tokill != null) tokill.Kill();
            storedData.SpawnerCH47.Remove(deluint);
        }*/
////////////////////////////////
// ENTITY KILL
//////////////////

        void OnEntityKill(BaseNetworkable entity)
        {
            if (!loaded) return;
            if (entity == null) return;
            if (entity.net.ID == default) return;
            if (storedData.SpawnerCH47 == null && storedData.SpawnerMinicopters == null) return;
            if (storedData.SpawnerCH47.Contains(entity.net.ID.Value) == false && storedData.SpawnerMinicopters.Contains(entity.net.ID.Value) == false)
            {
                if (debug) Puts($"KILLED HELICOPTER not from Spawner plugin");
                return;
            }
            NetworkableId todelete = default;
            CH47Helicopter chcheck = entity as CH47Helicopter;
            if (chcheck != null)
            {
                if (storedData.SpawnerCH47.Contains(entity.net.ID.Value)) todelete = entity.net.ID;
                if (todelete != null)
                {
                    storedData.SpawnerCH47.Remove(todelete.Value);
                    if (debug) Puts($"KILLED CH47 - removed from list");
                }
                return;
            }
            Minicopter minicheck = entity as Minicopter;
            if (minicheck != null)
            {
                if (storedData.SpawnerMinicopters.Contains(entity.net.ID.Value)) todelete = entity.net.ID;
                if (todelete != null)
                {
                    storedData.SpawnerMinicopters.Remove(todelete.Value);
                    if (debug) Puts($"KILLED MINI - removed from list");
                }
            }
        }
    }
}
