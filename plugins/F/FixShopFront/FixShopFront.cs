namespace Oxide.Plugins
{
    [Info("Fix Shop Front", "Mevent", "1.0.2")]
    [Description("Fixed a bug with managing other people's weapon modifications when exchanging")]
    public class FixShopFront : CovalencePlugin
    {
        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item == null || player == null || action != "drop") return null;

            var shopFront = item.GetRootContainer()?.entityOwner as ShopFront;
            if (shopFront == null) return null;

            var parentItem = item.parentItem;
            if (parentItem == null) return null;

            if (shopFront.customerInventory.itemList.Contains(parentItem))
            {
                if (shopFront.customerPlayer != player)
                    return true;
            }
            else
            {
                if (shopFront.vendorPlayer != player)
                    return true;
            }

            return null;
        }

        private object CanMoveItem(Item item, PlayerInventory inventory, uint targetContainer, int targetSlot,
            int amount)
        {
            var player = inventory.GetComponent<BasePlayer>();
            if (player == null)
                return null;

            var shopFront = item.GetRootContainer()?.entityOwner as ShopFront;
            if (shopFront != null)
            {
                var parentItem = item.parentItem;
                if (parentItem == null) return null;

                if (shopFront.customerInventory.itemList.Contains(parentItem))
                {
                    if (shopFront.customerPlayer != player)
                        return true;
                }
                else
                {
                    if (shopFront.vendorPlayer != player)
                        return true;
                }

                return null;
            }

            var container = inventory.FindContainer(targetContainer);
            if (container != null)
            {
                var parentItem = container.parent;
                if (parentItem == null) return null;

                var front = parentItem.GetRootContainer().entityOwner as ShopFront;
                if (front == null) return null;

                if (front.customerInventory.itemList.Contains(parentItem))
                {
                    if (front.customerPlayer != player) return true;
                }
                else
                {
                    if (front.vendorPlayer != player)
                        return true;
                }

                return null;
            }

            return null;
        }
    }
}