using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Default Radio Station", "marcuzz", "1.1.0")]
    [Description("Set default radio station for spawned, created or placed boomboxes.")]
    public class DefaultRadioStation : RustPlugin
    {
        private static PluginConfig _config;

        void OnEntitySpawned(DeployableBoomBox boombox)
        {
            if (!_config.SetDefaultHeld)
                return;

            if (boombox != null)
                SetBoomboxRadioIP(boombox.BoxController);
        }

        void OnEntitySpawned(HeldBoomBox boombox)
        {
            if (!_config.SetDefaultHeld)
                return;

            if (boombox != null)
                SetBoomboxRadioIP(boombox.BoxController);
        }

        void Unload()
        {
            _config = null;
            Config.Clear();
        }

        private void SetBoomboxRadioIP(BoomBox box)
        {
            int index = 0;
            if (_config.DefaultRadioStationUrlList.Count > 1)
                index = Random.Range(0, _config.DefaultRadioStationUrlList.Count);

            box.CurrentRadioIp = _config.DefaultRadioStationUrlList[index];
            box.baseEntity
                .ClientRPC<string>(null, "OnRadioIPChanged", box.CurrentRadioIp);
        }

        private static PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                DefaultRadioStationUrlList = new List<string> { "rustradio.facepunch.com" },
                SetDefaultDeployed = true,
                SetDefaultHeld = true,
            };
        }

        private class PluginConfig
        {
            [JsonProperty("Default radio station URL list (mp3 streams): ")] public List<string> DefaultRadioStationUrlList { get; set; }
            [JsonProperty("Set for deployed: ")] public bool SetDefaultDeployed { get; set; }
            [JsonProperty("Set for held: ")] public bool SetDefaultHeld { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config.WriteObject(GetDefaultConfig(), true);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
        }
    }
}