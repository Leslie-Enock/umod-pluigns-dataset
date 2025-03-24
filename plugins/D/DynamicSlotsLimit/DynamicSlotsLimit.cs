using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Dynamic Slots Limit", "Orange", "1.0.5")]
    [Description("Modifies maximal server slots based on current players count")]
    public class DynamicSlotsLimit : CovalencePlugin
    {
        #region Oxide Hooks

        private void OnServerInitialized()
        {
            CheckSlots();
        }
        
        private void OnUserConnected(IPlayer player)
        {
            CheckSlots();
        }

        private void OnUserDisconnected(IPlayer player)
        {
            CheckSlots();
        }

        private void Unload()
        {
            config = null;
        }

        #endregion

        #region Core

        private void CheckSlots()
        {
            var occupiedSlots = server.Players;
            if (occupiedSlots == 0)
            {
                server.MaxPlayers = config.slotsMin;
                return;
            }
            
            var maximalSlots = server.MaxPlayers;
            var freeSlots = maximalSlots - occupiedSlots;
            var change = freeSlots < config.triggerStep ? config.changeStep : -config.changeStep;
            var targetSlots = maximalSlots + change;
            if (targetSlots > config.slotsMin && targetSlots < config.slotsMax)
            {
                server.MaxPlayers += change;
            }
        }

        #endregion
        
        #region Configuration | 24.05.2020

        private static ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Minimal slots")]
            public int slotsMin = 100;

            [JsonProperty(PropertyName = "Maximal slots")]
            public int slotsMax = 200;
            
            [JsonProperty(PropertyName = "Step rate")]
            public int changeStep = 5;
            
            [JsonProperty(PropertyName = "Trigger when X slots left")]
            public int triggerStep = 10;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                for (var i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }
                
                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion
    }
}