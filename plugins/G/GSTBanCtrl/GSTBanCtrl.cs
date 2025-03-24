using ConVar;
using Facepunch.Extend;
using Facepunch.Math;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("GST BanCtrl", "Cameron", "1.0.4")]
    [Description("Centralised ban list for multiple servers.")]
    public class GSTBanCtrl : CovalencePlugin
    {
        private Dictionary<string, CachedPlayer> CachedJoins = new Dictionary<string, CachedPlayer>();
        private readonly string apiUrl = "https://api.gameservertools.com";

        //private readonly string apiUrl = "https://localhost:44361";
        private Dictionary<string, string> headers = new Dictionary<string, string>();

        private int port = 28015;

        private void Init()
        {
            LoadConfigData();
        }

        private void LoadConfigData()
        {
            port = int.Parse(Config["Port"].ToString());
            string apiKey = Config["APIKEY"].ToString();
            if (string.IsNullOrEmpty(apiKey))
                LogError("NO API KEY PROVIDED! Please ensure you have added your api key in the config file");
            else
            {
                headers.Add("Content-Type", "application/json");
                headers.Add("ApiKey", apiKey);
            }
        }

        #region WebRequests

        private void SubmitNewBan(AddBanClass newBan, IPlayer admin, Action<AddBanClass> successCallBack)
        {
            newBan.ServerPort = port;
            string body = JsonConvert.SerializeObject(newBan);

            webrequest.Enqueue($"{apiUrl}/api/Ban/AddBan", body, (code, response) =>
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
            }, this, Core.Libraries.RequestMethod.PUT, headers);
        }

        #endregion WebRequests

        #region Altered Facepunch Methods

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

        #endregion Altered Facepunch Methods

        #region Commands

        [Command("Ban")]
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
                ServerUsers.User user = ServerUsers.Get(player.userID);

                string noReasonString = lang.GetMessage("NoReason", this, iplayer.Id);

                string notes = args.Length < 2 ? noReasonString : args[1];

                long expiry;
                string durationSuffix;

                if (!TryGetBanExpiry(args.Length < 3 ? string.Empty : args[2], 2, iplayer, out expiry, out durationSuffix))
                    return;

                AddBanClass ban = new AddBanClass();
                ban.SteamId = user.steamid.ToString();
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
                        shareBan.SteamId = user.steamid.ToString();
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

        #region Hooks

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
            if (CachedJoins.ContainsKey(id))
            {
                CachedPlayer data = CachedJoins[id];
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

                CachedJoins.Remove(id);
            }

            webrequest.Enqueue($"{apiUrl}/api/Ban/GetActiveBans?steamId={ownerId}&serverPort={port}", null, (code, response) =>
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

                        CachedJoins.Add(connection.userid.ToString(), new CachedPlayer() { reason = kickReason, timeOfAdd = DateTime.Now });
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
                    Puts($"Something went wrong code {code}");
                }
            }, this, Core.Libraries.RequestMethod.GET, headers);
            return;
        }

        #endregion Hooks

        #region Classes

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

        private class CachedPlayer
        {
            public string reason { get; set; }
            public DateTime timeOfAdd { get; set; }
        }

        #endregion Classes

        protected override void LoadDefaultConfig()
        {
            Config["APIKEY"] = "";
            Config["Port"] = 28015;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
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
            }, this); ;
        }
    }
}