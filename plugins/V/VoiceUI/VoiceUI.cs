using Network;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Voice UI", "Pinkstink", "1.0.4")]
    [Description("Displays UI of players names who are actively transmitting voice")]
    public class VoiceUI : RustPlugin
    {
        private const string USE_PERMISSION = "voiceui.use";
        private const string ADMIN_PERMISSION = "voiceui.admin";
        private const string ROOT_UI_NAME = "VoiceUI";

        private VoiceUIConfiguration config;
        private Dictionary<BasePlayer, List<VoiceRecord>> voiceRecords = new Dictionary<BasePlayer, List<VoiceRecord>>();
        private float timeSinceLastUpdate = 0f;

        void Init()
        {
            permission.RegisterPermission(USE_PERMISSION, this);
            permission.RegisterPermission(ADMIN_PERMISSION, this);
            config = Config.ReadObject<VoiceUIConfiguration>();
        }

        void Unload()
        {
            voiceRecords = null;
        }

        #region Plugin Core
        void OnFrame()
        {
            timeSinceLastUpdate += Time.deltaTime;
            if (timeSinceLastUpdate < config.UpdateInterval) return;

            ProcessVoice();
            timeSinceLastUpdate = 0;
        }

        void ProcessVoice()
        {
            var newRecords = new Dictionary<BasePlayer, List<VoiceRecord>>();

            foreach (var record in voiceRecords)
            {
                if (record.Key == null || !record.Key.IsConnected) continue;

                if (record.Value.Count <= 0)
                {
                    CuiHelper.DestroyUi(record.Key, ROOT_UI_NAME);
                    continue;
                }

                newRecords.Add(record.Key, new List<VoiceRecord>());

                List<string> elements = new List<string>
                {
                    @"
                    {
                        ""name"": ""VoiceUI"",
                        ""parent"": ""Hud"",
                        ""components"": [
                            {
                                ""type"": ""UnityEngine.UI.Image"",
                                ""color"": ""0 0 0 0""
                            },
                            {
                                ""type"": ""RectTransform"",
                                ""anchormin"": ""0.65 0.020"",
                                ""anchormax"": ""0.83 0.135""
                            }
                        ]
                    }
                    "
                };

                elements.AddRange(GenerateVoiceRecordGrid(record.Value.OrderBy(x => x.distance), "VoiceUI_Records", ROOT_UI_NAME));

                CuiHelper.DestroyUi(record.Key, ROOT_UI_NAME);
                CuiHelper.AddUi(record.Key, "[" + string.Join(",", elements).Replace("\n", string.Empty) + "]");
            }

            voiceRecords = newRecords;
        }

        void OnPlayerVoice(BasePlayer voiceSender)
        {
            List<Connection> recipients = BaseNetworkable.GetConnectionsWithin(voiceSender.transform.position, 100f);

            foreach (var connection in recipients)
            {
                var recipientPlayer = connection.player as BasePlayer;
                if (recipientPlayer == null || recipientPlayer == voiceSender) continue;
                if (config.ToggledByDefault && config.ToggledPlayers.Contains(recipientPlayer.userID)) continue;
                if (!config.ToggledByDefault && !config.ToggledPlayers.Contains(recipientPlayer.userID)) continue;

                if (!voiceRecords.ContainsKey(recipientPlayer))
                    voiceRecords.Add(recipientPlayer, new List<VoiceRecord>());

                var idx = voiceRecords[recipientPlayer].FindIndex(x => x.player == voiceSender);

                if (idx >= 0)
                    voiceRecords[recipientPlayer].RemoveAt(idx);

                voiceRecords[recipientPlayer].Add(new VoiceRecord(voiceSender, Vector3.Distance(voiceSender.transform.position, recipientPlayer.transform.position)));
            }
        }

        struct VoiceRecord
        {
            public BasePlayer player;
            public DateTime time;
            public float distance;

            public VoiceRecord(BasePlayer basePlayer, float distance)
            {
                player = basePlayer;
                this.distance = distance;
                time = DateTime.Now;
            }
        }
        #endregion

        #region Commands
        [ChatCommand("showvoice")]
        void ChatCMD_ShowVoice(BasePlayer player, string command, string[] args)
        {
            bool hasPermission = player.IsAdmin || permission.UserHasPermission(player.UserIDString, USE_PERMISSION);
            if (!hasPermission) return;

            if (config.ToggledByDefault)
            {
                if (config.ToggledPlayers.Remove(player.userID)) SaveConfig();
            }
            else
            {
                if (config.ToggledPlayers.Add(player.userID)) SaveConfig();
            }
            SendFormattedMessage(player, "Voice UI", lang.GetMessage("UIVisible", this, player.UserIDString));
        }

        [ChatCommand("hidevoice")]
        void ChatCMD_HideVoice(BasePlayer player, string command, string[] args)
        {
            bool hasPermission = player.IsAdmin || permission.UserHasPermission(player.UserIDString, USE_PERMISSION);
            if (!hasPermission) return;

            if (config.ToggledByDefault)
            {
                if (config.ToggledPlayers.Add(player.userID)) SaveConfig();
            }
            else
            {
                if (config.ToggledPlayers.Remove(player.userID)) SaveConfig();
            }
            SendFormattedMessage(player, "Voice UI", lang.GetMessage("UIHidden", this, player.UserIDString));
        }

        [ChatCommand("voice")]
        void ChatCMD_Voice(BasePlayer player, string command, string[] args)
        {
            bool hasPermission = player.IsAdmin || permission.UserHasPermission(player.UserIDString, ADMIN_PERMISSION);
            if (!hasPermission) return;

            if (args.Length < 1)
            {
                string showArgs = lang.GetMessage("ShowConfig", this, player.UserIDString);
                SendFormattedMessage(player, "Voice UI", string.Format(showArgs, Version, config.UpdateInterval, config.UIColorSensitivity, config.ToggledByDefault));
                return;
            }

            switch (args[0].ToUpper())
            {
                case "INTERVAL":
                case "UPDATEINTERVAL":
                    if (args.Length < 2)
                    {
                        SendFormattedMessage(player, "Voice UI", string.Format(lang.GetMessage("CurrentUpdateInterval", this, player.UserIDString), config.UpdateInterval));
                        break;
                    }
                    float newInterval;
                    if (!float.TryParse(args[1], out newInterval))
                    {
                        SendFormattedMessage(player, "Voice UI", lang.GetMessage("UpdateIntervalFormatError", this, player.UserIDString));
                        break;
                    }
                    config.UpdateInterval = newInterval;
                    SaveConfig();
                    SendFormattedMessage(player, "Voice UI", string.Format(lang.GetMessage("UpdateIntervalSet", this, player.UserIDString), config.UpdateInterval));
                    break;

                case "UITOGGLEDBYDEFAULT":
                case "TOGGLEDBYDEFAULT":
                    if (args.Length < 2)
                    {
                        SendFormattedMessage(player, "Voice UI", string.Format(lang.GetMessage("CurrentToggledByDefault", this, player.UserIDString), config.ToggledByDefault));
                        break;
                    }
                    bool newToggledByDefault;
                    if (!bool.TryParse(args[1], out newToggledByDefault))
                    {
                        SendFormattedMessage(player, "Voice UI", lang.GetMessage("ToggledByDefaultFormatError", this, player.UserIDString));
                        break;
                    }
                    config.ToggledByDefault = newToggledByDefault;
                    SaveConfig();
                    SendFormattedMessage(player, "Voice UI", lang.GetMessage(config.ToggledByDefault ? "IsToggledByDefault" : "NotToggledByDefault", this, player.UserIDString));
                    break;

                case "TOGGLED":
                case "SHOWTOGGLED":
                    SendFormattedMessage(player, "Voice UI", string.Format(lang.GetMessage("ToggledUsers", this, player.UserIDString), config.ToggledPlayers.Count, string.Join("\n", config.ToggledPlayers)));
                    break;

                case "SENSITIVITY":
                case "UICOLORSENSITIVITY":
                    if (args.Length < 2)
                    {
                        SendFormattedMessage(player, "Voice UI", string.Format(lang.GetMessage("CurrentSensitivity", this, player.UserIDString), config.UIColorSensitivity));
                        break;
                    }
                    float newSensitivity;
                    if (!float.TryParse(args[1], out newSensitivity))
                    {
                        SendFormattedMessage(player, "Voice UI", lang.GetMessage("SensitivityFormatError", this, player.UserIDString));
                        break;
                    }
                    config.UIColorSensitivity = newSensitivity;
                    SaveConfig();
                    SendFormattedMessage(player, "Voice UI", string.Format(lang.GetMessage("SensitivitySet", this, player.UserIDString), config.UIColorSensitivity));
                    break;

                default:
                    SendFormattedMessage(player, "Voice UI", lang.GetMessage("ArgNotFound", this, player.UserIDString));
                    break;
            }
        }
        #endregion

        #region Helpers
        List<string> GenerateVoiceRecordGrid(IEnumerable<VoiceRecord> records, string identifier, string parentName)
        {
            List<string> elements = new List<string>();

            // Total Grid Count
            int cols = (int)Mathf.Clamp((float)Math.Sqrt(records.Count()), 1, 3);
            int rows = (int)Math.Max(Math.Ceiling((double)records.Count() / cols), 5);

            int nameTrimLength = 32 / cols;

            // Sizing
            float xSize = 1f / cols;
            float ySize = 1f / rows;

            // Spacing
            float xSpacing = xSize / 80;
            float ySpacing = ySize / 120;

            for (int i = 0; i < records.Count(); i++)
            {
                int row = i / cols;
                int col = i % cols;

                float xMin = xSize * col + xSpacing;
                float xMax = xSize * col + xSize - xSpacing;

                float yMin = 1f - (ySize * row) - ySize + ySpacing;
                float yMax = 1f - (ySize * row) - ySpacing;

                VoiceRecord record = records.ElementAt(i);
                string text = record.player.displayName.Trim().Substring(0, Math.Min(nameTrimLength, record.player.displayName.Length)).EscapeRichText();
                float green = Math.Max(0, Math.Min(record.distance / config.UIColorSensitivity, 1));

                int textSize = (int)(75 * (yMax - yMin));

                var label = @"
                    {
                        ""name"": """ + $"{identifier}_{record.player.UserIDString}" + @""",
                        ""parent"": """ + parentName + @""",
                        ""components"": [
                            {
                                ""type"": ""UnityEngine.UI.Text"",
                                ""text"": """ + text + @""",
                                ""align"": ""MiddleLeft"",
                                ""fontSize"": " + textSize.ToString() + @",
                                ""color"": ""1 " + green.ToString() + @" 0 1""
                            },
                            {
                                ""type"": ""RectTransform"",
                                ""anchormin"": """ + $"{xMin} {yMin}" + @""",
                                ""anchormax"": """ + $"{xMax} {yMax}" + @"""
                            },
                            {
                                ""type"": ""UnityEngine.UI.Outline"",
                                ""color"": ""0 0 0 1"",
                                ""distance"": ""1.0 -0.8""
                            }
                        ]
                    }
                ";
                elements.Add(label);
            }

            return elements;
        }

        void SendFormattedMessage(BasePlayer player, string from, string message, string color = "#ff0000")
        {
            player.ChatMessage(string.Format("<color={0}>{1}</color>: {2}", color, from, message));
        }
        #endregion

        #region Localisation
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UIHidden"] = "UI is now hidden",
                ["UIVisible"] = "UI is now visible",

                ["ShowConfig"] = "Version {0}\n/showvoice\n/hidevoice\n/voice updateinterval <number>\n/voice uicolorsensitivity <number>\n/voice toggledbydefault <true / false>\n/voice toggled\n\nCurrent Configuration:\nUI Update Interval: {1}\nUI Color Sensitivity: {2}\nUI Toggled By Default: {3}",
                ["ArgNotFound"] = "Argument not found",

                ["CurrentUpdateInterval"] = "Current Update Interval set to {0} seconds",
                ["UpdateIntervalFormatError"] = "Update Interval must be a decimal number in seconds",
                ["UpdateIntervalSet"] = "Update Interval set to {0} seconds",

                ["CurrentSensitivity"] = "Current UI Color Sensitivity set to {0}",
                ["SensitivityFormatError"] = "UI Color Sensitivity must be a decimal number between 1 and 100",
                ["SensitivitySet"] = "UI Color Sensitivity set to {0}",

                ["ToggledUsers"] = "Toggled count: {0}\n{1}",

                ["CurrentToggledByDefault"] = "Toggled By Default is set to {0}",
                ["IsToggledByDefault"] = "UI is now toggled by default",
                ["NotToggledByDefault"] = "UI is no longer toggled by default",
                ["ToggledByDefaultFormatError"] = "Toggled By Default must be either \"true\" or \"false\""
            }, this);
        }
        #endregion

        #region Configuration
        protected override void LoadDefaultConfig() => Config.WriteObject(new VoiceUIConfiguration(), true);

        void SaveConfig() => Config.WriteObject(config, true);

        class VoiceUIConfiguration
        {
            private float updateInterval = 0.5f;
            public float UpdateInterval
            {
                get { return updateInterval; }
                set { updateInterval = value; }
            }
            private float uiColorSensitivity = 50f;
            public float UIColorSensitivity
            {
                get { return uiColorSensitivity; }
                set { uiColorSensitivity = Mathf.Clamp(value % 100, 1, 100); }
            }
            private HashSet<ulong> toggledPlayers = new HashSet<ulong>();
            public HashSet<ulong> ToggledPlayers
            {
                get { return toggledPlayers; }
                private set { toggledPlayers = value; }
            }

            private bool toggledByDefault = false;
            public bool ToggledByDefault
            {
                get
                {
                    return toggledByDefault;
                }
                set
                {
                    if (toggledByDefault != value)
                        ToggledPlayers.Clear();

                    toggledByDefault = value;
                }
            }
        }

        void OnUserPermissionRevoked(string id, string permName)
        {
            ulong userID;
            if (!ulong.TryParse(id, out userID)) return;

            if (permName == USE_PERMISSION && config.ToggledPlayers.Remove(userID)) SaveConfig();
        }

        void OnRconCommand(IPEndPoint ip, string command, string[] args)
        {
            switch (command.ToUpper())
            {
                case "GLOBAL.REMOVEOWNER":
                case "REMOVEOWNER":
                    ulong userID;
                    if (args.Length < 0 || !ulong.TryParse(args[0], out userID)) return;
                    if (!permission.UserHasPermission(args[0], USE_PERMISSION) && config.ToggledPlayers.Remove(userID)) SaveConfig();
                    break;

                default:
                    return;
            }
        }

        void OnPlayerConnected(Network.Message packet)
        {
            ulong userID = packet.connection.userid;
            bool isAdmin = packet.connection.authLevel > 0;
            bool isToggled = config.ToggledPlayers.Contains(userID);
            if (!isToggled) return;

            if (isAdmin) return;
            if (permission.UserHasPermission(userID.ToString(), USE_PERMISSION)) return;

            if (config.ToggledPlayers.Remove(userID)) SaveConfig();
        }
        #endregion
    }
}
