namespace Oxide.Plugins
{
    [Info("Usable to Belt", "Wulf", "1.2.2")]
    [Description("Any usable item will be moved to your belt if there is space")]
    public class UsableToBelt : RustPlugin
    {
        private const string permAllow = "usabletobelt.allow";

        private void Init() => permission.RegisterPermission(permAllow, this);

        private void HandleItem(Item item, BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permAllow))
            {
                return;
            }

            bool alreadyHasStack = false;
            ItemContainer belt = player.inventory.containerBelt;
            ItemContainer main = player.inventory.containerMain;

            foreach (Item invItem in main.itemList)
            {
                if (item.info.itemid == invItem.info.itemid)
                {
                    if (invItem.info.stackable > 1)
                    {
                        alreadyHasStack = true;
                    }
                }
            }

            if (alreadyHasStack)
            {
                return;
            }

            if (item.info.category != ItemCategory.Weapon && item.info.category != ItemCategory.Tool &&
                item.info.category != ItemCategory.Medical && item.info.category != ItemCategory.Food &&
                item.info.category != ItemCategory.Construction)
            {
                return;
            }

            for (int i = 0; i < PlayerBelt.MaxBeltSlots; i++)
            {
                if (!belt.SlotTaken(item, i))
                {
                    timer.Once(0.1f, () => item.MoveToContainer(belt, i));
                    break;
                }

                continue;
            }
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            BasePlayer player = container.GetOwnerPlayer();
            if (player != null && !container.HasFlag(ItemContainer.Flag.Belt))
            {
                HandleItem(item, player);
            }
        }
    }
}
