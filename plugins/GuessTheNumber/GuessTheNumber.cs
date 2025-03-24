using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using System.Text;
using UnityEngine;
using System.Linq;
using Oxide.Game.Rust.Cui;
using Oxide.Core;

namespace Oxide.Plugins
{
    /*==============================================================================================================
    *    
    *    THANKS to Dora the original creator of this plugin
    *    THANKS to redBDGR the previous maintainer of this plugin upto v2.0.2
    *    THANKS to Krungh Crow the previous maintainer of this plugin upto v2.1.3
    *
     ==============================================================================================================*/

    [Info("Guess The Number", "Mabel", "2.2.3")]
    [Description("An event that requires player to guess the correct number")]

    class GuessTheNumber : RustPlugin
    {
        [PluginReference] private readonly Plugin Battlepass, ServerRewards, Economics, SkillTree, XPerience;

        private Data data;
        public Dictionary<ulong, int> playerInfo = new Dictionary<ulong, int>();

        bool useEconomics = true;
        bool useEconomicsloss = true;
        bool useServerRewards = false;
        bool useServerRewardsloss = false;
        bool useBattlepass1 = false;
        bool useBattlepass2 = false;
        bool useBattlepassloss = false;
        bool useSkillTree = false;
        bool useSkillTreeloss = false;
        bool useXPerience = false;
        bool useXPerienceloss = false;
        bool useItem = false;
        bool useCommand = false;
        bool autoEventsEnabled = false;
        bool showAttempts = false;
        bool showAttemptsTips = false;
        bool useChat = false;
        bool useGameTip = true;
        float gameTipDuration = 20f;
        float autoEventTime = 600f;
        float eventLength = 30f;
        int minDefault = 1;
        int maxDefault = 1000;
        int maxTries = 1;
        int MinPlayer = 1;
        int economicsWinReward = 20;
        int economicsLossReward = 10;
        int serverRewardsWinReward = 20;
        int serverRewardsLossReward = 10;
        int battlepassWinReward1 = 20;
        int battlepassWinReward2 = 20;
        int battlepassLossReward1 = 10;
        int battlepassLossReward2 = 10;
        int skillTreeWinReward = 100;
        int skillTreeLossReward = 50;
        int xPerienceWinReward = 100;
        int xPerienceLossReward = 50;
        int itemWinReward = 100;
        string itemName = "scrap";
        ulong itemSkin = 0;
        string itemCustomName = "";
        string commandName = "Display Name For Reward Messages";
        string commandToExecute = "o.grant user {playerId} some.permission";
        string chatCommand = "top";


        string Prefix = "[<color=#abf229>GuessTheNumber</color>] ";
        ulong SteamIDIcon = 0;
        private bool AddSecondCurrency;
        private bool AddFirstCurrency;

        const string permissionNameADMIN = "guessthenumber.admin";
        const string permissionNameENTER = "guessthenumber.enter";

        bool Changed = false;
        bool eventActive = false;
        Timer eventTimer;
        Timer autoRepeatTimer;
        int minNumber = 0;
        int maxNumber = 0;
        bool hasEconomics = false;
        bool hasServerRewards = false;
        bool hasBattlepass = false;
        bool hasSkillTree = false;
        bool hasXPerince = false;
        int number = 0;

