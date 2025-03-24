namespace Oxide.Plugins
{
    [Info("All Bags Cooldown", "Flashtim", "1.0.4")]
    [Description("Put same cooldown on all sleeping bags of player")]
    class AllBagsCooldown : RustPlugin
    {
        private const string permAllow = "allbagscooldown.nosync.allow";

        private void Init()
        {
            permission.RegisterPermission(permAllow, this);
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (!permission.UserHasPermission(player.UserIDString, permAllow))
                ResetSpawnTargets(player);
        }

        private void ResetSpawnTargets(BasePlayer player)
        {
            SleepingBag[] bags = SleepingBag.FindForPlayer(player.userID, true);
            
            float maxTime = 0;

            foreach (SleepingBag bag in bags) {
                if(maxTime < bag.unlockTime)
                {
                    maxTime = bag.unlockTime;
                } 
            }
            foreach (SleepingBag bag in bags) {
                bag.unlockTime = maxTime;
            }
        }
    }
}
