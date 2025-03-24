using Newtonsoft.Json;
namespace Oxide.Plugins
{
    [Info("Health Vehicles", "SwenenzY", "1.0.1")]
    [Description("Raises vehicles health.")]
    public class HealthVehicles : RustPlugin
    {
        #region Config

        private ConfigFile _config;
        public class ConfigFile
        {
            // Mini
            [JsonProperty(PropertyName = "Mini start health ( ex : 500 )")]
            public float MiniStartHealth;
            [JsonProperty(PropertyName = "Mini max health ( ex : 1000 )")]
            public float MiniMaxHealth;
            // Scrap
            [JsonProperty(PropertyName = "Scrap start health ( ex : 500 )")]
            public float ScrapStartHealth;
            [JsonProperty(PropertyName = "Scrap max health ( ex : 2000 )")]
            public float ScrapMaxHealth;
            // Balloon
            [JsonProperty(PropertyName = "Balloon start health ( ex : 500 )")]
            public float BalloonStartHealth;
            [JsonProperty(PropertyName = "Balloon max health ( ex : 2000 )")]
            public float BalloonMaxHealth;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    // Mini
                    MiniStartHealth = 500f,
                    MiniMaxHealth = 1000f,
                    // Scrap
                    ScrapStartHealth = 500f,
                    ScrapMaxHealth = 2000f,
                    // Balloon
                    BalloonStartHealth = 500f,
                    BalloonMaxHealth = 2000f

                };
            }
        }
        protected override void LoadDefaultConfig() => _config = ConfigFile.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
        }
        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Vehicles

        #region Mini Copter

        void OnEntitySpawned(MiniCopter mini)
        {
            if (mini == null)
            {
                return;
            }

            mini.SetMaxHealth(_config.MiniMaxHealth);
            mini._health = _config.MiniStartHealth;
        }

        #endregion

        #region Scrap Copter

        void OnEntitySpawned(ScrapTransportHelicopter scrap)
        {
            if (scrap == null)
            {
                return;
            }

            scrap.SetMaxHealth(_config.ScrapMaxHealth); 
            scrap._health = _config.ScrapStartHealth;

        }

        #endregion

        #region Balloon

        void OnEntitySpawned(HotAirBalloon balloon)
        {
            if (balloon == null)
            {
                return;
            }

            balloon.SetMaxHealth(_config.BalloonMaxHealth);
            balloon._health = _config.BalloonStartHealth;

        }

        #endregion

        #endregion
    }
}