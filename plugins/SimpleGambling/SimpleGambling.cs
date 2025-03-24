using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

/*
 * This update 1.4.0
 * Updated to Covalence plugin
 * Added admin permission
 * Commands are both chat and console commands.
 *
 * TODO
 * Add Server Rewards Support, Add UI option, Add HumanNPC Support.
 */

namespace Oxide.Plugins
{
    [Info("Simple Gambling", "Khan", "1.4.0")]
    [Description("Allows admins to create item gambling")]
    class SimpleGambling : CovalencePlugin
    {
        private const string Use = "simplegambling.allowed";
        private const string Admin = "simplegambling.admin";
        ConfigData configData;
        StoredData storedData;

        #region Classes

        class StoredData
        {
            public List<Gamble> gambles;
            public List<Preserved> preserveds;
            public List<Reward> rewards;

            public StoredData()
            {
                gambles = new List<Gamble>();
                preserveds = new List<Preserved>();
                rewards = new List<Reward>();
            }
        }

        class ConfigData
        {
            public bool preservePot;
            public bool showWinners;
        }

        public class Reward
        {
            public ulong id;
            public string item;
            public int quantity;

            public Reward(ulong id, string item, int quantity)
            {
                this.id = id;
                this.item = item;
                this.quantity = quantity;
            }
        }

        public class Gamble
        {
            public string name, item;
            public int numbers, itemAmount;
            public List<Bets> bets;
            public int preservedPot;

            public Gamble(string name, string item, int itemAmount, int numbers, int preservedPot)
            {
                this.name = name;
                this.item = item;
                this.itemAmount = itemAmount;
                this.numbers = numbers;
                this.preservedPot = preservedPot;
                bets = new List<Bets>();
            }
        }

        public class Bets
        {
            public ulong player;
            public int number;

            public Bets(ulong player, int number)
            {
                this.player = player;
                this.number = number;
            }
        }

        public class Preserved
        {
            public int amount;
            public string itemName;

            public Preserved(int amount, string itemName)
            {
                this.amount = amount;
                this.itemName = itemName;
            }
        }

        #endregion

        #region Config+Data+Lang

        private void LoadAll()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("SimpleGamblingData");
            configData = Config.ReadObject<ConfigData>();
        }

        private void SaveAll()
        {
            Config.WriteObject(configData, true);
            Interface.Oxide.DataFileSystem.WriteObject("SimpleGamblingData", storedData);
        }

        protected override void LoadDefaultConfig()//called the first time 
        {
            ConfigData defaultConfig = new ConfigData();
            Config.WriteObject(defaultConfig, true);
        }

        private void Init()
        {
            permission.RegisterPermission(Use, this);
            permission.RegisterPermission( Admin, this);
            LoadAll();
        }

