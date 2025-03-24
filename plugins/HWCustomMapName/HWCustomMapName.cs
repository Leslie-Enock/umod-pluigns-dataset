using System;
using Steamworks;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("HW Custom Map Name", "klauz24", "1.5.3"), Description("Changes map name at the server list.")]
    internal class HWCustomMapName : CovalencePlugin
    {
        private DateTime _nextWipe;

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty("Map name")]
            public string MapName = "by klauz24";

            [JsonProperty("Random map name")]
            public RandomMapName RMN = new RandomMapName();

            [JsonProperty("Wipe countdown")]
            public WipeCountdown WPC = new WipeCountdown();

            public class RandomMapName
            {
                [JsonProperty("Enable random map name")]
                public bool EnableRandomMapName = false;

                [JsonProperty(PropertyName = "Random map names")]
                public List<string> RandomMapNames { get; set; } = new List<string>
                {
                    "by",
                    "klauz24"
                };
            }

            public class WipeCountdown
            {
                [JsonProperty("Enable wipe countdown")]
                public bool EnableWipeCountdown = false;

                [JsonProperty("Wipe countdown format")]
                public string WipeCountdownFormat = "Wipe in: {0}";

                [JsonProperty("Next wipe year")]
                public int NextWipeYear = 2020;

                [JsonProperty("Next wipe month")]
                public int NextWipeMonth = 7;

                [JsonProperty("Next wipe day")]
                public int NextWipeDay = 12;

                [JsonProperty("Next wipe hour")]
                public int NextWipeHours = 11;

                [JsonProperty("Next wipe minutes")]
                public int NextWipeMinutes = 0;
            }

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    throw new JsonException();
                }

                if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            PrintWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Days", "{0}d. {1}h."},
                {"Hours", "{0}h. {1}m."},
                {"Minutes", "{0}m. {1}s."},
                {"Seconds", "{0}s."}
            }, this);
        }

        private void OnServerInitialized()
        {
            _nextWipe = new DateTime(_config.WPC.NextWipeYear, _config.WPC.NextWipeMonth, _config.WPC.NextWipeDay, _config.WPC.NextWipeHours, _config.WPC.NextWipeMinutes, 00);
            timer.Every(1f, () =>
            {
                var str = GetMapName();
                UpdateMapName(str);
            });
        }

        private string GetMapName()
        {
            if (_config.RMN.EnableRandomMapName && _config.WPC.EnableWipeCountdown)
            {
                PrintWarning("Random map name and wipe countdown are enabled, please choose one of them or disable both.");
                return "Error, check console.";
            }
            if (_config.RMN.EnableRandomMapName)
            {
                var randomEntry = _config.RMN.RandomMapNames[new Random().Next(0, _config.RMN.RandomMapNames.Count)];
                return randomEntry;
            }
            if (_config.WPC.EnableWipeCountdown)
            {
                var time = (_nextWipe - DateTime.Now).TotalSeconds;
                return string.Format(_config.WPC.WipeCountdownFormat, FormatTime(time));
            }
            return _config.MapName;
        }

        private string FormatTime(double time)
        {
            var timeSpan = TimeSpan.FromSeconds(time);
            if (timeSpan.TotalSeconds < 1)
            {
                return null;
            }
            if (Math.Floor(timeSpan.TotalDays) >= 1)
            {
                return string.Format(Lang("Days"), timeSpan.Days, timeSpan.Hours);
            }
            if (Math.Floor(timeSpan.TotalMinutes) >= 60)
            {
                return string.Format(Lang("Hours"), timeSpan.Hours, timeSpan.Minutes);
            }
            if (Math.Floor(timeSpan.TotalSeconds) >= 60)
            {
                return string.Format(Lang("Minutes"), timeSpan.Minutes, timeSpan.Seconds);
            }
            return string.Format(Lang("Seconds"), timeSpan.Seconds);
        }

        private string Lang(string key) => lang.GetMessage(key, this);

        private void UpdateMapName(string str)
        {
#if HURTWORLD
            SteamGameServer.SetMapName(str);
#endif
#if RUST
            SteamServer.MapName = str;
#endif
        }
    }
}