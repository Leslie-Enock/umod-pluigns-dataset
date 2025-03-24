using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("No Ammo Consumtion", "KibbeWater", "0.1.6")]
    [Description("Blocks from consuming ammunition from your inventory")]
    class NoAmmoConsumption : RustPlugin
    {
        private PluginConfig _config;

        private string permissionUse = "noammoconsumption.use";

        private Dictionary<BaseProjectile, Timer> reloadingWeapons = new Dictionary<BaseProjectile, Timer>();

        private List<int> segmentLoadedWeapons = new List<int>() {
            1588298435, //Bolt action rile
            -1123473824, //Multiple Grenade Launcher
            795371088, //Pump Shotgun
            -41440462 //Spas Shotgun
        };

        #region Config
        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new JsonException();
                }
            }
            catch
            {
                PrintWarning("Loaded default config.");
                
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        private class PermissionInfo {
            public string permissionName;
            public List<int> ammoList;
        }

        private class PluginConfig
        {
            public List<PermissionInfo> allowedAmmoGroups = new List<PermissionInfo>();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    allowedAmmoGroups = new List<PermissionInfo>() {
                        new PermissionInfo() {
                            permissionName = "rifleAmmo",
                            ammoList = new List<int>() {
                                -1211166256, //ammo.rifle
                                1712070256 //ammo.rifle.hv
                            }
                        },
                        new PermissionInfo() {
                            permissionName = "pistolAmmo",
                            ammoList = new List<int>() {
                                785728077, //ammo.pistol
                                -1691396643 //ammo.pistol.hv
                            }
                        },
                        new PermissionInfo() {
                            permissionName = "shotgunAmmo",
                            ammoList = new List<int>() {
                                -1685290200, //ammo.shotgun
                                -727717969, //ammo.shotgun.slug
                                588596902 //ammo.handmade.shell
                            }
                        },
                        new PermissionInfo() {
                            permissionName = "bowArrows",
                            ammoList = new List<int>() {
                                215754713, //arrow.bone
                                -1234735557, //arrow.wooden
                                -1023065463 //arrow.hv
                            }
                        }
                    }
                };
            }
        }
        #endregion

        #region Methods
        public bool isAllowed(BasePlayer player, int ammoID) {
            if (!permission.UserHasPermission(player.UserIDString, permissionUse))
                return false;
            for (int i = 0; i < _config.allowedAmmoGroups.Count; i++)
                if (permission.UserHasPermission(player.UserIDString, "noammoconsumption." + _config.allowedAmmoGroups[i].permissionName)){
                    for (int x = 0; x < _config.allowedAmmoGroups[i].ammoList.Count; x++)
                        if (ammoID == _config.allowedAmmoGroups[i].ammoList[x])
                            return true;
                } else
                    return false;
            return false;
        }

        private bool IsSegmentedLoaded(BaseProjectile weapon) {
            return IsSegmentedLoaded(weapon.GetItem().info.itemid);
        }

        private bool IsSegmentedLoaded(int weponId) {
            foreach (var ID in segmentLoadedWeapons)
                if (ID == weponId)
                    return true;
            return false;
        }
        #endregion

        #region Hooks
        private void Init()
        {
            permission.RegisterPermission(permissionUse, this);
            for (int i = 0; i < _config.allowedAmmoGroups.Count; i++)
                permission.RegisterPermission("noammoconsumption." + _config.allowedAmmoGroups[i].permissionName, this);
        }

        object OnReloadWeapon(BasePlayer player, BaseProjectile projectile)
        {
            int itemID = projectile.primaryMagazine.ammoType.itemid;
            if (!isAllowed(player, itemID))
                return null;
            else {
                var mag = projectile.primaryMagazine;
                var newReloadTime = IsSegmentedLoaded(projectile) ? (mag.capacity - mag.contents) * (projectile.reloadTime / mag.capacity) : projectile.reloadTime;
                var reloadTimer = timer.Once(newReloadTime, () =>
                {
                    projectile.primaryMagazine.contents = projectile.primaryMagazine.capacity;
                    projectile.SendNetworkUpdateImmediate();
                    if (reloadingWeapons.ContainsKey(projectile))
                        reloadingWeapons.Remove(projectile);
                });

                if (!reloadingWeapons.ContainsKey(projectile)) //Prevent already added exceptions in console
                    reloadingWeapons.Add(projectile, reloadTimer);
                
                return true;
            }
        }

        object OnAmmoUnload(BaseProjectile projectile, Item item, BasePlayer player)
        {
            int itemID = projectile.primaryMagazine.ammoType.itemid;
            if (isAllowed(player, itemID)) {
                projectile.primaryMagazine.contents = 0;
                projectile.SendNetworkUpdateImmediate();
                return true;
            }
            return null;
        }

        object OnSwitchAmmo(BasePlayer player, BaseProjectile projectile)
        {
            int itemID = projectile.primaryMagazine.ammoType.itemid;
            if (isAllowed(player, itemID)) {
                projectile.primaryMagazine.contents = 0;
                projectile.SendNetworkUpdateImmediate();
            }
            return null;
        }

        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            foreach (var weapon in reloadingWeapons)
                if (weapon.Key.GetItem() == oldItem) {
                    weapon.Value.Destroy();
                    reloadingWeapons.Remove(weapon.Key);
                    break;
                }
        }

        private void Unload()
        {
            reloadingWeapons.Clear();
        }
        #endregion
    }
}