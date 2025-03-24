using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Linq;
using System.Collections.Generic; // list function
using Oxide.Core.Libraries.Covalence;
using System;   //toshorttimestring
using System.Globalization;
using Oxide.Core;
using Rust; 
using System.Text;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Survey My Spot", "BuzZ[PHOQUE]", "1.0.0")]
    [Description("Set a spot to survey, it will log enter/exit of players & npc")]

/*======================================================================================================================= 
*
*   1.0.0   20190908    +code refresh
*
*    THANKS TO THE OXIDE TEAM for coding quality and time spent for community
*
*=======================================================================================================================*/

    public class SurveyMySpot : RustPlugin
    {

        [PluginReference] Plugin ZoneManager;

        bool debug = false;
        string version = "version 1.0.0";

        public Dictionary<ulong, int> reading = new Dictionary<ulong, int>();    


    class StoredData
    {

        public Dictionary<ulong, Dataz> ulongdata = new Dictionary<ulong, Dataz>();    
        
        public StoredData()
        {
        }
    }

        private StoredData storedData;

    class Dataz
    {

        public string playerspot;    
        public bool playeronly;    
        public bool playerchat; 
        public int playerenter; 
        public int playerexit; 
        public int npcenter; 
        public int npcexit; 
        public List<string> loglist = new List<string>();
        
        public Dataz()
        {
        }
    }

        private Dataz dataz;


        bool IsPlayerOnSpot(string spotID, BasePlayer player) => (bool)ZoneManager?.Call("isPlayerInZone", spotID, player);
        string CheckZoneID(string checkID) => (string)ZoneManager?.Call("CheckZoneID", checkID);

        static string SurveyPanel;
        static string LogPanel;
        private bool ConfigChanged;


        string Prefix = "[SMS] :";           // CHAT PLUGIN PREFIX
        string PrefixColor = "#d47600";         // CHAT PLUGIN PREFIX COLOR
        string ChatColor = "#ffba64";           // CHAT MESSAGE COLOR

        ulong SteamIDIcon = 76561198383370822;          // SteamID FOR PLUGIN ICON
        bool PlayerOnly;                        // LOG NPC & PLAYER, OR PLAYER ONLY
        //int logpage;
        //string logpagestring;
        const string SurveyPermission = "surveymyspot.use"; 

#region INITIALISATION

		private void OnServerInitialized()
        {
            LoadVariables();
            permission.RegisterPermission(SurveyPermission, this);

        }

        void Loaded()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("SurveyMySpot");
            if (ZoneManager == false)
                    {
                        PrintError("ZoneManager.cs is needed and not present.");
                    }
        }

        void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject("SurveyMySpot", storedData);
        }
#endregion

#region MESSAGES / LANG

        void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {

                {"EnterPlayerMsg", "-> ENTER PLAYER :"}, 
                {"EnterNPCMsg", "-> ENTER NPC :"}, 
                {"ExitPlayerMsg", "-> EXIT PLAYER :"}, 
                {"ExitNPCMsg", "-> EXIT NPC :"}, 
                {"ZMNeededMsg", "Error : ZoneManager plugin is needed to start SurveyMySpot."}, 
                {"SpotSetMsg", "Setting your spot at your location."}, 
                {"SpotVerifMsg", "Your spot has been set and verified."}, 
                {"SpotErrorMsg", "An error occured. Please re try."}, 
                {"SpotDstryMsg", "Spot has been removed, and log cleared."}, 
                {"NoPermMsg", "You don't have the permission for this."}, 
                {"PanelSurveyMsg", "MY SURVEY SPOT PANEL"}, 
                {"PanelSurveyUnderMsg", "Set & Survey your spot -"}, 
                {"ZoneAlreadyMsg", "ERROR : Zone already exists."}, 

            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {

                {"EnterPlayerMsg", "-> ENTREE JOUEUR :"},
                {"EnterNPCMsg", "-> ENTREE NPC :"}, 
                {"ExitPlayerMsg", "-> SORTIE PLAYER :"}, 
                {"ExitNPCMsg", "-> SORTIE NPC :"}, 
                {"ZMNeededMsg", "Erreur : le plugin ZoneManager doit être installé pour utiliser SurveyMySpot."},
                {"SpotSetMsg", "Mise en place de votre spot sur votre position."}, 
                {"SpotVerifMsg", "Votre spot est prêt et vérifié."}, 
                {"SpotErrorMsg", "Une erreur est survenue, veuillez re essayer."}, 
                {"SpotDstryMsg", "Votre spot a été supprimé, et votre log effacé."}, 
                {"NoPermMsg", "Vous n'y êtes pas autorisé."}, 
                {"PanelSurveyMsg", "MON SPOT SOUS SURVEILLANCE"},
                {"PanelSurveyUnderMsg", "Créez & surveillez votre spot -"},
                {"ZoneAlreadyMsg", "ERREUR : la Zone existe déjà."}, 

            }, this, "fr");
        }

