using System;
using System.Linq;
using System.Collections.Generic;
using Oxide.Core;
using Newtonsoft.Json;
using UnityEngine;
using Rust;

namespace Oxide.Plugins
{
    [Info("Spawn Entity Group", "Obito", "1.0.3")]
    [Description("Random spawn a custom entity group in a random map location")]

    class SpawnEntityGroup : RustPlugin
    {
        #region Defaults

        private float terrainSize = TerrainMeta.Size.x;

        #endregion

        #region Oxide Hooks

        void OnServerInitialized()
        {
            timer.Every(config.spawnTime, () => {
                Vector3 pos = GetGroundPosition((Vector3)GetSpawnPos());
                SpawnLooter(pos);
                if (config.consoleLog)
                {
                    Puts(string.Format(config.logMsg, pos));
                }
            });
        }

        #endregion

        #region Helpers

        public void SpawnLooter(Vector3 pos)
        {
            if(pos != null)
            {
                foreach(var value in config.entities)
                {
                    var prefab = value.prefab;
                    var position = pos + GetVector(value);
                    var entity = GameManager.server.CreateEntity(prefab, position);
                    if (entity == null) continue;
                    entity?.Spawn();
                    AddRigidbody(entity);
                }
            }
        }

        private void AddRigidbody(BaseEntity entity)
        {
            Rigidbody rigidbody = entity.gameObject.GetComponent<Rigidbody>();
            if (rigidbody == null) rigidbody = entity.gameObject.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.drag = 0.3f;
            rigidbody.isKinematic = true;
            var mesh = (MeshCollider)entity.gameObject.GetComponent<MeshCollider>();
            if (mesh == null) mesh = entity.gameObject.AddComponent<MeshCollider>();
            mesh.convex = false;
        }

        private Vector3 GetVector(EntityData ent)
        {
            return new Vector3(ent.x, ent.y, ent.z);
        }

        #region Method by 'Egor Blagov' from plugin Random Respawner

        private Vector3? GetSpawnPos() 
        {
            for (int i = 0; i < 150; i++) 
            {
                Vector3 randomPos = new Vector3(
                    UnityEngine.Random.Range(-TerrainMeta.Size.x / 4, TerrainMeta.Size.x / 4),
                    10f,
                    UnityEngine.Random.Range(-TerrainMeta.Size.z / 4, TerrainMeta.Size.z / 4)
                );

                if (this.TestPosIsValid(ref randomPos)) 
                {
                    return randomPos;
                }
            }

            return null;
        }

        private bool TestPosIsValid(ref Vector3 randomPos) {
            RaycastHit hitInfo;


            if (WaterLevel.Test(randomPos + new Vector3(0, 1.3f, 0))) 
            {
                return false;
            }

            var colliders = new List<Collider>();
            Vis.Colliders(randomPos, 3f, colliders);

            if (colliders.Where(col => col.name.ToLower().Contains("prevent") && col.name.ToLower().Contains("building")).Count() > 0) 
            {
                return false;
            }

            var entities = new List<BaseEntity>();
            Vis.Entities(randomPos, 3f, entities);
            if (entities.Where(ent => ent is BaseHelicopter).Count() > 0) 
            {
                return false;
            }

            if (10f > 0) 
            {
                var players = new List<BasePlayer>();
                Vis.Entities(randomPos, 10f, players);
                if (players.Count > 0) {
                    return false;
                }
            }

            var cupboards = new List<BuildingPrivlidge>();
            Vis.Entities(randomPos, 20f + 10f, cupboards);
            if (cupboards.Count > 0) 
            {
                return false;
            }

            return true;
        }

        #endregion

        //Credits: Wulf
        private static LayerMask GROUND_MASKS = LayerMask.GetMask("Terrain", "World", "Construction");
        static Vector3 GetGroundPosition(Vector3 sourcePos)
        {
            RaycastHit hitInfo;            
            if (Physics.Raycast(sourcePos, Vector3.down, out hitInfo, GROUND_MASKS)){
                sourcePos.y = hitInfo.point.y;
            }
            sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));
            return sourcePos;
        }

        #endregion

        #region Config

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Spawn time")]
            public float spawnTime { get; set; }

            [JsonProperty(PropertyName = "Show console log")]
            public bool consoleLog { get; set; }

            [JsonProperty(PropertyName = "Log message")]
            public string logMsg { get; set; }

            [JsonProperty(PropertyName = "Entities")]
            public List<EntityData> entities;
        }

        private class EntityData
        {
            public string prefab;
            public float x;
            public float y;
            public float z;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                spawnTime = 300f,
                consoleLog = true,
                logMsg = "A entity group spawned at: {0}",

                entities = new List<EntityData>()
                {
                    new EntityData()
                    {
                        prefab = "assets/bundled/prefabs/radtown/loot_barrel_1.prefab",
                        x = 1f,
                        y = 0f,
                        z = 0f
                    },
                    new EntityData()
                    {
                        prefab = "assets/bundled/prefabs/radtown/loot_barrel_2.prefab",
                        x = -1f,
                        y = 0f,
                        z = 0f
                    },
                    new EntityData()
                    {
                        prefab = "assets/bundled/prefabs/radtown/crate_normal_2.prefab",
                        x = 0f,
                        y = 0f,
                        z = 0f
                    },
                    new EntityData()
                    {
                        prefab = "assets/prefabs/npc/patrol helicopter/heli_crate.prefab",
                        x = 0f,
                        y = 1.5f,
                        z = 0f
                    }
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
            PrintWarning("The default configuration file has been created!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion
    }
}