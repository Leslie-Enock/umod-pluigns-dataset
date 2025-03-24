using System;
using System.Collections.Generic;

using UnityEngine;
using System.Linq;

using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Attachment Blocker", "0x89A", "1.0.1")]
    [Description("Block application of specific attachments per weapon")]
    class AttachmentBlocker : RustPlugin
    {
        private Configuration config;

        private const string bypassPerm = "attachmentblocker.bypass";

        void Init() => permission.RegisterPermission(bypassPerm, this);

        ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            Item parentItem = container.parent;

            if (parentItem != null && config.blockedItems.ContainsKey(parentItem.info.shortname))
            {
                BasePlayer player = parentItem.GetOwnerPlayer() ?? item.GetOwnerPlayer();
                if (player != null && permission.UserHasPermission(player.UserIDString, bypassPerm)) return null;

                List<string> list = config.blockedItems[parentItem.info.shortname];

                if (list.Contains(item.info.itemid.ToString()) || list.Contains(item.info.shortname)) 
                    return ItemContainer.CanAcceptResult.CannotAccept;
            }

            return null;
        }

        #region -Configuration-

        private class Configuration
        {
            [JsonProperty(PropertyName = "Blocked Items")]
            public Dictionary<string, List<string>> blockedItems = new Dictionary<string, List<string>>();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Failed to load config, using default values");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration();

            foreach (ItemDefinition itemdef in ItemManager.GetItemDefinitions())
            {
                if (HasInventory(itemdef)) config.blockedItems.Add(itemdef.shortname, new List<string>());
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        private bool HasInventory(ItemDefinition itemdef)
        {
            foreach (ItemMod mod in itemdef.itemMods)
            {
                if ((mod as ItemModContainer)?.availableSlots.Count > 0) return true;
            }

            return false;
        }

        #endregion
    }
}
