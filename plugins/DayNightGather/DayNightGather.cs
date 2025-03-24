// Requires: GatherManager

using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Day Night Gather", "klauz24", "1.1.1"), Description("Sets different gather rates for day and night.")]
    internal class DayNightGather : RustPlugin
    {
        [PluginReference] readonly Plugin TimeOfDay;

        private bool _isDay;

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Scale dispensers")]
            public bool ScaleDispensers = true;

            [JsonProperty(PropertyName = "Chat announcements")]
            public bool ChatAnnouncements = true;

            [JsonProperty(PropertyName = "Time check interval (Only used if TimeOfDay plugin is not installed)")]
            public int TimeCheckInterval = 60;

            [JsonProperty(PropertyName = "Day")]
            public Values Day = new Values();

            [JsonProperty(PropertyName = "Night")]
            public Values Night = new Values();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        private class Values
        {
            public int Dispenser = 1;
            public int Pickup = 1;
            public int Quarry = 1;
            public int Excavator = 1;
            public int Survey = 1;
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
                    Puts("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                Puts($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Announcement", "The server rates have changed due to time change! To know more, type <color=#008000ff>/gather</color>."}
            }, this);
        }

        private void OnServerInitialized()
        {
            if (TimeOfDay == null)
            {
                timer.Every(_config.TimeCheckInterval, () => IsDayOrNight());
            }
            _isDay = IsDay;
            SetRates(_isDay);
        }

        private void OnTimeSunrise() => OnTimeChange(_config.Day.Dispenser, _config.Day.Pickup, _config.Day.Quarry, _config.Day.Excavator, _config.Day.Survey, true);

        private void OnTimeSunset() => OnTimeChange(_config.Night.Dispenser, _config.Night.Pickup, _config.Night.Quarry, _config.Night.Excavator, _config.Night.Survey, false);

        private void OnTimeChange(int dispenser, int pickup, int quarry, int excavator, int survey, bool boolean)
        {
            Server.Command($"gather.rate dispenser * {dispenser}");
            Server.Command($"gather.rate pickup * {pickup}");
            Server.Command($"gather.rate quarry * {quarry}");
            Server.Command($"gather.rate excavator * {excavator}");
            Server.Command($"gather.rate survey * {survey}");
            if (_config.ScaleDispensers)
            {
                Server.Command($"dispenser.scale tree {dispenser}");
                Server.Command($"dispenser.scale ore {dispenser}");
                Server.Command($"dispenser.scale corpse {dispenser}");
            }
            if (_config.ChatAnnouncements)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    var message = lang.GetMessage("Announcement", this, player.UserIDString);
                    player.ChatMessage(message);
                }
            }
            _isDay = boolean;
        }

        private void SetRates(bool boolean)
        {
            if (boolean)
            {
                OnTimeSunrise();
            }
            else
            {
                OnTimeSunset();
            }
        }

        private void IsDayOrNight()
        {
            if ((IsDay && _isDay) || (!IsDay && !_isDay))
            {
                return;
            }
            if (IsDay)
            {
                SetRates(true);
            }
            else
            {
                SetRates(false);
            }
        }

        private bool IsDay => TOD_Sky.Instance.IsDay;
    }
}
