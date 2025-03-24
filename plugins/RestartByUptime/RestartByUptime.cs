using System;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Restart By Uptime", "Orange", "1.0.4")]
    [Description("Restarts server when uptime reaches specific time and players count is specific")]
    public class RestartByUptime : CovalencePlugin
    {
        #region Vars

        private DateTime nextRestartTime;
        private Timer timerObject;

        #endregion
        
        #region Oxide Hooks

        private void OnServerInitialized()
        {
            nextRestartTime = DateTime.UtcNow.AddSeconds(config.maxUptimeSeconds);
            timerObject = timer.Every(60, CheckUptime);
        }

        private void Unload()
        {
            config = null;
        }

        #endregion

        #region Core

        private void CheckUptime()
        {
            if (server.Players > config.playersBound)
            {
                return;
            }

            #if RUST
            if (UnityEngine.Time.realtimeSinceStartup < config.maxUptimeSeconds)
            {
                return;
            }
            #else
            if (DateTime.UtcNow < nextRestartTime)
            {
                return;
            }
            #endif

            if (timerObject != null && timerObject.Destroyed == false)
            {
                timerObject.Destroy();
            }
            
            server.Command(config.command);
        }

        #endregion
        
        #region Configuration | 24.05.2020

        private static ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Max Uptime (seconds)")]
            public float maxUptimeSeconds = 12 * 60 * 60;

            [JsonProperty(PropertyName = "If on server more than X players, don't trigger restart")]
            public int playersBound = 0;

            [JsonProperty(PropertyName = "Command")]
            public string command = "restart 1";
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
                    LogError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }
                
                LoadDefaultConfig();
                return;
            }

            ValidateConfig();
            SaveConfig();
        }

        private void ValidateConfig()
        {
            if (server.Name.Contains("[DEBUG]") == true)
            {
                LogWarning("Using default configuration on debug server");
                config = new ConfigData();
            }
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