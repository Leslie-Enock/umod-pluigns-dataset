using ConVar;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("GST Report", "mrcameron999", "1.0.5")]
    [Description("Extends F7 reporting with Discord Linker and gameservertools.com integration")]
    public class GSTReport : CovalencePlugin
    {
        private readonly string connection = "https://api.gameservertools.com/";

        private bool loggingEnabled = false;
        private readonly float timeout = 1000f;
        private int port;
        private Dictionary<string, string> headers = new Dictionary<string, string>();

        private Dictionary<ulong, List<string>> messages = new Dictionary<ulong, List<string>>();

        private void Init()
        {
            LoadConfigData();
        }

        private void LoadConfigData()
        {
            loggingEnabled = bool.Parse(Config["DebugLoggingEnabled"].ToString());
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

        #region Hooks

        private void OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
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
            Dictionary<string, object> parameters = new Dictionary<string, object>();

            List<KeyValuePair<int, List<string>>> files = new List<KeyValuePair<int, List<string>>>();

            ReportType typeOfReport = GetTypeIdFromType(type);
            parameters.Add("ReporterId", reporter.userID.ToString());
            parameters.Add("ReportedPlayerId", targetId);
            parameters.Add("Subject", subject);

            parameters.Add("Reason", message);
            parameters.Add("ServerPort", port);
            parameters.Add("Type", GetTypeIdFromType(type));//find the type

            if (typeOfReport == ReportType.Abusive || typeOfReport == ReportType.Spam) // Get the chat log
            {
                ulong playerIdLong = ulong.Parse(targetId);
                if (messages.ContainsKey(playerIdLong))
                {
                    List<string> userMessages = messages[playerIdLong];
                    if(userMessages.Count > 100)
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

            string body = JsonConvert.SerializeObject(parameters);

            if(loggingEnabled)
                Puts($"Sending report...");

            webrequest.Enqueue($"{connection}api/Report/AddReport", body, (code, response) =>
            {
                if(loggingEnabled)
                    Puts($"Got result back!\nCode: {code}\n Response: {response}");

            }, this, Core.Libraries.RequestMethod.POST, headers, timeout);
        }

        #endregion Hooks

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

        #region config

        protected override void LoadDefaultConfig()
        {
            Config["DebugLoggingEnabled"] = false;
            Config["APIKEY"] = "";
            Config["Port"] = 28015;
        }

        #endregion config
        private enum ReportType
        {
            Abusive = 1,
            Cheat = 2,
            Spam = 3,
            Name = 4
        }
    }
}