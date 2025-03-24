namespace Oxide.Plugins
{
    [Info("No Igniter Drain", "Lincoln", "1.0.3")]
    [Description("Prevent Igniters from damaging themselves while in use.")]

    public class NoIgniterDrain : RustPlugin
    {
        private void OnServerInitialized()
        {
            permission.RegisterPermission("NoIgniterDrain.unlimited", this);

            foreach (var entity in UnityEngine.Object.FindObjectsOfType<Igniter>())
            {
                OnEntitySpawned(entity);
            }
        }

        private void OnEntitySpawned(Igniter entity)
        {
            var player = entity.OwnerID.ToString();
            if(!permission.UserHasPermission(player, "NoIgniterDrain.unlimited"))
            {
                return;
            }
            else
            {
                entity.SelfDamagePerIgnite = 0f;
            }
        }

        private void Unload()
        {
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<Igniter>())
            {
                entity.SelfDamagePerIgnite = 0.5f;
            }
        }
    }
}