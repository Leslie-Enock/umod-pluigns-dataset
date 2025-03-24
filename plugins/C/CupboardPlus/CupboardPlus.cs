using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Cupboard Plus", "Mevent", "1.0.0")]
    [Description("Destroys the home and runs commands after cupboard destroying")]
    public class CupboardPlus : RustPlugin
    {
        #region Config

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Commands List", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Commands = new List<string>
            {
                "cmd 1 {ownerId}",
                "cmd 2"
            };

            [JsonProperty(PropertyName = "Destroy the home?")]
            public bool HomeDestroy = true;
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
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion
        
        #region Hooks
        
        private void OnEntityDeath(BuildingPrivlidge cupboard, HitInfo info)
        {
            if (cupboard == null) return;
            
            if (_config.HomeDestroy) 
                ServerMgr.Instance.StartCoroutine(DestroyEntities(cupboard.children));
            
            _config.Commands.ForEach(command =>
            {
                command = command.Replace("{ownerId}", cupboard.OwnerID.ToString());
                if (string.IsNullOrEmpty(command)) return;
                
                Server.Command(command);
            });
        }

        #endregion

        #region Utils

        private IEnumerator DestroyEntities(List<BaseEntity> entities)
        {
            var i = 0;
            foreach (var entity in entities)
            {
                if (entity != null && !entity.IsDestroyed)
                    entity.Kill();

                if (i++ == 10)
                {
                    i = 0;
                    yield return CoroutineEx.waitForFixedUpdate;
                }
            }
        }

        #endregion
    }
}