using System.Collections.Generic;
using Newtonsoft.Json;
using Rust;

namespace Oxide.Plugins
{
    [Info("Swim Damage Toggle", "Hockeygel23", "0.0.7")]
    [Description("It gives a player immunity to drowning, cold damage, and cold exposure when a player is swimming")]
    public class SwimDamageToggle : RustPlugin
    {
        #region Vars

        private const string AdminPermission = "swimdamagetoggle.admin";
        private const string PlayerPermission = "swimdamagetoggle.use";

        private ConfigData config;

        #endregion

        #region Config

        private class ConfigData
        {
            [JsonProperty("No Drowning Damage")]
            public bool DrowningDamage;

            [JsonProperty("No Cold Exposure Damage")]
            public bool ColdExposure;

            [JsonProperty("No Cold Damage")]
            public bool ColdDamage;
        }

        private ConfigData GenerateConfig()
        {
            return new ConfigData
            {
                DrowningDamage = false,
                ColdExposure = false,
                ColdDamage = false
            };
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MissingArg", "Empty or incorrect argument! '/swimdamage drown/cold/coldexposure'"},
                {"ColdDamage", "You have set cold damage to <color={0}>{1}</color>"},
                {"DrownDamage", "You have set drowning damage to <color={0}>{1}</color>"},
                {"ColdExposure", " You have set cold exposure damage to <color={0}>{1}</color>"},
                {"NoPerm", "Unkown command: swimdamage"}
            }, this);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    throw new JsonException();
                }
            }
            catch
            {
                PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");

                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = new ConfigData();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(PlayerPermission, this);
            permission.RegisterPermission(AdminPermission, this);
        }

        private void OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, PlayerPermission) || !player.IsSwimming())
            {
                return;
            }

            if (!config.DrowningDamage)
            {
                info.damageTypes.Set(DamageType.Drowned, 0f);
            }

            if (!config.ColdExposure)
            {
                info.damageTypes.Set(DamageType.ColdExposure, 0f);
            }

            if (!config.ColdDamage)
            {
                info.damageTypes.Set(DamageType.Cold, 0f);
            }
        }

        #endregion

        #region Command

        [ChatCommand("swimdamage")]
        private void cmdToggle(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                player.ChatMessage(Lang("NoPerm", player.UserIDString));
                return;
            }
            if (args.Length != 1)
            {
                player.ChatMessage(Lang("MissingArg", player.UserIDString));
                return;
            }

            switch(args[0].ToLower())
            {
                case "drown":
                    config.DrowningDamage = !config.DrowningDamage;
                    player.ChatMessage(Lang("DrownDamage", player.UserIDString, config.DrowningDamage ? "green" : "red", config.DrowningDamage));
                    break;
                case "cold":
                    config.ColdDamage = !config.ColdDamage;
                    player.ChatMessage(Lang("ColdDamage", player.UserIDString, config.ColdDamage ? "green" : "red", config.ColdDamage));
                    break;
                case "coldexposure":
                    config.ColdExposure = !config.ColdExposure;
                    player.ChatMessage(Lang("ColdExposure", player.UserIDString, config.ColdExposure ? "green" : "red", config.ColdExposure));
                    break;
                default:
                    player.ChatMessage(Lang("MissingArg", player.UserIDString));
                    break;
            }
            SaveConfig();
        }

        #endregion
    }
}