using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("ContainerControl", "Hamster", "1.0.1")]
    [Description("Allows you to restrict types of items which can be put in a certain kind of containers")]
    class ContainerControl : RustPlugin
    {

        #region Config
        private PluginConfig config;
        private struct PluginConfig
        {
            [JsonProperty("Containers")]
            public Dictionary<string, ContainerEntry> Containers { get; set; }
        }
        
        private struct ContainerEntry
        {
            [JsonProperty("Allow")]
            public bool Allow { get; set; }
            [JsonProperty("Items")]
            public List<string> ListItem { get; set; }
        }
        
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Сreate a new configuration file");
            config = new PluginConfig()
            {
                Containers = new Dictionary<string, ContainerEntry>
                {
                    ["cupboard.tool.deployed"] = new ContainerEntry
                    {
                        Allow = false,
                        ListItem = new List<string>
                        {
                            "cloth",
                            "scrap",
                            "sulfur",
                            "sulfur.ore",
                            "charcoal",
                            "hq.metal.ore",
                            "fat.animal",
                            "leather",
                            "crude.oil",
                            "gunpowder",
                            "metal.ore",
                            "lowgradefuel"
                        }
                    },
                    ["campfire"] = new ContainerEntry
                    {
                        Allow = true,
                        ListItem = new List<string>
                        {
                            "wood",
                            "charcoal",
                            "horsemeat.raw",
                            "bearmeat",
                            "deermeat.raw",
                            "fish.raw",
                            "meat.boar",
                            "wolfmeat.raw",
                            "chicken.raw",
                            "humanmeat.raw",
                            "can.tuna.empty",
                            "can.beans.empty"
                        }
                    }
                }
            };
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Oxide hook

        private ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            if (container == null || item == null) return null;
            BaseEntity baseEntity = container.entityOwner;
            if (baseEntity == null) return null;
            if (baseEntity.OwnerID <= 76560000000000000L) return null;
            ContainerEntry values;
            if (!config.Containers.TryGetValue(baseEntity.ShortPrefabName, out values)) return null;
            if (values.Allow)
            {
                return values.ListItem.Contains(item.info.shortname) ? ItemContainer.CanAcceptResult.CanAccept : ItemContainer.CanAcceptResult.CannotAccept;
            }
            else
            {
                if (values.ListItem.Contains(item.info.shortname)) return ItemContainer.CanAcceptResult.CannotAccept;
                return null;
            }
        }

        #endregion

    }
}