#endregion

#region CONFIG

    protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        private void LoadVariables()
        {

            Prefix = Convert.ToString(GetConfig("Chat Settings", "Prefix", "[SMS] :"));                       // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Chat Settings", "PrefixColor", "#d47600"));                // CHAT PLUGIN PREFIX COLOR
            ChatColor = Convert.ToString(GetConfig("Chat Settings", "ChatColor", "#ffba64"));                    // CHAT MESSAGE COLOR
            //SteamIDIcon = Convert.ToUlong(GetConfig("Settings", "SteamIDIcon", 76561198383370822));        // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / 76561198842176097 /

            if (!ConfigChanged) return;
            SaveConfig();
            ConfigChanged = false;
        }

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

#region ON ENTER SPOT

        void OnEnterZone(string ZoneID, BasePlayer player)//            if (player.userID.IsSteamId() == false)
        {
            string gender;
            if (player.userID.IsSteamId() == false) gender = "npc";
            else gender = "human";
            SomethingHappenedOnZone (ZoneID, player, "enter", gender);
        }

#region ON EXIT SPOT
        void OnExitZone(string ZoneID, BasePlayer player)
        {
             string gender;
            if (player.userID.IsSteamId() == false) gender = "npc";
            else gender = "human";
            SomethingHappenedOnZone (ZoneID, player, "exit", gender);
        }

