using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Oxide.Plugins
{
    [Info("Limit RCON", "Wulf", "1.0.0")]
    [Description("Limits RCON access to specific IP addresses")]
    class LimitRCON : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Allow local IP addresses")]
            public bool AllowLocal = true;

            [JsonProperty("List of allowed IP addresses to allow", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> IpAddresses = new List<string> { "1.1.1.1", "8.8.8.8" };

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

        #region IP Handling

        private object OnRconConnection(IPAddress ipAddress)
        {
            if (config.AllowLocal && IsLocalIp(ipAddress.ToString()))
            {
                Log($"{ipAddress} is a local IP address, allowing connection to RCON");
                return null;
            }

            if (!config.IpAddresses.Contains(ipAddress.ToString()))
            {
                Log($"{ipAddress} is not allowed to connect to RCON, denying");
                return true;
            }

            return null;
        }

        private static bool IsLocalIp(string ipAddress)
        {
            string[] split = ipAddress.Split(new[] { "." }, StringSplitOptions.RemoveEmptyEntries);
            int[] ip = new[] { int.Parse(split[0]), int.Parse(split[1]), int.Parse(split[2]), int.Parse(split[3]) };
            return ip[0] == 10 || ip[0] == 127 || (ip[0] == 192 && ip[1] == 168) || (ip[0] == 172 && (ip[1] >= 16 && ip[1] <= 31));
        }

        #endregion IP Handling
    }
}
