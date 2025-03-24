using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Auto Deposit", "DC", "1.0.1")]
    [Description("Simply adds items to containers if that container already has that item!")]
    class AutoDeposit : RustPlugin
    {

        #region Variables

        List<BasePlayer> playersSort = new List<BasePlayer>();

        #endregion

        #region Commands

        private void CommandDeposit(BasePlayer player, string commands, string[] args)
        {
            //Checks to see if player has permissions.
            if (!permission.UserHasPermission(player.UserIDString, "autodeposit.use"))
            {
                player.ChatMessage($"{lang.GetMessage("NoPermission", this)}");
                return;
            }

            //Check if player is currently sorting if they are disable the sorting or enable.
            if (!playersSort.Contains(player))
            {
                playersSort.Add(player);
                

                if (config.useTimer)
                {
                    player.ChatMessage($"{lang.GetMessage("DepositActiveTimer", this)}");

                    timer.Once(config.autoDisableTime, () =>
                    {
                        playersSort.Remove(player);
                        player.ChatMessage($"{lang.GetMessage("DepositDeactivated", this)}");
                    });
                }
                else
                {
                    player.ChatMessage($"{lang.GetMessage("DepositActiveNoTimer", this)}");
                }
            }
            else
            {
                playersSort.Remove(player);
                player.ChatMessage($"{lang.GetMessage("DepositDeactivated", this)}");
            }
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = $"You don't have permission to use this command.",
                ["DepositActiveNoTimer"] = $"AutoDeposit active, use /{config.depositCommand} to deactivate.",
                ["DepositActiveTimer"] = $"AutoDeposit active, auto disabling in {config.autoDisableTime} seconds.",
                ["DepositDeactivated"] = $"AutoDeposit deactivated."
            }, this);
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            cmd.AddChatCommand(config.depositCommand, this, CommandDeposit);
            permission.RegisterPermission("autodeposit.use", this);
        }

        /*
        Does a simple check on if an entity can be looted if it can it then checks if its on the config block list of containers.
        After it will do a simple compare of the items in both containers.
        If they match it will check if the item is on the config block list of items. If it isn't it will move the item.
        */

        private void OnLootEntity(BasePlayer player, StorageContainer entity)
        {

            if (player == null || entity == null)
                return;

            if (!playersSort.Contains(player))
                return;

            //Checks to see if player has access to storage
            if (entity.CanBeLooted(player))
            {
                //Compares the items and moves them if they can, also checks if container is ignored by config or playerData
                if (config.allowedContainers.Contains(entity.ShortPrefabName))
                {

                    ItemContainer pContainer = player.inventory.containerMain;
                    ItemContainer eContainer = entity.inventory;

                    List<Item> toMove = CompareItemContainers(pContainer, eContainer);

                    foreach (Item item in toMove)
                    {
                        string itemName = item.info.shortname;

                        if (!config.disallowedItemNames.Contains(itemName))
                        {
                            item.MoveToContainer(eContainer);
                        }
                    }
                }
            }
        }

        #endregion

        #region Helpers

        private List<Item> CompareItemContainers(ItemContainer fContainer, ItemContainer sContainer)
        {
            List<Item> itemsToMove = new List<Item>();

            if (fContainer != null && sContainer != null)
            {
                foreach (Item pItem in fContainer.itemList)
                {
                    foreach (Item sItem in sContainer.itemList)
                    {
                        if (pItem.info.itemid != sItem.info.itemid)
                        {
                            continue;
                        }

                        itemsToMove.Add(pItem);
                        break;
                    }
                }
            }
            return itemsToMove;
        }

        #endregion

        #region Config

        private Configuration config;

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();
            }
            catch { }

            if (config == null)
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        public class Configuration
        {
            [JsonProperty(PropertyName = "Allowed Containers")]
            public string[] allowedContainers = new string[]
            {
                "box.wooden.large",
                "woodbox_deployed",
                "coffinstorage",
                "vendingmachine.deployed",
                "dropbox.deployed",
                "cupboard.tool.deployed",
                "fridge.deployed"
            };

            [JsonProperty(PropertyName = "Disallowed Items")]
            public string[] disallowedItemNames = new string[]
            {

            };

            [JsonProperty(PropertyName = "Use Timer")]
            public bool useTimer = true;

            [JsonProperty(PropertyName = "Autodisable Time")]
            public float autoDisableTime = 10f;

            [JsonProperty(PropertyName = "Deposit Command")]
            public string depositCommand = "depo";

        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultConfig() => config = new Configuration();

        #endregion

    }
}