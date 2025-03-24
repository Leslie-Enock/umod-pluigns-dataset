using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Loot Spawnpoints", "Orange", "1.0.0")]
    [Description("Add extra loot spawnpoints on custom maps")]
    public class LootSpawnpoints : RustPlugin
    {
        #region Oxide Hooks

        private void Init()
        {
            cmd.AddConsoleCommand("spawnpoints.get", this, "cmdGetSpawnpointsConsole");
        }
        
        private void OnServerInitialized()
        {
            LoadData();
            CreateSpawnpoints();
        }

        private void Unload()
        {
            UnityEngine.Object.FindObjectsOfType<Spawner>().ToList().ForEach(UnityEngine.Object.Destroy);
        }

        #endregion

        #region Commands

        private void cmdGetSpawnpointsConsole(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                return;
            }
            
            GetSpawnpoints();
            PrintWarning($"{data.Count} objects saved");
            Unload();
            CreateSpawnpoints();
        }

        #endregion

        #region Core

        private void GetSpawnpoints()
        {
            var objects = UnityEngine.Object.FindObjectsOfType<LootContainer>().Where(x => IsAllowed(x.ShortPrefabName));
            data.Clear();

            foreach (var obj in objects)
            {
                data.Add(new BaseEntityToSave
                {
                    prefab = obj.PrefabName,
                    rotation = obj.transform.rotation,
                    position = obj.transform.position
                });
            }
            
            SaveData();
        }

        private bool IsAllowed(string shortName)
        {
            return config.whitelist.Count > 0 ? config.whitelist.Contains(shortName) : config.blacklist.Contains(shortName);
        }

        private void CreateSpawnpoints()
        {
            UnityEngine.Object.FindObjectsOfType<Spawner>().ToList().ForEach(UnityEngine.Object.Destroy);
            
            foreach (var value in data)
            {
                var time = config.defaultRespawnTimer;
                var name = value.prefab;
                if (config.respawnTimers.ContainsKey(name))
                {
                    time = config.respawnTimers[name];
                }
                
                var entity = new GameObject().AddComponent<Spawner>();
                entity.transform.position = value.position;
                entity.transform.rotation = value.rotation;
                entity.time = time;
                entity.prefab = name;
            }
            
            PrintWarning($"{UnityEngine.Object.FindObjectsOfType<Spawner>().Length} spawners loaded");
        }

        #endregion
        
        #region Configuration 1.0.0

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "A. Whitelist (keep empty to use blacklist)")]
            public List<string> whitelist;

            [JsonProperty(PropertyName = "B. Blacklist")]
            public List<string> blacklist;

            [JsonProperty(PropertyName = "C. Respawn timers")]
            public Dictionary<string, int> respawnTimers;

            [JsonProperty(PropertyName = "Default respawn timer")]
            public int defaultRespawnTimer;

            [JsonProperty(PropertyName = "Allow stacking")]
            public bool allowStacking;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                defaultRespawnTimer = 60 * 20,
                allowStacking = false,
                whitelist = new List<string>
                {
                    "crate_elite",
                    "crate_normal",
                    "crate_normal_2"
                },
                blacklist = new List<string>
                {
                    
                },
                respawnTimers = new Dictionary<string, int>
                {
                    {"assets/bundled/prefabs/radtown/crate_elite.prefab", 60 * 40},
                    {"assets/bundled/prefabs/radtown/crate_normal.prefab", 60 * 25},
                    {"assets/bundled/prefabs/radtown/crate_normal_2.prefab", 60 * 15}
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
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
        
        #region Data 1.0.0

        private const string filename = "LootSpawpoints";
        private List<BaseEntityToSave> data = new List<BaseEntityToSave>();

        private void LoadData()
        {
            try
            {
                // TODO: Solve quaternion issue 
                var value = Interface.Oxide.DataFileSystem.ReadObject<string>(filename); 
                data = JsonConvert.DeserializeObject<List<BaseEntityToSave>>(value);
            }
            catch (Exception e)
            {
                PrintWarning(e.Message);
            }
        }

        private void SaveData()
        {
            // TODO: Solve quaternion issue
            var value = JsonConvert.SerializeObject(data, Formatting.None, new JsonSerializerSettings {ReferenceLoopHandling = ReferenceLoopHandling.Ignore});
            Interface.Oxide.DataFileSystem.WriteObject(filename, value);
        }

        #endregion
        
        #region Classes

        private class BaseEntityToSave
        {
            public string prefab;
            public Quaternion rotation;
            public Vector3 position;
        }

        private class Spawner : MonoBehaviour
        {
            public string prefab;
            public int time;

            private void Start()
            {
                InvokeRepeating("Spawn", 0, time);
            }

            private void Spawn()
            {
                if (!config.allowStacking && HasContainerNearby())
                {
                    return;
                }
                
                var entity = GameManager.server.CreateEntity(prefab, transform.position, transform.rotation);
                entity?.Spawn();
            }

            private bool HasContainerNearby()
            {
                var containers = new List<LootContainer>();
                Vis.Entities(transform.position, 0.5f, containers);
                return containers.Count > 0;
            }
        }
        
        #endregion
    }
}