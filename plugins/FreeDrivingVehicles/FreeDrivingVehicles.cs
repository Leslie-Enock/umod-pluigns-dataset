using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Free Driving Vehicles", "MON@H", "1.1.4")]
    [Description("Allows player to drive vehicles without fuel.")]
    public class FreeDrivingVehicles : RustPlugin
    {
        #region Variables

        private const string PermissionAll = "freedrivingvehicles.all";
        private const string PermissionRHIB = "freedrivingvehicles.rhib";
        private const string PermissionRowboat = "freedrivingvehicles.rowboat";
        private const string PermissionWorkcart = "freedrivingvehicles.workcart";
        private const string PermissionModularCar = "freedrivingvehicles.modularcar";
        private const string PermissionMiniCopter = "freedrivingvehicles.minicopter";
        private const string PermissionSnowmobile = "freedrivingvehicles.snowmobile";
        private const string PermissionScrapCopter = "freedrivingvehicles.scrapcopter";
        private const string PermissionSubmarineDuo = "freedrivingvehicles.submarineduo";
        private const string PermissionSubmarineSolo = "freedrivingvehicles.submarinesolo";

        private readonly Hash<uint, string> _prefabPermissions = new Hash<uint, string>();

        #endregion Variables

        #region Initialization

        private void Init()
        {
            Unsubscribe(nameof(OnFuelCheck));

            permission.RegisterPermission(PermissionAll, this);
            permission.RegisterPermission(PermissionRHIB, this);
            permission.RegisterPermission(PermissionRowboat, this);
            permission.RegisterPermission(PermissionWorkcart, this);
            permission.RegisterPermission(PermissionModularCar, this);
            permission.RegisterPermission(PermissionMiniCopter, this);
            permission.RegisterPermission(PermissionScrapCopter, this);
            permission.RegisterPermission(PermissionSubmarineDuo, this);
            permission.RegisterPermission(PermissionSubmarineSolo, this);
        }

        private void OnServerInitialized()
        {
            CreateCache();

            Subscribe(nameof(OnFuelCheck));
        }

        #endregion Initialization

        #region Configuration

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Free driving HotAirBalloons for ALL players")]
            public bool HotAirBalloonAllowed = false;

            [JsonProperty(PropertyName = "Only authorized players can drive vehicles")]
            public bool AuthorizedOnlyAllowed = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(_configData);

        #endregion Configuration

        #region OxideHooks

        private object OnFuelCheck(EntityFuelSystem fuelSystem)
        {
            if (fuelSystem == null)
            {
                return null;
            }

            if (_configData.HotAirBalloonAllowed)
            {
                HotAirBalloon hotAirBalloon = fuelSystem.fuelStorageInstance.Get(true)?.parentEntity.Get(true) as HotAirBalloon;

                if (hotAirBalloon != null)
                {
                    CacheHasFuel(fuelSystem);
                    return true;
                }
            }

            BaseVehicle baseVehicle = fuelSystem.fuelStorageInstance.Get(true)?.parentEntity.Get(true) as BaseVehicle;

            if (baseVehicle != null)
            {
                BasePlayer player = baseVehicle.GetDriver();

                if (player != null)
                {
                    if (permission.UserHasPermission(player.UserIDString, PermissionAll))
                    {
                        CacheHasFuel(fuelSystem);
                        return true;
                    }

                    return HandleFreeDriving(fuelSystem, baseVehicle.GetEntity().prefabID, player.UserIDString);
                }
            }

            return null;
        }

        #endregion OxideHooks

        #region Core

        private void CreateCache()
        {
            _prefabPermissions[StringPool.Get("assets/content/vehicles/boats/rhib/rhib.prefab")] = PermissionRHIB;
            _prefabPermissions[StringPool.Get("assets/content/vehicles/boats/rowboat/rowboat.prefab")] = PermissionRowboat;
            _prefabPermissions[StringPool.Get("assets/content/vehicles/minicopter/minicopter.entity.prefab")] = PermissionMiniCopter;
            _prefabPermissions[StringPool.Get("assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab")] = PermissionModularCar;
            _prefabPermissions[StringPool.Get("assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab")] = PermissionModularCar;
            _prefabPermissions[StringPool.Get("assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab")] = PermissionModularCar;
            _prefabPermissions[StringPool.Get("assets/content/vehicles/modularcar/car_chassis_2module.entity.prefab")] = PermissionModularCar;
            _prefabPermissions[StringPool.Get("assets/content/vehicles/modularcar/car_chassis_3module.entity.prefab")] = PermissionModularCar;
            _prefabPermissions[StringPool.Get("assets/content/vehicles/modularcar/car_chassis_4module.entity.prefab")] = PermissionModularCar;
            _prefabPermissions[StringPool.Get("assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab")] = PermissionScrapCopter;
            _prefabPermissions[StringPool.Get("assets/content/vehicles/snowmobiles/snowmobile.prefab")] = PermissionSnowmobile;
            _prefabPermissions[StringPool.Get("assets/content/vehicles/submarine/submarineduo.entity.prefab")] = PermissionSubmarineDuo;
            _prefabPermissions[StringPool.Get("assets/content/vehicles/submarine/submarinesolo.entity.prefab")] = PermissionSubmarineSolo;
            _prefabPermissions[StringPool.Get("assets/content/vehicles/workcart/workcart.entity.prefab")] = PermissionWorkcart;
        }

        private object HandleFreeDriving(EntityFuelSystem fuelSystem, uint prefabID, string userIDString)
        {
            string prefabPermission = _prefabPermissions[prefabID];

            if (!string.IsNullOrEmpty(prefabPermission) && permission.UserHasPermission(userIDString, prefabPermission))
            {
                CacheHasFuel(fuelSystem);
                return true;
            }
            
            if (_configData.AuthorizedOnlyAllowed)
            {
                return false;
            }

            return null;
        }

        private void CacheHasFuel(EntityFuelSystem fuelSystem)
        {
            fuelSystem.cachedHasFuel = true;
            fuelSystem.nextFuelCheckTime = UnityEngine.Time.time + UnityEngine.Random.Range(10f, 20f);
        }

        #endregion Core
    }
}