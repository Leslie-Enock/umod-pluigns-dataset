using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using Rust;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Tell Me X", "Krungh Crow", "1.0.1")]
    [Description("Calculate X and get Bonus Ammo, RP Points, Economics")]
    public class TellMeX : RustPlugin

    #region Changelogs and ToDo
    /***********************************************************************************************************************
    *
    *    THANKS to BuzZ[PHOQUE] to creator of this plugin
    *    
    *    v1.0.1 : Added amount of minimum players to be online to start the game
    *    v1.0.1 : Added Battlepass support
    *             Added clear data on unload
    *             Reorganised cfg (delete old cfg before updating)
    *
    ************************************************************************************************************************/
    #endregion

    {
        [PluginReference]
        Plugin Battlepass, ServerRewards, Economics, GUIAnnouncements;

        #region declaration des variables

        float TellMeXRate = 600f;                       // FREQUENCY OF THE GAME IN SECONDS / FREQUENCE DU JEU EN SECONDES
        float TellMeXLength = 25f;                      // DURATION OF THE GAME IN SECONDS / DUREE DU JEU EN SECONDES
        int MinPlayer = 1;
        //const float TellMeXLength = Lengthf;          // FUTURE CUSTOM DURATION OF THE GAME IN SECONDS / DUREE DU JEU EN SECONDES
        float TellMeXEndTime;                           // FOR WHEN IT ENDS. VALUE GIVEN AT START VOID                                                         
        float NextTellMeXTime;                          // FOR TIME UNTIL NEXT GAME                                   
        bool TellMeXIsOn;                               // TRUE WHEN GAME IS ON
        bool OnReloading;                               // if ServerRewards Plugin reloads
        int QuantityToWin;                              // RANDOMISED QUANTITY TO APPLY TO RANDOMISED ITEM TO WIN

        List<ulong> TellMeXPlayerIDs;                   // TO STORE PLAYERIDS THAT TRIED TO FIND X
        private string Math = "";                       // TO
        private string XToFind = "";       
        private string ItemWon = "";
        private string ItemToWin = "";
        private string ToWait;
        private bool ConfigChanged;
        
        #endregion

        #region CUSTOMISABLES    
        // POUR LA CUSTOMISATION DU PLUGIN


        private string Prefix = "[TellMeX] ";           // CHAT PLUGIN PREFIX
        private string PrefixColor = "#47ff6f";         // CHAT PLUGIN PREFIX COLOR
        private string ChatColor = "#a0ffb5";           // CHAT MESSAGE COLOR
        ulong SteamIDIcon = 76561198842176097;          // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / 76561198842176097 /
        private string WinnerColor = "#4fffc7";         // WINNER NAME COLOR
        private string MathColor = "#e2ffe9";           // MATH COLOR
        private bool UseServerRewards = true;
        private bool UseEconomics = false;
        private bool UseBattlepass = false;
        bool useBattlepass1 = false;
        bool useBattlepass2 = false;
        bool useBattlepassloss = false;
        int battlepassWinReward1 = 20;
        int battlepassWinReward2 = 20;
        int battlepassLossReward1 = 10;
        int battlepassLossReward2 = 10;
        private bool UseGUI = false;
        //int Rate=25;                                  // FUTURE CUSTOM RATE OF THE GAME
        //int Length=25;                                // FUTURE CUSTOM DURATION OF THE GAME
        int RPOnWin = 5;                                // RP POINTS ADDED ON WIN
        int RPOnLose = 1;                               // RP ADDED ON LOSE   
        int EcoOnWin = 5;                                // RP POINTS ADDED ON WIN
        int EcoOnLose = 1;                               // RP ADDED ON LOSE   

        #endregion

        #region DICTIONNAIRES

        // DICTIONNAIRE ITEMS TO RANDOMIZE
        private Dictionary<int, string> Item = new Dictionary<int, string>() 
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

        // DICTIONNAIRE QUANTITY TO RANDOMIZE
        private Dictionary<int, int> Quantity = new Dictionary<int, int>()
        {
            [0] = 5,
            [1] = 10,
            [2] = 15,
            [3] = 20,           
            [4] = 25,           
            [5] = 30,           
        };

        // DICTIONNAIRE DES DIFFERENTS CALCULS
        private Dictionary<int, string> Calculs = new Dictionary<int, string>()
        {
            [0] = "({X} x {Y}) + {Z}",
            [1] = "{X} x ({Y} - {Z})",
            [2] = "{X} x ({Y} + {Z})",
            [3] = "{X} + ({Y} x {Z})",
            [4] = "{X} - ({Y} x {Z})",
            [5] = "({X} x {Y}) - {Z}",
            [6] = "{X} x {Y} x {Z}",            
        };

        #endregion

        #region MESSAGES / LANG
        // MESSAGES TO CALL - ENGLISH VERSION

        void LoadDefaultMessages()
        {

            lang.RegisterMessages(new Dictionary<string, string>
            {

                {"StartTellMeXMsg", "X ="},
                {"CommandTellMeXMsg", "CALCULATE X AND TELL IT TO ME ! (example: <color=yellow>/x 1234</color>)"},
                {"NextTellMeXMsg", "Next 'Tell Me X' will start in "},
                {"AlreadyTellMeXMsg", "You've already played !\nTry again in "},
                {"InvalidTellMeXMsg", "Invalid guess.\nTry something like <color=yellow>/x 1234</color>"},
                {"NotNumericTellMeXMsg", "It is not a number !\nTry something like <color=yellow>/x 1234</color>"},
                {"WonTellMeXMsg", "did find X\nand has won :"},
                {"EndTellMeXMsg", "X was "},
                {"ExpiredTellMeXMsg", "was not found in time !"},
                {"LoseTellMeXMsg", "X is not equal to this..."},
                {"LoseTellMeXRPMsg", "X is not equal to this... BUT for playing you won "},
                {"SorryErrorMsg", "Sorry an error has occured ! Please Tell Krungh Crow about this Thank you !. Item to give was null. gift was : "},

            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {

                {"StartTellMeXMsg", "X ="},
                {"CommandTellMeXMsg", "CALCULEZ LA VALEUR DE X ET DONNEZ LA MOI ! (exemple: <color=yellow>/x 1234</color>)"},
                {"NextTellMeXMsg", "Le prochain calcul se fera dans "},
                {"AlreadyTellMeXMsg", "Vous avez déjà essayé !\nEssayez de nouveau dans "},
                {"InvalidTellMeXMsg", "Valeur invalide.\nEssayez dans ce format<color=yellow>/x 1234</color>"},
                {"NotNumericTellMeXMsg", "Ce n'est pas un nombre !\nUtilisez ce format <color=yellow>/x 1234</color>"},
                {"WonTellMeXMsg", "a trouvé(e) X\net a remporté(e) :"},
                {"EndTellMeXMsg", "X était "},
                {"ExpiredTellMeXMsg", "n'a pas été trouvé à temps !"},
                {"LoseTellMeXMsg", "X n'est pas égal à ce nombre..."},
                {"LoseTellMeXRPMsg", "X n'est pas égal à ce nombre... MAIS pour la participation, vous avez gagné "},
                {"SorryErrorMsg", "Désolé, une erreur a eue lieu. Touchez en un mot à Krungh Crow, Merci ! L'objet à donner été 'nul'. Le cadeau était : "},


            }, this, "fr");
        }


        #endregion

        #region SERVER REWARDS PLUGIN VERIFICATION DU .CS ET WARNING
        void Loaded()
        {
            if (UseServerRewards == true)
            {
                if (ServerRewards == false)
                {
                    PrintError("ServerRewards.cs is not present. Change your config option to disable RP rewards and reload TellMeX. Thank you.");
                }
            }

            if (UseEconomics == true)
            {
                if (Economics == false)
                {
                    PrintError("Economics.cs is not present. Change your config option to disable $ rewards and reload TellMeX. Thank you.");
                }
            }

            if (UseBattlepass == true || useBattlepass1 == true || useBattlepass2 == true)
            {
                if (Battlepass == false) PrintError("Battlepass is not installed. Change your config option to disable Battlepass settings and reload TellMeC. Thank you.");
            }
        }

        #endregion

        #region Unload
        void Unload()
        {
            if (TellMeXPlayerIDs != null)
            {
                TellMeXIsOn = false;
                TellMeXPlayerIDs.Clear();
            }
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

            Prefix = Convert.ToString(GetConfig("Message Settings", "Prefix", "[TellMeX] "));                      // CHAT PLUGIN PREFIX
            PrefixColor = Convert.ToString(GetConfig("Message Settings", "PrefixColor", "#47ff6f"));               // CHAT PLUGIN PREFIX COLOR
            ChatColor = Convert.ToString(GetConfig("Message Settings", "ChatColor", "#a0ffb5"));                   // CHAT MESSAGE COLOR
            SteamIDIcon = Convert.ToUInt64(GetConfig("Message Settings", "SteamIDIcon", 76561198842176097));            // SteamID FOR PLUGIN ICON - STEAM PROFILE CREATED FOR THIS PLUGIN / 76561198842176097 /
            WinnerColor = Convert.ToString(GetConfig("Message Settings", "Color For Winner Name", "#4fffc7"));     // WINNER NAME COLOR
            MathColor = Convert.ToString(GetConfig("Message Settings", "Color Of Math Expression", "#e2ffe9"));
            UseGUI = Convert.ToBoolean(GetConfig("Message Settings", "Use GuiAnnouncement on win", "false"));
            UseServerRewards = Convert.ToBoolean(GetConfig("Rewards Settings", "Use Server Rewards", true));
            RPOnWin = Convert.ToInt32(GetConfig("Rewards Settings", "RP Points on Win", 5));
            RPOnLose = Convert.ToInt32(GetConfig("Rewards Settings", "RP Points on Lose", 1));
            UseEconomics = Convert.ToBoolean(GetConfig("Rewards Settings", "Use Economics", false));
            EcoOnWin = Convert.ToInt32(GetConfig("Rewards Settings", "Economics on Win", 5));
            EcoOnLose = Convert.ToInt32(GetConfig("Rewards Settings", "Economics on Lose", 1));
            TellMeXRate = Convert.ToSingle(GetConfig("Game repeater", "Rate in seconds", "600"));
            TellMeXLength = Convert.ToSingle(GetConfig("Game length", "in seconds", "25"));
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

        #region INITIALISATION
        // INITIALISATION
		private void OnServerInitialized()
        {
        LoadVariables();
        NextTellMeXTime = Time.time + TellMeXRate;
        TellMeXPlayerIDs = new List<ulong>();
        }
        #endregion

        #region ON TICK
        // ON TICKET 
        void OnTick()
        {            
            // si TMX est off + next <timer
            if(!TellMeXIsOn && NextTellMeXTime < Time.time)
            {
                if (BasePlayer.activePlayerList.Count >= MinPlayer)
                {
                    StartTellMeX();
                }
                else
                {
                    return;
                }
                //StartTellMeX();                                                
            }
            
            if(TellMeXIsOn && TellMeXEndTime < Time.time)
            {
                TellMeXExpired();
            }
        }
        #endregion

        #region ON EXPIRATION
        // EXPIRATION 
        void TellMeXExpired()
        {
            // ExpiredTellMeXMsg = "was not found in time !";
            Server.Broadcast($"<color={ChatColor}> X ({XToFind}) {lang.GetMessage("ExpiredTellMeXMsg", this)} </color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon); 
            TellMeXIsOn = false;
            NextTellMeXTime = Time.time + TellMeXRate;
            TellMeXPlayerIDs.Clear();
        }
        #endregion

        #region START DU CALCUL
        // START DU CALCUL
        private void StartTellMeX()
        {
            
            TellMeXIsOn = true;

            TellMeXEndTime = Time.time + TellMeXLength;

            int RandomCalcul = Core.Random.Range(0, 7);                         // NOMBRE CHOIX CALCUL
            int RandomItem = Core.Random.Range(0, 10);                          // NOMBRE CHOIX ITEM
            int RandomQuantity = Core.Random.Range(0, 6);                       // NOMBRE CHOIX QUANTITE
            Math = Calculs[RandomCalcul];                                       // RESULTAT CHOIX CACUL
            ItemToWin = Item[RandomItem];                                       // RESULTAT CHOIX ITEM
            QuantityToWin = Quantity[RandomQuantity];                           // RESULTAT CHOIX QUANTITE
            int X = Core.Random.Range(2, 20);                                   // X AU HASARD
            int Y = Core.Random.Range(2, 20);                                   // Y AU HASARD
            int Z = Core.Random.Range(2, 20);                                   // Z AU HASARD
            Math = Math.Replace("{X}", X.ToString()).Replace("{Y}", Y.ToString()).Replace("{Z}", Z.ToString()); // REMPLACE LES VALEURS DANS LE CALCUL
            XToFind = DoMath(X, Y, Z, RandomCalcul);                            // EXECUTE DOMATH AVEC LES VALEURS XYZ ET LE RANDOM POUR AVOIR X
            if (XToFind == "")                                                  // SI LE X FINAL EST VIDE -> MESSAGE D ERREUR AVEC INFOS
            {
                PrintWarning($"Contact BuzZ[PHOQUE] with this :\nMath Error !\ncalcul n°{RandomCalcul} with X = {X}; Y = {Y}; Z = {Z}");
                if (BasePlayer.activePlayerList.Count >= MinPlayer)
                {
                    StartTellMeX();
                }
                else
                {
                    return;
                }
                //StartTellMeX();
            }
            BroadcastMath(true);                                                // DURANT LE START ON ENVOI LE MESSAGE BROADCAST DU CALCUL A FAIRE
            Puts($"TellMeX started, X = {Math} = {XToFind}");                   // ENVOI A LA CONSOLE LE CALCUL ET SON RESULTAT X

        }
        private string DoMath(int X, int Y, int Z, int RandomCalcul)            // EXECUTE LE CALCUL SUIVANT LE CAS DU RANDOM ET RETOURNE LE RESULTAT
        {
            switch (RandomCalcul)
            {
                case 0:
                    return $"{(X * Y) + Z}";
                case 1:
                    return $"{X * (Y - Z)}";
                case 2:
                    return $"{X * (Y + Z)}";
                case 3:
                    return $"{X + (Y * Z)}";
                case 4 :
                    return $"{X - (Y * Z)}";
                case 5 :
                    return $"{(X * Y) - Z}";
                case 6 :
                    return $"{X * Y * Z}";
                default:
                    return "";
            }
        }

        #endregion

        #region CHAT COMMAND /x /X


        [ChatCommand("x")]                                                              // SUR COMMANDE CHAT /x ou /X
        private void TellMeXCommand(BasePlayer player, string command, string[] args)
        {
            if (TellMeXIsOn == false)                                                           // SI GAME !ON, DISPLAY NEXT
            {
                float ToNext = Time.time - NextTellMeXTime;                             // CALCUL INTERVALLE EN SECONDES
                ToWait = ToNext.ToString("######;######");                              // ARRONDI ET SUPPRESSION DU NEGATIF
                // NextTellMeXMsg = "Next 'Tell Me X' will start in ";
                Player.Message(player, $"<color={ChatColor}> {lang.GetMessage("NextTellMeXMsg", this, player.UserIDString)} {ToWait} seconds</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }
            if (TellMeXPlayerIDs.Contains(player.userID))                                // CHECK IF ALREADY PLAYED
            {
                float ToNext = TellMeXRate - (Time.time - NextTellMeXTime) + TellMeXRate; //v0.2
                ToWait = ToNext.ToString("######");
                // AlreadyTellMeXMsg = "You've already played !\nTry again in ";
                Player.Message(player, $"<color={ChatColor}> {lang.GetMessage("AlreadyTellMeXMsg", this, player.UserIDString)} {ToWait} seconds</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }
    
            if(args.Length != 1)                                                        // si les arguments sont vides        
            {
                // InvalidTellMeXMsg = "Invalid guess.\nTry something like <color=yellow>/x 1234</color>";
                Player.Message(player, $"<color={ChatColor}> {lang.GetMessage("InvalidTellMeXMsg", this, player.UserIDString)} </color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);                
                return;
            }
    
            int PlayerNumber;                                                           // déclaration de l integer PlayerNumber
            
            bool isNumeric = int.TryParse(args[0], out PlayerNumber);                   // si c est numerique, on arrondi les args de la commande pour en sortir le PLayerNumber
            
            if(!isNumeric)                                                              // if chat is not numeric      
            {
                // NotNumericTellMeXMsg par defaut : ""
                Player.Message(player, $"<color={ChatColor}> {lang.GetMessage("NotNumericTellMeXMsg", this, player.UserIDString)} </color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                return;
            }

            if (args.Contains(XToFind))                                                 // WINNER SI X EST DANS LES ARGS DE LA COMMANDE PLAYER
            {
                TellMeXIsOn = false;                                                    // GAME IS !ON
                NextTellMeXTime = Time.time + TellMeXRate;                              // NEXT TIME = TIMER + RATE
                TellMeXPlayerIDs.Clear();                                               // VIDE LA LISTE DES JOUEURS
                GivePlayerGift(player, ItemToWin);                                      // EXECUTE GIVEPLAYER AVEC player ET ItemToWin



                string message = $"<color={ChatColor}> <color={WinnerColor}>{player.displayName}</color> {lang.GetMessage("WonTellMeXMsg", this, player.UserIDString)} [{ItemWon}]</color>";

                if (UseServerRewards == true)
                {
                    if (ServerRewards == true)
                    {
                        message = $"{message} + <color=#ffe556>[{RPOnWin}.RP]</color>";
                        ServerRewards?.Call("AddPoints", player.userID, (int)RPOnWin);          // HOOK VERS PLUGIN ServerRewards POUR ADD RPWin
                        if (UseGUI == true)
                        {
                            GUIAnnouncements?.Call("CreateAnnouncement", ($"{player.displayName} {lang.GetMessage("WonTellMeXMsg", this, player.UserIDString)} [{ItemWon}] + [{RPOnWin}.RP]"), "blue", "yellow");
                        }
                    }
                }            

                else if (UseEconomics == true)
                {
                    if (Economics == true)
                    {
                        double amount = Convert.ToDouble(EcoOnWin);
                        message = $"{message} + <color=#ffe556>[{EcoOnWin}.$]</color>";
                        Economics.Call("Deposit", player.userID, amount);          // HOOK VERS PLUGIN Economics POUR ADD EcoWin
                        if (UseGUI == true)
                        {
                            GUIAnnouncements?.Call("CreateAnnouncement", ($"{player.displayName} {lang.GetMessage("WonTellMeXMsg", this, player.UserIDString)} [{ItemWon}] + [{EcoOnWin}.$]"), "blue", "yellow");
                        }
                    }

                }
                else if (UseBattlepass == true)
                {
                    if (useBattlepass1)
                    {
                        Battlepass?.Call("AddFirstCurrency", player.userID, battlepassWinReward1);
                        {
                            message = $"{message} + <color=#ffe556>[{battlepassWinReward1}.BP1]</color>";
                            if (UseGUI == true)
                            {
                                GUIAnnouncements?.Call("CreateAnnouncement", ($"{player.displayName} {lang.GetMessage("WonTellMeXMsg", this, player.UserIDString)} [{ItemWon}] + [{battlepassWinReward1}.BP1]"), "blue", "yellow");
                            }
                        }
                    }

                    else if (useBattlepass2)
                    {
                        Battlepass?.Call("AddSecondCurrency", player.userID, battlepassWinReward2);
                        {
                            message = $"{message} + <color=#ffe556>[{battlepassWinReward2}.BP2]</color>";
                            if (UseGUI == true)
                            {
                                GUIAnnouncements?.Call("CreateAnnouncement", ($"{player.displayName} {lang.GetMessage("WonTellMeXMsg", this, player.UserIDString)} [{ItemWon}] + [{battlepassWinReward2}.BP2]"), "blue", "yellow");
                            }
                        }
                    }
                }

                Server.Broadcast($"{message}",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon); 

                BroadcastMath(false);                                                   // PASSE LE VOID BROADCAST A FALSE
            }
            else                                                                        // WE COULD SAY LOSER
            {            

                string message = $"<color={ChatColor}> {lang.GetMessage("LoseTellMeXMsg", this, player.UserIDString)} </color>";

                if (UseServerRewards == true)
                {
                    if (ServerRewards == true)
                    {
                    message = $"{message} + <color=#ffe556>[{RPOnLose}.RP]</color>";
                    ServerRewards?.Call("AddPoints", player.userID, (int)RPOnLose);          // HOOK VERS PLUGIN ServerRewards POUR ADD RPWin
                    }

                }            

                if (UseEconomics == true)
                {
                    if (Economics == true)
                    {
                    double amount = Convert.ToDouble(EcoOnLose);
                    message = $"{message} + <color=#ffe556>[{EcoOnLose}.$]</color>";
                    Economics.Call("Deposit", player.userID, amount);          // HOOK VERS PLUGIN Economics POUR ADD EcoWin
                    }

                }
                else if (UseBattlepass == true)
                {
                    if (useBattlepass1)
                    {
                        Battlepass?.Call("AddFirstCurrency", player.userID, battlepassLossReward1);
                        {
                            message = $"{message} + <color=#ffe556>[{battlepassLossReward1}.$]</color>";
                        }
                    }

                    if (useBattlepass2)
                    {
                        Battlepass?.Call("AddSecondCurrency", player.userID, battlepassLossReward2);
                        {
                            message = $"{message} + <color=#ffe556>[{battlepassLossReward2}.$]</color>";
                        }
                    }
                }
                Player.Message(player, $"{message}",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
                TellMeXPlayerIDs.Add(player.userID);                                   // ADD PLAYER TO THOSE WHO TRIED TO FIND X

            }
        }

        #endregion

        #region BROADCAST MATH CALCUL

        private void BroadcastMath(bool start)
        {
            if (start)                                                                  // SI START On -> BROADCAST StartMsg                                                       
            {
            Server.Broadcast($"<color={ChatColor}> {lang.GetMessage("StartTellMeXMsg", this)} <color={MathColor}>{Math}</color>\n{lang.GetMessage("CommandTellMeXMsg", this)} </color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
//            Server.Broadcast($"<color={ChatColor}> {lang.GetMessage("StartTellMeXMsg", this)} <color={MathColor}>{Math}</color>\n{lang.GetMessage("CommandTellMeXMsg", this)} </color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);

            }
            else                                                                        // SINON -> BROADCAST EndMsg                                                                    
            {
            Server.Broadcast($"<color={ChatColor}> {lang.GetMessage("EndTellMeXMsg", this)} {XToFind}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);
            }
        }
        #endregion

        #region GIVE TO PLAYER

        private void GivePlayerGift(BasePlayer player, string gift)
        {
            Item item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(gift).itemid,QuantityToWin);
            if (item == null)
            {
            Player.Message(player, $"<color={ChatColor}> {lang.GetMessage("SorryErrorMsg", this)} {ItemToWin}</color>",$"<color={PrefixColor}> {Prefix} </color>", SteamIDIcon);               
            return;
            }
            player.GiveItem(item);
            ItemWon = $"{QuantityToWin} x {gift}";
        }

        #endregion

    }
}
