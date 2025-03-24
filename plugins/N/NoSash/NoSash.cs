using System.Linq;

namespace Oxide.Plugins
{
    [Info("No Sash", "Wulf", "1.0.2")]
    [Description("Blocks sashes from showing on players")]
    public class NoSash : CovalencePlugin
    {
        private void OnServerInitialized()
        {
            foreach (BasePlayer player in BaseNetworkable.serverEntities.OfType<BasePlayer>())
            {
                OnPlayerSpawn(player);
            }
        }

        private void Unload()
        {
            foreach (BasePlayer player in BaseNetworkable.serverEntities.OfType<BasePlayer>())
            {
                if (player?.inventory != null)
                {
                    player.inventory.containerBelt.onItemAddedRemoved -= OnItemAddedRemoved;
                    player.inventory.containerMain.onItemAddedRemoved -= OnItemAddedRemoved;
                    player.inventory.containerWear.onItemAddedRemoved -= OnItemAddedRemoved;
                }
            }
        }

        private void OnPlayerSpawn(BasePlayer player)
        {
            NextFrame(() =>
            {
                if (player?.inventory != null)
                {
                    player.inventory.containerBelt.onItemAddedRemoved += OnItemAddedRemoved;
                    player.inventory.containerMain.onItemAddedRemoved += OnItemAddedRemoved;
                    player.inventory.containerWear.onItemAddedRemoved += OnItemAddedRemoved;
                }
            });
        }

        private void OnItemAddedRemoved(Item item, bool added)
        {
            BasePlayer player = item?.parent?.playerOwner;
            if (player != null)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.DisplaySash, false);
            }
        }
    }
}
