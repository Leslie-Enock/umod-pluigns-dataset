using Newtonsoft.Json;
using System;
using System.Collections.Generic;
namespace Oxide.Plugins
{
    [Info("Recycle Modifier", "birthdates", "1.0.3")]
    [Description("Ability to change the output of the recycler")]
    public class RecycleModifier : RustPlugin
    {
        private ConfigFile config;

        public class ConfigFile
        {
            [JsonProperty(PropertyName = "Blacklisted items (wont get the modifier)")]
            public List<string> bAP;

            [JsonProperty(PropertyName = "Modifier")]
            public float mod;



            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    mod = 2f,
                    bAP = new List<string>()
                    {
                        "rock",
                        "locker"
                    }

                };
            }

        }


        void OnRecycleItem(Recycler recycler, Item item)
        {

            if (config.bAP.Contains(item.info.shortname))
            {
                return;
            }

            recycler.inventory.Remove(item);
            foreach (var i in item.info.Blueprint.ingredients)
            {
                var z = ItemManager.CreateByPartialName(i.itemDef.shortname, Convert.ToInt32(Convert.ToInt32(i.amount / 2) * config.mod - (int)i.amount / 2));
                if(z == null) continue;
                recycler.MoveItemToOutput(z);

            }


        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            config = Config.ReadObject<ConfigFile>();
            if (config == null)
            {
                LoadDefaultConfig();
            }
        }


        protected override void LoadDefaultConfig()
        {
            config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"NoPermission", "You don't have any permission."},
            }, this);
        }



        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private void Init()
        {
            LoadConfig();
        }



    }
}