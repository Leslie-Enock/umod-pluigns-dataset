using System.Collections.Generic;
using Facepunch;
using Rust;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
        [Info("IgniterPlus", "Bran", "0.0.5")]
        [Description("Configure electrical igniters")]
    public class IgniterPlus : RustPlugin
    {
        List<Igniter> igniters = new List<Igniter>();

        int counter = 0;
        private void OnServerInitialized()
        {
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<Igniter>())
            {
                counter++;
                OnEntitySpawned(entity);
            }

            Puts($"OnServerInitialized {counter} igniters were processed.");
        }



        private void OnEntitySpawned(Igniter entity)
        {

            entity.SelfDamagePerIgnite = configData.Options.SelfDamagePerIgnite;
            entity.IgniteRange = configData.Options.IgniteRange;
            entity.IgniteFrequency = configData.Options.IgniteFrequency;
            entity.PowerConsumption = configData.Options.PowerConsumption;
            if (configData.Options.Power)
            {
                entity.UpdateHasPower(25, 1);
            }
        }

        private void Unload()
        {
            foreach (var entity in igniters)
            {
                entity.SelfDamagePerIgnite = 0.5f;
                entity.IgniteRange = 5f;
                entity.IgniteFrequency = 1f;
                entity.PowerConsumption = 2;
                entity.UpdateHasPower(0, 1);
            }
        }

        #region Config
        void Init()
        {
            LoadConfigVariables();
        }

        private ConfigData configData;
        class ConfigData
        {
            public Options Options = new Options();
        }

        class Options
        {
            
            public float SelfDamagePerIgnite = 0.5f;
            public float IgniteRange = 5f;
            public float IgniteFrequency = 1f;
            public int PowerConsumption = 2;
            public bool Power = false;
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
            SaveConfig(configData);
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            var config = new ConfigData();
            SaveConfig(config);
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion
    }

}