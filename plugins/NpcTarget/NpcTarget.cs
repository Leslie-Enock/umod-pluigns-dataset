namespace Oxide.Plugins
{
    [Info("NPC Target", "misticos", "1.0.4")]
    [Description("Prevent NPCs from targeting each other")]
    class NpcTarget : RustPlugin
    {
        private object OnNpcTarget(BaseEntity attacker, BaseEntity entity)
        {
            if (IsNpc(entity))
                return true;

            return null;
        }

        private object CanBradleyApcTarget(BradleyAPC attacker, BaseEntity entity)
        {
            if (IsNpc(entity))
                return false;

            return null;
        }

        private bool IsNpc(BaseEntity entity) =>
            entity != null && entity.IsNpc;
    }
}