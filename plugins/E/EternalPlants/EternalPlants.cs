using System;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Eternal Plants", "0x89A", "1.0.1")]
    [Description("Plants do not die after they are full grown")]
    class EternalPlants : RustPlugin
    {
        private const string _usePerm = "eternalplants.use";
        
        private void Init()
        {
            permission.RegisterPermission(_usePerm, this);
        }

        private object OnGrowableStateChange(GrowableEntity entity, PlantProperties.State state)
        {
            // (entity.harvests < entity.Properties.maxHarvests) - so plant dies when gathered.
            if (HasPermission(entity.OwnerID) && state == PlantProperties.State.Dying && entity.harvests < entity.Properties.maxHarvests)
            {
                entity.stageAge = entity.currentStage.lifeLengthSeconds;
                entity.Heal(100f);
                return true;
            }

            return null;
        }

        private bool HasPermission(ulong id)
        {
            return (_config.eternalOwnerless && id == 0) || permission.UserHasPermission(id.ToString(), _usePerm);
        }

        #region -Configuration-

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty("Ownerless plants don't die")]
            public bool eternalOwnerless = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Failed to load config, using default values");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
    }
}
