using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Perma Map", "Wulf", "1.0.8")]
    [Description("Makes sure that players always have access to a map")]
    public class PermaMap : CovalencePlugin
    {
        #region Initialization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UnableToCraft"] = "You already have a map hidden in your inventory! Press your map button to use it"
            }, this);
        }

        private const float checkTime = 5f;
        private const string permUse = "permamap.use";

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
        }

        #endregion Initialization

        #region Crafting Block

        private object CanCraft(ItemCrafter itemCrafter, ItemBlueprint blueprint)
        {
            if (blueprint.name != "map.item")
            {
                return null;
            }

            BasePlayer player = itemCrafter.containers[0]?.GetOwnerPlayer();
            if (player == null)
            {
                return false;
            }

            if (permission.UserHasPermission(player.UserIDString, permUse))
            {
                player.ChatMessage(GetLang("UnableToCraft", player.UserIDString));
                return false;
            }

            return null;
        }

        #endregion Crafting Block

        #region Permament Map

        private void AddMap(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, permUse))
            {
                return;
            }

            player.inventory.containerBelt.capacity = 7;
            if (player.inventory.containerBelt.GetSlot(6) != null)
            {
                return;
            }

            Item item = ItemManager.CreateByItemID(107868, 1);
            if (item != null)
            {
                item.MoveToContainer(player.inventory.containerBelt, 6);
            }
        }

        private void RemoveMap(BasePlayer player)
        {
            Item item = player.inventory.containerBelt.GetSlot(6);
            if (item != null)
            {
                item.RemoveFromContainer();
                item.Remove();
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            timer.Once(checkTime, () =>
            {
                if (player.IsReceivingSnapshot || player.IsSleeping())
                {
                    OnPlayerConnected(player);
                    return;
                }

                AddMap(player);
            });
        }

        private void OnPlayerDeath(BasePlayer player) => RemoveMap(player);

        private void OnPlayerRespawned(BasePlayer player) => AddMap(player);

        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (item.info.itemid != 107868)
            {
                return;
            }

            BasePlayer player = container.GetOwnerPlayer();
            if (player == null)
            {
                return;
            }

            if (container == player.inventory.containerBelt)
            {
                NextTick(() =>
                {
                    if (container.GetSlot(6) != null)
                    {
                        Item unknownItem = container.GetSlot(6);
                        if (unknownItem.info.itemid == 107868)
                        {
                            return;
                        }

                        if (!player.inventory.containerMain.IsFull())
                        {
                            unknownItem.MoveToContainer(player.inventory.containerMain);
                        }
                        else
                        {
                            unknownItem.Drop(player.transform.position, Vector3.down);
                        }
                    }
                    item.MoveToContainer(container, 6);
                });
            }
        }

        #endregion Permament Map

        #region Event Hooks

        private void JoinedEvent(BasePlayer player)
        {
            timer.Once(checkTime, () =>
            {
                if (player.IsSleeping())
                {
                    JoinedEvent(player);
                }
                else
                {
                    RemoveMap(player);
                }
            });
        }

        private void LeftEvent(BasePlayer player) => AddMap(player);

        #endregion Event Hooks

        #region Helpers

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }

        #endregion Helpers
    }
}