        void LoadVariables()
        {
            //UI Chat Command
            chatCommand = Convert.ToString(GetConfig("LeaderBoard UI Settings", "Command", "top"));
            //Announce
            showAttempts = Convert.ToBoolean(GetConfig("Announce Settings", "Show all Guess Attempts to chat", false));
            showAttemptsTips = Convert.ToBoolean(GetConfig("Announce Settings", "Show all Guess Attempts to GameTips", false));
            useChat = Convert.ToBoolean(GetConfig("Announce Settings", "Use Chat Messages", false));
            Prefix = Convert.ToString(GetConfig("Announce Settings", "Prefix", "[<color=#abf229>Guess The Number</color>] "));
            SteamIDIcon = Convert.ToUInt64(GetConfig("Announce Settings", "SteamID", 76561199090290915));
            useGameTip = Convert.ToBoolean(GetConfig("Announce Settings", "Use Game Tip Messages", true));
            gameTipDuration = Convert.ToInt32(GetConfig("Announce Settings", "Game Tip Duration", 20));
            //Online
            MinPlayer = Convert.ToInt32(GetConfig("Online Settings", "Minimum amount of players to be online to start the game", "1"));
            //Events
            autoEventsEnabled = Convert.ToBoolean(GetConfig("Event Settings", "Auto Events Enabled", false));
            autoEventTime = Convert.ToInt32(GetConfig("Event Settings", "Auto Event Repeat Time", 600));
            eventLength = Convert.ToInt32(GetConfig("Event Settings", "Event Length", 30));
            minDefault = Convert.ToInt32(GetConfig("Event Settings", "Default Number Min", 1));
            maxDefault = Convert.ToInt32(GetConfig("Event Settings", "Default Number Max", 100));
            maxTries = Convert.ToInt32(GetConfig("Event Settings", "Max Tries", 1));
            //Economics
            useEconomics = Convert.ToBoolean(GetConfig("Reward Economics Settings", "Use Economics", true));
            useEconomicsloss = Convert.ToBoolean(GetConfig("Reward Economics Settings", "Use Economics on loss", true));
            economicsWinReward = Convert.ToInt32(GetConfig("Reward Economics Settings", "Amount (win)", 20));
            economicsLossReward = Convert.ToInt32(GetConfig("Reward Economics Settings", "Amount (loss)", 10));
            //ServerRewards
            useServerRewards = Convert.ToBoolean(GetConfig("Reward ServerRewards Settings", "Use ServerRewards", false));
            useServerRewardsloss = Convert.ToBoolean(GetConfig("Reward ServerRewards Settings", "Use ServerRewards on loss", false));
            serverRewardsWinReward = Convert.ToInt32(GetConfig("Reward ServerRewards Settings", "Amount (win)", 20));
            serverRewardsLossReward = Convert.ToInt32(GetConfig("Reward ServerRewards Settings", "Amount (loss)", 10));
            //Battlepass
            useBattlepass1 = Convert.ToBoolean(GetConfig("Reward Battlepass Settings", "Use Battlepass 1st currency", false));
            useBattlepass2 = Convert.ToBoolean(GetConfig("Reward Battlepass Settings", "Use Battlepass 2nd currency", false));
            useBattlepassloss = Convert.ToBoolean(GetConfig("Reward Battlepass Settings", "Use Battlepass on loss", false));
            battlepassWinReward1 = Convert.ToInt32(GetConfig("Reward Battlepass Settings", "Amount 1st currency (win)", 20));
            battlepassWinReward2 = Convert.ToInt32(GetConfig("Reward Battlepass Settings", "Amount 2nd currency (win)", 20));
            battlepassLossReward1 = Convert.ToInt32(GetConfig("Reward Battlepass Settings", "Amount 1st currency (loss)", 10));
            battlepassLossReward2 = Convert.ToInt32(GetConfig("Reward Battlepass Settings", "Amount 2nd currency (loss)", 10));
            //SkillTree
            useSkillTree = Convert.ToBoolean(GetConfig("Reward SkillTree Settings", "Use SkillTree", false));
            useSkillTreeloss = Convert.ToBoolean(GetConfig("Reward SkillTree Settings", "Use SkillTree on loss", false));
            skillTreeWinReward = Convert.ToInt32(GetConfig("Reward SkillTree Settings", "Amount (win)", 100));
            skillTreeLossReward = Convert.ToInt32(GetConfig("Reward SkillTree Settings", "Amount (loss)", 50));
            //XPerience
            useXPerience = Convert.ToBoolean(GetConfig("Reward XPerience Settings", "Use XPerience", false));
            useXPerienceloss = Convert.ToBoolean(GetConfig("Reward XPerience Settings", "Use XPerience on loss", false));
            xPerienceWinReward = Convert.ToInt32(GetConfig("Reward XPerience Settings", "Amount (win)", 100));
            xPerienceLossReward = Convert.ToInt32(GetConfig("Reward XPerience Settings", "Amount (loss)", 50));
            //Item
            useItem = Convert.ToBoolean(GetConfig("Reward Item Settings", "Use Item", false));
            itemName = Convert.ToString(GetConfig("Reward Item Settings", "Item Shortname", "scrap"));
            itemSkin = Convert.ToUInt64(GetConfig("Reward Item Settings", "Item Skin ID", 0));
            itemCustomName = Convert.ToString(GetConfig("Reward Item Settings", "Custom Display Name", ""));
            itemWinReward = Convert.ToInt32(GetConfig("Reward Item Settings", "Amount", 100));
            //Command
            useCommand = Convert.ToBoolean(GetConfig("Reward Command Settings", "Use Command", false));
            commandName = Convert.ToString(GetConfig("Reward Command Settings", "Command Name", "Display Name For Reward Messages"));
            commandToExecute = Convert.ToString(GetConfig("Reward Command Settings", "Command To Execute", "o.grant user {playerId} some.permission"));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }

        void Init()
        {
            LoadData();
            permission.RegisterPermission(permissionNameADMIN, this);
            permission.RegisterPermission(permissionNameENTER, this);
            LoadVariables();
        }

        void Unload()
        {
            killUI();

            if (autoEventsEnabled)
                if (!autoRepeatTimer.Destroyed)
                {
                    autoRepeatTimer.Destroy();
                }
            return;
        }

        private void OnServerInitialized()
        {
            LoadVariables();

            cmd.AddChatCommand(chatCommand, this, nameof(TopCommand));

            if (autoEventsEnabled)
            {
                if (eventActive)
                {
                    return;
                }
                autoRepeatTimer = timer.Repeat(autoEventTime, 0, () =>
                {
                    if (BasePlayer.activePlayerList.Count >= MinPlayer)
                    {
                        minNumber = minDefault;
                        maxNumber = maxDefault;
                        number = Convert.ToInt32(Math.Round(Convert.ToDouble(UnityEngine.Random.Range(Convert.ToSingle(minNumber), Convert.ToSingle(maxNumber)))));
                        StartEvent();
                    }
                    else
                    {
                        return;
                    }
                });
            }

            if (!Economics)
                hasEconomics = false;
            else
                hasEconomics = true;

            if (!ServerRewards)
                hasServerRewards = false;
            else
                hasServerRewards = true;

            if (!Battlepass)
                hasBattlepass = false;
            else
                hasBattlepass = true;

            if (!SkillTree)
                hasSkillTree = false;
            else
                hasSkillTree = true;

            if (!XPerience)
                hasXPerince = false;
            else
                hasXPerince = true;
        }

