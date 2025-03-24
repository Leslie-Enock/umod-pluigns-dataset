using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Supply Alert", "Ultra", "1.1.2")]
    [Description("Sound and visual alert for dropped supply")]

    class SupplyAlert : RustPlugin
    {
        #region Fields

        Timer guiAlertTimer;
        CuiPanel alertPanel;
        string alertPanelName = "alertPanelName";

        #endregion

        #region Chat Commands

        [ChatCommand("satest")]
        void SupplyAlertTest(BasePlayer player)
        {
            RunSupplyAlert();
        }

        #endregion

        #region Oxide Hooks

        void OnServerInitialized()
        {
            InitCUI();
        }

        void OnEntitySpawned(BaseEntity baseEntity)
        {
            if (baseEntity != null && baseEntity is SupplyDrop)
            {
                RunSupplyAlert();
            }
        }

        void Unload()
        {
            DestroyGUIAlerts();

            guiAlertTimer?.DestroyToPool();
            guiAlertTimer = null;
        }

        #endregion

        #region Core

        void RunSupplyAlert()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (configData.ChatAlertEnabled) SendReply(player, $"<color=#ee2211>{configData.ChatAlertText}</color>");
                if (configData.SoundAlertEnabled) Effect.server.Run($"{configData.SoundAlertAsset}", player.transform.position);
                if (configData.GUIAlertEnabled) DisplayAlertGUI(player);
            }

            if (configData.GUIAlertEnabled) guiAlertTimer = timer.Once(configData.GUIAlertDuration, () => DestroyGUIAlerts());
        }

        #endregion

        #region Config

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Sound alert enabled")]
            public bool SoundAlertEnabled;

            [JsonProperty(PropertyName = "Sound alert asset")]
            public string SoundAlertAsset;

            [JsonProperty(PropertyName = "Chat alert enabled")]
            public bool ChatAlertEnabled;

            [JsonProperty(PropertyName = "Chat alert text")]
            public string ChatAlertText;

            [JsonProperty(PropertyName = "GUI alert enabled")]
            public bool GUIAlertEnabled;

            [JsonProperty(PropertyName = "GUI alert text")]
            public string GUIAlertText;

            [JsonProperty(PropertyName = "GUI alert duration (seconds)")]
            public float GUIAlertDuration;
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

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            configData = new ConfigData()
            {
                ChatAlertEnabled = true,
                ChatAlertText = "Supply in the air",
                GUIAlertEnabled = true,
                GUIAlertText = "SUPPLY IN THE AIR",
                GUIAlertDuration = 3,
                SoundAlertEnabled = true,
                SoundAlertAsset = "assets/bundled/prefabs/fx/invite_notice.prefab"
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
            alertPanel = new CuiPanel
            {
                CursorEnabled = false,
                RectTransform =
                    {
                        AnchorMin = $"0.4 0.77",
                        AnchorMax = $"0.6 0.81"
                    },
                Image =
                 { Color = "0 0 0 0.8" }
            };
        }

        void DisplayAlertGUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, alertPanelName);

            var container = new CuiElementContainer();
            container.Add(alertPanel, name: alertPanelName);
            container.Add(GetLabel($"{configData.GUIAlertText}", align: TextAnchor.MiddleCenter), alertPanelName);

            CuiHelper.AddUi(player, container);
        }

        void DestroyGUIAlerts()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, alertPanelName);
            }
        }

        CuiLabel GetLabel(string text, int size = 13, string anchorMin = "0.05 0.02", string anchorMax = "0.98 0.9", TextAnchor align = TextAnchor.MiddleCenter, string color = "1 1 1 1", string font = "robotocondensed-regular.ttf")
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
