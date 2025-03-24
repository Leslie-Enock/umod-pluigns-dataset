using System.Collections.Generic;
using Oxide.Core.Configuration;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using System;
using Oxide.Game.Rust.Cui;
using System.Text.RegularExpressions;
using Color = UnityEngine.Color;
using VLB;
using Rust;

namespace Oxide.Plugins
{
    [Info("Monument Plus Lite", "Ts3Hosting", "1.1.0")]
    [Description("Auto spawn prefabs at monuments")]
    public class MonumentPlus : RustPlugin
    {
        public static MonumentPlus _;
        public List<NetworkableId> spawnedEntitys = new List<NetworkableId>();
        private const int DamageTypeMax = (int)DamageType.LAST;

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPerm"] = "<color=#ce422b>You need permishion to use this command.</color>",
                ["info"] = "<color=#ce422b>MonumentName:</color> \"{0}\" \n <color=#ce422b>Position:</color>\n x = \"{1}\"\n y = \"{2}\"\n z = \"{3}\"\n</color>"
            }, this);
        }

        #region Config

        private ConfigData configData;
        class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public Settings settings { get; set; }

            [JsonProperty(PropertyName = "Spawn Addons At")]
            public Entity entity { get; set; }

            public class Settings
            {
                public bool AutoSpawnOnBoot { get; set; }
            }

            public class Entity
            {
                public Dictionary<string, itemSpawning> Information { get; set; }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
        }
        protected override void LoadConfig()
        {
            _ = this;
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                settings = new ConfigData.Settings
                {
                    AutoSpawnOnBoot = false,
                },

                entity = new ConfigData.Entity
                {
                    Information = new Dictionary<string, itemSpawning>()
                },

                Version = Version
            };
        }

        class itemSpawning
        {
            public bool enabled;
            public string MonumentName;
            public string prefab;
            public Vector3 pos;
            public float rotate;
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new VersionNumber(1, 0, 1))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion Config

        private void Init()
        {
            permission.RegisterPermission("monumentplus.info", this);
        }

        private void OnServerInitialized()
        {
            if (configData.entity.Information.Count() <= 0)
            {
                configData.entity.Information.Add("supermarket", new itemSpawning());
                configData.entity.Information["supermarket"].MonumentName = "supermarket";
                configData.entity.Information["supermarket"].prefab = "assets/bundled/prefabs/static/modularcarlift.static.prefab";
                configData.entity.Information["supermarket"].pos = new Vector3(0.2f, 0f, 17.5f);
                configData.entity.Information["supermarket"].rotate = 0.0f;

                configData.entity.Information.Add("gasstation", new itemSpawning());
                configData.entity.Information["gasstation"].MonumentName = "gas_station";
                configData.entity.Information["gasstation"].prefab = "assets/bundled/prefabs/static/modularcarlift.static.prefab";
                configData.entity.Information["gasstation"].pos = new Vector3(4.2f, 3.0f, -0.5f);
                configData.entity.Information["gasstation"].rotate = 0.0f;

                Config.WriteObject(configData, true);
            }

            if (configData.settings.AutoSpawnOnBoot)
                spawningAddons(false);
        }

        private void Unload()
        {
            foreach (NetworkableId entity in spawnedEntitys.ToList())
            {
                var networkable = BaseNetworkable.serverEntities.Find(entity);
                if (networkable != null) networkable?.Kill();
            }
            _ = null;
        }

        [ChatCommand("monumentinfo")]
        private void GetLocations(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, "monumentplus.info"))
            {
                SendReply(player, lang.GetMessage("NoPerm", this));
                return;
            }
            float lowestDist = float.MaxValue;
            MonumentInfo closest = null;
            string name = "Unknown";
            foreach (var monument in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if (GetMonumentName(monument) == null || GetMonumentName(monument).Contains("substation")) continue;
                float dist = Vector3.Distance(player.transform.position, monument.transform.position);
                if (dist < lowestDist)
                {
                    lowestDist = dist;
                    closest = monument;
                    name = GetMonumentName(monument);
                    if (name.Contains("monument_marker.prefab"))
                        name = "\"" + monument.gameObject?.gameObject?.transform?.parent?.gameObject?.transform?.root?.name + "\"";
                }
            }

            var localPos = closest.transform.InverseTransformPoint(player.transform.position);
            var rotation = player.transform.rotation;
            if (MonumentToName.ContainsKey(name))
                SendReply(player, lang.GetMessage("info", this), MonumentToName[name], localPos.x, localPos.y, localPos.z);
            else SendReply(player, lang.GetMessage("info", this), name, localPos.x, localPos.y, localPos.z);
            //Puts(name + " " + localPos.ToString( "F4" ));
        }

        public string GetMonumentName(MonumentInfo monument)
        {
            var gameObject = monument.gameObject;

            while (gameObject.name.StartsWith("assets/") == false && gameObject.transform.parent != null)
            {
                gameObject = gameObject.transform.parent.gameObject;
            }
            if (gameObject?.name != null && gameObject.name.Contains("monument_marker.prefab"))
                return monument.gameObject?.gameObject?.transform?.parent?.gameObject?.transform?.root?.name;

            return gameObject?.name;
        }

        private void spawningAddons(bool unLoading)
        {
            foreach (var key in configData.entity.Information.ToList())
            {
                if (!key.Value.enabled) continue;
                foreach (var monument in TerrainMeta.Path.Monuments)
                {
                    Vector3 itemsVector = Vector3.zero;
                    if (monument == null) continue;
                    if (!monument.name.ToLower().Contains(key.Value.MonumentName.ToLower())) continue;
                    itemsVector = monument.transform.TransformPoint(key.Value.pos);
                    if (itemsVector == null || itemsVector == Vector3.zero) continue;

                    var itemsEntity = GameManager.server.CreateEntity(key.Value.prefab, itemsVector, Quaternion.Euler(monument.transform.localEulerAngles.x, monument.transform.localEulerAngles.y + key.Value.rotate, monument.transform.localEulerAngles.z));

                    if (itemsEntity != null)
                    {
                        itemsEntity.enableSaving = false;
                        RemoveGroundWatch(itemsEntity);
                        itemsEntity.Spawn();
                        RemoveGroundWatch(itemsEntity);
                        spawnedEntitys.Add(itemsEntity.net.ID);
                    }
                }
            }
        }

        private void RemoveGroundWatch(BaseEntity entity)
        {
            if (entity == null) return;
            if (entity.GetComponent<GroundWatch>() != null)
                UnityEngine.Object.DestroyImmediate(entity.GetComponent<GroundWatch>());
            if (entity.GetComponent<DestroyOnGroundMissing>() != null)
                UnityEngine.Object.DestroyImmediate(entity.GetComponent<DestroyOnGroundMissing>());
        }

        private void OnEntityTakeDamage(BaseEntity entity, HitInfo hitInfo)
        {
            if (hitInfo != null && entity != null && entity.net != null && spawnedEntitys.Contains(entity.net.ID) && hitInfo.damageTypes != null)
            {
                for (var i = 0; i < DamageTypeMax; i++)
                {
                    hitInfo.damageTypes.Scale((DamageType)i, 0);
                }
            }
        }

        private static Dictionary<string, string> MonumentToName
        {
            get
            {
                return new Dictionary<string, string>()
                {
                { "assets/bundled/prefabs/autospawn/monument/small/warehouse.prefab", "warehouse" },
                { "assets/bundled/prefabs/autospawn/monument/lighthouse/lighthouse.prefab", "lighthouse" },
                { "assets/bundled/prefabs/autospawn/monument/small/satellite_dish.prefab", "satellite_dish" },
                { "assets/bundled/prefabs/autospawn/monument/small/sphere_tank.prefab", "sphere_tank" },
                { "assets/bundled/prefabs/autospawn/monument/harbor/harbor_1.prefab", "harbor_1" },
                { "assets/bundled/prefabs/autospawn/monument/harbor/harbor_2.prefab", "harbor_2" },
                { "assets/bundled/prefabs/autospawn/monument/large/airfield_1.prefab", "airfield" },
                { "assets/bundled/prefabs/autospawn/monument/large/junkyard_1.prefab", "junkyard" },
                { "assets/bundled/prefabs/autospawn/monument/large/launch_site_1.prefab", "launch_site" },
                { "assets/bundled/prefabs/autospawn/monument/large/military_tunnel_1.prefab", "military_tunnel" },
                { "assets/bundled/prefabs/autospawn/monument/large/powerplant_1.prefab", "powerplant" },
                { "assets/bundled/prefabs/autospawn/monument/large/trainyard_1.prefab", "trainyard" },
                { "assets/bundled/prefabs/autospawn/monument/large/water_treatment_plant_1.prefab", "water_treatment_plant" },
                { "assets/bundled/prefabs/autospawn/monument/medium/bandit_town.prefab", "bandit_town" },
                { "assets/bundled/prefabs/autospawn/monument/medium/compound.prefab", "compound" },
                { "assets/bundled/prefabs/autospawn/monument/medium/radtown_small_3.prefab", "radtown_small_3" },
                { "assets/bundled/prefabs/autospawn/monument/small/gas_station_1.prefab", "gas_station" },
                { "assets/bundled/prefabs/autospawn/monument/roadside/gas_station_1.prefab", "gas_station" },
                { "assets/bundled/prefabs/autospawn/monument/small/mining_quarry_a.prefab", "mining_quarry_a" },
                { "assets/bundled/prefabs/autospawn/monument/small/mining_quarry_b.prefab", "mining_quarry_b" },
                { "assets/bundled/prefabs/autospawn/monument/small/mining_quarry_c.prefab", "mining_quarry_c" },
                { "assets/bundled/prefabs/autospawn/monument/offshore/oilrig_2.prefab", "oilrig_2" },
                { "assets/bundled/prefabs/autospawn/monument/offshore/oilrig_1.prefab", "oilrig_1" },
                };
            }
        }
    }
}