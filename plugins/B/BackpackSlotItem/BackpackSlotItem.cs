// MIT License
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Backpack Slot Item", "Lorenzo", "1.0.2")]
    [Description("Modify item allowed in backpack slot")]
    class BackpackSlotItem : CovalencePlugin
    {
        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Items allowed in Backpack slot", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> ItemsForBackpackSlot = new List<string> { "diving.tank" };
        };

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion Configuration


        private Dictionary<string, BackupInfo> backupinfo = new Dictionary<string, BackupInfo>();

        private struct BackupInfo {
            public ItemDefinition.Flag flag;
            public Wearable.OccupationSlots occupationOver;
        }

        #region Hooks
        
        private void OnServerInitialized()
        {
            foreach (string  name in _config.ItemsForBackpackSlot)
            {
                ItemDefinition item = ItemManager.FindDefinitionByPartialName(name);
                ItemModWearable component = item?.GetComponent<ItemModWearable>();

                if (item != null && component !=null && name != "parachute")
                {
                    BackupInfo info;
                    if (!backupinfo.TryGetValue(name, out info))
                    {
                        info = new BackupInfo();
                        info.flag = item.flags;
                        info.occupationOver = component.targetWearable.occupationOver;

                        // update flags
                        item.flags |= ItemDefinition.Flag.Backpack | ItemDefinition.Flag.NotAllowedInBelt;
                        component.targetWearable.occupationOver = Wearable.OccupationSlots.Back;
                        backupinfo.Add(name, info);
                    }
                }
            }
        }

        private void Unload()
        {
            foreach (var KVP in backupinfo)
            {
                ItemDefinition item = ItemManager.FindDefinitionByPartialName(KVP.Key);
                ItemModWearable component = item?.GetComponent<ItemModWearable>();

                if (item != null && component!=null && KVP.Key != "parachute")
                {
                    item.flags = KVP.Value.flag;
                    component.targetWearable.occupationOver  = KVP.Value.occupationOver;
                }
            }
        }
        #endregion Hooks

    }
}