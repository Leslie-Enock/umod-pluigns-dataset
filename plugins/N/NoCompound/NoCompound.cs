using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("No Compound", "Orange", "1.0.1")]
    [Description("Removing compound (outpost) and bandit camp components (turrets, bots, safe zones)")]
    public class NoCompound : RustPlugin
    {
        #region Vars

        private const float distance = 100f;
        private List<Vector3> banditTown = new List<Vector3>();
        private List<Vector3> compound = new List<Vector3>();
        private bool nearCompound(Vector3 pos) => compound.Any(x => Vector3.Distance(pos, x) < distance);
        private bool nearBanditTown(Vector3 pos) => banditTown.Any(x => Vector3.Distance(pos, x) < distance);
        
        #endregion

        #region Oxide Hooks

        private void Init()
        {
            if (config.removeHostileTimer == false)
            {
                Unsubscribe(nameof(OnEntityMarkHostile));
            }
        }

        private void OnServerInitialized()
        {
            CheckObjects();            
        }
        
        private void OnEntitySpawned(NPCPlayer player)
        {
            NextTick(() =>
            {
                CheckPlayer(player);
            });
        }
        
        private object OnEntityMarkHostile(BaseCombatEntity entity, float duration)
        {
            return true;
        }

        #endregion

        #region Core

        private void CheckObjects()
        {
            foreach (var obj in UnityEngine.Object.FindObjectsOfType<MonumentInfo>())
            {
                if (obj.name.Contains("compound"))
                {
                    compound.Add(obj.transform.position);
                }

                if (obj.name.Contains("bandit_town"))
                {
                    banditTown.Add(obj.transform.position);
                }
            }
            
            if (config.removeTurrets == true)
            {
                foreach (var obj in UnityEngine.Object.FindObjectsOfType<NPCAutoTurret>())
                {
                    if (obj.OwnerID == 0)
                    {
                        UnityEngine.Object.Destroy(obj);
                    }
                }
            }

            if (config.removeNPCCompound == true || config.removeNPCBanditTown == true)
            {
                foreach (var obj in UnityEngine.Object.FindObjectsOfType<NPCPlayer>())
                {
                    CheckPlayer(obj);
                }
            }

            if (config.removeBanditCampSZ == true || config.removeCompoundCampSZ == true)
            {
                foreach (var obj in UnityEngine.Object.FindObjectsOfType<TriggerSafeZone>())
                {
                    CheckSafeZone(obj);
                }
            }
        }

        private void CheckPlayer(NPCPlayer player)
        {
            if (player.IsValid() == false || player.IsDestroyed == true)
            {
                return;
            }

            if (player is VehicleVendor)
            {
                if (config.removeVehicleVendor == true)
                {
                    player.Kill();
                }
                
                return;
            }

            if (config.removeNPCCompound == true && nearCompound(player.transform.position))
            {
                player.Kill();
                return;
            }

            if (config.removeNPCBanditTown == true && nearBanditTown(player.transform.position))
            {
                player.Kill();
                return;
            }
        }

        private void CheckSafeZone(TriggerSafeZone trigger)
        {
            if (config.removeBanditCampSZ == true && nearBanditTown(trigger.transform.position))
            {
                UnityEngine.Object.Destroy(trigger);
                return;
            }
            
            if (config.removeCompoundCampSZ == true && nearCompound(trigger.transform.position))
            {
                UnityEngine.Object.Destroy(trigger);
                return;
            }
        }

        #endregion
        
        #region Configuration | 24.05.2020

        private static ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Remove NPC Turrets")]
            public bool removeTurrets = false;
            
            [JsonProperty(PropertyName = "Remove vehicle vendors")]
            public bool removeVehicleVendor = false;
            
            [JsonProperty(PropertyName = "Remove Compound safe zone")]
            public bool removeCompoundCampSZ = false;

            [JsonProperty(PropertyName = "Remove Compound npc-s")]
            public bool removeNPCCompound = false;
            
            [JsonProperty(PropertyName = "Remove Bandit Camp safe zone")]
            public bool removeBanditCampSZ = false;
            
            [JsonProperty(PropertyName = "Remove Bandit Camp npc-s")]
            public bool removeNPCBanditTown = false;

            [JsonProperty(PropertyName = "Remove hostile timer")]
            public bool removeHostileTimer = false;
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
                for (var i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }
                
                LoadDefaultConfig();
                return;
            }

            ValidateConfig();
            SaveConfig();
        }

        private void ValidateConfig()
        {
            if (ConVar.Server.hostname.Contains("[DEBUG]") == true)
            {
                PrintWarning("Using default configuration on debug server");
                config = new ConfigData();
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion
    }
}