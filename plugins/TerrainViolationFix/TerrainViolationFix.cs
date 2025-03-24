namespace Oxide.Plugins
{
    [Info("Terrain Violation Fix", "Tryhard", "1.0.3")]
    [Description("Disables inside terrain violation 200 kicks for admins")]
    public class TerrainViolationFix : RustPlugin
    {
        private void Init() => permission.RegisterPermission("TerrainViolationFix.on", this);

        private object OnPlayerViolation(BasePlayer player, AntiHackType type)
        {
            if (type == AntiHackType.InsideTerrain && permission.UserHasPermission(player.UserIDString, "TerrainViolationFix.on")) return false;
            return null;
        }
    }
}