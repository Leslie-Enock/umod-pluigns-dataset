using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Horse Settings", "hoppel", "1.0.0")]
    [Description("Allows you to adjust ridable horses for players with certain permissions")]
    public class HorseSettings : RustPlugin
    {
        #region Declaration

        private Configuration _config;
        private HorseConfiguration _default = new HorseConfiguration();

        #endregion

        #region Hooks

        private void Init()
        {
            foreach (var setting in _config.HorseSettingsList)
            {
                permission.RegisterPermission(setting.Key, this);
            }
        }

        private void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            if (!(player?.GetMountedVehicle() is RidableHorse))
            {
                return;
            }

            var horse = player.GetMountedVehicle() as RidableHorse;
            if (horse == null)
            {
                return;
            }

            foreach (var setting in _config.HorseSettingsList)
            {
                if (permission.UserHasPermission(player.UserIDString, setting.Key))
                {
                    ApplyHorseSettings(horse, setting.Value);
                    return;
                }
            }

            ApplyHorseSettings(horse, _default);
        }

        #endregion

        #region Functions

        private void ApplyHorseSettings(RidableHorse horse, HorseConfiguration config)
        {
            horse.walkSpeed = config.SpeedSetting.WalkSpeed;
            horse.trotSpeed = config.SpeedSetting.TrotSpeed;
            horse.runSpeed = config.SpeedSetting.RunSpeed;
            horse.roadSpeedBonus = config.SpeedSetting.RoadBonusSpeed;
            horse.turnSpeed = config.SpeedSetting.TurnSpeed;

            horse.maxWaterDepth = config.MiscSetting.MaxWaterDepth;
            horse.maxStaminaSeconds = config.MiscSetting.MaxStaminaSeconds;

            horse.staminaCoreLossRatio = config.MetabolismSetting.StaminaCoreLossRatio;
            horse.staminaCoreSpeedBonus = config.MetabolismSetting.StaminaCoreSpeedBonus;
            horse.staminaReplenishRatioMoving = config.MetabolismSetting.StaminaRecoveryRatioMoving;
            horse.staminaReplenishRatioStanding = config.MetabolismSetting.StaminaRecoveryRatioStanding;
            horse.calorieToStaminaRatio = config.MetabolismSetting.CaloriesToStaminaRatio;
            horse.maxStaminaCoreFromWater = config.MetabolismSetting.WaterToStaminaRatio;
        }

        #endregion

        #region HorseSettingClass

        public class HorseConfiguration
        {
            public SpeedSettings SpeedSetting = new SpeedSettings
            {
                WalkSpeed = 2f,
                RoadBonusSpeed = 2f,
                RunSpeed = 14f,
                TrotSpeed = 7f,
                TurnSpeed = 30f
            };

            public MiscSetttings MiscSetting = new MiscSetttings
            {
                MaxWaterDepth = 1.5f,
                MaxStaminaSeconds = 20f
            };

            public MetabolismSettings MetabolismSetting = new MetabolismSettings
            {
                StaminaCoreLossRatio = 0.1f,
                StaminaCoreSpeedBonus = 3f,
                StaminaRecoveryRatioMoving = 0.5f,
                StaminaRecoveryRatioStanding = 1f,
                CaloriesToStaminaRatio = 0.1f,
                WaterToStaminaRatio = 0.5f
            };


            public class SpeedSettings
            {
                public float WalkSpeed;
                public float TrotSpeed;
                public float RunSpeed;
                public float RoadBonusSpeed;
                public float TurnSpeed;
            }

            public class MiscSetttings
            {
                [JsonProperty("How deep can the horse go into water")]
                public float MaxWaterDepth;

                [JsonProperty("Max Stamina (seconds)")]
                public float MaxStaminaSeconds;
            }

            public class MetabolismSettings
            {
                [JsonProperty("How much Stamina the horse is using")]
                public float StaminaCoreLossRatio;

                [JsonProperty("Stamina core speed bonus")]
                public float StaminaCoreSpeedBonus;

                [JsonProperty("Stamina recovery ratio while moving")]
                public float StaminaRecoveryRatioMoving;

                [JsonProperty("Stamina recovery ratio while standing")]
                public float StaminaRecoveryRatioStanding;

                [JsonProperty("Calories used to recover stamina")]
                public float CaloriesToStaminaRatio;

                [JsonProperty("Water used to recover stamina")]
                public float WaterToStaminaRatio;
            }
        }

        #endregion

        #region Config

        public class Configuration
        {
            [JsonProperty(PropertyName = "Settings list")]
            public Dictionary<string, HorseConfiguration> HorseSettingsList;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    HorseSettingsList = new Dictionary<string, HorseConfiguration>
                    {
                        ["horsesettings.admin"] = new HorseConfiguration
                        {
                            MetabolismSetting = new HorseConfiguration.MetabolismSettings
                            {
                                StaminaCoreLossRatio = 0f,
                                StaminaCoreSpeedBonus = 30f,
                                StaminaRecoveryRatioMoving = 200f,
                                StaminaRecoveryRatioStanding = 100f,
                                CaloriesToStaminaRatio = 0f,
                                WaterToStaminaRatio = 0f
                            },
                            MiscSetting = new HorseConfiguration.MiscSetttings
                            {
                                MaxWaterDepth = 100f,
                                MaxStaminaSeconds = 1000f
                            },
                            SpeedSetting = new HorseConfiguration.SpeedSettings
                            {
                                WalkSpeed = 100f,
                                RoadBonusSpeed = 20f,
                                RunSpeed = 140f,
                                TrotSpeed = 70f,
                                TurnSpeed = 90f
                            }
                        },
                        ["horsesettings.vip"] = new HorseConfiguration
                        {
                            MetabolismSetting = new HorseConfiguration.MetabolismSettings
                            {
                                StaminaCoreLossRatio = 0.05f,
                                StaminaCoreSpeedBonus = 5f,
                                StaminaRecoveryRatioMoving = 0.8f,
                                StaminaRecoveryRatioStanding = 3f,
                                CaloriesToStaminaRatio = 0.05f,
                                WaterToStaminaRatio = 0.3f
                            },
                            MiscSetting = new HorseConfiguration.MiscSetttings
                            {
                                MaxWaterDepth = 5f,
                                MaxStaminaSeconds = 40f
                            },
                            SpeedSetting = new HorseConfiguration.SpeedSettings
                            {
                                WalkSpeed = 4f,
                                RoadBonusSpeed = 3f,
                                RunSpeed = 17f,
                                TrotSpeed = 10f,
                                TurnSpeed = 34f
                            }
                        }
                    }
                };
            }
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }
        protected override void LoadDefaultConfig() => _config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
    }
}
