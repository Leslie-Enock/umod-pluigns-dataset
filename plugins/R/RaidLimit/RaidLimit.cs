// Requires: ImageLibrary

using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins;
using Rust;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Raid Limit", "noname", "2.2.3")]
    [Description("Limits the number of raids per day on buildings")]
    class RaidLimit : CovalencePlugin
    {
        [PluginReference]
        Plugin ImageLibrary, PlaytimeTracker;

        private static RaidLimit Instance;

        private int MaskInt = LayerMask.GetMask("Construction", "Prevent Building", "Deployed");
        private const string raidlimit_admin_Perm = "raidlimit.admin";
        private const string raidlimit_bypass_Perm = "raidlimit.bypass";

        private List<ulong> PlayerSpamMessageBlockFlags;

        #region Hooks

        private new void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");

            Config.WriteObject(GetDefaultConfig(), true);
        }

        private void Init()
        {
            Instance = this;

            RegisterPermissions();
            LoadConfig();
            LoadPlayersData();
            ChargeDataCheck();
        }

        private void OnServerSave()
        {
            SavePlayersData();
        }

        private void Unload()
        {
            PlayersDestroyRaidLimitUI();
            SavePlayersData();

            Instance = null;
        }

        private void Loaded()
        {
            RegisterUICommand();
        }

        private void OnServerInitialized()
        {
            PlayerSpamMessageBlockFlags = new List<ulong>();
            playersDateTimeData = new PlayersDateTimeData();
            PlayersAddToData();
            PlayersUpdateNameData();

            LoadImage();
            PlayersUpdateRaidLimitUI(true);

            StartTimer();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsNpc) return;

            PlayerAddToData(player);
            PlayerUpdateNameData(player);

            timer.Once(4f, () =>
            {
                PlayerAddToData(player);
                PlayerUpdateNameData(player);
                PlayerUpdateRaidLimitUI(player, true);
            });
        }

        private void OnUserGroupAdded(string id, string groupName)
        {
            if (!permission.GroupHasPermission(groupName, raidlimit_bypass_Perm))
                return;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.UserIDString == id)
                    PlayerUpdateRaidLimitUI(player, false);
            }
        }

        private void OnUserGroupRemoved(string id, string groupName)
        {
            if (!permission.GroupHasPermission(groupName, raidlimit_bypass_Perm))
                return;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.UserIDString == id)
                    PlayerUpdateRaidLimitUI(player, false);
            }
        }

        private void OnUserPermissionGranted(string id, string permName)
        {
            if (permName != raidlimit_bypass_Perm)
                return;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.UserIDString == id)
                    PlayerUpdateRaidLimitUI(player, false);
            }
        }

        private void OnUserPermissionRevoked(string id, string permName)
        {
            if (permName != raidlimit_bypass_Perm)
                return;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.UserIDString == id)
                    PlayerUpdateRaidLimitUI(player, false);
            }
        }

        private void OnGroupPermissionGranted(string name, string perm)
        {
            if (perm != raidlimit_bypass_Perm)
                return;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasGroup(player.UserIDString, name))
                    PlayerUpdateRaidLimitUI(player, false);
            }
        }

        private void OnGroupPermissionRevoked(string name, string perm)
        {
            if (perm != raidlimit_bypass_Perm)
                return;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (permission.UserHasGroup(player.UserIDString, name))
                    PlayerUpdateRaidLimitUI(player, false);
            }
        }

        private void OnGroupDeleted(string name)
        {
            PlayersUpdateRaidLimitUI(false);
        }

        #endregion

        #region PluginIO

        #region ConfigManage

        private PluginConfig config;

        private void LoadConfig()
        {
            config = Config.ReadObject<PluginConfig>();

            if (config == null)
                config = GetDefaultConfig();
            else
                CheckMissingVariable();
        }

        private class PluginConfig
        {
            [JsonProperty("1.RaidLimitOperationType")]
            public RaidLimitOperationType RaidLimitOperationType;
            [JsonProperty("2.RaidLimitSettings")]
            public RaidLimitSettings RaidLimitSettings;
            [JsonProperty("3.UISettings")]
            public UISettings UISettings;
            [JsonProperty("4.TeamSyncSettings")]
            public TeamSyncSettings TeamSyncSettings;
        }

        private class RaidLimitOperationType
        {
            [JsonProperty("1.1.OperationType")]
            public int OperationType;
            [JsonProperty("1.2.ObjectOwnerIdentification")]
            public ObjectOwnerIdentification ObjectOwnerIdentification;
            [JsonProperty("1.3.ToolCupboardIdentification")]
            public ToolCupboardIdentification ToolCupboardIdentification;
        }

        private class ObjectOwnerIdentification
        {
            [JsonProperty("1.2.1.ObjectSearchDepth")]
            public int ObjectSearchDepth;
            [JsonProperty("1.2.2.ObjectSearchRange")]
            public int ObjectSearchRange;
        }

        private class ToolCupboardIdentification
        {
            [JsonProperty("1.3.1.ObjectSearchDepth")]
            public int ObjectSearchDepth;
            [JsonProperty("1.3.2.ObjectSearchRange")]
            public int ObjectSearchRange;
            [JsonProperty("1.3.3.CheckToolCupboardInstanceID")]
            public bool CheckToolCupboardInstanceID;
            [JsonProperty("1.3.4.CheckAuthorizedPeoples")]
            public bool CheckAuthorizedPeoples;
        }

        private class RaidLimitSettings
        {
            [JsonProperty("2.1.OneTimeMaximumRaidableHomeCount")]
            public int? OneTimeMaximumRaidableHomeCount;
            [JsonProperty("2.2.NoobCantRaidSecond")]
            public int? NoobCantRaidSecond;
            [JsonProperty("2.3.InitializeCounterOnMidnightTime")]
            public bool? InitializeCounterOnMidnightTime;
            [JsonProperty("2.4.MidnightTimeDetectionTimerInterval")]
            public int? MidnightTimeDetectionTimerInterval;
            [JsonProperty("2.5.CounterChargeDelay")]
            public int? CounterChargeDelay;
            [JsonProperty("2.6.CounterChargeDelayType")]
            public int? CounterChargeDelayType;
            [JsonProperty("2.7.CounterChargeType")]
            public int? CounterChargeType;
        }

        private class UISettings
        {
            [JsonProperty("3.1.UIEnable")]
            public bool UIEnable;
            [JsonProperty("3.2.UIUpdateInterval")]
            public int UIUpdateInterval;
            [JsonProperty("3.3.UIPosition")]
            public UIPosition UIPosition;
        }

        private class UIPosition
        {
            [JsonProperty("3.3.1.AnchorMin")]
            public string AnchorMin;
            [JsonProperty("3.3.2.AnchorMax")]
            public string AnchorMax;
        }

        private class TeamSyncSettings
        {
            [JsonProperty("4.1.TeamCounterSync")]
            public bool TeamCounterSync;
            [JsonProperty("4.2.PreventTempDisband")]
            public bool PreventTempDisband;
            [JsonProperty("4.3.OldTeamSaveInterval")]
            public int OldTeamSaveInterval;
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                RaidLimitOperationType = new RaidLimitOperationType()
                {
                    OperationType = 0,

                    ObjectOwnerIdentification = new ObjectOwnerIdentification()
                    {
                        ObjectSearchDepth = -1,
                        ObjectSearchRange = 5
                    },

                    ToolCupboardIdentification = new ToolCupboardIdentification()
                    {
                        ObjectSearchDepth = -1,
                        ObjectSearchRange = 5,
                        CheckToolCupboardInstanceID = true,
                        CheckAuthorizedPeoples = true
                    }
                },

                RaidLimitSettings = new RaidLimitSettings()
                {
                    OneTimeMaximumRaidableHomeCount = 2,
                    NoobCantRaidSecond = 10800,//3h
                    InitializeCounterOnMidnightTime = true,
                    CounterChargeDelay = 43200,//12h  -1 == disable
                    CounterChargeDelayType = 0,//0 == realtime, 1 == playtime
                    CounterChargeType = 0//0 == Charge when used up, 1 == Charge if there is not enough
                },

                UISettings = new UISettings()
                {
                    UIEnable = true,
                    UIUpdateInterval = 3,

                    UIPosition = new UIPosition()
                    {
                        AnchorMin = "0.28 0.025",
                        AnchorMax = "0.3392 0.06"
                    }
                },

                TeamSyncSettings = new TeamSyncSettings()
                {
                    TeamCounterSync = true,
                    PreventTempDisband = true,
                    OldTeamSaveInterval = 600
                }
            };
        }

        private void CheckMissingVariable()
        {
            bool Missed = false;

            if (config.RaidLimitSettings.OneTimeMaximumRaidableHomeCount == null)
            {
                config.RaidLimitSettings.OneTimeMaximumRaidableHomeCount = 2;
                Missed = true;
            }

            if (config.RaidLimitSettings.NoobCantRaidSecond == null)
            {
                config.RaidLimitSettings.NoobCantRaidSecond = 10800;
                Missed = true;
            }

            if (config.RaidLimitSettings.InitializeCounterOnMidnightTime == null)
            {
                config.RaidLimitSettings.InitializeCounterOnMidnightTime = true;
                Missed = true;
            }

            if (config.RaidLimitSettings.MidnightTimeDetectionTimerInterval == null)
            {
                config.RaidLimitSettings.MidnightTimeDetectionTimerInterval = 10;
                Missed = true;
            }

            if (config.RaidLimitSettings.CounterChargeDelay == null)
            {
                config.RaidLimitSettings.CounterChargeDelay = 43200;
                Missed = true;
            }

            if (config.RaidLimitSettings.CounterChargeDelayType == null)
            {
                config.RaidLimitSettings.CounterChargeDelayType = 0;
                Missed = true;
            }

            if (config.RaidLimitSettings.CounterChargeType == null)
            {
                config.RaidLimitSettings.CounterChargeType = 0;
                Missed = true;
            }

            if (Missed)
                Config.WriteObject(config, true);
        }

        #endregion

        #region DataManage

        DynamicConfigFile playersdataFile;
        PlayersData playersData;

        DynamicConfigFile playersuidataFile;
        PlayersUIData playersUIData;

        DynamicConfigFile playersteamdataFile;
        PlayersTeamData playersTeamData;

        private void LoadPlayersData()
        {
            playersdataFile = Interface.Oxide.DataFileSystem.GetDatafile("RaidLimitPlayerData");
            playersData = playersdataFile.ReadObject<PlayersData>();

            if (playersData == null)
                playersData = new PlayersData();

            playersuidataFile = Interface.Oxide.DataFileSystem.GetDatafile("RaidLimitPlayerUIData");
            playersUIData = playersuidataFile.ReadObject<PlayersUIData>();

            if (playersUIData == null)
                playersUIData = new PlayersUIData();

            playersteamdataFile = Interface.Oxide.DataFileSystem.GetDatafile("RaidLimitPlayerTeamData");
            playersTeamData = playersuidataFile.ReadObject<PlayersTeamData>();

            if (playersTeamData == null)
                playersTeamData = new PlayersTeamData();
        }

        private void ChargeDataCheck()
        {
            foreach (var item in playersData.Players)
            {
                PlayerInfo playerinfo = item.Value;

                if (config.RaidLimitSettings.CounterChargeDelay != -1)
                {
                    switch (config.RaidLimitSettings.CounterChargeType)
                    {
                        case 0:
                            if (playerinfo.RaidLeftCount == 0 && playerinfo.Charging == false)
                                playerinfo.AddChargeSchedule();
                            break;

                        case 1:
                            if (playerinfo.RaidLeftCount < config.RaidLimitSettings.OneTimeMaximumRaidableHomeCount && playerinfo.Charging == false)
                                playerinfo.AddChargeSchedule();
                            break;

                        default:
                            goto case 0;
                    }
                }
            }
        }

        private void SavePlayersData()
        {
            Puts("Saving RaidLimit PlayersDataFile...");
            playersdataFile.WriteObject<PlayersData>(playersData);
            playersuidataFile.WriteObject<PlayersUIData>(playersUIData);
            playersteamdataFile.WriteObject<PlayersTeamData>(playersTeamData);
        }

        private class PlayersTeamData
        {
            public Dictionary<ulong, PlayerTeam> TeamsData1;
            public Dictionary<ulong, PlayerTeam> TeamsData2;
            public int OldDataIndicator;

            public PlayersTeamData()
            {
                TeamsData1 = new Dictionary<ulong, PlayerTeam>();
                TeamsData2 = new Dictionary<ulong, PlayerTeam>();

                OldDataIndicator = 2;
            }

            public void UpdateData()
            {
                Dictionary<ulong, PlayerTeam> OldTeamsData;
                if (OldDataIndicator == 1)
                    OldTeamsData = TeamsData2;
                else
                    OldTeamsData = TeamsData1;

                OldTeamsData.Clear();
                foreach (var item in RelationshipManager.ServerInstance.teams)
                {
                    OldTeamsData.Add(item.Key, new PlayerTeam(item.Value.members));
                }

                if (OldDataIndicator == 1)
                    OldDataIndicator = 2;
                else
                    OldDataIndicator = 1;
            }
        }

        private class PlayerTeam
        {
            public List<ulong> Members { get; set; }

            public PlayerTeam()
            {
                Members = new List<ulong>();
            }

            public PlayerTeam(List<ulong> members)
            {
                Members = new List<ulong>(members);
            }
        }

        private class PlayersUIData
        {
            public Dictionary<string, bool> PlayersUIToggle;

            public PlayersUIData()
            {
                PlayersUIToggle = new Dictionary<string, bool>();
            }

            public void AddPlayer(BasePlayer player)
            {
                if (!PlayersUIToggle.ContainsKey(player.UserIDString) && player != null)
                {
                    PlayersUIToggle.Add(player.UserIDString, true);
                }
            }
        }

        private class PlayersData
        {
            public DateTime excutteddate;
            public Dictionary<string, PlayerInfo> Players;

            public PlayersData()
            {
                excutteddate = DateTime.MinValue;
                Players = new Dictionary<string, PlayerInfo>();
            }

            public void AddPlayer(string playerId)
            {
                if (!Players.ContainsKey(playerId) && playerId != null)
                {
                    Players.Add(playerId, new PlayerInfo(playerId));
                }
            }

            public void AddPlayer(BasePlayer player)
            {
                if (!Players.ContainsKey(player.UserIDString) && player != null)
                {
                    Players.Add(player.UserIDString, new PlayerInfo(player.IPlayer));
                }
            }

            public void PlayerAddTime(string PlayerID, TimeSpan time)
            {
                if (Players.ContainsKey(PlayerID))
                {
                    Players[PlayerID].Playtime = Players[PlayerID].Playtime.Add(time);
                }
            }
        }

        private class PlayerInfo
        {
            public string Id;
            public string Name;
            public int RaidLeftCount;
            public List<RaidCountItem> RaidCountItems;

            public bool Charging;
            public TimeSpan NextChargePlaytime;
            public DateTime NextChargeRealtime;

            public TimeSpan Playtime;
            public bool NoobCanRaidPlaytimeTracker;
            public bool NoobCanRaid;

            public PlayerInfo()
            {
                //for json deserialize
            }

            public PlayerInfo(string playerId)
            {
                Id = playerId;
                Name = null;
                RaidLeftCount = 0;
                RaidCountItems = new List<RaidCountItem>();

                Charging = false;
                NextChargePlaytime = new TimeSpan(0, 0, 0);
                NextChargeRealtime = DateTime.MinValue;

                Playtime = new TimeSpan(0, 0, 0);
                NoobCanRaidPlaytimeTracker = false;
                NoobCanRaid = false;
            }

            public PlayerInfo(IPlayer player)
            {
                Id = player.Id;
                Name = player.Name;
                RaidLeftCount = 0;
                RaidCountItems = new List<RaidCountItem>();

                Charging = false;
                NextChargePlaytime = new TimeSpan(0, 0, 0);
                NextChargeRealtime = DateTime.MinValue;

                Playtime = new TimeSpan(0, 0, 0);
                NoobCanRaidPlaytimeTracker = false;
                NoobCanRaid = false;
            }

            public bool AddRaidTarget(RaidCountItem raidCountItem)
            {
                if (RaidLeftCount <= 0)
                    return false;

                RaidLeftCount--;
                RaidCountItems.Add(raidCountItem);
                return true;
            }

            public void AddChargeSchedule()
            {
                TimeSpan CounterChargeDelay = TimeSpan.FromSeconds(Instance.config.RaidLimitSettings.CounterChargeDelay.Value);

                NextChargePlaytime = Instance.GetPlayerPlaytime(Id) + CounterChargeDelay;
                NextChargeRealtime = DateTime.Now + CounterChargeDelay;

                Charging = true;
            }

            public void StopCharging()
            {
                Charging = false;
            }

            public void ChargeRaidCount(bool chargeAll)
            {
                if (Instance.config.RaidLimitSettings.OneTimeMaximumRaidableHomeCount <= RaidLeftCount)
                    return;

                if (chargeAll)
                    RaidLeftCount = Instance.config.RaidLimitSettings.OneTimeMaximumRaidableHomeCount.Value;
                else
                    RaidLeftCount++;

                if (Instance.config.RaidLimitSettings.InitializeCounterOnMidnightTime == false)
                {
                    if (chargeAll)
                        RaidCountItems.Clear();
                    else
                        RaidCountItems.RemoveAt(RaidCountItems.Count - 1);
                }
            }

            public int AddRaidCount(int amount)
            {
                int CountSave = RaidLeftCount;

                RaidLeftCount += amount;

                if (RaidLeftCount < 0)
                    RaidLeftCount = 0;
                else if (Instance.config.RaidLimitSettings.OneTimeMaximumRaidableHomeCount < RaidLeftCount)
                    RaidLeftCount = Instance.config.RaidLimitSettings.OneTimeMaximumRaidableHomeCount.Value;


                for (int i = 0; i < amount; i++)
                {
                    if (RaidCountItems.Count <= 0)
                        break;
                    RaidCountItems.RemoveAt(RaidCountItems.Count - 1);
                }

                return RaidLeftCount - CountSave;
            }

            public void ResetRaidDataNCount()
            {
                RaidLeftCount = Instance.config.RaidLimitSettings.OneTimeMaximumRaidableHomeCount.Value;
                RaidCountItems.Clear();
            }
        }

        private class RaidCountItem
        {
            public List<ulong> RaidTargets;
            public List<int> TCInstanceIDs;

            public RaidCountItem()
            {
                //for json deserialize
            }

            public RaidCountItem(List<ulong> raidTargets, List<int> tcInstanceIDs)
            {
                RaidTargets = raidTargets;
                TCInstanceIDs = tcInstanceIDs;
            }

            public void AddRaidTargetsRange(List<ulong> raidTargets)
            {
                foreach (var item in raidTargets)
                {
                    if (RaidTargets.Contains(item) == false)
                        RaidTargets.Add(item);
                }
            }

            public void AddTCInstanceIDsRange(List<int> tcInstanceIDs)
            {
                foreach (var item in tcInstanceIDs)
                {
                    if (TCInstanceIDs.Contains(item) == false)
                        TCInstanceIDs.Add(item);
                }
            }
        }

        #endregion

        #region PermissionManage

        private void RegisterPermissions()
        {
            permission.RegisterPermission(raidlimit_admin_Perm, this);
            permission.RegisterPermission(raidlimit_bypass_Perm, this);
        }

        #endregion

        #region LangManage

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "You do not have permission to use the '{0}' command.",
                ["Date Changed"] = "The day has changed. The raid limit has been initialized.",
                ["You Can Raid"] = "You have enough play time to raid.",
                ["Add RaidList"] = "The owner of this building has been added to the raid list. you can raid {0} more time.",
                ["Add RaidList TC"] = "The building has been added to the raid list. you can raid {0} more time.",
                ["Team Member Raiding"] = "Team member \"{0}\" is raiding. Raid limit counter reduced. The owner of the house where the team is raiding has been added to the raid list.",
                ["Left Raid Count"] = "You can raid {0} more time.",
                ["Raid Blocked"] = "You can't raid any more today.",
                ["Raid Blocked NoRefill"] = "You can't raid any more.",
                ["Raid Blocked RNextTime"] = "You can't raid right now. After {0} seconds in realtime, Raid count will be recharged.",
                ["Raid Blocked PNextTime"] = "You can't raid right now. After {0} seconds in playtime, Raid count will be recharged.",
                ["Raid Blocked Time"] = "You have to play the game for more than {0}seconds before you can raid it. Your play time is {1}seconds.",
                ["Raid Reset Specific Slayer"] = "Your raid limit has been refilled.",
                ["Raid List Reset"] = "The raid limit has been initialized.",
                ["bypass GUI Msg"] = "<color=#FFE400>bypass</color>",
                ["SteamID Not Found"] = "Could not find this SteamID: {0}.",
                ["Player Not Found"] = "Could not find this player: {0}.",
                ["Multiple Players Found"] = "Found multiple players!\n\n{0}",
                ["NotEnoughArgument"] = "to run this command you need {0} arguments.",
                ["Invalid Parameter"] = "'{0}' is an invalid parameter.",
                ["Count IncreasedC"] = "{0}'s raidlimit has increased by '{1}'.",
                ["Count DecreasedC"] = "{0}'s raidlimit has decreased by '{1}'.",
                ["Count IncreasedP"] = "raidlimit has increased by '{0}'.",
                ["Count DecreasedP"] = "raidlimit has decreased by '{0}'."
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "'{0}' 명령어를 사용하실 권한이 없습니다.",
                ["Date Changed"] = "날이 바뀌었습니다. 레이드 제한이 초기화 되었습니다.",
                ["You Can Raid"] = "플레이타임이 충분하므로 이제 레이드가 가능합니다.",
                ["Add RaidList"] = "이 건축물의 주인이 레이드 가능 목록에 추가되었습니다. 앞으로 {0}번 더 레이드 할 수 있습니다.",
                ["Add RaidList TC"] = "이 건묵물이 레이드 가능 목록에 추가되었습니다. 앞으로 {0}번 더 레이드 할 수 있습니다.",
                ["Team Member Raiding"] = "팀원 \"{0}\" 님이 레이드 중입니다. 레이드 제한이 감소하였습니다. 레이드 당하고 있는 건축물의 주인이 레이드 가능 목록에 추가되었습니다.",
                ["Left Raid Count"] = "{0}번 더 레이드 하실 수 있습니다.",
                ["Raid Blocked"] = "오늘은 더이상 레이드를 할 수 없습니다.",
                ["Raid Blocked NoRefill"] = "더이상 레이드를 할 수 없습니다.",
                ["Raid Blocked RNextTime"] = "더이상 레이드를 할 수 없습니다. 리얼타임을 기준으로 {0}초후에 레이드 제한 횟수가 다시 충전됩니다.",
                ["Raid Blocked PNextTime"] = "더이상 레이드를 할 수 없습니다. 플레이타임을 기준으로 {0}초후에 레이드 제한 횟수가 다시 충전됩니다.",
                ["Raid Blocked Time"] = "레이드를 하기위해선 플레이타임이 {0}초 이상이여야합니다. 현재 누적 플레이타임은 {1}초입니다.",
                ["Raid Reset Specific Slayer"] = "레이드 제한횟수가 재충전 되었습니다.",
                ["Raid List Reset"] = "레이드 제한횟수가 재충전 되었습니다.",
                ["bypass GUI Msg"] = "<color=#FFE400>무한</color>",
                ["SteamID Not Found"] = "{0} 와 일치하는 스팀 아이디를 가진 플레이어가 없습니다.",
                ["Player Not Found"] = "{0} 와 일치하는 이름을 가진 플레이어가 없습니다.",
                ["Multiple Players Found"] = "여러명의 플레이어를 검색했습니다!\n\n{0}",
                ["NotEnoughArgument"] = "이명령어를 실행하기 위해서는 {0} 개의 값이 필요합니다.",
                ["Invalid Parameter"] = "'{0}'는 유효하지 않은 파라미터 입니다.",
                ["Count IncreasedC"] = "{0}님의 레이드제한이 '{1}'만큼 증가하였습니다.",
                ["Count DecreasedC"] = "{0}님의 레이드제한이 '{1}'만큼 감소하였습니다.",
                ["Count IncreasedP"] = "레이드제한이 '{0}'만큼 증가하였습니다.",
                ["Count DecreasedP"] = "레이드제한이 '{0}'만큼 감소하였습니다."
            }, this, "ko");
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        #endregion

        #endregion

        #region TimerManager

        private void StartTimer()
        {
            if (config.TeamSyncSettings.PreventTempDisband == true)
                timer.Every(config.TeamSyncSettings.OldTeamSaveInterval, PreventTempDisband_Timer_Tick);

            if (config.RaidLimitSettings.InitializeCounterOnMidnightTime == true)
                timer.Every(config.RaidLimitSettings.MidnightTimeDetectionTimerInterval.Value, CheckDayChanged_Timer_Tick);

            timer.Every(config.UISettings.UIUpdateInterval, UIUpdate_Timer_Tick);
        }

        private void PreventTempDisband_Timer_Tick()
        {
            playersTeamData.UpdateData();
        }

        private void CheckDayChanged_Timer_Tick()
        {
            if (playersData.excutteddate.DayOfYear < DateTime.Now.DayOfYear || playersData.excutteddate.Year < DateTime.Now.Year)
            {
                Puts(Lang("Date Changed", null));
                foreach (IPlayer current in players.Connected)
                    current.Message(Lang("Date Changed", current.Id));

                foreach (var item in playersData.Players)
                {
                    item.Value.ResetRaidDataNCount();
                    item.Value.StopCharging();
                }
                SavePlayersData();
                PlayersUpdateRaidLimitUI(false);

                playersData.excutteddate = DateTime.Now;
            }
        }

        private void UIUpdate_Timer_Tick()//done
        {
            foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
            {
                PlayerInfo playerInfo = playersData.Players[basePlayer.UserIDString];
                TimeSpan playtime = GetPlayerPlaytime(basePlayer);

                if (GetCanRaid(playerInfo) == false)
                {
                    if (playtime.TotalSeconds > config.RaidLimitSettings.NoobCantRaidSecond)
                    {
                        if (PlaytimeTracker != null)
                            playerInfo.NoobCanRaidPlaytimeTracker = true;
                        else
                            playerInfo.NoobCanRaid = true;

                        playerInfo.ChargeRaidCount(true);

                        if (!permission.UserHasPermission(basePlayer.UserIDString, raidlimit_bypass_Perm))
                            basePlayer.ChatMessage(Lang("You Can Raid", basePlayer.UserIDString));
                        UpdatePlayerCountUI(basePlayer, false);
                    }
                }

                if (GetCanRaid(playerInfo) == false)
                {
                    UpdatePlayerNoobTimeUI(basePlayer, playtime, false);
                }
                else
                {
                    if (playerInfo.Charging && config.RaidLimitSettings.CounterChargeDelay != -1)
                    {
                        switch (config.RaidLimitSettings.CounterChargeDelayType)
                        {
                            case 0://realtume
                                switch (config.RaidLimitSettings.CounterChargeType)
                                {
                                    case 0://Charge when used up
                                        if (playerInfo.RaidLeftCount != 0)
                                            break;

                                        if (DateTime.Now >= playerInfo.NextChargeRealtime)
                                        {
                                            playerInfo.ChargeRaidCount(true);
                                            basePlayer.ChatMessage(Lang("Raid Reset Specific Slayer", basePlayer.UserIDString));

                                            playerInfo.StopCharging();

                                            PlayerUpdateRaidLimitUI(basePlayer, false);
                                        }
                                        break;

                                    case 1://Charge if there is not enough
                                        if (DateTime.Now >= playerInfo.NextChargeRealtime)
                                        {
                                            playerInfo.ChargeRaidCount(false);
                                            basePlayer.ChatMessage(Lang("Raid Reset Specific Slayer", basePlayer.UserIDString));

                                            if (config.RaidLimitSettings.OneTimeMaximumRaidableHomeCount <= playerInfo.RaidLeftCount)
                                                playerInfo.StopCharging();
                                            else
                                                playerInfo.AddChargeSchedule();

                                            PlayerUpdateRaidLimitUI(basePlayer, false);
                                        }
                                        break;

                                    default:
                                        goto case 0;
                                }
                                break;

                            case 1://playtime
                                switch (config.RaidLimitSettings.CounterChargeType)
                                {
                                    case 0://Charge when used up
                                        if (playerInfo.RaidLeftCount != 0)
                                            break;

                                        if (playtime >= playerInfo.NextChargePlaytime)
                                        {
                                            playerInfo.ChargeRaidCount(true);
                                            basePlayer.ChatMessage(Lang("Raid Reset Specific Slayer", basePlayer.UserIDString));

                                            playerInfo.StopCharging();

                                            PlayerUpdateRaidLimitUI(basePlayer, false);
                                        }
                                        break;

                                    case 1://Charge if there is not enough
                                        if (playtime >= playerInfo.NextChargePlaytime)
                                        {
                                            playerInfo.ChargeRaidCount(false);
                                            basePlayer.ChatMessage(Lang("Raid Reset Specific Slayer", basePlayer.UserIDString));

                                            if (config.RaidLimitSettings.OneTimeMaximumRaidableHomeCount <= playerInfo.RaidLeftCount)
                                                playerInfo.StopCharging();
                                            else
                                                playerInfo.AddChargeSchedule();

                                            PlayerUpdateRaidLimitUI(basePlayer, false);
                                        }
                                        break;

                                    default:
                                        goto case 0;
                                }
                                break;

                            default:
                                goto case 0;
                        }

                        if (playerInfo.RaidLeftCount == 0)
                        {
                            switch (config.RaidLimitSettings.CounterChargeDelayType)
                            {
                                case 0:
                                    UpdatePlayerTimeUI(basePlayer, playerInfo.NextChargeRealtime - DateTime.Now, false);
                                    break;

                                case 1:
                                    UpdatePlayerTimeUI(basePlayer, playerInfo.NextChargePlaytime - playtime, false);
                                    break;

                                default:
                                    goto case 0;
                            }
                        }
                    }
                }
            }

            if (PlaytimeTracker == null)
            {
                TimeSpan timerticktimeSpan = DateTime.Now - playersDateTimeData.SaveTickTime;

                foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
                {
                    if (playersDateTimeData.BasePlayerList.Contains(basePlayer))
                    {
                        PlayerInfo playerInfo = playersData.Players[basePlayer.UserIDString];

                        if (playerInfo.Playtime.TotalSeconds < config.RaidLimitSettings.NoobCantRaidSecond)
                            playersData.PlayerAddTime(basePlayer.UserIDString, timerticktimeSpan);
                        else
                        {
                            if (playerInfo.NoobCanRaid == false)
                            {
                                playerInfo.NoobCanRaid = true;
                            }
                        }
                    }
                }

                playersDateTimeData.SaveTickTime = DateTime.Now;
                playersDateTimeData.BasePlayerList = BasePlayer.activePlayerList;
            }
            else
            {
                playersDateTimeData.SaveTickTime = DateTime.Now;
            }
        }

        #endregion

        #region InterCommand

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || entity.gameObject == null || info == null)
                return;

            if (((1 << entity.gameObject.layer) & MaskInt) != 0)
            {
                BasePlayer player = info.Initiator as BasePlayer;// ?? entity.lastAttacker;

                if (player == null ||
                    player.IsNpc ||
                    !entity.OwnerID.IsSteamId()||
                    player.UserIDString == entity.OwnerID.ToString() ||
                    info.damageTypes.Get(DamageType.Explosion) == 0 ||
                    permission.UserHasPermission(player.UserIDString, raidlimit_bypass_Perm))
                    return;

                PlayerInfo playerinfo = playersData.Players[player.UserIDString];

                if (GetCanRaid(playerinfo) == false)
                {
                    TimeSpan playtime = GetPlayerPlaytime(player);
                    TryChatMessage(player, Lang("Raid Blocked Time", player.UserIDString, config.RaidLimitSettings.NoobCantRaidSecond, playtime.TotalSeconds));
                    info.damageTypes.Scale(DamageType.Explosion, 0);
                    return;
                }

                /////////////////////////////////////////////////////////////////////

                if (config.RaidLimitOperationType.OperationType != 1)
                {
                    if (CheckRaidCountItemContainsNAdd(playerinfo.RaidCountItems, new RaidCountItem(new List<ulong>() { entity.OwnerID }, new List<int>())) == true)
                        return;

                    if (permission.UserHasPermission(entity.OwnerID.ToString(), "antinoobraid.noob"))
                    {
                        info.damageTypes.Scale(DamageType.Explosion, 0);
                        return;
                    }
                }

                string addraidlistmsg = (config.RaidLimitOperationType.OperationType == 1 &&
                    config.RaidLimitOperationType.ToolCupboardIdentification.CheckToolCupboardInstanceID == true &&
                    config.RaidLimitOperationType.ToolCupboardIdentification.CheckAuthorizedPeoples == false) ? "Add RaidList TC" : "Add RaidList";

                if (0 < playerinfo.RaidLeftCount)
                {
                    RaidCountItem RaidCountItem = FindLinkedStructuresRaidCountItem(entity);
                    if (RaidCountItem == null)
                    {
                        info.damageTypes.Scale(DamageType.Explosion, 0);
                        return;
                    }

                    if (RaidCountItem.RaidTargets.Count == 0 && RaidCountItem.TCInstanceIDs.Count == 0)
                        return;

                    if (config.RaidLimitOperationType.OperationType == 1)
                    {
                        if (RaidCountItem.RaidTargets.Contains(player.userID))
                            return;
                    }

                    if (CheckRaidCountItemContainsNAdd(playerinfo.RaidCountItems, RaidCountItem) == true)
                        return;

                    playerinfo.AddRaidTarget(RaidCountItem);
                    if (config.RaidLimitSettings.CounterChargeDelay != -1)
                    {
                        switch (config.RaidLimitSettings.CounterChargeType)
                        {
                            case 0:
                                if (playerinfo.RaidLeftCount == 0)
                                    playerinfo.AddChargeSchedule();
                                break;

                            case 1:
                                if (playerinfo.RaidLeftCount < config.RaidLimitSettings.OneTimeMaximumRaidableHomeCount)
                                    playerinfo.AddChargeSchedule();
                                break;

                            default:
                                goto case 0;
                        }
                    }

                    if (playerinfo.Charging && playerinfo.RaidLeftCount == 0)
                    {
                        switch (config.RaidLimitSettings.CounterChargeDelayType)
                        {
                            case 0:
                                UpdatePlayerTimeUI(player, playerinfo.NextChargeRealtime - DateTime.Now, false);
                                break;

                            case 1:
                                TimeSpan playerplaytime = GetPlayerPlaytime(player);
                                UpdatePlayerTimeUI(player, playerinfo.NextChargePlaytime - playerplaytime, false);
                                break;

                            default:
                                goto case 0;
                        }
                    }
                    else
                        UpdatePlayerCountUI(player, false);

                    player.ChatMessage(Lang(addraidlistmsg, player.UserIDString, playerinfo.RaidLeftCount));

                    if (config.TeamSyncSettings.TeamCounterSync == true && player.currentTeam != 0)
                    {
                        List<ulong> Team;

                        if (config.TeamSyncSettings.PreventTempDisband == true)
                        {
                            Team = new List<ulong>(RelationshipManager.ServerInstance.FindTeam(player.currentTeam).members);

                            Dictionary<ulong, PlayerTeam> oldTeamData;
                            if (playersTeamData.OldDataIndicator == 1)
                                oldTeamData = playersTeamData.TeamsData1;
                            else
                                oldTeamData = playersTeamData.TeamsData2;

                            if (oldTeamData.ContainsKey(player.currentTeam))
                            {
                                Team.AddRange(oldTeamData[player.currentTeam].Members);
                                Team = Team.Distinct().ToList();
                            }
                        }
                        else
                            Team = RelationshipManager.ServerInstance.FindTeam(player.currentTeam).members;

                        foreach (var member in Team)
                        {
                            if (member != player.userID)
                            {
                                if (playersData.Players.ContainsKey(member.ToString()) == false)
                                    playersData.AddPlayer(member.ToString());
                                PlayerInfo playerInfo = playersData.Players[member.ToString()];

                                if (0 < playerInfo.RaidLeftCount && CheckRaidCountItemContainsNAdd(playerInfo.RaidCountItems, RaidCountItem) == false)
                                {
                                    BasePlayer memberBplayer = TryGetPlayer(member);

                                    playerInfo.AddRaidTarget(RaidCountItem);
                                    if (config.RaidLimitSettings.CounterChargeDelay != -1)
                                    {
                                        switch (config.RaidLimitSettings.CounterChargeType)
                                        {
                                            case 0:
                                                if (playerInfo.RaidLeftCount == 0)
                                                    playerInfo.AddChargeSchedule();
                                                break;

                                            case 1:
                                                if (playerInfo.RaidLeftCount < config.RaidLimitSettings.OneTimeMaximumRaidableHomeCount)
                                                    playerInfo.AddChargeSchedule();
                                                break;

                                            default:
                                                goto case 0;
                                        }
                                    }

                                    if (playerInfo.Charging && playerInfo.RaidLeftCount == 0)
                                    {
                                        switch (config.RaidLimitSettings.CounterChargeDelayType)
                                        {
                                            case 0:
                                                UpdatePlayerTimeUI(memberBplayer, playerInfo.NextChargeRealtime - DateTime.Now, false);
                                                break;

                                            case 1:
                                                if (memberBplayer == null)
                                                    break;

                                                TimeSpan playerplaytime = GetPlayerPlaytime(memberBplayer);
                                                UpdatePlayerTimeUI(memberBplayer, playerInfo.NextChargePlaytime - playerplaytime, false);
                                                break;

                                            default:
                                                goto case 0;
                                        }
                                    }
                                    else
                                        UpdatePlayerCountUI(player, false);

                                    UpdatePlayerCountUI(memberBplayer, false);
                                    memberBplayer?.ChatMessage(Lang("Team Member Raiding", member.ToString(), player.displayName));
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (playerinfo.Charging)
                    {
                        int leftseconds;
                        switch (config.RaidLimitSettings.CounterChargeDelayType)
                        {
                            case 0:
                                leftseconds = (int)(playerinfo.NextChargeRealtime - DateTime.Now).TotalSeconds;
                                TryChatMessage(player, Lang("Raid Blocked RNextTime", player.UserIDString, leftseconds));
                                break;

                            case 1:
                                TimeSpan playerplaytime = GetPlayerPlaytime(player);
                                leftseconds = (int)(playerinfo.NextChargePlaytime - playerplaytime).TotalSeconds;
                                TryChatMessage(player, Lang("Raid Blocked PNextTime", player.UserIDString, leftseconds));
                                break;

                            default:
                                goto case 0;
                        }
                    }
                    else
                    {
                        if (config.RaidLimitSettings.InitializeCounterOnMidnightTime == true)
                            TryChatMessage(player, Lang("Raid Blocked", player.UserIDString));
                        else
                            TryChatMessage(player, Lang("Raid Blocked NoRefill", player.UserIDString));
                    }

                    info.damageTypes.Scale(DamageType.Explosion, 0);
                }
            }
            return;
        }

        private void PlayerAddToData(BasePlayer player)
        {
            playersData.AddPlayer(player);
            playersUIData.AddPlayer(player);
        }

        private void PlayersAddToData()
        {
            foreach (var BasePlayer in BasePlayer.activePlayerList)
            {
                PlayerAddToData(BasePlayer);
            }
        }

        private void PlayerUpdateNameData(BasePlayer player)
        {
            if (playersData.Players.ContainsKey(player.UserIDString))
                playersData.Players[player.UserIDString].Name = player.displayName;
        }

        private void PlayersUpdateNameData()
        {
            foreach (var BasePlayer in BasePlayer.activePlayerList)
            {
                PlayerUpdateNameData(BasePlayer);
            }
        }

        private bool CheckRaidCountItemContainsNAdd(List<RaidCountItem> player, RaidCountItem target)
        {
            bool returnValue = false;

            foreach (var playeritem in player)
            {
                foreach (var searcheditem in target.RaidTargets)
                {
                    if (playeritem.RaidTargets.Contains(searcheditem))
                    {
                        playeritem.AddRaidTargetsRange(target.RaidTargets);
                        returnValue = true;
                        break;
                    }
                }

                foreach (var searcheditem in target.TCInstanceIDs)
                {
                    if (playeritem.TCInstanceIDs.Contains(searcheditem))
                    {
                        playeritem.AddTCInstanceIDsRange(target.TCInstanceIDs);
                        returnValue = true;
                        break;
                    }
                }
            }

            return returnValue;
        }

        private RaidCountItem FindLinkedStructuresRaidCountItem(BaseEntity entity)
        {
            int ObjectSearchRange;
            int ObjectSearchDepth;

            switch (config.RaidLimitOperationType.OperationType)
            {
                case 0:
                    ObjectSearchRange = config.RaidLimitOperationType.ObjectOwnerIdentification.ObjectSearchRange;
                    ObjectSearchDepth = config.RaidLimitOperationType.ObjectOwnerIdentification.ObjectSearchDepth;
                    break;

                case 1:
                    ObjectSearchRange = config.RaidLimitOperationType.ToolCupboardIdentification.ObjectSearchRange;
                    ObjectSearchDepth = config.RaidLimitOperationType.ToolCupboardIdentification.ObjectSearchDepth;
                    break;

                default:
                    goto case 0;
            }

            List<BaseEntity> SearchedEntity = new List<BaseEntity>();
            List<BaseEntity> RemoveEntity = new List<BaseEntity>();
            List<BaseEntity> ExpendEntity = new List<BaseEntity>();

            List<BaseEntity> EntityList = new List<BaseEntity>();
            Vis.Entities<BaseEntity>(entity.transform.position, ObjectSearchRange, EntityList, MaskInt);

            int Depth = 0;

            while (!(EntityList.Count == 0))
            {
                if (ObjectSearchDepth != -1)
                {
                    if (Depth >= ObjectSearchDepth)
                    {
                        break;
                    }
                    Depth++;
                }

                foreach (var item in EntityList)
                {
                    if (SearchedEntity.Contains(item) == false)
                    {
                        SearchedEntity.Add(item);
                        ExpendEntity.Add(item);
                        RemoveEntity.Add(item);
                    }
                    else
                    {
                        RemoveEntity.Add(item);
                    }
                }

                foreach (var item in ExpendEntity)
                {
                    Vis.Entities<BaseEntity>(item.transform.position, ObjectSearchRange, EntityList, MaskInt);
                }

                ExpendEntity.Clear();
                EntityList = EntityList.Distinct().ToList();

                foreach (var item in RemoveEntity)
                {
                    if (EntityList.Contains(item))
                        EntityList.Remove(item);
                }
                RemoveEntity.Clear();
            }

            RaidCountItem raidCountItem;

            switch (config.RaidLimitOperationType.OperationType)
            {
                case 0:
                    List<ulong> Owners = new List<ulong>();
                    Owners.Add(entity.OwnerID);

                    foreach (var item in SearchedEntity)
                    {
                        if (Owners.Contains(item.OwnerID) == false)
                            Owners.Add(item.OwnerID);
                    }

                    raidCountItem = new RaidCountItem(Owners, new List<int>());
                    break;

                case 1:
                    List<ulong> AuthUsers = new List<ulong>();
                    List<int> CupboardInstanceIDs = new List<int>();
                    int noobusercount = 0;

                    foreach (var item in SearchedEntity)
                    {
                        BuildingPrivlidge cupboard = item.GetComponentInParent<BuildingPrivlidge>();
                        if (cupboard != null)
                        {
                            if (config.RaidLimitOperationType.ToolCupboardIdentification.CheckToolCupboardInstanceID)
                                CupboardInstanceIDs.Add(cupboard.GetInstanceID());

                            if (config.RaidLimitOperationType.ToolCupboardIdentification.CheckAuthorizedPeoples)
                            {
                                foreach (ProtoBuf.PlayerNameID playernameid in cupboard.authorizedPlayers)
                                {
                                    if (AuthUsers.Contains(playernameid.userid) == false)
                                    {
                                        AuthUsers.Add(playernameid.userid);

                                        if (permission.UserHasPermission(playernameid.userid.ToString(), "antinoobraid.noob"))
                                            noobusercount++;
                                    }
                                }
                            }
                        }
                    }

                    if (0 < noobusercount && noobusercount == AuthUsers.Count)
                        raidCountItem = null;//cant raid
                    else
                        raidCountItem = new RaidCountItem(AuthUsers, CupboardInstanceIDs);
                    break;

                default:
                    goto case 0;
            }

            return raidCountItem;
        }

        PlayersDateTimeData playersDateTimeData;

        private class PlayersDateTimeData
        {
            public ListHashSet<BasePlayer> BasePlayerList = BasePlayer.activePlayerList;
            public DateTime SaveTickTime = DateTime.Now;

            public PlayersDateTimeData()
            {
            }
        }

        #endregion

        #region Command/API

        [Command("rl.reset")]
        private void ResetLimitCount(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                if (!player.HasPermission(raidlimit_admin_Perm))
                {
                    player.Reply(Lang("No Permission", player.Id, command));
                    return;
                }
            }

            foreach (var item in playersData.Players)
            {
                item.Value.ResetRaidDataNCount();
                item.Value.StopCharging();
            }
            SavePlayersData();

            Puts(Lang("Raid List Reset", null));
            foreach (IPlayer current in players.Connected)
                current.Reply(Lang("Raid List Reset", current.Id));
            PlayersUpdateRaidLimitUI(false);
        }

        [Command("rl.addvalue")]
        private void IncreaseLimitCount(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                if (!player.HasPermission(raidlimit_admin_Perm))
                {
                    player.Reply(Lang("No Permission", player.Id, command));
                    return;
                }
            }

            int parsedint;

            if (1 < args.Length && int.TryParse(args[1], out parsedint))
            {
                IPlayer TPlayer = GetPlayer(args[0], player);

                if (!playersData.Players.ContainsKey(TPlayer.Id))
                    playersData.Players.Add(TPlayer.Id, new PlayerInfo(TPlayer));

                int result = playersData.Players[TPlayer.Id].AddRaidCount(parsedint);
                PlayerUpdateRaidLimitUI(TPlayer.Object as BasePlayer, false);

                if (result < 0)
                {
                    player.Reply(Lang("Count DecreasedC", player.Id, TPlayer.Name, ((int)Mathf.Abs(result)).ToString()));
                    TPlayer.Reply(Lang("Count DecreasedP", TPlayer.Id, ((int)Mathf.Abs(result)).ToString()));
                }
                else
                {
                    player.Reply(Lang("Count IncreasedC", player.Id, TPlayer.Name, result.ToString()));
                    TPlayer.Reply(Lang("Count IncreasedP", TPlayer.Id, result.ToString()));
                }
            }
            else
                player.Reply(Lang("NotEnoughArgument", player.Id, 2.ToString()));
        }

        [Command("rlcheck")]
        private void CheckLimitCount(IPlayer player, string command, string[] args)
        {
            if (playersData.Players.ContainsKey(player.Id) == false)
                return;

            PlayerInfo playerinfo = playersData.Players[player.Id];

            if (GetCanRaid(playerinfo))
            {
                player.Reply(Lang("Left Raid Count", player.Id, (playerinfo.RaidLeftCount)));
            }
            else
            {
                TimeSpan playtime = GetPlayerPlaytime(player);
                player.Reply(Lang("Raid Blocked Time", player.Id, config.RaidLimitSettings.NoobCantRaidSecond, playtime.TotalSeconds));
            }
        }

        private void UIToggle(IPlayer player, string command, string[] args)
        {
            bool playerUIToggle = playersUIData.PlayersUIToggle[player.Id];
            if (playerUIToggle == true)
            {
                HideUI(player.Object as BasePlayer);
            }
            else
            {
                ShowUI(player.Object as BasePlayer);
            }
        }

        private void RegisterUICommand()
        {
            if (config.UISettings.UIEnable)
                AddCovalenceCommand("RaidUI", "UIToggle");
        }

        //API

        private void ShowUI(BasePlayer player)
        {
            playersUIData.PlayersUIToggle[player.UserIDString] = true;
            PlayerUpdateRaidLimitUI(player, true);
        }

        private void HideUI(BasePlayer player)
        {
            playersUIData.PlayersUIToggle[player.UserIDString] = false;
            PlayerDestroyRaidLimitUI(player);
        }

        //

        #endregion

        #region GUI

        public string RaidLimit_boomb = "RaidLimit_boomb";
        public string BaseRaidLimitUI = "BaseRaidLimitUI";
        public string LabelPanel = "LabelPanel";

        public void LoadImage()
        {
            if (ImageLibrary.Call<bool>("HasImage", RaidLimit_boomb))
                return;

            ImageLibrary.Call<bool>("AddImage", "https://i.imgur.com/t3QdFmG.png", RaidLimit_boomb);
        }

        private void UpdateRaidLimitUI(BasePlayer player, bool Imageupdate, int FontSize, string Msg)
        {
            if (player == null)
                return;

            if (permission.UserHasPermission(player.UserIDString, raidlimit_bypass_Perm))
                Msg = Lang("bypass GUI Msg", player.UserIDString);

            if (playersUIData.PlayersUIToggle[player.UserIDString] == false)
                return;

            if (playersData.Players.ContainsKey(player.UserIDString) == false)
                playersData.AddPlayer(player);

            CuiElementContainer RaidLimitGUI = new CuiElementContainer();

            if (Imageupdate == true)
            {
                string panel = RaidLimitGUI.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image =
                    {
                        Color = "1 1 1 0.2"
                    },

                    RectTransform =
                    {
                        AnchorMin = config.UISettings.UIPosition.AnchorMin,
                        AnchorMax = config.UISettings.UIPosition.AnchorMax
                    },
                }, "Under", BaseRaidLimitUI);

                RaidLimitGUI.Add(new CuiElement
                {
                    Name = "BombImage",
                    Parent = BaseRaidLimitUI,
                    Components =
                    {
                        new CuiRawImageComponent { Color = "1 1 1 1", Png = ImageLibrary.Call<string>("GetImage", RaidLimit_boomb) },
                        new CuiRectTransformComponent { AnchorMin = "0 0.02", AnchorMax = "0.32 0.93" }
                    }
                });
            }

            string labelpanel = RaidLimitGUI.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image =
                {
                    Color = "0 0 0 0"
                },

                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                },
            }, BaseRaidLimitUI, LabelPanel);

            RaidLimitGUI.Add(new CuiLabel
            {
                Text =
                {
                    Text = Msg,
                    FontSize = FontSize,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = "0.3 0",
                    AnchorMax = "1.0 1"
                }
            }, labelpanel);

            if (Imageupdate)
            {
                CuiHelper.DestroyUi(player, BaseRaidLimitUI);
            }
            else
            {
                CuiHelper.DestroyUi(player, LabelPanel);
            }
            CuiHelper.AddUi(player, RaidLimitGUI);
        }

        private void UpdatePlayerCountUI(BasePlayer player, bool imageupdate)
        {
            if (player == null)
                return;

            if (config.UISettings.UIEnable == false)
                return;

            PlayerInfo playerInfo = playersData.Players[player.UserIDString];
            string LeftCount = playerInfo.RaidLeftCount + "/" + config.RaidLimitSettings.OneTimeMaximumRaidableHomeCount;
            UpdateRaidLimitUI(player, imageupdate, 14, LeftCount);
        }

        private void UpdatePlayerNoobTimeUI(BasePlayer player, TimeSpan playtime, bool imageupdate)
        {
            UpdatePlayerTimeUI(player, TimeSpan.FromSeconds(config.RaidLimitSettings.NoobCantRaidSecond.Value).Subtract(playtime), imageupdate);
        }

        private void UpdatePlayerTimeUI(BasePlayer player, TimeSpan displaytime, bool imageupdate)
        {
            if (config.UISettings.UIEnable == false)
                return;

            string timestring = GetimeToString(displaytime);

            UpdateRaidLimitUI(player, imageupdate, 12, timestring);
        }

        private void PlayerUpdateRaidLimitUI(BasePlayer player, bool imageupdate)
        {
            if (config.UISettings.UIEnable == false)
                return;

            PlayerInfo playerInfo = playersData.Players[player.UserIDString];
            TimeSpan playtime = GetPlayerPlaytime(player);

            if (GetCanRaid(playerInfo))
            {
                if (playerInfo.Charging)
                {
                    switch (config.RaidLimitSettings.CounterChargeDelayType)
                    {
                        case 0:
                            UpdatePlayerTimeUI(player, playerInfo.NextChargeRealtime - DateTime.Now, imageupdate);
                            break;

                        case 1:
                            UpdatePlayerTimeUI(player, playerInfo.NextChargePlaytime - playtime, imageupdate);
                            break;

                        default:
                            goto case 0;
                    }
                }
                else
                    UpdatePlayerCountUI(player, imageupdate);
            }
            else
            {
                if (playerInfo.Charging)
                {
                    switch (config.RaidLimitSettings.CounterChargeDelayType)
                    {
                        case 0:
                            UpdatePlayerTimeUI(player, playerInfo.NextChargeRealtime - DateTime.Now, imageupdate);
                            break;

                        case 1:
                            UpdatePlayerTimeUI(player, playerInfo.NextChargePlaytime - playtime, imageupdate);
                            break;

                        default:
                            goto case 0;
                    }
                }
                else
                    UpdatePlayerNoobTimeUI(player, playtime, imageupdate);
            }
        }

        private void PlayerDestroyRaidLimitUI(BasePlayer player)
        {
            if (config.UISettings.UIEnable == false)
                return;

            CuiHelper.DestroyUi(player, BaseRaidLimitUI);
        }

        private void PlayersUpdateRaidLimitUI(bool imageupdate)
        {
            if (config.UISettings.UIEnable == false)
                return;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                PlayerInfo playerInfo = playersData.Players[player.UserIDString];

                if (GetCanRaid(playerInfo))
                {
                    UpdatePlayerCountUI(player, imageupdate);
                }
                else
                {
                    TimeSpan playtime = GetPlayerPlaytime(player);

                    if (playerInfo.Charging)
                    {
                        switch (config.RaidLimitSettings.CounterChargeDelayType)
                        {
                            case 0:
                                UpdatePlayerTimeUI(player, playerInfo.NextChargeRealtime - DateTime.Now, imageupdate);
                                break;

                            case 1:
                                UpdatePlayerTimeUI(player, playerInfo.NextChargePlaytime - playtime, imageupdate);
                                break;

                            default:
                                goto case 0;
                        }
                    }
                    else
                        UpdatePlayerNoobTimeUI(player, playtime, imageupdate);
                }
            }
        }

        private void PlayersDestroyRaidLimitUI()
        {
            if (config.UISettings.UIEnable == false)
                return;

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, BaseRaidLimitUI);
            }
        }

        #endregion

        #region Helper

        private IPlayer GetPlayer(string nameOrID, IPlayer player)
        {
            List<BasePlayer> BasePlayerList = BasePlayer.activePlayerList.ToList();
            List<IPlayer> PlayersList = new List<IPlayer>();

            foreach (var item in BasePlayerList)
            {
                PlayersList.Add(item.IPlayer);
            }

            if (nameOrID.IsSteamId())
            {
                IPlayer result = PlayersList.Find((p) => p.Id == nameOrID);

                if (result == null)
                    player.Reply(Lang("SteamID Not Found", player?.Id, nameOrID));

                return result;
            }

            List<IPlayer> foundPlayers = new List<IPlayer>();

            foreach (IPlayer current in PlayersList)
            {
                if (current.Name.ToLower() == nameOrID.ToLower())
                    return current;

                if (current.Name.ToLower().Contains(nameOrID.ToLower()))
                    foundPlayers.Add(current);
            }

            switch (foundPlayers.Count)
            {
                case 0:
                    player.Reply(Lang("Player Not Found", player?.Id, nameOrID));
                    break;
                case 1:
                    return foundPlayers[0];
                default:
                    string[] names = (from current in foundPlayers select $"- {current.Name}").ToArray();
                    player.Reply(Lang("Multiple Players Found", player?.Id, string.Join("\n", names)));
                    break;
            }
            return null;
        }

        private void TryChatMessage(BasePlayer player, string msg)
        {
            if (PlayerSpamMessageBlockFlags.Contains(player.userID))
                return;

            player.ChatMessage(msg);
            PlayerSpamMessageBlockFlags.Add(player.userID);
            timer.Once(1f, () =>
            {
                PlayerSpamMessageBlockFlags.Remove(player.userID);
            });
        }

        private bool GetCanRaid(PlayerInfo playerInfo)
        {
            if (PlaytimeTracker != null)
            {
                return playerInfo.NoobCanRaidPlaytimeTracker;
            }
            else
            {
                return playerInfo.NoobCanRaid;
            }
        }

        private string GetimeToString(TimeSpan lefttime)
        {
            var days = lefttime.Days;
            var hours = lefttime.Hours;
            hours += (days * 24);
            var mins = lefttime.Minutes;
            var secs = lefttime.Seconds;
            return string.Format("<color=red>{0:00}:{1:00}:{2:00}</color>", hours, mins, secs);
        }

        private TimeSpan GetPlayerPlaytime(IPlayer player) => GetPlayerPlaytime(player.Id);

        private TimeSpan GetPlayerPlaytime(BasePlayer player) => GetPlayerPlaytime(player.UserIDString);

        private TimeSpan GetPlayerPlaytime(string playerId)
        {
            if (PlaytimeTracker != null)
            {
                double? obj = PlaytimeTracker.Call<double>("GetPlayTime", playerId);

                if (obj != null)
                {
                    return TimeSpan.FromSeconds(obj.Value);
                }
                else
                {
                    return playersData.Players[playerId].Playtime;
                }
            }
            return playersData.Players[playerId].Playtime;
        }

        private BasePlayer TryGetPlayer(ulong playerId)
        {
            return BasePlayer.FindByID(playerId);
        }

        #endregion
    }
}