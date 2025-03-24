using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Hemp Daddy", "TacoSauce", "1.0.1")]
    [Description("Modifies gather rate of hemp plants when planted in planters.")]
    class HempDaddy : RustPlugin
    {
        private void OnGrowableGathered(GrowableEntity growable, Item item, BasePlayer player)
        {
            if(item.info.displayName.english == "Cloth")
            {   item.amount = (int)(item.amount * config.modifier);
            }
        }
        private confData config;
        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new confData(),true);
        }
        private void Init()
        {
            config = Config.ReadObject<confData>();
        }
        private new void SaveConfig()
        {
            Config.WriteObject(config,true);
        }
        public class confData
        {
            [JsonProperty("Cloth Mulitplier")]
            public int modifier = 1;
        }
    }
}