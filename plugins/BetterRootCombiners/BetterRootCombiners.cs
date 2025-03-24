namespace Oxide.Plugins
{
    [Info("Better Root Combiners", "WhiteThunder", "1.0.1")]
    [Description("Allows root combiners to accept input from any electrical source.")]
    internal class BetterRootCombiners : CovalencePlugin
    {
        private const string PermissionUse = "betterrootcombiners.use";
        private const string RootCombinerPrefab = "assets/prefabs/deployable/playerioents/gates/combiner/electrical.combiner.deployed.prefab";
        private uint RootCombinerPrefabId;

        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            RootCombinerPrefabId = GameManager.server.FindPrefab(RootCombinerPrefab)
                ?.GetComponent<ElectricalCombiner>()?.prefabID ?? 0;

            if (RootCombinerPrefabId == 0)
            {
                LogError($"Unable to determine prefabID of {RootCombinerPrefab}");
                return;
            }

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var rootCombiner = entity as ElectricalCombiner;
                if (rootCombiner == null)
                    continue;

                OnEntitySpawned(rootCombiner);
            }

            Subscribe(nameof(OnEntitySpawned));
        }

        private void OnEntitySpawned(ElectricalCombiner combiner)
        {
            if (combiner.prefabID != RootCombinerPrefabId)
                return;

            var rootConnectionsOnly = combiner.OwnerID == 0
                || !permission.UserHasPermission(combiner.OwnerID.ToString(), PermissionUse);

            foreach (var input in combiner.inputs)
            {
                input.rootConnectionsOnly = rootConnectionsOnly;
            }
        }
    }
}
