#region

using ConVar;
using Facepunch.Extend;
using Facepunch.Math;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#endregion

namespace Oxide.Plugins
{
    [Info("Game Server Tools", "mrcameron999", "1.0.7")]
    [Description("Adds several adamin tools for use with gameservertools.com")]
    public class GameServerTools : CovalencePlugin
    {
        private Dictionary<string, ApprovedCachedPlayer> _approvedCachedJoins = new Dictionary<string, ApprovedCachedPlayer>(); //stored data on player approve
        private readonly Dictionary<IPlayer, CachedPlayer> _cachedJoins = new Dictionary<IPlayer, CachedPlayer>(); //Stored data on playerConnected

        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>();
        //private readonly string _connection = "https://localhost:44361/";
        private readonly string _connection = "https://api.gameservertools.com/";
        
        private readonly float timeout = 1000f;

        private string _linkedGroupName = "DiscordLinked";
        private string _nitroGroupName = "Nitro";
        private int _port = 28015;
        private bool _showClaimMessage;
        private bool _loggingEnabled = false;
        private bool _disableBanCtrl = false;

        private Dictionary<ulong, List<string>> messages = new Dictionary<ulong, List<string>>();

        private void Init() // Called when server starts. Created any missing groups
        {
            LoadConfigData();
            permission.CreateGroup(_linkedGroupName, "linked", 0);
            permission.CreateGroup(_nitroGroupName, "nitro", 0);
            permission.RegisterPermission("GameServerTools.linked", this);
            permission.GrantGroupPermission(_linkedGroupName, "GameServerTools.linked", this);

            _headers.Add("Content-Type", "application/json");

            if (_disableBanCtrl)
            {
                Unsubscribe("OnPlayerBanned");
                Unsubscribe("OnUserApprove");
            }
            else
            {
                AddUniversalCommand("ban", "OverrideBanCommand");
            }

            if(_loggingEnabled)
                Puts($"GST Debug Info\n Your server port is {_port} your api key is {_headers["ApiKey"]}");
        }

        private void LoadConfigData() //Loads config data
        {
            if (Config["DisableBanCtrl"] == null)
            {
                Config["DisableBanCtrl"] = false;
                SaveConfig();
            }
            if (Config["Port"] == null)
            {
                Config["Port"] = 28015;
                SaveConfig();
            }


            string apiKey = Config["APIKEY"].ToString();

            if (apiKey == "")
                LogError("NO API KEY PROVIDED! Please ensure you have added your api key in the config file");
            else
                _headers.Add("ApiKey", apiKey);

            _linkedGroupName = Config["OxideGroupNameForLinked"].ToString();
            _nitroGroupName = Config["OxideGroupNameForNitro"].ToString();
            _showClaimMessage = (bool)Config["DisplayMessageOnClaimRewards"];
            _loggingEnabled = bool.Parse(Config["DebugLoggingEnabled"].ToString());
            _disableBanCtrl = bool.Parse(Config["DisableBanCtrl"].ToString());

            _port = int.Parse(Config["Port"].ToString());
        }

        #region Hooks

        private void OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel) //Stores all player chats and uses them later for reports
        {
            List<string> messageList;
            if (!messages.TryGetValue(player.userID, out messageList))
            {
                messageList = new List<string>();
                messages.Add(player.userID, messageList);
            }

            messageList.Insert(0, $"{DateTime.UtcNow} UTC | {channel} | {message}");
        }

