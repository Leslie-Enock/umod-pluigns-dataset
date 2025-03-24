using System;
using System.Collections.Generic;

using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Vending Machine Timeout", "0x89A", "1.0.15")]
    [Description("Prevents players from hogging vending machines at outpost")]
    class VendingMachineTimeout : RustPlugin
    {
        private Dictionary<ulong, Timer> timeoutTimers = new Dictionary<ulong, Timer>();

        private const string bypass = "vendingmachinetimeout.bypass";

        private void Init() => permission.RegisterPermission(bypass, this);
        
        void OnOpenVendingShop(NPCVendingMachine machine, BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, bypass))
            {
                if (!timeoutTimers.ContainsKey(player.userID)) timeoutTimers.Add(player.userID, null);

                timeoutTimers[player.userID] = timer.Once(config.timeoutSeconds, () => { if (player != null) player.EndLooting(); });
            }
        }

        void OnLootEntityEnd(BasePlayer player, NPCVendingMachine entity)
        {
            Timer timer;
            if (!timeoutTimers.ContainsKey(player.userID) || !timeoutTimers.TryGetValue(player.userID, out timer) || timer == null) return;

            timer.Destroy();
        }

        #region -Configuration-

        private Configuration config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Timeout time (seconds)")]
            public float timeoutSeconds = 30f;
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
                PrintError("Error loading config, using default values");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion
    }
}