#endregion

        void SomethingHappenedOnZone (string ZoneID, BasePlayer player, string reason, string gender)
        {

            //if (player.userID.IsSteamId() == false) return;           
//          string TimeIs = DateTime.Now.ToString("dd,hh,mm");
            string TimeIs = DateTime.Now.ToString();
string logstring = string.Empty;
string msg = string.Empty;
            if (reason =="enter")
            {
                logstring = $"{TimeIs} : {ZoneID} {lang.GetMessage("EnterPlayerMsg", this, player.UserIDString)} {player.displayName} {player.userID}";
                msg = $"<color={ChatColor}>{ZoneID} {lang.GetMessage("EnterPlayerMsg", this, player.UserIDString)} {player.displayName}</color>";
            }
            if (reason == "exit")
            {
                logstring = $"{TimeIs} : {ZoneID} {lang.GetMessage("ExitPlayerMsg", this, player.UserIDString)} {player.displayName} {player.userID}";
                msg = $"<color={ChatColor}>{ZoneID} {lang.GetMessage("ExitPlayerMsg", this, player.UserIDString)} {player.displayName}</color>";
            }
                foreach (var spotsofdaplugin in storedData.ulongdata)
                {
                    if (spotsofdaplugin.Value.playeronly && gender == "npc") return;
                    if (spotsofdaplugin.Value.playerspot == ZoneID)
                    {
                        if (debug == true) Puts($"-> LOGGED IN {ZoneID} : PLAYER {player.displayName}");

                        if (spotsofdaplugin.Value.playerchat)    
                        {
                            foreach(BasePlayer player2 in BasePlayer.activePlayerList)
                            {
                                if (player.userID == player2.userID)
                                {
                                    Player.Message(player2, msg,$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                                }
                            }
                        }
                        int count = spotsofdaplugin.Value.playerenter;
                        count = count + 1;
                        spotsofdaplugin.Value.playerenter = count;
                        spotsofdaplugin.Value.loglist.Add(logstring);
                        if (gender == "human" && reason == "enter") spotsofdaplugin.Value.playerenter = spotsofdaplugin.Value.playerenter + 1;
                        if (gender == "npc" && reason == "enter") spotsofdaplugin.Value.npcenter = spotsofdaplugin.Value.npcenter + 1;
                        if (gender == "human" && reason == "exit") spotsofdaplugin.Value.playerexit = spotsofdaplugin.Value.playerexit + 1;
                        if (gender == "npc" && reason == "exit") spotsofdaplugin.Value.npcexit = spotsofdaplugin.Value.npcexit + 1;
                        KeepHundred(ZoneID, spotsofdaplugin.Key);
                        return;
                    }
                }
        }

#endregion



#region PLAYERONLY PREF

        [ConsoleCommand("MySurveySpotPlayerOnly")]
        private void MySurveySpotOnly(ConsoleSystem.Arg arg)       
        {
                var player = arg.Connection.player as BasePlayer;
                    storedData.ulongdata[player.userID].playeronly = !storedData.ulongdata[player.userID].playeronly;
                    if (debug) Puts($"-> CHANGED NPC/HUMAN log preference");
                    CuiHelper.DestroyUi(player, SurveyPanel);             
                    SurveyCui(player, null, null);   
                    return;           

        }
#endregion

#region PLAYER CHAT PREF

        [ConsoleCommand("MySurveySpotChat")]
        private void MySurveySpotChat(ConsoleSystem.Arg arg)       
        {

                var player = arg.Connection.player as BasePlayer;
                    storedData.ulongdata[player.userID].playerchat = !storedData.ulongdata[player.userID].playerchat;
                    if (debug) Puts($"-> CHANGED NPC/HUMAN log preference");
                    CuiHelper.DestroyUi(player, SurveyPanel);             
                    SurveyCui(player, null, null);   
                    return;     

        }
#endregion

#region BACK TO PANEL

        [ConsoleCommand("MySurveySpotBackToPanel")]
        private void MySurveySpotBackTo(ConsoleSystem.Arg arg)       
        {
                var player = arg.Connection.player as BasePlayer;
                CuiHelper.DestroyUi(player, LogPanel);             
                SurveyCui(player, null, null);            
        }
#endregion

#region PURGE PLAYER LOG

        [ConsoleCommand("MySurveySpotPurgeMyLog")]
        private void MySurveySpotPurge(ConsoleSystem.Arg arg)       
        {
                var player = arg.Connection.player as BasePlayer;
            ulong playerID = player.userID;
            RemoveLog (storedData.ulongdata[player.userID].playerspot, player.userID);
                CuiHelper.DestroyUi(player, SurveyPanel);             
                SurveyCui(player, null, null);          
        }

        private void RemoveLog(string spot, ulong playerID)       
        {
                storedData.ulongdata[playerID].playerenter = 0;
                storedData.ulongdata[playerID].playerexit = 0;
                storedData.ulongdata[playerID].npcenter = 0;
                storedData.ulongdata[playerID].npcexit = 0;
                storedData.ulongdata[playerID].loglist.Clear();

                    if (debug == true) 
                        Puts($"-> PURGE LOG FROM SPOT {spot}");
        }
#endregion

#region LOG 100 LIMIT

        private void KeepHundred(string zoneID, ulong spotowner)       
        {
            List<string> thisloglist = storedData.ulongdata[spotowner].loglist;
            List<string> hundredloglist = new List<string>();

            int count = thisloglist.Count;
            if (count > 100)
            {
                int todel = count - 100;
                string[] logarray = thisloglist.ToArray(); 
                int Round = 1;
                int round = -1;
                for (Round = 1; Round <= todel ; Round++)            
                {
                    round = round + 1;
                    hundredloglist.Add(logarray[round]);
                }
                foreach (string value in hundredloglist)
                {
                    storedData.ulongdata[spotowner].loglist.Remove(value);
                }                    
            }
        }

#endregion

#region SPOTZONE

        [ConsoleCommand("MySurveySpotZone")]
        private void MySurveySpot(ConsoleSystem.Arg arg)       
        {

            var player = arg.Connection.player as BasePlayer;
            ulong playerID = player.userID;
            string playerIDstring = playerID.ToString();
            string playername = player.displayName;
            string shortplayerID = playerIDstring.Substring(10, 6);
            string shortplayername = playername;
            string spot;
            bool verify;
            var position = player.transform.position;
            if (shortplayername.Length > 8) shortplayername = shortplayername.Substring(0,8);
            if (ZoneManager == false)
                {
                    Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("ZMNeededMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                    return;
                }
            string[] Flags;
            Flags = new string[] { "name", $"[SPOT]{shortplayername}", "enter_message" ,null, "radius", "5" };             

            if (string.IsNullOrEmpty(storedData.ulongdata[player.userID].playerspot) == true)
            {
                spot = $"999666{shortplayerID}";
                string checkingfirst = CheckZoneID(spot);  
                if (checkingfirst == spot)
                {
                    Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("ZoneAlreadyMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                    return;
                }
                Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("SpotSetMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                ZoneManager?.Call("CreateOrUpdateZone", $"{spot}", Flags, position);
                string checking = CheckZoneID(spot);  
                if (checking == spot) Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("SpotVerifMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                if (checking == null)
                {
                    Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("SpotErrorMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                    return;                    
                }
                storedData.ulongdata[player.userID].playerspot = spot;
                if (debug == true) Puts($"-> NEW SPOT {spot} from {shortplayername}");
                CuiHelper.DestroyUi(player, SurveyPanel);
                SurveyCui(player, null, null);
                return;
            }

            else
            {
                Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("SpotDstryMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                spot = storedData.ulongdata[player.userID].playerspot;
                RemoveLog(spot, playerID);
                storedData.ulongdata[player.userID].playerspot = string.Empty;
                spot = $"999666{shortplayerID}";                
                ZoneManager?.Call("EraseZone", $"{spot}");
                if (debug == true) Puts($"-> REMOVED SPOT {spot} from {shortplayername}");
                CuiHelper.DestroyUi(player, SurveyPanel);
                SurveyCui(player, null, null);                
            }
       }



#endregion

#region PANEL MY SURVEY

        [ChatCommand("mysurvey")]
        private void SurveyCui(BasePlayer player, string command, string[] args)
        {
            ulong playerID = player.userID;
            string playername = player.displayName;
            string shortplayername = playername.Substring(0,8);

            var debutcolonnegauche = 0.05;
            var fincolonnegauche = 0.35;
            var debutcolonnemilieu = 0.37;
            var fincolonnemilieu = 0.58;
            var debutcolonnedroite = 0.60;
            var fincolonnedroite = 0.85;
            var debutcolonnemenu = 0.87;
            var fincolonnemenu = 0.98;

            string PanelColor = "1.0 0.8 0.5 0.5";
            string buttonColor = "1.0 0.8 0.5 0.8";
            string buttonCloseColor = "0.6 0.26 0.2 1";
            string statuscolor = "1.0 0.1 0.1 1";
            string idcolor = "1.0 0.1 0.1 1";
            string namecolor = "1.0 0.1 0.1 1";

            string Green = "0.5 1.0 0.0 1";
            //string Red = "1.0 0.1 0.1 1";

            string StatusSpot = "OFF";
            string buttononly = "NO";
            string buttonchat = "NO";

            string spot = "NONE";
            string spotname = "NONE";
            bool ChatMsg;
            //logpage = 1;
            //logpagestring = "from 001 to 025";
            reading.Remove(player.userID);
            reading.Add(player.userID, 1);

            int playerenter = 0;
            int playerexit = 0;      
            int npcenter = 0;
            int npcexit = 0;
            List<string> thisloglist = new List<string>();
            int logcount = 0;

            if (!storedData.ulongdata.ContainsKey(player.userID))
            {
                Dataz newdata = new Dataz {};
                storedData.ulongdata.Add(player.userID, newdata);
            }

                playerenter = storedData.ulongdata[player.userID].playerenter;
                playerexit = storedData.ulongdata[player.userID].playerexit;            
                npcenter = storedData.ulongdata[player.userID].npcenter;
                npcexit = storedData.ulongdata[player.userID].npcexit;

                if (storedData.ulongdata[player.userID].playeronly == true)
                {
                    buttononly = "YES";
                }

            if (storedData.ulongdata[player.userID].playerchat == true) buttonchat = "YES";
            thisloglist = storedData.ulongdata[player.userID].loglist;
            if (string.IsNullOrEmpty(storedData.ulongdata[player.userID].playerspot) == false)
            {  
                StatusSpot = "ON";
                spot = storedData.ulongdata[player.userID].playerspot;               
                statuscolor = Green;   
                idcolor = Green;
                namecolor = Green;
                spotname = $"[SPOT]{shortplayername}";
            }

            bool hasperm = permission.UserHasPermission(player.UserIDString, SurveyPermission);
            if (hasperm == false)
            {
                Player.Message(player, $"<color={ChatColor}>{lang.GetMessage("NoPermMsg", this, player.UserIDString)}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }
            if (thisloglist.Count > 0) logcount = thisloglist.Count;
            var CuiElement = new CuiElementContainer();
            SurveyPanel = CuiElement.Add(new CuiPanel { Image = { Color = $"{PanelColor}"}, RectTransform = { AnchorMin = "0.25 0.25", AnchorMax = "0.75 0.8"}, CursorEnabled = true});
            var closeButton = new CuiButton { Button = { Close = SurveyPanel, Color = $"{buttonCloseColor}" },
                RectTransform = { AnchorMin = "0.85 0.85", AnchorMax = "0.95 0.95" },
                Text = { Text = "[X]\nClose", FontSize = 16, Align = TextAnchor.MiddleCenter }};
            CuiElement.Add(closeButton, SurveyPanel);
            var ButtonInfo = CuiElement.Add(new CuiButton
            {
                Button = { Command = "MySurveySpotLog", Color = $"0.0 0.5 1.0 0.5" },
                RectTransform = { AnchorMin = "0.73 0.85", AnchorMax = "0.83 0.95" },
                Text = { Text = $"LOG", Color = "0.8 1.0 1.0 1", FontSize = 14, Align = TextAnchor.MiddleCenter}
            }, SurveyPanel);
            var TextLineIntro1 = CuiElement.Add(new CuiLabel
            {
                Text = { Text = $"{lang.GetMessage("PanelSurveyMsg", this, player.UserIDString)}", FontSize = 30, Align = TextAnchor.MiddleCenter },
                RectTransform = { AnchorMin = $"{debutcolonnegauche} 0.85", AnchorMax = $"0.71 0.95" }
            }, SurveyPanel);

            var TextLineIntro2 = CuiElement.Add(new CuiLabel
            {
                Text = { Text = $"<i>{lang.GetMessage("PanelSurveyUnderMsg", this, player.UserIDString)} {version}</i>", FontSize = 14, Align = TextAnchor.MiddleCenter},
                RectTransform = { AnchorMin = $"{debutcolonnegauche} 0.78", AnchorMax = $"0.71 0.84" }
            }, SurveyPanel);

            var TextLine1 = CuiElement.Add(new CuiLabel
            {
                Text = { Text = "YOUR SPOT IS :", Color = "0.0 0.0 0.0 1.0", FontSize = 18, Align = TextAnchor.MiddleRight},
                RectTransform = { AnchorMin = $"{debutcolonnegauche} 0.65", AnchorMax = $"{fincolonnegauche} 0.70"}
            }, SurveyPanel);

            var TextLine1b = CuiElement.Add(new CuiLabel
            {
                Text = { Text = "<i>Click to toggle ON/OFF your spot.</i>", Color = "0.0 0.0 0.0 1.0", FontSize = 12, Align = TextAnchor.MiddleLeft},
                RectTransform = { AnchorMin = $"{debutcolonnedroite} 0.65", AnchorMax = $"{fincolonnemenu} 0.70"}
            }, SurveyPanel);

            var ButtonLine1 = CuiElement.Add(new CuiButton
            {
                Button = { Command = "MySurveySpotZone", Color = $"{statuscolor}" },
                RectTransform = { AnchorMin = $"{debutcolonnemilieu} 0.65", AnchorMax = $"{fincolonnemilieu} 0.70" },
                Text = { Text = $"{StatusSpot}", Color = "0.0 0.0 0.0 1.0", FontSize = 18, Align = TextAnchor.MiddleCenter}
            }, SurveyPanel);

            var TextLine2 = CuiElement.Add(new CuiLabel
            {
                Text = { Text = "ZONE ID : ", Color = "0.0 0.0 0.0 1.0", FontSize = 18, Align = TextAnchor.MiddleRight },
                RectTransform = { AnchorMin = $"{debutcolonnegauche} 0.55",
                    AnchorMax = $"{fincolonnegauche} 0.60"
                }
            }, SurveyPanel);

            var ButtonLine2 = CuiElement.Add(new CuiButton
            {
                Button =
                {
                    Command = "",
                    Color = $"{idcolor}"
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnemilieu} 0.55",
                    AnchorMax = $"{fincolonnemilieu} 0.60"
                },
                Text =
                {
                    Text = $"{spot}",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter
                }
            }, SurveyPanel);

            var ButtonLine2b = CuiElement.Add(new CuiButton
            {
                Button =
                {
                    Command = "",
                    Color = $"{buttonColor}"
                },
                Text =
                {
                    Text = $"** TOTAL ** PLAYER : {playerenter} ENTER / {playerexit} EXIT **",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnedroite} 0.55",
                    AnchorMax = $"{fincolonnemenu} 0.60"
                }
            }, SurveyPanel);

            var TextLine3 = CuiElement.Add(new CuiLabel
            {
                Text =
                {
                    Text = "ZONE NAME : ",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 18,
                    Align = TextAnchor.MiddleRight
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnegauche} 0.45",
                    AnchorMax = $"{fincolonnegauche} 0.50"
                }
            }, SurveyPanel);

            var ButtonLine3 = CuiElement.Add(new CuiButton
            {
                Button =
                {
                    Command = "",
                    Color = $"{namecolor}"
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnemilieu} 0.45",
                    AnchorMax = $"{fincolonnemilieu} 0.50"
                },
                Text =
                {
                    Text = $"{spotname}",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter
                }
            }, SurveyPanel);

            var ButtonLine3b = CuiElement.Add(new CuiButton
            {
                Button =
                {
                    Command = "",
                    Color = $"{buttonColor}"
                },
                Text =
                {
                    Text = $"** TOTAL ** NPC : {npcenter} ENTER / {npcexit} EXIT **",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 12,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnedroite} 0.45",
                    AnchorMax = $"{fincolonnemenu} 0.50"
                }
            }, SurveyPanel);

            var TextLine4 = CuiElement.Add(new CuiLabel
            {
                Text =
                {
                    Text = "ENTRIES IN YOUR LOG :",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 16,
                    Align = TextAnchor.MiddleRight
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnegauche} 0.35",
                    AnchorMax = $"{fincolonnegauche} 0.40"
                }
            }, SurveyPanel);

            var TextLine4b = CuiElement.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnedroite} 0.35",
                    AnchorMax = $"{fincolonnemenu} 0.40"
                },
                Text =
                {
                    Text = $"<i>LOG will display 100 last records.</i>",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 12, 
                    Align = TextAnchor.MiddleLeft
                }
            }, SurveyPanel);

            var ButtonLine4 = CuiElement.Add(new CuiButton
            {
                Button =
                {
                    Command = "",
                    Color = $"{buttonColor}"
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnemilieu} 0.35",
                    AnchorMax = $"{fincolonnemilieu} 0.40"
                },
                Text =
                {
                    Text = $"{logcount.ToString()} / 100",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 16, 
                    Align = TextAnchor.MiddleCenter
                }
            }, SurveyPanel);



            var TextLine5 = CuiElement.Add(new CuiLabel
            {
                Text =
                {
                    Text = "CLEAN YOUR LOG :",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 16,
                    Align = TextAnchor.MiddleRight
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnegauche} 0.25",
                    AnchorMax = $"{fincolonnegauche} 0.30"
                }
            }, SurveyPanel);

            var TextLine5b = CuiElement.Add(new CuiLabel
            {
                Text =
                {
                    Text = "<i>Click to clean your log</i>",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnedroite} 0.25",
                    AnchorMax = $"{fincolonnemenu} 0.30"
                }
            }, SurveyPanel);

            var ButtonLine5 = CuiElement.Add(new CuiButton
            {
                Button =
                {
                    Command = "MySurveySpotPurgeMyLog",
                    Color = $"{buttonColor}"
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnemilieu} 0.25",
                    AnchorMax = $"{fincolonnemilieu} 0.30"
                },
                Text =
                {
                    Text = "PURGE NOW",
                    Color = "1.0 0.1 0.1 1.0",
                    FontSize = 16, 
                    Align = TextAnchor.MiddleCenter
                }
            }, SurveyPanel);

            var TextLine6 = CuiElement.Add(new CuiLabel
            {
                Text =
                {
                    Text = "NOTIFY IN CHAT :",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 16,
                    Align = TextAnchor.MiddleRight
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnegauche} 0.15",
                    AnchorMax = $"{fincolonnegauche} 0.20"
                }
            }, SurveyPanel);

            var TextLine6b = CuiElement.Add(new CuiLabel
            {
                Text =
                {
                    Text = "<i>Chat message at enter/exit of your spot</i>",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnedroite} 0.15",
                    AnchorMax = $"{fincolonnemenu} 0.20"
                }
            }, SurveyPanel);

            var ButtonLine6 = CuiElement.Add(new CuiButton
            {
                Button =
                {
                    Command = "MySurveySpotChat",
                    Color = $"{buttonColor}"
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnemilieu} 0.15",
                    AnchorMax = $"{fincolonnemilieu} 0.20"
                },
                Text =
                {
                    Text = $"{buttonchat}",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 16, 
                    Align = TextAnchor.MiddleCenter
                }
            }, SurveyPanel);

            var TextLine7 = CuiElement.Add(new CuiLabel
            {
                Text =
                {
                    Text = "LOG & CHAT REAL PLAYERS ONLY :",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 12,
                    Align = TextAnchor.MiddleRight
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnegauche} 0.05",
                    AnchorMax = $"{fincolonnegauche} 0.10"
                }
            }, SurveyPanel);

            var TextLine7b = CuiElement.Add(new CuiLabel
            {
                Text =
                {
                    Text = "<i> NPC & PLAYER or PLAYER only</i>",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 12,
                    Align = TextAnchor.MiddleLeft
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnedroite} 0.05",
                    AnchorMax = $"{fincolonnemenu} 0.10"
                }
            }, SurveyPanel);

            var ButtonLine7 = CuiElement.Add(new CuiButton
            {
                Button =
                {
                    Command = "MySurveySpotPlayerOnly",
                    Color = $"{buttonColor}"
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnemilieu} 0.05",
                    AnchorMax = $"{fincolonnemilieu} 0.10"
                },
                Text =
                {
                    Text = $"{buttononly}",
                    Color = "0.0 0.0 0.0 1.0",
                    FontSize = 16, 
                    Align = TextAnchor.MiddleCenter
                }
            }, SurveyPanel);

            CuiHelper.AddUi(player, CuiElement);
        }

