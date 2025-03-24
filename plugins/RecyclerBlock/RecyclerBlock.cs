using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Recycler Block", "Krungh Crow", "1.0.2")]
    [Description("Disables using the recycler")]

    class RecyclerBlock : RustPlugin
    {
        void Init() => permission.RegisterPermission("recyclerblock.bypass" , this);

        private object CanLootEntity(BasePlayer player, Recycler recycler)
        {

            if (player == null || recycler == null) return null;
            if (permission.UserHasPermission(player.UserIDString, "recyclerblock.bypass")) return null;
            return false;
        }
    }
}