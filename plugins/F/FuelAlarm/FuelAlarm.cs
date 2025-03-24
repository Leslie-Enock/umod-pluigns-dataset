using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Fuel Alarm", "Oryx", "1.0.3")]
    [Description("Trigger alarm sound when fuel is below a certin level.")]

    class FuelAlarm : RustPlugin
    {
        #region Fields

        private string perm = "fuelalarm.allow";
        private string prefabSound;
        private int threshold;
        private bool usePerm;
        private float timeInterval;

        private List<VehicleCache> cacheList = new List<VehicleCache>();
        private List<ulong> disabledList;

        ConfigData configData;

        public class VehicleCache
        {
            public List<BasePlayer> playerlist = new List<BasePlayer>();
            public BaseMountable entity;

            public int getFuel()
            {
                return (entity.GetParentEntity() as MotorRowboat).GetFuelSystem()?.GetFuelAmount() ?? 0;
            }
        }
        #endregion

        #region Config
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                settings = new Settings
                {
                    threshold = 20,
                    prefabsound = "assets/bundled/prefabs/fx/invite_notice.prefab",
                    timeinterval = 3f
                },
                permissionSettings = new PermissonSettings
                {
                    useperm = false
                }
            };

            Config.WriteObject(config, true);
            configData = Config.ReadObject<ConfigData>();
            loadVariables();
        }

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public Settings settings { get; set; }
            [JsonProperty(PropertyName = "Permission Settings")]
            public PermissonSettings permissionSettings { get; set; }
        }

        private class Settings
        {
            [JsonProperty(PropertyName = "Perfab Sound")]
            public string prefabsound { get; set; }
            [JsonProperty(PropertyName = "Threshold")]
            public int threshold { get; set; }
            [JsonProperty(PropertyName = "TimeInterval")]
            public float timeinterval { get; set; }
        }

        private class PermissonSettings
        {
            [JsonProperty(PropertyName = "Use Permission")]
            public bool useperm { get; set; }
        }

        private void loadVariables()
        {
            configData = Config.ReadObject<ConfigData>();

            threshold = configData.settings.threshold;
            prefabSound = configData.settings.prefabsound;
            timeInterval = configData.settings.timeinterval;

            usePerm = configData.permissionSettings.useperm;
        }
        #endregion

        #region Hooks
        void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            if (entity == null | player == null) { return; }

            if (!(entity.GetParentEntity() as MotorRowboat || entity.GetParentEntity() as MiniCopter)) { return; }

            if ((!permission.UserHasPermission(player.UserIDString, perm)) && usePerm) { return; }

            if (GetVehcileCache(entity) != null)
            {
                AddPlayer(entity, player);
            }
            else
            {
                VehicleCache vehicleCache = new VehicleCache();
                vehicleCache.entity = entity;
                cacheList.Add(vehicleCache);

                AddPlayer(entity, player);
            }
        }

        void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            if (IsVehicleInCache(entity))
            {
                RemovePlayer(entity, player);

                if (GetVehcileCache(entity).playerlist.Count == 0)
                {
                    RemoveVehicleFromCache(entity);
                }

            }
        }

        void OnServerInitialized()
        {
            AlarmManager();
        }

        private void Init()
        {
            permission.RegisterPermission(perm, this);
            ReadData();
            loadVariables();
            cmd.AddChatCommand("fuelalarm", this, nameof(CmdFuelAlarm));
        }

        void Unload()
        {
            SaveData();
        }

        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            foreach (VehicleCache vehicle in cacheList)
            {
                foreach (BasePlayer p in vehicle.playerlist)
                {
                    if (p == player)
                    {
                        RemovePlayer(vehicle.entity, player);
                        return null;
                    }
                }
            }

            return null;
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            VehicleCache vehicle = IsPlayerInCache(player);

            if(vehicle != null)
            {
                RemovePlayer(vehicle.entity, player);
            }
        }

        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity.GetParentEntity() as MotorRowboat || entity.GetParentEntity() as MiniCopter)
            {
                if (GetVehcileCache(entity as BaseMountable) != null)
                {
                    RemoveVehicleFromCache(entity as BaseMountable);
                }
            }
        }
        #endregion

        #region Methods
        public VehicleCache GetVehcileCache(BaseMountable entity)
        {
            return cacheList.Find(x => x.entity = entity);
        }

        public bool IsVehicleInCache(BaseMountable entity)
        {
            if (entity == null) { return false; }

            VehicleCache vehicle = GetVehcileCache(entity);

            return (cacheList.Contains(vehicle));
        }

        public VehicleCache IsPlayerInCache(BasePlayer player)
        {
            foreach(VehicleCache vehicle in cacheList)
            {
                bool isPlayer = vehicle.playerlist.Any(x => x = player);

                if (isPlayer)
                {
                    return vehicle;
                }
            }
            return null;
        }

        public void RemovePlayer(BaseMountable entity, BasePlayer player)
        {
            if (player == null) { return; }

            GetVehcileCache(entity).playerlist.Remove(player);
        }

        public void RemoveVehicleFromCache(BaseMountable entity)
        {
            if(entity == null) { return; }
            cacheList.Remove(cacheList.Find(x => x.entity = entity));
        }

        public void AddPlayer(BaseMountable entity, BasePlayer player)
        {

            if (entity == null) { return; }
            if (player == null) { return; }

            GetVehcileCache(entity).playerlist.Add(player);
        }

        public void AlarmManager()
        {
            timer.Every(timeInterval, () =>
            {
                foreach (VehicleCache vehicle in cacheList)
                {
                    BasePlayer player = vehicle.playerlist[0];

                    if (usePerm)
                    {
                        if (permission.UserHasPermission(player.UserIDString, perm))
                        {
                            if (vehicle.getFuel() <= threshold)
                            {
                                bool isDisabled = disabledList.Contains(player.userID);
                                if (!isDisabled)
                                {
                                    Effect.server.Run(prefabSound, vehicle.playerlist[0].transform.position);
                                }

                            }
                        }
                    }
                    else
                    {
                        if (vehicle.getFuel() <= threshold)
                        {
                            bool isDisabled = disabledList.Contains(player.userID);
                            if (!isDisabled)
                            {
                                Effect.server.Run(prefabSound, vehicle.playerlist[0].transform.position);
                            }

                        }
                    }
                }
            });
        }
        #endregion

        #region Command
        private void CmdFuelAlarm(BasePlayer player, string command, string[] args)
        {
            bool isDisabled = disabledList.Any(x => x == player.userID);

            if (isDisabled)
            {
                disabledList.Remove(player.userID);
                Send(player, "Enabled");

            }
            else
            {
                disabledList.Add(player.userID);
                Send(player, "Disabled");
            }

            SaveData();
        }
        #endregion

        #region Data Methods
        public void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, disabledList);
        }

        public void ReadData()
        {
            try
            {
                disabledList = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>("FuelAlarm");
            }
            catch
            {
                disabledList = new List<ulong>();
            }

        }
        #endregion

        #region Localization
        public void Send(BasePlayer player, string key)
        {
            SendReply(player, lang.GetMessage(key, this, player.UserIDString));
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Enabled"] = "Alarm is now enabled",
                ["Disabled"] = "Alarm is now disabled"
            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Enabled"] = "Тревога включена",
                ["Disabled"] = "Тревога отключена"
            }, this, "ru");
        }
        #endregion
    }
}