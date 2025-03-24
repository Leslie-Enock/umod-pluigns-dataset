using Newtonsoft.Json;
using JetBrains.Annotations;

namespace Oxide.Plugins
{
    [Info("Key Lock Raiding", "birthdates", "1.0.2")]
    [Description("Bring back key lock guessing!")]
    public class KeyLockRaiding : RustPlugin
    {
        #region Hooks

        /// <summary>
        ///     When the server initializes, reset all key locks that have a bigger combination than <see cref="ConfigFile.MaxCombinations"/>
        /// </summary>
        [UsedImplicitly]
        private void OnServerInitialized()
        {
            var count = -1;
            var list = BaseNetworkable.serverEntities.entityList.Values;
            while (count++ < list.Count)
            {
                var keyLock = list[count] as KeyLock;
                if (keyLock == null || keyLock.keyCode < _config.MaxCombinations) continue;
                OnItemDeployed(null, null, keyLock);
            }
        }
        
        /// <summary>
        ///     When a <see cref="KeyLock"/> is deployed, reset it's combination to a random number between 0 & <see cref="ConfigFile.MaxCombinations"/>
        /// </summary>
        /// <param name="deployer">Person who placed the lock</param>
        /// <param name="entity">Parent</param>
        /// <param name="keyLock">Key lock</param>
        private void OnItemDeployed(Deployer deployer, BaseEntity entity, KeyLock keyLock)
        {
            keyLock.keyCode = Core.Random.Range(_config.MaxCombinations);
        }

        #endregion
        
        #region Configuration

        private ConfigFile _config;

        public class ConfigFile
        {
            [JsonProperty("Max Possible Combinations (Rust default is 100000)")]
            public int MaxCombinations { get; set; }
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    MaxCombinations = 100
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null)
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion
    }
}