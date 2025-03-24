namespace Oxide.Plugins
{
	[Info("No Heli Flyhack", "Dooby Skoo", "1.2.1")]
	[Description("Prevents players getting kicked for flyhacking after dismounting helicopters.")]

	public class NoHeliFlyhack : RustPlugin
	{
        private const string FHPerm = "noheliflyhack.use";
        private void Init()
        {
            permission.RegisterPermission(FHPerm, this);
        }

        private void OnEntityDismounted(BaseNetworkable entity, BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, FHPerm) && (entity.GetParentEntity() is PlayerHelicopter) || (entity.GetParentEntity() is CH47Helicopter))
            {
                player.PauseFlyHackDetection(5.0f);
            }
        }
    }
}