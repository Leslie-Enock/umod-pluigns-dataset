using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Perfect Repair", "The Friendly Chap", "1.1.2")]
    [Description("Items will be fully repaired, removing the permanent penalty (red bar)")]
    public class PerfectRepair : RustPlugin
	
/*	MIT License

	©2024 The Friendly Chap

	Permission is hereby granted, free of charge, to any person obtaining a copy
	of this software and associated documentation files (the "Software"), to deal
	in the Software without restriction, including without limitation the rights
	to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
	copies of the Software, and to permit persons to whom the Software is
	furnished to do so, subject to the following conditions:

	The above copyright notice and this permission notice shall be included in all
	copies or substantial portions of the Software.

	THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
	IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
	FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
	AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
	LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
	OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
	SOFTWARE.
*/	
	
    {
        #region Oxide Hooks

        // Called when the server is fully initialized
        private void OnServerInitialized()
        {
            // Iterate over all item definitions in the game
            foreach (var def in ItemManager.itemList)
            {
                // Set maintainMaxCondition to true to ensure items maintain maximum condition
                def.condition.maintainMaxCondition = true;
            }
            
            // Print a warning to the server log about potential lag
            PrintWarning("All containers will be checked, it can cause small lag");

            // Start the coroutine to check and repair items in all containers
            ServerMgr.Instance.StartCoroutine(CheckContainers());
			// ShowLogo();
        }

        // Coroutine to check and repair items in all containers
        private IEnumerator CheckContainers()
        {
            // Iterate over all network entities on the server
            foreach (var bEntity in BaseNetworkable.serverEntities)
            {
                // Check if the entity is a storage container
                var container = bEntity.GetComponent<StorageContainer>();
                if (container != null)
                {
                    // Iterate over all items in the container's inventory
                    foreach (var item in container.inventory.itemList ?? new List<Item>())
                    {
                        // If the item has a condition, set its max condition and mark it as dirty
                        if (item.hasCondition)
                        {
                            item._maxCondition = item.info.condition.max;
                            item.MarkDirty();
                        }
                    }

                    // Wait for the next frame to avoid lag spikes
                    yield return new WaitForEndOfFrame();
                }

                // Check if the entity is a player
                var player = bEntity.GetComponent<BasePlayer>();
                if (player != null)
                {
                    // Iterate over all items in the player's inventory
                    foreach (var item in player.inventory.AllItems())
                    {
                        // If the item has a condition, set its max condition and mark it as dirty
                        if (item.hasCondition)
                        {
                            item._maxCondition = item.info.condition.max;
                            item.MarkDirty();
                        }
                    }

                    // Wait for the next frame to avoid lag spikes
                    yield return new WaitForEndOfFrame();
                }

                // Check if the entity is a lootable corpse
                var corpse = bEntity.GetComponent<LootableCorpse>();
                if (corpse != null)
                {
                    // Iterate over all items in the corpse's containers
                    foreach (var item in corpse.containers.SelectMany(x => x.itemList))
                    {
                        // If the item has a condition, set its max condition and mark it as dirty
                        if (item.hasCondition)
                        {
                            item._maxCondition = item.info.condition.max;
                            item.MarkDirty();
                        }
                    }

                    // Wait for the next frame to avoid lag spikes
                    yield return new WaitForEndOfFrame();
                }

                // Check if the entity is a dropped item
                var droppedItem = bEntity.GetComponent<DroppedItem>();
                if (droppedItem != null)
                {
                    // If the dropped item has a condition, set its max condition
                    if (droppedItem.item.hasCondition)
                    {
                        droppedItem.item._maxCondition = droppedItem.item.info.condition.max;
                    }

                    // Wait for the next frame to avoid lag spikes
                    yield return new WaitForEndOfFrame();
                }

                // Check if the entity is a dropped item container
                var droppedContainer = bEntity.GetComponent<DroppedItemContainer>();
                if (droppedContainer != null)
                {
                    // Iterate over all items in the dropped container's inventory
                    foreach (var item in droppedContainer.inventory.itemList)
                    {
                        // If the item has a condition, set its max condition and mark it as dirty
                        if (item.hasCondition)
                        {
                            item._maxCondition = item.info.condition.max;
                            item.MarkDirty();
                        }
                    }

                    // Wait for the next frame to avoid lag spikes
                    yield return new WaitForEndOfFrame();
                }

                // Yield at the end of each entity to avoid lag spikes
                yield return new WaitForEndOfFrame();
            }
        }
        		private void ShowLogo()
        {
			Puts(" _______ __               _______        __                 __ __             ______ __           ©2024");
			Puts("|_     _|  |--.-----.    |    ___|.----.|__|.-----.-----.--|  |  |.--.--.    |      |  |--.---.-.-----.");
			Puts("  |   | |     |  -__|    |    ___||   _||  ||  -__|     |  _  |  ||  |  |    |   ---|     |  _  |  _  |");
			Puts("  |___| |__|__|_____|    |___|    |__|  |__||_____|__|__|_____|__||___  |    |______|__|__|___._|   __|");
			Puts("                         Perfect Repair v1.1.1                    |_____| thefriendlychap.co.za |__|");      
        }  
        #endregion
    }
}