        void Loaded()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "You cannot use this command!",
                ["Event Already Active"] = "There is currently already an event that is active!",
                ["Event Started"] = "A random number event has started, correctly guess the random number to win a prize!\nUse /guess <number> to enter between <color=#abf229>{0}</color> and <color=#abf229>{1}</color>",
                ["Help Message"] = "<color=#abf229>/gtn start</color> (this will use the default min/max set in the config)",
                ["Help Message1"] = "<color=#abf229>/gtn start <min number> <max number></color> (allows you to set custom min/max numbers)",
                ["Help Message2"] = "<color=#abf229>/gtn end</color> (will end the current event)",
                ["No Event"] = "There are no current events active",
                ["Max Tries"] = "You have already guessed the maximum number of times",
                ["Event Win"] = "<color=#abf229>{0}</color> has won the event! (correct number was <color=#abf229>{1}</color>)",
                ["Battlepass Reward1"] = "For winning you are rewarded (BP1) : <color=#abf229>{0}</color>",
                ["Battlepass loss Reward1"] = "Incorrect answer you get (BP1) : <color=#abf229>{0}</color>",
                ["Battlepass Reward2"] = "For winning you are rewarded (BP2) : <color=#abf229>{0}</color>",
                ["Battlepass loss Reward2"] = "Incorrect answer you get (BP2) : <color=#abf229>{0}</color>",
                ["Economics Reward"] = "For winning you are rewarded $ <color=#abf229>{0}</color>",
                ["Economics loss Reward"] = "Incorrect answer you get $ <color=#abf229>{0}</color>",
                ["ServerRewards Reward"] = "For winning you are rewarded <color=#abf229>{0}</color> RP",
                ["ServerRewards loss Reward"] = "Incorrect answer you get <color=#abf229>{0}</color> RP",
                ["Wrong Number"] = "You guessed the wrong number\nGuesses remaining this round : <color=#abf229>{0}</color>",
                ["/guess Invalid Syntax"] = "Invalid syntax! /guess <number>",
                ["Event Timed Out"] = "The event time has run out and no one successfully guessed the number!\nThe Number to guess was : <color=#abf229>{0}</color>",
                ["Invalid Guess Entry"] = "The guess you entered was invalid! numbers only please",
                ["Event Created"] = "The event has been succesfully created, the winning number is <color=#abf229>{0}</color>",
                ["GTN console invalid syntax"] = "Invalid syntax! gtn <start/end> <min number> <max number>",
                ["SkillTree Reward"] = "For winning you are rewarded <color=#abf229>{0}</color> XP",
                ["SkillTree loss Reward"] = "Incorrect answer you get <color=#abf229>{0}</color> XP",
                ["XPerience Reward"] = "For winning you are rewarded <color=#abf229>{0}</color> XP",
                ["XPerience loss Reward"] = "Incorrect answer you get <color=#abf229>{0}</color> XP",
                ["Item Reward"] = "For winning you are rewarded <color=#abf229>{0}</color>",
                ["Command Reward"] = "For winning you are rewarded <color=#abf229>{0}</color>",
                ["UI_TITLE"] = "Guess The Number",
                ["UI_TOP_TEXT"] = "Top 10 Players",
                ["UI_NO_PLAYERS"] = "Nobody Has Played Yet! :(",
                ["UI_PLAYERS"] = "{0}. {1} - Wins: <color=#abf229>{2}</color>"

            }, this);
        }

        [ConsoleCommand("gtn")]
        void GTNCONSOLECMD(ConsoleSystem.Arg args)
        {
            if (args.Connection != null)
                return;
            if (args.Args == null)
            {
                args.ReplyWith(msg("GTN console invalid syntax"));
                return;
            }
            if (args.Args.Length == 0)
            {
                args.ReplyWith(msg("GTN console invalid syntax"));
                return;
            }
            if (args.Args.Length > 3)
            {
                args.ReplyWith(msg("GTN console invalid syntax"));
                return;
            }
            if (args.Args[0] == null)
            {
                args.ReplyWith(msg("GTN console invalid syntax"));
                return;
            }
            if (args.Args[0] == "start")
            {
                if (eventActive)
                {
                    args.ReplyWith(msg("Event Already Active"));
                    return;
                }
                if (args.Args.Length == 3)
                {
                    minNumber = Convert.ToInt32(args.Args[1]);
                    maxNumber = Convert.ToInt32(args.Args[2]);
                    if (minNumber != 0 && maxNumber != 0)
                    {
                        number = Convert.ToInt32(Math.Round(Convert.ToDouble(UnityEngine.Random.Range(Convert.ToSingle(minNumber), Convert.ToSingle(maxNumber)))));
                        StartEvent();
                        args.ReplyWith(string.Format(msg("Event Created"), number.ToString()));
                    }
                    else
                    {
                        args.ReplyWith(msg("Invalid Params"));
                        return;
                    }
                }
                else
                {
                    minNumber = minDefault;
                    maxNumber = maxDefault;
                    number = Convert.ToInt32(Math.Round(Convert.ToDouble(UnityEngine.Random.Range(Convert.ToSingle(minNumber), Convert.ToSingle(maxNumber)))));
                    StartEvent();
                    args.ReplyWith(string.Format(msg("Event Created"), number.ToString()));
                }
                if (autoEventsEnabled)
                    if (!autoRepeatTimer.Destroyed)
                    {
                        autoRepeatTimer.Destroy();
                        autoRepeatTimer = timer.Repeat(autoEventTime, 0, () => StartEvent());
                    }
                return;
            }
            else if (args.Args[0] == "end")
            {
                if (eventActive == false)
                {
                    args.ReplyWith(msg("No Event"));
                    return;
                }
                if (!eventTimer.Destroyed || eventTimer != null)
                    eventTimer.Destroy();
                if (autoEventsEnabled)
                    if (!autoRepeatTimer.Destroyed)
                    {
                        autoRepeatTimer.Destroy();
                        autoRepeatTimer = timer.Repeat(autoEventTime, 0, () => StartEvent());
                    }
                eventActive = false;
                args.ReplyWith("The current event has been cancelled");

                if (useChat)
                {
                    SendMessageToActivePlayers(msg("Event Timed Out"));
                }
                if (useGameTip)
                {
                    SendToastToActivePlayers(msg("Event Timed Out"));
                }
            }
            else
                args.ReplyWith(msg("GTN console invalid syntax"));
            return;
        }

        [ChatCommand("gtn")]
        private void startGuessNumberEvent(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameADMIN))
            {
                if (useChat)
                {
                    Player.Message(player, $"{lang.GetMessage("No Permission", this, player.UserIDString)}", Prefix, SteamIDIcon);
                }
                if (useGameTip)
                {
                    player.ShowToast(GameTip.Styles.Red_Normal, $"{lang.GetMessage("No Permission", this, player.UserIDString)}");
                }
                return;
            }
            if (args.Length == 0)
            {
                player.ChatMessage(DoHelpMenu());
                return;
            }
            if (args.Length > 3)
            {
                player.ChatMessage(DoHelpMenu());
                return;
            }
            if (args[0] == null)
            {
                player.ChatMessage(DoHelpMenu());
                return;
            }
            if (args[0] == "start")
            {
                if (eventActive)
                {
                    if (useChat)
                    {
                        Player.Message(player, $"{lang.GetMessage("Event Already Active", this, player.UserIDString)}", Prefix, SteamIDIcon);
                    }
                    if (useGameTip)
                    {
                        player.ShowToast(GameTip.Styles.Red_Normal, $"{lang.GetMessage("Event Already Active", this, player.UserIDString)}");
                    }
                    return;
                }
                if (args.Length == 3)
                {
                    minNumber = Convert.ToInt32(args[1]);
                    maxNumber = Convert.ToInt32(args[2]);
                    if (minNumber != 0 && maxNumber != 0)
                    {
                        number = Convert.ToInt32(Math.Round(Convert.ToDouble(UnityEngine.Random.Range(Convert.ToSingle(minNumber), Convert.ToSingle(maxNumber)))));
                        StartEvent();

                        if (useChat)
                        {
                            Player.Message(player, $"{string.Format(lang.GetMessage("Event Created", this, player.UserIDString), number)}", Prefix, SteamIDIcon);
                        }
                        if (useGameTip)
                        {
                            player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("Event Created", this, player.UserIDString), number)}", gameTipDuration);

                            timer.Once(gameTipDuration, () =>
                            {
                                player.SendConsoleCommand("gametip.hidegametip");
                            });
                        }
                    }
                    else
                    {
                        if (useChat)
                        {
                            Player.Message(player, $"{lang.GetMessage("Invalid Params", this, player.UserIDString)}", Prefix, SteamIDIcon);
                        }
                        if (useGameTip)
                        {
                            player.ShowToast(GameTip.Styles.Red_Normal, $"{lang.GetMessage("Invalid Params", this, player.UserIDString)}");
                        }
                        return;
                    }
                }
                else
                {
                    minNumber = minDefault;
                    maxNumber = maxDefault;
                    number = Convert.ToInt32(Math.Round(Convert.ToDouble(UnityEngine.Random.Range(Convert.ToSingle(minNumber), Convert.ToSingle(maxNumber)))));
                    StartEvent();

                    if (useChat)
                    {
                        Player.Message(player, $"{string.Format(lang.GetMessage("Event Created", this, player.UserIDString), number)}", Prefix, SteamIDIcon);
                    }
                    if (useGameTip)
                    {
                        player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("Event Created", this, player.UserIDString), number)}", gameTipDuration);

                        timer.Once(gameTipDuration, () =>
                        {
                            player.SendConsoleCommand("gametip.hidegametip");
                        });
                    }
                }
                if (autoEventsEnabled)
                    if (!autoRepeatTimer.Destroyed)
                    {
                        autoRepeatTimer.Destroy();
                        autoRepeatTimer = timer.Repeat(autoEventTime, 0, () => StartEvent());
                    }
                return;
            }
            else if (args[0] == "end")
            {
                if (eventActive == false)
                {
                    if (useChat)
                    {
                        Player.Message(player, $"{lang.GetMessage("No Event", this, player.UserIDString)}", Prefix, SteamIDIcon);
                    }
                    if (useGameTip)
                    {
                        player.ShowToast(GameTip.Styles.Red_Normal, $"{lang.GetMessage("No Event", this, player.UserIDString)}");
                    }
                    return;
                }
                if (!eventTimer.Destroyed || eventTimer != null)
                    eventTimer.Destroy();
                if (autoEventsEnabled)
                    if (!autoRepeatTimer.Destroyed)
                    {
                        autoRepeatTimer.Destroy();
                        autoRepeatTimer = timer.Repeat(autoEventTime, 0, () => StartEvent());
                    }
                eventActive = false;
                if (useChat)
                {
                    SendMessageToActivePlayers(msg("Event Timed Out"));
                }
                if (useGameTip)
                {
                    SendToastToActivePlayers(msg("Event Timed Out"));
                }
            }
            else
            {
                if (useChat)
                {
                    Player.Message(player, $"{lang.GetMessage("Help Message", this, player.UserIDString)}", Prefix, SteamIDIcon);
                }
                if (useGameTip)
                {
                    player.SendConsoleCommand("gametip.showgametip", $"{lang.GetMessage("Help Message", this, player.UserIDString)}", gameTipDuration);

                    timer.Once(gameTipDuration, () =>
                    {
                        player.SendConsoleCommand("gametip.hidegametip");
                    });
                }
            }
            return;
        }

        [ChatCommand("guess")]
        private void numberReply(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionNameENTER))
            {
                if (useChat)
                {
                    Player.Message(player, $"{lang.GetMessage("No Permission", this, player.UserIDString)}", Prefix, SteamIDIcon);
                }
                if (useGameTip)
                {
                    player.ShowToast(GameTip.Styles.Red_Normal, $"{lang.GetMessage("No Permission", this, player.UserIDString)}");
                }
                return;
            }
            if (!eventActive)
            {
                if (useChat)
                {
                    Player.Message(player, $"{lang.GetMessage("No Event", this, player.UserIDString)}", Prefix, SteamIDIcon);
                }
                if (useGameTip)
                {
                    player.ShowToast(GameTip.Styles.Red_Normal, $"{lang.GetMessage("No Event", this, player.UserIDString)}");
                }
                return;
            }

            if (args.Length == 1)
            {
                if (!IsNumber(args[0]))
                {
                    if (useChat)
                    {
                        Player.Message(player, $"{lang.GetMessage("Invalid Guess Entry", this, player.UserIDString)}", Prefix, SteamIDIcon);
                    }
                    if (useGameTip)
                    {
                        player.ShowToast(GameTip.Styles.Red_Normal, $"{lang.GetMessage("Invalid Guess Entry", this, player.UserIDString)}");
                    }
                    return;
                }
                int playerNum = Convert.ToInt32(args[0]);
                if (!playerInfo.ContainsKey(player.userID))
                    playerInfo.Add(player.userID, 0);
                if (playerInfo[player.userID] >= maxTries)
                {
                    if (useChat)
                    {
                        Player.Message(player, $"{lang.GetMessage("Max Tries", this, player.UserIDString)}", Prefix, SteamIDIcon);
                    }
                    if (useGameTip)
                    {
                        player.ShowToast(GameTip.Styles.Red_Normal, $"{lang.GetMessage("Max Tries", this, player.UserIDString)}");
                    }
                    return;
                }
                if (args[0] == "0")
                {
                    if (useChat)
                    {
                        Player.Message(player, $"{lang.GetMessage("You are not allowed to guess this number", this, player.UserIDString)}", Prefix, SteamIDIcon);
                    }
                    if (useGameTip)
                    {
                        player.ShowToast(GameTip.Styles.Red_Normal, $"{lang.GetMessage("You are not allowed to guess this number", this, player.UserIDString)}");
                    }
                    return;
                }

                if (showAttempts == true)
                {
                    SendMessageToActivePlayers($"<color=#abf229>{player.displayName}</color> guessed {args[0].ToString()}");
                }
                if (showAttemptsTips == true)
                {
                    SendToastToActivePlayers($"<color=#abf229>{player.displayName}</color> guessed {args[0].ToString()}");
                }
                if (playerNum == number)
                {
                    if (useChat)
                    {
                        SendMessageToActivePlayers(string.Format(msg("Event Win", player.UserIDString), player.displayName, number.ToString()));
                    }
                    if (useGameTip)
                    {
                        SendToastToActivePlayers(string.Format(msg("Event Win", player.UserIDString), player.displayName, number));
                    }

                    if (hasEconomics)
                    {
                        if (useEconomics)
                        {
                            if ((bool)Economics?.Call("Deposit", (ulong)player.userID, (double)economicsWinReward))
                            {
                                if (useChat)
                                {
                                    Player.Message(player, $"{string.Format(lang.GetMessage("Economics Reward", this, player.UserIDString), economicsWinReward)}", Prefix, SteamIDIcon);
                                }
                                if (useGameTip)
                                {
                                    player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("Economics Reward", this, player.UserIDString), economicsWinReward)}", gameTipDuration);

                                    timer.Once(gameTipDuration, () =>
                                    {
                                        player.SendConsoleCommand("gametip.hidegametip");
                                    });
                                }
                            }
                        }
                    }

                    if (hasServerRewards)
                    {
                        if (useServerRewards)
                        {
                            ServerRewards?.Call("AddPoints", (ulong)player.userID, (int)serverRewardsWinReward);
                            {
                                if (useChat)
                                {
                                    Player.Message(player, $"{string.Format(lang.GetMessage("ServerRewards Reward", this, player.UserIDString), serverRewardsWinReward)}", Prefix, SteamIDIcon);
                                }
                                if (useGameTip)
                                {
                                    player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("ServerRewards Reward", this, player.UserIDString), serverRewardsWinReward)}", gameTipDuration);

                                    timer.Once(gameTipDuration, () =>
                                    {
                                        player.SendConsoleCommand("gametip.hidegametip");
                                    });
                                }
                            }
                        }
                    }

                    if (hasBattlepass)
                    {
                        if (useBattlepass1)
                        {
                            Battlepass?.Call("AddFirstCurrency", (ulong)player.userID, battlepassWinReward1);
                            {
                                if (useChat)
                                {
                                    Player.Message(player, $"{string.Format(lang.GetMessage("Battlepass Reward1", this, player.UserIDString), battlepassWinReward1)}", Prefix, SteamIDIcon);
                                }
                                if (useGameTip)
                                {
                                    player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("Battlepass Reward1", this, player.UserIDString), battlepassWinReward1)}", gameTipDuration);

                                    timer.Once(gameTipDuration, () =>
                                    {
                                        player.SendConsoleCommand("gametip.hidegametip");
                                    });
                                }
                            }
                        }

                        if (useBattlepass2)
                        {
                            Battlepass?.Call("AddSecondCurrency", (ulong)player.userID, battlepassWinReward2);
                            {
                                if (useChat)
                                {
                                    Player.Message(player, $"{string.Format(lang.GetMessage("Battlepass Reward2", this, player.UserIDString), battlepassWinReward2)}", Prefix, SteamIDIcon);
                                }
                                if (useGameTip)
                                {
                                    player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("Battlepass Reward2", this, player.UserIDString), battlepassWinReward2)}", gameTipDuration);

                                    timer.Once(gameTipDuration, () =>
                                    {
                                        player.SendConsoleCommand("gametip.hidegametip");
                                    });
                                }
                            }
                        }
                    }

                    if (hasSkillTree)
                    {
                        if (useSkillTree)
                        {
                            SkillTree?.Call("AwardXP", (ulong)player.userID, (double)skillTreeWinReward);
                            {
                                if (useChat)
                                {
                                    Player.Message(player, $"{string.Format(lang.GetMessage("SkillTree Reward", this, player.UserIDString), skillTreeWinReward)}", Prefix, SteamIDIcon);
                                }
                                if (useGameTip)
                                {
                                    player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("SkillTree Reward", this, player.UserIDString), skillTreeWinReward)}", gameTipDuration);

                                    timer.Once(gameTipDuration, () =>
                                    {
                                        player.SendConsoleCommand("gametip.hidegametip");
                                    });
                                }
                            }
                        }
                    }

                    if (hasXPerince)
                    {
                        if (useXPerience)
                        {
                            XPerience?.Call("GiveXP", player, (double)xPerienceWinReward);
                            {
                                if (useChat)
                                {
                                    Player.Message(player, $"{string.Format(lang.GetMessage("XPerience Reward", this, player.UserIDString), xPerienceWinReward)}", Prefix, SteamIDIcon);
                                }
                                if (useGameTip)
                                {
                                    player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("XPerience Reward", this, player.UserIDString), xPerienceWinReward)}", gameTipDuration);

                                    timer.Once(gameTipDuration, () =>
                                    {
                                        player.SendConsoleCommand("gametip.hidegametip");
                                    });
                                }
                            }
                        }
                    }
                    if (useItem)
                    {
                        int amount = itemWinReward;
                        var item = ItemManager.CreateByName(itemName, amount, itemSkin);
                        var displayName = string.IsNullOrEmpty(itemCustomName) ? item.info.displayName.translated : itemCustomName;
                        item.name = displayName;
                        player.GiveItem(item);

                        if (useChat)
                        {
                            Player.Message(player, $"{string.Format(lang.GetMessage("Item Reward", this, player.UserIDString), displayName)}", Prefix, SteamIDIcon);
                        }
                        if (useGameTip)
                        {
                            player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("Item Reward", this, player.UserIDString), displayName)}", gameTipDuration);

                            timer.Once(gameTipDuration, () =>
                            {
                                player.SendConsoleCommand("gametip.hidegametip");
                            });
                        }
                    }
                    if (useCommand)
                    {
                        var formattedCommand = commandToExecute.Replace("{playerId}", player.UserIDString);
                        Server.Command(formattedCommand);

                        if (useChat)
                        {
                            Player.Message(player, $"{string.Format(lang.GetMessage("Command Reward", this, player.UserIDString), commandName)}", Prefix, SteamIDIcon);
                        }
                        if (useGameTip)
                        {
                            player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("Command Reward", this, player.UserIDString), commandName)}", gameTipDuration);

                            timer.Once(gameTipDuration, () =>
                            {
                                player.SendConsoleCommand("gametip.hidegametip");
                            });
                        }
                    }
                    LogWinner(player.displayName);

                    number = 0;
                    eventActive = false;
                    playerInfo.Clear();
                    eventTimer.Destroy();
                    autoRepeatTimer = timer.Repeat(autoEventTime, 0, () => StartEvent());
                }
                else
                {
                    playerInfo[player.userID]++;
                    if (useChat)
                    {
                        Player.Message(player, $"{string.Format(lang.GetMessage("Wrong Number", this, player.UserIDString), (playerInfo[player.userID] - maxTries))}", Prefix, SteamIDIcon);
                    }
                    if (useGameTip)
                    {
                        player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("Wrong Number", this, player.UserIDString), (playerInfo[player.userID] - maxTries))}", gameTipDuration);

                        timer.Once(gameTipDuration, () =>
                        {
                            player.SendConsoleCommand("gametip.hidegametip");
                        });
                    }
                    if (hasEconomics)
                    {
                        if (useEconomics)
                        {
                            if (useEconomicsloss)
                            {
                                if ((bool)Economics?.Call("Deposit", (ulong)player.userID, (double)economicsLossReward))
                                {
                                    if (useChat)
                                    {
                                        Player.Message(player, $"{string.Format(lang.GetMessage("Economics loss Reward", this, player.UserIDString), economicsLossReward)}", Prefix, SteamIDIcon);
                                    }
                                    if (useGameTip)
                                    {
                                        player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("Economics loss Reward", this, player.UserIDString), economicsLossReward)}", gameTipDuration);

                                        timer.Once(gameTipDuration, () =>
                                        {
                                            player?.SendConsoleCommand("gametip.hidegametip");
                                        });
                                    }
                                }
                            }
                        }
                    }

                    if (hasServerRewards)
                    {
                        if (useServerRewards)
                        {
                            if (useServerRewardsloss)
                            {
                                ServerRewards?.Call("AddPoints", (ulong)player.userID, (int)serverRewardsLossReward);
                                {
                                    if (useChat)
                                    {
                                        Player.Message(player, $"{string.Format(lang.GetMessage("ServerRewards loss Reward", this, player.UserIDString), serverRewardsLossReward)}", Prefix, SteamIDIcon);
                                    }
                                    if (useGameTip)
                                    {
                                        player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("ServerRewards loss Reward", this, player.UserIDString), serverRewardsLossReward)}", gameTipDuration);

                                        timer.Once(gameTipDuration, () =>
                                        {
                                            player?.SendConsoleCommand("gametip.hidegametip");
                                        });
                                    }
                                }
                            }
                        }
                    }
                    if (hasBattlepass)
                    {
                        if (useBattlepass1)
                        {
                            if (useBattlepassloss)
                            {
                                Battlepass?.Call("AddFirstCurrency", (ulong)player.userID, battlepassLossReward1);
                                {
                                    if (useChat)
                                    {
                                        Player.Message(player, $"{string.Format(lang.GetMessage("Battlepass loss Reward1", this, player.UserIDString), battlepassLossReward1)}", Prefix, SteamIDIcon);
                                    }
                                    if (useGameTip)
                                    {
                                        player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("Battlepass loss Reward1", this, player.UserIDString), battlepassLossReward1)}", gameTipDuration);

                                        timer.Once(gameTipDuration, () =>
                                        {
                                            player?.SendConsoleCommand("gametip.hidegametip");
                                        });
                                    }
                                }
                            }
                        }

                        if (useBattlepass2)
                        {
                            if (useBattlepassloss)
                            {
                                Battlepass?.Call("AddSecondCurrency", (ulong)player.userID, battlepassWinReward2);
                                {
                                    if (useChat)
                                    {
                                        Player.Message(player, $"{string.Format(lang.GetMessage("Battlepass loss Reward2", this, player.UserIDString), battlepassLossReward2)}", Prefix, SteamIDIcon);
                                    }
                                    if (useGameTip)
                                    {
                                        player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("Battlepass loss Reward2", this, player.UserIDString), battlepassLossReward2)}", gameTipDuration);

                                        timer.Once(gameTipDuration, () =>
                                        {
                                            player?.SendConsoleCommand("gametip.hidegametip");
                                        });
                                    }
                                }
                            }
                        }
                    }
                    if (hasSkillTree)
                    {
                        if (useSkillTreeloss)
                        {
                            SkillTree?.Call("AwardXP", (ulong)player.userID, (double)skillTreeLossReward);
                            {
                                if (useChat)
                                {
                                    Player.Message(player, $"{string.Format(lang.GetMessage("SkillTree loss Reward", this, player.UserIDString), skillTreeLossReward)}", Prefix, SteamIDIcon);
                                }
                                if (useGameTip)
                                {
                                    player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("SkillTree loss Reward", this, player.UserIDString), skillTreeLossReward)}", gameTipDuration);

                                    timer.Once(gameTipDuration, () =>
                                    {
                                        player?.SendConsoleCommand("gametip.hidegametip");
                                    });
                                }
                            }
                        }
                    }
                    if (hasXPerince)
                    {
                        if (useXPerienceloss)
                        {
                            XPerience?.Call("GiveXP", player, (double)xPerienceLossReward);
                            {
                                if (useChat)
                                {
                                    Player.Message(player, $"{string.Format(lang.GetMessage("XPerience loss Reward", this, player.UserIDString), xPerienceLossReward)}", Prefix, SteamIDIcon);
                                }
                                if (useGameTip)
                                {
                                    player.SendConsoleCommand("gametip.showgametip", $"{string.Format(lang.GetMessage("XPerience loss Reward", this, player.UserIDString), xPerienceLossReward)}", gameTipDuration);

                                    timer.Once(gameTipDuration, () =>
                                    {
                                        player?.SendConsoleCommand("gametip.hidegametip");
                                    });
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                if (useChat)
                {
                    Player.Message(player, $"{lang.GetMessage("/guess Invalid Syntax", this, player.UserIDString)}", Prefix, SteamIDIcon);
                }
                if (useGameTip)
                {
                    player.ShowToast(GameTip.Styles.Red_Normal, $"{lang.GetMessage("/guess Invalid Syntax", this, player.UserIDString)}");
                }
            }
            return;
        }

        void StartEvent()
        {
            if (eventActive)
            {
                return;
            }
            if (number == 0)
            {
                return;
            }
            else
            {
                if (useChat)
                {
                    SendMessageToActivePlayers($"{string.Format(lang.GetMessage("Event Started", this), minNumber, maxNumber)}");
                }
                if (useGameTip)
                {
                    SendToastToActivePlayers($"{string.Format(lang.GetMessage("Event Started", this), minNumber, maxNumber)}");
                }
                Puts($"Started a random game and the number to guess is {number.ToString()}");
                eventActive = true;
                eventTimer = timer.Once(eventLength, () =>
                {
                    if (useChat)
                    {
                        SendMessageToActivePlayers($"{string.Format(lang.GetMessage("Event Timed Out", this), number)}");
                    }
                    if (useGameTip)
                    {
                        SendToastToActivePlayers($"{string.Format(lang.GetMessage("Event Timed Out", this), number)}");
                    }
                    eventActive = false;
                    playerInfo.Clear();
                });
            }
        }

        string DoHelpMenu()
        {
            StringBuilder x = new StringBuilder();
            x.AppendLine(msg("Help Message"));
            x.AppendLine(msg("Help Message1"));
            x.AppendLine(msg("Help Message2"));
            return x.ToString().TrimEnd();
        }

        bool IsNumber(string str)
        {
            foreach (char c in str)
                if (c < '0' || c > '9')
                    return false;
            return true;
        }

        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);

        void SendToastToActivePlayers(string messageKey)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player != null)
                {
                    player.SendConsoleCommand("gametip.showgametip", lang.GetMessage(messageKey, this, player.UserIDString), gameTipDuration);

                    timer.Once(gameTipDuration, () =>
                    {
                        player?.SendConsoleCommand("gametip.hidegametip");
                    });
                }
            }
        }
        void SendMessageToActivePlayers(string messageKey)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player != null)
                {
                    Player.Message(player, lang.GetMessage(messageKey, this, player.UserIDString), Prefix, SteamIDIcon);
                }
            }
        }

        [ConsoleCommand("gtn_wipe")]
        private void WipeData(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null)
            {
                ResetData();
            }
        }

        public class Data
        {
            public Dictionary<string, int> Winners { get; set; } = new Dictionary<string, int>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("GuessTheNumber_Data", data);
        }

        private void LoadData()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<Data>("GuessTheNumber_Data") ?? new Data();
        }

        private void ResetData()
        {
            data = new Data();
            SaveData();
            Puts("All Guess The Number data has been reset...");
        }

        void OnNewSave()
        {
            ResetData();
        }

        private void LogWinner(string playerName)
        {
            if (data.Winners.ContainsKey(playerName))
            {
                data.Winners[playerName]++;
            }
            else
            {
                data.Winners[playerName] = 1;
            }
            SaveData();
        }

        private Dictionary<string, int> GetWinners()
        {
            return data.Winners;
        }

        private List<KeyValuePair<string, int>> GetTopWinners()
        {
            var winners = GetWinners();
            var sortedWinners = new List<KeyValuePair<string, int>>(winners);
            sortedWinners.Sort((pair1, pair2) => pair2.Value.CompareTo(pair1.Value));
            return sortedWinners.Take(10).ToList();
        }

        private void TopCommand(BasePlayer player, string cmd)
        {
            if (player != null)
            {
                GuessTheNumberUI(player);
            }
        }

        private HashSet<BasePlayer> UiPlayers = new HashSet<BasePlayer>();

        [ConsoleCommand("guessnumber.close")]
        private void CloseGuessTheNumberUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player != null)
            {
                CuiHelper.DestroyUi(player, "GuessTheNumberUI");
                UiPlayers.Remove(player);
            }
        }

        private void killUI()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, "GuessTheNumberUI");
            }
        }

        private void GuessTheNumberUI(BasePlayer player)
        {
            if (!UiPlayers.Contains(player))
            {
                UiPlayers.Add(player);
            }

            CuiHelper.DestroyUi(player, "GuessTheNumberUI");

            var cuiElements = new CuiElementContainer();

            cuiElements.Add(new CuiPanel
            {
                Image = { Color = "0.70 0.67 0.65 0.3", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -208", OffsetMax = "250 208" },
                CursorEnabled = true
            },
            "Overlay", "GuessTheNumberUI");

            cuiElements.Add(new CuiElement
            {
                Parent = "GuessTheNumberUI",
                Name = "BackGround",
                Components = { new CuiImageComponent { Material = "assets/icons/iconmaterial.mat", Sprite = "assets/content/ui/ui.background.transparent.radial.psd", Color = "0 0 0 0" }, new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-250 -208", OffsetMax = "250 208" }, },
            });

            cuiElements.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-840 -445", OffsetMax = "840 445" }
            },
            "GuessTheNumberUI", "GuessTheNumberUIOffset");

            cuiElements.Add(new CuiPanel
            {
                Image = { Color = "0.1529412 0.1411765 0.1137255 1", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.35 0.25", AnchorMax = "0.65 0.75" }
            },
            "GuessTheNumberUIOffset", "GuessTheNumberUIMainBk");

            cuiElements.Add(new CuiPanel
            {
                Image = { Color = "0.1686275 0.1607843 0.1411765 0.9", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.02 0.05", AnchorMax = "0.979 0.90" }
            },
            "GuessTheNumberUIMainBk", "GuessTheNumberUIMainIS");

            cuiElements.Add(new CuiElement
            {
                Parent = "GuessTheNumberUIMainBk",
                Name = "TEXT",
                Components = { new CuiTextComponent { Text = lang.GetMessage("UI_TITLE", this, player.UserIDString), FontSize = 30, Align = TextAnchor.MiddleCenter, Color = "0.9686275 0.9215686 0.8823529 0.7", Font = "robotocondensed-bold.ttf" }, new CuiRectTransformComponent { AnchorMin = "0 0.5", AnchorMax = "1 1.4", OffsetMin = "0 0", OffsetMax = "0 0" } },
            });

            cuiElements.Add(new CuiButton
            {
                Button = { Command = "guessnumber.close", Color = "0.8 0.28 0.2 1", Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                RectTransform = { AnchorMin = "0.92 0.92", AnchorMax = "0.98 0.98" },
            }, "GuessTheNumberUIMainBk", "GuessTheNumberUIClose");

            cuiElements.Add(new CuiElement
            {
                Parent = "GuessTheNumberUIClose",
                Name = "GuessTheNumberUICloseIcon",
                Components = { new CuiImageComponent { Material = "assets/icons/iconmaterial.mat", Sprite = "assets/icons/close.png", Color = "0.729 0.694 0.658 1" }, new CuiRectTransformComponent { AnchorMin = "0.3 0.3", AnchorMax = "0.7 0.7" } },
            });

            cuiElements.Add(new CuiLabel
            {
                Text = { Text = lang.GetMessage("UI_TOP_TEXT", this, player.UserIDString), FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0.9686275 0.9215686 0.8823529 0.7", Font = "robotocondensed-bold.ttf" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 320" }
            }, "GuessTheNumberUIMainIS");

            var topWinners = GetTopWinners();
            if (topWinners.Count == 0)
            {
                cuiElements.Add(new CuiLabel
                {
                    Text = { Text = lang.GetMessage("UI_NO_PLAYERS", this, player.UserIDString), FontSize = 18, Align = TextAnchor.MiddleCenter, Color = "0.9686275 0.9215686 0.8823529 0.7", Font = "robotocondensed-bold.ttf" },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0" }
                }, "GuessTheNumberUIMainIS");
            }
            else
            {
                float topPosition = 0.85f;
                float heightStep = 0.08f;

                for (int i = 0; i < topWinners.Count; i++)
                {
                    var winner = topWinners[i];
                    cuiElements.Add(new CuiLabel
                    {
                        Text = { Text = string.Format(lang.GetMessage("UI_PLAYERS", this, player.UserIDString), i + 1, winner.Key, winner.Value), FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "0.9686275 0.9215686 0.8823529 0.7", },
                        RectTransform = { AnchorMin = $"0 {(topPosition - (i + 1) * heightStep)}", AnchorMax = $"1 {(topPosition - i * heightStep)}" }
                    }, "GuessTheNumberUIMainIS");
                }
            }
            CuiHelper.AddUi(player, cuiElements);
        }
    }
}