using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using Oxide.Core;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("CupboardProtection", "Wulf/lukespragg and birthdates Maintained by NooBlet", "1.9.4")]
    [Description("Makes cupboards and their foundations invulnerable, unable to be destroyed.")]

    public class CupboardProtection : RustPlugin
    {
        private readonly int Mask = LayerMask.GetMask("Construction");

        #region Hooks

        private object OnEntityTakeDamage(DecayEntity entity, HitInfo info)
        {
            var Initiator = info.Initiator;
            if (!Initiator) return null;

            if (entity.name.Contains("cupboard"))
            {
                if (Configuration.OwnerCanDoDamage)
                {
                    if (Configuration.teamCanDoDamage)
                    {
                        if (info.InitiatorPlayer != null && canBreakTC(info.InitiatorPlayer, entity)) return null;
                    }

                    if (info.InitiatorPlayer != null && info.InitiatorPlayer.userID == entity.OwnerID) return null;
                }
                return CHook("CanDamageTc", Initiator, entity);
            }

            if (!Configuration.foundation || !entity.name.Contains("foundation")) return null;
           
           
            return IDData.IDs.Values.ToList().Exists(id => id == entity.net.ID.Value) ? CHook("CanDamageTcFloor", Initiator, entity) : null;
        }

        private static object CHook(string Name, BaseEntity Player, DecayEntity Entity)
        {
            var Hook = Interface.CallHook(Name, Player, Entity);
            return Hook is bool ? Hook : false;
        }

        private void Init()
        {
            LoadConfig();
            if (!Configuration.foundation)
            {
                Unsubscribe("OnEntityBuilt");
                Unsubscribe("OnEntityKill");
            }           
            timer10m();
            IDData = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);
        }

        private void OnNewSave(string filename)
        {
            Puts("Wipe Detected Clearing Data File");
            IDData.IDs.Clear();
            SaveData();
        }

        private void Unload() => SaveData();

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            if (go == null)
            {
                return;
            }


            var Priv = go.GetComponent<BaseEntity>() as BuildingPrivlidge;
            if (Priv == null)
            {
                return;
            }


            Vector3 oldPosition = Priv.transform.position;
            Vector3 tempPosition = new Vector3(Priv.transform.position.x, Priv.transform.position.y + 1F, Priv.transform.position.z);
            Priv.transform.position = tempPosition;
            var Foundation = GetFoundation(Priv);
            Priv.transform.position = oldPosition;


            if (Foundation == null)
            {
                return;
            }


            IDData.IDs.Add(Priv.net.ID.Value, Foundation.net.ID.Value);
            SaveData();
        }
        //private void OnEntityBuilt(Planner plan, GameObject go)
        //{
        //    var Priv = go.GetComponent<BaseEntity>() as BuildingPrivlidge;
        //    if (!Priv) return;
        //    Vector3 oldPosition = Priv.transform.position;
        //    Vector3 tempPosition = new Vector3(Priv.transform.position.x, Priv.transform.position.y + 1F, Priv.transform.position.z);
        //    Priv.transform.position = tempPosition;
        //    var Foundation = GetFoundation(Priv);
        //    Priv.transform.position = oldPosition;
        //    if (!Foundation) return;           
        //    IDData.IDs.Add(Priv.net.ID.Value, Foundation.net.ID.Value);
        //}

        private BuildingBlock GetFoundation(BuildingPrivlidge Priv) => Physics.RaycastAll(Priv.transform.position, Vector3.down, 2f, Mask, QueryTriggerInteraction.Ignore).Select(Hit => Hit.GetEntity() as BuildingBlock).FirstOrDefault(E => E);

        private void OnEntityKill(BuildingPrivlidge entity)
        {
            if (IDData.IDs.ContainsKey(entity.net.ID.Value))
            {
                IDData.IDs.Remove(entity.net.ID.Value);
            }
        }
        #endregion

        #region Methods

        void CheckTCs()
        {
            foreach (var go in BuildingPrivlidge.serverEntities)
            {
                var Priv = go.GetComponent<BaseEntity>() as BuildingPrivlidge;
                if (!Priv) continue;
                if (IDData.IDs.ContainsKey(Priv.net.ID.Value)) { continue; }
                Vector3 oldPosition = Priv.transform.position;
                Vector3 tempPosition = new Vector3(Priv.transform.position.x, Priv.transform.position.y + 1F, Priv.transform.position.z);
                Priv.transform.position = tempPosition;
                var Foundation = GetFoundation(Priv);
                Priv.transform.position = oldPosition;
                if (!Foundation) continue;
                IDData.IDs.Add(Priv.net.ID.Value, Foundation.net.ID.Value);
            }
        }
        private void timer10m()
        {
            timer.Every(600f, () =>
            {
                CheckTCs();

            });
        }
        private bool canBreakTC(BasePlayer player, DecayEntity Entity)
        {
            foreach(var p in player.currentTeam.ToString())
            {
                if (p == Entity.OwnerID) { return true; }
            }
            return false;
        }

        #endregion Methods

        #region Data
        private Data IDData;

        private class Data
        {
            public readonly Dictionary<ulong, ulong> IDs = new Dictionary<ulong, ulong>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, IDData);
        }
        #endregion

        #region Configuration

        private ConfigFile Configuration;

        public class ConfigFile
        {

            [JsonProperty("Foundation Invincible?")]
            public bool foundation;
            [JsonProperty("TC Owner can Damage TC and Foundation?")]
            public bool OwnerCanDoDamage;
            [JsonProperty("Team can Damage TC and Foundation?")]
            public bool teamCanDoDamage;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    foundation = true, OwnerCanDoDamage = false, teamCanDoDamage = false                   

                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Configuration = Config.ReadObject<ConfigFile>();
            if (Config == null)
            {
                LoadDefaultConfig();
            }
        }
       

        protected override void LoadDefaultConfig()
        {
            Configuration = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");

        }

        protected override void SaveConfig()
        {
            Config.WriteObject(Configuration);
        }
        #endregion
    }
}