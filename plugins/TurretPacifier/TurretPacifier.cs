namespace Oxide.Plugins
{
    [Info("Turret Pacifier", "The Friendly Chap", "0.0.3")]
    [Description("Prevents AutoTurret from targeting innocents")]
    class TurretPacifier : RustPlugin
    {
        private object CanBeTargeted(BasePlayer player, AutoTurret turret)
        {
            return !player.IsNpc && !player.IsHostile() ? false : (object)null;
        }
    }
}
