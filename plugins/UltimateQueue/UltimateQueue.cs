using ConVar;
using Network;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Ultimate Queue", "Bobakanoosh", "1.0.4")]
    [Description("Adds a plethora of additional features to server queue for players with permission.")]
    class UltimateQueue : RustPlugin
    {
        private PluginConfig config;

        private const string FIRST_CONNECT = "ultimatequeue.firstconnect";
        private Dictionary<ulong, int> userDisconnectionTimes = new Dictionary<ulong, int>(); 

        private void Init()
        {
            LoadConfig();

            timer.Every(60f, () => CheckDisconnections());
        }

        private void Loaded()
        {
            RegisterPermissions();
        }

        #region Hooks

        private object CanBypassQueue(Connection connection)
        {
            if (connection.authLevel >= config.ignoreWithAuthlevel)
            {
                return true;
            }

            string userIdString = connection.userid.ToString();

            if (config.enableFirstConnectSkip && !permission.UserHasPermission(userIdString, FIRST_CONNECT))
            {
                return true;

            }
            else if (config.enableHoldQueue && userDisconnectionTimes.ContainsKey(connection.userid))
            {
                 return true;

            }
            else if (config.enableQueueSkip && UserHasAnyPermission(userIdString, config.permsGrantingSkipQueue))
            {
                return true;

            }

            return null;
        }

        private object CanClientLogin(Connection connection)
        {
            if (connection.authLevel >= config.ignoreWithAuthlevel)
            {
                return true;
            }

            string userId = connection.userid.ToString();

            if (config.enableQueueCapacity)
            {
                if (UserHasAnyPermission(userId, config.permsAffectedByQueueCapacity))
                {
                    if (Admin.ServerInfo().Queued >= config.queueCapacity) 
                    {
                        return lang.GetMessage("CapacityReached", this, userId);
                    }
                }

                return true;
            }

            return true;
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (config.enableHoldQueue)
            {
                if (UserHasAnyPermission(player.UserIDString, config.permsGrantingHoldQueue))
                {
                    userDisconnectionTimes[player.userID] = Epoch.Current();
                }
            }
        }
        private void OnPlayerConnected(BasePlayer player)
        {
            if (config.enableFirstConnectSkip)
            {
                if (!permission.UserHasPermission(player.UserIDString, FIRST_CONNECT))
                {
                    Puts($"Granting {player.displayName} {FIRST_CONNECT}");
                    permission.GrantUserPermission(player.UserIDString, FIRST_CONNECT, this);

                    if (config.enableGlobalFirstJoinWelcomeMessage)
                    {
                        MessageAllExclude(player, string.Format(lang.GetMessage("FirstJoinMessageGlobal", this), player.displayName));
                    }

                    if (config.enablePersonalFirstJoinWelcomeMessage)
                    {
                        PrintToChat(player, string.Format(lang.GetMessage("FirstJoinMessage", this)));
                    }

                }
            }
        }

        #endregion

        #region Helpers

        public void RegisterPermissions()
        {
            permission.RegisterPermission(FIRST_CONNECT, this);

            config.permsAffectedByQueueCapacity.ForEach(perm => TryRegister(perm));
            config.permsGrantingHoldQueue.ForEach(perm => TryRegister(perm));
            config.permsGrantingSkipQueue.ForEach(perm => TryRegister(perm));
        }

        private void TryRegister(string perm)
        {
            if (perm.StartsWith(Name.ToLower()) && !permission.PermissionExists(perm, this))
            {
                permission.RegisterPermission(perm, this);
            }
        }

        private bool UserHasAnyPermission(string userid, List<string> perms)
        {
            foreach(string perm in perms)
            {
                if (permission.UserHasPermission(userid, perm))
                {
                    return true;
                }
            }

            return false;
        }

        private void CheckDisconnections()
        {
            int current = Epoch.Current();
            int holdTimeSeconds = config.holdTime * 60;

            foreach(KeyValuePair<ulong, int> pair in userDisconnectionTimes.ToArray())
            {
                if (Epoch.SecondsElapsed(current, pair.Value) >= holdTimeSeconds)
                {
                    userDisconnectionTimes.Remove(pair.Key);
                }
            }
        }

        private void MessageAllExclude(BasePlayer exclude, string message, params object[] args)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.userID != exclude.userID)
                {
                    PrintToChat(player, message, args);
                }
            }
        }

        #endregion

        #region Config

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig
            {
                ignoreWithAuthlevel = 1,
                enableFirstConnectSkip = true,
                enableQueueCapacity = false,
                permsAffectedByQueueCapacity = new List<string> { "ultimatequeue.default" },
                queueCapacity = 50,
                enableHoldQueue = true,
                permsGrantingHoldQueue = new List<string> { "ultimatequeue.default", "ultimatequeue.vip" },
                holdTime = 5,
                enableQueueSkip = true,
                permsGrantingSkipQueue = new List<string> { "ultimatequeue.vip" },
                enablePersonalFirstJoinWelcomeMessage = true,
                enableGlobalFirstJoinWelcomeMessage = true,
            };

            SaveConfig();
        }

        private class PluginConfig
        {
            [JsonProperty("Auth Level required to skip queue no matter what (3 = none, 2 = admin, 1 = moderator)")]
            public int ignoreWithAuthlevel;

            [JsonProperty("Enable first connect skip queue?")]
            public bool enableFirstConnectSkip;

            [JsonProperty("Enable Queue Capacity?")]
            public bool enableQueueCapacity;

            [JsonProperty("If a player has one of the listed permissions, they are affected by queue capacity.")]
            public List<string> permsAffectedByQueueCapacity;

            [JsonProperty("Queue capacity")]
            public int queueCapacity;

            [JsonProperty("Enable Queue Holding?")]
            public bool enableHoldQueue;

            [JsonProperty("If a player has one of the listed permissions, their queue spot is held on disconnect")]
            public List<string> permsGrantingHoldQueue;

            [JsonProperty("Number of minutes to allow a user to reconnect without a queue")]
            public int holdTime;

            [JsonProperty("Enable Queue Skipping?")]
            public bool enableQueueSkip;

            [JsonProperty("If a player has one of the listed permissions, they will skip the queue")]
            public List<string> permsGrantingSkipQueue;

            [JsonProperty("When a user joins for the first time, message the new user. Only works with first connect queue skip enabled")]
            public bool enablePersonalFirstJoinWelcomeMessage;

            [JsonProperty("When a user joins for the first time, announce it to all users. Only works with first connect queue skip enabled")]
            public bool enableGlobalFirstJoinWelcomeMessage;

        }

        #endregion Config

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CapacityReached"] = "The server's queue is full.",
                ["FirstJoinMessage"] = "Welcome to the server!",
                ["FirstJoinMessageGlobal"] = "Welcome {0} to the server!"
            }, this);
        }

        #endregion

        #region Classes

        public static class Epoch
        {
            public static int Current()
            {
                DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                int currentEpochTime = (int)(DateTime.UtcNow - epochStart).TotalSeconds;

                return currentEpochTime;
            }

            public static int SecondsElapsed(int t1)
            {
                int difference = Current() - t1;

                return Mathf.Abs(difference);
            }

            public static int SecondsElapsed(int t1, int t2)
            {
                int difference = t1 - t2;

                return Mathf.Abs(difference);
            }

        }

        #endregion

    }

}
