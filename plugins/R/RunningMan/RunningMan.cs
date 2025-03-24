using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("Running Man", "sami37", "1.7.6")]
    [Description("Get rewarded for killing runner or just survive as runner")]
    class RunningMan : RustPlugin
    {
        #region Declaration
        [PluginReference] private Plugin Clans, Economics, Friends, KarmaSystem, ServerRewards;

        private Timer _compassRefresh, _countdownRefresh;
        private Timer _eventpause;
        private Timer _eventstart;
        private bool _eventStarted;
        private Timer _ingameTimer;
        private readonly Random _rnd = new Random();
        private BasePlayer _runningman;
        private string panelString = "RunningPanel", panelCountString = "RunningCountPanel";

        private Dictionary<string, Dictionary<string, RewardData>> _savedReward =
            new Dictionary<string, Dictionary<string, RewardData>>(StringComparer.OrdinalIgnoreCase);

        private Timer _stillRunnerTimer;
        private double _LastStartedEvent;
        private double _time2;
        private double _NextEvent;
        #endregion

        #region Class

        private class RewardData
        {
            public Dictionary<string, ValueAmount> RewardItems;
        }

        private class ValueAmount
        {
            public int MaxValue;
            public int MinValue;
        }

        #endregion

        #region Configuration
        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Default")]
            public Default Info = new Default();

            [JsonProperty(PropertyName = "TimeRange")]
            public TimeRangeInfo TimeRange = new TimeRangeInfo();

            [JsonProperty(PropertyName = "Reward")]
            public Reward RewardInfo = new Reward();

            [JsonProperty(PropertyName = "CompassUI Info")]
            public CompassUi CompassUiInfo = new CompassUi();

            [JsonProperty(PropertyName = "Countdown Info")]
            public CountdownUi CountdownUiInfo = new CountdownUi();

            public class Default
            {
                [JsonProperty(PropertyName = "ChatName")]
                public string ChatName = "EVENT";

                [JsonProperty(PropertyName = "authLevel")]
                public int AuthLevel = 1;

                [JsonProperty(PropertyName = "AutoStart")]
                public bool AutoStart = true;

                [JsonProperty(PropertyName = "Display Distance")]
                public bool DisplayDistance = true;

                [JsonProperty(PropertyName = "Count")]
                public int Count = 2;

                [JsonProperty(PropertyName = "StarteventTime")]
                public int StarteventTime = 30;

                [JsonProperty(PropertyName = "PauseeventTime")]
                public int PauseeventTime = 30;

                [JsonProperty(PropertyName = "DisconnectPendingTimer")]
                public int DisconnectPendingTimer = 30;

                [JsonProperty(PropertyName = "Excluded auth level")]
                public int Excludedauthlevel = 1;

                [JsonProperty(PropertyName = "Block friends kill reward")]
                public bool Blockfriendskillreward = true;

                [JsonProperty(PropertyName = "Block clans kill reward")]
                public bool Blockclanskillreward = true;
            }

            public class TimeRangeInfo
            {
                [JsonProperty(PropertyName = "Start War Time")]
                public int StartWarTime = 18;

                [JsonProperty(PropertyName = "End War Time")]
                public int EndWarTime = 8;

                [JsonProperty(PropertyName = "Enable Time Range")]
                public bool TimeRange;

            }

            public class Reward
            {
                [JsonProperty(PropertyName = "Random")]
                public bool Random = true;

                [JsonProperty(PropertyName = "RewardFixing")]
                public string RewardFixing = "wood";

                [JsonProperty(PropertyName = "RewardFixingAmount")]
                public int RewardFixingAmount = 10000;

                [JsonProperty(PropertyName = "KarmaSystem")]
                public KarmaSystem KarmaInfo = new KarmaSystem();

                public class KarmaSystem
                {
                    [JsonProperty(PropertyName = "PointToRemove")]
                    public int PointToRemove;

                    [JsonProperty(PropertyName = "PointToAdd")]
                    public int PointToAdd = 1;
                }
            }

            public class CompassUi
            {
                [JsonProperty(PropertyName = "AnchorMin")]
                public string AnchorMin = "0.03 0.067";

                [JsonProperty(PropertyName = "AnchorMax")]
                public string AnchorMax = "0.05 0.09";

                [JsonProperty(PropertyName = "Direction")]
                public Direction RunnerDirection = new Direction();

                public class Direction
                {
                    [JsonProperty(PropertyName = "North East")]
                    public string NorthEast = "N-E";

                    [JsonProperty(PropertyName = "South East")]
                    public string SouthEast = "S-E";

                    [JsonProperty(PropertyName = "North West")]
                    public string NorthWest = "N-W";

                    [JsonProperty(PropertyName = "South West")]
                    public string SouthWest = "S-W";

                    [JsonProperty(PropertyName = "No runner")]
                    public string None = "/";
                }

                [JsonProperty(PropertyName = "Disable while event is off")]
                public bool Disabled;
            }

            public class CountdownUi
            {
                [JsonProperty(PropertyName = "AnchorMin")]
                public string AnchorMin = "0.93 0.93";

                [JsonProperty(PropertyName = "AnchorMax")]
                public string AnchorMax = "1 1";

                [JsonProperty(PropertyName = "Disable UI while no runner")]
                public bool DisableNoRunner;

                [JsonProperty(PropertyName = "Disable UI while runner on")]
                public bool DisableRunnerOn;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        #endregion

        #region Umod Hook
        private void OnServerInitialized()
        {
            LoadConfig();

            _eventStarted = false;
            if (_config.Info.AutoStart && !_config.TimeRange.TimeRange)
            {
                _eventpause = timer.Once(60 * _config.Info.PauseeventTime, () => Startevent());
                _LastStartedEvent = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            }
            else if (_config.Info.AutoStart && _config.TimeRange.TimeRange)
            {
                _ingameTimer = timer.Once(20, CheckTime);
                _NextEvent = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                                 _config.TimeRange.StartWarTime, 0, 0).Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                CheckTime();
            }

            _compassRefresh = timer.Every(5, RefreshUi);
            _countdownRefresh = timer.Every(1, RefreshCountdownUi);
            LoadSavedData();
        }

        private void Init()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {
                    "StartEventRunner",
                    "<color=#C4FF00>{0}</color>: Running man {1}\nKill him and get the reward!\nCommand: /run - to know the distance to the target."
                },
                {"NotEnoughPlayers", "<color=#C4FF00>{0}</color>: There aren't enough players to start the event."},
                {"RunnerSaved", "<color=#C4FF00>{0}</color>: {1} ran away from the chase and received a reward!"},
                {"StillRunner", "<color=#C4FF00>{0}</color>: {1} your are still the runner."},
                {"RunnerBackOnline", "<color=#C4FF00>{0}</color>: {1} is back online.\nKill him and get the reward!"},
                {"RunnerKilled", "<color=#C4FF00>{0}</color>: Player - {1} kill {2} and received a reward!"},
                {
                    "RunnerDistance",
                    "<color=#C4FF00>{0}</color>: Player - {1},\n is at a distance of {2}\nKill him and get the reward!"
                },
                {"UntilEndOfEvent", "<color=#C4FF00>{0}</color>: Until the end of event left: {1} minutes"},
                {"NotRunningEvent", "<color=#C4FF00>{0}</color>: At the moment the event is not running"},
                {
                    "UntilStartOfEvent",
                    "<color=#C4FF00>{0}</color>: Before the start of the event remained: {1} minutes"
                },
                {"RunCommandHelp", "Use \"/run\" to find out information about the running man"},
                {
                    "AdminCommandHelp",
                    "Use \"/eventon\" for start event Running Man\nUse \"/eventoff\" for start event Running Man"
                },
                {
                    "AdminAddCommandHelp",
                    "Use \"/running add <Package Name> <ItemName or money or karma> <MinAmount> <MaxAmount>\" to add item."
                },
                {
                    "AdminRemoveCommandHelp",
                    "Use \"/running remove <PackageName> <ItemName or karma or money>\" to remove item."
                },
                {"NobodyOnline", "<color=#C4FF00>{0}</color>: You can't run event while there is nobody online"},
                {"NoPerm", "<color=#C4FF00>{0}</color>: You have no rights to do this!"},
                {"RunnerLeaved", "<color=#C4FF00>{0}</color>: {1} got scared and ran away!"},
                {"EventStopped", "<color=#C4FF00>{0}</color>: Event has stopped!"},
                {"PackageDontExist", "<color=#C4FF00>{0}</color>: This package don't exist."},
                {"MissingItemFromPackage", "<color=#C4FF00>{0}</color>: Item not found in package."},
                {"ItemRemoved", "<color=#C4FF00>{0}</color>: Successfully removed item {1}."},
                {"ItemAdded", "<color=#C4FF00>{0}</color>: Successfully added item {1} to package {2}."},
                {"PackageAdded", "<color=#C4FF00>{0}</color>: Successfully added package {1} and inserted item to it."},
                {"ItemExist", "<color=#C4FF00>{0}</color>: Item already exist in package."},
                {"Rewarded", "<color=#C4FF00>{0}</color>: You won a reward : {1}."}
            }, this);
            permission.RegisterPermission("runningman.admin", this);
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        private void Unload()
        {
            _eventpause?.Destroy();
            _eventstart?.Destroy();
            _ingameTimer?.Destroy();
            _compassRefresh?.Destroy();
            _countdownRefresh?.Destroy();
            _runningman = null;
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUi(player);
                DestroyCountdownUi(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (_runningman != null)
                if (player == _runningman)
                    _stillRunnerTimer = timer.Once(60 * _config.Info.DisconnectPendingTimer,
                        DestroyLeaveEvent);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (_runningman != null)
                if (_runningman == player)
                {
                    SendReply(player,
                        string.Format(lang.GetMessage("StillRunner", this, player.UserIDString),
                            _config.Info.ChatName, _runningman.displayName));
                    BroadcastChat(string.Format(lang.GetMessage("RunnerBackOnline", this),
                        _config.Info.ChatName, _runningman.displayName));
                    _stillRunnerTimer?.Destroy();
                }
                else
                {
                    SendHelpText(player);
                }
        }

        private void OnPlayerDeath(BasePlayer victim, HitInfo hitinfo)
        {
            var attacker = hitinfo?.Initiator?.ToPlayer();
            if (attacker == null || victim == null || _runningman == null)
                return;
            if (attacker == victim)
                return;
            if (victim != _runningman)
                return;
            if (Friends != null && (bool)Friends?.CallHook("AreFriends", attacker.userID, victim.userID) && _config.Info.Blockfriendskillreward)
                return;

            var attackerclan = Clans?.CallHook("GetClanOf", attacker.userID);
            var victimclan = Clans?.CallHook("GetClanOf", victim.userID);

            if (victimclan != null && attackerclan != null && victimclan == attackerclan && _config.Info.Blockclanskillreward)
                return;

            Runlog(string.Format(lang.GetMessage("RunnerKilled", this), _config.Info.ChatName, attacker.displayName, _runningman.displayName));

            BroadcastChat(string.Format(lang.GetMessage("RunnerKilled", this), _config.Info.ChatName, attacker.displayName, _runningman.displayName));
            var inv = attacker.inventory;
            if (_config.RewardInfo.Random)
            {
                if (_savedReward?["killer"] == null)
                {
                    PrintWarning("Reward list is empty, please add items, using FixingReward option...");
                    inv?.GiveItem(ItemManager.CreateByName(_config.RewardInfo.RewardFixing, _config.RewardInfo.RewardFixingAmount), inv.containerMain);
                    return;
                }

                var rand = _savedReward["killer"].ElementAt(_rnd.Next(0, _savedReward["killer"].Count));
                foreach (var data in rand.Value.RewardItems)
                {
                    var randomReward = _rnd.Next(data.Value.MinValue, data.Value.MaxValue);
                    switch (data.Key.ToLower())
                    {
                        case "karma":
                            if (KarmaSystem != null && KarmaSystem.IsLoaded)
                            {
                                var player = covalence.Players.FindPlayerById(attacker.UserIDString);
                                KarmaSystem.Call("AddKarma", player, (double)randomReward);
                                SendReply(attacker,
                                    string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                        _config.Info.ChatName, data.Key + " x " + randomReward));
                            }
                            else
                            {
                                inv?.GiveItem(
                                    ItemManager.CreateByName(_config.RewardInfo.RewardFixing,
                                        _config.RewardInfo.RewardFixingAmount), inv.containerMain);
                                SendReply(attacker, string.Format(
                                    lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                    _config.Info.ChatName, _config.RewardInfo.RewardFixing + " x " + _config.RewardInfo.RewardFixingAmount));
                            }

                            break;
                        case "money":
                            if (Economics != null && Economics.IsLoaded)
                            {
                                Economics.CallHook("Deposit", attacker.userID,
                                    randomReward);
                                SendReply(attacker,
                                    string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                        _config.Info.ChatName, "money x " + randomReward));
                            }
                            else
                            {
                                inv?.GiveItem(
                                    ItemManager.CreateByName(_config.RewardInfo.RewardFixing,
                                        _config.RewardInfo.RewardFixingAmount), inv.containerMain);
                                SendReply(attacker, string.Format(
                                    lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                    _config.Info.ChatName, _config.RewardInfo.RewardFixing + " x " + _config.RewardInfo.RewardFixingAmount));
                            }

                            break;
                        case "serverreward":
                            if (ServerRewards != null && ServerRewards.IsLoaded)
                            {
                                ServerRewards.CallHook("AddPoints", attacker.userID, randomReward);
                                SendReply(attacker,
                                    string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                        _config.Info.ChatName,
                                        "ServerRewards points x " + randomReward));
                            }
                            else
                            {
                                inv?.GiveItem(
                                    ItemManager.CreateByName(_config.RewardInfo.RewardFixing,
                                        _config.RewardInfo.RewardFixingAmount), inv.containerMain);
                                SendReply(attacker, string.Format(
                                    lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                    _config.Info.ChatName, _config.RewardInfo.RewardFixing + " x " + _config.RewardInfo.RewardFixingAmount));
                            }

                            break;
                        default:
                            var item = ItemManager.CreateByName(data.Key,
                                randomReward);
                            if (item != null)
                            {
                                inv?.GiveItem(item, inv.containerMain);
                                SendReply(attacker,
                                    string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                        _config.Info.ChatName,
                                        item.info.displayName.english + " x " + item.amount));
                            }
                            else
                            {
                                PrintError($"Failed to create item...{rand.Key}");
                            }

                            break;
                    }
                }
            }
            else
            {
                switch ((_config.RewardInfo.RewardFixing).ToLower())
                {
                    case "karma":
                        if (KarmaSystem != null && KarmaSystem.IsLoaded)
                        {
                            var player = covalence.Players.FindPlayerById(attacker.UserIDString);
                            KarmaSystem.Call("AddKarma", player, _config.RewardInfo.RewardFixingAmount);
                            SendReply(attacker,
                                string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                    _config.Info.ChatName,
                                    "Karma point x " + _config.RewardInfo.RewardFixingAmount));
                        }
                        else
                        {
                            inv?.GiveItem(
                                ItemManager.CreateByName(_config.RewardInfo.RewardFixing,
                                    _config.RewardInfo.RewardFixingAmount), inv.containerMain);
                            SendReply(attacker, string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                _config.Info.ChatName,
                                _config.RewardInfo.RewardFixing + " x " +
                                _config.RewardInfo.RewardFixingAmount));
                        }

                        break;
                    case "money":
                        if (Economics != null && Economics.IsLoaded)
                        {
                            Economics.CallHook("Deposit", attacker.userID,
                                _config.RewardInfo.RewardFixingAmount);
                            SendReply(attacker,
                                string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                    _config.Info.ChatName,
                                    "money x " + _config.RewardInfo.RewardFixingAmount));
                        }

                        break;
                    case "serverreward":
                        if (ServerRewards != null && ServerRewards.IsLoaded)
                        {
                            ServerRewards.CallHook("AddPoints", attacker.userID,
                                _config.RewardInfo.RewardFixingAmount);

                            SendReply(attacker,
                                string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                    _config.Info.ChatName,
                                    "ServerRewards points x " + _config.RewardInfo.RewardFixingAmount));
                        }

                        break;
                    default:
                        var item = ItemManager.CreateByName(_config.RewardInfo.RewardFixing,
                            _config.RewardInfo.RewardFixingAmount);
                        if (item != null)
                        {
                            inv?.GiveItem(item, inv.containerMain);
                            SendReply(attacker,
                                string.Format(lang.GetMessage("Rewarded", this, attacker.UserIDString),
                                    _config.Info.ChatName,
                                    item.info.displayName.english + " x " + item.amount));
                        }
                        else
                        {
                            PrintError($"Failed to create item...{_config.RewardInfo.RewardFixing}");
                        }

                        break;
                }
            }

            _eventstart?.Destroy();
            _eventstart = null;
            _runningman = null;
            Runlog("timer eventstart stopped");
            _eventpause?.Destroy();

            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUi(player);
                DestroyCountdownUi(player);
            }

            _eventpause = timer.Once(60 * _config.Info.PauseeventTime, () => Startevent());
            _LastStartedEvent = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        #endregion

        #region General function
        private bool HasAccess(BasePlayer player, string permissionName)
        {
            if (player.net.connection.authLevel > 1) return true;
            return permission.UserHasPermission(player.userID.ToString(), permissionName);
        }

        private void CheckTime()
        {
            if (DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds >= _config.TimeRange.StartWarTime &&
                DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds < 24 ||
                DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds >= 0 &&
                DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds < _config.TimeRange.EndWarTime)
            {
                if (!_eventStarted)
                {
                    _eventpause = timer.Once(60 * _config.Info.PauseeventTime, () => Startevent());
                    _eventStarted = true;
                }
            }
            else
            {
                if (_eventStarted)
                {
                    _eventStarted = false;
                    DestroyEvent();
                }

                _eventpause = timer.Once(20, CheckTime);
            }
        }

        private void LoadSavedData()
        {
            _savedReward = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, Dictionary<string, RewardData>>>(Name);
            if (_savedReward.Count == 0)
            {
                _savedReward["runner"] = new Dictionary<string, RewardData>
                {
                    {
                        "Karma", new RewardData
                        {
                            RewardItems = new Dictionary<string, ValueAmount>
                            {
                                {
                                    "Karma", new ValueAmount
                                    {
                                        MinValue = 0,
                                        MaxValue = 1
                                    }
                                }
                            }
                        }
                    },
                    {
                        "ServerReward", new RewardData
                        {
                            RewardItems = new Dictionary<string, ValueAmount>
                            {
                                {
                                    "serverreward", new ValueAmount
                                    {
                                        MinValue = 0,
                                        MaxValue = 1
                                    }
                                }
                            }
                        }
                    },
                    {
                        "build", new RewardData
                        {
                            RewardItems = new Dictionary<string, ValueAmount>
                            {
                                {
                                    "wood", new ValueAmount
                                    {
                                        MinValue = 1000,
                                        MaxValue = 10000
                                    }
                                },
                                {
                                    "stones", new ValueAmount
                                    {
                                        MinValue = 1000,
                                        MaxValue = 10000
                                    }
                                }
                            }
                        }
                    }
                };
                _savedReward["killer"] = new Dictionary<string, RewardData>
                {
                    {
                        "Karma", new RewardData
                        {
                            RewardItems = new Dictionary<string, ValueAmount>
                            {
                                {
                                    "Karma", new ValueAmount
                                    {
                                        MinValue = 0,
                                        MaxValue = 1
                                    }
                                }
                            }
                        }
                    },
                    {
                        "ServerReward", new RewardData
                        {
                            RewardItems = new Dictionary<string, ValueAmount>
                            {
                                {
                                    "serverreward", new ValueAmount
                                    {
                                        MinValue = 0,
                                        MaxValue = 1
                                    }
                                }
                            }
                        }
                    },
                    {
                        "build", new RewardData
                        {
                            RewardItems = new Dictionary<string, ValueAmount>
                            {
                                {
                                    "wood", new ValueAmount
                                    {
                                        MinValue = 1000,
                                        MaxValue = 10000
                                    }
                                },
                                {
                                    "stones", new ValueAmount
                                    {
                                        MinValue = 1000,
                                        MaxValue = 10000
                                    }
                                }
                            }
                        }
                    }
                };
                PrintWarning("Failed to load data file, generating a new one...");
            }

            SaveLoadedData();
        }

        private void SaveLoadedData()
        {
            try
            {
                Interface.Oxide.DataFileSystem.WriteObject(Name, _savedReward);
            }
            catch (Exception)
            {
                PrintWarning("Failed to save data file.");
            }
        }

        private void Startevent(string playerId = null)
        {
            if (_eventpause != null)
            {
                _eventpause.Destroy();
                _runningman = null;
            }

            if (_eventstart != null)
            {
                _eventstart.Destroy();
                _runningman = null;
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUi(player);
            }

            if (BasePlayer.activePlayerList != null)
            {

                if (playerId != null)
                {
                    var player = BasePlayer.Find(playerId);

                    if (player != null)
                    {
                        _runningman = player;

                        Runlog("Running man: " + _runningman.displayName);
                        BroadcastChat(string.Format(lang.GetMessage("StartEventRunner", this), _config.Info.ChatName, _runningman.displayName));

                        _eventstart = timer.Once(60 * _config.Info.StarteventTime, Runningstop);
                        _LastStartedEvent = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                        return;
                    }
                    else
                    {
                        BroadcastChat(string.Format(lang.GetMessage("NotEnoughPlayers", this), _config.Info.ChatName));
                        _eventpause?.Destroy();
                        _eventpause = timer.Once(60 * _config.Info.PauseeventTime, () => Startevent());
                        _LastStartedEvent = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                        return;
                    }
                }

                var auth = _config.Info.Excludedauthlevel;
                var t = auth == 0 ? BasePlayer.activePlayerList : BasePlayer.activePlayerList.Where(x => x.net.connection.authLevel < auth);
                var enumerable = t.ToList();

                if (enumerable.Count >= _config.Info.Count && enumerable.Count > 0)
                {
                    var basePlayers = t as BasePlayer[] ?? enumerable.ToArray();
                    var randI = _rnd.Next(0, basePlayers.Length);
                    _runningman = basePlayers[randI];

                    Runlog("Running man: " + _runningman.displayName);
                    BroadcastChat(string.Format(lang.GetMessage("StartEventRunner", this), _config.Info.ChatName, _runningman.displayName));

                    _eventstart = timer.Once(60 * _config.Info.StarteventTime, Runningstop);
                    _LastStartedEvent = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                }
                else
                {
                    BroadcastChat(string.Format(lang.GetMessage("NotEnoughPlayers", this), _config.Info.ChatName));
                    _eventpause?.Destroy();
                    _eventpause = timer.Once(60 * _config.Info.PauseeventTime, () => Startevent());
                    _LastStartedEvent = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                }
            }
        }

        private void Runningstop()
        {
            Runlog("Running man - " + _runningman.displayName + " ran away from the chase and received as a reward!");

            BroadcastChat(string.Format(lang.GetMessage("RunnerSaved", this), _config.Info.ChatName, _runningman.displayName));

            var inv = _runningman.inventory;
            if (_config.RewardInfo.Random)
            {
                if (_savedReward?["runner"] == null)
                {
                    PrintWarning("Reward list is empty, please add items");
                    inv?.GiveItem(ItemManager.CreateByName(_config.RewardInfo.RewardFixing, _config.RewardInfo.RewardFixingAmount), inv.containerMain);
                    return;
                }

                Runlog("random");
                var rand = _savedReward["runner"].ElementAt(_rnd.Next(0, _savedReward["runner"].Count));
                foreach (var data in rand.Value.RewardItems)
                {
                    var randomReward = _rnd.Next(data.Value.MinValue, data.Value.MaxValue);
                    switch (data.Key.ToLower())
                    {
                        case "karma":
                            if (KarmaSystem != null && KarmaSystem.IsLoaded)
                            {
                                var player = covalence.Players.FindPlayerById(_runningman.UserIDString);
                                KarmaSystem.Call("AddKarma", player, (double)randomReward);
                                SendReply(_runningman,
                                    string.Format(lang.GetMessage("Rewarded", this, _runningman.UserIDString),
                                        _config.Info.ChatName, data.Key + " x " + randomReward));
                            }
                            else
                            {
                                inv?.GiveItem(
                                    ItemManager.CreateByName(_config.RewardInfo.RewardFixing,
                                        _config.RewardInfo.RewardFixingAmount), inv.containerMain);
                                SendReply(_runningman, string.Format(
                                    lang.GetMessage("Rewarded", this, _runningman.UserIDString),
                                    _config.Info.ChatName, $"{_config.RewardInfo.RewardFixing} x {_config.RewardInfo.RewardFixingAmount}"));
                            }

                            break;
                        case "money":
                            if (Economics != null && Economics.IsLoaded)
                            {
                                Economics?.CallHook("Deposit", _runningman.userID,
                                    randomReward);
                                SendReply(_runningman,
                                    string.Format(lang.GetMessage("Rewarded", this, _runningman.UserIDString),
                                        _config.Info.ChatName, "money x " + randomReward));
                            }
                            else
                            {
                                inv?.GiveItem(
                                    ItemManager.CreateByName(_config.RewardInfo.RewardFixing,
                                        _config.RewardInfo.RewardFixingAmount), inv.containerMain);
                                SendReply(_runningman, string.Format(
                                    lang.GetMessage("Rewarded", this, _runningman.UserIDString),
                                    _config.Info.ChatName, _config.RewardInfo.RewardFixing +
                                                                            " x " +
                                                                            _config.RewardInfo.RewardFixingAmount));
                            }

                            break;
                        case "serverreward":
                            if (ServerRewards != null && ServerRewards.IsLoaded)
                            {
                                ServerRewards?.CallHook("AddPoints", _runningman.userID, randomReward);
                                SendReply(_runningman,
                                    string.Format(lang.GetMessage("Rewarded", this, _runningman.UserIDString),
                                        _config.Info.ChatName,
                                        "ServerRewards points x " + randomReward));
                            }
                            else
                            {
                                inv?.GiveItem(
                                    ItemManager.CreateByName(_config.RewardInfo.RewardFixing,
                                        _config.RewardInfo.RewardFixingAmount), inv.containerMain);
                                SendReply(_runningman, string.Format(
                                    lang.GetMessage("Rewarded", this, _runningman.UserIDString),
                                    _config.Info.ChatName, _config.RewardInfo.RewardFixing +
                                                                            " x " +
                                                                            _config.RewardInfo.RewardFixingAmount));
                            }

                            break;
                        default:
                            var item = ItemManager.CreateByName(data.Key,
                                randomReward);
                            if (item != null)
                            {
                                inv?.GiveItem(item, inv.containerMain);
                                SendReply(_runningman,
                                    string.Format(lang.GetMessage("Rewarded", this, _runningman.UserIDString),
                                        _config.Info.ChatName,
                                        item.info.displayName.english + " x " + item.amount));
                            }
                            else
                            {
                                PrintError($"Failed to create item...{rand.Key}");
                            }

                            break;
                    }
                }
            }
            else
            {
                inv?.GiveItem(ItemManager.CreateByName(_config.RewardInfo.RewardFixing,
                    _config.RewardInfo.RewardFixingAmount), inv.containerMain);
            }

            _eventstart.Destroy();
            _eventstart = null;
            _runningman = null;
            Runlog("timer eventstart stopped");
            _eventpause?.Destroy();

            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUi(player);
                DestroyCountdownUi(player);
            }

            _eventpause = timer.Once(60 * _config.Info.PauseeventTime, () => Startevent());
            _LastStartedEvent = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        private void BroadcastChat(string msg = null)
        {
            foreach (var player in BasePlayer.activePlayerList) SendReply(player, msg ?? " ", "");
        }

        private void Runlog(string text)
        {
            Puts("[EVENT] +--------------- RUNNING MAN -----------------");
            Puts("[EVENT] | " + text);
            Puts("[EVENT] +---------------------------------------------");
        }

        private void SendHelpText(BasePlayer player)
        {
            player.ChatMessage(lang.GetMessage("RunCommandHelp", this, player.UserIDString));
            var authlevel = player.net.connection.authLevel;
            if (authlevel >= _config.Info.AuthLevel)
            {
                player.ChatMessage(lang.GetMessage("AdminCommandHelp", this, player.UserIDString));
                player.ChatMessage(lang.GetMessage("AdminAddCommandHelp", this, player.UserIDString));
                player.ChatMessage(lang.GetMessage("AdminRemoveCommandHelp", this, player.UserIDString));
            }
        }

        private void DestroyEvent()
        {
            if (_eventpause != null)
            {
                _eventpause.Destroy();
                _eventpause = null;
                _runningman = null;
                Runlog("timer eventpause stopped");
            }

            if (_eventstart != null)
            {
                _eventstart.Destroy();
                _eventstart = null;
                _runningman = null;
                Runlog("timer eventstart stopped");
            }

            _ingameTimer?.Destroy();
            _compassRefresh?.Destroy();
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUi(player);
                DestroyCountdownUi(player);
            }
        }

        private void DestroyLeaveEvent()
        {
            if (_runningman != null)
            {
                Runlog("Player " + _runningman.displayName + " got scared and ran away!");
                BroadcastChat(string.Format(lang.GetMessage("RunnerLeaved", this),
                    _config.Info.ChatName, _runningman.displayName));
            }

            if (_eventpause != null)
            {
                _eventpause.Destroy();
                _eventpause = null;
                _runningman = null;
                Runlog("timer eventpause stopped");
            }

            if (_eventstart != null)
            {
                _eventstart.Destroy();
                _eventstart = null;
                _runningman = null;
                Runlog("timer eventstart stopped");
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUi(player);
            }

            Runlog("Running Man has stopped");
            _eventpause = timer.Once(60 * _config.Info.PauseeventTime, () => Startevent());
            _LastStartedEvent = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        #endregion

        #region Commands function
        [ChatCommand("run")]
        private void CmdRun(BasePlayer player, string cmd, string[] args)
        {
            if (!player)
                return;
            if (_runningman != null)
            {
                var xr = _runningman.transform.position.x;
                var zr = _runningman.transform.position.z;
                var xk = player.transform.position.x;
                var zk = player.transform.position.z;
                var dist = Math.Floor(Math.Sqrt(Math.Pow(xr - xk, 2) + Math.Pow(zr - zk, 2)));
                if (_config.Info.DisplayDistance)
                    SendReply(player,
                        string.Format(lang.GetMessage("RunnerDistance", this, player.UserIDString),
                            _config.Info.ChatName, _runningman.displayName, dist));
                else
                    SendReply(player,
                        string.Format(lang.GetMessage("RunnerDistance", this, player.UserIDString),
                            _config.Info.ChatName, _runningman.displayName, "unknown"));
                _time2 = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                var time3 = _time2 - _LastStartedEvent;
                time3 = _eventstart.Delay - time3;
                time3 = Math.Floor(time3 / 60);
                SendReply(player,
                    string.Format(lang.GetMessage("UntilEndOfEvent", this, player.UserIDString),
                        _config.Info.ChatName, time3));
            }
            else
            {
                _time2 = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

                var time3 = _time2 - _LastStartedEvent;
                if (_eventpause != null)
                {
                    time3 = _eventpause.Delay - time3;
                    time3 = Math.Floor(time3 / 60);
                    SendReply(player,
                        string.Format(lang.GetMessage("NotRunningEvent", this, player.UserIDString),
                            _config.Info.ChatName));
                    SendReply(player,
                        string.Format(lang.GetMessage("UntilStartOfEvent", this, player.UserIDString),
                            _config.Info.ChatName, time3));
                }
                else
                {
                    SendReply(player,
                        string.Format(lang.GetMessage("NotRunningEvent", this, player.UserIDString),
                            _config.Info.ChatName));
                }
            }
        }

        [ChatCommand("eventon")]
        private void CmdEvent(BasePlayer player, string cmd, string[] args)
        {
            if (!HasAccess(player, "runningman.admin"))
            {
                SendReply(player,
                    string.Format(lang.GetMessage("NoPerm", this, player.UserIDString), _config.Info.ChatName));
                return;
            }

            if (_config.TimeRange.TimeRange)
            {
                _ingameTimer = timer.Once(20, CheckTime);
                if (DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds >= _config.TimeRange.StartWarTime &&
                    DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds < 24 ||
                    DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds >= 0 &&
                    DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds < _config.TimeRange.EndWarTime)
                {
                    _time2 = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

                    var time3 = _time2 - _LastStartedEvent;
                    _NextEvent = _eventpause.Delay - time3;
                }
                else
                {
                    _NextEvent = new DateTime(DateTime.Now.Year, DateTime.Now.Month, DateTime.Now.Day,
                                         _config.TimeRange.StartWarTime, 0, 0).Subtract(new DateTime(1970, 1, 1))
                                     .TotalSeconds - DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                }
            }
            else
            {
                if(args.Length == 1)
                    Startevent(args[0]);
                else
                    Startevent();
                _LastStartedEvent = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            }
        }

        [ConsoleCommand("eventon")]
        private void CcmdEvent(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return;
            if (arg.Player().net.connection.authLevel >= _config.Info.AuthLevel)
            {
                if (_eventpause != null)
                {
                    _eventpause.Destroy();
                    _runningman = null;
                    Runlog("timer eventpause stopped");
                }

                if (_eventstart != null)
                {
                    _eventstart.Destroy();
                    _runningman = null;
                    Runlog("timer eventstart stopped");
                }

                var onlineplayers = BasePlayer.activePlayerList;
                var randI = _rnd.Next(0, onlineplayers.Count);
                _runningman = onlineplayers[randI];
                Runlog("Running man: " + _runningman.displayName);
                BroadcastChat(string.Format(lang.GetMessage("StartEventRunner", this),
                    _config.Info.ChatName, _runningman.displayName));
                _eventstart = timer.Once(60 * _config.Info.StarteventTime, Runningstop);
                _LastStartedEvent = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
            }
            else
            {
                arg.ReplyWith(string.Format(lang.GetMessage("NoPerm", this, arg.Player().UserIDString),
                    _config.Info.ChatName));
            }
        }

        [ConsoleCommand("eventoff")]
        private void CmdEventOf(ConsoleSystem.Arg arg)
        {
            if (arg.Player() == null) return;
            if (arg.Player().net.connection.authLevel >= _config.Info.AuthLevel)
                DestroyEvent();
            else
                arg.ReplyWith(string.Format(lang.GetMessage("NoPerm", this, arg.Player().UserIDString),
                    _config.Info.ChatName));
        }

        [ChatCommand("eventoff")]
        private void CmdEventOff(BasePlayer player, string cmd, string[] args)
        {
            if (!HasAccess(player, "runningman.admin"))
            {
                SendReply(player,
                    string.Format(lang.GetMessage("NoPerm", this, player.UserIDString), _config.Info.ChatName));
                return;
            }

			DestroyEvent();
			
			Runlog("Running Man has stopped");
            SendReply(player,
                string.Format(lang.GetMessage("EventStopped", this, player.UserIDString),
                    _config.Info.ChatName));
        }

        [ChatCommand("running")]
        private void CmdChat(BasePlayer player, string cmd, string[] args)
        {
            if (!HasAccess(player, "runningman.admin"))
            {
                SendReply(player,
                    string.Format(lang.GetMessage("NoPerm", this, player.UserIDString), _config.Info.ChatName));
                return;
            }

            if (args == null)
            {
                SendHelpText(player);
                return;
            }

            string action;
            string package;
            string item;
            string type;

            if (args.Length >= 0 && args.Length <= 4)
            {
                SendHelpText(player);
                return;
            }

            if (args.Length < 5)
            {
                type = args[0].ToLower();
                action = args[1].ToLower();
                package = args[2].ToLower();
                item = args[3].ToLower();
                if (action == "remove")
                {
                    if (type == "k" || type == "killer")
                        type = "killer";
                    else
                        type = "runner";
                    switch (args.Length)
                    {
                        case 2:
                            if (_savedReward[type].ContainsKey(package))
                                _savedReward[type].Remove(package);
                            else
                                SendReply(player,
                                    string.Format(lang.GetMessage("PackageDontExist", this, player.UserIDString),
                                        _config.Info.ChatName));
                            break;
                        case 3:
                            if (_savedReward.ContainsKey(package))
                                if (_savedReward[type][package].RewardItems.ContainsKey(item))
                                {
                                    _savedReward[type][package].RewardItems.Remove(item);
                                    SendReply(player,
                                        string.Format(lang.GetMessage("ItemRemoved", this, player.UserIDString),
                                            _config.Info.ChatName, item));
                                }
                                else
                                {
                                    SendReply(player,
                                        string.Format(
                                            lang.GetMessage("MissingItemFromPackage", this, player.UserIDString),
                                            _config.Info.ChatName));
                                }
                            else
                                SendReply(player,
                                    string.Format(lang.GetMessage("PackageDontExist", this, player.UserIDString),
                                        _config.Info.ChatName));

                            break;
                    }
                }
                else
                {
                    SendHelpText(player);
                }
            }

            if (args.Length == 6)
            {
                type = args[0].ToLower();
                action = args[1].ToLower();
                package = args[2].ToLower();
                item = args[3].ToLower();
                var minamount = int.Parse(args[4]);
                var maxamount = int.Parse(args[5]);

                if (action == "add")
                {
                    if (type == "k" || type == "killer")
                        type = "killer";
                    else
                        type = "runner";
                    if (_savedReward[type].ContainsKey(package))
                    {
                        if (_savedReward[type][package].RewardItems.ContainsKey(item))
                        {
                            SendReply(player,
                                string.Format(lang.GetMessage("ItemExist", this, player.UserIDString),
                                    _config.Info.ChatName));
                            return;
                        }

                        _savedReward[type][package].RewardItems.Add(item, new ValueAmount
                        {
                            MinValue = minamount,
                            MaxValue = maxamount
                        });
                        SendReply(player,
                            string.Format(lang.GetMessage("ItemAdded", this, player.UserIDString),
                                _config.Info.ChatName, item, package));
                        SaveLoadedData();
                    }
                    else
                    {
                        _savedReward[type].Add(package, new RewardData
                        {
                            RewardItems = new Dictionary<string, ValueAmount>
                            {
                                {
                                    item, new ValueAmount
                                    {
                                        MinValue = minamount,
                                        MaxValue = maxamount
                                    }
                                }
                            }
                        });
                        SendReply(player,
                            string.Format(lang.GetMessage("PackageAdded", this, player.UserIDString),
                                _config.Info.ChatName, package));
                        SaveLoadedData();
                    }
                }
                else
                {
                    SendHelpText(player);
                }
            }
        }

        #endregion

        #region Compass/countdown UI

        private void DestroyUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, panelString);
        }

        private void RefreshUi()
        {
            foreach (var player in BasePlayer.activePlayerList)
                if (player != _runningman)
                {

                    if (_runningman == null && !_config.CompassUiInfo.Disabled)
                    {
                        DestroyUi(player);
                        CreateUi(player);
                    }
                    if (_runningman != null)
                    {
                        DestroyUi(player);
                        CreateUi(player);
                    }
                }
        }

        private void CreateUi(BasePlayer player)
        {
            var panel = new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = _config.CompassUiInfo.AnchorMin,
                            AnchorMax = _config.CompassUiInfo.AnchorMax
                        }
                    },
                    new CuiElement().Parent, panelString
                },
                {
                    new CuiLabel
                    {
                        RectTransform =
                        {
                            AnchorMax = "1 1",
                            AnchorMin = "0 0"
                        },
                        Text =
                        {
                            Text = GetDirection(player),
                            Align = TextAnchor.MiddleCenter,
                            Color = "0 0 0 1"
                        }
                    },
                    panelString
                }
            };

            CuiHelper.AddUi(player, panel);
        }

        private string GetDirection(BasePlayer player)
        {
            if (_runningman == null)
                return _config.CompassUiInfo.RunnerDirection.None;

            var vect = _runningman.transform.position;
            var pos = player.transform.position;

            if (pos.x < vect.x && pos.z < vect.z)
                return _config.CompassUiInfo.RunnerDirection.NorthEast;
            if (pos.x > vect.x && pos.z < vect.z)
                return _config.CompassUiInfo.RunnerDirection.NorthWest;
            if (pos.x < vect.x && pos.z > vect.z)
                return _config.CompassUiInfo.RunnerDirection.SouthEast;
            if (pos.x > vect.x && pos.z > vect.z)
                return _config.CompassUiInfo.RunnerDirection.SouthWest;


            return _config.CompassUiInfo.RunnerDirection.None;
        }

        private void CreateCountdownUi(BasePlayer player)
        {
            double time3;
            string texts = "";
            if (_runningman == null)
            {
                if (_config.TimeRange.TimeRange)
                {
                    if (DateTime.Now.Hour < _config.TimeRange.StartWarTime)
                    {
                        if (_eventpause != null)
                        {
                            time3 = _NextEvent - DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                            var t = TimeSpan.FromSeconds(time3);
                            string answer = t.Hours != 0 ? $"{t.Hours:D2}H:{t.Minutes:D2}m:{t.Seconds:D2}s" : $"{t.Minutes:D2}m:{t.Seconds:D2}s";
                            texts = $"Next event in\n{answer}";
                        }
                    }
                }
                else
                {
                    _time2 = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;

                    time3 = _time2 - _LastStartedEvent;

                    if (_eventpause != null)
                    {
                        time3 = _eventpause.Delay - time3;

                        var t = TimeSpan.FromSeconds(time3);

                        string answer = t.Hours != 0 ? $"{t.Hours:D2}H:{t.Minutes:D2}m:{t.Seconds:D2}s" : $"{t.Minutes:D2}m:{t.Seconds:D2}s";
                        texts = $"Next event in\n{answer}";
                    }
                }
            }
            else
            {
                if ((int)_LastStartedEvent == 0)
                    return;

                _time2 = DateTime.Now.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
                time3 = _time2 - _LastStartedEvent;
                if (_eventstart != null) time3 = _eventstart.Delay - time3;

                var t = TimeSpan.FromSeconds(time3);

                string answer = t.Hours != 0 ? $"{t.Hours:D2}H:{t.Minutes:D2}m:{t.Seconds:D2}s" : $"{t.Minutes:D2}m:{t.Seconds:D2}s";
                texts = $"Time left\n{answer}";
            }

            var panel = new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = _config.CountdownUiInfo.AnchorMin,
                            AnchorMax = _config.CountdownUiInfo.AnchorMax
                        }
                    },
                    new CuiElement().Parent, panelCountString
                }
            };


            panel.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMax = "1 1",
                    AnchorMin = "0 0"
                },
                Text =
                {
                    Text = texts,
                    Align = TextAnchor.MiddleCenter,
                    Color = "0 0 0 1"
                }
            }, panelCountString);

            if (_runningman == null && !_config.CountdownUiInfo.DisableNoRunner)
            {
                CuiHelper.AddUi(player, panel);
                return;
            }

            if (_runningman != null && !_config.CountdownUiInfo.DisableRunnerOn)
            {
                CuiHelper.AddUi(player, panel);
            }
        }

        private void DestroyCountdownUi(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, panelCountString);
        }

        private void RefreshCountdownUi()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyCountdownUi(player);
                CreateCountdownUi(player);
            }
        }

        #endregion
    }
}