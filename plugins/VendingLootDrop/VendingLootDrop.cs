using Newtonsoft.Json;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Vending Loot Drop", "Bazz3l", "1.0.6")]
    [Description("Drops vending machine contents when destroyed.")]
    class VendingLootDrop : RustPlugin
    {
        #region Fields

        private PluginConfig _config;

        #endregion

        #region Config

        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new JsonException();
                }
            }
            catch
            {
                PrintError("Loaded default config.");

                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        class PluginConfig
        {
            [JsonProperty(PropertyName = "Drop chance (percents, 1f = 100%)")]
            public float DropChance;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    DropChance = 0.8f
                };
            }
        }

        #endregion

        #region Oxide

        void OnServerInitialized()
        {
            Subscribe(nameof(OnEntitySpawned));

            foreach (VendingMachine machine in BaseNetworkable.serverEntities.OfType<VendingMachine>())
            {
                OnEntitySpawned(machine);
            }
        }

        void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
        }

        void OnEntitySpawned(VendingMachine machine) => machine.dropsLoot = UnityEngine.Random.value <= _config.DropChance;

        #endregion
    }
}