using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Disable Temperature Functions", "The Friendly Chap", "1.0.2")]
    [Description("Prevents cold/heat damage/overlay for players")]
    public class DisableTemperatureFunctions : RustPlugin
    {
        #region ConfigFileStuff
        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Debug Mode")]
            public bool debug = false;
            [JsonProperty(PropertyName = "Set Temprature to (°C)")]
            public float usertemp = 30.0f;
            [JsonProperty(PropertyName = "Use permission : ")]
            public bool usePerm = false;
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

        void Init()
        {
            if (!LoadConfigVariables())
            {
                Puts("Config file issue detected. Please delete file, or check syntax and fix.");
                return;
            }
            permission.RegisterPermission(permDisable, this);
        }
            

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            configData = new ConfigData();
            SaveConfig(configData);
        }
        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        private const string permDisable = "disabletemperaturefunctions.use";
		
        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList.ToList())
            {
                OnPlayerSleepEnded(player);
            }
            
            foreach (var player in BasePlayer.sleepingPlayerList.ToList())
            {
                OnPlayerSleep(player);
            }
        }
        
        private void OnPlayerSleep(BasePlayer player)
        {
            Check(player);
        }
        
        private void OnPlayerSleepEnded(BasePlayer player)
        {
            Check(player);
        }

        private void FixTemp(BasePlayer player)
        {
            if (configData.debug) Puts($"Initial Values : {player.metabolism.temperature.max}, {player.metabolism.temperature.min}, {player.metabolism.temperature.value}");
            if (configData.debug) Puts($" Adjusting Tolerance for {player.displayName}");
            player.metabolism.temperature.max = configData.usertemp;
            player.metabolism.temperature.min = configData.usertemp;
            player.metabolism.temperature.value = configData.usertemp;
            player.SendNetworkUpdate();
            if (configData.debug) Puts($"Changed Values : {player.metabolism.temperature.max}, {player.metabolism.temperature.min}, {player.metabolism.temperature.value}");
            return;
        }

        private void Check(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permDisable))
            {
                FixTemp(player);
                return;
            }
            else if (!configData.usePerm)
            {
                FixTemp(player);
                return;
            }
        }
    }
}