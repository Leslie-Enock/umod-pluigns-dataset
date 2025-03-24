using System.Collections.Generic;
using UnityEngine;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Stack Fuel Generator", "ninco90", "1.0.1")]
    [Description("Change the maximum fuel capacity.")]
    class StackFuelGenerator : RustPlugin
    {
        private ConfigData config;

        private class ConfigData
        {
            public int StackMax { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData
            {
                StackMax = 1000
            };
            Config.WriteObject(config, true);
        }

        private void Init()
        {
            config = Config.ReadObject<ConfigData>();
        }

        void OnServerInitialized(){
            foreach(var generator in GameObject.FindObjectsOfType<FuelGenerator>()){
               generator.inventory.maxStackSize = config.StackMax;
            }
        }

        private void OnEntitySpawned(FuelGenerator entity){
             entity.inventory.maxStackSize = config.StackMax;
        }
    }
}