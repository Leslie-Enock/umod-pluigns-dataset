using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Auto Broadcast", "Wulf", "1.0.9")]
    [Description("Sends randomly configured chat messages every X amount of seconds")]

    class AutoBroadcast : CovalencePlugin
    {
        #region Initialization

        const string permBroadcast = "autobroadcast.broadcast";
        bool random;
        int interval;
        int nextKey;

        protected override void LoadDefaultConfig()
        {
            Config["Randomize Messages (true/false)"] = random = GetConfig("Randomize Messages (true/false)", false);
            Config["Broadcast Interval (Seconds)"] = interval = GetConfig("Broadcast Interval (Seconds)", 300);
            SaveConfig();
        }

        void OnServerInitialized()
        {
            LoadDefaultConfig();
            permission.RegisterPermission(permBroadcast, this);

            if (lang.GetLanguages(this).Length == 0 || lang.GetMessages(lang.GetServerLanguage(), this)?.Count == 0)
            {
                lang.RegisterMessages(new Dictionary<string, string>
                {
                    ["ExampleMessage"] = "This is an example. Change it, remove it, translate it, whatever!",
                    ["AnotherExample"] = "This is another example, notice the comma at the end of the line above..."
                }, this, lang.GetServerLanguage());
            }
            else
            {
                foreach (var language in lang.GetLanguages(this))
                {
                    var messages = new Dictionary<string, string>();
                    foreach (var message in lang.GetMessages(language, this)) messages.Add(message.Key, message.Value);
                    lang.RegisterMessages(messages, this, language);
                }
            }
            Broadcast();
        }

        #endregion

        #region Broadcasting

        void Broadcast()
        {
            if (lang.GetLanguages(this) == null || lang.GetMessages(lang.GetServerLanguage(), this).Count == 0) return;

            timer.Every(interval, () =>
            {
                if (players.Connected.Count() <= 0) return;

                Dictionary<string, string> messages = null;
                foreach (var player in players.Connected)
                {
                    messages = lang.GetMessages(lang.GetLanguage(player.Id), this) ?? lang.GetMessages(lang.GetServerLanguage(), this);

                    if (messages == null || messages.Count == 0)
                    {
                        LogWarning($"No messages found for {player.Name} in {lang.GetLanguage(player.Id)} or {lang.GetServerLanguage()}");
                        continue;
                    }

                    var message = random ? messages.ElementAt(new Random().Next(0, messages.Count - 1)) : messages.ElementAt(nextKey);
                    if (message.Key != null) player.Message(Lang(message.Key, player.Id));
                }
                nextKey = nextKey + 1 == messages.Count ? 0 : nextKey + 1; // TODO: Don't assume that all languages have same count
            });
        }

        #endregion

        #region Commands

        [Command("broadcast")]
        void CmdChatBroadcast(IPlayer player, string command, string[] args)
        {
            if (args.Length != 1 || !player.HasPermission(permBroadcast)) return;

            foreach (var target in players.Connected)
            {
                var message = lang.GetMessage(args[0], this, target.Id);
                if (!string.IsNullOrEmpty(message))
                {
                    target.Message(message);
                }
            }
        }

        #endregion

        #region Helpers

        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}