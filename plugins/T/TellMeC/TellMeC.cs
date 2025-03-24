using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Rust;
using Oxide.Core.Plugins;
using System.Globalization;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Tell Me C", "Krungh Crow", "1.1.4")]
    [Description("Tell THE correct color combination you see, and get Ammo and RP or Eco Points")]
    public class TellMeC: RustPlugin

    #region Changelogs and ToDo
    /*==============================================================================================================
    *    
    *    THANKS to BuzZ[PHOQUE] the original creator of this plugin
    *    
    *    v1.1.2 :   changed timer calls on checkup
    *    v1.1.3 :   Possible fix for timers not starting
    *               Added clear data on unload
    *               Added support for Battlepass
    *    v1.1.4 :   Fixed double calls
    *    
     ==============================================================================================================*/
    #endregion

    {
        [PluginReference]     
        Plugin Battlepass, ServerRewards, Economics, GUIAnnouncements;

        #region Variables

        string ColorToFind;
        string WinWord;
        List<ulong> TellMeCPlayerIDs; 
        private bool ConfigChanged;
        private string ItemWon = "";
        private string ItemToWin = "";
        int QuantityToWin;
        bool TellMeCIsOn;
        string MixC;
        string finalsentence;
        float TellMeCRate = 600;
        float TellMeCLength = 25;
        private string ToWait;

        bool debug = false;

        string Prefix = "<color=purple>[Color Game]</color> ";
        ulong SteamIDIcon = 76561198842641699;// STEAM PROFILE CREATED FOR THIS PLUGIN : ID = 76561198842641699
        string WinnerColor = "yellow";
        private bool UseServerRewards = false;
        private bool UseEconomics = false;
        private bool UseBattlepass = false;
        bool useBattlepass1 = false;
        bool useBattlepass2 = false;
        bool useBattlepassloss = false;
        int battlepassWinReward1 = 20;
        int battlepassWinReward2 = 20;
        int battlepassLossReward1 = 10;
        int battlepassLossReward2 = 10;
        int PointsOnWin = 250;
        int PointsOnLoss = 25;
        int MinPlayer = 1;
        private bool UseGUI = false;


        #endregion

        #region Librarys

        Dictionary<int, string> Item = new Dictionary<int, string>()
        {
            [0] = "ammo.pistol",
            [1] = "ammo.pistol.fire",
            [2] = "ammo.pistol.hv",
            [3] = "ammo.rifle",
            [4] = "ammo.rifle.explosive",
            [5] = "ammo.rifle.hv",
            [6] = "ammo.rifle.incendiary",
            [7] = "ammo.shotgun",
            [8] = "ammo.shotgun.slug",
            [9] = "ammo.handmade.shell",            
        };

        Dictionary<int, int> Quantity = new Dictionary<int, int>()
        {
            [0] = 5,
            [1] = 10,
            [2] = 15,
            [3] = 20,           
            [4] = 25,           
            [5] = 30,           
        };

        Dictionary<string, string> colorRGB = new Dictionary<string, string>()
        {
               { "#2d2d2d" , "black" },
               { "#00c5e8" , "blue" },
               { "#54ff68" , "green" },
               { "#a3a3a3" , "grey" },
               { "#ffb163" , "orange" },
               { "#bf64fc", "purple" },
               { "#ff726b" ,"red" },
               { "white" , "white" },
               { "#fffc75" ,"yellow" },   
        };
        
        Dictionary<string, string> randomed = new Dictionary<string, string>();    
        List<String> mixRGB = new List<string>();
        List<String> mixTOSEE = new List<string>();

        #endregion

        #region LanguageAPI

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"StartTellMeCMsg", "Find the correct color match you see !\n(ex: <color=yellow>/color white</color>)"},
                {"NextTellMeCMsg", "The game restarts every "},
                {"AlreadyTellMeCMsg", "You have already played this round !\n"},
                {"InvalidTellMeCMsg", "Invalid entry.\nTry something like /c white"},
                {"WonTellMeCMsg", "found the color match !\nand has won :"},
                {"EndTellMeCMsg", "The correct color match was "},
                {"ExpiredTellMeCMsg", "was not found in time !"},
                {"LoseTellMeCMsg", "This is NOT THE CORRECT COLOR MATCH... for trying you won "},
                {"SorryErrorMsg", "Sorry an error has occured ! Please Tell <color=red>Krungh Crow</color> about this Thank you !. Item to give was null. gift was : "},

            }, this, "en");
        }

        #endregion

        #region CONFIG

        protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            Prefix = Convert.ToString(GetConfig("Message Settings", "Prefix", "<color=purple>[Color Game]</color> "));
            SteamIDIcon = Convert.ToUInt64(GetConfig("Message Settings", "SteamIDIcon", "76561198842641699"));
            WinnerColor = Convert.ToString(GetConfig("Message Settings", "Color For Winner Name", "yellow"));
            UseGUI = Convert.ToBoolean(GetConfig("Message Settings", "Use GuiAnnouncement on win", "false"));
            UseServerRewards = Convert.ToBoolean(GetConfig("Rewards Settings", "Use Server Rewards", "false"));
            UseEconomics = Convert.ToBoolean(GetConfig("Rewards Settings", "Use Economics", "false"));
            PointsOnWin = Convert.ToInt32(GetConfig("Rewards Settings", "Points on Win", "250"));
            PointsOnLoss = Convert.ToInt32(GetConfig("Rewards Settings", "Points on Loss", "25"));
            TellMeCRate = Convert.ToSingle(GetConfig("Game repeater", "Rate in seconds", "600"));
            TellMeCLength = Convert.ToSingle(GetConfig("Game length", "in seconds", "25"));
            MinPlayer = Convert.ToInt32(GetConfig("Online Settings", "Minimum amount of players to be online to start the game", "1"));
            //Battlepass
            UseBattlepass = Convert.ToBoolean(GetConfig("Reward Battlepass Settings", "Use Battlepass", false));
            useBattlepass1 = Convert.ToBoolean(GetConfig("Reward Battlepass Settings", "Use Battlepass 1st currency", false));
            useBattlepass2 = Convert.ToBoolean(GetConfig("Reward Battlepass Settings", "Use Battlepass 2nd currency", false));
            useBattlepassloss = Convert.ToBoolean(GetConfig("Reward Battlepass Settings", "Use Battlepass on loss", false));
            battlepassWinReward1 = Convert.ToInt32(GetConfig("Reward Battlepass Settings", "Amount 1st currency (win)", 20));
            battlepassWinReward2 = Convert.ToInt32(GetConfig("Reward Battlepass Settings", "Amount 2nd currency (win)", 20));
            battlepassLossReward1 = Convert.ToInt32(GetConfig("Reward Battlepass Settings", "Amount 1st currency (loss)", 10));
            battlepassLossReward2 = Convert.ToInt32(GetConfig("Reward Battlepass Settings", "Amount 2nd currency (loss)", 10));

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

        #region SERVER REWARDS PLUGIN VERIFICATION DU .CS ET ERROR
        void Loaded()
        {
            if (UseServerRewards == true)
            {
                if (ServerRewards == false) PrintError("ServerRewards is not installed. Change your config option to disable ServerRewards and reload TellMeC. Thank you.");
            }
            if (UseEconomics == true)
            {
                if (Economics == false) PrintError("Economics is not installed. Change your config option to disable Economics and reload TellMeC. Thank you.");
            }
            if (UseBattlepass == true || useBattlepass1 == true || useBattlepass2 == true)
            {
                if (Battlepass == false) PrintError("Battlepass is not installed. Change your config option to disable Battlepass settings and reload TellMeC. Thank you.");
            }

            if (TellMeCLength >= TellMeCRate) PrintError("Game length is bigger than game rate. Change your config options in seconds and reload TellMeC. Thank you.");
        }

        void Unload()
        {
            if (TellMeCPlayerIDs != null)
            {
                TellMeCIsOn = false;
                TellMeCPlayerIDs.Clear();
            }
        }

        #endregion

        #region EXPIRATION

        void TellMeCExpired()
        {
            Server.Broadcast($"Color {MixC} {lang.GetMessage("ExpiredTellMeCMsg", this)}",Prefix, SteamIDIcon); 
            TellMeCIsOn = false;    
            TellMeCPlayerIDs.Clear();
        }
        #endregion

        #region ON SERVER INIT

		private void Init()
        {
            randomed.Clear();
            mixRGB.Clear();
            mixTOSEE.Clear();
            LoadVariables();
        }

        private void OnServerInitialized()
        {
            timer.Every(TellMeCRate, () =>
            {
                //Puts($"Trigger for check each {TellMeCRate} sec");
                if (TellMeCIsOn == false)
                {
                    if (BasePlayer.activePlayerList.Count >= MinPlayer && (TellMeCIsOn == false))
                    {
                        //Puts("Trigger for check when minimum players is online");
                        {
                            StartTellMeC();
                        }
                    }
                    else
                    {
                        //Puts("Noone online or TellMeCIsON");
                        return;
                    }
                }
                else if (TellMeCIsOn == true)
                {
                    return;
                    //Puts("TellMeCIsON");
                }
            });
        }
        #endregion

        #region TELLMEC

        private void StartTellMeC()
        {
            TellMeCPlayerIDs = new List<ulong>();
            string ColorToFound = Randomizer();
            ColorToFind = ColorToFound;
            colorRGB.TryGetValue(ColorToFind, out WinWord);               
            if (debug) Puts($" Win value in RGB : {ColorToFind}; in clear to see {WinWord}");
            MixC = $"<color={ColorToFind}>{WinWord}</color>";
            BuildMix();
            string[] mixRGBstring = mixRGB.ToArray();
            string[] mixTOSEEstring = mixTOSEE.ToArray();
            List<string> mixTOSEEconvert = new List<string>();
            int MixRound = 1;
            int MixDisplayed = 15;
            int MixToRandom = 15;
            List<string> mixtodisplay = new List<string>();
            mixtodisplay.Add(MixC);
            for (MixRound = 1; MixRound <= MixDisplayed; MixRound++)
            {
                int round = MixRound -1;
                string tosee;
                string WordZ = mixTOSEEstring[round];
                colorRGB.TryGetValue(WordZ, out tosee);
                mixTOSEEconvert.Add(tosee);
            }
            string[] mixTOSEEclear = mixTOSEEconvert.ToArray();
            MixRound=1;
            for (MixRound = 1; MixRound <= MixDisplayed; MixRound++)
            {
                int round = MixRound -1;
                string MixYold;
                string ColorY = mixRGBstring[round];
                string WordY = mixTOSEEclear[round];
                string MixY = $"<color={ColorY}> {WordY}</color>";
                mixtodisplay.Add(MixY);
                if (debug) Puts($"STEP {MixRound} MIX DONE : {MixY}");
            }
            if (debug) Puts($"MIX C FOR THE WINNER TO SEE {MixC}");
            List<string> sentence = new List<string>();
            MixRound=1;
            int lines = mixtodisplay.Count;
            if (debug) Puts($"nombre a display {lines.ToString()}");
            for (MixRound = 1; MixRound <= lines; MixRound++)
            {   
                MixRound=MixRound -1;
                int RandomLine = Core.Random.Range(0, lines);
                string mixed = mixtodisplay[RandomLine];
                if (debug)
                {
                    Puts($"lignes restantes {lines.ToString()}");
                    Puts($"string mixed {mixed}");
                }
                lines = mixtodisplay.Count - 1;
                sentence.Add(mixed);
                mixtodisplay.RemoveAt(RandomLine);
            }
            string[] sentenceTOSEE = sentence.ToArray();
            finalsentence =$"{sentenceTOSEE[0]}, {sentenceTOSEE[1]}, {sentenceTOSEE[2]}, {sentenceTOSEE[3]}, {sentenceTOSEE[4]}, {sentenceTOSEE[5]}, {sentenceTOSEE[6]}, {sentenceTOSEE[7]}\n{sentenceTOSEE[8]}, {sentenceTOSEE[9]}, {sentenceTOSEE[10]}, {sentenceTOSEE[11]}, {sentenceTOSEE[12]}, {sentenceTOSEE[13]}, {sentenceTOSEE[14]}, {sentenceTOSEE[15]}";
            if (debug) Puts($"{finalsentence}");
            BroadcastSentence(true);
            Puts($"Color Game has started. The color to match is : {WinWord}");
            TellMeCIsOn = true;
            timer.Once(TellMeCLength, () =>
            {
                if (TellMeCIsOn) TellMeCExpired();
            });
            int RandomQuantity = Core.Random.Range(0,6);
            int RandomItem = Core.Random.Range(0,10);
            ItemToWin = Item[RandomItem];
            QuantityToWin = Quantity[RandomQuantity];
        }

        #endregion

        #region BUILD THE MIX

        public void BuildMix()
        {
            int MixRound = 1;
            int WordsDisplayed = 14;
            for (MixRound = 1; MixRound <= WordsDisplayed; MixRound++)
            {
                string ColorX = Randomizer();
                string WordX = Randomizer();
                string IsAlready;
                if (ColorX == WordX)
                {
                    MixRound = MixRound -1;
                    WordsDisplayed = WordsDisplayed +1;
                    if (debug) Puts($"STEP {MixRound} , EQUALITY.");
                    continue;
                }
                mixRGB.Add(ColorX);
                mixTOSEE.Add(WordX);
                if (debug) Puts($"STEP {MixRound} , PAIR {ColorX} - {WordX} ADDED TO DICO.");
            }
        }

        string Randomizer()
        {
            int RandomRGB;
            RandomRGB = Core.Random.Range(0, 8);
            List<String> RGBKeys = colorRGB.Keys.ToList();
            string[] RGBstring = RGBKeys.ToArray();
            string ColorToFinder = RGBstring[RandomRGB];
            return(ColorToFinder);
        }

        #endregion

        #region BROADCAST

        // BROADCAST

        void BroadcastSentence(bool start)
        {
            if (start) Server.Broadcast($"{lang.GetMessage("StartTellMeCMsg", this)}\n{finalsentence}",Prefix, SteamIDIcon);
            else Server.Broadcast($"{lang.GetMessage("EndTellMeCMsg", this)} {MixC}",Prefix, SteamIDIcon);
        }        

        #endregion

        #region CHAT COMMAND /color

        [ChatCommand("color")]
        void TellMeCCommand(BasePlayer player, string command, string[] args)
        {
            if (!TellMeCIsOn)
            {
                Player.Message(player, $"{lang.GetMessage("NextTellMeCMsg", this, player.UserIDString)} {TellMeCRate} seconds",Prefix, SteamIDIcon);
                return;
            }

            if(TellMeCPlayerIDs.Contains(player.userID))
            {
                Player.Message(player, $"{lang.GetMessage("AlreadyTellMeCMsg", this, player.UserIDString)}",Prefix, SteamIDIcon);
                return;
            }

            if(args.Length != 1)
            {
                Player.Message(player, $"{lang.GetMessage("InvalidTellMeCMsg", this, player.UserIDString)}",Prefix, SteamIDIcon);                
                return;
            }
            string answer = args[0];
            if (answer.ToLower().Contains(WinWord))
            {
                TellMeCIsOn = false;
                TellMeCPlayerIDs.Clear();
                GivePlayerGift(player, ItemToWin);
                if (UseServerRewards == true)
                {
                    ServerRewards?.Call("AddPoints", player.userID, (int)PointsOnWin);
                    Server.Broadcast($"<color={WinnerColor}>{player.displayName}</color> {lang.GetMessage("WonTellMeCMsg", this, player.UserIDString)} [{ItemWon}] + [{PointsOnWin}.RP]", Prefix, SteamIDIcon);
                    if (UseGUI == true)
                    {
                        GUIAnnouncements?.Call("CreateAnnouncement", ($"{player.displayName} {lang.GetMessage("WonTellMeCMsg", this, player.UserIDString)} [{ItemWon}] + [{PointsOnWin}.RP]"), "blue", "yellow");
                    }
                }
                else if (UseEconomics == true)
                {
                    if ((bool)Economics?.Call("Deposit", player.userID, (double)PointsOnWin))
                        Server.Broadcast($"<color={WinnerColor}>{player.displayName}</color> {lang.GetMessage("WonTellMeCMsg", this, player.UserIDString)} [{ItemWon}] + [{PointsOnWin}.$]", Prefix, SteamIDIcon);
                    if (UseGUI == true)
                    {
                        GUIAnnouncements?.Call("CreateAnnouncement", ($"{player.displayName} {lang.GetMessage("WonTellMeCMsg", this, player.UserIDString)} [{ItemWon}] + [{PointsOnWin}.$]"), "blue", "yellow");
                    }
                }
                else if (UseBattlepass == true)
                {
                    if (useBattlepass1)
                    {
                        Battlepass?.Call("AddFirstCurrency", player.userID, battlepassWinReward1);
                        {
                            Server.Broadcast($"<color={WinnerColor}>{player.displayName}</color> {lang.GetMessage("WonTellMeCMsg", this, player.UserIDString)} [{ItemWon}] + [{battlepassWinReward1}.BP1]", Prefix, SteamIDIcon);
                            if (UseGUI == true)
                            {
                                GUIAnnouncements?.Call("CreateAnnouncement", ($"{player.displayName} {lang.GetMessage("WonTellMeCMsg", this, player.UserIDString)} [{ItemWon}] + [{battlepassWinReward1}.BP1]"), "blue", "yellow");
                            }
                        }
                    }

                    else if (useBattlepass2)
                    {
                        Battlepass?.Call("AddSecondCurrency", player.userID, battlepassWinReward2);
                        {
                            Server.Broadcast($"<color={WinnerColor}>{player.displayName}</color> {lang.GetMessage("WonTellMeCMsg", this, player.UserIDString)} [{ItemWon}] + [{battlepassWinReward2}.BP2]", Prefix, SteamIDIcon);
                            if (UseGUI == true)
                            {
                                GUIAnnouncements?.Call("CreateAnnouncement", ($"{player.displayName} {lang.GetMessage("WonTellMeCMsg", this, player.UserIDString)} [{ItemWon}] + [{battlepassWinReward2}.BP2]"), "blue", "yellow");
                            }
                        }
                    }
                }
                else
                {
                    Server.Broadcast($"<color={WinnerColor}>{player.displayName}</color> {lang.GetMessage("WonTellMeCMsg", this, player.UserIDString)} [{ItemWon}]", Prefix, SteamIDIcon);
                }
                BroadcastSentence(false);
            }
            else
            {        
                if (UseServerRewards)
                {                
                    ServerRewards?.Call("AddPoints", player.userID, (int)PointsOnLoss);
                    Player.Message(player, $"{lang.GetMessage("LoseTellMeCMsg", this, player.UserIDString)} [{PointsOnLoss}.RP]",Prefix, SteamIDIcon);               
                }
                else if (UseEconomics)
                {
                    if ((bool)Economics?.Call("Deposit", player.userID, (double)PointsOnLoss))
                    Player.Message(player, $"{lang.GetMessage("LoseTellMeCMsg", this, player.UserIDString)} [{PointsOnLoss}.RP]", Prefix, SteamIDIcon);
                }
                else if (UseBattlepass == true)
                {
                    if (useBattlepass1)
                    {
                        Battlepass?.Call("AddFirstCurrency", player.userID, battlepassLossReward1);
                        {
                            Player.Message(player, $"{lang.GetMessage("LoseTellMeCMsg", this, player.UserIDString)} [{battlepassLossReward1}.BP1]", Prefix, SteamIDIcon);
                        }
                    }

                    if (useBattlepass2)
                    {
                        Battlepass?.Call("AddSecondCurrency", player.userID, battlepassLossReward2);
                        {
                            Player.Message(player, $"{lang.GetMessage("LoseTellMeCMsg", this, player.UserIDString)} [{battlepassLossReward2}.BP2]", Prefix, SteamIDIcon);
                        }
                    }
                }
                TellMeCPlayerIDs.Add(player.userID);
            }
        }

        #endregion

        #region GIVE TO PLAYER
        void GivePlayerGift(BasePlayer player, string gift)
        {
            Item item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(gift).itemid,QuantityToWin);
            if (item == null)
            {
                Player.Message(player, $"{lang.GetMessage("SorryErrorMsg", this)} {ItemToWin}",Prefix, SteamIDIcon);               
                return;
            }
            player.GiveItem(item);
            ItemWon = $"{QuantityToWin} x {gift}";
        }
        #endregion
    }
}