        private string Lang(string key, string id, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private new void LoadDefaultMessages()
        {
            //English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PlayerHelp"] = "type \"/gamble current\" to see current gambles or \"gamble reward\" to redeem rewards",
                ["Winners"] = "The winners are: ",
                ["AdminHelp"] = "type \"/gamble set\" or \"/gamble finish\" or \"/gamble config\" for help",
                ["ConfigHelp"] = "use \"/gamble Config Winners\" and TRUE or FALSE to show winner's names on chat when a gamble finishes\n\nuse \"/gamble Config PreservePot\" and TRUE or FALSE to preserve all betted items and add them to the next gamble",
                ["WinnersValues"] = "INVALID VALUE\nshould winner's names be printed on chat?\nvalues TRUE or FALSE",
                ["PreserveValues"] = "INVALID VALUE\nshould the pot be preserved for the next bet if there is no winner?\nvalues TRUE or FALSE",
                ["ConfigChanged"] = "{0} set to {1}",
                ["ItemNotFound"] = "{0} is not a valid item shortname",
                ["GambleNotFound"] = "gamble not found",
                ["DontHavePermission"] = "<color=#00ffffff>[Simple Gambling]</color> You do not have permission to use this command",
                ["NewGamble"] = "A gambling has just started, type \"/gamble play {0} \" and a number from 1 to {1}, to bet {2} {3}",
                ["CurrentGambles"] = "{0} gambling haves {1} items on the pool, type \"/gamble play {2} \" and a number from 1 to {3} to bet {4} {5}",
                ["NoGambles"] = "There are no current gambles",
                ["ValidNumbers"] = "Valid numbers from 1 to {0}",
                ["InvalidItemAmount"] = "Item amount can not be greater than the item stack limit or less than 1",
                ["AlreadyBet"] = "You have already bet",
                ["GamblingFinished"] = "{0} gambling finished! number {1} won, the loot will be shared between {2} players",
                ["GamblingFinishedNoWinner"] = "{0} gambling finished but no one won! the number was {1}",
                ["ExistingGamble"] = "There is already a gamble with that name",
                ["GambleCreated"] = "{0} gamble created",
                ["BetPlaced"] = "your bet in {0} has been placed",
                ["NotEnoughItems"] = "Yo do not have enough items to bet",
                ["InvalidNumbers"] = "numbers should be greater than 1",
                ["SetHelp"] = "\"/gamble Set gambleName itemName itemAmount numbers\"\ngambleName can be anything but just use 1 word\nitemName must be the shortname of the item\nitemAmount is the amount of items you need to have to play\nnumbers is the amount of numbers you can bet on\n\nFor example: \"/gamble set Rockets ammo.rocket.basic 3 20\"\nplayers will be able to bet 3 rocket on numbers from 1 to 20",
                ["FinishHelp"] = "use \"/gamble Finish gambleName\" to finish a gambling\nFor example \"/gamble finish Rockets\"",
                ["RewardReceived"] = "Rewards Received!",
                ["RewardInventorySpace"] = "You do not have enough space in your inventory",
                ["NoRewards"] = "There are no available rewards"
            }, this);
        }

        #endregion

        #region Helper Methods
        
        private void PrintToChat(string format, params object[] args)
        {
            if (BasePlayer.activePlayerList.Count < 1)
                return;
            ConsoleNetwork.BroadcastToAllClients("chat.add", (object) 2, (object) 0, args.Length != 0 ? (object) string.Format(format, args) : (object) format);
        }

        #endregion

        #region Logic

        private string PlayerEqualNull(IPlayer player, string langKey, params object[] args)
        {
            return player == null ? string.Format(lang.GetMessage(langKey, this), args) : Lang(langKey, player.Id, args);
        }

        private void ReplyOrPutLang(IPlayer player, string langKey, params object[] args)
        {
            if(player==null)
                Puts(string.Format(lang.GetMessage(langKey, this), args));
            else
                player.Reply( Lang(langKey, player.Id, args));
        }

        private void ReplyOrPutString(IPlayer player, string message)
        {
            if (player == null)
                Puts(message);
            else
                player.Reply(message);
        }

        private int GetGambleIndex(string gambleName)//-1 if it does not exist
        {
            for (int i = 0; i < storedData.gambles.Count; i++)
            {
                if (storedData.gambles[i].name.ToLower() == gambleName.ToLower())
                    return i;//return gamble index
            }

            return -1;
        }

        private bool DoesItemExist(string name)//does this item exist?
        {
            if (ItemManager.FindItemDefinition(name) != null)
                return true;

            return false;
        }

        private string GetItemName(string shortname)
        {
            return ItemManager.FindItemDefinition(shortname).displayName.english;
        }

        private string TryToBet(BasePlayer player, string name, string number)
        {
            if (storedData.gambles.Count == 0)//no gambles
                return Lang("NoGambles", player.UserIDString);

            int index = GetGambleIndex(name);

            if (index == -1)
                return Lang("GambleNotFound", player.UserIDString);

            int num = 0;

            if (!(int.TryParse(number, out num)) || num < 1 || num > storedData.gambles[index].numbers)//is number invalid?
                return Lang("ValidNumbers", player.UserIDString, storedData.gambles[index].numbers.ToString());

            for (int j = 0; j < storedData.gambles[index].bets.Count; j++)//search this name in bets list
            {
                if (storedData.gambles[index].bets[j].player == player.userID)//name found
                    return Lang("AlreadyBet", player.UserIDString);//can't bet
            }

            Item[] items = player.inventory.AllItems();//stores all items

            for (int k = 0; k < items.Length; k++)//search for the betted item
            {
                if (items[k].info.shortname != storedData.gambles[index].item || items[k].amount < storedData.gambles[index].itemAmount)//if this isn't the item or isn't enough
                    continue;//keep searching

                if (items[k].amount == storedData.gambles[index].itemAmount)//if exact amount
                    items[k].RemoveFromContainer();

                else//haves more
                {
                    items[k].amount -= storedData.gambles[index].itemAmount;//just subtract
                    items[k].MarkDirty();//update UI
                }

                storedData.gambles[index].bets.Add(new Bets(player.userID, num));//add this bet to the gamble
                return Lang("BetPlaced", player.UserIDString, name);
            }

            return Lang("NotEnoughItems", player.UserIDString);
        }

        private string TryToSet(string name, string item, string itemA, string number, IPlayer player = null)
        {
            if (!DoesItemExist(item))
                return PlayerEqualNull(player, "ItemNotFound", item);
                
            int itemAmount;

            if (!int.TryParse(itemA, out itemAmount) || itemAmount > ItemManager.FindItemDefinition(item).stackable || itemAmount <= 0)//is item amount invalid?
                return PlayerEqualNull(player, "InvalidItemAmount");

            int numbers;

            if (!int.TryParse(number, out numbers) || numbers <= 1)//is gamble numbers invalid?
                return PlayerEqualNull(player, "InvalidNumbers");

            if (GetGambleIndex(name) != -1)//if gamble already exists
                return PlayerEqualNull(player, "ExistingGamble");
            

            CreateGamble(name, item, itemAmount, numbers, player);
            return PlayerEqualNull(player, "GambleCreated", name);
        }

        private void CreateGamble(string name, string iName, int iAmount, int numbers, IPlayer player = null)
        {
            storedData.gambles.Add(new Gamble(name, iName, iAmount, numbers, GetAndDeletePreservedPot(iName)));//creates gamble and stores it in data

            PrintToChat(PlayerEqualNull(player, "NewGamble", name, numbers.ToString(), iAmount.ToString(), GetItemName(iName)));
        }

        private int GetAndDeletePreservedPot(string itemName)
        {
            if (!configData.preservePot)//if config disabled
                return 0;

            int preserved = 0;
            for (int i = 0; i < storedData.preserveds.Count; i++)//search for this item
            {
                if (storedData.preserveds[i].itemName == itemName)//found
                {
                    preserved = storedData.preserveds[i].amount;//save amount
                    storedData.preserveds.RemoveAt(i);//delete preserved pot
                    break;
                }
            }

            return preserved;
        }

        private void AddToPreservedPot(string itemName, int amount)//called when gamble finishes with no winner
        {
            if (!configData.preservePot || amount == 0)//if config disabled or no bets
                return;

            for (int i = 0; i < storedData.preserveds.Count; i++)//search for this item
            {
                if (storedData.preserveds[i].itemName == itemName)//found
                {
                    storedData.preserveds[i].amount += amount;//add more 
                    return;
                }
            }

            storedData.preserveds.Add(new Preserved(amount, itemName));//if wasn't found then create it
        }

        private void TryFinishGamble(string name, IPlayer player = null)
        {
            int index = GetGambleIndex(name);

            if (index == -1)//gamble not found
            {
                ReplyOrPutLang(player, "GambleNotFound");
                return;
            }

            Gamble gamble= storedData.gambles[index];
            int winnerNumber = UnityEngine.Random.Range(1, gamble.numbers + 1);

            List<ulong> winners = new List<ulong>();
            for (int i = 0; i < gamble.bets.Count; i++)//add all matching numbers to winners list
            {
                if (gamble.bets[i].number == winnerNumber)
                    winners.Add(gamble.bets[i].player);
            }


            if (winners.Count != 0)
            {
                PrintToChat(PlayerEqualNull(player,"GamblingFinished", name, winnerNumber, winners.Count));

                int eachAmount = (gamble.itemAmount * gamble.bets.Count + gamble.preservedPot) / winners.Count;
                foreach (ulong win in winners)
                    storedData.rewards.Add(new Reward(win, gamble.item, gamble.itemAmount));

                

                if (configData.showWinners)
                {
                    PrintToChat(PlayerEqualNull(player, "Winners"));
                    string winnersString = "";

                    foreach (ulong win in winners)
                        winnersString += BasePlayer.Find(win.ToString()).displayName + ", ";


                    PrintToChat(winnersString.Remove(winnersString.Length - 2));//deletes the last comma+space and prints
                }

            }
            else//no one won
            {
                AddToPreservedPot(gamble.item, gamble.itemAmount * gamble.bets.Count + gamble.preservedPot);

                PrintToChat(PlayerEqualNull(player, "GamblingFinishedNoWinner", name, winnerNumber));
            }

            Puts("Gamble " + gamble.name + " finished");
            storedData.gambles.RemoveAt(index);
        }

        private void ShowCurrentGambles(IPlayer player)
        {
            if (storedData.gambles.Count == 0)
            {
                ReplyOrPutLang(player, "NoGambles");
                return;
            }

            for (int i = 0; i < storedData.gambles.Count; i++)
            {
                Gamble gamble = storedData.gambles[i];
                ReplyOrPutLang(player, "CurrentGambles", gamble.name, (gamble.bets.Count * gamble.itemAmount + gamble.preservedPot).ToString(), gamble.name, gamble.numbers, gamble.itemAmount, GetItemName(gamble.item));
            }
        }

        private string TryGetReward(BasePlayer player)
        {
            for (int i = 0; i < storedData.rewards.Count; i++)
            {
                if (storedData.rewards[i].id != player.userID)
                    continue;

                if (player.inventory.GiveItem(ItemManager.CreateByName(storedData.rewards[i].item, storedData.rewards[i].quantity)))
                {
                    storedData.rewards.RemoveAt(i);
                    return Lang("RewardReceived", player.UserIDString);
                }
                else
                    return Lang("RewardInventorySpace", player.UserIDString);
            }
            
            return Lang("NoRewards", player.UserIDString);
        }

        private void TryChangeConfig(string variable, string newValue, IPlayer player = null)
        {
            switch (variable)
            {
                case "winners":
                    bool parsedWinners;
                    if (bool.TryParse(newValue, out parsedWinners))
                    {
                        configData.showWinners = parsedWinners;
                        ReplyOrPutLang(player, "ConfigChanged", variable, parsedWinners.ToString());
                        return;
                    }

                    ReplyOrPutLang(player, "WinnersValues");
                    break;


                case "preservepot":
                    bool parsedPot;
                    if (bool.TryParse(newValue, out parsedPot))
                    {
                        configData.preservePot = parsedPot;
                        ReplyOrPutLang(player, "ConfigChanged", variable, parsedPot.ToString());
                        return;
                    }

                    ReplyOrPutLang(player, "PreserveValues");
                    break;


                default:
                    ReplyOrPutLang(player, "ConfigHelp");
                    break;
            }
        }

        #endregion

        #region commands

        [Command("gamble")]  // if player has perm "allowed" they get access to /gamble play, /gamble current, /gamble reward
        private void CmdGamble(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission("simplegambling.allowed") && !player.HasPermission("simplegambling.admin"))
            {
                player.Reply( Lang("DontHavePermission", player.Id));
                return;
            }

            if (args.Length < 1)
            {
                if(player.HasPermission("simplegambling.admin"))
                    player.Reply( Lang("AdminHelp", player.Id));
                else
                    player.Reply( Lang("PlayerHelp", player.Id));
                return;
            }

            if (args[0].ToLower() == "play")
            {
                if (args.Length == 3)
                {
                    player.Reply( TryToBet(player.Object as BasePlayer, args[1], args[2]));
                    SaveAll();
                }
                else
                    ShowCurrentGambles(player);
                
                return;
            }

            if (args[0].ToLower() == "current")
            {
                ShowCurrentGambles(player);
                return;
            }

            if (args[0].ToLower() == "reward")
            {
                player.Reply( TryGetReward(player.Object as BasePlayer));
                return;
            }

            if (player.HasPermission("simplegambling.admin"))
                Commands(player, args);
        }

        private void Commands(IPlayer player, string[] args)
        {
            switch (args[0].ToLower())
            {
                case "set":
                    if (args.Length == 5)
                    {
                        ReplyOrPutString(player, TryToSet(args[1], args[2].ToLower(), args[3], args[4], player));
                        break;
                    }

                    ReplyOrPutLang(player, "SetHelp");
                    break;


                case "finish":
                    if (args.Length == 2)
                    {
                        TryFinishGamble(args[1], player);
                        break;
                    }

                    ReplyOrPutLang(player, "FinishHelp");
                    break;

                case "current":
                    ShowCurrentGambles(player);
                    break;


                case "config":
                    if (args.Length == 3)
                    {
                        TryChangeConfig(args[1].ToLower(), args[2], player);

                        break;
                    }

                    ReplyOrPutLang(player, "ConfigHelp");
                    break;

                case "resetpot":
                    storedData.preserveds = new List<Preserved>();

                    break;
            }

            SaveAll();
        }

        #endregion

    }
}
