using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Arrow Raiding", "birthdates", "2.0.1")]
    [Description("Break wooden doors with arrows like old Rust")]
    public class ArrowRaiding : RustPlugin
    {
        #region Variables
        private readonly string permission_use = "arrowraiding.use";
        #endregion

        #region Hooks
        private void Init()
        {
            LoadConfig();
            permission.RegisterPermission(permission_use, this);
        } 

        void OnEntityTakeDamage(Door entity, HitInfo info)
        {
            if(info.Weapon == null || !info.IsProjectile() || info.InitiatorPlayer == null || !entity.PrefabName.Contains("wood") || !info.InitiatorPlayer.IPlayer.HasPermission(permission_use)) return;
            ArrowDamage BaseDamage;
            if(!_config.ArrowDamage.TryGetValue(info.ProjectilePrefab.name, out BaseDamage)) return; 
            float Mult; 
            if(!_config.BowMultipliers.TryGetValue(info.Weapon.ShortPrefabName, out Mult)) Mult = 1;
            info.damageTypes.Set(Rust.DamageType.Arrow, Core.Random.Range(BaseDamage.MinDamage, BaseDamage.MaxDamage + 1) * Mult);
        }
        #endregion

        #region Configuration & Language
        public ConfigFile _config;

        public class ArrowDamage
        {
            [JsonProperty("Minimum Damage")]
            public float MinDamage;
            [JsonProperty("Maximum Damage")]
            public float MaxDamage;
        }

        public class ConfigFile
        {
            [JsonProperty("Arrow Damage")]
            public Dictionary<string, ArrowDamage> ArrowDamage;

            [JsonProperty("Weapon Multipliers")]
            public Dictionary<string, float> BowMultipliers;
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                   ArrowDamage = new Dictionary<string, ArrowDamage>
                   {
                       {"arrow_wooden", new ArrowDamage{ MinDamage = 1f, MaxDamage = 2f}},
                       {"arrow_bone", new ArrowDamage{ MinDamage = 3f, MaxDamage = 5f}},
                       {"arrow_hv", new ArrowDamage{ MinDamage = 0.5f, MaxDamage = 1f}},
                       {"arrow_fire", new ArrowDamage{ MinDamage = 5f, MaxDamage = 6f}}
                   },
                   BowMultipliers = new Dictionary<string, float>
                   {
                       {"bow_hunting.entity", 1f},
                       {"compound_bow.entity", 1.5f},
                       {"crossbow.entity", 2f}
                   },
                };
            }
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
    
        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
        #endregion
    }
}
//Generated with birthdates' Plugin Maker
