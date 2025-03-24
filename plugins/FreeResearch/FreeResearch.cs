using System.Linq;						// Where

namespace Oxide.Plugins
{
	[Info("Free Research", "Zugzwang", "1.0.11")]
	[Description("Free blueprint unlocking from research tables and workbench tech trees.")]		

	class FreeResearch : RustPlugin
	{
		const int ScrapID = -932201673;
		const int MaxResearchScrap = 500;
		
		#region Startup and Unload

		// Load scrap in ResearchTables and enable instant results.
		void OnEntitySpawned(ResearchTable entity)
		{
			entity.pickup.requireEmptyInv = false;
			entity.researchDuration = 0;
			
			Item i = entity.inventory.FindItemByItemID(ScrapID);
			if (i != null)
			{
				i.amount = MaxResearchScrap;
			}
			else
			{
				i = ItemManager.CreateByItemID(ScrapID, MaxResearchScrap);
				if (i != null && !i.MoveToContainer(entity.inventory, 1))
				{
					i.Remove();
				}
			}
		}
		
		// Setup all ResearchTables on Load.
		void OnServerInitialized()
		{
			foreach (ResearchTable entity in BaseNetworkable.serverEntities.Where(x => x is ResearchTable))
			{
				OnEntitySpawned(entity);
			}
		}
		
		// Revert all ResearchTables on Unload.
		void Unload()
		{
			foreach (ResearchTable entity in BaseNetworkable.serverEntities.Where(x => x is ResearchTable))
			{
				entity.pickup.requireEmptyInv = true;
				entity.researchDuration = 10f;
				entity.inventory.Remove(entity.inventory.GetSlot(1));
			}
		}
		
      #endregion Startup and Unload		

		#region Cost Control and TechTree Override
		
		// Allow unlocking all TechTree items unconditionally.
		object CanUnlockTechTreeNode(BasePlayer player, TechTreeData.NodeInstance node, TechTreeData techTree)
		{
			return true;
		}
		
		// Set research cost to zero.
		object OnResearchCostDetermine()
      {
			return (int)0;
		}
		
		#endregion Cost Control and TechTree Override

		#region ResearchTable Inventory Control
		
		// Prevent player from adding more scrap to a ResearchTable.
		object CanAcceptItem(ItemContainer container, Item item, int targetPos)
		{
			if (	container?.entityOwner is not ResearchTable ||
					item?.info?.itemid != ScrapID ||
					targetPos != 1 ||
					container.GetSlot(targetPos) == null
				) return null;
				
			return ItemContainer.CanAcceptResult.CannotAccept;
		}		
		
		// Prevent player from moving scrap in or out of a ResearchTable.
		object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainerId, int targetSlot, int num, bool flag)
		{
			if (item?.info?.itemid != ScrapID) return null;
			if (item?.parent?.entityOwner is ResearchTable) return false;
			if (playerLoot?.FindContainer(targetContainerId)?.entityOwner is ResearchTable) return false;
			return null;
		}
		
		// Prevent player from stealing scrap by throwing the whole stack on the ground.
		void OnItemRemovedFromContainer(ItemContainer container, Item item)
		{
			if (item?.info?.itemid == ScrapID && container?.entityOwner is ResearchTable)
			{
				NextFrame(() => { item.MoveToContainer(container, 1, true); });
			}
		}		

		// Prevent player from stealing scrap by throwing a partial stack on the ground.		
		Item OnItemSplit(Item item, int amount)
		{
			if (item?.parent?.entityOwner is ResearchTable && item.position == 1) return item;
			return null;
		}		
		
		#endregion ResearchTable Inventory Control
		
		#region ResearchTable Pickup

		// Prevent player from stealing scrap when picking up a ResearchTable.
		object CanPickupEntity(BasePlayer player, ResearchTable entity)
		{
			if (player.CanBuild() && player.IsHoldingEntity<Hammer>())
			{
				Item i = entity.inventory.GetSlot(0);
				if (i != null && !i.MoveToContainer(player.inventory.containerMain))
				{
					i.Drop(player.inventory.containerMain.dropPosition, player.inventory.containerMain.dropVelocity, new UnityEngine.Quaternion());
				}
				entity.inventory.Clear();
				return true;
			}
			return null;
		}	
		
		#endregion ResearchTable Pickup 		
		
	}
}