#endregion

#region LOGPAGE

        [ConsoleCommand("SurveyMySpotPage1")]
        private void SurveyMySpotPage1(ConsoleSystem.Arg arg) 
        {
           var player = arg.Connection.player as BasePlayer;
                reading.Remove(player.userID);
                reading.Add(player.userID, 1);
                if (debug == true) Puts($"-> LOG PAGE 1");
                CuiHelper.DestroyUi(player, LogPanel);             
                MySurveyLog(arg);   
        }
        [ConsoleCommand("SurveyMySpotPage2")]

        private void SurveyMySpotPage2(ConsoleSystem.Arg arg) 
        {
           var player = arg.Connection.player as BasePlayer;
                reading.Remove(player.userID);
                reading.Add(player.userID, 2);
                    if (debug == true)    
                    {
                        Puts($"-> LOG PAGE 2");
                    }
                CuiHelper.DestroyUi(player, LogPanel);             
                MySurveyLog(arg);   
        }
        [ConsoleCommand("SurveyMySpotPage3")]

        private void SurveyMySpotPage3(ConsoleSystem.Arg arg) 
        {
           var player = arg.Connection.player as BasePlayer;
                reading.Remove(player.userID);
                reading.Add(player.userID, 3);
                    if (debug == true)    
                    {
                        Puts($"-> LOG PAGE 3");
                    }
                CuiHelper.DestroyUi(player, LogPanel);             
                MySurveyLog(arg);   
        }

        [ConsoleCommand("SurveyMySpotPage4")]
        private void SurveyMySpotPage4(ConsoleSystem.Arg arg) 
        {
           var player = arg.Connection.player as BasePlayer;
                reading.Remove(player.userID);
                reading.Add(player.userID, 4);
                    if (debug == true)    
                    {
                        Puts($"-> LOG PAGE 4");
                    }
                CuiHelper.DestroyUi(player, LogPanel);             
                MySurveyLog(arg);   
        }
