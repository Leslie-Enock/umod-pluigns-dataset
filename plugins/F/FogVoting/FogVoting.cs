using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Fog Voting", "Ultra", "1.2.21")]
    [Description("Initializes voting to remove fog from the environment")]

    class FogVoting : RustPlugin
    {
        #region Fields

        bool isVotingOpen = false;
        Timer fogCheckTimer;
        Timer votingTimer;
        Timer votingPanelRefreshTimer;
        DateTime votingStart;
        List<string> usersVotingNo = new List<string>();

        // CUI panels
        static CuiPanel votingPanel;
        string votingPanelName = "votingPanelName";
        string fogYesPanelName = "fogYesPanelName";
        string fogNoPanelName = "fogNoPanelName";
        string votingScorePanelName = "votingScorePanelName";


        #endregion

        #region Oxide Hooks

        void OnServerInitialized()
        {
            InitCUI();
            CheckCurrentFog();
        }

        void Unload()
        {
            DestroyVotingPanel();

            DestroyTimer(fogCheckTimer);
            DestroyTimer(votingTimer);
            DestroyTimer(votingPanelRefreshTimer);
        }

        #endregion

        #region Chat Commands

        [ChatCommand("nofog")]
        void FogNo(BasePlayer player)
        {
            if (!isVotingOpen)
            {
                SendReply(player, "Fog voting is not open now");
            }

            if (!usersVotingNo.Contains(player.UserIDString))
            {
                usersVotingNo.Add(player.UserIDString);
                CheckVoting();
            }
        }

        [ChatCommand("setfog")]
        void SetFog(BasePlayer player, string command, string[] args)
        {
            if (player.IsAdmin)
            {
                float fogValue = 0;
                if (args.Length == 1 && float.TryParse(args[0], out fogValue))
                {
                    if (fogValue >= 0F && fogValue <= 1F)
                    {
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, $"weather.fog {fogValue}");
                    }
                }
            }
        }

        [ChatCommand("checkfog")]
        void CheckFog(BasePlayer player, string command, string[] args)
        {
            if (isVotingOpen) return;
            if (player.IsAdmin)
            {
                CheckCurrentFog();
            }
        }

        #endregion

        #region Core

        void CheckCurrentFog()
        {
            DestroyTimer(fogCheckTimer);

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (Climate.GetFog(player.transform.position) > configData.FogLimit)
                {
                    OpenVoting();
                    return;
                }
            }

            fogCheckTimer = timer.Once(configData.FogCheckInterval, () => CheckCurrentFog());
        }

        void OpenVoting()
        {
            isVotingOpen = true;
            usersVotingNo = new List<string>();
            votingStart = DateTime.UtcNow;
            ShowVotingPanel();
            votingTimer = timer.Once(configData.VotingDuration, () => CloseVoting(intervalMultiplier: 5));

            PrintToChat($"<color=#89b38a>FOG VOTING:</color>: vote <color=#89b38a>/nofog</color> to remove fog");
        }

        void CheckVoting()
        {
            float requiredVotes = BasePlayer.activePlayerList.Count * (configData.VotePercentage / 100);
            if (requiredVotes < 1) requiredVotes = 1;

            if (usersVotingNo.Count >= requiredVotes)
            {
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "weather.fog 0");
                PrintToChat($"<color=#89b38a>FOG VOTING:</color>: Fog has been <color=#89b38a>removed</color>");
                CloseVoting();
            }
        }

        void CloseVoting(float intervalMultiplier = 1)
        {
            isVotingOpen = false;
            DestroyTimer(votingTimer);
            DestroyVotingPanel();
            fogCheckTimer = timer.Once(configData.FogCheckInterval * intervalMultiplier, () => CheckCurrentFog());
        }

        void DestroyTimer(Timer timer)
        {
            timer?.DestroyToPool();
            timer = null;
        }

        void DestroyVotingPanel()
        {
            DestroyTimer(votingPanelRefreshTimer);

            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, votingPanelName);
            }
        }

        #endregion

        #region Config

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Fog value to start voting 0.0 - 1.0")]
            public float FogLimit;

            [JsonProperty(PropertyName = "Interval to check the current fog (seconds)")]
            public float FogCheckInterval;

            [JsonProperty(PropertyName = "Voting duration (seconds)")]
            public float VotingDuration;

            [JsonProperty(PropertyName = "Required vote percentage")]
            public float VotePercentage;
        }

        protected override void LoadConfig()
        {
            try
            {
                base.LoadConfig();
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }

            if (configData.FogLimit < 0 || configData.FogLimit > 1) configData.FogLimit = 0.1F;
            if (configData.FogCheckInterval < 1) configData.FogCheckInterval = 60;
            if (configData.VotingDuration < 10) configData.VotingDuration = 30;
            if (configData.VotePercentage <= 0 || configData.VotePercentage > 100) configData.VotePercentage = 40;

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData()
            {
                FogLimit = 0.3F,
                FogCheckInterval = 60,
                VotingDuration = 30
            };
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(configData, true);
            base.SaveConfig();
        }

        #endregion

        #region CUI

        void InitCUI()
        {
            votingPanel = new CuiPanel
            {
                CursorEnabled = false,
                RectTransform =
                    {
                        AnchorMin = $"0.4 0.74",
                        AnchorMax = $"0.6 0.81"
                    },
                Image =
                 { Color = "0 0 0 0.8" }
            };
        }

        void ShowVotingPanel() {
            /**
             * Moved the below equations out of the loop, to improve performance. 
             */
            float requiredVotes = BasePlayer.activePlayerList.Count * (configData.VotePercentage / 100);
            float progress = (float)(DateTime.UtcNow - votingStart).TotalSeconds / configData.VotingDuration;
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, votingPanelName);

                if (isVotingOpen)
                {
                    var container = new CuiElementContainer();
                    container.Add(votingPanel, name: votingPanelName);

                    if (requiredVotes < 1) requiredVotes = 1;

                    // progress bar
                    container.Add(GetPanel(anchorMin: "0 0", anchorMax: $"{progress} 0.1", color: "1 1 1 0.4"), votingPanelName, name: fogNoPanelName);

                    // voting score 
                    container.Add(GetPanel(anchorMin: "0 0.1", anchorMax: "1 0.6"), votingPanelName, name: votingScorePanelName);
                    container.Add(GetLabel($"{usersVotingNo.Count}/{BasePlayer.activePlayerList.Count}", align: TextAnchor.MiddleCenter, size: 12), votingScorePanelName);

                    // voting info 
                    container.Add(GetPanel(anchorMin: "0 0.6", anchorMax: "1 1"), votingPanelName, name: fogNoPanelName);
                    container.Add(GetLabel($"Vote <color=#89b38a>/nofog</color> to remove fog - {requiredVotes} vote(s) required", align: TextAnchor.MiddleCenter, size: 12), fogNoPanelName);


                    CuiHelper.AddUi(player, container);
                }
            }

            if (isVotingOpen) votingPanelRefreshTimer = timer.Once(2F, () => ShowVotingPanel());
        }

        CuiPanel GetPanel(string anchorMin = "0 0", string anchorMax = "1 1", string color = "0.1 0.1 0.1 0", bool cursorEnabled = false)
        {
            return new CuiPanel
            {
                CursorEnabled = cursorEnabled,
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                },
                Image = { Color = color }
            };
        }

        CuiLabel GetLabel(string text, int size = 14, string anchorMin = "0.05 0.02", string anchorMax = "0.98 0.9", TextAnchor align = TextAnchor.MiddleCenter, string color = "1 1 1 1", string font = "robotocondensed-regular.ttf")
        {
            return new CuiLabel
            {
                Text =
                    {
                        Text = text,
                        FontSize = size,
                        Align = align,
                        Color = color,
                        Font = font
                    },
                RectTransform =
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax
                    },
            };
        }

        #endregion
    }
}