        private void OnPlayerReported(BasePlayer reporter, string targetName, string targetId, string subject, string message, string type)
        {
            ReportType typeOfReport = GetTypeIdFromType(type);

            Dictionary<string, object> parameters = new Dictionary<string, object>
            {
                { "ReporterId", reporter.userID.ToString() },
                { "ReportedPlayerId", targetId },
                { "Subject", subject },
                { "Reason", message },
                { "ServerPort", _port },
                { "Type", typeOfReport }
            };

            List<KeyValuePair<int, List<string>>> files = new List<KeyValuePair<int, List<string>>>();

            if (typeOfReport == ReportType.Abusive || typeOfReport == ReportType.Spam) // Get the chat log
            {
                ulong playerIdLong = ulong.Parse(targetId);
                if (messages.ContainsKey(playerIdLong))
                {
                    List<string> userMessages = messages[playerIdLong];
                    if (userMessages.Count > 100)
                    {
                        userMessages.RemoveRange(100, userMessages.Count);
                    }

                    files.Add(new KeyValuePair<int, List<string>>(0, messages[playerIdLong]));
                }
            }

            if (typeOfReport == ReportType.Cheat)
            {
                BasePlayer target = BasePlayer.Find(targetId);
                if (target != null)
                {
                    int oldDelay = ConVar.Server.combatlogdelay;
                    ConVar.Server.combatlogdelay = 0;
                    string combatLogString = target.stats.combat.Get(ConVar.Server.combatlogsize);
                    ConVar.Server.combatlogdelay = oldDelay;

                    files.Add(new KeyValuePair<int, List<string>>(1, combatLogString.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None).ToList()));
                }
            }

            parameters.Add("TextFiles", files);

