// Requires: DiscordMessages
using Oxide.Core.Plugins;
using System;
using System.Text.RegularExpressions;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Discord Server Messages", "takocchi", "1.2.41")]
    [Description("Logs SERVER messages and cheat logs to Discord Chat")]
    class DiscordServerMessages : RustPlugin
    {
        [PluginReference]
        private Plugin DiscordMessages;

        string webhookURL;
        string cheatLogURL;

        bool logCheats;
        bool fancyMessage;

        //Static readonly, compiled due to this RegEx being used a lot, so this will be less costy for the Server.
        //Even if plugin is unloaded, this Regex won't be called anymore, so it won't be a huge loss to the Server.
        static readonly Regex isGivingItem = new Regex(@"(.*?)\s+gave\s+(.*?)\s+(\d+)\s+x\s(.*?)\s?", RegexOptions.Compiled);
        static readonly Regex isFlyhack = new Regex(@"Kicking\s(.*?)\s\(FlyHack\sViolation\sLevel\s(\d+)\.*(\d+)*\)", RegexOptions.Compiled);
        static readonly Regex eggCollected = new Regex(@"(.*?)\sis\sthe\stop\sbunny\swith\s(\d+)\seggs\scollected\.", RegexOptions.Compiled);

        protected override void LoadDefaultConfig()
        {
            Config["webhookURL"] = webhookURL = GetConfig("webhookURL", "Your URL here.");
            Config["cheatLog"] = logCheats = GetConfig("cheatLog", false);
            Config["cheatLogURL"] = cheatLogURL = GetConfig("cheatLogURL", "Your URL here.");
            Config["fancyMessage"] = fancyMessage = GetConfig("fancyMessage", true);
            SaveConfig();
        }

        void Init()
        {
            LoadDefaultConfig();
        }

        void OnServerMessage(string message, string name, string color, ulong id)
        {
            
            string discordMessage;
            // Server broadcasted giving message, return.
            if (CheckItemCheat(message))
            {
                discordMessage = ModifyMessage(message, fancyMessage);
                DiscordMessages?.Call("API_SendTextMessage", cheatLogURL, discordMessage);
                return;
            }
            // Return if it is FlyHack
            if (CheckFlyHack(message))
                return;
            // Return if it is Eastern Event.
            if (CheckEasterEvent(message))
                return;

            // Server wrote a real message here.
            discordMessage = ModifyMessage(message, fancyMessage);
            DiscordMessages?.Call("API_SendTextMessage", webhookURL, discordMessage);
        }

        private bool CheckItemCheat(string message)
        {
            var isItemMatch = isGivingItem.IsMatch(message);
            if (isItemMatch && logCheats)
                return true;
            return false;
        }
        private bool CheckFlyHack(string message)
        {
            var isFlyhackMatch = isFlyhack.IsMatch(message);
            if (isFlyhackMatch)
                return true;
            return false;

        }
        private bool CheckEasterEvent(string message)
        {
            if (message == "Wow, no one played so no one won.")
                return true;

            var isEggMatch = eggCollected.IsMatch(message);
            if (isEggMatch)
                return true;
            return false;
        }
        private string ModifyMessage(string message, bool fancy = true)
        {
            var time = DateTime.Now.ToShortTimeString();
            var msg = "";
            if (fancy)
                msg = $"```CSS\n[{time}] #SERVER: {message}```";
            else
                msg = $"[{time}] **__SERVER:__** {message}";
            return msg;
        }
        T GetConfig<T>(string name, T value) => Config[name] == null ? value : (T)Convert.ChangeType(Config[name], typeof(T));
    }
}