namespace Oxide.Plugins
{
    [Info("No Loot", "Wulf", "1.1.0")]
    [Description("Removes all loot containers and prevents them from spawning")]
    class NoLoot : CovalencePlugin
    {
        #region Initialization

        private void Init() => Unsubscribe(nameof(OnEntitySpawned));

        private void OnServerInitialized()
        {
            int count = 0;
            foreach (LootContainer entity in UnityEngine.Resources.FindObjectsOfTypeAll<LootContainer>())
            {
                if (ProcessEntity(entity))
                {
                    count++;
                }
            }
            Subscribe(nameof(OnEntitySpawned));
            Puts($"Removed {count} loot containers");
        }

        #endregion Initialization

        #region Loot Handling

        private bool ProcessEntity(BaseEntity entity)
        {
            if (!entity.isActiveAndEnabled || entity.IsDestroyed || entity.OwnerID != 0)
            {
                return false;
            }

            if (entity is JunkPile)
            {
                JunkPile junkPile = entity as JunkPile;
                junkPile.CancelInvoke("TimeOut");
                junkPile.CancelInvoke("CheckEmpty");
                junkPile.CancelInvoke("Effect");
                junkPile.CancelInvoke("SinkAndDestroy");
                junkPile.Kill();
            }
            else
            {
                entity.Kill();
            }

            return true;
        }

        private void OnEntitySpawned(JunkPile junkPile) => ProcessEntity(junkPile);

        private void OnEntitySpawned(LootContainer container) => ProcessEntity(container);

        #endregion Loot Handling
    }
}
