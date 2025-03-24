
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    /*
     * TODO List:
     * ----------
     * - Players can only see the markers of the TCs they're authorized on
     * 
     * BUGS:
     * ------
     * 
     * */

    [Info("TCMap", "TheBandolero", "1.0.1", ResourceId = 0)]
    [Description("Shows all tool cupboards in the game map, and hovering over the TC mark shows a tooltip with the name of the players authorized in it.")]
    public class TCMap : RustPlugin
    {
        private const string Prefab = "cupboard.tool.deployed";
        private List<BaseEntity> listOfTCs = new List<BaseEntity>();
        private List<AuthPlayer> listOfAuthPlayers = new List<AuthPlayer>();
        private List<int> listMarkersIDs = new List<int>();
        private bool showToAllPlayers = false;

        void Init()
        {
            permission.RegisterPermission("tcmap.admin", this);
        }

        void OnServerInitialized()
        {
            DoTheMagic();
        }

        object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            timer.Once(0.5f, () =>
            {
                KillAllMarkers();
                DoTheMagic();
            });
            return null;
        }

        object OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            timer.Once(0.5f, () =>
            {
                KillAllMarkers();
                DoTheMagic();
            });
            return null;
        }

        object OnCupboardClearList(BuildingPrivlidge privilege, BasePlayer player)
        {
            timer.Once(0.5f, () =>
            {
                KillAllMarkers();
                DoTheMagic();
            });
            return null;
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity != null && entity.ShortPrefabName == Prefab)
            {
                KillAllMarkers();
                DoTheMagic();
            }
        }

        void Unload()
        {
            KillAllMarkers();
        }

        private void DoTheMagic ()
        {
            int count = 0;
            //listMarkersIDs = new List<int>();
            //listOfTCs = new List<BaseEntity>();
            listOfTCs = GetTCList();

            foreach (BaseEntity tc in listOfTCs)
            {
                if (tc != null)
                {
                    count++;
                    bool isFirst = true;
                    string authPlayersNamesForMarker = "";
                    //PrintWarning("TC nº" + count + " with ID: " + tc.GetInstanceID().ToString() + " and Position: " + tc.ServerPosition);

                    listOfAuthPlayers = GetAuthList(tc, count);
                    if (listOfAuthPlayers != null && listOfAuthPlayers.Any())
                    {
                        foreach (AuthPlayer authPlayer in listOfAuthPlayers)
                        {
                            if (isFirst)
                            {
                                isFirst = false;
                                authPlayersNamesForMarker += authPlayer.GetAuthPlayerName();
                            }
                            else
                            {
                                authPlayersNamesForMarker += "\n" + authPlayer.GetAuthPlayerName();
                            }

                            /*PrintWarning("PlayerName: " + authPlayer.GetAuthPlayerName() + " --- PlayerID: " + authPlayer.GetAuthPlayerID() + " --- Owner: " + authPlayer.GetPlayerIsOwner()
                                + " --- CurrentlyOnline: " + authPlayer.GetAuthPlayerOnline());*/
                        }
                        PutTCMarksOnMap(tc.ServerPosition, authPlayersNamesForMarker);
                        listOfAuthPlayers.Clear();
                    }                    
                }
            }
        }

        private List<BaseEntity> GetTCList()
        {
            List<BaseEntity> tcList = new List<BaseEntity>();

            foreach (var tcb in GameObject.FindObjectsOfType<BaseEntity>())
            {
                if (tcb != null && IsCupboardEntity(tcb))
                {
                    tcList.Add(tcb);
                }
            }
            return tcList;
        }

        private bool IsCupboardEntity(BaseEntity entity) => entity != null && entity.ShortPrefabName == Prefab;

        private List<AuthPlayer> GetAuthList(BaseEntity tc, int count)
        {
            if (tc != null)
            {
                var tcPrivilege = tc.gameObject.GetComponentInParent<BuildingPrivlidge>();
                if (tcPrivilege != null)
                {
                    if (tcPrivilege.authorizedPlayers == null || tcPrivilege.authorizedPlayers.Count == 0)
                    {
                        PrintWarning("No players authorized in this TC.");
                        List<AuthPlayer> authList = new List<AuthPlayer>();
                        return authList;
                    }
                    else
                    {
                        List<AuthPlayer> authList = new List<AuthPlayer>();
                        foreach (ProtoBuf.PlayerNameID playerNameID in tcPrivilege.authorizedPlayers)
                        {
                            if (playerNameID != null)
                            {
                                bool isOwner = false;

                                if (playerNameID.userid == tc.OwnerID)
                                {
                                    isOwner = true;
                                }
                                authList.Add(new AuthPlayer(playerNameID.userid, playerNameID.username, isOwner, IsPlayerOnline(playerNameID.userid)));
                            }                                
                        }

                        return authList;
                    }
                }
                return null;                
            }
            return null;            
        }

        private bool IsPlayerOnline(ulong tcPlayerID)
        {
            if (BasePlayer.FindByID(tcPlayerID) != null)
            {
                return true;
            }
            return false;
        }

        private class AuthPlayer
        {
            private ulong playerID;
            private string playerName;
            private bool isOwner;
            private bool playerOnline;

            public AuthPlayer(ulong pID, string pName, bool pIsOwner, bool pOnline)
            {
                playerID = pID;
                playerName = pName;
                isOwner = pIsOwner;
                playerOnline = pOnline;
            }

            public ulong GetAuthPlayerID()
            {
                return playerID;
            }

            public void SetAuthPlayerID(ulong pID)
            {
                playerID = pID;
            }

            public string GetAuthPlayerName()
            {
                return playerName;
            }

            public void SetAuthPlayerID(string pName)
            {
                playerName = pName;
            }

            public bool GetPlayerIsOwner()
            {
                return isOwner;
            }

            public void SetPlayerIsOwner(bool pIsOwner)
            {
                isOwner = pIsOwner;
            }

            public bool GetAuthPlayerOnline()
            {
                return playerOnline;
            }

            public void SetAuthPlayerOnline(bool pOnline)
            {
                playerOnline = pOnline;
            }
        }

        // MAP MARKERS
        private void PutTCMarksOnMap(Vector3 tcPos, string markerAuthNames)
        {
            MapMarkerGenericRadius m = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", tcPos) as MapMarkerGenericRadius;
            m.alpha = 1.0f;
            m.color1 = Color.black;
            m.color2 = Color.black;
            m.radius = 2.5f;

            MapMarkerGenericRadius m2 = GameManager.server.CreateEntity("assets/prefabs/tools/map/genericradiusmarker.prefab", tcPos) as MapMarkerGenericRadius;
            m2.alpha = 1.0f;
            m2.color1 = Color.cyan;
            m2.color2 = Color.black;
            m2.radius = 2.0f;

            VendingMachineMapMarker m3 = GameManager.server.CreateEntity("assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", tcPos) as VendingMachineMapMarker;
            m3.markerShopName = markerAuthNames;

            m3.Spawn();
            m.Spawn();
            m.SendUpdate();
            m2.Spawn();
            m2.SendUpdate();

            listMarkersIDs.Add(m.GetInstanceID());
            listMarkersIDs.Add(m2.GetInstanceID());
            listMarkersIDs.Add(m3.GetInstanceID());
        }

        object CanNetworkTo(BaseNetworkable entity, BasePlayer player)
        {
            if (entity == null || player == null) return null;
            //if ((entity.ShortPrefabName == "genericradiusmarker" || entity.ShortPrefabName == "vending_mapmarker") && !player.IsAdmin) return false;
            if (!showToAllPlayers)
            {
                if ((entity.ShortPrefabName == "genericradiusmarker" || entity.ShortPrefabName == "vending_mapmarker") && !permission.UserHasPermission(player.userID.ToString(), "tcmap.admin")) return false;
            }            
            return null;
        }

        private void KillAllMarkers()
        {
            foreach (var mark in GameObject.FindObjectsOfType<MapMarkerGenericRadius>())
            {
                if (listMarkersIDs.Contains(mark.GetInstanceID())){
                    mark.Kill();
                    mark.SendUpdate();
                }
            }
            foreach (var mark in GameObject.FindObjectsOfType<VendingMachineMapMarker>())
            {
                if (listMarkersIDs.Contains(mark.GetInstanceID()))
                {
                    mark.Kill();
                }
            }

            listMarkersIDs.Clear();
            listOfAuthPlayers.Clear();
            listOfTCs.Clear();
        }

        // CHAT COMMANDS
        [ChatCommand("tcmap"), Permission("tcmap.admin")]
        private void TCMapChatCommands(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.userID.ToString(), "tcmap.admin"))
            {
                if (args == null || args.Length == 0)
                {

                }
                else if (args[0].Equals("clear"))
                {
                    KillAllMarkers();
                    player.ChatMessage("<color=#ffa500ff>TCMap markers <b>REMOVED!</b></color>");
                }
                else if (args[0].Equals("update"))
                {
                    KillAllMarkers();
                    DoTheMagic();
                    player.ChatMessage("<color=#ffa500ff>TCMap markers <b>UPDATED!</b></color>");
                }
                else if (args[0].Equals("showtoall"))
                {
                    if (showToAllPlayers)
                    {
                        showToAllPlayers = false;
                        KillAllMarkers();
                        DoTheMagic();
                        player.ChatMessage("<color=#ffa500ff>TCMap show everyone: </color><b>" + showToAllPlayers + "</b>");
                    }
                    else
                    {
                        showToAllPlayers = true;
                        KillAllMarkers();
                        DoTheMagic();
                        player.ChatMessage("<color=#ffa500ff>TCMap show everyone: </color><b>" + showToAllPlayers + "</b>");
                    }
                }
                else if (args[0].Equals("help"))
                {
                    player.ChatMessage("<color=#ffa500ff><b><size=22>TCMapHelp</size></b></color>"
                        + "\n\n<color=#add8e6ff><b>/tcmap clear</b></color>  Removes all TC marks from map."
                        + "\n<color=#add8e6ff><b>/tcmap update</b></color>  Updates all TC marks on the map."
                        + "\n<color=#add8e6ff><b>/tcmap showtoall</b></color>  Shows all TC marks to ALL players."
                        + "\n<color=#add8e6ff><b>/tcmap help</b></color>  Shows this help menu."
                        + "\n\n<color=#ffa500ff><b><size=20>•</size></b></color> <color=#add8e6ff><b>Show to all is set to: </b></color> " + showToAllPlayers);
                }
            }
        }

    }    
}