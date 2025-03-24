using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Permission Effects", "noname", "1.1.1")]
    [Description("Enhance the permission system by abstracting Oxide permission system")]
    class PermissionEffects : CovalencePlugin
    {
        [PluginReference]
        private Plugin PlaytimeTracker, UIScaleManager;

        public static PermissionEffects Plugin;
        public static string permissioneffects_admin_perm_admin_perm = "permissioneffects.admin";

        #region GUIField

        Dictionary<string, Timer> PlayersViewingUi = new Dictionary<string, Timer>();
        string CursorUIPanel = "CursorUIPanel";
        string UIBaseInvPanel = "UIBaseInvPanel";
        string UIBasePanel = "UIBasePanel";
        string BackGroundPanel = "BackGroundPanel";
        string UIBaseBtnPanel = "UIBaseBtnPanel";
        string UIPMBtn = "UIPMBtn";
        string ESBtn = "ESBtn";

        #endregion

        #region Hooks
        
        private new void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");

            Config.WriteObject(GetDefaultConfig(), true);
        }

        private void Init()
        {
            LoadConfig();
            LoadPlayersData();
            LoadGroupsData();
            LoadThreadsData();
            LoadPlayersGUIData();
        }

        void OnServerSave()
        {
            puts("Saving Permission Effects DataFile...");
            SavePlayersData();
            SaveGroupsData();
            SaveThreadsData();
            SavePlayersGUIData();
        }

        void Unload()
        {
            puts("Saving Permission Effects DataFile...");
            SavePlayersData();
            SaveGroupsData();
            SaveThreadsData();
            SavePlayersGUIData();

            PlayersRemoveBaseinvUI();

            Plugin = null;
        }

        private void OnServerInitialized()
        {
            Plugin = this;
            RegisterPermissions();
            RegisterCommands();
            StartTimer();
            playersUIData.ResetOnlinePlayersData();
            PlayersAddBaseinvUI();
            RegisterTestEffects();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsNpc) return;

            playersUIData.ResetPlayerData(player);
            timer.Once(5f, () =>
            {
                PlayerAddBaseinvUI(player);
                UpdatePlayerGUI(player);
            });
        }

        #endregion

        #region Classes

        #region BaseThread

        private enum PlayerThreadType
        {
            BasePlayerEffectThread,
            EffectThread,
            RealtimeEffectThread,
            PlaytimeEffectThread
        }

        private enum DataThreadType
        {
            BaseDataEffectThread,
            EffectThreadData,
            RealtimeEffectThreadData,
            PlaytimeEffectThreadData
        }

        private class BasePlayerEffectThread : IEquatable<BasePlayerEffectThread>
        {
            public PlayerThreadType PlayerThreadType { get; set; }
            public string ThreadName { get; set; }
            public string ThreadColor { get; set; }
            public List<string> Permissions { get; set; }

            public DateTime DisconnectAutoExpireDate { get; set; }
            public TimeSpan? DisconnectAutoExpireTime { get; set; }

            public DateTime ExpireDate { get; set; }
            public TimeSpan ExpireTime { get; set; }

            public BasePlayerEffectThread()
            {
                PlayerThreadType = PlayerThreadType.BasePlayerEffectThread;
            }

            public bool Equals(BasePlayerEffectThread other)
            {
                return other != null &&
                       ThreadName == other.ThreadName &&
                       ThreadColor == other.ThreadColor &&
                       PlayerThreadType == other.PlayerThreadType;
            }

            public override bool Equals(object obj)
            {
                var other = obj as BasePlayerEffectThread;
                return other != null &&
                       ThreadName == other.ThreadName &&
                       ThreadColor == other.ThreadColor &&
                       PlayerThreadType == other.PlayerThreadType;
            }

            public override int GetHashCode()
            {
                var hashCode = 909284198;
                hashCode = hashCode * -1521134295 + PlayerThreadType.GetHashCode();
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ThreadName);
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ThreadColor);
                return hashCode;
            }

            public void GrantUserPermissions(string id)
            {
                foreach (var item in Permissions)
                {
                    Plugin.permission.GrantUserPermission(id, item, null);
                }
            }

            public void GrantGroupPermissions(string groupName)
            {
                foreach (var item in Permissions)
                {
                    Plugin.permission.GrantGroupPermission(groupName, item, null);
                }
            }

            public void RevokeUserPermissions(string id, Dictionary<string, BasePlayerEffectThread> PlayerEffectThreads)
            {
                foreach (var item in Permissions)
                {
                    bool OthersContains = false;
                    foreach (var item2 in PlayerEffectThreads)
                    {
                        if (item2.Value.ThreadName != ThreadName && item2.Value.Permissions.Contains(item))
                        {
                            OthersContains = true;
                            break;
                        }
                    }
                    if (OthersContains == false)
                        Plugin.permission.RevokeUserPermission(id, item);
                }
            }

            public void RevokeUserPermissions(string id)
            {
                foreach (var item in Permissions)
                {
                    Plugin.permission.RevokeUserPermission(id, item);
                }
            }

            public void RevokeGroupPermissions(string groupName, Dictionary<string, BasePlayerEffectThread> PlayerEffectThreads)
            {
                foreach (var item in Permissions)
                {
                    bool OthersContains = false;
                    foreach (var item2 in PlayerEffectThreads)
                    {
                        if (item2.Value.ThreadName != ThreadName && item2.Value.Permissions.Contains(item))
                        {
                            OthersContains = true;
                            break;
                        }
                    }
                    if (OthersContains == false)
                        Plugin.permission.RevokeGroupPermission(groupName, item);
                }
            }

            public void RevokeGroupPermissions(string groupName)
            {
                foreach (var item in Permissions)
                {
                    Plugin.permission.RevokeGroupPermission(groupName, item);
                }
            }
        }

        private class BaseDataEffectThread
        {
            public DataThreadType DataThreadType { get; set; }
            public string ThreadName { get; set; }
            public string ThreadColor { get; set; }
            public List<string> Permissions { get; set; }

            public TimeSpan? DisconnectAutoExpireTime { get; set; }

            public TimeSpan ExpireTime { get; set; }

            public BaseDataEffectThread()
            {
                DataThreadType = DataThreadType.BaseDataEffectThread;
            }

            public BasePlayerEffectThread ToPlayerEffectThread(string PlayerID)
            {
                switch (DataThreadType)
                {
                    case DataThreadType.BaseDataEffectThread:
                        return new EffectThread(ThreadName, ThreadColor, Permissions, DisconnectAutoExpireTime);
                    case DataThreadType.EffectThreadData:
                        return new EffectThread(ThreadName, ThreadColor, Permissions, DisconnectAutoExpireTime);
                    case DataThreadType.RealtimeEffectThreadData:
                        return new RealtimeEffectThread(ThreadName, ThreadColor, Permissions, DisconnectAutoExpireTime, ExpireTime);
                    case DataThreadType.PlaytimeEffectThreadData:
                        return new PlaytimeEffectThread(ThreadName, ThreadColor, Permissions, DisconnectAutoExpireTime, ExpireTime, PlayerID);
                    default:
                        return null;
                }
            }
        }

        #endregion

        #region PlayerThread

        private class EffectThread : BasePlayerEffectThread
        {
            public EffectThread()
            {
                PlayerThreadType = PlayerThreadType.EffectThread;
            }

            public EffectThread(string threadName, string threadColor, List<string> permissions, TimeSpan? AutoExpireTIme) //default
            {
                PlayerThreadType = PlayerThreadType.EffectThread;

                ThreadName = threadName;
                ThreadColor = threadColor;
                Permissions = permissions;

                if (AutoExpireTIme != null)
                    DisconnectAutoExpireDate = DateTime.Now + (TimeSpan)AutoExpireTIme;

                DisconnectAutoExpireTime = AutoExpireTIme;
            }
        }

        private class RealtimeEffectThread : EffectThread
        {
            public RealtimeEffectThread()
            {
                PlayerThreadType = PlayerThreadType.RealtimeEffectThread;
            }

            public RealtimeEffectThread(string threadName, string threadColor, List<string> permissions, TimeSpan? AutoExpireTIme, TimeSpan expireTime)//realtime
                : base(threadName, threadColor, permissions, AutoExpireTIme)
            {
                PlayerThreadType = PlayerThreadType.RealtimeEffectThread;

                ExpireDate = DateTime.Now + expireTime;
            }
        }

        private class PlaytimeEffectThread : EffectThread
        {
            public PlaytimeEffectThread()
            {
                PlayerThreadType = PlayerThreadType.PlaytimeEffectThread;
            }

            public PlaytimeEffectThread(string threadName, string threadColor, List<string> permissions, TimeSpan? AutoExpireTIme, TimeSpan expireTime, string PlayerID)//playtime
                : base(threadName, threadColor, permissions, AutoExpireTIme)
            {
                PlayerThreadType = PlayerThreadType.PlaytimeEffectThread;

                TimeSpan? PlayerPlaytime = Plugin.GetPlayerPlaytime(PlayerID);
                if (PlayerPlaytime == null)
                {
                    ExpireTime = new TimeSpan();
                }
                else
                {
                    ExpireTime = (TimeSpan)PlayerPlaytime + expireTime;
                }
            }
        }

        #endregion

        #region DataThread

        private class EffectThreadData : BaseDataEffectThread
        {
            public EffectThreadData()
            {
                DataThreadType = DataThreadType.EffectThreadData;
            }

            public EffectThreadData(string threadName, string threadColor, List<string> permissions, TimeSpan? AutoExpireTIme)
            {
                DataThreadType = DataThreadType.EffectThreadData;

                ThreadName = threadName;
                ThreadColor = threadColor;
                Permissions = permissions;

                DisconnectAutoExpireTime = AutoExpireTIme;
            }
        }

        private class RealtimeEffectThreadData : EffectThreadData
        {
            public RealtimeEffectThreadData()
            {
                DataThreadType = DataThreadType.RealtimeEffectThreadData;
            }

            public RealtimeEffectThreadData(string threadName, string threadColor, List<string> permissions, TimeSpan? AutoExpireTIme, TimeSpan expireTime)
                : base(threadName, threadColor, permissions, AutoExpireTIme)
            {
                DataThreadType = DataThreadType.RealtimeEffectThreadData;

                ExpireTime = expireTime;
            }
        }

        private class PlaytimeEffectThreadData : EffectThreadData
        {
            public PlaytimeEffectThreadData()
            {
                DataThreadType = DataThreadType.PlaytimeEffectThreadData;
            }

            public PlaytimeEffectThreadData(string threadName, string threadColor, List<string> permissions, TimeSpan? AutoExpireTIme, TimeSpan expireTime)
                : base(threadName, threadColor, permissions, AutoExpireTIme)
            {
                DataThreadType = DataThreadType.PlaytimeEffectThreadData;

                ExpireTime = expireTime;
            }
        }

        #endregion

        #endregion

        #region PluginIO

        #region ConfigManage

        private PluginConfig config;

        private new void LoadConfig()
        {
            config = Config.ReadObject<PluginConfig>();

            if (config == null)
                config = GetDefaultConfig();

            VersionUpdate(config);
        }

        private void VersionUpdate(PluginConfig config)
        {
            if (config.ConfigVersion < new VersionNumber(1, 0, 5))
            {
                config.UIPosions = GetUIPosionConfig();
            }

            if (config.ConfigVersion < this.Version)
            {
                config.ConfigVersion = this.Version;
                Config.WriteObject(config, true);
                Puts("Config version has been updated");
            }
        }

        private class PluginConfig
        {
            public bool use_BroadCast;
            public bool use_Message;
            public bool use_ConsoleMessage;
            public int UIUpdateTimerInterval;
            public int TimeLimitTimerInterval;
            public int DisconnectDetectTimerInterval;
            public string EPSCommand;
            public string EffectCommand;
            public Dictionary<int, Posion> UIPosions;
            public class Posion
            {
                public float YAnchorMax;
                public float YAnchorMin;
                public float XAnchorMax;
                public float XAnchorMin;
            }
            public VersionNumber ConfigVersion;
        }

        private PluginConfig GetDefaultConfig()
        {
            PluginConfig pluginConfig = new PluginConfig
            {
                use_BroadCast = false,
                use_Message = true,
                use_ConsoleMessage = true,
                UIUpdateTimerInterval = 4,
                TimeLimitTimerInterval = 4,
                DisconnectDetectTimerInterval = 10,
                EPSCommand = "eps",
                EffectCommand = "effect",
                ConfigVersion = this.Version
            };
            pluginConfig.UIPosions = GetUIPosionConfig();
            return pluginConfig;
        }

        private Dictionary<int, PluginConfig.Posion> GetUIPosionConfig()
        {
            return new Dictionary<int, PluginConfig.Posion>
            {
                {
                    10,
                    new PluginConfig.Posion()
                    {
                        YAnchorMax = 0.11f,
                        YAnchorMin = 0.11f,
                        XAnchorMax = 0.6395f,
                        XAnchorMin = 0.3445f
                    }
                },
                {
                    8,
                    new PluginConfig.Posion()
                    {
                        YAnchorMax = 0.11f,
                        YAnchorMin = 0.11f,
                        XAnchorMax = 0.6105f,
                        XAnchorMin = 0.375f
                    }
                },
                {
                    6,
                    new PluginConfig.Posion()
                    {
                        YAnchorMax = 0.11f,
                        YAnchorMin = 0.11f,
                        XAnchorMax = 0.5825f,
                        XAnchorMin = 0.4056f
                    }
                }
            };
        }

        #endregion

        #region DataManage

        #region PlayersData

        DynamicConfigFile playersdataFile = Interface.Oxide.DataFileSystem.GetDatafile("PermissionEffects/PlayerData");
        PlayersData playersData;

        private void LoadPlayersData()
        {
            playersData = playersdataFile.ReadObject<PlayersData>();

            if (playersData == null)
                playersData = new PlayersData();
        }

        private void SavePlayersData()
        {
            playersdataFile.WriteObject(playersData);
        }

        private class PlayersData
        {
            public Dictionary<string, PlayerInfo> Players { get; set; }

            public PlayersData()
            {
                Players = new Dictionary<string, PlayerInfo>();
            }

            public void ResetData(IPlayer player)
            {
                foreach (var item in Players)
                {
                    foreach (var EffectThread in item.Value.PlayerEffectThreads)
                    {
                        EffectThread.Value.RevokeUserPermissions(item.Value.Id);
                    }
                }
                Players.Clear();
                Plugin.UpdatePlayersGUI();
                Plugin.SendReplyMessage(player, Plugin.Lang("Reseted PlayersData", player?.Id));
            }

            public void UpdatePlayers()
            {
                List<string> ClonedUsersKey = new List<string>();
                foreach (var item in Players)
                {
                    ClonedUsersKey.Add(item.Key);
                }

                foreach (var item in ClonedUsersKey)
                {
                    Players[item].UpdateEffects();
                }
            }

            public void AddEffectToPlayer(IPlayer target, IPlayer player, string EffectThreadName)
            {
                if (Players.ContainsKey(target.Id) == false)
                {
                    Players.Add(target.Id, new PlayerInfo(target));
                }

                Players[target.Id].AddEffect(EffectThreadName, player);
            }

            public void DeleteEffectFromPlayer(IPlayer target, IPlayer player, string EffectThreadName)
            {
                if (Players.ContainsKey(target.Id) == false)
                {
                    Plugin.SendReplyMessage(player, Plugin.Lang("Effect Is Not Exist On Player", player?.Id, EffectThreadName));

                    return;
                }

                Players[target.Id].DeleteEffect(EffectThreadName, player);
            }

            public void DeleteEffectFromPlayer(string target, IPlayer player, string EffectThreadName)
            {
                if (Players.ContainsKey(target) == false)
                {
                    Plugin.SendReplyMessage(player, Plugin.Lang("Effect Is Not Exist On Player", player?.Id, EffectThreadName));

                    return;
                }

                Players[target].DeleteEffect(EffectThreadName, player);
            }

            public void DeleteAllEffectFromPlayer(IPlayer target, IPlayer player)
            {
                if (Players.ContainsKey(target.Id) == false)
                {
                    Plugin.SendReplyMessage(player, Plugin.Lang("Effect Is Not Exist On Player", player?.Id, "All"));

                    return;
                }

                Players[target.Id].DeleteAllEffect(player);
            }

            public bool PlayerHasEffect(string playerId, string effectThreadName)
            {
                if (playerId.IsSteamId() == false)
                {
                    return false;
                }
                if (Players.ContainsKey(playerId) == false)
                {
                    return false;
                }

                PlayerInfo playerInfo = Players[playerId];

                if (playerInfo.PlayerEffectThreads.ContainsKey(effectThreadName))
                {
                    if (playerInfo.PlayerEffectThreads[effectThreadName].PlayerThreadType == PlayerThreadType.PlaytimeEffectThread && Plugin.PlaytimeTracker == null)
                    {
                        return false;
                    }
                    else
                    {
                        return true;
                    }
                }
                else
                {
                    foreach (string Groupname in Plugin.permission.GetUserGroups(playerId))
                    {
                        if (Plugin.groupsData.GroupHasEffect(Groupname, effectThreadName) == true)
                        {
                            return true;
                        }
                    }
                    return false;
                }
            }

            public TimeSpan? GetPlayerEffectLeftTime(string playerId, string effectThreadName)
            {
                if (Players.ContainsKey(playerId) == false)
                {
                    return null;
                }

                PlayerInfo playerInfo = Players[playerId];

                TimeSpan EffectLeftTime = new TimeSpan(0, 0, 0);

                if (playerInfo.PlayerEffectThreads.ContainsKey(effectThreadName))
                {
                    BasePlayerEffectThread baseEffectThread = playerInfo.PlayerEffectThreads[effectThreadName];

                    switch (baseEffectThread.PlayerThreadType)
                    {
                        case PlayerThreadType.EffectThread:
                            EffectLeftTime = TimeSpan.FromSeconds(-1);
                            break;

                        case PlayerThreadType.RealtimeEffectThread:
                            EffectLeftTime = baseEffectThread.ExpireDate.Subtract(DateTime.Now);
                            break;

                        case PlayerThreadType.PlaytimeEffectThread:
                            TimeSpan? PlayerPlaytime = Plugin.GetPlayerPlaytime(playerId);
                            if (PlayerPlaytime == null)
                            {
                                break;
                            }

                            EffectLeftTime = baseEffectThread.ExpireTime - (TimeSpan)PlayerPlaytime;
                            break;

                        default:
                            break;
                    }
                }

                string[] userGroups = Plugin.permission.GetUserGroups(playerId);

                if (userGroups.Length == 0)
                {
                    return EffectLeftTime;
                }

                foreach (string Groupname in userGroups)
                {
                    if (Plugin.groupsData.Groups.ContainsKey(Groupname) == false)
                        continue;

                    var Group = Plugin.groupsData.Groups[Groupname];

                    if (Group.PlayerEffectThreads.ContainsKey(effectThreadName))
                    {
                        BasePlayerEffectThread baseEffectThread = Group.PlayerEffectThreads[effectThreadName];

                        switch (baseEffectThread.PlayerThreadType)
                        {
                            case PlayerThreadType.EffectThread:
                                if (TimeSpan.FromSeconds(-1) > EffectLeftTime)
                                    EffectLeftTime = TimeSpan.FromSeconds(-1);
                                return EffectLeftTime;

                            case PlayerThreadType.RealtimeEffectThread:
                                TimeSpan LeftTime = baseEffectThread.ExpireDate.Subtract(DateTime.Now);
                                if (LeftTime > EffectLeftTime)
                                    EffectLeftTime = LeftTime;
                                return EffectLeftTime;

                            default:
                                break;
                        }
                    }
                }
                return null;
            }
        }

        private class PlayerInfo
        {
            public string Id { get; set; }
            public string Name { get; set; }
            public Dictionary<string, BasePlayerEffectThread> PlayerEffectThreads { get; set; }

            public PlayerInfo() { }

            public PlayerInfo(IPlayer player)
            {
                Id = player.Id;
                Name = player.Name;
                PlayerEffectThreads = new Dictionary<string, BasePlayerEffectThread>();
            }

            public void UpdateEffects()
            {
                List<BasePlayerEffectThread> DeleteThreads = new List<BasePlayerEffectThread>();
                var PlayerEffectThreadsList = PlayerEffectThreads.ToList();

                for (int j = 0; j < PlayerEffectThreadsList.Count; j++)
                {
                    BasePlayerEffectThread effectThread = PlayerEffectThreadsList[j].Value;

                    switch (effectThread.PlayerThreadType)
                    {
                        case PlayerThreadType.EffectThread:
                            break;

                        case PlayerThreadType.RealtimeEffectThread:
                            DateTime dt = effectThread.ExpireDate;
                            DateTime dtnow = DateTime.Now;

                            if (DateTime.Compare(dt, dtnow) <= 0)
                            {
                                DeleteThreads.Add(effectThread);
                            }
                            break;

                        case PlayerThreadType.PlaytimeEffectThread:
                            if (Plugin.PlaytimeTracker == null)
                            {
                                break;
                            }
                            TimeSpan? PlayerPlaytime = Plugin.GetPlayerPlaytime(Id);
                            if (PlayerPlaytime != null && PlayerPlaytime > effectThread.ExpireTime)
                            {
                                DeleteThreads.Add(effectThread);
                            }
                            break;

                        default:
                            break;
                    }
                }

                foreach (var item in DeleteThreads)
                {
                    DeleteEffect(item.ThreadName, null);
                }
            }

            public void UpdateEffect(string EffectName)
            {
                if (PlayerEffectThreads.ContainsKey(EffectName) == false)
                    return;

                BasePlayerEffectThread effectThread = PlayerEffectThreads[EffectName];

                switch (effectThread.PlayerThreadType)
                {
                    case PlayerThreadType.EffectThread:
                        break;

                    case PlayerThreadType.RealtimeEffectThread:
                        DateTime dt = effectThread.ExpireDate;
                        DateTime dtnow = DateTime.Now;

                        if (DateTime.Compare(dt, dtnow) <= 0)
                        {
                            DeleteEffect(EffectName, null);
                        }
                        break;

                    case PlayerThreadType.PlaytimeEffectThread:
                        if (Plugin.PlaytimeTracker == null)
                        {
                            break;
                        }
                        TimeSpan? PlayerPlaytime = Plugin.GetPlayerPlaytime(Id);
                        if (PlayerPlaytime != null && PlayerPlaytime > effectThread.ExpireTime)
                        {
                            DeleteEffect(EffectName, null);
                        }
                        break;

                    default:
                        break;
                }
            }

            public void AddEffect(string EffectThreadName, IPlayer player)
            {
                if (Plugin.registeredThreads.DataEffectThreads.ContainsKey(EffectThreadName))
                {
                    if (PlayerEffectThreads.ContainsKey(EffectThreadName))
                    {
                        BaseDataEffectThread regeffectThread = Plugin.registeredThreads.DataEffectThreads[EffectThreadName];
                        if (regeffectThread.DataThreadType == DataThreadType.PlaytimeEffectThreadData && Plugin.PlaytimeTracker == null)
                        {
                            Plugin.SendChatMessage(player, Plugin.Lang("PlaytimeTracker not Found", player?.Id));
                            return;
                        }
                        BasePlayerEffectThread effectThread = regeffectThread.ToPlayerEffectThread(Id);

                        PlayerEffectThreads[EffectThreadName] = effectThread;
                        effectThread.GrantUserPermissions(Id);

                        Plugin.SendReplyMessage(player, Plugin.Lang("EffectOverwrittedConsoleNotice", player?.Id, EffectThreadName, Name));
                        Plugin.SendChatMessage(Plugin.GetPlayer(Id), Plugin.Lang("EffectOverwrittedNotice", player?.Id, EffectThreadName));
                        Plugin.SendBroadcastMessage(Plugin.Lang("EffectOverwrittedConsoleNotice", player?.Id, EffectThreadName, Name));
                    }
                    else
                    {
                        BaseDataEffectThread regeffectThread = Plugin.registeredThreads.DataEffectThreads[EffectThreadName];
                        if (regeffectThread.DataThreadType == DataThreadType.PlaytimeEffectThreadData && Plugin.PlaytimeTracker == null)
                        {
                            Plugin.SendChatMessage(player, Plugin.Lang("PlaytimeTracker not Found", player?.Id));
                            return;
                        }
                        BasePlayerEffectThread effectThread = regeffectThread.ToPlayerEffectThread(Id);

                        PlayerEffectThreads.Add(EffectThreadName, effectThread);
                        effectThread.GrantUserPermissions(Id);

                        Plugin.SendReplyMessage(player, Plugin.Lang("EffectAddedConsoleNotice", player?.Id, EffectThreadName, Name));
                        Plugin.SendChatMessage(Plugin.GetPlayer(Id), Plugin.Lang("EffectAddedNotice", player?.Id, EffectThreadName));
                        Plugin.SendBroadcastMessage(Plugin.Lang("EffectAddedConsoleNotice", player?.Id, EffectThreadName, Name));
                    }

                    Plugin.UpdatePlayerGUI(Id);
                }
                else
                {
                    Plugin.SendReplyMessage(player, Plugin.Lang("Effect Is Not Exist", player?.Id, EffectThreadName));
                }
            }

            public void DeleteEffect(string EffectThreadName, IPlayer player)
            {
                if (PlayerEffectThreads.ContainsKey(EffectThreadName))
                {
                    PlayerEffectThreads[EffectThreadName].RevokeUserPermissions(Id, PlayerEffectThreads);
                    PlayerEffectThreads.Remove(EffectThreadName);

                    Plugin.SendReplyMessage(player, Plugin.Lang("EffectRemovedConsoleNotice", player?.Id, EffectThreadName, Name));
                    Plugin.SendChatMessage(Plugin.GetPlayer(Id), Plugin.Lang("EffectRemovedNotice", player?.Id, EffectThreadName));
                    Plugin.SendBroadcastMessage(Plugin.Lang("EffectRemovedConsoleNotice", player?.Id, EffectThreadName, Name));

                    Plugin.UpdatePlayersGUI();
                    if (PlayerEffectThreads.Count == 0)
                    {
                        Plugin.playersData.Players.Remove(Id);
                    }
                }
                else
                {
                    Plugin.SendReplyMessage(player, Plugin.Lang("Effect Is Not Exist On Player", player?.Id, EffectThreadName));
                }
            }

            public void DeleteAllEffect(IPlayer player)
            {
                if (PlayerEffectThreads.Count == 0)
                {
                    Plugin.SendReplyMessage(player, Plugin.Lang("Effect Is Not Exist On Player", player?.Id, "All"));
                    return;
                }
                else
                {
                    foreach (var item in PlayerEffectThreads)
                    {
                        item.Value.RevokeUserPermissions(Id);
                    }
                    Plugin.playersData.Players.Remove(Id);

                    Plugin.SendReplyMessage(player, Plugin.Lang("EffectRemovedConsoleNotice", player?.Id, "All", Name));
                    Plugin.SendChatMessage(Plugin.GetPlayer(Id), Plugin.Lang("EffectRemovedNotice", player?.Id, "All"));
                    Plugin.SendBroadcastMessage(Plugin.Lang("EffectRemovedConsoleNotice", player?.Id, "All", Name));

                    Plugin.UpdatePlayerGUI(Id);
                }
            }
        }

        #endregion

        #region GroupsData

        DynamicConfigFile groupsdataFile = Interface.Oxide.DataFileSystem.GetDatafile("PermissionEffects/GroupData");
        GroupsData groupsData;

        private void LoadGroupsData()
        {
            groupsData = groupsdataFile.ReadObject<GroupsData>();

            if (groupsData == null)
                groupsData = new GroupsData();
        }

        private void SaveGroupsData()
        {
            groupsdataFile.WriteObject(groupsData);
        }

        private class GroupsData
        {
            public Dictionary<string, GroupInfo> Groups { get; set; }

            public GroupsData()
            {
                Groups = new Dictionary<string, GroupInfo>();
            }

            public void ResetData(IPlayer player)
            {
                foreach (var item in Groups)
                {
                    foreach (var EffectThread in item.Value.PlayerEffectThreads)
                    {
                        EffectThread.Value.RevokeGroupPermissions(item.Value.GroupName);
                    }
                }
                Groups.Clear();
                Plugin.UpdatePlayersGUI();
                Plugin.SendReplyMessage(player, Plugin.Lang("Reseted GroupsData", player?.Id));
            }

            public void UpdateGroup()
            {
                List<string> ClonedGroupKey = new List<string>();
                foreach (var item in Groups)
                {
                    ClonedGroupKey.Add(item.Key);
                }

                var permissionGroups = Plugin.permission.GetGroups();

                foreach (var item in ClonedGroupKey)
                {
                    Groups[item].UpdateEffects();
                }

                foreach (var item in ClonedGroupKey)
                {
                    if (permissionGroups.Contains(item) == false)
                    {
                        if (Groups.ContainsKey(item))
                            Groups.Remove(item);
                    }
                }
            }

            public void AddEffectToGroup(string GroupName, IPlayer player, string EffectThreadName)
            {
                if (Plugin.permission.GroupExists(GroupName) == false)
                {
                    Plugin.SendReplyMessage(player, Plugin.Lang("GroupNotExist", player?.Id, GroupName));

                    return;
                }

                if (Groups.ContainsKey(GroupName) == false)
                {
                    Groups.Add(GroupName, new GroupInfo(GroupName));
                }

                Groups[GroupName].AddEffect(EffectThreadName, player);
            }

            public void DeleteEffectFromGroup(string GroupName, IPlayer player, string EffectThreadName)
            {
                if (Plugin.permission.GroupExists(GroupName) == false)
                {
                    Groups.Remove(GroupName);
                }

                if (Groups.ContainsKey(GroupName) == false)
                {
                    return;
                }

                Groups[GroupName].DeleteEffect(EffectThreadName, player);
            }

            public void DeleteAllEffectFromGroup(string GroupName, IPlayer player)
            {
                if (Groups.ContainsKey(GroupName) == false)
                {
                    Plugin.SendReplyMessage(player, Plugin.Lang("GroupNotExist", player?.Id, GroupName));

                    return;
                }

                Groups[GroupName].DeleteAllEffect(player);
            }

            public bool GroupHasEffect(string groupName, string effectThreadName)
            {
                if (Groups.ContainsKey(groupName) == false)
                {
                    return false;
                }

                GroupInfo groupInfo = Groups[groupName];

                if (groupInfo.PlayerEffectThreads.ContainsKey(effectThreadName))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public TimeSpan? GetGroupEffectLeftTime(string groupName, string effectThreadName)
            {
                if (Plugin.groupsData.Groups.ContainsKey(groupName) == false)
                    return null;

                var Group = Plugin.groupsData.Groups[groupName];

                if (Group.PlayerEffectThreads.ContainsKey(effectThreadName))
                {
                    BasePlayerEffectThread baseEffectThread = Group.PlayerEffectThreads[effectThreadName];

                    switch (baseEffectThread.PlayerThreadType)
                    {
                        case PlayerThreadType.EffectThread:
                            return TimeSpan.FromSeconds(-1);

                        case PlayerThreadType.RealtimeEffectThread:
                            TimeSpan LeftTime = baseEffectThread.ExpireDate.Subtract(DateTime.Now);
                            return LeftTime;

                        default:
                            break;
                    }
                }

                return null;
            }
        }

        private class GroupInfo
        {
            public string GroupName { get; set; }
            public Dictionary<string, BasePlayerEffectThread> PlayerEffectThreads { get; set; }

            public GroupInfo() { }

            public GroupInfo(string groupName)
            {
                GroupName = groupName;
                PlayerEffectThreads = new Dictionary<string, BasePlayerEffectThread>();
            }

            public void UpdateEffects()
            {
                List<BasePlayerEffectThread> DeleteThreads = new List<BasePlayerEffectThread>();
                var PlayerEffectThreadsList = PlayerEffectThreads.ToList();

                for (int j = 0; j < PlayerEffectThreadsList.Count; j++)
                {
                    BasePlayerEffectThread effectThread = PlayerEffectThreadsList[j].Value;

                    switch (effectThread.PlayerThreadType)
                    {
                        case PlayerThreadType.EffectThread:
                            break;

                        case PlayerThreadType.RealtimeEffectThread:
                            DateTime dt = effectThread.ExpireDate;
                            DateTime dtnow = DateTime.Now;

                            if (DateTime.Compare(dt, dtnow) <= 0)
                            {
                                DeleteThreads.Add(effectThread);
                            }
                            break;

                        default:
                            break;
                    }
                }

                foreach (var item in DeleteThreads)
                {
                    DeleteEffect(item.ThreadName, null);
                }
            }

            public void UpdateEffect(string EffectName)
            {
                if (PlayerEffectThreads.ContainsKey(EffectName) == false)
                    return;

                BasePlayerEffectThread effectThread = PlayerEffectThreads[EffectName];

                switch (effectThread.PlayerThreadType)
                {
                    case PlayerThreadType.EffectThread:
                        break;

                    case PlayerThreadType.RealtimeEffectThread:
                        DateTime dt = effectThread.ExpireDate;
                        DateTime dtnow = DateTime.Now;

                        if (DateTime.Compare(dt, dtnow) <= 0)
                        {
                            DeleteEffect(EffectName, null);
                        }
                        break;

                    default:
                        break;
                }
            }

            public void AddEffect(string EffectThreadName, IPlayer player)
            {
                if (Plugin.registeredThreads.DataEffectThreads.ContainsKey(EffectThreadName))
                {
                    if (PlayerEffectThreads.ContainsKey(EffectThreadName))
                    {
                        BaseDataEffectThread regeffectThread = Plugin.registeredThreads.DataEffectThreads[EffectThreadName];
                        if (regeffectThread.DataThreadType == DataThreadType.PlaytimeEffectThreadData)
                        {
                            Plugin.SendReplyMessage(player, Plugin.Lang("CantAddPlaytimeEffectToGroup", player?.Id));
                            return;
                        }
                        BasePlayerEffectThread effectThread = regeffectThread.ToPlayerEffectThread(null);

                        PlayerEffectThreads[EffectThreadName] = effectThread;
                        effectThread.GrantGroupPermissions(GroupName);

                        Plugin.SendReplyMessage(player, Plugin.Lang("EffectOverwrittedConsoleNoticeGroup", player?.Id, EffectThreadName, GroupName));
                        Plugin.SendBroadcastMessage(Plugin.Lang("EffectOverwrittedConsoleNoticeGroup", player?.Id, EffectThreadName, GroupName));
                    }
                    else
                    {
                        BaseDataEffectThread regeffectThread = Plugin.registeredThreads.DataEffectThreads[EffectThreadName];
                        if (regeffectThread.DataThreadType == DataThreadType.PlaytimeEffectThreadData)
                        {
                            Plugin.SendReplyMessage(player, Plugin.Lang("CantAddPlaytimeEffectToGroup", player?.Id));
                            return;
                        }
                        BasePlayerEffectThread effectThread = regeffectThread.ToPlayerEffectThread(null);

                        PlayerEffectThreads.Add(EffectThreadName, effectThread);
                        effectThread.GrantGroupPermissions(GroupName);

                        Plugin.SendReplyMessage(player, Plugin.Lang("EffectAddedConsoleNoticeGroup", player?.Id, EffectThreadName, GroupName));
                        Plugin.SendBroadcastMessage(Plugin.Lang("EffectAddedConsoleNoticeGroup", player?.Id, EffectThreadName, GroupName));
                    }

                    Plugin.UpdatePlayersGUI();
                }
                else
                {
                    Plugin.SendReplyMessage(player, Plugin.Lang("Effect Is Not Exist", player?.Id, EffectThreadName));
                }
            }

            public void DeleteEffect(string EffectThreadName, IPlayer player)
            {
                if (PlayerEffectThreads.ContainsKey(EffectThreadName))
                {
                    PlayerEffectThreads[EffectThreadName].RevokeGroupPermissions(GroupName, PlayerEffectThreads);
                    PlayerEffectThreads.Remove(EffectThreadName);
                    if (PlayerEffectThreads.Count == 0)
                    {
                        Plugin.groupsData.Groups.Remove(GroupName);
                    }

                    Plugin.SendReplyMessage(player, Plugin.Lang("EffectRemovedConsoleNoticeGroup", player?.Id, EffectThreadName, GroupName));
                    Plugin.SendBroadcastMessage(Plugin.Lang("EffectRemovedConsoleNoticeGroup", player?.Id, EffectThreadName, GroupName));

                    Plugin.UpdatePlayersGUI();
                }
                else
                {
                    Plugin.SendReplyMessage(player, Plugin.Lang("Effect Is Not Exist On Group", player?.Id, EffectThreadName));
                }
            }

            public void DeleteAllEffect(IPlayer player)
            {
                if (PlayerEffectThreads.Count != 0)
                {
                    foreach (var item in PlayerEffectThreads)
                    {
                        item.Value.RevokeGroupPermissions(GroupName);
                    }
                    Plugin.groupsData.Groups.Remove(GroupName);

                    Plugin.SendReplyMessage(player, Plugin.Lang("EffectRemovedConsoleNoticeGroup", player?.Id, "All", GroupName));
                    Plugin.SendBroadcastMessage(Plugin.Lang("EffectRemovedConsoleNoticeGroup", player?.Id, "All", GroupName));

                    Plugin.UpdatePlayersGUI();
                }
                else
                {
                    Plugin.SendReplyMessage(player, Plugin.Lang("Effect Is Not Exist On Group", player?.Id, "All"));
                }
            }
        }

        #endregion

        #region ThreadsData

        DynamicConfigFile threadsdataFile = Interface.Oxide.DataFileSystem.GetDatafile("PermissionEffects/Threads");
        RegisteredThreads registeredThreads;

        private void LoadThreadsData()
        {
            registeredThreads = threadsdataFile.ReadObject<RegisteredThreads>();

            if (registeredThreads == null)
                registeredThreads = new RegisteredThreads();
        }

        private void SaveThreadsData()
        {
            threadsdataFile.WriteObject(registeredThreads);
        }

        private class RegisteredThreads
        {
            public Dictionary<string, BaseDataEffectThread> DataEffectThreads { get; set; }

            public RegisteredThreads()
            {
                DataEffectThreads = new Dictionary<string, BaseDataEffectThread>();
            }

            public void RegisterThread(BaseDataEffectThread dataEffectThread, IPlayer player)
            {
                dataEffectThread.Permissions = dataEffectThread.Permissions.Distinct().ToList();

                List<string> DeletePerms = new List<string>();

                foreach (var permission in dataEffectThread.Permissions)
                {
                    if (Plugin.permission.PermissionExists(permission) == false)
                        DeletePerms.Add(permission);
                }

                foreach (var DeletePerm in DeletePerms)
                {
                    Plugin.SendReplyMessage(player, Plugin.Lang("Permission Is Not Exist", player?.Id, DeletePerm));
                    dataEffectThread.Permissions.Remove(DeletePerm);
                }

                if (dataEffectThread.Permissions.Count == 0)
                {
                    Plugin.SendReplyMessage(player, Plugin.Lang("Permission Is Empty", player?.Id, dataEffectThread.ThreadName));
                }
                else if (DataEffectThreads.ContainsKey(dataEffectThread.ThreadName))
                {
                    DataEffectThreads[dataEffectThread.ThreadName] = dataEffectThread;
                    Plugin.SendReplyMessage(player, Plugin.Lang("OverwriteThread", player?.Id, dataEffectThread.ThreadName));
                }
                else
                {
                    DataEffectThreads.Add(dataEffectThread.ThreadName, dataEffectThread);
                    Plugin.SendReplyMessage(player, Plugin.Lang("RegisterThread", player?.Id, dataEffectThread.ThreadName));
                }
            }

            public void UnRegisterThread(string effectThreadName, IPlayer player)
            {
                if (DataEffectThreads.ContainsKey(effectThreadName))
                {
                    DataEffectThreads.Remove(effectThreadName);
                    Plugin.SendReplyMessage(player, Plugin.Lang("UnregisterThread", player?.Id, effectThreadName));
                }
                else
                {
                    Plugin.SendReplyMessage(player, Plugin.Lang("Effect Is Not Exist", player?.Id, effectThreadName));
                }
            }

            public bool ThreadIsExist(string ThreadName)
            {
                if (DataEffectThreads.ContainsKey(ThreadName))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
        }

        #endregion

        #region PlayersGUIData

        DynamicConfigFile playersuidataFile = Interface.Oxide.DataFileSystem.GetDatafile("PermissionEffects/PlayerGUIData");
        PlayersGUIData playersUIData;

        private void LoadPlayersGUIData()
        {
            playersUIData = playersuidataFile.ReadObject<PlayersGUIData>();

            if (playersUIData == null)
                playersUIData = new PlayersGUIData();
        }

        private void SavePlayersGUIData()
        {
            playersuidataFile.WriteObject<PlayersGUIData>(playersUIData);
        }

        private class PlayersGUIData
        {
            public Dictionary<string, PlayerGUIInfo> Players = new Dictionary<string, PlayerGUIInfo>();

            public PlayersGUIData()
            {

            }

            public void ResetPlayerData(BasePlayer player)
            {
                if (!Players.ContainsKey(player.UserIDString))
                {
                    Players.Add(player.UserIDString, new PlayerGUIInfo(player.IPlayer));
                }
            }

            public void ResetOnlinePlayersData()
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    ResetPlayerData(player);
                }
            }
        }

        private class PlayerGUIInfo
        {
            public string Id;
            public string Name;
            public bool useUI = true;
            public int EffectPage = 0;

            public PlayerGUIInfo()
            {

            }

            public PlayerGUIInfo(IPlayer player)
            {
                Id = player.Id;
                Name = player.Name;
            }
        }

        #endregion

        #endregion

        #region LangManage

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "You do not have permission to use the '{0}' command.",
                ["SteamID Not Found"] = "Could not find this SteamID: {0}.",
                ["Player Not Found"] = "Could not find this player: {0}.",
                ["Multiple Players Found"] = "Found multiple players!\n\n{0}",

                ["PlaytimeTracker not Found"] = "PlaytimeTracker is not Loaded",

                ["RegisterThread"] = "Thread '{0}' is Registered",
                ["OverwriteThread"] = "Thread '{0}' is Overwritted",
                ["UnregisterThread"] = "Thread '{0}' is UnRegistered",

                ["RegisterThreadPlaytime"] = "Thread '{0}' is Registered Time Based On Playtime",
                ["OverwriteThreadPlaytime"] = "Thread '{0}' is Overwritted Time Based On Playtime",

                ["EffectAddedConsoleNotice"] = "Effect '{0}' is Added to {1}",
                ["EffectOverwrittedConsoleNotice"] = "Effect '{0}' is Overwritted to {1}",
                ["EffectRemovedConsoleNotice"] = "Effect '{0}' is Removed from {1}",

                ["EffectAddedConsoleNoticeGroup"] = "Effect '{0}' is Added to {1} Group",
                ["EffectOverwrittedConsoleNoticeGroup"] = "Effect '{0}' is Overwritted to {1} Group",
                ["EffectRemovedConsoleNoticeGroup"] = "Effect '{0}' is Removed from {1} Group",

                ["CantAddPlaytimeEffectToGroup"] = "Playtime-based effects cannot be added to the group",

                ["Effect Is Not Exist On Player"] = "Effect '{0}' is not exist on player!",
                ["Effect Is Not Exist On Group"] = "Effect '{0}' is not exist on group!",

                ["Reseted PlayersData"] = "Players Data has been initialized",
                ["Reseted GroupsData"] = "Groups Data has been initialized",

                ["EffectAddedNotice"] = "Effect '{0}' is Added",
                ["EffectOverwrittedNotice"] = "Effect '{0}' is Overwritted",
                ["EffectRemovedNotice"] = "Effect '{0}' is Removed",

                ["NotEnoughArgument"] = "to run this command you need {0} arguments, do /eps for more information.",
                ["Invalid Parameter"] = "'{0}' is an invalid parameter, do /eps for more information.",
                ["Effect Is Not Exist"] = "'{0}' is not exist effect.",
                ["Permission Is Not Exist"] = "'{0}' is not exist permission.",
                ["Permission Is Empty"] = "'{0}' Effect permission is Empty\n" +
                                      "Fail to Add Effect!",

                ["GroupNotExist"] = "{0} is not Exist Group",

                ["RegisteredDataList"] = "EPS Registered Threads\n" +
                                  "\n" +
                                  "{0}\n",

                ["RegisteredPermList"] = "{0}",

                ["RegisteredThreadList"] = "======================EffectThread\n" +
                                           "ThreadName : {0}\n" +
                                           "HexThreadColor : {1}\n" +
                                           "DisconnectAutoExpireTime : {2}\n" +
                                           "-----------Permissions\n" +
                                           "{3}\n" +
                                           "\n",
                ["RegisteredRtThreadList"] = "======================RealTImeEffectThread\n" +
                                           "ThreadName : {0}\n" +
                                           "HexThreadColor : {1}\n" +
                                           "DisconnectAutoExpireTime : {2}s\n" +
                                           "EffectTime : {3}s\n" +
                                           "-----------Permissions\n" +
                                           "{4}\n" +
                                           "\n",
                ["RegisteredPtThreadList"] = "======================PlayTimeEffectThread\n" +
                                           "ThreadName : {0}\n" +
                                           "HexThreadColor : {1}\n" +
                                           "DisconnectAutoExpireTime : {2}s\n" +
                                           "EffectTime : {3}s\n" +
                                           "-----------Permissions\n" +
                                           "{4}\n" +
                                           "\n",

                ["SingleThread"] = "{0} - {1}",
                ["SinglePlaytimeThread"] = "{0} - {1} playtime",

                ["PlayerThreads"] = "====================={0}\n" +
                                    "{1}\n" +
                                    "=======Contained In Group\n" +
                                    "{2}\n",

                ["PlayersThreadsHead"] = "Players EffectThreads\n",

                ["PlayerThreadsHead"] = "{0}'s EffectThreads\n",

                ["GroupsThreadsHead"] = "Groups EffectThreads\n",

                ["GroupThreads"] = "====================={0}\n" +
                                   "{1}\n",

                ["Data Is Empty"] = "no data",

                ["CommandHelp"] = "Permission Effects Commands\n" +
                                  "\n" +
                                  "---add effect to player/group\n" +
                                  "/eps add <user/group> <playername/groupname> <effectthread>\n" +
                                  "---remove effect from player/group\n" +
                                  "/eps remove <user/group> <playername/groupname> <effectthread>\n" +
                                  "---remove all effect from player/group\n" +
                                  "/eps removeall <user/group> <playername/groupname>\n" +
                                  "\n" +
                                  "---reset players/groups thread data\n" +
                                  "/eps reset <user/group/all>\n" +
                                  "---register thread\n" +
                                  "/eps thread add <threadname> <threadcolor> <permission1,permission2,...> <sec> <rt/pt> <DisconnExpireSec>\n" +
                                  "---unregister thread\n" +
                                  "/eps thread remove <threadname>\n" +
                                  "\n" +
                                  "---Check Registered Thread\n" +
                                  "/eps rlist\n" +
                                  "---Check Players Thread\n" +
                                  "/eps plist\n" +
                                  "---Check Groups Thread\n" +
                                  "/eps glist\n" +
                                  "---Check Player Thread\n" +
                                  "/eps pt <player>\n" +
                                  "---Check My Thread\n" +
                                  "/effect"
            }, this);
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        #endregion

        #region PermissionManage

        private void RegisterPermissions()
        {
            permission.RegisterPermission(permissioneffects_admin_perm_admin_perm, this);
        }

        #endregion

        #endregion

        #region TestEffectThreads

        private void RegisterTestEffects()
        {
            permission.RegisterPermission("permissioneffects.test", this);

            registeredThreads.RegisterThread(GetTestEffectRegThread(), null);
            registeredThreads.RegisterThread(GetTestEffect2RegThread(), null);
            registeredThreads.RegisterThread(GetTestEffect3RegThread(), null);
        }

        private EffectThreadData GetTestEffectRegThread()
        {
            string ThreadName = "TestThread";

            List<string> effectPermissions = new List<string>();
            effectPermissions.Add("permissioneffects.test");

            return new EffectThreadData(ThreadName, GetHexColor(Color.red), effectPermissions, null);
        }

        private PlaytimeEffectThreadData GetTestEffect2RegThread()
        {
            string ThreadName = "TestThread2";

            List<string> effectPermissions = new List<string>();
            effectPermissions.Add("permissioneffects.test");

            return new PlaytimeEffectThreadData(ThreadName, GetHexColor(Color.gray), effectPermissions, new TimeSpan(0, 3, 0), new TimeSpan(0, 10, 0));
        }

        private RealtimeEffectThreadData GetTestEffect3RegThread()
        {
            string ThreadName = "TestThread3";

            List<string> effectPermissions = new List<string>();
            effectPermissions.Add("permissioneffects.test");

            return new RealtimeEffectThreadData(ThreadName, GetHexColor(Color.green), effectPermissions, new TimeSpan(1, 0, 0), new TimeSpan(1, 0, 0));
        }

        #endregion

        #region TimerManager

        private void StartTimer()
        {
            timer.Every(config.TimeLimitTimerInterval, TimeLimitTimer_Tick);
            timer.Every(config.UIUpdateTimerInterval, UIUpdateTimer_Tick);
            timer.Every(config.DisconnectDetectTimerInterval, DisconnectDetectTimer_Tick);
        }

        private void TimeLimitTimer_Tick()
        {
            playersData.UpdatePlayers();
            groupsData.UpdateGroup();
        }

        private void UIUpdateTimer_Tick()
        {
            UpdatePlayersGUI();
        }

        private void DisconnectDetectTimer_Tick()
        {
            List<BasePlayer> BasePlayerList = BasePlayer.activePlayerList.ToList();

            var Playerslist = playersData.Players.ToList();

            for (int i = 0; i < Playerslist.Count; i++)
            {
                PlayerInfo playerInfo = Playerslist[i].Value;

                List<BasePlayerEffectThread> DeleteThreads = new List<BasePlayerEffectThread>();
                var PlayerEffectThreadsList = playerInfo.PlayerEffectThreads.ToList();

                for (int j = 0; j < playerInfo.PlayerEffectThreads.Count; j++)
                {
                    BasePlayerEffectThread effectThread = PlayerEffectThreadsList[j].Value;

                    if (effectThread.DisconnectAutoExpireTime != null)
                    {
                        if (PlayerIsOnline(playerInfo.Id) == true)
                        {
                            DateTime EffectDisconnectExpireTime = DateTime.Now;
                            TimeSpan timeSpan = (TimeSpan)effectThread.DisconnectAutoExpireTime;
                            EffectDisconnectExpireTime = EffectDisconnectExpireTime + timeSpan;
                            effectThread.DisconnectAutoExpireDate = EffectDisconnectExpireTime;
                        }
                        else
                        {
                            DateTime dt = effectThread.DisconnectAutoExpireDate;
                            if (DateTime.Compare(dt, DateTime.Now) <= 0)
                            {
                                DeleteThreads.Add(effectThread);
                            }
                        }
                    }
                }

                foreach (var item in DeleteThreads)
                {
                    playerInfo.DeleteEffect(item.ThreadName, null);
                }
            }
        }

        #endregion

        #region InterCommand

        #region GUI

        private void UpdatePlayerGUI(string Id)
        {
            UpdatePagePlayerUIInfo(GetPlayerFromID(Id));
        }

        private void UpdatePlayerGUI(BasePlayer player)
        {
            UpdatePagePlayerUIInfo(player);
        }

        private void UpdatePlayersGUI()
        {
            UpdateUIInfo();
        }

        private void UpdateUIInfo()
        {
            List<BasePlayer> BasePlayerList = BasePlayer.activePlayerList.ToList();

            foreach (var BPlayeritem in BasePlayerList)
            {
                if (playersUIData.Players.ContainsKey(BPlayeritem.UserIDString) == false)
                    continue;
                PlayerGUIInfo playerGUIInfo = playersUIData.Players[BPlayeritem.UserIDString];

                List<BasePlayerEffectThread> basePlayerEffectThreads = GetPlayerGUIData(BPlayeritem.UserIDString);
                if (basePlayerEffectThreads.Count == 0)
                    continue;
                List<BasePlayerEffectThread> DisplayEffectthread = new List<BasePlayerEffectThread>();
                bool UseUpPM = false;
                bool UseDownPM = false;

                if (basePlayerEffectThreads.Count - 3 < playerGUIInfo.EffectPage)
                {
                    if (basePlayerEffectThreads.Count <= 3)
                        playerGUIInfo.EffectPage = 0;
                    else
                        playerGUIInfo.EffectPage = basePlayerEffectThreads.Count - 3;
                }

                if (3 < basePlayerEffectThreads.Count)
                {
                    if (playerGUIInfo.EffectPage != basePlayerEffectThreads.Count - 3)
                    {
                        UseUpPM = true;
                    }
                    if (playerGUIInfo.EffectPage > 0)
                    {
                        UseDownPM = true;
                    }
                }

                for (int i = 0; i < 3; i++)
                {
                    if (basePlayerEffectThreads.Count == playerGUIInfo.EffectPage + i)
                        break;

                    DisplayEffectthread.Add(basePlayerEffectThreads[playerGUIInfo.EffectPage + i]);
                }

                BasePlayer BPlayer = GetPlayerFromID(BPlayeritem.UserIDString);
                if (BPlayer != null)
                    UpdateBaseUI(BPlayer, DisplayEffectthread, UseUpPM, UseDownPM);
            }
        }

        private void UpdatePagePlayerUIInfo(BasePlayer player)
        {
            if (playersUIData.Players.ContainsKey(player.UserIDString) == false)
                return;

            PlayerGUIInfo playerGUIInfo = playersUIData.Players[player.UserIDString];

            List<BasePlayerEffectThread> basePlayerEffectThreads = GetPlayerGUIData(player.UserIDString);
            List<BasePlayerEffectThread> DisplayEffectthread = new List<BasePlayerEffectThread>();

            bool UseUpPM = false;
            bool UseDownPM = false;

            if (basePlayerEffectThreads.Count - 3 < playerGUIInfo.EffectPage)
            {
                if (basePlayerEffectThreads.Count <= 3)
                    playerGUIInfo.EffectPage = 0;
                else
                    playerGUIInfo.EffectPage = basePlayerEffectThreads.Count - 3;
            }

            if (3 < basePlayerEffectThreads.Count)
            {
                if (playerGUIInfo.EffectPage != basePlayerEffectThreads.Count - 3)
                {
                    UseUpPM = true;
                }
                if (playerGUIInfo.EffectPage > 0)
                {
                    UseDownPM = true;
                }
            }

            var PlayerEffectThreadsList = basePlayerEffectThreads.ToList();

            for (int i = 0; i < 3; i++)
            {
                if (basePlayerEffectThreads.Count == playerGUIInfo.EffectPage + i)
                    break;

                DisplayEffectthread.Add(PlayerEffectThreadsList[playerGUIInfo.EffectPage + i]);
            }

            UpdateBaseUI(player, DisplayEffectthread, UseUpPM, UseDownPM);
        }

        private List<BasePlayerEffectThread> GetPlayerGUIData(string Id)
        {
            List<BasePlayerEffectThread> effectthreadUIs = new List<BasePlayerEffectThread>();

            if (playersData.Players.ContainsKey(Id))
            {
                PlayerInfo playerinfo = playersData.Players[Id];

                foreach (var effectthread in playerinfo.PlayerEffectThreads)
                {
                    effectthreadUIs.Add(effectthread.Value);
                }
            }
            foreach (var groupname in permission.GetUserGroups(Id))
            {
                if (groupsData.Groups.ContainsKey(groupname) == false)
                    continue;

                foreach (var effectthread in groupsData.Groups[groupname].PlayerEffectThreads)
                {
                    effectthreadUIs.Add(effectthread.Value);
                }
            }

            return effectthreadUIs.Distinct().ToList();
        }

        #endregion

        #region GetListString

        private string GetRegisteredThread(IPlayer player)
        {
            string effectthreads = "";

            if (registeredThreads.DataEffectThreads.Count == 0)
                effectthreads += Lang("no data", player?.Id);

            foreach (var Thread in registeredThreads.DataEffectThreads)
            {
                string TeffectPermissions = "";

                foreach (var PermissioniT in Thread.Value.Permissions)
                {
                    TeffectPermissions += Lang("RegisteredPermList", player?.Id, PermissioniT);
                }

                switch (Thread.Value.DataThreadType)
                {
                    case DataThreadType.EffectThreadData:
                        effectthreads += Lang("RegisteredThreadList", player?.Id, Thread.Value.ThreadName, Thread.Value.ThreadColor, Thread.Value.DisconnectAutoExpireTime?.TotalSeconds, TeffectPermissions);
                        break;

                    case DataThreadType.RealtimeEffectThreadData:
                        effectthreads += Lang("RegisteredRtThreadList", player?.Id, Thread.Value.ThreadName, Thread.Value.ThreadColor, Thread.Value.DisconnectAutoExpireTime?.TotalSeconds, Thread.Value.ExpireTime.TotalSeconds, TeffectPermissions);
                        break;

                    case DataThreadType.PlaytimeEffectThreadData:
                        effectthreads += Lang("RegisteredPtThreadList", player?.Id, Thread.Value.ThreadName, Thread.Value.ThreadColor, Thread.Value.DisconnectAutoExpireTime?.TotalSeconds, Thread.Value.ExpireTime.TotalSeconds, TeffectPermissions);
                        break;

                    default:
                        break;
                }
            }

            effectthreads = effectthreads.Substring(0, effectthreads.Length - 2);

            return Lang("RegisteredDataList", player?.Id, effectthreads);
        }

        private string GetPlayersThread(IPlayer player)
        {
            string playersThread = Lang("PlayersThreadsHead", player?.Id);

            if (playersData.Players.Count == 0)
                playersThread += Lang("no data", player?.Id);

            foreach (var playerinfo in playersData.Players)
            {
                bool hasgroupeffect = false;
                int hidecount = 0;
                if (playerinfo.Value.PlayerEffectThreads.Count == 0)
                    hidecount += 1;
                if (Plugin.permission.UserHasAnyGroup(playerinfo.Value.Id) == false)
                    hidecount += 2;

                string playerThread = "";

                foreach (var effectthread in playerinfo.Value.PlayerEffectThreads)
                {
                    string Lefttime;
                    TimeSpan LeftTime;

                    switch (effectthread.Value.PlayerThreadType)
                    {
                        case PlayerThreadType.EffectThread:
                            Lefttime = "-1";

                            playerThread += Lang("SingleThread", player?.Id, effectthread.Value.ThreadName, Lefttime) + "\n";
                            break;

                        case PlayerThreadType.RealtimeEffectThread:
                            DateTime dt = (effectthread.Value as RealtimeEffectThread).ExpireDate;

                            LeftTime = dt.Subtract(DateTime.Now);
                            Lefttime = LeftTime.ToString();

                            playerThread += Lang("SingleThread", player?.Id, effectthread.Value.ThreadName, Lefttime) + "\n";
                            break;

                        case PlayerThreadType.PlaytimeEffectThread:
                            TimeSpan? PlayerPlaytime = Plugin.GetPlayerPlaytime(playerinfo.Value.Id);

                            if (PlayerPlaytime == null)
                                Lefttime = "Error";
                            else
                            {
                                LeftTime = effectthread.Value.ExpireTime - (TimeSpan)PlayerPlaytime;
                                Lefttime = LeftTime.ToString();
                            }


                            playerThread += Lang("SinglePlaytimeThread", player?.Id, effectthread.Value.ThreadName, Lefttime) + "\n";
                            break;

                        default:
                            Lefttime = "-1";

                            playerThread += Lang("SingleThread", player?.Id, effectthread.Value.ThreadName, Lefttime) + "\n";
                            break;
                    }
                }

                string playerGroupThread = "";
                if (Plugin.permission.UserHasAnyGroup(playerinfo.Value.Id) == false)
                    playerGroupThread += Lang("no data", player?.Id);

                foreach (var groupname in Plugin.permission.GetUserGroups(playerinfo.Value.Id))
                {
                    if (groupsData.Groups.ContainsKey(groupname) == false)
                        continue;

                    foreach (var effectthread in groupsData.Groups[groupname].PlayerEffectThreads)
                    {
                        hasgroupeffect = true;

                        string Lefttime;

                        switch (effectthread.Value.PlayerThreadType)
                        {
                            case PlayerThreadType.EffectThread:
                                Lefttime = "-1";
                                break;

                            case PlayerThreadType.RealtimeEffectThread:
                                DateTime dt = (effectthread.Value as RealtimeEffectThread).ExpireDate;

                                TimeSpan LeftTime = dt.Subtract(DateTime.Now);
                                Lefttime = LeftTime.ToString();
                                break;

                            default:
                                Lefttime = "-1";
                                break;
                        }

                        playerGroupThread += Lang("SingleThread", player?.Id, effectthread.Value.ThreadName, Lefttime) + "\n";
                    }
                }

                if (hidecount == 3)
                    continue;
                if (hidecount == 1 && hasgroupeffect == false)
                    continue;

                playersThread += Lang("PlayerThreads", player?.Id, playerinfo.Value.Name, playerThread, playerGroupThread);
            }

            return playersThread;
        }

        private string GetPlayerThread(IPlayer player, IPlayer target)
        {
            string playerRThread = Lang("PlayerThreadsHead", player?.Id, target.Name);

            if (playersData.Players.ContainsKey(target.Id) == false)
                playerRThread += Lang("no data", player?.Id);
            else
            {
                PlayerInfo playerinfo = playersData.Players[target.Id];

                bool hasgroupeffect = false;
                int hidecount = 0;
                if (playerinfo.PlayerEffectThreads.Count == 0)
                    hidecount += 1;
                if (Plugin.permission.UserHasAnyGroup(playerinfo.Id) == false)
                    hidecount += 2;

                string playerThread = "";

                foreach (var effectthread in playerinfo.PlayerEffectThreads)
                {
                    string Lefttime;
                    TimeSpan LeftTime;

                    switch (effectthread.Value.PlayerThreadType)
                    {
                        case PlayerThreadType.EffectThread:
                            Lefttime = "-1";

                            playerThread += Lang("SingleThread", player?.Id, effectthread.Value.ThreadName, Lefttime) + "\n";
                            break;

                        case PlayerThreadType.RealtimeEffectThread:
                            DateTime dt = (effectthread.Value as RealtimeEffectThread).ExpireDate;

                            LeftTime = dt.Subtract(DateTime.Now);
                            Lefttime = LeftTime.ToString();

                            playerThread += Lang("SingleThread", player?.Id, effectthread.Value.ThreadName, Lefttime) + "\n";
                            break;

                        case PlayerThreadType.PlaytimeEffectThread:
                            TimeSpan? PlayerPlaytime = Plugin.GetPlayerPlaytime(playerinfo.Id);

                            if (PlayerPlaytime == null)
                                Lefttime = "Error";
                            else
                            {
                                LeftTime = effectthread.Value.ExpireTime - (TimeSpan)PlayerPlaytime;
                                Lefttime = LeftTime.ToString();
                            }

                            playerThread += Lang("SinglePlaytimeThread", player?.Id, effectthread.Value.ThreadName, Lefttime) + "\n";
                            break;

                        default:
                            Lefttime = "-1";

                            playerThread += Lang("SingleThread", player?.Id, effectthread.Value.ThreadName, Lefttime) + "\n";
                            break;
                    }
                }

                string playerGroupThread = "";
                if (Plugin.permission.UserHasAnyGroup(playerinfo.Id) == false)
                    playerGroupThread += Lang("no data", player?.Id);

                foreach (var groupname in Plugin.permission.GetUserGroups(playerinfo.Id))
                {
                    if (groupsData.Groups.ContainsKey(groupname) == false)
                        continue;
                    foreach (var effectthread in groupsData.Groups[groupname].PlayerEffectThreads)
                    {
                        hasgroupeffect = true;

                        string Lefttime;

                        switch (effectthread.Value.PlayerThreadType)
                        {
                            case PlayerThreadType.EffectThread:
                                Lefttime = "-1";
                                break;

                            case PlayerThreadType.RealtimeEffectThread:
                                DateTime dt = effectthread.Value.ExpireDate;

                                TimeSpan LeftTime = dt.Subtract(DateTime.Now);
                                Lefttime = LeftTime.ToString();
                                break;

                            default:
                                Lefttime = "-1";
                                break;
                        }

                        playerGroupThread += Lang("SingleThread", player?.Id, effectthread.Value.ThreadName, Lefttime) + "\n";
                    }
                }

                if (hidecount == 3)
                {
                    playerRThread = Lang("no data", player?.Id);
                    return playerRThread;
                }
                if (hidecount == 1 && hasgroupeffect == false)
                {
                    playerRThread = Lang("no data", player?.Id);
                    return playerRThread;
                }

                playerRThread += Lang("PlayerThreads", player?.Id, playerinfo.Name, playerThread, playerGroupThread);
                return playerRThread;
            }

            return playerRThread;
        }

        private string GetGroupThread(IPlayer player)
        {
            string GroupsThread = Lang("GroupsThreadsHead", player?.Id);

            if (groupsData.Groups.Count == 0)
                return GroupsThread += Lang("no data", player?.Id);

            foreach (var groupinfo in groupsData.Groups)
            {
                string GroupThread = "";

                if (groupinfo.Value.PlayerEffectThreads.Count == 0)
                    GroupThread += Lang("no data", player?.Id);

                foreach (var effectthread in groupsData.Groups[groupinfo.Value.GroupName].PlayerEffectThreads)
                {
                    string Lefttime;

                    switch (effectthread.Value.PlayerThreadType)
                    {
                        case PlayerThreadType.EffectThread:
                            Lefttime = "-1";
                            break;

                        case PlayerThreadType.RealtimeEffectThread:
                            DateTime dt = (effectthread.Value as RealtimeEffectThread).ExpireDate;

                            TimeSpan LeftTime = dt.Subtract(DateTime.Now);
                            Lefttime = LeftTime.ToString();
                            break;

                        default:
                            Lefttime = "-1";
                            break;
                    }

                    GroupThread += Lang("SingleThread", player?.Id, effectthread.Value.ThreadName, Lefttime) + "\n";
                }

                GroupsThread += Lang("GroupThreads", player?.Id, groupinfo.Value.GroupName, GroupThread);
            }

            return GroupsThread;
        }

        #endregion

        #endregion

        #region Command

        void BasicCommand(IPlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
            {
                if (!player.HasPermission(permissioneffects_admin_perm_admin_perm) && !player.IsServer)
                {
                    SendReplyMessage(player, Lang("No Permission", player?.Id, command));
                    return;
                }
            }

            if (args.Length >= 1)
            {
                if (args[0].ToLower() == "add")
                {
                    if (args.Length >= 4)
                    {
                        if (args[1].ToLower() == "user")
                        {
                            IPlayer Tplayer = GetPlayer(args[2], player);

                            if (Tplayer == null)
                                return;

                            playersData.AddEffectToPlayer(Tplayer, player, args[3]);
                        }
                        if (args[1].ToLower() == "group")
                        {
                            groupsData.AddEffectToGroup(args[2], player, args[3]);
                        }
                    }
                    else
                    {
                        SendReplyMessage(player, Lang("NotEnoughArgument", player?.Id, 4.ToString()));
                    }
                }
                else if (args[0].ToLower() == "remove")
                {
                    if (args.Length >= 4)
                    {
                        if (args[1].ToLower() == "user")
                        {
                            IPlayer Tplayer = GetPlayer(args[2], player);

                            if (Tplayer == null)
                                return;

                            playersData.DeleteEffectFromPlayer(Tplayer.Id, player, args[3]);
                        }
                        if (args[1].ToLower() == "group")
                        {
                            groupsData.DeleteEffectFromGroup(args[2], player, args[3]);
                        }
                    }
                    else
                    {
                        SendReplyMessage(player, Lang("NotEnoughArgument", player?.Id, 4.ToString()));
                    }
                }
                else if (args[0].ToLower() == "removeall")
                {
                    if (args.Length >= 3)
                    {
                        if (args[1].ToLower() == "user")
                        {
                            IPlayer Tplayer = GetPlayer(args[2], player);

                            if (Tplayer == null)
                                return;

                            playersData.DeleteAllEffectFromPlayer(Tplayer, player);
                        }
                        else if (args[1].ToLower() == "group")
                        {
                            groupsData.DeleteAllEffectFromGroup(args[2], player);
                        }
                    }
                    else
                    {
                        SendReplyMessage(player, Lang("NotEnoughArgument", player?.Id, 3.ToString()));
                    }
                }
                else if (args[0].ToLower() == "reset")
                {
                    if (args.Length >= 2)
                    {
                        if (args[1].ToLower() == "user")
                        {
                            playersData.ResetData(player);
                        }
                        else if (args[1].ToLower() == "group")
                        {
                            groupsData.ResetData(player);
                        }
                        else if (args[1].ToLower() == "all")
                        {
                            playersData.ResetData(player);
                            groupsData.ResetData(player);
                        }
                    }
                    else
                    {
                        SendReplyMessage(player, Lang("NotEnoughArgument", player?.Id, 3.ToString()));
                    }
                }
                else if (args[0].ToLower() == "thread")
                {
                    if (args.Length >= 2)
                    {
                        if (args[1].ToLower() == "add")
                        {
                            if (args.Length >= 7)
                            {
                                if (args[6].ToLower() == "rt")
                                {
                                    int arg5i = 0;
                                    int arg7i = 0;
                                    if (int.TryParse(args[5], out arg5i) && int.TryParse(args[7], out arg7i))
                                    {
                                        string[] splitedperms = args[4].ToLower().Split(',');

                                        BaseDataEffectThread baseDataEffectThread;
                                        if (arg5i == 0)
                                            baseDataEffectThread = new EffectThreadData(args[2], GetHexColorFromFormat(args[3]), splitedperms.ToList(), (arg7i == 0) ? null : (TimeSpan?)TimeSpan.FromSeconds(arg7i));
                                        else
                                            baseDataEffectThread = new RealtimeEffectThreadData(args[2], GetHexColorFromFormat(args[3]), splitedperms.ToList(), (arg7i == 0) ? null : (TimeSpan?)TimeSpan.FromSeconds(arg7i), TimeSpan.FromSeconds(arg5i));

                                        registeredThreads.RegisterThread(baseDataEffectThread, player);
                                    }
                                    else
                                    {
                                        SendReplyMessage(player, Lang("Invalid Parameter", player?.Id, args[1]));
                                    }
                                }
                                else if (args[6].ToLower() == "pt")
                                {
                                    int arg5i = 0;
                                    int arg7i = 0;
                                    if (int.TryParse(args[5], out arg5i) && int.TryParse(args[7], out arg7i))
                                    {
                                        string[] splitedperms = args[4].ToLower().Split(',');

                                        BaseDataEffectThread baseDataEffectThread;
                                        if (arg5i == 0)
                                            baseDataEffectThread = new EffectThreadData(args[2], GetHexColorFromFormat(args[3]), splitedperms.ToList(), (arg7i == 0) ? null : (TimeSpan?)TimeSpan.FromSeconds(arg7i));
                                        else
                                            baseDataEffectThread = new PlaytimeEffectThreadData(args[2], GetHexColorFromFormat(args[3]), splitedperms.ToList(), (arg7i == 0) ? null : (TimeSpan?)TimeSpan.FromSeconds(arg7i), TimeSpan.FromSeconds(arg5i));

                                        registeredThreads.RegisterThread(baseDataEffectThread, player);
                                    }
                                    else
                                    {
                                        SendReplyMessage(player, Lang("Invalid Parameter", player?.Id, args[1]));
                                    }
                                }
                                else
                                {
                                    SendReplyMessage(player, Lang("Invalid Parameter", player?.Id, args[1]));
                                }
                            }
                            else
                            {
                                SendReplyMessage(player, Lang("NotEnoughArgument", player?.Id, 7.ToString()));
                            }
                        }
                        else if (args[1].ToLower() == "remove")
                        {
                            if (args.Length >= 3)
                            {
                                registeredThreads.UnRegisterThread(args[2], player);
                            }
                            else
                            {
                                SendReplyMessage(player, Lang("NotEnoughArgument", player?.Id, 3.ToString()));
                            }
                        }
                        else
                        {
                            SendReplyMessage(player, Lang("Invalid Parameter", player?.Id, args[1]));
                        }
                    }
                    else
                    {
                        SendReplyMessage(player, Lang("NotEnoughArgument", player?.Id, 2.ToString()));
                    }
                }
                else if (args[0] == "rlist")
                {
                    SendReplyMessage(player, GetRegisteredThread(player));
                }
                else if (args[0] == "plist")
                {
                    SendReplyMessage(player, GetPlayersThread(player));
                }
                else if (args[0] == "glist")
                {
                    SendReplyMessage(player, GetGroupThread(player));
                }
                else if (args[0] == "pt")
                {
                    if (args.Length >= 2)
                    {
                        IPlayer Tplayer = GetPlayer(args[1], player);

                        if (Tplayer == null)
                            return;

                        SendReplyMessage(player, GetPlayerThread(player, Tplayer));
                    }
                    else
                    {
                        SendReplyMessage(player, Lang("NotEnoughArgument", player?.Id, 2.ToString()));
                    }
                }
                else
                {
                    SendReplyMessage(player, Lang("Invalid Parameter", player?.Id, args[0]));
                }
            }
            else
            {
                SendReplyMessage(player, Lang("CommandHelp", player?.Id));
            }
        }

        void CheckEffectCommand(IPlayer player, string command, string[] args)
        {
            SendReplyMessage(player, GetPlayerThread(player, player));
        }

        void RegisterCommands()
        {
            AddCovalenceCommand(config.EffectCommand, nameof(CheckEffectCommand));
            AddCovalenceCommand(config.EPSCommand, nameof(BasicCommand));
        }

        #region GUI

        [Command("EffecterUI.set")]
        void SetEffectUIPage(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                return;
            }

            var BPlayer = player.Object as BasePlayer;

            if (args.Length >= 1)
            {
                switch (args[0])
                {
                    case "++":
                        PlayerAddCursor(BPlayer);
                        playersUIData.Players[player.Id].EffectPage++;
                        UpdatePagePlayerUIInfo(BPlayer);
                        break;
                    case "--":
                        PlayerAddCursor(BPlayer);
                        if (playersUIData.Players[player.Id].EffectPage > 0)
                        {
                            playersUIData.Players[player.Id].EffectPage--;
                            UpdatePagePlayerUIInfo(BPlayer);
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        [Command("eui")]
        void SetUICommand(IPlayer player, string command, string[] args)
        {
            if (player.IsServer)
            {
                return;
            }

            var BPlayer = player.Object as BasePlayer;
            PlayerGUIInfo playerGUIInfo = playersUIData.Players[player.Id];

            if (playerGUIInfo.useUI == true)
            {
                PlayerAddCursor(BPlayer);
                playerGUIInfo.useUI = false;
                UpdatePagePlayerUIInfo(BPlayer);
                SendReplyMessage(player, "off");
            }
            else
            {
                PlayerAddCursor(BPlayer);
                playerGUIInfo.useUI = true;
                UpdatePagePlayerUIInfo(BPlayer);
                SendReplyMessage(player, "on");
            }
        }

        #endregion

        #endregion

        #region UI

        private void OnUIScaleChanged(IPlayer player)
        {
            PlayerAddBaseinvUI(player.Object as BasePlayer);
            UpdatePagePlayerUIInfo(player.Object as BasePlayer);
        }

        private void PlayerAddCursor(BasePlayer player)
        {
            CuiElementContainer CursorUI = new CuiElementContainer();

            CursorUI.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image =
                    {
                        Color = "0 0 0 0"
                    },

                RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0.00001 0.00001"
                    },
            }, "Hud", CursorUIPanel);

            CuiHelper.DestroyUi(player, CursorUIPanel);
            CuiHelper.AddUi(player, CursorUI);

            if (PlayersViewingUi.ContainsKey(player.UserIDString))
            {
                PlayersViewingUi[player.UserIDString].Reset();
            }
            else
            {
                PlayersViewingUi.Add(player.UserIDString, timer.Once(3f, () =>
                {
                    CuiHelper.DestroyUi(player, CursorUIPanel);
                    PlayersViewingUi.Remove(player.UserIDString);
                }));
            }
        }

        private void PlayerAddBaseinvUI(BasePlayer player)
        {
            CuiElementContainer EffectUI = new CuiElementContainer();

            string AnchorMin = GetAnchorMin(player, 0);
            string AnchorMax = GetAnchorMax(player, 3);

            EffectUI.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image =
                    {
                        Color = "0 0 0 0"
                    },

                RectTransform =
                    {
                        AnchorMin = AnchorMin,
                        AnchorMax = AnchorMax
                    },
            }, "Hud", UIBaseInvPanel);

            CuiHelper.DestroyUi(player, UIBaseInvPanel);
            CuiHelper.AddUi(player, EffectUI);
        }

        private void PlayersAddBaseinvUI()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiElementContainer EffectUI = new CuiElementContainer();

                string AnchorMin = GetAnchorMin(player, 0);
                string AnchorMax = GetAnchorMax(player, 3);

                EffectUI.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image =
                {
                    Color = "0 0 0 0"
                },

                    RectTransform =
                {
                    AnchorMin = AnchorMin,
                    AnchorMax = AnchorMax
                },
                }, "Hud", UIBaseInvPanel);

                CuiHelper.DestroyUi(player, UIBaseInvPanel);
                CuiHelper.AddUi(player, EffectUI);
            }
        }

        private void PlayersRemoveBaseinvUI()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, UIBaseInvPanel);
            }
        }

        private void UpdateBaseUI(BasePlayer player, List<BasePlayerEffectThread> effectthreadUIs, bool useUpPM, bool useDownPM)
        {
            CuiElementContainer EffectUI = new CuiElementContainer();

            EffectUI.Add(new CuiPanel
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
            }, UIBaseInvPanel, UIBasePanel);

            if (playersUIData.Players[player.UserIDString].useUI)
            {
                for (int i = 0; i < effectthreadUIs.Count; i++)
                {
                    EffectUI.AddRange(SingleEffectUI(player, i, effectthreadUIs[i]));
                }
                if (effectthreadUIs.Count > 0)
                    EffectUI.AddRange(EndBarUI(effectthreadUIs.Count));

                EffectUI.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image =
                    {
                        Color = "0 0 0 0"
                    },

                    RectTransform =
                    {
                        AnchorMin = "0.92 0",
                        AnchorMax = "0.997 1"
                    },
                }, UIBasePanel, UIBaseBtnPanel);

                if (effectthreadUIs.Count > 0)
                {
                    if (useUpPM)
                    {
                        EffectUI.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = GetSingleAnchorMin(2),
                                AnchorMax = GetBtnAnchorMax(2)
                            },
                            Button =
                            {
                                Color = "1 1 1 0.4",
                                Command = "EffecterUI.set ++"
                            },
                            Text =
                            {
                                Text = "△",
                                FontSize = 17,
                                Align = TextAnchor.MiddleCenter
                            }
                        }, UIBaseBtnPanel, UIPMBtn);
                    }
                    if (useDownPM)
                    {
                        EffectUI.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = GetSingleAnchorMin(1),
                                AnchorMax = GetBtnAnchorMax(1)
                            },
                            Button =
                            {
                                Color = "1 1 1 0.4",
                                Command = "EffecterUI.set --"
                            },
                            Text =
                            {
                                Text = "▽",
                                FontSize = 17,
                                Align = TextAnchor.MiddleCenter
                            }
                        }, UIBaseBtnPanel, UIPMBtn);
                    }

                    EffectUI.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = GetSingleAnchorMin(0),
                            AnchorMax = GetBtnAnchorMax(0)
                        },
                        Button =
                        {
                            Color = "1 1 1 0.5",
                            Command = "eui"
                        },
                        Text =
                        {
                            Text = "▼",
                            FontSize = 13,
                            Align = TextAnchor.MiddleCenter
                        }
                    }, UIBaseBtnPanel, ESBtn);
                }
            }
            else
            {
                if (effectthreadUIs.Count > 0)
                {
                    EffectUI.Add(new CuiPanel
                    {
                        CursorEnabled = false,
                        Image =
                        {
                            Color = "0 0 0 0"
                        },

                        RectTransform =
                        {
                            AnchorMin = "0.92 0",
                            AnchorMax = "0.997 1"
                        },
                    }, UIBasePanel, UIBaseBtnPanel);

                    EffectUI.Add(new CuiButton
                    {
                        RectTransform =
                    {
                        AnchorMin = GetSingleAnchorMin(0),
                        AnchorMax = GetBtnAnchorMax(0)
                    },
                        Button =
                    {
                        Color = "1 1 1 0.3",
                        Command = "eui"
                    },
                        Text =
                    {
                        Text = "▲",
                        FontSize = 13,
                        Align = TextAnchor.MiddleCenter
                    }
                    }, UIBaseBtnPanel, ESBtn);
                }
            }

            CuiHelper.DestroyUi(player, UIBasePanel);
            CuiHelper.AddUi(player, EffectUI);
        }

        private CuiElementContainer SingleEffectUI(BasePlayer player, int index, BasePlayerEffectThread effectthreadUI)
        {
            CuiElementContainer EffectUI = new CuiElementContainer();

            string AnchorMin = GetSingleAnchorMin(index);
            string AnchorMax = GetSingleAnchorMax(index);
            string LeftTImeAnchorMax = GetLeftTImeAnchorMax(player, index);

            Color EffectColor;
            if (ColorUtility.TryParseHtmlString(effectthreadUI.ThreadColor, out EffectColor) == false)
                EffectColor = Color.cyan;
            string UIEffectColor = EffectColor.r.ToString() + ' ' + EffectColor.g.ToString() + ' ' + EffectColor.b.ToString();

            string TextColorThreadName;
            string TextColorLefttime;

            if (effectthreadUI.PlayerThreadType == PlayerThreadType.PlaytimeEffectThread)
            {
                TimeSpan? playtime = GetPlayerPlaytime(player.UserIDString);
                if (playtime == null)
                    TextColorLefttime = "<color=black>" + GetLeftTimeString(new TimeSpan()) + "</color>";
                else
                    TextColorLefttime = "<color=black>" + GetLeftTimeString(effectthreadUI.ExpireTime - (TimeSpan)playtime) + "</color>";
                TextColorThreadName = "<color=black>" + effectthreadUI.ThreadName + "</color>";
            }
            else
            {
                TextColorLefttime = "<color=white>" + GetLeftTimeString(effectthreadUI.ExpireDate - DateTime.Now) + "</color>";
                TextColorThreadName = "<color=white>" + effectthreadUI.ThreadName + "</color>";
            }

            EffectUI.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image =
                {
                    Color = UIEffectColor + " 0.6"
                },

                RectTransform =
                {
                    AnchorMin = AnchorMin,
                    AnchorMax = AnchorMax
                },
            }, UIBasePanel, BackGroundPanel);

            EffectUI.Add(new CuiLabel
            {
                Text =
                {
                    Text = TextColorThreadName,
                    FontSize = 13,
                    Align = TextAnchor.MiddleCenter
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
            }, BackGroundPanel);

            if (effectthreadUI.PlayerThreadType != PlayerThreadType.EffectThread)
            {
                EffectUI.Add(new CuiPanel
                {
                    CursorEnabled = false,
                    Image =
                    {
                        Color = UIEffectColor + " 0.7"
                    },

                    RectTransform =
                    {
                        AnchorMin = AnchorMin,
                        AnchorMax = LeftTImeAnchorMax
                    },
                }, UIBasePanel, BackGroundPanel);

                EffectUI.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = TextColorLefttime,
                        FontSize = 13,
                        Align = TextAnchor.MiddleCenter
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    }
                }, BackGroundPanel);
            }

            return EffectUI;
        }

        private CuiElementContainer EndBarUI(int index)
        {
            CuiElementContainer EffectUI = new CuiElementContainer();

            string AnchorMin = GetSingleAnchorMin(index);
            string AnchorMax = GetEndAnchorMax(index);

            EffectUI.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image =
                {
                    Color = "1 1 1 0.3"
                },

                RectTransform =
                {
                    AnchorMin = AnchorMin,
                    AnchorMax = AnchorMax
                },
            }, UIBasePanel, BackGroundPanel);

            return EffectUI;
        }

        double IndexGap = 0.031;

        private string GetAnchorMin(BasePlayer player, int index)
        {
            float BeltSize = GetUISize(player.UserIDString);
            int CompareBeltSize = (int)(BeltSize * 10);

            string YMin;
            if (config.UIPosions.ContainsKey(CompareBeltSize))
                YMin = (config.UIPosions[CompareBeltSize].YAnchorMin + (IndexGap * index)).ToString();
            else
                YMin = (config.UIPosions.ElementAt(0).Value.YAnchorMin + (IndexGap * index)).ToString();

            if (config.UIPosions.ContainsKey(CompareBeltSize))
                return config.UIPosions[CompareBeltSize].XAnchorMin + " " + YMin;
            else
                return config.UIPosions.ElementAt(0).Value.XAnchorMin + " " + YMin;
        }

        private string GetAnchorMax(BasePlayer player, int index)
        {
            float BeltSize = GetUISize(player.UserIDString);
            int CompareBeltSize = (int)(BeltSize * 10);

            string YMax;
            if (config.UIPosions.ContainsKey(CompareBeltSize))
                YMax = (config.UIPosions[CompareBeltSize].YAnchorMax + 0.03 + (IndexGap * index)).ToString();
            else
                YMax = (config.UIPosions.ElementAt(0).Value.YAnchorMax + 0.03 + (IndexGap * index)).ToString();

            if (config.UIPosions.ContainsKey(CompareBeltSize))
                return config.UIPosions[CompareBeltSize].XAnchorMax + " " + YMax;
            else
                return config.UIPosions.ElementAt(0).Value.XAnchorMax + " " + YMax;
        }

        private string GetLeftTImeAnchorMax(BasePlayer player, int index)
        {
            return "0.22 " + (0.29 * (index + 1) + 0.015 * index).ToString();
        }

        private string GetEndAnchorMax(int index)
        {
            return "1 " + ((0.29 * (index) + 0.015 * index) + 0.07).ToString();
        }

        private string GetSingleAnchorMin(int index)
        {
            return "0 " + (0.29 * (index) + 0.015 * index).ToString();
        }

        private string GetSingleAnchorMax(int index)
        {
            return "1 " + (0.29 * (index + 1) + 0.015 * index).ToString();
        }

        private string GetBtnAnchorMax(int index)
        {
            return "1 " + (0.29 * (index + 1) + 0.015 * index - 0.01).ToString();
        }

        #endregion

        #region API

        private void API_AddEffectToPlayer(IPlayer player, string EffectThreadName)
        {
            playersData.AddEffectToPlayer(player, null, EffectThreadName);
        }

        private void API_DeleteEffectFromPlayer(IPlayer player, string EffectThreadName)
        {
            playersData.DeleteEffectFromPlayer(player.Id, null, EffectThreadName);
        }

        private void API_AddEffectToGroup(string GroupName, string EffectThreadName)
        {
            groupsData.AddEffectToGroup(GroupName, null, EffectThreadName);
        }

        private void API_DeleteEffectFromGroup(string GroupName, string EffectThreadName)
        {
            groupsData.DeleteEffectFromGroup(GroupName, null, EffectThreadName);
        }

        private void API_DeleteAllEffectFromGroup(string GroupName)
        {
            groupsData.DeleteAllEffectFromGroup(GroupName, null);
        }

        private void API_RegisterThread(int ThreadType, string ThreadName, string HexThreadColor, string[] Permissions, int AutoExpireTime, int EffectTime)
        {
            TimeSpan? autoExpireTime;

            if (AutoExpireTime == 0)
                autoExpireTime = null;
            else
                autoExpireTime = TimeSpan.FromSeconds(AutoExpireTime);

            BaseDataEffectThread effectThread = null;

            if (EffectTime == 0)
            {
                effectThread = new EffectThreadData(ThreadName, GetHexColorFromFormat(HexThreadColor), Permissions.ToList(), autoExpireTime);
            }

            switch (ThreadType)
            {
                case 0:
                    effectThread = new RealtimeEffectThreadData(ThreadName, GetHexColorFromFormat(HexThreadColor), Permissions.ToList(), autoExpireTime, TimeSpan.FromSeconds(EffectTime));
                    break;

                case 1:
                    effectThread = new PlaytimeEffectThreadData(ThreadName, GetHexColorFromFormat(HexThreadColor), Permissions.ToList(), autoExpireTime, TimeSpan.FromSeconds(EffectTime));
                    break;

                default:
                    break;
            }

            if (effectThread != null)
                registeredThreads.RegisterThread(effectThread, null);
        }

        private void API_UnRegisterThread(string ThreadName)
        {
            registeredThreads.UnRegisterThread(ThreadName, null);
        }

        private bool API_ThreadIsExist(string ThreadName)
        {
            return registeredThreads.ThreadIsExist(ThreadName);
        }

        private bool API_PlayerHasEffect(string playerId, string effectThreadName)
        {
            return playersData.PlayerHasEffect(playerId, effectThreadName);
        }

        private TimeSpan? API_GetPlayerEffectLeftTime(IPlayer player, string effectThreadName)
        {
            return playersData.GetPlayerEffectLeftTime(player.Id, effectThreadName);
        }

        #endregion

        #region Helper

        private float GetUISize(string playerID)
        {
            if (UIScaleManager != null)
                return (float)UIScaleManager.Call("API_CheckPlayerUISize", playerID);
            else
                return 0;
        }

        private string GetLeftTimeString(TimeSpan lefttime)
        {
            string lefttimestr;

            if (lefttime == new TimeSpan(0, 0, -1))
                lefttimestr = "-1";
            else if (0 < lefttime.Days)
                lefttimestr = lefttime.ToString(@"dd\.hh\:mm\:ss");
            else
                lefttimestr = lefttime.ToString(@"hh\:mm\:ss");

            return lefttimestr;
        }

        private TimeSpan? GetPlayerPlaytime(string playerID)
        {
            if (PlaytimeTracker != null)
            {
                object obj = PlaytimeTracker.Call("GetPlayTime", playerID);

                if (obj != null)
                {
                    return TimeSpan.FromSeconds((double)obj);
                }
            }
            return null;
        }

        private string GetHexColorFromFormat(string color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(GetColorFromString(color));
        }

        private string GetHexColor(Color color)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(color);
        }

        private Color GetColorFromString(string str)
        {
            str = str.ToLower();

            Color color = new Color();

            switch (str)
            {
                case "black":
                    color = Color.black;
                    break;
                case "blue":
                    color = Color.blue;
                    break;
                case "clear":
                    color = Color.clear;
                    break;
                case "cyan":
                    color = Color.cyan;
                    break;
                case "gray":
                    color = Color.gray;
                    break;
                case "green":
                    color = Color.green;
                    break;
                case "magenta":
                    color = Color.magenta;
                    break;
                case "red":
                    color = Color.red;
                    break;
                case "white":
                    color = Color.white;
                    break;
                case "yellow":
                    color = Color.yellow;
                    break;
                default:
                    if (ColorUtility.TryParseHtmlString(str, out color) == false)
                        color = Color.cyan;
                    break;
            }

            return color;
        }

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
                    SendReplyMessage(player, Lang("SteamID Not Found", player?.Id, nameOrID));

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
                    SendReplyMessage(player, Lang("Player Not Found", player?.Id, nameOrID));
                    break;
                case 1:
                    return foundPlayers[0];
                default:
                    string[] names = (from current in foundPlayers select $"- {current.Name}").ToArray();
                    SendReplyMessage(player, Lang("Multiple Players Found", player?.Id, string.Join("\n", names)));
                    break;
            }
            return null;
        }

        private IPlayer GetPlayer(string nameOrID)
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
                    break;
                case 1:
                    return foundPlayers[0];
                default:
                    string[] names = (from current in foundPlayers select $"- {current.Name}").ToArray();
                    break;
            }
            return null;
        }

        private BasePlayer GetPlayerFromID(string ID)
        {
            List<BasePlayer> BasePlayerList = BasePlayer.activePlayerList.ToList();

            BasePlayer result = BasePlayerList.Find((p) => p.UserIDString == ID);

            return result;
        }

        private bool PlayerIsOnline(string ID)
        {
            List<BasePlayer> BasePlayerList = BasePlayer.activePlayerList.ToList();

            BasePlayer result = BasePlayerList.Find((p) => p.UserIDString == ID);

            if (result == null)
                return false;
            else
                return true;
        }

        private void SendChatMessage(IPlayer player, string message)
        {
            if (message == null)
                return;
            if (player == null)
                Puts(message);
            else
                (player.Object as BasePlayer).ChatMessage(message);
        }

        private void SendReplyMessage(IPlayer player, string message)
        {
            if (player == null)
                Puts(message);
            else
                player.Reply(message);
        }

        private void SendBroadcastMessage(string message)
        {
            if (config.use_BroadCast)
            {
                foreach (IPlayer current in players.Connected)
                    SendChatMessage(current, message);
            }
        }

        private void puts(string msg)
        {
            if (config.use_ConsoleMessage == true)
                Puts(msg);
        }

        private void printWarning(string msg)
        {
            if (config.use_ConsoleMessage == true)
                PrintWarning(msg);
        }

        #endregion
    }
}