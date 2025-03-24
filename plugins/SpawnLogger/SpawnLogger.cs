using System.Linq;
using System;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using UnityEngine.Assertions.Must;

namespace Oxide.Plugins
{
    [Info("Spawn Logger", "un-boxing-man & Lincoln", "1.0.8")]
    [Description("Logs all player spawned items to a file.")]
    public class SpawnLogger : RustPlugin
    {
        #region config
        //Creating a config file
        private static PluginConfig config;
        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Excluded Logging  ")] public List<string> LoggingExcludeList { get; set; }


            public static PluginConfig DefaultConfig() => new PluginConfig()
            {
                LoggingExcludeList = new List<string>()
                    {
                        ""
                    }
            };

        }   
        protected override void LoadDefaultConfig()
        {
            PrintWarning("New configuration file created!!");
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
            SaveConfig();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }
        #endregion

        #region loging
        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity == null) return;
            var player = covalence.Players.FindPlayerById(entity.OwnerID.ToString());
            if (player == null ) return;
            var pos = $"({entity.transform.position.x} {entity.transform.position.y} {entity.transform.position.z})";
            if (config.LoggingExcludeList.Contains(entity.ShortPrefabName)) return;
            Log("Entity_Spawned", player.Name, player.Id, entity.ShortPrefabName, pos);
        }
        private void Log(string filename, string player, string id, string entity, string pos)
        {
            LogToFile(filename, $"[{DateTime.Now}] {player}[{id}]: {entity} {pos}", this);
        }
        #endregion

        // Credits Lincoln, For the config and more.
    }
}
