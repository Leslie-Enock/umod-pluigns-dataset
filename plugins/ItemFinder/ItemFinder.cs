using Oxide.Core.Libraries.Covalence;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using System;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Item Finder", "Camoec", 1.3)]
    [Description("Get count of specific item in the server")]

    public class ItemFinder : CovalencePlugin
    {
        private const string Perm = "itemfinder.use";
        private DateTime lastCommand = new DateTime();

        private PluginConfig _config;
        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Cooldown (is seconds)")]
            public int Cooldown = 5;
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);
        protected override void LoadDefaultConfig()
        {
            _config = new PluginConfig();
            SaveConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                    throw new Exception();

                SaveConfig(); // override posible obsolet / outdated config
            }
            catch (Exception)
            {
                PrintError("Loaded default config.");

                LoadDefaultConfig();
            }
        }

        private void Init()
        {
            permission.RegisterPermission(Perm, this);
        }

        private class ItemsInfo
        {
            public string shortname;
            public int itemId;

            public int droppedCount;
            public int dropped;
            public int inPlayers;
            public int inCointaners;

            public int totalCount => droppedCount + inPlayers + inCointaners;
        }

        [Command("itemfinder")]
        private void GetActiveEnts(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(Perm))
            {
                player.Reply(Lang("NoPermission", player.Id));
                return;
            }

            if (args.Length == 0)
            {
                player.Reply(Lang("InvalidSyntax", player.Id));
                return;
            }

            if(DateTime.Now - lastCommand < TimeSpan.FromSeconds(_config.Cooldown))
            {
                player.Reply(string.Format(Lang("Cooldown", player.Id), _config.Cooldown - (int)(DateTime.Now - lastCommand).TotalSeconds));
                return;
            }

            lastCommand = DateTime.Now;

            var info = GetInfo(args[0]);

            if (info == null)
            {
                player.Reply(string.Format(Lang("NotFound", player.Id), info.shortname));
                return;
            }

            player.Reply(string.Format(Lang("Found", player.Id), info.itemId, info.dropped, info.inCointaners, info.inPlayers, info.totalCount));
        }

        private int? GetItemId(string shortname) => ItemManager.FindItemDefinition(shortname)?.itemid;
        private ItemsInfo GetInfo(string shortname)
        {
            ItemsInfo info = new ItemsInfo();
            info.shortname = shortname;
            int? itemid = GetItemId(shortname);
            info.itemId = itemid != null ? itemid.Value : -1;

            ItemDefinition itemDef = null;
            for (int i = 0; i <  ItemManager.itemList.Count;i++)
            {
                if (ItemManager.itemList[i].shortname == shortname)
                    itemDef = ItemManager.itemList[i];
            }

            if(itemDef == null)
            {
                // item not found
                return null;
            }

            // Get in players inventory
            foreach(BasePlayer player in BasePlayer.allPlayerList)
            {
                if (player == null)
                    continue;

                player.inventory.containerMain.itemList.ForEach((item) =>
                {
                    if (item.info.itemid == info.itemId)
                        info.inPlayers += item.amount;
                });
                player.inventory.containerBelt.itemList.ForEach((item) =>
                {
                    if (item.info.itemid == info.itemId)
                        info.inPlayers += item.amount;
                });
                player.inventory.containerWear.itemList.ForEach((item) =>
                {
                    if (item.info.itemid == info.itemId)
                        info.inPlayers += item.amount;
                });
            }

            // Get in AllCointainers
            foreach(var entity in BaseNetworkable.serverEntities)
            {
                var droppedItem = entity as DroppedItem;
                if(droppedItem != null)
                {
                    var item = droppedItem.GetItem();
                    if (item.info.itemid == info.itemId)
                    {
                        info.dropped += item.amount;
                    }
                    continue;
                }

                var container = entity as StorageContainer;
                if (container == null || container is LootContainer)
                    continue;

                var foundItems = container.inventory.FindItemsByItemID(info.itemId);
                foundItems.ForEach(item =>
                {
                    info.inCointaners += item.amount;
                });
            }

            return info;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["InvalidSyntax"] = "Use <color=#eb4213>/itemfinder</color> [item shortname]",
                ["NotFound"] = "Item '{0}' not found",
                ["Found"] = "<color=#eb4213>ItemInfo:</color>\r\nItemId:{0}\r\nDropped:{1}\r\nInContainers:{2}\r\ninPlayers:{3}\r\nTotal:{4}",
                ["NoPermission"] = "You not have permission to use this command",
                ["Cooldown"] = "You need to wait {0} seconds to use this command"
            }, this);
        }

        private string Lang(string key, string userid) => lang.GetMessage(key, this, userid);

    }
}