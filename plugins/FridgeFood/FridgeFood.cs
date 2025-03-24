using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FridgeFood", "sami37", "1.1.3")]
    [Description("Prevent food from being placed in box instead of fridge.")]
    public class FridgeFood : RustPlugin
    {
        [PluginReference] Plugin Backpacks;

        private List<object> listFood = new List<object>();
        private List<object> listContainer = new List<object>();
        List<object> defaultLists = new List<object>();

        private List<object> unallowedContainer = new List<object>
        {
            "box.wooden.large",
            "box.wooden",
            "coffin.storage",
            "small.stash",
            "locker",
            "smallbackpack",
            "largebackpack"
        };

        private List<ulong> AdminDebug = new List<ulong>();

        #region ConfigFunction
        string ListToString<T>(List<T> list, int first = 0, string seperator = ", ") => string.Join(seperator, (from val in list select val.ToString()).Skip(first).ToArray());
        void SetConfig(params object[] args) { List<string> stringArgs = (from arg in args select arg.ToString()).ToList(); stringArgs.RemoveAt(args.Length - 1); if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args); }
        T GetConfig<T>(T defaultVal, params object[] args) { List<string> stringArgs = (from arg in args select arg.ToString()).ToList(); if (Config.Get(stringArgs.ToArray()) == null) { PrintError($"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin."); return defaultVal; } return (T)System.Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T)); }
        #endregion

        private static BasePlayer GetPlayerFromContainer(ItemContainer container, Item item) =>
            item.GetOwnerPlayer() ??
            BasePlayer.activePlayerList.FirstOrDefault(
                p => p.inventory.loot.IsLooting() && p.inventory.loot.entitySource == container.entityOwner);

        private static ItemContainer GetRootContainer(Item item)
        {
            var container = item.parent;
            if (container == null)
                return null;

            while (container.parent?.parent != null && container.parent != item)
            {
                container = container.parent.parent;
            }

            return container;
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            var rootContainer = GetRootContainer(item) ?? container;
            var backpacksOwnerResult = Backpacks?.Call("API_GetBackpackOwnerId", rootContainer);
            if (backpacksOwnerResult is ulong && (ulong)backpacksOwnerResult > 0)
                return;

            if (container.parent?.parent != null && container.parent.parent.playerOwner != null)
            {
                var player = container.parent.parent.playerOwner;

                if (player != null)
                {
                    var name = container.parent.info.shortname.Replace("_deployed", "")
                        .Replace(".deployed", "")
                        .Replace("_", ".");

                    foreach (var cont in listContainer)
                    {
                        if (name == cont.ToString())
                        {
                            if (listFood.Contains(item.info.itemid) || listFood.Contains(item.info.shortname))
                            {
                                SendReply(player, lang.GetMessage("NotAllowedBackpack", this, player.UserIDString));
                                player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                                break;
                            }
                        }
                    }

                    if (AdminDebug.Contains(player.userID))
                        SendReply(player, string.Format(lang.GetMessage("ContainerName", this, player.UserIDString), name));
                }
            }

            if (container.playerOwner != null && container.playerOwner.inventory != null)
            {
                return;
            }

            if (container.entityOwner != null)
            {
                var player = GetPlayerFromContainer(container, item);
                {
                    var name = container.entityOwner.ShortPrefabName.Replace("_deployed", "")
                        .Replace(".deployed", "")
                        .Replace("_", ".");

                    foreach (var cont in listContainer)
                    {
                        if (container.entityOwner.ShortPrefabName.Contains("coffin"))
                        {
                            if (listFood.Contains(item.info.itemid) || listFood.Contains(item.info.shortname))
                            {
                                if (player != null)
                                {
                                    SendReply(player, lang.GetMessage("NotAllowed", this, player.UserIDString));
                                    player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                                }
                                else
                                {
                                    var dropPos = container.GetEntityOwner().ServerPosition;
                                    var newDrop = new Vector3(dropPos.x, dropPos.y + 1, dropPos.z);

                                    item.Drop(newDrop, Vector3.up);
                                }

                                break;
                            }
                        }
                        if (name == cont.ToString())
                        {
                            if (listFood.Contains(item.info.itemid) || listFood.Contains(item.info.shortname))
                            {
                                if (player != null)
                                {
                                    SendReply(player, lang.GetMessage("NotAllowed", this, player.UserIDString));
                                    player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
                                }
                                else
                                {
                                    var dropPos = container.GetEntityOwner().ServerPosition;
                                    var newDrop = new Vector3(dropPos.x, dropPos.y+1, dropPos.z);

                                    item.Drop(newDrop, Vector3.up);
                                }

                                break;
                            }
                        }
                    }

                    if (player != null)
                        if (AdminDebug.Contains(player.userID))
                        SendReply(player, string.Format(lang.GetMessage("ContainerName", this, player.UserIDString), name));
                }
            }
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();

            defaultLists = new List<object>(ItemManager.GetItemDefinitions().Where(itemDef => itemDef.category == ItemCategory.Food).Select(itemDef => itemDef.shortname).ToList());

            foreach (var item in defaultLists)
                if (!listFood.Contains(item))
                    listFood.Add(item);
            SetConfig("Food List", defaultLists);
            SetConfig("Unallowed container", unallowedContainer);
            SaveConfig();
        }

        private void OnServerInitialized()
        {
            listFood = GetConfig(defaultLists, "Food List");
            listContainer = GetConfig(unallowedContainer, "Unallowed container");

            SaveConfig();

            lang.RegisterMessages(
                new Dictionary<string, string>
                {
                    {"NotAllowed", "You are not allowed to put food in normal box."},
                    {"NotAllowedBackpack", "You are not allowed to put food in backpack."},
                    {"NoRight", "You don't have permission."},
                    {"Disabled", "Disabled debug system."},
                    {"Enabled", "Enabled debug system."},
                    {"ContainerName", "The name of this container is <color=red>{0}</color>."}
                },
                this);
        }

        [ChatCommand("fdebug")]
        void chatCommand(BasePlayer player, string cmd, string[] args)
        {
            if (player.IsAdmin)
            {
                if (AdminDebug != null)
                {
                    if (AdminDebug.Contains(player.userID))
                    {
                        AdminDebug.Remove(player.userID);
                        SendReply(player, lang.GetMessage("Disabled", this, player.UserIDString));
                    }
                    else
                    {
                        AdminDebug.Add(player.userID);
                        SendReply(player, lang.GetMessage("Enabled", this, player.UserIDString));
                    }
                }
            }
            else
            {
                SendReply(player, lang.GetMessage("NoRight", this, player.UserIDString));
            }
        }
    }
}