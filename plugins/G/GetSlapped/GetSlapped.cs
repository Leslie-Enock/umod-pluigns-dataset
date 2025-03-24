// Requires: Slap

using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Get Slapped", "klauz24", "1.0.3"), Description("Slaps players when they try to act tough in chat.")]
    internal class GetSlapped : CovalencePlugin
    {
        [PluginReference] private readonly Plugin Slap;

        private const string _slapBypass = "getslapped.bypass";

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Banned word, slap force (30 = 30% of health)")]
            public Dictionary<string, float> BannedWords = new Dictionary<string, float>()
            {
                {"Other word", 30f},
                {"Penalty word", 30f}
            };
        }

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
                SaveConfig();
            }
            catch
            {
                PrintWarning("Could not load a valid configuration file, creating a new configuration file.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void LoadDefaultMessages() => lang.RegisterMessages(new Dictionary<string, string>() { { "Message", "HEEY! Saying '{0}' is not cool, take this slap as a lesson." } }, this);

        private void Init() => permission.RegisterPermission(_slapBypass, this);

        private void OnUserChat(IPlayer player, string message)
        {
            if (!permission.UserHasPermission(player.Id, _slapBypass))
            {
                foreach (var kvp in _config.BannedWords)
                {
                    if (message.Contains(kvp.Key))
                    {
                        SlapPlayer(player, kvp.Value, kvp.Key);
                        break;
                    }
                }
            }
        }

        private void SlapPlayer(IPlayer player, float value, string message)
        {
            player.Message(string.Format(lang.GetMessage("Message", this, player.Id), message));
            Slap?.Call("SlapPlayer", player, value);
        }
    }
}
