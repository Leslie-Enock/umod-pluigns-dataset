namespace Oxide.Plugins
{
    [Info("No Crash Flying Vehicles", "MON@H", "2.0.0")]
    [Description("Prevents flying vehicles from crashing.")]
    public class NoCrashFlyingVehicles : RustPlugin
    {
        #region Class Fields

        private static readonly object _true = true;

        #endregion Class Fields

        #region Initialization

        private void Init() => Unsubscribe(nameof(OnEntityTakeDamage));

        private void OnServerInitialized() => Subscribe(nameof(OnEntityTakeDamage));

        #endregion Initialization

        #region Oxide Hooks

        private object OnEntityTakeDamage(BaseHelicopter entity, HitInfo info)
        {
            if (info != null
            && entity.IsValid()
            && info.Initiator.IsValid()
            && (info.Initiator is BaseHelicopter)
            && !info.damageTypes.Has(Rust.DamageType.Decay))
            {
                return _true;
            }

            return null;
        }

        #endregion Oxide Hooks
    }
}