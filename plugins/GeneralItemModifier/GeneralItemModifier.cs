using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("General Item Modifier", "Rick", "1.0.1")]
    [Description("Modifying global item parameters such as display name, icon")]
    public class GeneralItemModifier : RustPlugin
    {
        #region Oxide Hooks

        private void OnServerInitialized()
        {
            foreach (var definition in ItemManager.itemList)
            {
                var entry = config.items.FirstOrDefault(x => x.shortname == definition.shortname);
                if (entry != null)
                {
                    var mods = definition.itemMods.Where(x => !(x is ItemModStatsChanger));
                    var array = mods.Concat(new[] { ItemModStatsChanger.New(entry) }).ToArray();
                    definition.itemMods = array;
                }
            }
        }

        private void Unload()
        {
            foreach (var definition in ItemManager.itemList)
            {
                var entry = config.items.FirstOrDefault(x => x.shortname == definition.shortname);
                if (entry != null)
                {
                    var mods = definition.itemMods.Where(x => !(x is ItemModStatsChanger));
                    var array = mods.ToArray();
                    definition.itemMods = array;
                }
            }
        }

        #endregion

        #region Core

        private class ItemModStatsChanger : ItemMod
        {
            public static ItemModStatsChanger New(ItemEntry _entry)
            {
                var obj = new GameObject().AddComponent<ItemModStatsChanger>();
                obj.entry = _entry;
                return obj;
            }

            private ItemEntry entry;

            public override void OnItemCreated(Item item)
            {
                if (item.name == null && item.skin == 0)
                {
                    item.name = entry.displayName;
                    item.skin = entry.skinId;
                    item.MarkDirty();

                    var e = item.GetHeldEntity();

                    if (e.IsValid()) e.skinID = entry.skinId;
                }
            }
        }

        #endregion

        #region

        private static ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Values")]
            public ItemEntry[] items = new[]
            {
                new ItemEntry
                {
                    shortname = "scientistsuit_heavy",
                    skinId = 0,
                    displayName = "!! HEAVY SUIT !!"
                },
                new ItemEntry
                {
                    shortname = "rifle.ak",
                    skinId = 0,
                    displayName = "TOP GU"
                }
            };
        }

        private class ItemEntry
        {
            [JsonProperty(PropertyName = "Shortname")]
            public string shortname;

            [JsonProperty(PropertyName = "Display Name")]
            public string displayName;

            [JsonProperty(PropertyName = "New Icon (Skin)")]
            public ulong skinId;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                for (var i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupted!");
                }

                LoadDefaultConfig();
                return;
            }

            ValidateConfig();
            SaveConfig();
        }

        private void ValidateConfig()
        {
            if (Interface.Oxide.CallHook("OnConfigValidate") != null)
            {
                PrintWarning("Using default configuration...");
                config = new ConfigData();
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion
    }
}