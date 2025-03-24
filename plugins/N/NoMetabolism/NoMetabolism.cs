using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("No Metabolism", "MON@H", "1.1.3")]
    [Description("Allows the player to have a consistent metabolism.")]
    public class NoMetabolism : CovalencePlugin
    {
        #region Class Fields

        private const string PermissionUse = "nometabolism.use";

        #endregion Class Fields

        #region Initialization

        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
        }

        #endregion Initialization

        #region Configuration

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Activate for all players without permission")]
            public bool AllAllowed = false;

            [JsonProperty(PropertyName = "Activate for admins without permission")]
            public bool AdminsAllowed = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(_configData);

        #endregion Configuration

        #region OxideHooks

        object OnRunPlayerMetabolism(PlayerMetabolism metabolism, BasePlayer player)
        {
            if (player == null || metabolism == null)
            {
                return null;
            }

            if (_configData.AllAllowed || (_configData.AdminsAllowed && player.IsAdmin))
            {
                return HandlePlayerMetabolism(metabolism, player);
            }

            if (permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                return HandlePlayerMetabolism(metabolism, player);
            }

            return null;
        }

        #endregion OxideHooks

        #region Core

        private object HandlePlayerMetabolism(PlayerMetabolism metabolism, BasePlayer player)
        {
            if (metabolism.bleeding.value > metabolism.bleeding.min)
            {
                metabolism.bleeding.Reset();
                metabolism.isDirty = true;
                metabolism.SendChangesToClient();
            }

            if (metabolism.calories.value < metabolism.calories.max)
            {
                metabolism.calories.value = metabolism.calories.max;
            }

            if (metabolism.comfort.value < 0)
            {
                metabolism.comfort.value = 0;
            }

            if (metabolism.hydration.value < metabolism.hydration.max)
            {
                metabolism.hydration.value = metabolism.hydration.max;
            }

            if (metabolism.oxygen.value < metabolism.oxygen.max)
            {
                metabolism.oxygen.value = metabolism.oxygen.max;
            }

            if (metabolism.poison.value > metabolism.poison.min)
            {
                metabolism.poison.value = metabolism.poison.min;
            }

            if (metabolism.radiation_level.value > metabolism.radiation_level.min)
            {
                metabolism.radiation_level.value = metabolism.radiation_level.min;
            }

            if (metabolism.radiation_poison.value > metabolism.radiation_poison.min)
            {
                metabolism.radiation_poison.value = metabolism.radiation_poison.min;
            }

            if (metabolism.temperature.value > PlayerMetabolism.HotThreshold || metabolism.temperature.value < PlayerMetabolism.ColdThreshold)
            {
                metabolism.temperature.value = (PlayerMetabolism.HotThreshold + PlayerMetabolism.ColdThreshold) / 2;
            }

            if (metabolism.wetness.value > metabolism.wetness.min)
            {
                metabolism.wetness.value = metabolism.wetness.min;
            }

            if (metabolism.pending_health.value > 1)
            {
                player.Heal(1);
                metabolism.pending_health.value -= 1;
            }
            else if (metabolism.pending_health.value > 0)
            {
                player.Heal(metabolism.pending_health.value);
                metabolism.pending_health.value = 0;
            }

            player.SetPlayerFlag(BasePlayer.PlayerFlags.Workbench1, player.currentCraftLevel == 1f);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Workbench2, player.currentCraftLevel == 2f);
            player.SetPlayerFlag(BasePlayer.PlayerFlags.Workbench3, player.currentCraftLevel == 3f);

            player.SetPlayerFlag(BasePlayer.PlayerFlags.SafeZone, player.InSafeZone());

            return true;
        }

        #endregion Core
    }
}