            SendReport(parameters);
        }

        private void OnServerInitialized(bool initial)
        {
            if (initial)
            {
                ClearAllPlayerConnections();
            }
        }

        //This may fail but it doesnt matter it gets called on start up.(server crash, above method)
        private void OnServerShutdown() => ClearAllPlayerConnections();

        private void OnPlayerDisconnected(BasePlayer player)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();

            parameters.Add("SteamId", player.UserIDString);
            parameters.Add("ServerPort", _port);

            UserLeftServer(parameters);
        }

        private void OnPlayerBanned(string name, ulong id, string address, string reason)
        {
            AddBanClass ban = new AddBanClass();

            ban.SteamId = id.ToString();
            ban.Reason = reason;
            ban.ExpireTime = null;
            ban.BannedBy = null;

            SubmitNewBan(ban, null, (newBan) =>
            {
                ServerUsers.User user = ServerUsers.Get(id);
                if (user == null || user.group != ServerUsers.UserGroup.Banned)
                {
                    Puts("no user found that is banned");
                }
                else
                {
                    ServerUsers.Remove(id);
                    ServerUsers.Save();
                }
            });
        }

        private void OnUserApprove(Network.Connection connection)
        {
            ulong ownerId = connection.userid;

            string id = connection.userid.ToString();
            if (_approvedCachedJoins.ContainsKey(id))
            {
                ApprovedCachedPlayer data = _approvedCachedJoins[id];
                TimeSpan timeSinceAdd = DateTime.Now - data.timeOfAdd;

                if (timeSinceAdd.TotalMinutes < 1)
                {
                    timer.Once(5.0f, () => // Prevents spam joinning
                    {
                        if (connection != null)
                            Network.Net.sv.Kick(connection, data.reason, false);
                    });

                    return;
                }

                _approvedCachedJoins.Remove(id);
            }

            CheckForBans(connection, ownerId);
        }

        private void OnUserConnected(IPlayer player)
        {
            FetchDiscordLinkData(player);

            UserConnectedToServer(player);
        }

        #endregion Hooks

        #region Web Requests
        private void CheckForBans(Network.Connection connection, ulong ownerId)
        {
            webrequest.Enqueue($"{_connection}api/Ban/GetActiveBans?steamId={ownerId}&serverPort={_port}", null, (code, response) =>
            //webrequest.Enqueue($"{apiUrl}/api/Ban/ReturnSelf?thing='hello'", null, (code, response) =>
            {
                if (code == 200)
                {
                    List<AddBanClass> bans = JsonConvert.DeserializeObject<List<AddBanClass>>(response);
                    if (bans.Count > 0)
                    {
                        AddBanClass banSuccess = bans.FirstOrDefault();

                        string kickReason = lang.GetMessage("YouAreBannedMessage", this, connection.userid.ToString());
                        kickReason = kickReason.Replace("@reason", banSuccess.Reason);
                        Network.Net.sv.Kick(connection, kickReason, false);

                        _approvedCachedJoins.Add(connection.userid.ToString(), new ApprovedCachedPlayer() { reason = kickReason, timeOfAdd = DateTime.Now });
                    }
                }
                else if (code == 204)
                {
                    //Puts($"You are not banned!");
                }
                else if (code == 0)
                {
                    Puts($"-=-=-=-gameservertools.com is unreachable!-=-=-=-=-");
                }
                else
                {
                    Puts($"Something went wrong code {code} {response}");
                }
            }, this, Core.Libraries.RequestMethod.GET, _headers);
        }
        private void SendReport(Dictionary<string, object> parameters)
        {
            string body = JsonConvert.SerializeObject(parameters);

            if (_loggingEnabled)
                Puts($"Sending report...");

            webrequest.Enqueue($"{_connection}api/Report/AddReport", body, (code, response) =>
            {
                if (_loggingEnabled)
                    Puts($"Got result back!\nCode: {code}\n Response: {response}");
            }, this, RequestMethod.POST, _headers, timeout);
        }

        private void ClearAllPlayerConnections()
        {
            webrequest.Enqueue($"{_connection}api/Stat/ServerStarted?serverPort={_port}", null, (code, response) =>
            {
            }, this, Core.Libraries.RequestMethod.POST, _headers, timeout);
        }

        private void SubmitNewBan(AddBanClass newBan, IPlayer admin, Action<AddBanClass> successCallBack)
        {
            newBan.ServerPort = _port;
            string body = JsonConvert.SerializeObject(newBan);
            webrequest.Enqueue($"{_connection}/api/Ban/AddBan", body, (code, response) =>
            {
                if (code == 200)// Success
                {
                    AddBanClass banSuccess = JsonConvert.DeserializeObject<AddBanClass>(response);

                    BasePlayer playerToKick = BasePlayer.Find(banSuccess.SteamId.ToString());
                    if (playerToKick != null && playerToKick.IsConnected)
                    {
                        string messageReplaced = lang.GetMessage("YouAreBannedMessage", this, playerToKick.UserIDString);
                        messageReplaced = messageReplaced.Replace("@reason", banSuccess.Reason);
                        playerToKick.Kick(messageReplaced);
                    }

                    string broadCastMessage = lang.GetMessage("PlayerBannedBroadcastMsg", this, playerToKick.UserIDString);
                    broadCastMessage = broadCastMessage.Replace("@user", banSuccess.Reason);
                    Chat.Broadcast(broadCastMessage);

                    successCallBack.Invoke(banSuccess);
                }
                else if (code == 400)
                {
                    if (admin != null && admin.IsConnected)
                    {
                        string messageReplaced = lang.GetMessage("FailedToBan", this, admin.Id);
                        messageReplaced = messageReplaced.Replace("@response", response);
                        admin.Reply(messageReplaced);
                    }
                    Puts($"Failed to ban user! {response}");
                }
                else if (code == 401)
                {
                    if (admin != null && admin.IsConnected)
                    {
                        string messageReplaced = lang.GetMessage("FailedToBanNoPermission", this, admin.Id);
                        admin.Reply(messageReplaced);
                    }
                    Puts($"Failed to ban user! {response}");
                }
                else
                {
                    if (admin != null && admin.IsConnected)
                    {
                        string messageReplaced = lang.GetMessage("FailedToBan", this, admin.Id);
                        messageReplaced = messageReplaced.Replace("@response", response);
                        admin.Reply(messageReplaced);
                    }

                    Puts($"Ban failed: {response} Code: {code}");
                }
            }, this, Core.Libraries.RequestMethod.PUT, _headers);
        }

        private void UserLeftServer(Dictionary<string, object> parameters)
        {
            string body = JsonConvert.SerializeObject(parameters);

            webrequest.Enqueue($"{_connection}api/Stat/UserLeftServer", body, (code, response) =>
            {
            }, this, Core.Libraries.RequestMethod.PUT, _headers, timeout);
        }

        private void UserConnectedToServer(IPlayer player)
        {
            Dictionary<string, object> parameters = new Dictionary<string, object>();

            parameters.Add("SteamId", player.Id);
            parameters.Add("ServerPort", _port);

            string body = JsonConvert.SerializeObject(parameters);
            webrequest.Enqueue($"{_connection}api/Stat/UserJoinnedServer", body, (code, response) =>
            {
            }, this, Core.Libraries.RequestMethod.POST, _headers, timeout);
        }

        private void FetchDiscordLinkData(IPlayer player)
        {
            webrequest.Enqueue($"{_connection}api/Link/GetLinkData?steamId={player.Id}", null, (code, response) =>
            {
                if (code == 200)
                {
                    LinkModel linkData = JsonConvert.DeserializeObject<LinkModel>(response);
                    //check if they have left the discord here
                    HandleDiscordLinkerConnect(linkData, player);
                }
                else
                {
                    player.Reply("Something went wrong with this command. Please contact the admin of the server");
                    Debug.LogError($"Error checking link: {response} {code}");
                }
            }, this, RequestMethod.GET, _headers, timeout);
        }

        #endregion WebRequests

        #region DiscordLinker

        private void HandleDiscordLinkerConnect(LinkModel linkData, IPlayer player)
        {
            if (linkData.LinkId == 0) // not linked
            {
                string joinMessageNotLinked = lang.GetMessage("JoinMessageNotLinked", this, player.Id);
                player.Reply($"{joinMessageNotLinked}");

                bool userHasGroup = player.BelongsToGroup(_linkedGroupName);
                if (userHasGroup)
                {
                    player.RemoveFromGroup(_linkedGroupName);
                    player.RemoveFromGroup(_nitroGroupName);
                    Interface.CallHook("OnDiscordUserUnLinked", player);
                }
            }
            else if (linkData.LinkId != 0 && !linkData.InDiscord) // left discord
            {
                string joinMessageLeft = lang.GetMessage("JoinMessageLeft", this, player.Id);
                player.Reply($"{joinMessageLeft}");
                //removes the from the group encase they unlinked their account

                bool userHasGroup = player.BelongsToGroup(_linkedGroupName);
                if (userHasGroup)
                {
                    player.RemoveFromGroup(_linkedGroupName);
                    player.RemoveFromGroup(_nitroGroupName);
                    Interface.CallHook("OnDiscordUserUnLinked", player);
                }
            }
            else // Linked
            {
                string joinMessageLinked = lang.GetMessage("JoinMessageLinked", this, player.Id);
                player.Reply($"{joinMessageLinked}");
                bool userHasGroup = player.BelongsToGroup(_linkedGroupName);
                if (!userHasGroup)
                {
                    player.AddToGroup(_linkedGroupName);
                    Interface.CallHook("OnDiscordUserAddedToGroup", player);
                }

                // Check for nitro rewards
                bool userHasNitro = player.BelongsToGroup(_nitroGroupName);
                if (userHasNitro && !linkData.NitroBoosted)
                {
                    string noLongerBoostingMessage = lang.GetMessage("NitroLostMessage", this, player.Id);

                    player.Reply(noLongerBoostingMessage);

                    player.RemoveFromGroup(_nitroGroupName);
                    Interface.CallHook("OnNitroBoostRemove", player);
                    // Do discord nitro stuff here
                }
                else if (!userHasNitro && linkData.NitroBoosted)
                {
                    string nowBoostingMessage = lang.GetMessage("NitroGainMessage", this, player.Id);
                    player.Reply(nowBoostingMessage);

                    player.AddToGroup(_nitroGroupName);
                    Interface.CallHook("OnNitroBoost", player);
                }
            }
        }

        #endregion

        #region Helpers

        private ReportType GetTypeIdFromType(string type)
        {
            switch (type)
            {
                case "abusive":
                    return ReportType.Abusive;

                case "cheat":
                    return ReportType.Cheat;

                case "spam":
                    return ReportType.Spam;

                case "name":
                    return ReportType.Name;
            }
            return ReportType.Abusive;
        }

        private void MessageAllPlayers(string message)
        {
            foreach (IPlayer player in players.Connected)
            {
                player.Message(message);
            }
        }

        private bool TryGetBanExpiry(
          string arg,
          int n,
          IPlayer iplayer,
          out long expiry,
          out string durationSuffix)
        {
            expiry = GetTimestamp(arg, n, -1L);
            durationSuffix = (string)null;
            int current = Epoch.Current;
            if (expiry > 0L && expiry <= (long)current)
            {
                string messageReplaced = lang.GetMessage("PastExireDate", this, iplayer.Id);
                iplayer.Reply(messageReplaced);
                return false;
            }
            durationSuffix = expiry > 0L ? " for " + (expiry - (long)current).FormatSecondsLong() : "";
            return true;
        }

        private long GetTimestamp(string arg, int iArg, long def = 0)
        {
            string s = arg == string.Empty ? null : arg;
            if (s == null)
                return def;
            int num = 3600;
            if (s.Length > 1 && char.IsLetter(s[s.Length - 1]))
            {
                switch (s[s.Length - 1])
                {
                    case 'M':
                        num = 2592000;
                        break;

                    case 'Y':
                        num = 31536000;
                        break;

                    case 'd':
                        num = 86400;
                        break;

                    case 'h':
                        num = 3600;
                        break;

                    case 'm':
                        num = 60;
                        break;

                    case 's':
                        num = 1;
                        break;

                    case 'w':
                        num = 604800;
                        break;
                }

                s = s.Substring(0, s.Length - 1);
            }
            long result;
            if (!long.TryParse(s, out result))
                return def;
            if (result > 0L && result <= 315360000L)
                result = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + result * (long)num;
            return result;
        }

        #endregion helpers

        #region Commands

        [Command("Near")]
        private void FindNear(IPlayer iplayer, string command, string[] args)
        {
            if (iplayer.IsServer)
            {
                BasePlayer target = BasePlayer.Find(args[0]);
                if (target != null)
                {
                    IOrderedEnumerable<BasePlayer> orderList = BasePlayer.activePlayerList.OrderBy(p => Vector3.Distance(p.transform.position, target.transform.position));

                    int i = 0;
                    List<ulong> discordId = new List<ulong>();
                    foreach (BasePlayer player in orderList)
                    {
                        if (player.userID == target.userID)
                        {
                            continue;
                        }

                        if (i >= 15)
                        {
                            break;
                        }
                        discordId.Add(player.userID);
                        i++;
                    }
                    iplayer.Reply(String.Join(",", discordId.ToArray()));
                }
            }
            else
            {
                iplayer.Reply("Not server");
            }
        }

        [Command("checklink")] //Used by gst to automatically check a users account. This is not called by users
        private void CheckLinkCommand(IPlayer iplayer, string command, string[] args)
        {
            if (!iplayer.IsServer) return;

            BasePlayer player = BasePlayer.Find(args[0]);
            if (player == null || !player.IsConnected)
            {
                return;
            }

            if (args.Length > 1)
            {
                if (_showClaimMessage)
                {
                    string messageReplaced = lang.GetMessage("BroadcastMessage", this, iplayer.Id);
                    string newMessage = messageReplaced.Replace("@userName", player.displayName);
                    MessageAllPlayers(newMessage);
                }
            }

            FetchDiscordLinkData(player.IPlayer);
        }

        [Command("nitro", "linked")]
        private void NitroCheck(IPlayer iplayer, string command, string[] args)
        {
            CachedPlayer data;
            if (_cachedJoins.TryGetValue(iplayer, out data))
            {
                TimeSpan timeSinceAdd = DateTime.Now - data.TimeOfAdd;
                if (timeSinceAdd.TotalMinutes < 1)
                {
                    string message = lang.GetMessage("RecentlyUsedThisCommand", this, iplayer.Id);
                    iplayer.Reply(message);
                    return;
                }

                _cachedJoins.Remove(iplayer);
            }

            _cachedJoins.Add(iplayer, new CachedPlayer());

            string messageCheckingAccount = lang.GetMessage("CheckingAccount", this, iplayer.Id);
            iplayer.Reply(messageCheckingAccount);

            FetchDiscordLinkData(iplayer);
        }

        private void OverrideBanCommand(IPlayer iplayer, string command, string[] args)
        {
            if (!iplayer.IsAdmin && !iplayer.IsServer) // Replicate normal server permissions
            {
                return;
            }
            if (args.Length < 1) // Replicate normal server permissions
            {
                string messageReplaced = lang.GetMessage("InvalidArguments", this, iplayer.Id);
                iplayer.Reply(messageReplaced);
                return;
            }

            BasePlayer player = args[0] == null ? null : BasePlayer.Find(args[0]);
            if (player == null || player.net == null || player.net.connection == null)
            {
                string messageReplaced = lang.GetMessage("NoPlayerFound", this, iplayer.Id);
                iplayer.Reply(messageReplaced);
            }
            else
            {
                string noReasonString = lang.GetMessage("NoReason", this, iplayer.Id);

                string notes = args.Length < 2 ? noReasonString : args[1];

                long expiry;
                string durationSuffix;
               
                if (!TryGetBanExpiry(args.Length < 3 ? string.Empty : args[2], 2, iplayer, out expiry, out durationSuffix))
                    return;
                
                AddBanClass ban = new AddBanClass();
                
                ban.SteamId = player.UserIDString;
                ban.Reason = notes;
                ban.BannedBy = iplayer.IsServer ? null : iplayer.Id;
                
                if (expiry > 0L)
                {
                    
                    ban.ExpireTime = DateTimeOffset.FromUnixTimeSeconds(expiry).DateTime;
                }
                else
                    ban.ExpireTime = null;
                
                SubmitNewBan(ban, iplayer, (sumbitedBan) =>
                {
                   
                    if (iplayer != null && iplayer.IsConnected)
                    {
                        string messageReplaced = lang.GetMessage("BanSentSuccess", this, iplayer.Id);
                        messageReplaced = messageReplaced.Replace("@niceBanId", sumbitedBan.NiceBanId);
                        iplayer.Reply(messageReplaced);
                    }
                    if (player.IsConnected && player.net.connection.ownerid != 0UL && (long)player.net.connection.ownerid != (long)player.net.connection.userid)
                    {
                        string banReason = string.Empty;
                        if (iplayer != null && iplayer.IsConnected)
                        {
                            string messageReplaced = lang.GetMessage("FamilyShareAccount", this, iplayer.Id);
                            iplayer.Reply(messageReplaced);
                            banReason = lang.GetMessage("FamilyShareReason", this, iplayer.Id);
                        }
                        else
                        {
                            banReason = lang.GetMessage("FamilyShareReason", this);
                        }
                        banReason = banReason.Replace("@player", player.net.connection.userid.ToString());
                        banReason = banReason.Replace("@niceBanId", sumbitedBan.NiceBanId);

                        AddBanClass shareBan = new AddBanClass();
                        shareBan.SteamId = player.net.connection.ownerid.ToString();
                        shareBan.Reason = banReason;
                        shareBan.BannedBy = iplayer.Id;
                        if (expiry > 0L)
                        {
                            shareBan.ExpireTime = DateTimeOffset.FromUnixTimeSeconds(expiry).DateTime;
                        }
                        else
                        {
                            shareBan.ExpireTime = null;
                        }
                        SubmitNewBan(ban, iplayer, (sumbitedBanFamilyShare) =>
                        {
                            if (iplayer != null && iplayer.IsConnected)
                            {
                                string messageReplaced = lang.GetMessage("BanSentSuccess", this, iplayer.Id);
                                messageReplaced = messageReplaced.Replace("@niceBanId", sumbitedBan.NiceBanId);
                                iplayer.Reply(messageReplaced);
                            }
                        });
                    }
                });
            }
        }

        [Command("AddAllBans")]
        private void AddAllBans(IPlayer iplayer, string command, string[] args)
        {
            if (iplayer != null && iplayer.IsAdmin)
            {
                List<ServerUsers.User> list = ServerUsers.GetAll(ServerUsers.UserGroup.Banned).ToList<ServerUsers.User>();
                float time = 0.0f;
                int i = 1;
                foreach (ServerUsers.User user in list)
                {
                    timer.Once(time, () =>
                    {
                        if (iplayer != null && iplayer.IsConnected)
                        {
                            string messageReplaced = lang.GetMessage("MassBanMessage", this, iplayer.Id);
                            messageReplaced = messageReplaced.Replace("@user", user.steamid.ToString());
                            messageReplaced = messageReplaced.Replace("@listCount", list.Count.ToString());
                            messageReplaced = messageReplaced.Replace("@index", i.ToString());
                            iplayer.Reply(messageReplaced);
                        }

                        i++;
                        AddBanClass ban = new AddBanClass();
                        ban.SteamId = user.steamid.ToString();
                        ban.Reason = user.notes;
                        ban.BannedBy = null;
                        ban.DontSendRconKick = true;
                        if (user.expiry > 0L)
                        {
                            long minsToAdd = (user.expiry - (long)Facepunch.Math.Epoch.Current);// / 60.0f);
                            minsToAdd = minsToAdd / 60;
                            ban.ExpireTime = DateTime.UtcNow.AddMinutes(minsToAdd);
                        }
                        else
                            ban.ExpireTime = null;

                        SubmitNewBan(ban, iplayer, (newban) => { });
                    });
                    time = time + 0.5f;
                }
            }
        }

        #endregion Commands

        #region Classes and Enums

        private enum ReportType
        {
            Abusive = 1,
            Cheat = 2,
            Spam = 3,
            Name = 4
        }

        public class LinkModel
        {
            public int LinkId { get; set; }
            public long SteamId { get; set; }
            public long DiscordId { get; set; }
            public int OrgId { get; set; }
            public DateTime LinkDate { get; set; }
            public bool InDiscord { get; set; }
            public bool ClaimedRewards { get; set; }
            public int? NitroBoostId { get; set; }
            public bool NitroBoosted { get; set; }
        }

        private class CachedPlayer
        {
            public CachedPlayer()
            {
                TimeOfAdd = DateTime.UtcNow;
            }

            public DateTime TimeOfAdd { get; }
        }

        private class AddBanClass
        {
            public string SteamId { get; set; }
            public string Reason { get; set; }
            public DateTime? ExpireTime { get; set; }
            public int OrgId { get; set; }
            public int? ServerId { get; set; }
            public string BannedBy { get; set; }
            public string NiceBanId { get; set; }
            public int ServerPort { get; set; }
            public bool DontSendRconKick { get; set; }
        }

        private class ApprovedCachedPlayer
        {
            public string reason { get; set; }
            public DateTime timeOfAdd { get; set; }
        }

        #endregion classes

        #region Config and Lang

        protected override void LoadDefaultConfig()
        {
            Config["APIKEY"] = "";
            Config["OxideGroupNameForLinked"] = "DiscordLinked";
            Config["OxideGroupNameForNitro"] = "NitroBoosted";
            Config["DisplayMessageOnClaimRewards"] = true;
            Config["DebugLoggingEnabled"] = false;
            Config["DisableBanCtrl"] = false;
            Config["Port"] = 28015;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BroadcastMessage"] = "@userName has just claimed some really cool  reward for linkking his account head to discordlinker.com to claim yours",
                ["JoinMessageLinked"] = "Your account is linked!",
                ["JoinMessageNotLinked"] = "Your account is NOT linked!",
                ["JoinMessageLeft"] = "Your left our discord :( You will no longer get rewards",
                ["RecentlyUsedThisCommand"] = "You recently used this command",
                ["CheckingAccount"] = "Checking your account...",
                ["AccountLinkSuccess"] = "Account Link successfull!",
                ["AlreadyLinked"] = "Your account has not been linked! Link your account at discordlinker.com/",
                ["UnkownError"] = "Unkown error",
                ["NitroLostMessage"] = "You are no longer nitro boosting this amazing server!",
                ["NitroGainMessage"] = "You are now nitro boosting this amazing server you will now get this awsome thing!",
                ["YouAreBannedMessage"] = "You are banned! Reason: @reason Head to discord.com/yourserver to appeal this ban",
                ["FailedToBan"] = "Failed to ban user! @response",
                ["FailedToBanNoPermission"] = "Failed to ban user! You do not have permission on gameservertools.com to ban users! Please contact your server owner to get this resolved",
                ["PastExireDate"] = "Expiry time is in the past",
                ["NoPlayerFound"] = "Player not found",
                ["NoReason"] = "No Reason Given",
                ["BanSentSuccess"] = "Ban Succesfully sent to Game Server Tools. BanId: @niceBanId",
                ["FamilyShareAccount"] = "Found family share account. Sending ban!",
                ["FamilyShareReason"] = "Family share owner of @player, Share Ban Id: @niceBanId",
                ["MassBanMessage"] = "@index/@listCount Sending ban for @user",
                ["PlayerBannedBroadcastMsg"] = "Player @user has been banned.",
                ["InvalidArguments"] = "Please provide a user to ban"
            }, this);
        }

        #endregion config
    }
}