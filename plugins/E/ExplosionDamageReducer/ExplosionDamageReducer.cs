/*
 ########### README ####################################################
                                                                             
  !!! DON'T EDIT THIS FILE !!!
                                                                     
 ########### CHANGES ###################################################

 1.0.0
    - Plugin release

 #######################################################################
*/

using Oxide.Core;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Explosion Damage Reducer", "paulsimik", "1.0.0")]
    [Description("Set damage value for Rockets, High Velocity Rockets and HE Grenades only to players")]
    class ExplosionDamageReducer : RustPlugin
    {
        #region [Oxide Hooks]

        private void OnEntityTakeDamage(BasePlayer victim, HitInfo info)
        {
            if (victim == null || victim.IsNpc || info.damageTypes == null || info.InitiatorPlayer == null)
                return;

            var shortName = info.WeaponPrefab?.ShortPrefabName;
            if (string.IsNullOrEmpty(shortName))
                return;

            if (victim == info.InitiatorPlayer && !config.attackerReduceDamage)
                return;

            float damage = 100;
            switch (shortName)
            {
                case "rocket_basic":
                    {
                        damage = config.rocket;
                        break;
                    }
                case "rocket_hv":
                    {
                        damage = config.hvRocket;
                        break;
                    }
                case "40mm_grenade_he":
                    {
                        damage = config.heGrenade;
                        break;
                    }
            }

            info.damageTypes.ScaleAll(0.01f * damage);
        }

        #endregion

        #region [Classes]

        private Configuration config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Apply reduced damage to the attacker")]
            public bool attackerReduceDamage;

            [JsonProperty(PropertyName = "Rocket")]
            public int rocket;

            [JsonProperty(PropertyName = "High Velocity Rocket")]
            public int hvRocket;

            [JsonProperty(PropertyName = "HE Grenade")]
            public int heGrenade;

            public VersionNumber version;
        }

        #endregion

        #region [Config]

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                attackerReduceDamage = false,
                rocket = 100,
                hvRocket = 100,
                heGrenade = 100,
                version = Version
            };
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();
            Puts("Generating new configuration file........");
        }

        protected override void SaveConfig() => Config.WriteObject(config, true);

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                    LoadDefaultConfig();
            }
            catch
            {
                PrintError("######### Configuration file is not valid! #########");
                return;
            }

            SaveConfig();
        }

        #endregion
    }
}