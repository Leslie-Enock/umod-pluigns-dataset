using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Turret Config", "Calytic", "2.1.0")]
    [Description("Allows customizing and configuring the way turrets work")]
    class TurretConfig : RustPlugin
    {
        #region Variables

        [PluginReference]
        // ReSharper disable once InconsistentNaming
        private Plugin Vanish = null;

        private const string AutoTurretPrefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
        private const string FlameTurretPrefab = "assets/prefabs/npc/flame turret/flameturret.deployed.prefab";

        #endregion

        private uint _autoTurretPrefabId;
        private uint _flameTurretPrefabId;

        #region Configuration

        // GLOBAL OVERRIDES
        private bool _adminOverride;
        private List<object> _animals;
        private bool _animalOverride;
        private bool _sleepOverride;
        private bool _infiniteAmmo;

        // MODIFIERS
        private bool _useGlobalDamageModifier;
        private float _globalDamageModifier;

        // AUTO TURRET DEFAULTS
        private float _defaultBulletModifier;
        private float _defaultBulletSpeed;
        private string _defaultAmmoType;
        private float _defaultSightRange;
        private float _defaultAutoHealth;
        private float _defaultAimCone;

        // AUTO TURRET PERMISSION-BASED SETTINGS
        private Dictionary<string, object> _bulletModifiers;
        private Dictionary<string, object> _bulletSpeeds;
        private Dictionary<string, object> _ammoTypes;
        private Dictionary<string, object> _sightRanges;
        private Dictionary<string, object> _autoHealths;
        private Dictionary<string, object> _aimCones;

        // FLAME TURRET DEFAULTS
        private float _defaultFlameModifier;
        private float _defaultArc;
        private float _defaultTriggerDuration;
        private float _defaultFlameRange;
        private float _defaultFlameRadius;
        private float _defaultFuelPerSec;
        private float _defaultFlameHealth;

        // FLAME TURRET PERMISSION-BASED SETTINGS
        private Dictionary<string, object> _flameModifiers;
        private Dictionary<string, object> _arcs;
        private Dictionary<string, object> _triggerDurations;
        private Dictionary<string, object> _flameRanges;
        private Dictionary<string, object> _flameRadiuses;
        private Dictionary<string, object> _fuelPerSecs;
        private Dictionary<string, object> _flameHealths;

        #endregion

        private void Init()
        {
            LoadData();

            _autoTurretPrefabId = StringPool.Get(AutoTurretPrefab);
            _flameTurretPrefabId = StringPool.Get(FlameTurretPrefab);

            permission.RegisterPermission("turretconfig.infiniteammo", this);

            _adminOverride = GetConfig("Settings", "adminOverride", true);
            _animalOverride = GetConfig("Settings", "animalOverride", false);
            _sleepOverride = GetConfig("Settings", "sleepOverride", false);
            _animals = GetConfig("Settings", "animals", GetPassiveAnimals());
            _infiniteAmmo = GetConfig("Settings", "infiniteAmmo", false);

            _useGlobalDamageModifier = GetConfig("Settings", "useGlobalDamageModifier", false);
            _globalDamageModifier = GetConfig("Settings", "globalDamageModifier", 1f);

            _defaultBulletModifier = GetConfig("Auto", "defaultBulletModifier", 1f);
            _defaultAutoHealth = GetConfig("Auto", "defaultAutoHealth", 1000f);
            _defaultAimCone = GetConfig("Auto", "defaultAimCone", 5f);
            _defaultSightRange = GetConfig("Auto", "defaultSightRange", 30f);
            _defaultBulletSpeed = GetConfig("Auto", "defaultBulletSpeed", 10f);
            _defaultAmmoType = GetConfig("Auto", "defaultAmmoType", "ammo.rifle");

            _bulletModifiers = GetConfig("Auto", "bulletModifiers", GetDefaultBulletModifiers());
            _bulletSpeeds = GetConfig("Auto", "bulletSpeeds", GetDefaultBulletSpeeds());
            _ammoTypes = GetConfig("Auto", "ammoTypes", GetDefaultAmmoTypes());
            _sightRanges = GetConfig("Auto", "sightRanges", GetDefaultSightRanges());
            _autoHealths = GetConfig("Auto", "autoHealths", GetDefaultAutoHealths());
            _aimCones = GetConfig("Auto", "aimCones", GetDefaultAimCones());

            _defaultFlameModifier = GetConfig("Flame", "defaultFlameModifier", 1f);
            _defaultArc = GetConfig("Flame", "defaultArc", 45f);
            _defaultTriggerDuration = GetConfig("Flame", "defaultTriggerDuration", 5f);
            _defaultFlameRange = GetConfig("Flame", "defaultFlameRange", 7f);
            _defaultFlameRadius = GetConfig("Flame", "defaultFlameRadius", 4f);
            _defaultFuelPerSec = GetConfig("Flame", "defaultFuelPerSec", 1f);
            _defaultFlameHealth = GetConfig("Flame", "defaultFlameHealth", 300f);

            _flameModifiers = GetConfig("Flame", "flameModifiers", GetDefaultFlameModifiers());
            _arcs = GetConfig("Flame", "arcs", GetDefaultArcs());
            _triggerDurations = GetConfig("Flame", "triggerDurations", GetDefaultTriggerDurations());
            _flameRanges = GetConfig("Flame", "flameRanges", GetDefaultFlameRanges());
            _flameRadiuses = GetConfig("Flame", "flameRadiuses", GetDefaultFlameRadiuses());
            _fuelPerSecs = GetConfig("Flame", "fuelPerSecs", GetDefaultFuelPerSecs());
            _flameHealths = GetConfig("Flame", "flameHealths", GetDefaultFlameHealths());

            LoadPermissions(_bulletModifiers);
            LoadPermissions(_bulletSpeeds);
            LoadPermissions(_ammoTypes);
            LoadPermissions(_sightRanges);
            LoadPermissions(_autoHealths);
            LoadPermissions(_aimCones);

            LoadPermissions(_arcs);
            LoadPermissions(_triggerDurations);
            LoadPermissions(_flameRanges);
            LoadPermissions(_flameRadiuses);
            LoadPermissions(_fuelPerSecs);
            LoadPermissions(_flameHealths);
        }

        private void OnServerInitialized()
        {
            LoadAutoTurrets();
            LoadFlameTurrets();

            if (_useGlobalDamageModifier)
            {
                Subscribe("OnPlayerAttack");
            }
            else
            {
                Unsubscribe("OnPlayerAttack");
            }

            if (_infiniteAmmo)
            {
                Subscribe("OnItemUse");
                Subscribe("OnLootEntity");
                Subscribe("OnTurretStartup");
                Subscribe("OnTurretShutdown");
                Subscribe("CanPickupEntity");
            }
            else
            {
                Unsubscribe("OnItemUse");
                Unsubscribe("OnLootEntity");
                Unsubscribe("OnTurretStartup");
                Unsubscribe("OnTurretShutdown");
                Unsubscribe("CanPickupEntity");
            }

            if (_animalOverride || _adminOverride || _sleepOverride)
            {
                Subscribe("CanBeTargeted");
            }
            else
            {
                Unsubscribe("CanBeTargeted");
            }
        }

        private void LoadPermissions(Dictionary<string, object> type)
        {
            foreach (var kvp in type)
            {
                if (!permission.PermissionExists(kvp.Key))
                {
                    if (!string.IsNullOrEmpty(kvp.Key))
                    {
                        permission.RegisterPermission(kvp.Key, this);
                    }
                }
            }
        }

        protected void LoadFlameTurrets()
        {
            var turrets = UnityEngine.Object.FindObjectsOfType<FlameTurret>();

            if (turrets.Length > 0)
            {
                var i = 0;
                for (var index = turrets.Length - 1; index >= 0; index--)
                {
                    UpdateFlameTurret(turrets[index], true);
                    i++;
                }

                PrintWarning("Configured {0} flame turrets", i);
            }
        }

        protected void LoadAutoTurrets()
        {
            var turrets = UnityEngine.Object.FindObjectsOfType<AutoTurret>();

            if (turrets.Length > 0)
            {
                var i = 0;
                for (var index = turrets.Length - 1; index >= 0; index--)
                {
                    UpdateAutoTurret(turrets[index]);
                    i++;
                }

                PrintWarning("Configured {0} turrets", i);
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating new configuration");
            Config.Clear();

            Config["Settings", "adminOverride"] = true;
            Config["Settings", "sleepOverride"] = false;
            Config["Settings", "animalOverride"] = true;
            Config["Settings", "useGlobalDamageModifier"] = false;
            Config["Settings", "globalDamageModifier"] = 1f;

            Config["Settings", "animals"] = GetPassiveAnimals();
            Config["Settings", "infiniteAmmo"] = false;

            Config["Auto", "defaultBulletModifier"] = 1f;
            Config["Auto", "defaultBulletSpeed"] = 200f;
            Config["Auto", "defaultAmmoType"] = "ammo.rifle";
            Config["Auto", "defaultSightRange"] = 30f;
            Config["Auto", "defaultAutoHealth"] = 1000;
            Config["Auto", "defaultAimCone"] = 5f;

            Config["Auto", "bulletModifiers"] = GetDefaultBulletModifiers();
            Config["Auto", "bulletSpeeds"] = GetDefaultBulletSpeeds();
            Config["Auto", "ammoTypes"] = GetDefaultAmmoTypes();
            Config["Auto", "sightRanges"] = GetDefaultSightRanges();
            Config["Auto", "autoHealths"] = GetDefaultAutoHealths();
            Config["Auto", "aimCones"] = GetDefaultAimCones();

            Config["Flame", "defaultFlameModifier"] = 1f;
            Config["Flame", "defaultArc"] = 45f;
            Config["Flame", "defaultTriggerDuration"] = 5f;
            Config["Flame", "defaultFlameRange"] = 7f;
            Config["Flame", "defaultFlameRadius"] = 4f;
            Config["Flame", "defaultFuelPerSec"] = 1f;
            Config["Flame", "defaultFlameHealth"] = 300f;

            Config["Flame", "flameModifiers"] = GetDefaultFlameModifiers();
            Config["Flame", "arcs"] = GetDefaultArcs();
            Config["Flame", "triggerDurations"] = GetDefaultTriggerDurations();
            Config["Flame", "flameRanges"] = GetDefaultFlameRanges();
            Config["Flame", "flameRadiuses"] = GetDefaultFlameRadiuses();
            Config["Flame", "fuelPerSecs"] = GetDefaultFuelPerSecs();
            Config["Flame", "flameHealths"] = GetDefaultFlameHealths();
        }

        protected void ReloadConfig()
        {
            Config["VERSION"] = Version.ToString();

            // NEW CONFIGURATION OPTIONS HERE
            // END NEW CONFIGURATION OPTIONS

            PrintWarning("Upgrading Configuration File");
            SaveConfig();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Denied: Permission", "You lack permission to do that"}
            }, this);
        }

        private List<object> GetPassiveAnimals()
        {
            return new List<object>
            {
                "stag",
                "boar",
                "chicken",
                "horse"
            };
        }

        private Dictionary<string, object> GetDefaultBulletModifiers()
        {
            return new Dictionary<string, object>
            {
                {"turretconfig.default", 2f}
            };
        }

        private Dictionary<string, object> GetDefaultFlameModifiers()
        {
            return new Dictionary<string, object>
            {
                {"turretconfig.default", 2f}
            };
        }

        private Dictionary<string, object> GetDefaultBulletSpeeds()
        {
            return new Dictionary<string, object>
            {
                {"turretconfig.default", 200f}
            };
        }

        private Dictionary<string, object> GetDefaultSightRanges()
        {
            return new Dictionary<string, object>
            {
                {"turretconfig.default", 30f}
            };
        }

        private Dictionary<string, object> GetDefaultAmmoTypes()
        {
            return new Dictionary<string, object>
            {
                {"turretconfig.default", "ammo.rifle"}
            };
        }

        private Dictionary<string, object> GetDefaultAutoHealths()
        {
            return new Dictionary<string, object>
            {
                {"turretconfig.default", 1000f}
            };
        }

        private Dictionary<string, object> GetDefaultAimCones()
        {
            return new Dictionary<string, object>
            {
                {"turretconfig.default", 5f}
            };
        }

        private Dictionary<string, object> GetDefaultArcs()
        {
            return new Dictionary<string, object>
            {
                {"turretconfig.default", 45f}
            };
        }

        private Dictionary<string, object> GetDefaultTriggerDurations()
        {
            return new Dictionary<string, object>
            {
                {"turretconfig.default", 5f}
            };
        }

        private Dictionary<string, object> GetDefaultFlameRanges()
        {
            return new Dictionary<string, object>
            {
                {"turretconfig.default", 7f}
            };
        }

        private Dictionary<string, object> GetDefaultFlameRadiuses()
        {
            return new Dictionary<string, object>
            {
                {"turretconfig.default", 4f}
            };
        }

        private Dictionary<string, object> GetDefaultFuelPerSecs()
        {
            return new Dictionary<string, object>
            {
                {"turretconfig.default", 1f}
            };
        }

        private Dictionary<string, object> GetDefaultFlameHealths()
        {
            return new Dictionary<string, object>
            {
                {"turretconfig.default", 300f}
            };
        }

        private void LoadData()
        {
            if (Config["VERSION"] == null)
            {
                // FOR COMPATIBILITY WITH INITIAL VERSIONS WITHOUT VERSIONED CONFIG
                ReloadConfig();
            }
            else if (GetConfig("VERSION", Version.ToString()) != Version.ToString())
            {
                // ADDS NEW, IF ANY, CONFIGURATION OPTIONS
                ReloadConfig();
            }
        }

        [ConsoleCommand("turrets.reload")]
        private void CcTurretReload(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
            {
                if (arg.Connection.authLevel < 1)
                {
                    SendReply(arg, GetMsg("Denied: Permission", arg.Connection.userid.ToString()));
                    return;
                }
            }

            LoadAutoTurrets();
            LoadFlameTurrets();
        }

        private void OnLootEntity(BasePlayer looter, BaseEntity target)
        {
            if (!_infiniteAmmo) return;
            if (!permission.UserHasPermission(target.OwnerID.ToString(), "turretconfig.infiniteammo")) return;

            if (target is AutoTurret)
            {
                var autoTurret = target as AutoTurret;

                while(autoTurret.GetTotalAmmo() > 0)
                {
                    autoTurret.GetAttachedWeapon().primaryMagazine.contents = 0;
                    autoTurret.Reload();

                    autoTurret.UpdateTotalAmmo();
                }
            }
            else if(target is FlameTurret)
            {
                timer.Once(0.01f, looter.EndLooting);
            }
        }

        private void OnItemUse(Item item, int amount)
        {
            if (!_infiniteAmmo) return;

            var entity = item.parent?.entityOwner;
            if (entity != null)
            {
                if (entity is AutoTurret)
                {
                    var autoTurret = entity as AutoTurret;
                    if (autoTurret != null && autoTurret.IsPowered() && autoTurret.IsOnline())
                    {
                        if (!permission.UserHasPermission(autoTurret.OwnerID.ToString(), "turretconfig.infiniteammo")) return;
                        RefillAutoTurretAmmo(autoTurret);
                    }
                }

                if(entity is FlameTurret)
                {
                    var flameTurret = entity as FlameTurret;
                    if(flameTurret != null && !flameTurret.HasFuel())
                    {
                        if (!permission.UserHasPermission(flameTurret.OwnerID.ToString(), "turretconfig.infiniteammo")) return;
                        RefillFlameTurretFuel(flameTurret);
                    }
                }

            }
        }

        void OnTurretStartup(AutoTurret autoTurret)
        {
            CheckAutoTurretAmmo(autoTurret);
        }

        void OnTurretShutdown(AutoTurret autoTurret)
        {
            if (!_infiniteAmmo) return;
            if (!permission.UserHasPermission(autoTurret.OwnerID.ToString(), "turretconfig.infiniteammo")) return;

            while(autoTurret.GetTotalAmmo() > 0)
            {
                autoTurret.GetAttachedWeapon().primaryMagazine.contents = 0;
                autoTurret.Reload();

                autoTurret.UpdateTotalAmmo();
            }
        }

        private void CheckAutoTurretAmmo(AutoTurret autoTurret)
        {
            if (!_infiniteAmmo) return;
            if (!permission.UserHasPermission(autoTurret.OwnerID.ToString(), "turretconfig.infiniteammo")) return;

            RefillAutoTurretAmmo(autoTurret);

            autoTurret.Invoke(() => CheckAutoTurretAmmo(autoTurret), 5.75f);
        }

        private void RefillAutoTurretAmmo(AutoTurret autoTurret)
        {
            if (autoTurret.GetTotalAmmo() <= 1 && autoTurret.IsPowered() && autoTurret.IsOnline() && autoTurret.GetAttachedWeapon() != null)
            {
                autoTurret.inventory.AddItem(autoTurret.GetAttachedWeapon().primaryMagazine.ammoType, autoTurret.GetAttachedWeapon().primaryMagazine.capacity);
                autoTurret.Reload();
                autoTurret.SendNetworkUpdateImmediate(false);
            }
        }

        private void CheckFlameTurretFuel(FlameTurret flameTurret)
        {
            if (!_infiniteAmmo) return;
            if (!permission.UserHasPermission(flameTurret.OwnerID.ToString(), "turretconfig.infiniteammo")) return;

            RefillFlameTurretFuel(flameTurret);

            flameTurret.Invoke(() => CheckFlameTurretFuel(flameTurret), 5.75f);
        }

        private void RefillFlameTurretFuel(FlameTurret flameTurret)
        {
            if (!flameTurret.HasFuel())
            {
                foreach (var item in flameTurret.inventory.onlyAllowedItems)
                {
                    flameTurret.inventory.AddItem(item, 50);
                }
                flameTurret.SendNetworkUpdateImmediate(false);
            }
        }

        void CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            if (!_infiniteAmmo) return;

            if(entity is FlameTurret)
            {
                if (!permission.UserHasPermission(entity.OwnerID.ToString(), "turretconfig.infiniteammo")) return;

                var flameTurret = entity as FlameTurret;
                if(flameTurret != null)
                {
                    flameTurret.inventory.Clear();
                }
            }
        }

        private object CanBeTargeted(BaseCombatEntity target, MonoBehaviour turret)
        {
            if (target is BasePlayer)
            {
                var isInvisible = Vanish?.Call("IsInvisible", target);
                if (isInvisible != null && (bool)isInvisible)
                {
                    return null;
                }
            }

            if (!(turret is AutoTurret) && !(turret is FlameTurret))
            {
                return null;
            }

            if (_animalOverride && target.GetComponent<BaseNpc>() != null)
            {
                if (_animals.Count > 0)
                {
                    if (_animals.Contains(target.ShortPrefabName.Replace(".prefab", "").ToLower()))
                    {
                        return false;
                    }

                    return null;
                }

                return false;
            }

            if (target.ToPlayer() == null)
            {
                return null;
            }

            var targetPlayer = target.ToPlayer();

            if (_adminOverride && targetPlayer.IsConnected && targetPlayer.net.connection.authLevel > 0)
            {
                return false;
            }
            if (_sleepOverride && targetPlayer.IsSleeping())
            {
                return false;
            }

            return null;
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity == null) return;

            if (entity.prefabID == _autoTurretPrefabId)
            {
                UpdateAutoTurret((AutoTurret)entity, true);
            }
            else if (entity.prefabID == _flameTurretPrefabId)
            {
                UpdateFlameTurret((FlameTurret)entity, true);
            }
        }

        private T FromPermission<T>(string userID, Dictionary<string, object> options, T defaultValue)
        {
            if (!string.IsNullOrEmpty(userID) && userID != "0")
            {
                foreach (var kvp in options)
                {
                    if (permission.UserHasPermission(userID, kvp.Key))
                    {
                        return (T)Convert.ChangeType(kvp.Value, typeof(T));
                    }
                }
            }

            return defaultValue;
        }

        private void InitializeTurret(BaseCombatEntity turret, float turretHealth, bool justCreated = false)
        {
            if (justCreated)
            {
                turret._health = turretHealth;
            }
            turret._maxHealth = turretHealth;

            if (justCreated)
            {
                turret.InitializeHealth(turretHealth, turretHealth);
            }
            else
            {
                turret.InitializeHealth(turret.health, turretHealth);
            }

            turret.startHealth = turretHealth;
        }

        private void UpdateFlameTurret(FlameTurret turret, bool justCreated = false)
        {
            CheckFlameTurretFuel(turret);

            var userID = turret.OwnerID.ToString();

            var turretHealth = FromPermission(userID, _flameHealths, _defaultFlameHealth);

            InitializeTurret(turret, turretHealth, justCreated);

            turret.arc = FromPermission(userID, _arcs, _defaultArc);
            turret.triggeredDuration = FromPermission(userID, _triggerDurations, _defaultTriggerDuration);
            turret.flameRange = FromPermission(userID, _flameRanges, _defaultFlameRange);
            turret.flameRadius = FromPermission(userID, _flameRadiuses, _defaultFlameRadius);
            turret.fuelPerSec = FromPermission(userID, _fuelPerSecs, _defaultFuelPerSec);

            turret.SendNetworkUpdateImmediate(justCreated);
        }

        private void UpdateAutoTurret(AutoTurret turret, bool justCreated = false)
        {
            CheckAutoTurretAmmo(turret);

            var userID = turret.OwnerID.ToString();

            var turretHealth = FromPermission(userID, _autoHealths, _defaultAutoHealth);
            var ammoType = FromPermission(userID, _ammoTypes, _defaultAmmoType);

            InitializeTurret(turret, turretHealth, justCreated);

            turret.bulletSpeed = FromPermission(userID, _bulletSpeeds, _defaultBulletSpeed);
            turret.sightRange = FromPermission(userID, _sightRanges, _defaultSightRange);
            turret.aimCone = FromPermission(userID, _aimCones, _defaultAimCone);

            var def = ItemManager.FindItemDefinition(ammoType);
            if (def != null)
            {
                var weapon = turret.GetAttachedWeapon();
                if (weapon?.primaryMagazine != null && weapon.IsValid())
                    weapon.primaryMagazine.ammoType = def;

                var projectile = def.GetComponent<ItemModProjectile>();
                if (projectile?.projectileObject != null && projectile.projectileObject.isValid)
                {
                    turret.gun_fire_effect.guid = projectile.projectileObject.guid;
                    turret.bulletEffect.guid = projectile.projectileObject.guid;
                }
            }
            else
            {
                PrintWarning("No ammo of type ({0})", ammoType);
            }


            if(turret.IsPowered() && turret.IsOnline())
            {
                turret.Reload();
            }

            turret.SendNetworkUpdateImmediate(justCreated);
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (hitInfo.Initiator != null)
            {
                var modifier = 1f;
                if (hitInfo.Initiator.prefabID == _autoTurretPrefabId)
                {
                    modifier = FromPermission(hitInfo.Initiator.OwnerID.ToString(), _bulletModifiers, _defaultBulletModifier);
                }
                else if (hitInfo.Initiator.prefabID == _flameTurretPrefabId)
                {
                    modifier = FromPermission(hitInfo.Initiator.OwnerID.ToString(), _flameModifiers, _defaultFlameModifier);
                }

                hitInfo.damageTypes.ScaleAll(modifier);
            }
        }

        private void OnPlayerAttack(BasePlayer attacker, HitInfo hitInfo)
        {
            if (attacker == null || hitInfo == null || hitInfo.HitEntity == null) return;

            if (_useGlobalDamageModifier && hitInfo.HitEntity.prefabID == _autoTurretPrefabId || hitInfo.HitEntity.prefabID == _flameTurretPrefabId)
            {
                hitInfo.damageTypes.ScaleAll(_globalDamageModifier);
            }
        }

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null)
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(Config[name], typeof(T));
        }

        private T GetConfig<T>(string name, string name2, T defaultValue)
        {
            if (Config[name, name2] == null)
            {
                return defaultValue;
            }

            return (T)Convert.ChangeType(Config[name, name2], typeof(T));
        }

        private string GetMsg(string key, string userID = null)
        {
            return lang.GetMessage(key, this, userID);
        }
    }
}