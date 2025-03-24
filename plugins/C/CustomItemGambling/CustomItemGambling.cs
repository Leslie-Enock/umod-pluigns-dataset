using System.Collections.Generic;
using System;

namespace Oxide.Plugins
{
    [Info("Custom Item Gambling", "Pro Noob (patched by chrome)", "1.0.2")]
    [Description("Allows putting items other than scrap into the scrap wheel at Bandit Camp")]
    class CustomItemGambling : CovalencePlugin
    {
        #region Fields
        private const string usePermission = "customitemgambling.use";

        private ConfigData configData;
        private Dictionary<ItemContainer, ItemDefinition[]> originalAllowedItems = new Dictionary<ItemContainer, ItemDefinition[]>();
        private List<string> itemWhitelist;
        private bool allowAnyItem;
        #endregion

        #region Config
        private class ConfigData
        {
            public List<string> ItemWhitelist = new List<string>();
            public bool AllowAnyItem;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                ItemWhitelist = new List<string> { "scrap" },
                AllowAnyItem = false
            };
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Config not found or unreadable, generating new config file");
            Config.WriteObject(GetDefaultConfig(), true);
        }
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            permission.RegisterPermission(usePermission, this);
            configData = Config.ReadObject<ConfigData>();
            itemWhitelist = configData.ItemWhitelist;
            allowAnyItem = configData.AllowAnyItem;
        }

        // Resetting allowed items lists back to their original values
        void Unload() => ResetContainer();

        private object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainerID, int targetSlot, int amount)
        {
            var basePlayer = item?.GetOwnerPlayer();

            if (basePlayer != null)
            {
                if (!basePlayer.IsNpc)
                {
                    ItemContainer targetContainer = playerLoot.FindContainer(targetContainerID);
                    BigWheelBettingTerminal bigWheelBettingTerminal;

                    try
                    {
                        bigWheelBettingTerminal = (BigWheelBettingTerminal)targetContainer?.entityOwner;
                    }
                    catch (Exception)
                    {
                        return null;
                    }

                    if (bigWheelBettingTerminal != null)
                    {
                        // Reset container allowed items and let the game handle the rest
                        if (!basePlayer.IPlayer.HasPermission(usePermission))
                        {
                            ResetContainer(targetContainer);
                            return null;
                        }

                        if (!itemWhitelist.Contains(item.info.shortname) && !allowAnyItem)
                            return false;

                        // Prevent mixing items since weird stuff happens if you do
                        if (targetContainer.itemList.Count > 0)
                        {
                            foreach (Item containerItem in targetContainer.itemList)
                            {
                                if (containerItem.info.shortname != item.info.shortname)
                                {
                                    return false;
                                }
                            }
                        }

                        if (!targetContainer.onlyAllowedItems.Contains(item.info))
                        {
                            if (!originalAllowedItems.ContainsKey(targetContainer))
                            {
                                originalAllowedItems.Add(targetContainer, targetContainer.onlyAllowedItems);
                            }

                            ItemDefinition[] allowedItems = new ItemDefinition[targetContainer.onlyAllowedItems.Length + 1];

                            // Add current item to allowed items list
                            for (int i = 0; i < allowedItems.Length - 1; i++)
                            {
                                allowedItems[i] = targetContainer.onlyAllowedItems[i];
                            }

                            allowedItems[allowedItems.Length - 1] = item.info;
                            targetContainer.SetOnlyAllowedItems(allowedItems);
                        }
                    }
                }
            }

            return null;
        }
        #endregion

        #region Methods
        private void ResetContainer(ItemContainer itemContainer = null)
        {
            if (itemContainer == null)
            {
                foreach (var keyValuePair in originalAllowedItems)
                {
                    keyValuePair.Key.SetOnlyAllowedItems(keyValuePair.Value);
                }
            }
            else
            {
                ItemDefinition[] itemDefinitions;
                originalAllowedItems.TryGetValue(itemContainer, out itemDefinitions);
                if (itemDefinitions != null)
                {
                    itemContainer.SetOnlyAllowedItems(itemDefinitions);
                }
            }
        }
        #endregion
    }
}