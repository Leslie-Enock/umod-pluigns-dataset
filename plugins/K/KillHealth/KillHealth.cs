using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Kill Health", "Orange", "1.0.0")]
    [Description("Get health for killing another player")]
    public class KillHealth : RustPlugin
    {
        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            var initiator = info?.InitiatorPlayer;
            if (initiator == null || player.userID.IsSteamId() == false || initiator?.userID.IsSteamId() == false)
            {
                return;
            }
            
            initiator.Heal(100f);
        }
    }
}