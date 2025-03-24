namespace Oxide.Plugins
{
    [Info("Whoa Boy", "Clearshot", "1.1.0")]
    [Description("Stop horses from running away when you dismount")]
    class WhoaBoy : CovalencePlugin
    {
        private PluginConfig _config;

        private void Init()
        {
            permission.RegisterPermission("whoaboy.use", this);
        }

        void OnEntityDismounted(BaseVehicleSeat seat, BasePlayer player)
        {
            if (_config.usePermission && player != null && !permission.UserHasPermission(player.UserIDString, "whoaboy.use")) return;
            if (seat == null) return;

            RidableHorse horse = seat.GetParentEntity() as RidableHorse;
            if (horse == null) return;
 
            if (horse.currentSpeed > 4f)
            {
                horse.SwitchMoveState(_config.stopType.Contains("stop") ? BaseRidableAnimal.RunState.stopped : BaseRidableAnimal.RunState.walk);

                if (_config.stopType == "stopAndStand" && horse.CanStand())
                    horse.ClientRPC(null, "Stand");
            }
            else
            {
                horse.SwitchMoveState(BaseRidableAnimal.RunState.stopped);
            }

            horse.currentSpeed = 0f;
            horse.desiredRotation = 0f;
        }

        #region Config

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
        }

        private class PluginConfig
        {
            public bool usePermission = false;
            public string stopType = "stop";
        }

        #endregion
    }
}
