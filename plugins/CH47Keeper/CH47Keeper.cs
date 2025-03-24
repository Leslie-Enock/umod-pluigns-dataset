using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("CH47 Keeper", "Homi", "0.1.1")]
    [Description("Restore position and rotation of CH47 own by user when the server restarts.")]
    public class CH47Keeper : RustPlugin
    {
        #region config
        class CH47Data
        {
            public ulong ownerid;
            public Vector3 pos;
            public float rotX;
            public float rotY;
            public float rotZ;
            public float rotW;
        }
        #endregion

        #region Hooks
        private void OnServerInitialized()
        {
            ch47_Data = Interface.Oxide.DataFileSystem.ReadObject<List<CH47Data>>("CH47Keeper");
            if (!CheckIsWipe())
            {
                RestoreCH47();
            }
        }
        void Unload()
        {
            SaveCH47();
        }
        private void OnServerSave()
        {
            SaveCH47();
        }
        #endregion

        #region Honmono
        private readonly DynamicConfigFile dataFile1 = Interface.Oxide.DataFileSystem.GetFile("CH47Keeper");
        private List<CH47Data> ch47_Data = new List<CH47Data>();
        private void SaveCH47()
        {
            if (ch47_Data.Count > 0)
            {
                ch47_Data.Clear();
            }
            foreach (BaseNetworkable bna in BaseNetworkable.serverEntities.entityList.Values)
            {
                if (bna.ShortPrefabName == "ch47.entity")
                {
                    BaseEntity ent = (BaseEntity)bna;
                    if (!(ent.transform.position.x < -5000) && !(ent.transform.position.x > 5000) && !(ent.transform.position.y > 5000) && !(ent.transform.position.z < -5000) && !(ent.transform.position.z > 5000))
                    {
                        ch47_Data.Add(new CH47Data() { ownerid = ent.OwnerID, pos = ent.transform.position, rotX = ent.transform.rotation.x, rotY = ent.transform.rotation.y, rotZ = ent.transform.rotation.z, rotW = ent.transform.rotation.w });
                    }
                }
            }
            dataFile1.WriteObject(ch47_Data);
            Puts("CH47 Save");
        }
        private void RestoreCH47()
        {
            foreach (CH47Data ch47 in ch47_Data)
            {
                bool ishere = false;
                foreach (BaseNetworkable bna in BaseNetworkable.serverEntities.entityList.Values)
                {
                    if (bna.ShortPrefabName == "ch47.entity" && Vector3.Distance(bna.transform.position, ch47.pos) < 1)
                    {
                        ishere = true;
                    }
                }
                if (!ishere)
                {
                    BaseEntity ent = GameManager.server.CreateEntity("assets/prefabs/npc/ch47/ch47.entity.prefab", ch47.pos, new Quaternion() { x = ch47.rotX, y = ch47.rotY, z = ch47.rotZ, w = ch47.rotW }, true);
                    if (ent == null) { PrintWarning("Entity is Null"); }
                    ent.OwnerID = ch47.ownerid;
                    ent.Spawn();
                }
            }
        }
        private bool CheckIsWipe()
        {
            foreach (BaseNetworkable bna in BaseNetworkable.serverEntities.entityList.Values)
            {
                if (bna.ShortPrefabName.Contains("foundation") || (BasePlayer.activePlayerList.Count > 1) || (BasePlayer.sleepingPlayerList.Count > 1))
                {
                    return false;
                }
            }
            return true;
        }
        #endregion
    }
}