using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Limited Smoke Grenade", "Lincoln", "1.0.3")]
    [Description("Limit the smoke grenade duration time.")]
    public class LimitedSmokeGrenade : RustPlugin
    {
        #region Vars
        
        private const string permUse = "LimitedSmokeGrenade.use";
        
        #endregion
        
        #region Oxide Hooks
        
        private void Init()
        {
            permission.RegisterPermission(permUse, this);
            config = Config.ReadObject<PluginConfig>();
        }
        
        private void OnExplosiveThrown(BasePlayer player, SmokeGrenade smokeGrenade)
        {
            SmokeGrenade(player, smokeGrenade);
        }
        private void OnExplosiveDropped(BasePlayer player, SmokeGrenade smokeGrenade)
        {
            SmokeGrenade(player, smokeGrenade);
        }
        private void OnRocketLaunched(BasePlayer player, SmokeGrenade smokeGrenade)
        {
            SmokeGrenade(player, smokeGrenade);
        }
        private void SmokeGrenade(BasePlayer player, SmokeGrenade smokeGrenade)
        {
            if (permission.UserHasPermission(player.UserIDString, permUse))
            {
                smokeGrenade.smokeDuration = config.SmokeDurationInSeconds;
            }
        }
        
        #endregion

        #region Config
        
        private class PluginConfig
        {
            [JsonProperty("Smoke Duration (seconds)")]
            public int SmokeDurationInSeconds;
        }

        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                SmokeDurationInSeconds = 45
            };
        }
        
        #endregion
    }
}