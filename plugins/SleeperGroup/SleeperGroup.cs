using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Sleeper Group", "Wulf", "1.0.0")]
    [Description("Puts players in a permissions group on disconnect if sleeping")]
    class SleeperGroup : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Sleeper group name")]
            public string SleeperGroup = "sleeper";

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            LogWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        private void Init()
        {
            if (!permission.GroupExists(config.SleeperGroup))
            {
                permission.CreateGroup(config.SleeperGroup, config.SleeperGroup, 0);
            }
        }

        private void OnUserConnected(IPlayer player)
        {
            if (player.BelongsToGroup(config.SleeperGroup))
            {
                player.RemoveFromGroup(config.SleeperGroup);
            }
        }

        private void OnUserDisconnected(IPlayer player)
        {
            if (player.IsSleeping && !player.BelongsToGroup(config.SleeperGroup))
            {
                player.AddToGroup(config.SleeperGroup);
            }
        }
    }
}
