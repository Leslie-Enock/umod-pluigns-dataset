namespace Oxide.Plugins
{
    [Info("Instant Barrels", "Mevent", "1.0.3")]
    [Description("Allows you to destroy barrels and roadsigns with one hit")]
    public class InstantBarrels : CovalencePlugin
    {
        #region Fields

        private const string PermUse = "InstantBarrels.use";

        private const string PermRoadSigns = "InstantBarrels.roadsigns";

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermUse, this);

            permission.RegisterPermission(PermRoadSigns, this);
        }

        private void OnEntityTakeDamage(LootContainer container, HitInfo info)
        {
            if (container == null || info == null) return;

            var player = info.InitiatorPlayer;
            if (player == null) return;

            var cov = player.IPlayer;
            if (cov == null) return;

            if (cov.HasPermission(PermUse) && container.ShortPrefabName.Contains("barrel") ||
                cov.HasPermission(PermRoadSigns) && container.ShortPrefabName.Contains("roadsign"))
                info.damageTypes.ScaleAll(1000f);
        }

        #endregion
    }
}