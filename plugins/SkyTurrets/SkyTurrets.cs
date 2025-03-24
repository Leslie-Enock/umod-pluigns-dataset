using Facepunch;
using Rust;
using System;
using System.Globalization;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Sky Turrets", "Xavier", "3.0.6")]
    [Description("Allows auto turrets and SAM sites to target patrol helicopters and chinooks")]
    public class SkyTurrets : RustPlugin
    {
        static SkyTurrets plugin;
        const string skyturretperms = "skyturrets.use";
        private DynamicConfigFile data;
        private StoredData storedData;
   
        private List<ulong> turretIDs = new List<ulong>();
        private List<ulong> PoweredTurretIDs = new List<ulong>();
        private List<ulong> samIDs = new List<ulong>();
   
        private class StoredData
        {
            public List<ulong> turretIDs = new List<ulong>();
            public List<ulong> PoweredTurretIDs = new List<ulong>();
            public List<ulong> samIDs = new List<ulong>();
        }
   
        private void SaveData()
        {
            storedData.turretIDs = turretIDs;
            storedData.PoweredTurretIDs = PoweredTurretIDs;
            storedData.samIDs = samIDs;
            data.WriteObject(storedData);
            Puts(lang.GetMessage("Saving", this));
        }

        private bool ConfigChanged;
   
        bool PowerlessTurrets = true;
        float AirTargetRadius = 150f;
        bool NeedsPerms = true;
        int SamSiteAttackDistance = 300;
        float CheckForTargetEveryXSeconds = 5f;
   
        BUTTON PowerButton = BUTTON.FIRE_THIRD;
        string Button ="FIRE_THIRD";
   
        void LoadVariables()
        {
            AirTargetRadius = Convert.ToSingle(GetConfig("Settings", "Patrol Helicopter max target distance", "150"));
            PowerlessTurrets = Convert.ToBoolean(GetConfig("Settings","Turrets need power? (Uses Button to power on/off)", "true"));
            NeedsPerms = Convert.ToBoolean(GetConfig("Settings","Needs permissions - (skyturrets.use)", "true"));
            Button = Convert.ToString(GetConfig("Settings","Default Power Button", "FIRE_THIRD"));
  
            SamSiteAttackDistance = Convert.ToInt32(GetConfig("Settings", "SamSite distance to start targetting", "300"));
            CheckForTargetEveryXSeconds = Convert.ToSingle(GetConfig("Settings", "SamSite Frequency Check for Patrol Helicopters in Seconds", "5"));

            if (ConfigChanged)
            {
                PrintWarning(lang.GetMessage("ConfigChanged", this));
                SaveConfig();
            }
            else
            {
                ConfigChanged = false;
                return;
            }
        }
   
        #region Config Reader
   
        private object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                ConfigChanged = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                ConfigChanged = true;
            }
            return value;
        }
   
        #endregion
   
        protected override void LoadDefaultConfig()
        {
            LoadVariables();
        }
   
        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(skyturretperms, this);
  
            data = Interface.Oxide.DataFileSystem.GetFile(Name);
  
            try
            {
                storedData = data.ReadObject<StoredData>();
                samIDs = storedData.samIDs;
                turretIDs = storedData.turretIDs;
                PoweredTurretIDs = storedData.PoweredTurretIDs;
            }
            catch
            {
                storedData = new StoredData();
            }
  
            plugin = this;
        }
   
        private void OnServerSave() => SaveData();
   
        void Unload()
        {
            foreach (var autoturret in UnityEngine.Object.FindObjectsOfType<AutoTurret>())
            {
                var at = autoturret.GetComponent<AntiHeli>();
                if (at)
                {
                    at.UnloadDestroy();
                    if (autoturret.IsOnline())
                    {
                        autoturret.SetIsOnline(false);
                        autoturret.SendNetworkUpdateImmediate();
                    }
                }
            }
  
            foreach (var sam in UnityEngine.Object.FindObjectsOfType<SamSite>())
            {
                var ss = sam.GetComponent<HeliTargeting>();
                if (ss)
                {
                    ss.UnloadDestroy();
                    sam.UpdateHasPower(0, 1);
                    sam.SendNetworkUpdateImmediate();
                }
            }
  
            SaveData();
            plugin = null;
        }
   
        void OnServerInitialized()
        {
            foreach (var autoturret in UnityEngine.Object.FindObjectsOfType<AutoTurret>())
            {
                if (turretIDs.Contains(autoturret.net.ID))
                {
                    autoturret.gameObject.AddComponent<AntiHeli>();
                    if (PoweredTurretIDs.Contains(autoturret.net.ID))
                    {
                        autoturret.SetIsOnline(true);
                        autoturret.SendNetworkUpdateImmediate();
                    }
                }    
            }
  
            foreach (var sam in UnityEngine.Object.FindObjectsOfType<SamSite>())
            {
                if (samIDs.Contains(sam.net.ID))
                {
                    sam.gameObject.AddComponent<HeliTargeting>();
                }
            }
        }
   
        #region PlayerInput
        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!PowerlessTurrets)
                TurretInput(input, player);
        }

        public void TurretInput(InputState input, BasePlayer player)
        {
            BUTTON.TryParse(Button, out PowerButton);
  
            if (input.WasJustPressed(PowerButton))
            {    
                RaycastHit hit;
                if (Physics.SphereCast(player.eyes.position, 0.5f, Quaternion.Euler(player.serverInput.current.aimAngles) * Vector3.forward, out hit))
                {
                    AutoTurret autoturret = hit.GetEntity()?.GetComponent<AutoTurret>();
                    SamSite samsite = hit.GetEntity()?.GetComponent<SamSite>();

                    if (autoturret != null)
                    {
                        var at = autoturret.GetComponent<AntiHeli>();
                        if (!at) return;

                        if (hit.distance >= 1.5) return;

                        if (autoturret.IsOnline() && autoturret.IsAuthed(player))
                        {
                            autoturret.SetIsOnline(false);
                            PoweredTurretIDs.Remove(autoturret.net.ID);
                        }
                        else
                        {
                            autoturret.SetIsOnline(true);
                            PoweredTurretIDs.Add(autoturret.net.ID);
                        }
                        autoturret.SendNetworkUpdateImmediate();
                    }

                    if (samsite != null)
                    {
                        var ss = samsite.GetComponent<HeliTargeting>();
                        if (!ss) return;
    
                        if (hit.distance >= 1.5) return;

                        if (samsite.IsPowered())
                        {
                            samsite.UpdateHasPower(0, 1);
                        }
                        else
                        {
                            samsite.UpdateHasPower(25, 1);
                        }
                        samsite.SendNetworkUpdateImmediate();
                    }
                }
            }
        }
  
        #endregion
  
        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"ConfigChanged", "Your configuration has changed"},
                {"Saving", "Data saved successfully"},
            }, this);
        }
        #endregion
   
        #region Hooks
        void OnEntitySpawned(AutoTurret turret)
        {
            if (turret.OwnerID != 0)
            {
                if (IsAllowed(turret))
                {
                    turret.gameObject.AddComponent<AntiHeli>();
                    turretIDs.Add(turret.net.ID);
                }
            }
        }
   
        void OnEntityKill(AutoTurret turret)
        {
            if (turret.OwnerID == 0) return;
  
            if (turretIDs.Contains(turret.net.ID))
            {
                turretIDs.Remove(turret.net.ID);
            }
  
            if (PoweredTurretIDs.Contains(turret.net.ID))
            {
                PoweredTurretIDs.Remove(turret.net.ID);
            }
        }
   
        void OnEntitySpawned(SamSite entity)
        {
            if (entity.OwnerID == 0) return;
  
            if (SamIsAllowed(entity))
            {
                entity.gameObject.AddComponent<HeliTargeting>();
                samIDs.Add(entity.net.ID);
            }
        }
   
        void OnEntityKill(SamSite entity)
        {
            samIDs.Remove(entity.net.ID);
        }
        #endregion
   
        #region Functions
        private static BasePlayer FindOwner(string nameOrId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrId)
                    return activePlayer;
                if (activePlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                    return activePlayer;
            }
            return null;
        }
   
        private bool IsAllowed(AutoTurret turret)
        {
            var player = FindOwner(turret.OwnerID.ToString());
            bool hasPermission = permission.UserHasPermission(player.UserIDString, skyturretperms);
  
            if (!NeedsPerms)
                return true;
            else
                return hasPermission;
        }
   
        private bool SamIsAllowed(SamSite sam)
        {
            var player = FindOwner(sam.OwnerID.ToString());
            bool hasPermission = permission.UserHasPermission(player.UserIDString, skyturretperms);
  
            if (!NeedsPerms)
                return true;
            else
                return hasPermission;
        }
        #endregion
   
        #region MonoBehaviour
        private class AntiHeli : MonoBehaviour
        {
            private AutoTurret turret;
            private BaseEntity entity;
            private BaseCombatEntity target;
  
            private void Awake()
            {
                turret = gameObject.GetComponent<AutoTurret>();
                entity = gameObject.GetComponent<BaseEntity>();
 
                var collider = entity.gameObject.AddComponent<SphereCollider>();
                collider.gameObject.layer = (int)Layer.Reserved1;
                collider.radius = plugin.AirTargetRadius;
                collider.isTrigger = true;
            }

            private void FixedUpdate()
            {
                if (target != null  && turret.IsOnline())
                {
                    turret.target = target;
                    turret.UpdateFacingToTarget();
                }
                else
                {
                    target = null;
                    turret.target = null;
                }
            }
  
            private void OnTriggerEnter(Collider col)
            {
                if (col.name == "patrol_helicopter" || col.name == "ch47_collider_front_motor")
                {
                    BaseCombatEntity heli = col.GetComponentInParent<BaseCombatEntity>();
                    target = heli;
                }
            }

            private void OnTriggerStay(Collider col)
            {
                if (col.name == "patrol_helicopter" || col.name == "ch47_collider_front_motor")
                {
                    BaseCombatEntity heli = col.GetComponentInParent<BaseCombatEntity>();
                    target = heli;
                }    
            }

            private void OnTriggerExit(Collider col)
            {
                if (col.name == "patrol_helicopter" || col.name == "ch47_collider_front_motor")
                {
                    turret.target = null;
                    target = null;
                }
            }
  
            public void UnloadDestroy()
            {
                target = null;
                turret.target = null;
                Destroy(this);
            }
  
            public void Destroy()
            {
                if (plugin.turretIDs.Contains(turret.net.ID))
                    plugin.turretIDs.Remove(turret.net.ID);
            }
        }
   
        private class HeliTargeting : MonoBehaviour
        {
            private SamSite samsite;
            private BaseEntity entity;
  
            private void Awake()
            {
                entity = gameObject.GetComponent<BaseEntity>();
                samsite = entity.GetComponent<SamSite>();
                samsite.UpdateHasPower(25, 1);
                samsite.SendNetworkUpdateImmediate();
                InvokeRepeating("FindTargets", plugin.CheckForTargetEveryXSeconds, 1.0f);
            }
  
            internal void FindTargets()
            {
                if (!samsite.IsPowered()) return;
 
                if (samsite.currentTarget.IsUnityNull<SamSite.ISamSiteTarget>())
                {
                    List<SamSite.ISamSiteTarget> nearby = Pool.GetList<SamSite.ISamSiteTarget>();
                    Vis.Entities(samsite.eyePoint.transform.position, plugin.SamSiteAttackDistance, nearby);

                    SamSite.ISamSiteTarget currentTarget1 = null;

                    foreach (SamSite.ISamSiteTarget currentTarget2 in nearby)
                    {
                        string prefabname = ((BaseCombatEntity) currentTarget2)?.PrefabName ?? string.Empty;
                        if (string.IsNullOrEmpty(prefabname)) return;
    
                        if (currentTarget2.CenterPoint().y >= samsite.eyePoint.transform.position.y && currentTarget2.IsVisible(samsite.eyePoint.transform.position, plugin.SamSiteAttackDistance * 2f)  && prefabname.Contains("patrolhelicopter.prefab") || prefabname.Contains("ch47scientists.entity.prefab") || prefabname.Contains("cargo_plane.prefab"))
                            currentTarget1 = currentTarget2;

                        Pool.FreeList<SamSite.ISamSiteTarget>(ref nearby);
                    }

                    samsite.currentTarget = currentTarget1;
                }
 
                if (!samsite.currentTarget.IsUnityNull<SamSite.ISamSiteTarget>())
                {
                    float distance = Vector3.Distance(samsite.transform.position, ((BaseCombatEntity) samsite.currentTarget).transform.position);
                    if (distance <= plugin.SamSiteAttackDistance)
                    {
                        samsite.InvokeRandomized(new Action(samsite.WeaponTick), 0.0f, 0.5f, 0.2f);
                    }
                    else if (distance > plugin.SamSiteAttackDistance)
                    {
                        samsite.currentTarget = null;
                        samsite.CancelInvoke(new Action(samsite.WeaponTick));
                    }
                }
            }

            public void UnloadDestroy()
            {
                Destroy(this);    
            }
  
            public void Destroy()
            {
                if (plugin.samIDs.Contains(samsite.net.ID))
                    plugin.samIDs.Remove(samsite.net.ID);
                CancelInvoke("FindTargets");
            }
        }
        #endregion
    }
}
