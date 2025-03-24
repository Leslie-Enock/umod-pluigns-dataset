using System;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Runaway Boats", "0x89A", "1.2.1")]
    [Description("Stops boats from sailing away on dismount")]
    class RunawayBoats : RustPlugin
    {
        private const string _canUse = "runawayboats.use";

        void Init() => permission.RegisterPermission(_canUse, this);

        private void OnEntityDismounted(BaseMountable mount, BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, _canUse))
            {
                return;
            }

            MotorRowboat boat = mount.GetParentEntity() as MotorRowboat;
            if (boat == null)
            {
                return;
            }

            StopBoat(boat);
        }

        private void StopBoat(MotorRowboat boat)
        {
            bool hasDriver = boat.HasDriver();

            if ((!hasDriver && (!boat.AnyMounted() || _config.stopWithPassengers)) || (hasDriver && _config.stopIfNotDriver))
            {
                boat.gasPedal = 0;
            }
        }

        #region -Configuration-

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty("Stop if dismounted player is not driver")]
            public bool stopIfNotDriver = true;

            [JsonProperty("Stop if boat has passengers")]
            public bool stopWithPassengers = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Failed to load config, using default values");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
    }
}