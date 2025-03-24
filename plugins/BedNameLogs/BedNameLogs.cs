using System;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using Oxide.Core.Libraries;


namespace Oxide.Plugins
{

    [Info("Bed Name Logs", "Zeeuss", "0.1.3")]
    [Description("A logger for beds and sleeping bags renames.")]

    public class BedNameLogs : RustPlugin
    {

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                //FORMAT: XYZ playername playerid renamed sleeping bag from previous to current
                ["SleepingBagRename"] = "{0} {1} ({2}) renamed sleeping bag from {3} to {4}",
                ["BedRename"] = "{0} {1} ({2}) renamed bed from {3} to {4}"

            }, this);
        }
        #endregion

        #region Json
        private const string DiscordJson = @"{
            ""embeds"":[{
                    ""color"": ""${discord.embed.color}"",
                    ""fields"": [
                    {
                        ""name"": ""${player.field.name}"",
                        ""value"": ""${player}""
                    },
                    {
                        ""name"": ""${message.field.name}"",
                        ""value"": ""${message}""
                    }
                ]
            }]
        }";
        #endregion

        #region Hooks

        void CanRenameBed(BasePlayer player, SleepingBag bed, string bedName)
        {

            if (player == null)
            {
                return;
            }


            if (bed == null)
            {
                return;
            }


            string xyzpos = $"X: {Math.Round((decimal)bed.ServerPosition.x)} Y: {Math.Round((decimal)bed.ServerPosition.y)} Z: {Math.Round((decimal)bed.ServerPosition.z)}";
            string playerName = $"{player?.displayName}";
            string playerID = $"{player?.UserIDString}";
            string bedPrevious = $"{bed?.niceName}";
            string bedCurrent = $"{bedName}";

            if (configData == null)
            {
                LoadConfigVariables();
            }

            if (bed?.ShortPrefabName == "sleepingbag_leather_deployed")
            {
                LogToFile("Renames", String.Format(lang.GetMessage("SleepingBagRename", this, null), xyzpos, playerName, playerID, bedPrevious, bedCurrent), this, true);

                if (configData.dcLogs != true)
                    return;

                logToDc(player, xyzpos, playerName, playerID, bedPrevious, bedCurrent, "SleepingBagRename");
            }
            else if (bed?.ShortPrefabName == "bed_deployed")
            {
                LogToFile("Renames", String.Format(lang.GetMessage("BedRename", this, null), xyzpos, playerName, playerID, bedPrevious, bedCurrent), this, true);

                if (configData.dcLogs != true)
                    return;

                logToDc(player, xyzpos, playerName, playerID, bedPrevious, bedCurrent, "BedRename");
                

            }

        }
        #endregion

        #region Config
        private ConfigData configData;
        class ConfigData
        {

            [JsonProperty(PropertyName = "log to discord?")]
            public bool dcLogs = true;
            [JsonProperty(PropertyName = "Discord webhook url")]
            public string discordWebHookURL = "Your discord webhook URL";

        }

        private bool LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConfig(configData);
            return true;
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData();
            SaveConfig(configData);
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region Helper
        private void logToDc(BasePlayer player, string xyzpos, string playerName, string playerID, string bedPrevious, string bedCurrent, string langM)
        {


            string content = DiscordJson
                            .Replace("${discord.embed.color}", "16538684")
                            .Replace("${player.field.name}", "Player")
                            .Replace("${player}", $"{player?.displayName} ({player?.UserIDString})")
                            .Replace("${message.field.name}", "Rename Log")
                            .Replace("${message}", String.Format(lang.GetMessage(langM, this, null), xyzpos, playerName, playerID, bedPrevious, bedCurrent));

            if (configData == null)
            {
                LoadConfigVariables();
            }

            if (!configData.discordWebHookURL.Contains("/api/webhooks") || string.IsNullOrEmpty(configData.discordWebHookURL.ToString()) || content == null)
            {
                return;
            }

            webrequest.Enqueue(configData.discordWebHookURL, content, (code, response) =>
            {

                if (code != 204)
                {
                    Puts($"Discord.com responded with code {code}");
                }
            }, this, RequestMethod.POST, new Dictionary<string, string> { ["Content-Type"] = "application/json" });



        }
        #endregion

    }
}