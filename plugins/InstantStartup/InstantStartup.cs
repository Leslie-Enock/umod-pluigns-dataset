namespace Oxide.Plugins
{
    [Info("Instant Startup", "Kaysharp/patched by chrome", "1.0.3")]
    [Description("Instant engine Startup for Minicopter and Scrap Transport Helicopter")]
    public class InstantStartup : RustPlugin
    {
        private string permissionuse = "Instantstartup.use";
        private void Init()
        {
            permission.RegisterPermission(permissionuse, this);
        }
        private void OnEngineStarted(BaseMountable entity, BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionuse)) return;
            var vehicle = player.GetMountedVehicle();
            if (vehicle == null) return;
            if (vehicle is Minicopter)
            {
                var Heli = vehicle as Minicopter;
                if (Heli == null) return;
                Heli.engineController.FinishStartingEngine();
            }
        }
    }
}