using Newtonsoft.Json;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Minicopter Licence Plate", "The Friendly Chap", "1.1.6")]
    [Description("Spawn a licence plate (Small Wooden Board) at the back of the minicopter, with optional permissions.")]
    class MiniPlate : RustPlugin
    {
        const string PlatePrefab = "assets/prefabs/deployable/signs/sign.small.wood.prefab";
        private static readonly Vector3 PlatePosition = new Vector3(0.0f, 0.20f, -0.85f);
        private static readonly Quaternion PlateRotation = Quaternion.Euler(180, 0, 180);

        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Use Permissions")]
            public bool UsePermissions = false;

            [JsonProperty(PropertyName = "Debug")]
            public bool Debug = false;
        }

        void Init()
        {
            ShowLogo();
            permission.RegisterPermission("miniplate.use", this);

            if (!LoadConfigVariables())
            {
                Puts("Config file issue detected. Please delete the file or check the syntax and fix it.");
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config file.");
            configData = new ConfigData();
            SaveConfig(configData);
        }

        private bool LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConfig(configData);
            return true;
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        private void OnServerInitialized()
        {
            FindMinicopters();
        }

        void FindMinicopters()
        {
            foreach (var mini in BaseNetworkable.serverEntities.OfType<Minicopter>())
            {
                if (mini.children != null)
                {
                    foreach (var child in mini.children)
                    {
                        DestroyUnnecessaryComponents(child);
                    }
                }
            }
        }

        void OnEntitySpawned(Minicopter mini)
        {
            if (configData.UsePermissions)
            {
                string userID = $"{mini.OwnerID}";
                if (!permission.UserHasPermission(userID, "miniplate.use")) return;
            }

            if (mini.children == null || !mini.children.Any(child => child.PrefabName == PlatePrefab))
            {
                AttachLicensePlate(mini);
            }
        }

        void AttachLicensePlate(BaseVehicle vehicle)
        {
            CreateLicensePlate(vehicle, PlatePosition);
        }

        void CreateLicensePlate(BaseVehicle vehicle, Vector3 position)
        {
            var entity = GameManager.server.CreateEntity(PlatePrefab, vehicle.transform.position, PlateRotation);
            if (entity == null) return;

            DestroyUnnecessaryComponents(entity);
            entity.SetParent(vehicle);
            entity.transform.localPosition = position;
            entity.Spawn();
        }

        void DestroyUnnecessaryComponents(BaseEntity entity)
        {
            if (configData.Debug) Puts($"Destroying components for {entity}");

            var groundComp = entity.GetComponent<DestroyOnGroundMissing>();
            if (groundComp != null) UnityEngine.Object.DestroyImmediate(groundComp);

            var groundWatch = entity.GetComponent<GroundWatch>();
            if (groundWatch != null) UnityEngine.Object.DestroyImmediate(groundWatch);

            var decayComp = entity.GetComponent<DeployableDecay>();
            if (decayComp != null) UnityEngine.Object.DestroyImmediate(decayComp);

            var meshColliders = entity.GetComponentsInChildren<MeshCollider>();
            foreach (var meshCollider in meshColliders)
            {
                UnityEngine.Object.DestroyImmediate(meshCollider);
                if (entity.PrefabName == PlatePrefab)
                {
                    var boxCollider = entity.gameObject.AddComponent<BoxCollider>();
                    boxCollider.size = new Vector3(entity.bounds.size.x, entity.bounds.size.y, entity.bounds.size.z);
                }
            }
        }

        private void ShowLogo()
        {
            Puts(" _______ __               _______        __                 __ __             ______ __");
            Puts("|_     _|  |--.-----.    |    ___|.----.|__|.-----.-----.--|  |  |.--.--.    |      |  |--.---.-.-----.");
            Puts("  |   | |     |  -__|    |    ___||   _||  ||  -__|     |  _  |  ||  |  |    |   ---|     |  _  |  _  |");
            Puts("  |___| |__|__|_____|    |___|    |__|  |__||_____|__|__|_____|__||___  |    |______|__|__|___._|   __|");
            Puts("                         Check Plugins v1.0.0                     |_____|                       |__|");      
        }
    }
}
