using System;
using System.Linq;
using System.Collections.Generic;

namespace Oxide.Plugins 
{
    [Info("Group Messages", "Sasuke", "1.0.1")]
    [Description("Send randomly configured chat messages for each player based in your groups")]

    class GroupMessages : RustPlugin 
    {
        #region constants

        private string tag;
        private string color;
        private float interval;

        private Dictionary<string, object> messages = DefaultMessages();
        private static Dictionary<string, object> DefaultMessages()
        {
            return new Dictionary<string, object>()
            {
                ["default"] = new List<string>()
                {
                    "Did you know that new plug-ins are always launched in <color=#008cba>uMod</color> to make your server even better!?",
                    "Go to <color=#008cba>umod.org</color> to download new plugins to the server!"
                },
                ["admin"] = new List<string>()
                {
                    "Remember that you are an <color=#af5>administrator</color> and make this server a cool and fun environment!"
                }
            };
        }
        
        #endregion

        #region hooks

        protected override void LoadDefaultConfig()
        {
            Config["Chat tag"] = tag = GetConfig("Chat tag", "uMod");
            Config["Chat color"] = color = GetConfig("Chat color", "#008cba");
            Config["Interval"] = interval = GetConfig("Interval", 60f);
            Config["Messages"] = messages = GetConfig("Messages", messages);

            SaveConfig();
        }

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            StartAutoMessages();
        }

        #endregion

        #region functions

        private void StartAutoMessages()
        {
            timer.Every(interval, () => {
                var activeList = BasePlayer.activePlayerList;
                foreach (var player in activeList)
                {
                    var playerMessages = PlayerMessages(player);
                    if (playerMessages.Count < 1)
                        continue;

                    var randomMessage = playerMessages.GetRandom();
                    if (string.IsNullOrEmpty(randomMessage)) 
                        continue;

                    player.ChatMessage($"<color={color}>{tag}</color>: {randomMessage}");
                }
            });
        }

        #endregion

        #region tools

        private List<string> PlayerMessages(BasePlayer player)
        {
            var msgs = new List<string>();
            var parsedMessages = ParseDictionary(messages);

            foreach(var pair in parsedMessages)
            {
                if (!permission.GroupExists(pair.Key))
                {
                    permission.CreateGroup(pair.Key, string.Empty, 0);
                }

                if (permission.UserHasGroup(player.UserIDString, pair.Key))
                {
                    msgs.AddRange(pair.Value);
                }
            }

            return msgs;
        }

        private Dictionary<string, List<string>> ParseDictionary(Dictionary<string, object> dict)
        {
            var parsed = dict.ToDictionary(
                x => x.Key, 
                x => x.Value != null
                    ? (List<string>)(x.Value as List<object>)
                        .Select(val => val.ToString())
                        .ToList()
                    : new List<string>()
            );

            return parsed;
        }

        private T GetConfig<T>(string name, T value) 
            => Config[name] == null 
                ? value 
                : (T)Convert.ChangeType(Config[name], typeof(T));

        #endregion
    }
}