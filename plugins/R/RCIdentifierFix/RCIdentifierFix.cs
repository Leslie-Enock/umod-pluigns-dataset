using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("RC Identifier Fix", "WhiteThunder", "1.0.0")]
    [Description("Automatically updates saved RC identifiers in computer stations after an entity is destroyed and the ID is reused.")]
    internal class RCIdentifierFix : CovalencePlugin
    {
        #region Hooks

        private void OnEntityMounted(ComputerStation computerStation, BasePlayer player)
        {
            if (computerStation.controlBookmarks.IsEmpty())
                return;

            var bookmarkModifications = new Dictionary<string, uint>();

            foreach (var entry in computerStation.controlBookmarks)
            {
                var bookmarkName = entry.Key;
                var cachedEntityId = entry.Value;

                BaseEntity entityWithRCIdentifier;
                var controllable = FindControllable(bookmarkName, cachedEntityId, out entityWithRCIdentifier);
                if (controllable == null && entityWithRCIdentifier != null)
                    bookmarkModifications[bookmarkName] = entityWithRCIdentifier.net.ID;
            }

            // Performing modifications outside of the above foreach to avoid InvalidOperationException errors.
            foreach (var entry in bookmarkModifications)
                computerStation.controlBookmarks[entry.Key] = entry.Value;
        }

        #endregion

        #region Helper Methods

        private BaseEntity FindControllable(string bookmarkName, uint cachedEntityId, out BaseEntity entityWithRCIdentifier)
        {
            entityWithRCIdentifier = null;

            foreach (var controllable in RemoteControlEntity.allControllables)
            {
                var entity = controllable.GetEnt();
                if (entity == null)
                    continue;

                if (entity.net.ID == cachedEntityId)
                    return entity;

                if (ReferenceEquals(entityWithRCIdentifier, null) && controllable.GetIdentifier() == bookmarkName)
                    entityWithRCIdentifier = entity;
            }

            return null;
        }

        #endregion
    }
}
