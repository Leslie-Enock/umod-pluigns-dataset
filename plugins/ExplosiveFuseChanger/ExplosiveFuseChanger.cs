using System;
using Newtonsoft.Json;


namespace Oxide.Plugins
{
    [Info("Explosive Fuse Changer", "Hockeygel23", "0.0.3")]
    [Description("Changes the fuse time of certain explosives")]
    public class ExplosiveFuseChanger : RustPlugin
    {
        #region Vars
        private const string Permission = "ExplosiveFuseChanger.Use";
        private const string C4PrefabName = "explosive.timed.entity";
        private const string SatchelPrefabName = "explosive.satchel.entity";
        private const string BeanCanPrefabName = "grenade.beancan.entity";
        private const string F1NadePrefabName = "grenade.f1.entity";
        private const string SmokeNadePrefabName = "smoke_grenade.weapon";
        private const string SurveyChargePrefabName = "survey_charge";
        private static ConfigData config;

        #endregion

        #region Init
        private void Init()
        {
            permission.RegisterPermission(Permission, this);
        }

        #endregion

        #region Config

        private class ConfigData
        {
            [JsonProperty("C4 Fuse Time")]
            public float C4FuseTime;

            [JsonProperty("Satchel Fuse Time")]
            public float SatchelFuseTime;

            [JsonProperty( "BeanCan Fuse Time")]
            public float BeancanFuseTime;

            [JsonProperty("Survey Charge Fuse Time")]
            public float SurveyChargeFuseTime;

            [JsonProperty("F1 Explosive Time")]
            public float F1NadeFuseTime;

            [JsonProperty("Smoke Granade Fuse Time")]
            public float SmokeNadeFuseTime;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                C4FuseTime = 5,
                SatchelFuseTime = 5,
                BeancanFuseTime = 5,
                SurveyChargeFuseTime = 4,
                F1NadeFuseTime = 3,
                SmokeNadeFuseTime = 4
            };
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
                PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion

        #region OxideHooks
        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item)
        {
            if(!permission.UserHasPermission(player.UserIDString, Permission))
            {
                return;
            }
            var Explosive = entity as TimedExplosive;

            switch (item.ShortPrefabName)
            {
                case C4PrefabName:
                    Explosive.SetFuse(config.C4FuseTime);
                    break;

                case SatchelPrefabName:
                    Explosive.SetFuse(config.SatchelFuseTime);
                    break;

                case F1NadePrefabName:
                    Explosive.SetFuse(config.F1NadeFuseTime);
                    break;

                case BeanCanPrefabName:
                    Explosive.SetFuse(config.BeancanFuseTime);
                    break;

                case SmokeNadePrefabName:
                    Explosive.SetFuse(config.SmokeNadeFuseTime);
                    break;

                case SurveyChargePrefabName:
                    Explosive.SetFuse(config.SurveyChargeFuseTime);
                    break;
            }
        }
        #endregion

        private void Unload()
        {
            config = null;
        }
    }
}