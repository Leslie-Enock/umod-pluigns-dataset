using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Animal Home", "Krungh Crow", "1.0.3")]
    [Description("Adding a animal roam radius")]
    public class AnimalHome : RustPlugin
    {
    /**********************************************************************
     * 
     * v1.0.3   :   Added a additional check when animal reached home location
     * 
     **********************************************************************/
        public static AnimalHome instance;

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            OnStart();
        }

        private void Unload()
        {
            OnEnd();
        }
        
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            AddLogic(entity);
        }

        #endregion

        #region Helpers

        private void OnStart()
        {
            instance = this;

            foreach (var entity in UnityEngine.Object.FindObjectsOfType<BaseEntity>())
            {
                AddLogic(entity);
            }
        }

        private void OnEnd()
        {
            foreach (var logic in UnityEngine.Object.FindObjectsOfType<OLogic>().ToList())
            {
                logic.DoDestroy();
            }
        }

        private void AddLogic(BaseNetworkable entity)
        {
            var npc = entity.GetComponent<BaseNpc>();
            if (npc == null) {return;}
            if (config.blocked.Contains(entity.ShortPrefabName)) {return;}
            if (entity.GetComponent<OLogic>() != null) {return;}
            entity.gameObject.AddComponent<OLogic>();
            //Puts($"Added logic to : {entity}");
        }

        #endregion

        #region Config

        private static ConfigData config;
        
        private class ConfigData
        {    
            [JsonProperty(PropertyName = "Time between checks")]
            public float timer;
            
            [JsonProperty(PropertyName = "Max distance between Home and NPC")]
            public float distance;
            
            [JsonProperty(PropertyName = "Blocked NPC types (logic will not work for them)")]
            public List<string> blocked;
        }
        
        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                timer = 30f,
                distance = 50f,
                blocked = new List<string>
                {
                    "example.name",
                    "example.name",
                    "example.name"
                }
            };
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
   
            try
            {
                config = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region MonoBehaviour

        private class OLogic: MonoBehaviour
        {
            private Vector3 home;
            private BaseNpc npc;
            private float distance;
            private float time;

            private void Awake()
            {
                npc = GetComponent<BaseNpc>();
                home = npc.transform.position;
                distance = config.distance;
                time = config.timer;
                
                InvokeRepeating("CheckDistance", 1f, time);
            }

            private void CheckDistance()
            {
                if (npc == null || npc.IsDestroyed) return;

                if (Vector3.Distance(home, npc.transform.position) < 1)
                {
                    //instance.Puts($"{npc} is at home spawn point {home}");
                    return;
                }

                if (Vector3.Distance(home, npc.transform.position) > distance)
                {
                    npc.UpdateDestination(home);
                    //instance.Puts($"Sending {npc} home {home}");
                }
            }

            public void DoDestroy()
            {
                Destroy(this);
            }

            private void OnDestroy()
            {
                CancelInvoke("CheckDistance");
            }
        }

        #endregion
    }
}