#endregion

#region LOG PANEL

        [ConsoleCommand("MySurveySpotLog")]
        private void MySurveyLog(ConsoleSystem.Arg arg) 
        {
            Interface.Oxide.DataFileSystem.WriteObject("SurveyMySpot", storedData);

            var player = arg.Connection.player as BasePlayer;
            ulong playerID = player.userID;
            string playerIDstring = playerID.ToString();
            string playername = player.displayName;
            string shortplayerID = playerIDstring.Substring(10, 6);
            string shortplayername = playername.Substring(0,8);
            string spot = $"999666{shortplayerID}";

            var debutcolonnegauche = 0.05;
            var fincolonnegauche = 0.35;
            var debutcolonnemilieu = 0.37;
            var fincolonnemilieu = 0.58;
            var debutcolonnedroite = 0.60;
            var fincolonnedroite = 0.85;
            var debutcolonnemenu = 0.87;
            var fincolonnemenu = 0.98;

            string PanelColor = "0.5 0.5 0.5 0.5";
            string buttonCloseColor = "0.6 0.26 0.2 1";

            string logdisplayold;
            string logdisplay = "";
            string logdisplay1 = "";
            string logdisplay2 = "";
            string logdisplay3 = "";
            string logdisplay4 = "";

            List<string> thisloglist = storedData.ulongdata[player.userID].loglist;
            
            string[] logarray;
            logarray = thisloglist.ToArray();

            int logline = thisloglist.Count;
            int logline1;
            int logline2;
            int logline3;
            int logline4;
            int logpage = 1;
            string logpagestring = string.Empty;

            int Round = 1;
            int round = logline;

            if (logline <= 25)
            {

                for (Round = 1; Round <= logline ; Round++)            
                {
                    round = round - 1;
                    logdisplayold = $"{logarray[round]}";
                    logdisplay1 = $"{logdisplay1}\n{logdisplayold}";
                }
            }
          if (logline > 25)
            {
                logline1 = 25;
                logline2 = logline - 25;
                logline3 = logline - 50;
                logline4 = logline - 75;

                for (Round = 1; Round <= logline1 ; Round++)            
                {
                    round = round - 1;
                    logdisplayold = $"{logarray[round]}";
                    logdisplay1 = $"{logdisplay1}\n{logdisplayold}";
                }

                int round2 = logline2;

                for (Round = 1; Round <= logline2 ; Round++)            
                {
                    round2 = round2 - 1;
                    logdisplayold = $"{logarray[round2]}";
                    logdisplay2 = $"{logdisplay2}\n{logdisplayold}";
                }

                if (logline >50)
                {
                    int round3 = logline3;

                    for (Round = 1; Round <= logline3 ; Round++)            
                    {
                        round3 = round3 - 1;
                        logdisplayold = $"{logarray[round3]}";
                        logdisplay3 = $"{logdisplay3}\n{logdisplayold}";
                    }

                    if (logline >75)
                    {
                        int round4 = logline4;

                        if (logline >100)
                        {
                            logline4 = 25;
                        }
                        for (Round = 1; Round <= logline4 ; Round++)            
                        {
                            round4 = round4 - 1;
                            logdisplayold = $"{logarray[round4]}";
                            logdisplay4 = $"{logdisplay4}\n{logdisplayold}";
                        }

                    }
                }
            }
            if (reading.ContainsKey(player.userID))
            {
                reading.TryGetValue(player.userID, out logpage);
            }
            if (logpage == 1)
            {
                logdisplay = logdisplay1;
                logpagestring = "from 001 to 025";
            }
            if (logpage == 2)
            {
                logdisplay = logdisplay2;
                logpagestring = "from 025 to 050";
            }
            if (logpage == 3)
            {
                logdisplay = logdisplay3;
                logpagestring = "from 050 to 075";

            }
            if (logpage == 4)
            {
                logdisplay = logdisplay4;
                logpagestring = "from 075 to 100";

            }

            CuiHelper.DestroyUi(player, SurveyPanel);

            var CuiElement = new CuiElementContainer();

            LogPanel = CuiElement.Add(new CuiPanel

            {
                Image =
                {
                    //Color = "0.1 0.1 0.1 1"
                    Color = $"{PanelColor}"
                },
                RectTransform =
                {
                    AnchorMin = "0.25 0.25",
                    AnchorMax = "0.75 0.8"
                },
                CursorEnabled = true
            });

            var closeButton = new CuiButton
            {
                Button =
                {
                    Close = LogPanel,
                    Color = $"{buttonCloseColor}"
                },
                RectTransform =
                {
                    AnchorMin = "0.85 0.85",
                    AnchorMax = "0.95 0.95"
                },
                Text =
                {
                    Text = "[X]\nClose",
                    FontSize = 16,
                    Align = TextAnchor.MiddleCenter
                }
            };

            CuiElement.Add(closeButton, LogPanel);

            var ButtonInfo = CuiElement.Add(new CuiButton
            {
                Button =
                {
                    Command = "MySurveySpotBackToPanel",
                    Color = $"0.0 0.5 1.0 0.5"
                },
                RectTransform =
                {
                    AnchorMin = "0.73 0.85",   
                    AnchorMax = "0.83 0.95"
                },
                Text =
                {
                    Text = $"BACK",
                    Color = "0.8 1.0 1.0 1",
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter
                }
            }, LogPanel);

            var Page1 = CuiElement.Add(new CuiButton
            {
                Button =
                {
                    Command = "SurveyMySpotPage1",
                    Color = "0.25 0.25 0.65 0.5"
                },
                RectTransform =
                {
                    AnchorMin = "0.73 0.80",   
                    AnchorMax = "0.83 0.84"
                },
                Text =
                {
                    Text = $"001->025",
                    Color = "0.8 1.0 1.0 1",
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter
                }
            }, LogPanel);

            var Page2 = CuiElement.Add(new CuiButton
            {
                Button =
                {
                    Command = "SurveyMySpotPage2",
                    Color = "0.25 0.25 0.65 0.5"
                },
                RectTransform =
                {
                    AnchorMin = "0.85 0.80",   
                    AnchorMax = "0.95 0.84"
                },
                Text =
                {
                    Text = $"025->050",
                    Color = "0.8 1.0 1.0 1",
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter
                }
            }, LogPanel);

            var Page3 = CuiElement.Add(new CuiButton
            {
                Button =
                {
                    Command = "SurveyMySpotPage3",
                    Color = "0.25 0.25 0.65 0.5"
                },
                RectTransform =
                {
                    AnchorMin = "0.73 0.75",   
                    AnchorMax = "0.83 0.79"
                },
                Text =
                {
                    Text = $"050->075",
                    Color = "0.8 1.0 1.0 1",
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter
                }
            }, LogPanel);

            var Page4 = CuiElement.Add(new CuiButton
            {
                Button =
                {
                    Command = "SurveyMySpotPage4",
                    Color = "0.25 0.25 0.65 0.5"
                },
                RectTransform =
                {
                    AnchorMin = "0.85 0.75",   
                    AnchorMax = "0.95 0.79"
                },
                Text =
                {
                    Text = $"075->100",
                    Color = "0.8 1.0 1.0 1",
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter
                }
            }, LogPanel);


            var TextLineIntro1 = CuiElement.Add(new CuiLabel
            {
                Text =
                {
                    Text = "MY SURVEY SPOT PANEL",
                    FontSize = 30,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnegauche} 0.85",   
                    AnchorMax = $"0.71 0.95"
                }
            }, LogPanel);

            var TextLineIntro2 = CuiElement.Add(new CuiLabel
            {
                Text =
                {
                    Text = $"<i>Watch your Spot' Log {logpagestring}</i>",
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = $"{debutcolonnegauche} 0.78",   
                    AnchorMax = $"0.71 0.84"
                }
            }, LogPanel);

            var TextLine1 = CuiElement.Add(new CuiLabel
            {
                Text =
                {
                    Text = $"{logdisplay}",
                    FontSize = 10,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = $"0.01 0.01",   
                    AnchorMax = $"0.99 0.73"
                }
            }, LogPanel);

            CuiHelper.AddUi(player, CuiElement);
        }
#endregion

    }
}