namespace Oxide.Plugins
{
    [Info("Code Entry", "Wulf", "1.1.0")]
    [Description("Makes code locks always require code input for entry")]
    class CodeEntry : CovalencePlugin
    {
        private void CanUseLockedEntity(BasePlayer basePlayer, CodeLock codeLock)
        {
            // Check if entity is unlocked or already open
            if (!codeLock.IsLocked() || codeLock.GetParentEntity().IsOpen())
            {
                return;
            }

            // Remove player from whitelists so they are prompted for input
            if (codeLock.whitelistPlayers.Contains(basePlayer.userID))
            {
                codeLock.whitelistPlayers.Remove(basePlayer.userID);
            }
            else if (codeLock.guestPlayers.Contains(basePlayer.userID))
            {
                codeLock.guestPlayers.Remove(basePlayer.userID);
            }
        }

        private void OnCodeEntered(CodeLock codeLock, BasePlayer basePlayer, string code)
        {
            if (codeLock.code == code)
            {
                // Open doors on code acceptance
                BaseEntity entity = codeLock.GetParentEntity();
                if (entity != null && !entity.IsOpen())
                {
                    entity.SetFlag(BaseEntity.Flags.Open, true);
                    entity.SendNetworkUpdate();
                }

                // Open loot panels on code acceptance
                if (entity is StorageContainer)
                {
                    (entity as StorageContainer).PlayerOpenLoot(basePlayer);
                }
            }
        }
    }
}
