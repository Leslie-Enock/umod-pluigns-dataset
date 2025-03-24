using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("No MLG Water", "Polybro", "1.0.2")]
    [Description("Adds fall damage when falling into the water")]
    public class NoMlgWater : CovalencePlugin
    {
        #region Variables
        private static ConfigData config;
        List<ulong> pool = new List<ulong>();
        #endregion
        #region Hooks
        object OnPlayerTick(BasePlayer player, PlayerTick msg, bool wasPlayerStalled)
        {
            if ((!player.IsOnGround() && !(player.IsFlying || player.isMounted)) && (player.IsSwimming() && !pool.Contains(player.userID)))
            {
                ApplyFallDamage(player);
                pool.Add(player.userID);
            }
            if (!player.IsSwimming() && pool.Contains(player.userID)) pool.Remove(player.userID);
            return null;
        }
        private void Unload()
        {
            config = null;
        }
        #endregion
        #region Commands
        [Command("nmw.reload")]
        [Permission("nomlgwater.admin")]
        private void ReloadConfig(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer sender = (BasePlayer)iplayer.Object;
            try
            {
                LoadConfig();
                Respond(sender, "ReloadSuccess");
            }
            catch (Exception e)
            {
                PrintWarning($"Config loading error: {e.Message}");
            }
        }
        #endregion
        #region Calculations
        private void ApplyFallDamage(BasePlayer player)
        {
            float speed = player.estimatedSpeed;
            if (speed < config.MinimumSpeed) return;
            player.ApplyFallDamageFromVelocity((config.DecreaseEnterDamage && config.MinimumSpeed >= 15 == true ? Math.Abs(speed - Math.Abs(config.MinimumSpeed - 15)) : speed) * config.DamageExposure * -1);
            NextFrame(() =>
            {
                if (player.IsWounded() && !config.EnableWounded)
                {
                    player.Die();
                }
            });
        }
        #endregion
        #region Config
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<ConfigData>();
            config.DamageExposure = Math.Abs(config.DamageExposure);
            config.MinimumSpeed = Math.Abs(config.MinimumSpeed);
            if (config.MinimumSpeed < 15) config.DecreaseEnterDamage = false;
            Config.WriteObject(config, true);
        }
        protected override void SaveConfig() => Config.WriteObject(config, false);
        protected override void LoadDefaultConfig() => config = GetBaseConfig();
        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                DamageExposure = 0.75f,
                MinimumSpeed = 0.0f,
                DecreaseEnterDamage = false,
                EnableWounded = false
            };
        }
        public class ConfigData
        {
            [JsonProperty(PropertyName = "Damage Exposure")]
            public float DamageExposure { get; set; }
            [JsonProperty(PropertyName = "Minimum damage speed (>= 0)")]
            public float MinimumSpeed { get; set; }
            [JsonProperty(PropertyName = "Decrease enter damage (change only if minimum speed >= 15.0)")]
            public bool DecreaseEnterDamage { get; set; }
            [JsonProperty(PropertyName = "Allow wounded state after hitting the water too hard")]
            public bool EnableWounded { get; set; }
        }
        #endregion
        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string> { { "ReloadSuccess", "Successfully reloaded config." } }, this);
            lang.RegisterMessages(new Dictionary<string, string> { { "ReloadSuccess", "Configuración recargada con éxito." } }, this, "es");
            lang.RegisterMessages(new Dictionary<string, string> { { "ReloadSuccess", "Configuration rechargée avec succès." } }, this, "fr");
            lang.RegisterMessages(new Dictionary<string, string> { { "ReloadSuccess", "Konfiguration erfolgreich neu geladen." } }, this, "de");
            lang.RegisterMessages(new Dictionary<string, string> { { "ReloadSuccess", "Конфігурацію успішно перезавантажено." } }, this, "uk");
            lang.RegisterMessages(new Dictionary<string, string> { { "ReloadSuccess", "Pomyślnie załadowano ponownie konfigurację." } }, this, "pl");
            lang.RegisterMessages(new Dictionary<string, string> { { "ReloadSuccess", "Конфигурация успешно перезагружена." } }, this, "ru");
        }
        private void Respond(BasePlayer Player, string key) => Player.ChatMessage(lang.GetMessage(key, this, Player.UserIDString));
        #endregion
    }
}