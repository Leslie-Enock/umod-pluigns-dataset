using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Cactus Damage", "birthdates", "1.1.0")]
    [Description("Cacti deal damage to players harvesting/colliding with them.")]
    public class CactusDamage : RustPlugin
    {
        #region Hooks
        void Init() => LoadConfig();

        void OnDispenserGather(ResourceDispenser dispenser, BasePlayer entity)
        {
            if (!dispenser.name.Contains("cactus")) return;
            Hurt(entity, _config.harvestingDamage, dispenser.GetComponent<BaseEntity>() ?? entity);
        }

        void OnEntityTakeDamage(BasePlayer entity, HitInfo info)
        {
            if(info.Initiator?.ShortPrefabName.Contains("cactus") == false || info.damageTypes.Get(Rust.DamageType.Slash) > 0 || info.damageTypes.Get(Rust.DamageType.Bleeding) > 0) return;
            Hurt(entity, _config.collisionDamage, info.Initiator ?? entity);
        }

        void Hurt(BasePlayer Player, Damage Damage, BaseEntity Initiator)
        {
            var Amount = Core.Random.Range(Damage.MinDamage, Damage.MaxDamage);
            Player.Hurt(Amount, Rust.DamageType.Slash, Initiator);
            Player.metabolism.bleeding.value += Amount / 2;
        }
        #endregion

        #region Configuration
        public ConfigFile _config;

        public class Damage
        {
            [JsonProperty("Min Damage")]
            public float MinDamage;
            [JsonProperty("Max Damage")]
            public float MaxDamage;
        }

        public class ConfigFile
        {
            [JsonProperty("Harvesting Damage")]
            public Damage harvestingDamage;

            [JsonProperty("Collision Damage")]
            public Damage collisionDamage;
            public static ConfigFile DefaultConfig() => new ConfigFile()
            {
                harvestingDamage = new Damage
                {
                    MinDamage = 2f,
                    MaxDamage = 5f
                },
                collisionDamage = new Damage
                {
                    MinDamage = 2f,
                    MaxDamage = 5f
                }
            };
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if(_config == null)
            {
                LoadDefaultConfig();
            }
        }
    
        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion
    }
}
//Generated with birthdates' Plugin Maker
