using System.ComponentModel;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Instant Experiment", "MJSU", "1.0.2")]
    [Description("Allows players to instantly experiment")]
    internal class InstantExperiment : RustPlugin
    {
        #region Class Fields
        private PluginConfig _pluginConfig; //Plugin Config

        private const string UsePermission = "instantexperiment.use";
        #endregion

        #region Setup & Loading
        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
        }
        
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_pluginConfig);
        }
        #endregion

        #region Oxide Hook
        private void OnExperimentStarted(Workbench workbench, BasePlayer player)
        {
            if (!HasPermission(player, UsePermission))
            {
                return;
            }
            
            workbench.CancelInvoke(workbench.ExperimentComplete);
            workbench.Invoke(workbench.ExperimentComplete, _pluginConfig.ExperimentTime);
            workbench.SendNetworkUpdate();
        }
        #endregion

        #region Helper Methods
        private bool HasPermission(BasePlayer player, string perm) => permission.UserHasPermission(player.UserIDString, perm);
        #endregion

        #region Classes
        private class PluginConfig
        {
            [DefaultValue(0f)]
            [JsonProperty(PropertyName = "Experiment Time (Seconds)")]
            public float ExperimentTime { get; set; }
        }
        #endregion
    }
}
