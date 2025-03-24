using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Fall Damage", "Wulf", "3.0.0")]
    [Description("Modifies or disables the fall damage for players")]
    class FallDamage : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        class Configuration
        {
            [JsonProperty("Apply realistic fall damage")]
            public bool RealisticDamage { get; set; } = true;

            [JsonProperty("Damage modifier for falls")]
            public float DamageModifier { get; set; } = 12f;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            LogWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Permissions

        private const string permNone = "falldamage.none";

        private void Init()
        {
            permission.RegisterPermission(permNone, this);
        }

        #endregion Permissions

        #region Damage Modification

#if RUST

        private void OnEntityTakeDamage(BasePlayer player, HitInfo hitInfo)
        {
            if (hitInfo.damageTypes.Total() <= 0)
            {
                return;
            }

            Rust.DamageType damageType = hitInfo.damageTypes.GetMajorityDamageType();
            if (damageType != Rust.DamageType.Fall)
            {
                return;
            }

            if (permission.UserHasPermission(player.UserIDString, permNone))
            {
                hitInfo.damageTypes = new Rust.DamageTypeList();
            }
            else if (config.RealisticDamage)
            {
                float oldDamage = hitInfo.damageTypes.Total();
                float newDamage = (player.Health() / config.DamageModifier) * (oldDamage * 0.35f);
                hitInfo.damageTypes.Set(damageType, newDamage);
            }
            else
            {
                hitInfo.damageTypes.Set(damageType, hitInfo.damageTypes.Total() * config.DamageModifier);
            }
        }

#endif

#if HURTWORLD

        private object OnPlayerTakeDamage(PlayerSession session, EntityEffectSourceData source)
        {
            if (!source.Equals(EntityEffectSourceData.FallDamage))
            {
                return null;
            }

            if (permission.UserHasPermission(session.SteamId.ToString(), permNone))
            {
                return 0f;
            }

            if (config.RealisticDamage)
            {
                IEntityFluidEffect fluidEffect = session.WorldPlayerEntity.Stats.GetFluidEffect(EntityFluidEffectKeyDatabase.Instance.Health);
                if (fluidEffect != null)
                {
                    float health = fluidEffect.GetValue();
                    float newDamage = (health / config.DamageModifier) * (source.Value * 0.35f);
                    return newDamage;
                }
            }

            return source.Value * config.DamageModifier;
        }

#endif

        #endregion Damage Modification
    }
}
