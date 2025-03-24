#region

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

#endregion

namespace Oxide.Plugins
{
    [Info("Discord Linker", "mrcameron999", "2.1.1")]
    [Description("Provides a way of linking a players' Discord and Steam accounts")]
    public class DiscordLinker : CovalencePlugin
    {
        private readonly Dictionary<IPlayer, CachedPlayer> _cachedJoins = new Dictionary<IPlayer, CachedPlayer>();
        private readonly Dictionary<string, string> _headers = new Dictionary<string, string>();
        private readonly string connection = "https://api.gameservertools.com/";
        private readonly float timeout = 1000f;
        private string _groupName = "DiscordLinked";
        private string _groupNitro = "Nitro";

        //OnDiscordUserAddedToGroup user has been linked
        //OnDiscordUserUnLinked user unlinked
        private bool _showClaimMessage;

        private void Init()
        {
            LoadConfigData();
            permission.CreateGroup(_groupName, "linked", 0);
            permission.CreateGroup(_groupNitro, "nitro", 0);
            permission.RegisterPermission("discordLinker.linked", this);
            permission.GrantGroupPermission(_groupName, "discordLinker.linked", this);
        }

        private void LoadConfigData()
        {
            _groupName = Config["OxideGroupName"].ToString();

            if (!(bool) Config["CheckLinkOnConnect"])
            {
                Unsubscribe("OnPlayerConnected");
            }

            string apiKey = Config["APIKEY"].ToString();

            if (apiKey == "")
                LogError("NO API KEY PROVIDED! Please ensure you have added your api key in the config file");
            else
                _headers.Add("ApiKey", apiKey);


            if (Config["OxideGroupNameForNitro"] == null)
            {
                Config["OxideGroupNameForNitro"] = "Nitro";
                SaveConfig();
            }

            _groupNitro = Config["OxideGroupNameForNitro"].ToString();
            _showClaimMessage = (bool) Config["DisplayMessageOnClaimRewards"];
            if (string.IsNullOrEmpty(lang.GetMessage("NitroLostMessage", this)))
            {
                Dictionary<string, string> messages = lang.GetMessages("en", this);

                messages.Add("NitroLostMessage", "You are no longer nitro boosting this amazing server!");
                messages.Add("NitroGainMessage", "You are now nitro boosting this amazing server you will now get this awsome thing!");
                lang.RegisterMessages(messages, this);
            }
        }

        #region Hooks

        private void OnUserConnected(IPlayer player)
        {
            webrequest.Enqueue($"{connection}api/Link/GetLinkData?steamId={player.Id}", null, (code, response) =>
            {
                if (code == 200)
                {
                    LinkModel linkData = JsonConvert.DeserializeObject<LinkModel>(response);
                    //check if they have left the discord here

                    if (linkData.LinkId == 0) // not linked
                    {
                        string joinMessageNotLinked = lang.GetMessage("JoinMessageNotLinked", this, player.Id);
                        player.Reply($"{joinMessageNotLinked}");

                        bool userHasGroup = player.BelongsToGroup(_groupName);
                        if (userHasGroup)
                        {
                            player.RemoveFromGroup(_groupName);
                            player.RemoveFromGroup(_groupNitro);
                            Interface.CallHook("OnDiscordUserUnLinked", player);
                        }
                    }
                    else if (linkData.LinkId != 0 && !linkData.InDiscord) // left discord
                    {
                        string joinMessageLeft = lang.GetMessage("JoinMessageLeft", this, player.Id);
                        player.Reply($"{joinMessageLeft}");
                        //removes the from the group encase they unlinked their account

                        bool userHasGroup = player.BelongsToGroup(_groupName);
                        if (userHasGroup)
                        {
                            player.RemoveFromGroup(_groupName);
                            player.RemoveFromGroup(_groupNitro);
                            Interface.CallHook("OnDiscordUserUnLinked", player);
                        }
                    }
                    else // Linked
                    {
                        string joinMessageLinked = lang.GetMessage("JoinMessageLinked", this, player.Id);
                        player.Reply($"{joinMessageLinked}");
                        bool userHasGroup = player.BelongsToGroup(_groupName);
                        if (!userHasGroup)
                        {
                            player.AddToGroup(_groupName);
                            Interface.CallHook("OnDiscordUserAddedToGroup", player);
                        }

                        // Check for nitro rewards
                        bool userHasNitro = player.BelongsToGroup(_groupNitro);
                        if (userHasNitro && !linkData.NitroBoosted)
                        {
                            string noLongerBoostingMessage = lang.GetMessage("NitroLostMessage", this, player.Id);

                            player.Reply(noLongerBoostingMessage);

                            player.RemoveFromGroup(_groupNitro);
                            Interface.CallHook("OnNitroBoostRemove", player);
                            // Do discord nitro stuff here
                        }
                        else if (!userHasNitro && linkData.NitroBoosted)
                        {
                            string nowBoostingMessage = lang.GetMessage("NitroGainMessage", this, player.Id);
                            player.Reply(nowBoostingMessage);

                            player.AddToGroup(_groupNitro);
                            Interface.CallHook("OnNitroBoost", player);
                        }
                    }

                    return;
                }

                player.Reply("Something went wrong with this command. Please contact the admin of the server");
                Debug.LogError($"Error checking link: {response} {code}");
            }, this, RequestMethod.GET, _headers, timeout);
        }

        #endregion Hooks

        #region helpers

        private void MessageAllPlayers(string message)
        {
            foreach (IPlayer player in players.Connected)
            {
                player.Message(message);
            }
        }

        #endregion helpers

        #region Commands

        [Command("checklink")] //Used by gst to automatically check a users account
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

            OnUserConnected(player.IPlayer);
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

            OnUserConnected(iplayer);
        }

        #endregion Commands

        #region classes

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

        #endregion classes

        #region config

        protected override void LoadDefaultConfig()
        {
            Config["APIKEY"] = "";
            Config["OxideGroupName"] = "Discord Linked";
            Config["OxideGroupNameForNitro"] = "Discord Linked";
            Config["DisplayMessageOnClaimRewards"] = true;
            Config["CheckLinkOnConnect"] = true;
            Config["LogOnLink"] = true;
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
                ["NitroGainMessage"] = "You are now nitro boosting this amazing server you will now get this awsome thing!"
            }, this);
        }

        #endregion config
    }
}