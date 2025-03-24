using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Fps Override", "MJSU", "0.0.1")]
    [Description("Overrides the server target fps")]
    internal class FpsOverride : RustPlugin
    {
        #region Class Fields
        private PluginConfig _pluginConfig; //Plugin Config
        #endregion

        #region Setup & Loading
        private void Init()
        {
            ConfigLoad();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        private void ConfigLoad()
        {
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig config)
        {
            config.Fps = config.Fps == 0 ? Application.targetFrameRate : config.Fps;
            return config;
        }

        private void OnServerInitialized()
        {
            Application.targetFrameRate = _pluginConfig.Fps;
        }
        #endregion

        #region uMod Hook
        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            string command = $"{arg.cmd.Parent}.{arg.cmd.Name}";
            if (command == "fps.limit" && arg.Args != null && arg.Args.Length != 0)
            {
                int target = arg.GetInt(0);
                if (target > 0)
                {
                    Application.targetFrameRate = target;
                    _pluginConfig.Fps = target;
                    Interface.Oxide.LogInfo($"fps.limit: \"{target}\"");
                    Config.WriteObject(_pluginConfig);
                    return true;
                }
            }
            
            return null;
        }
        #endregion

        #region Classes
        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Server Fps Target")]
            public int Fps { get; set; }
        }
        #endregion
    }
}
