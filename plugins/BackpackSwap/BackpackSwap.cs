namespace Oxide.Plugins
{
    [Info("BackpackSwap", "Whispers88", "1.0.1")]
    [Description("Allows you to swap your backpacks without moving items manually")]
    public class BackpackSwap : CovalencePlugin
    {
        #region Hooks

        private object CanWearItem(PlayerInventory inventory, Item targetItem, int slot)
        {
            if (inventory == null || targetItem == null || !targetItem.IsBackpack() || slot != ItemContainer.BackpackSlotIndex) return null;
            Item currentitem = inventory.containerWear.GetSlot(slot);
            if (currentitem == null || !currentitem.IsBackpack()) return null;
            if (currentitem.contents.itemList.Count < 1 || currentitem.contents.itemList.Count > targetItem.contents.capacity) return null;

            targetItem.RemoveFromContainer();
            currentitem.RemoveFromContainer();

            targetItem.position = ItemContainer.BackpackSlotIndex;
            targetItem.SetParent(inventory.containerWear);

            for (int i = currentitem.contents.itemList.Count - 1; i >= 0; i--)
            {
                var item2move = currentitem.contents.itemList[i];
                if (item2move == null) continue;
                item2move.MoveToContainer(targetItem.contents);
            }

            inventory.GiveItem(currentitem);               

            return false;
        }

        #endregion Hooks

    }
}