using UnityEngine;
using System.Collections.Generic;
using System;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Ultimate Gamble", "ColdUnwanted", "0.2.5")]
    [Description("A all-in-one gambling system with in game time timer.")]

    public class UltimateGamble : RustPlugin
    {
        // Configuration
        private bool ConfigChanged;

        // Configuration variables
        // Raffle
        private bool raffleActive;
        private float raffleNextStartTime;
        private List<string> raffleStartTime;
        private int raffleJoinAmount;
        private int raffleRewardAmount;
        private int rafflePlayersNeeded;
        private string raffleReward;
        private string raffleJoin;
        private int raffleDaysSkip;
        private bool raffleUseServerTime;

        // Blackjack
        private string blackjackFee;
        private int blackjackMinimumFee;
        private float blackjackMultiplier;
        private int blackjackCooldown; // ToDo
        private int blackjackTimeout;

        // Variables declaration
        // General 
        private int neededAuthLevel;
        private List<DisconnectData> disconnectedPlayers;

        // Raffle
        private bool isRaffleTime = false;
        private List<ulong> joinedPlayersId;
        private string raffleJoinName;
        private string raffleRewardName;
        private bool raffleHasError = false;
        private bool customStart = false;

        // Blackjack
        private List<BlackjackData> blackjackDatas;
        private string blackjackFeeName;
        private bool blackjackHasError = false;
        private string[] cardName = { "A", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "K", "Q", "J" };

        #region Configuration
        private static List<object> defaultRaffleStartTime()
        {
            List<object> thisList = new List<object>();
            thisList.Add("10:00");
            thisList.Add("20:30");

            return thisList;
        }

        protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }

        private void LoadVariables()
        {
            // General settings
            neededAuthLevel = Convert.ToInt32(GetConfig("General", "Authentication Level Required To Used Admin Command", "2"));
            raffleActive = Convert.ToBoolean(GetConfig("General", "Raffle Activated", "true"));

            // Raffle
            raffleUseServerTime = Convert.ToBoolean(GetConfig("Raffle", "Use Game Time? If False, It Will Use Real Time", true));
            List<object> objectList = (List<object>)GetConfig("Raffle", "Starting Time Of The Raffle (24-Hours Format)", defaultRaffleStartTime());
            raffleStartTime = new List<string>();
            foreach (object obj in objectList)
            {
                raffleStartTime.Add(Convert.ToString(obj));
            }
            raffleDaysSkip = Convert.ToInt32(GetConfig("Raffle", "Days To Skip After Raffle Is Ran On The Day", "0"));
            raffleJoin = Convert.ToString(GetConfig("Raffle", "Join Item (Item Shortname https://www.corrosionhour.com/rust-item-list/)", "scrap"));
            raffleJoinAmount = Convert.ToInt32(GetConfig("Raffle", "Amount Needed To Join", "20"));
            raffleReward = Convert.ToString(GetConfig("Raffle", "Reward Item (Item Shortname https://www.corrosionhour.com/rust-item-list/)", "supply.signal"));
            raffleRewardAmount = Convert.ToInt32(GetConfig("Raffle", "Reward Amount", "1"));
            rafflePlayersNeeded = Convert.ToInt32(GetConfig("Raffle", "How Many People Is Needed To Calculate The Winner", "3"));

            // Blackjack
            blackjackFee = Convert.ToString(GetConfig("Blackjack", "The Reward/Join Item (Item Shortname https://www.corrosionhour.com/rust-item-list/)", "scrap"));
            blackjackMinimumFee = Convert.ToInt32(GetConfig("Blackjack", "The Minimum Amount Required To Join The Blackjack", "5"));
            blackjackMultiplier = Convert.ToSingle(GetConfig("Blackjack", "The Multiplying Amount For When The User Wins The Blackjack", "2"));
            blackjackCooldown = Convert.ToInt32(GetConfig("Blackjack", "The Cooldown Amount Between Each Blackjack (Seconds)", "30"));
            blackjackTimeout = Convert.ToInt32(GetConfig("Blackjack", "The Timeout Time For The Blackjack If User Did Not Respond", "30"));

            if (!ConfigChanged)
            {
                return;
            }

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

        #region Messages
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                // General
                ["CannotUseCommand"] = "You Are Not Allowed To Used This Command!",

                // Short Name Item Not Found Error Text
                ["RaffleJoinNotFound"] = "Raffle Join Item '{0}' Short Name Not Found! Raffle Has Been Disabled!",
                ["RaffleRewardNotFound"] = "Raffle Reward Item '{0}' Short Name Not Found! Raffle Has Been Disabled!",
                ["BlackjackRewardNotFound"] = "Blackjack Reward/Join item '{0}' Short Name Not Found! Blackjack Has Been Disabled!",

                // Compensate Text
                ["Compensate"] = "{0} {1} Has Been Compensated Due To Disconnect!",

                // Raffle Text
                ["RaffleStart"] = "Raffle Time! Join With {0} {1} To Stand A Chance To Win {2} {3}. Join With /raffle",
                ["RaffleFail"] = "Insufficient Players Joined, {0} Will Be Refunded!",
                ["RaffleEnd"] = "Raffle Has Ended! Congratulations {0} For Winning The Raffle!",
                ["RaffleHaventStart"] = "The Raffle Has Not Started Yet! Next Raffle Begins In {0} Minutes.",
                ["RaffleAlreadyJoined"] = "You Already Joined The Raffle!",
                ["RaffleInsufficient"] = "Insufficient {0}! Please Make Sure You Have {1} {0} In Your Inventory!",
                ["RaffleJoinedPlayer"] = "Successfully Joined The Raffle!",
                ["RaffleJoinedGloabl"] = "{0} Has Joined The Raffle!",
                ["RaffleFailStart"] = "Insufficient Player Online, Raffle Will Not Start This Round!",
                ["RaffleCustomStart"] = "Raffle Was Force Started!",
                ["RaffleCustomFail"] = "Raffle Could Not Be Started Due To Active Raffle!",

                // Blackjack Text
                ["BlackjackNoArgs"] = "Blackjack Usage: /backjack <amount of {0}>",
                ["BlackjackArgNotNumber"] = "'/blackjack <amount>' Requires The Amount To Be A Number!",
                ["BlackjackArgChoose"] = "Choose Between '/blackjack hit' Or '/blackjack stand'",
                ["BlackjackMinimumFail"] = "Minimum Of {0} {1} Is Required To Play Blackjack!",
                ["BlackjackInsufficient"] = "Insufficient {0}! Please Make Sure You Have {1} {0} In Your Inventory!",
                ["BlackjackNoSession"] = "You Have No Blackjack Session Currently Running!",
                ["BlackjackUserCard"] = "Your Cards: {0}. Value: {1}.",
                ["BlackjackBotCard"] = "Bot Cards: {0}. Value: {1}.",
                ["BlackjackUserWon"] = "Congratulation, You Have Won {0} {1}!",
                ["BlackjackUserLost"] = "Aww, You Lost {0} {1}!",
                ["BlackjackUserTied"] = "Aww, You Tied And Got Back {0} {1}!",

            }, this, "en"); ;
        }
        #endregion

        #region Player Disconnect / Connect Handler
        private class DisconnectData
        {
            public ulong userId;
            public string itemShortName;
            public int amount;

            public DisconnectData(ulong userId, string shortName, int amount)
            {
                this.userId = userId;
                this.itemShortName = shortName;
                this.amount = amount;
            }
        }

        void OnUserDisconnected(IPlayer player)
        {
            // Check if user got join the raffle
            bool didJoinRaffle = false;
            ulong thisPlayerId = Convert.ToUInt64(player.Id);

            foreach (ulong playerId in joinedPlayersId)
            {
                if (playerId == thisPlayerId)
                {
                    // User is in the list
                    didJoinRaffle = true;
                }
            }

            if (didJoinRaffle)
            {
                // Create the data then add the data into the list to compensate them when they connect later on
                DisconnectData data = new DisconnectData(thisPlayerId, raffleJoinName, raffleJoinAmount);
                disconnectedPlayers.Add(data);

                // Remove them from the list
                joinedPlayersId.Remove(thisPlayerId);
            }

            bool didJoinBlackjack = false;
            BlackjackData thisData = null;
            
            foreach (BlackjackData bjData in blackjackDatas)
            {
                if (bjData.thisPlayer.userID == thisPlayerId)
                {
                    // User is in the list
                    didJoinBlackjack = true;
                    thisData = bjData;
                }
            }

            if (didJoinBlackjack)
            {
                // Create the data then add the data into the list to compensate them when they connect later on
                DisconnectData data = new DisconnectData(thisPlayerId, thisData.item, thisData.amount);
                disconnectedPlayers.Add(data);

                // Remove them from the list
                blackjackDatas.Remove(thisData);
            }
        }

        void OnUserConnected(IPlayer player)
        {
            // Check if user is in the list of disconnected player 
            bool playerInList = false;
            ulong thisPlayerId = Convert.ToUInt64(player.Id);
            DisconnectData thisPlayerData = null;

            foreach (DisconnectData data in disconnectedPlayers)
            {
                if (data.userId == thisPlayerId)
                {
                    playerInList = true;
                    thisPlayerData = data;
                }
            }

            // Player is in the list
            if (playerInList && thisPlayerData != null)
            {
                // Compensate the player
                ItemDefinition theItem = ItemManager.FindItemDefinition(thisPlayerData.itemShortName);
                Item item = ItemManager.CreateByItemID(theItem.itemid, thisPlayerData.amount);
                BasePlayer thisPlayer = player.Object as BasePlayer;
                thisPlayer.GiveItem(item);

                // Generate message
                string message = lang.GetMessage("Compensate", this, thisPlayer.UserIDString);
                thisPlayer.ChatMessage(string.Format(message, thisPlayerData.amount, theItem.displayName));

                // Remove user from list
                disconnectedPlayers.Remove(thisPlayerData);
            }
        }
        #endregion

        #region Initialization
        private void Init()
        {
            // Initialize the lists
            joinedPlayersId = new List<ulong>();
            disconnectedPlayers = new List<DisconnectData>();
            blackjackDatas = new List<BlackjackData>();

            // Load configuration
            LoadVariables();

            // Permission 
            permission.RegisterPermission("ultimategamble.start", this);

            #region Raffle
            // Check and set the cost name and reward name for raffle
            ItemDefinition join = ItemManager.FindItemDefinition(raffleJoin);
            ItemDefinition reward = ItemManager.FindItemDefinition(raffleReward);

            if (join == null)
            {
                string message = lang.GetMessage("RaffleJoinNotFound", this);
                PrintError(string.Format(message, raffleJoin));

                raffleHasError = true;
            }

            if (reward == null)
            {
                string message = lang.GetMessage("RaffleRewardNotFound", this);
                PrintError(string.Format(message, raffleReward));

                raffleHasError = true;
            }

            if (join != null && reward != null)
            {
                raffleJoinName = ItemManager.FindItemDefinition(raffleJoin).displayName.english;
                raffleRewardName = ItemManager.FindItemDefinition(raffleReward).displayName.english;
            }

            // Initialize the Raffle if there's no error
            if (!raffleHasError && raffleActive)
            {
                // Run it after 5min
                timer.Once(300, () =>
                {
                    NextRaffleTime();
                    StartRaffleTimer();
                });
            }
            #endregion

            #region Blackjack
            ItemDefinition fee = ItemManager.FindItemDefinition(blackjackFee);

            if (fee == null)
            {
                string message = lang.GetMessage("BlackjackRewardNotFound", this);
                PrintError(string.Format(message, blackjackFee));

                blackjackHasError = true;
            }
            else
            {
                blackjackFeeName = fee.displayName.english;
            }
            #endregion
        }

        private float SecondsTillTime(float time)
        {
            if (raffleUseServerTime)
            {
                // Using Time Curve since there is no way to determine the seconds per minute except for using the curve.
                // Create a reversing Time Curve
                AnimationCurve TimeCurve = TOD_Sky.Instance.Components.Time.TimeCurve;
                AnimationCurve reverseTimeCurve = new AnimationCurve();
                float thisAmount = 0;
                while (thisAmount <= 24)
                {
                    reverseTimeCurve.AddKey(TimeCurve.Evaluate(thisAmount), thisAmount);
                    thisAmount += 0.1f;
                    thisAmount = Convert.ToSingle(Math.Round(thisAmount, 1));
                }

                // Find the time diff between the time we want and the current game time
                float diff = reverseTimeCurve.Evaluate(time) - reverseTimeCurve.Evaluate(ConVar.Env.time);
                // Get the day length, this is auto in real time minutes
                float dayLength = TOD_Sky.Instance.Components.Time.DayLengthInMinutes;

                // If negative, it means that the time has already pass so just loop it around with the day length
                if (diff < 0)
                {
                    diff = 24 + diff;
                }

                if (time >= 0 && time <= ConVar.Env.time)
                {
                    diff += 24 * raffleDaysSkip;
                }

                // Find the multiplier that will convert the game time from that hour to our seconds
                // Take it as 
                // 60 minutes our time = 24 hours in game time
                // 2.5 minutes our time = 1 hour in game time
                // 150 seconds our time = 1 hour in game time <- this is what we need
                // Note: diff returns in hours format
                float multiplier = dayLength * 60 / 24;
                float seconds = diff * multiplier;
                return seconds; // This will return our seconds needed to reach the next time
            }
            else
            {
                // Convert the time to hours & minutes
                int hours = (int)time;
                int minutes = (int)((time - hours) * 60);

                DateTime currentTime = DateTime.Now;
                DateTime targetTime = new DateTime(0, 0, 0, hours, minutes, 0);

                int nowSeconds = currentTime.Hour * 60 * 60 + currentTime.Minute * 60 + currentTime.Second;
                int targetSeconds = targetTime.Second * 60 * 60 + targetTime.Minute * 60 + targetTime.Second;

                int diff = targetSeconds - nowSeconds;

                return diff; // Return out seconds needed to reach the next time
            }
        }
        #endregion

        #region Raffle
        // Randomly choose a player that has joined the raffle session
        private void NextRaffleTime()
        {
            float diff = -1;

            // Extract each start time
            foreach(string time in raffleStartTime)
            {
                // Split the time
                string[] splitedTime = time.Split(':');

                // Convert it to float
                float thisTime = float.Parse(splitedTime[0]);

                // Convert the minutes to hours
                if (splitedTime.Length > 1)
                {
                    float thisMinutes = float.Parse(splitedTime[1]) / 60;
                    thisTime += thisMinutes;
                }

                // Get current time
                float currentTime = ConVar.Env.time;

                // Find the diff
                float thisDiff = thisTime - currentTime;
                
                // Check if negative, if it is change it to positive and add 24 hours to it
                if (thisDiff < 0)
                {
                    thisDiff += 24;
                }

                // Store the time for the nearest to the time
                if (diff == -1)
                {
                    diff = thisDiff;
                    raffleNextStartTime = thisTime;
                }
                else
                {
                    // Check if the current diff is shorter than the previous diff
                    if (thisDiff < diff && thisDiff > 0.25)
                    {
                        diff = thisDiff;
                        raffleNextStartTime = thisTime;
                    }
                }
            }
        }

        private void StartRaffleTimer()
        {
            timer.Once(SecondsTillTime(raffleNextStartTime), () =>
            {
                StartRaffle();
            });
        }

        private void EndRaffleTimer()
        {
            timer.Once(30f, () =>
            {
                EndRaffle();
            });
        }

        private void StartRaffle()
        {
            isRaffleTime = true;

            // Log the raffle start
            Puts("Raffle Started!");

            // Cancel it if there's not enough player in the server
            if (BasePlayer.activePlayerList.Count < rafflePlayersNeeded)
            {
                string message = lang.GetMessage("RaffleFailStart", this);
                Puts(message);

                isRaffleTime = false;
                
                if (!customStart)
                {
                    NextRaffleTime();
                    StartRaffleTimer();
                }

                return;
            }

            EndRaffleTimer();

            // Give Instructions
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                // Generate message
                string message = lang.GetMessage("RaffleStart", this, player.UserIDString);
                player.ChatMessage(string.Format(message, raffleJoinAmount, raffleJoinName, raffleRewardAmount, raffleRewardName));
            }
        }
        
        private void EndRaffle()
        {
            isRaffleTime = false;

            if (!customStart)
            {
                NextRaffleTime();
                StartRaffleTimer();
            }

            // Check the player needed
            if (joinedPlayersId.Count <= rafflePlayersNeeded)
            {
                // Not enough players, return scrap
                foreach (ulong playerID in joinedPlayersId)
                {
                    BasePlayer player = BasePlayer.FindByID(playerID);
                    Item item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(raffleJoin).itemid, raffleRewardAmount);
                    player.GiveItem(item);

                    // Generate message
                    string message = lang.GetMessage("RaffleFail", this, player.UserIDString);
                    player.ChatMessage(string.Format(message, raffleJoinName));
                }

                // Clear the list
                joinedPlayersId.Clear();

                // Log that the raffle ended and no one won due to insufficient player
                Puts("Raffle Ended Due To Insufficient Players!");

                return;
            }
            else
            {
                // Randomly choose a person
                int random = UnityEngine.Random.Range(0, joinedPlayersId.Count - 1);

                // Give the winner a an award
                BasePlayer winner = BasePlayer.FindByID(joinedPlayersId[random]);
                Item item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(raffleReward).itemid);
                winner.GiveItem(item);

                // Annouce Winner
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    string message = lang.GetMessage("RaffleEnd", this, player.UserIDString);
                    player.ChatMessage(string.Format(message, winner.displayName));
                }

                // Clear the list
                joinedPlayersId.Clear();

                // Log that the raffle ended and someone won
                Puts("Raffle Ended And Someone Won It!");

                return;
            }
        }

        [ChatCommand("raffle")]
        private void RaffleCommand(BasePlayer player, string command, string[] args)
        {
            // Check if they're using the command even tho it haven't start
            if (!isRaffleTime)
            {
                // Get the time till the next raffle
                float secondsTillNext = SecondsTillTime(raffleNextStartTime);
                int minutesTillNext = Mathf.CeilToInt(secondsTillNext / 60);

                string message = lang.GetMessage("RaffleHaventStart", this, player.UserIDString);
                player.ChatMessage(string.Format(message, minutesTillNext.ToString()));
                return;
            }

            // Check if they already joined
            if (joinedPlayersId.Contains(player.userID))
            {
                string message = lang.GetMessage("RaffleAlreadyJoined", this, player.UserIDString);
                player.ChatMessage(message);

                return;
            }

            // Check if they have the item to join in their inventory
            int itemId = ItemManager.FindItemDefinition(raffleJoin).itemid;
            int itemAmount = player.inventory.GetAmount(itemId);
            
            if (itemAmount < raffleRewardAmount)
            {
                string message = lang.GetMessage("RaffleInsufficient", this, player.UserIDString);
                player.ChatMessage(string.Format(message, raffleJoinName, raffleJoinAmount));

                return;
            }

            // By right here player should meet all the requirements
            // Add player to the list of players and remove their scrap
            joinedPlayersId.Add(player.userID);

            player.inventory.Take(null, itemId, raffleJoinAmount);

            // Successful message
            foreach (BasePlayer otherPlayers in BasePlayer.activePlayerList)
            {
                if (otherPlayers.userID == player.userID)
                {
                    string message = lang.GetMessage("RaffleJoinedPlayer", this, otherPlayers.UserIDString);
                    otherPlayers.ChatMessage(message);
                }
                else
                {
                    string message = lang.GetMessage("RaffleJoinedGlobal", this, otherPlayers.UserIDString);
                    otherPlayers.ChatMessage(string.Format(message, player.displayName));
                }
            }
        }

        [ChatCommand("raffle.start")]
        private void RaffleStartCommand(BasePlayer player, string command, string[] args)
        {
            IPlayer thisPlayer = player.IPlayer;

            // This command is to allow admin to straight start the raffle if there is no raffle currently running
            if (player.net.connection.authLevel < neededAuthLevel && !thisPlayer.HasPermission("ultimategamble.start"))
            {
                string message = lang.GetMessage("CannotUseCommand", this, player.UserIDString);
                player.ChatMessage(message);
                return;
            }

            // Check if there's a raffle currently running
            if (isRaffleTime)
            {
                string thisMessage = lang.GetMessage("RaffleCustomFail", this, player.UserIDString);
                player.ChatMessage(thisMessage);
            }
            else
            {
                // Here, user has the permission to use this command.
                customStart = true;

                // Send the user that uses it a message of confirmation
                string thisMessage = lang.GetMessage("RaffleCustomStart", this, player.UserIDString);
                player.ChatMessage(thisMessage);

                // Log in the console
                Puts("Raffle Was Force Started By " + player.displayName + "!");

                // Start the raffle
                StartRaffle();
            }
        }
        #endregion

        #region Blackjack
        // A player vs AI kind, typical 21
        // Class storer for blackjack data
        private class BlackjackData
        {
            public BasePlayer thisPlayer;
            public bool isActive = false;
            public int myCardValue = 0;
            public List<string> mycard;
            public int aiCardValue = 0;
            public List<string> aiCard;
            public float endTime;
            public int amount;
            public string item;
        }

        [ChatCommand("blackjack")]
        private void Blackjack(BasePlayer player, string command, string[] args)
        {
            // This command is split into 3 parts based on the args
            // 1: /blackjack <amount> 
            // 2: /blackjack hit
            // 3: /blackjack stand
            // TODO: Store and check if player hit already then disconnect. If they did then don't refund them.

            // Check if args was not specified or there was more than one args
            if (blackjackHasError)
            {
                return;
            }

            if (args == null || args.Length != 1)
            {
                // Display an error to user
                string message = lang.GetMessage("BlackjackNoArgs", this, player.UserIDString);
                player.ChatMessage(string.Format(message, blackjackFeeName));

                return;
            }

            if (args[0].ToLower() == "hit")
            {
                // Hit
                // Check if user has a datastored
                bool hasData = false;
                BlackjackData thisData = null;

                foreach (BlackjackData data in blackjackDatas)
                {
                    if (data.thisPlayer == player)
                    {
                        hasData = true;
                        thisData = data;
                    }
                }

                if (!hasData)
                {
                    // Display an error
                    string message = lang.GetMessage("BlackjackNoSession", this, player.UserIDString);
                    player.ChatMessage(message);

                    return;
                }

                // So by right here player should have a data stored already means that they already used /blackjack <amount>
                // HIT
                // User's card
                int userCardValue = thisData.myCardValue;
                List<string> userCard = thisData.mycard;

                // Choose a random card and add to the list
                int random = UnityEngine.Random.Range(1, cardName.Length);
                userCardValue = DetermineCardValue(userCard, cardName[random]);
                userCard.Add(cardName[random]);

                // Store then this send a message to the user
                foreach (BlackjackData data in blackjackDatas)
                {
                    if (data.thisPlayer == player)
                    {
                        data.myCardValue = userCardValue;
                        data.mycard = userCard;
                        data.endTime = Time.time;
                        thisData = data;
                    }
                }

                // Check if the value is greater than 21
                if (thisData.myCardValue >= 21 || thisData.mycard.Count >= 5)
                {
                    // End it
                    EndBlackjack(player, thisData);
                }
                else
                {
                    // Send it to user
                    string usermessage = lang.GetMessage("BlackjackUserCard", this, player.UserIDString);
                    player.ChatMessage(string.Format(usermessage, string.Join(", ", thisData.mycard.ToArray()), thisData.myCardValue));

                    string botmesage = lang.GetMessage("BlackjackBotCard", this, player.UserIDString);
                    player.ChatMessage(string.Format(botmesage, string.Join(", ", thisData.aiCard.ToArray()), thisData.aiCardValue));

                    string instruction = lang.GetMessage("BlackjackArgChoose", this, player.UserIDString);
                    player.ChatMessage(instruction);

                    // Start a timer
                    timer.Once(blackjackTimeout, () =>
                    {
                        bool hasUserData = false;
                        BlackjackData currentData = null;

                        foreach (BlackjackData data in blackjackDatas)
                        {
                            if (data.thisPlayer == player)
                            {
                                hasUserData = true;
                                currentData = data;
                            }
                        }

                        if (hasUserData)
                        {
                            if ((Time.time - currentData.endTime) >= blackjackTimeout)
                            {
                                EndBlackjack(player, currentData);
                            }
                        }
                    });
                }
            }
            else if (args[0].ToLower() == "stand")
            {
                // Stand
                // Check if user has a datastored
                bool hasData = false;
                BlackjackData thisData = null;

                foreach (BlackjackData data in blackjackDatas)
                {
                    if (data.thisPlayer == player)
                    {
                        hasData = true;
                        thisData = data;
                    }
                }

                if (!hasData)
                {
                    // Display an error
                    string message = lang.GetMessage("BlackjackNoSession", this, player.UserIDString);
                    player.ChatMessage(message);

                    return;
                }

                // End it
                EndBlackjack(player, thisData);
            }
            else
            {
                // Check if the arg can be converted to amount
                int amount;
                bool tryConvert = int.TryParse(args[0], out amount);

                if (!tryConvert)
                {
                    // Can't convert the string to int so the arg wasn't a number
                    // Display error
                    // Check if they have a blackjack currently running
                    bool hasBjRunning = false;

                    foreach (BlackjackData bjData in blackjackDatas)
                    {
                        if (bjData.thisPlayer == player && bjData.isActive)
                        {
                            hasBjRunning = true;
                        }
                    }

                    if (hasBjRunning)
                    {
                        // Bj was running so by right they should send hit or stand
                        string message = lang.GetMessage("BlackjackArgChoose", this, player.UserIDString);
                        player.ChatMessage(message);

                        return;
                    }
                    else
                    {
                        // Bj was not running so by right they should enter a value
                        string message = lang.GetMessage("BlackjackArgNotNumber", this, player.UserIDString);
                        player.ChatMessage(message);

                        return;
                    }
                }
                else
                {
                    // Check if the input amount meet the minimum requirement
                    if (amount < blackjackMinimumFee)
                    {
                        // Amount did not meet the minimum
                        string message = lang.GetMessage("BlackjackMinimumFail", this, player.UserIDString);
                        player.ChatMessage(string.Format(message, blackjackMinimumFee, blackjackFeeName));

                        return;
                    }

                    // Check if user already joined the blackjack
                    bool joined = false;

                    foreach (BlackjackData bjData in blackjackDatas)
                    {
                        if (bjData.thisPlayer == player && bjData.isActive)
                        {
                            joined = true;
                        }
                        else if (bjData.thisPlayer == player && !bjData.isActive)
                        {
                            // Cooldown...
                            string message = lang.GetMessage("BlackjackCooldown", this, player.UserIDString);
                            player.ChatMessage(string.Format(message)); //TODO
                        }
                    }

                    if (joined)
                    {
                        // User already joined so just give them an error message and end it 
                        string message = lang.GetMessage("BlackjackArgChoose", this, player.UserIDString);
                        player.ChatMessage(message);

                        return;
                    }

                    // Check if user has the required stuff in their inventory to join
                    int itemId = ItemManager.FindItemDefinition(blackjackFee).itemid;
                    int itemAmount = player.inventory.GetAmount(itemId);

                    if (itemAmount < amount)
                    {
                        string message = lang.GetMessage("BlackjackInsufficient", this, player.UserIDString);
                        player.ChatMessage(string.Format(message, blackjackFeeName, amount));

                        return;
                    }

                    // By right here, they should meet all the requirements
                    // Create the data
                    BlackjackData thisData = new BlackjackData();
                    thisData.thisPlayer = player;
                    thisData.isActive = true;

                    // Retrieve the fee
                    player.inventory.Take(null, itemId, amount);

                    // Start the blackjack interaction between user and an ai
                    string[] cardName = { "A", "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "K", "Q", "J" };

                    // Bot card
                    int botCardValue = 0;
                    List<string> botCard = new List<string>();

                    // Run a loop twice since they can only get 2 card
                    for (int i = 0; i < 2; i++)
                    {
                        int random = UnityEngine.Random.Range(1, cardName.Length);
                        botCardValue = DetermineCardValue(botCard, cardName[random]);
                        botCard.Add(cardName[random]);
                    }

                    // User's card
                    int userCardValue = 0;
                    List<string> userCard = new List<string>();
                    
                    // Run a loop twice since they can only get 2 card
                    for (int i = 0; i < 2; i++)
                    {
                        int random = UnityEngine.Random.Range(1, cardName.Length);
                        userCardValue = DetermineCardValue(userCard, cardName[random]);
                        userCard.Add(cardName[random]);
                    }

                    // Store the data
                    thisData.aiCard = botCard;
                    thisData.aiCardValue = botCardValue;
                    thisData.mycard = userCard;
                    thisData.myCardValue = userCardValue;
                    thisData.endTime = Time.time;
                    thisData.amount = amount;
                    thisData.item = blackjackFee;
                    blackjackDatas.Add(thisData);

                    // Send a message to user
                    string usermessage = lang.GetMessage("BlackjackUserCard", this, player.UserIDString);
                    player.ChatMessage(string.Format(usermessage, string.Join(", ", userCard.ToArray()), userCardValue));

                    string botmesage = lang.GetMessage("BlackjackBotCard", this, player.UserIDString);
                    player.ChatMessage(string.Format(botmesage, string.Join(", ", botCard.ToArray()), botCardValue));

                    string instruction = lang.GetMessage("BlackjackArgChoose", this, player.UserIDString);
                    player.ChatMessage(instruction);

                    // Start a timer
                    timer.Once(blackjackTimeout, () =>
                    {
                        bool hasUserData = false;
                        BlackjackData currentData = null;

                        foreach (BlackjackData data in blackjackDatas)
                        {
                            if (data.thisPlayer == player)
                            {
                                hasUserData = true;
                                currentData = data;
                            }
                        }

                        if (hasUserData)
                        {
                            if ((Time.time - currentData.endTime) >= blackjackTimeout)
                            {
                                EndBlackjack(player, currentData);
                            }
                        }
                    });
                }
            }
        }

        private void EndBlackjack(BasePlayer player, BlackjackData theData)
        {
            BlackjackData baseData = theData;
            // Check if the player card is below 21 
            if (theData.myCardValue < 21 && theData.mycard.Count < 5)
            {
                // Less than 21, so there's a chance that the bot value is greater
                // Run a loop if the bot's value is less than 17
                for (int i = 0; i < 5; i++)
                {
                    if (theData.aiCardValue < 17 && theData.aiCard.Count != 5)
                    {
                        // Bot card
                        int botCardValue = theData.aiCardValue;
                        List<string> botCard = theData.aiCard;

                        // Run a loop twice since they can only get 2 card
                        int random = UnityEngine.Random.Range(1, cardName.Length);
                        botCardValue = DetermineCardValue(botCard, cardName[random]);
                        botCard.Add(cardName[random]);

                        // Update the data into the data
                        theData.aiCardValue = botCardValue;
                        theData.aiCard = botCard;
                    }
                    else
                    {
                        break;
                    }
                }

                if (theData.aiCardValue > 21)
                {
                    // User wins
                    BlackjackEndMessage(player, theData, 1);
                }
                else if (theData.aiCardValue == 21)
                {
                    // Bot wins
                    BlackjackEndMessage(player, theData, 2);
                }
                else if (theData.aiCardValue < 21 && theData.aiCard.Count == 5)
                {
                    // Bot wins
                    BlackjackEndMessage(player, theData, 2);
                }
                else
                {
                    // Check if the user's card is greater than the bot's card
                    if (theData.myCardValue > theData.aiCardValue)
                    {
                        // User wins
                        BlackjackEndMessage(player, theData, 1);
                    }
                    else if (theData.myCardValue < theData.aiCardValue)
                    {
                        // Bot wins
                        BlackjackEndMessage(player, theData, 2);
                    }
                    else
                    {
                        // Tie
                        BlackjackEndMessage(player, theData, 3);
                    }
                }
            }
            else if (theData.myCardValue == 21)
            {
                // Player wins
                BlackjackEndMessage(player, theData, 1);
            }
            else if  (theData.mycard.Count == 5 && theData.myCardValue < 21)
            {
                // Player wins
                BlackjackEndMessage(player, theData, 1);
            }
            else
            {
                // Bot wins
                BlackjackEndMessage(player, theData, 2);
            }

            blackjackDatas.Remove(baseData);
        }

        private void BlackjackEndMessage(BasePlayer player, BlackjackData theData, int option)
        {
            // Display the base message of the cards
            string usermessage = lang.GetMessage("BlackjackUserCard", this, player.UserIDString);
            player.ChatMessage(string.Format(usermessage, string.Join(", ", theData.mycard.ToArray()), theData.myCardValue));

            string botmesage = lang.GetMessage("BlackjackBotCard", this, player.UserIDString);
            player.ChatMessage(string.Format(botmesage, string.Join(", ", theData.aiCard.ToArray()), theData.aiCardValue));

            string message = "";
            switch (option)
            {
                case 1:
                    // User won
                    Item item = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(theData.item).itemid, (int)(theData.amount * blackjackMultiplier));
                    player.GiveItem(item);

                    // Display user message
                    message = lang.GetMessage("BlackjackUserWon", this, player.UserIDString);
                    player.ChatMessage(string.Format(message, (theData.amount * blackjackMultiplier).ToString(), ItemManager.FindItemDefinition(theData.item).displayName.english));

                    break;
                case 2:
                    // User Lost
                    // Display user message
                    message = lang.GetMessage("BlackjackUserLost", this, player.UserIDString);
                    player.ChatMessage(string.Format(message, theData.amount.ToString(), ItemManager.FindItemDefinition(theData.item).displayName.english));

                    break;
                case 3:
                    // User tied
                    Item thisItem = ItemManager.CreateByItemID(ItemManager.FindItemDefinition(theData.item).itemid, theData.amount);
                    player.GiveItem(thisItem);

                    // Display user message
                    message = lang.GetMessage("BlackjackUserTied", this, player.UserIDString);
                    player.ChatMessage(string.Format(message, theData.amount.ToString(), ItemManager.FindItemDefinition(theData.item).displayName.english));
                    break;

            }
        }

        private int DetermineCardValue(List<string> theCard, string thisCard)
        {
            // Add the card to all card list
            List<string> allCard = new List<string>(theCard);
            allCard.Add(thisCard);

            // Calculate the amount
            int totalAmount = 0;

            List<string> clonedCard = new List<string>(allCard);
            List<string> arrangedCard = new List<string>();
            string[] cardList = { "1", "2", "3", "4", "5", "6", "7", "8", "9", "10", "K", "Q", "J", "A" };

            // Rearrange the card
            for (int i = 0; i < allCard.Count; i++)
            {
                // Loop the card list
                foreach (string card in cardList)
                {
                    // If contain the add the arranged one, remove the cloned one and stop it
                    if (clonedCard.Contains(card))
                    {
                        arrangedCard.Add(card);
                        clonedCard.Remove(card);
                        break;
                    }
                }
            }

            foreach (string cards in arrangedCard)
            {
                // Determine the card value
                switch (cards)
                {
                    case "A":
                        if (totalAmount >= 10)
                        {
                            // Change the value to 1
                            totalAmount += 1;
                        }
                        else
                        {
                            // Keep A as 11
                            totalAmount += 11;
                        }

                        break;
                    case "1":
                        totalAmount += 1;
                        break;
                    case "2":
                        totalAmount += 2;
                        break;
                    case "3":
                        totalAmount += 3;
                        break;
                    case "4":
                        totalAmount += 4;
                        break;
                    case "5":
                        totalAmount += 5;
                        break;
                    case "6":
                        totalAmount += 6;
                        break;
                    case "7":
                        totalAmount += 7;
                        break;
                    case "8":
                        totalAmount += 8;
                        break;
                    case "9":
                        totalAmount += 9;
                        break;
                    case "10":
                    case "K":
                    case "Q":
                    case "J":
                        totalAmount += 10;
                        break;
                }
            }
            
            return totalAmount;
        }
        #endregion

        #region Slots
        // A slot system no idea on how to get the specific icon in.
        #endregion

        #region Lottery
        // User select a random number and the system generate a random number, if both number are the same then they win
        #endregion

        #region HOT
        // Heads or tails maybeeee? 50/50 win rate?
        #endregion
    }
}
