using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Oxide.Plugins
{

    [Info("Blind Death", "UNKNOWN", "0.0.1")]
    [Description("Hide Killer DeathScreen")]
    public class BlindDeath : RustPlugin
    {
        string BlindContainer = "UI_Blind";

        private void OnEntityDeath(BasePlayer player)
        {
            timer.Once(0.2f, () =>
            {
                if (player == null || player.IsConnected == false || player.IsAlive()) return;

                BlindUI(player);
            });
        }
        private void OnPlayerRespawn(BasePlayer player)
        {
            timer.Once(_config.UI_DestoryTime, () =>
            {
                CuiHelper.DestroyUi(player, BlindContainer);
            });
        }

        private void BlindUI(BasePlayer player)
        {
            var score = new CuiElementContainer();

            CuiHelper.DestroyUi(player, BlindContainer);

            score.Add(new CuiPanel
            {
                Image =
                {
                    Color = _config.UI_Color
                },
                RectTransform =
                {
                    AnchorMin = _config.UI_AnchorMin,
                    AnchorMax = _config.UI_AnchorMax
                },
                
                CursorEnabled = false

            }, "Overlay", BlindContainer);

            CuiHelper.AddUi(player, score);
        }
        void Unload()
        {
            foreach (var players in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(players, BlindContainer);
            }
        }

        #region Config
        private Configuration _config;
        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        private class Configuration
        {
            [JsonProperty("UI DestoryTime")]
            public float UI_DestoryTime { get; set; } = 5.0f;
            [JsonProperty("UI AnchorMin")]
            public string UI_AnchorMin { get; set; } = "0.313 0.861";

            [JsonProperty("UI AnchorMax")]
            public string UI_AnchorMax { get; set; } = "0.703 1";
            [JsonProperty("UI Color")]
            public string UI_Color { get; set; } = "0 0 0 1";
        }
        #endregion
    }
}
