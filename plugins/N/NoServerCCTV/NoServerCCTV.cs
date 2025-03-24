using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("No Server CCTV Camaras", "NooBlet", "0.1.6")]
    [Description("Renames default Server CCTV camara identifiers")]
    public class NoServerCCTV : CovalencePlugin
    {
        #region Globel Varibles

       
        Dictionary<string, String> identifiers = new Dictionary<string, String>();

        #endregion Globel Varibles


        #region Hooks

        void OnServerInitialized()
        {
           
            SetServerCCTVs();
        }

        void Unload()
        {
            ResetServerCCTVnames();
        }

        #endregion Hooks


        #region Methods

      

        CCTV_RC[] GetCCTVs()
        {
            return GameObject.FindObjectsOfType<CCTV_RC>();
        }

        void SetServerCCTVs()
        {
            foreach (var item in GetCCTVs())
            {
                if (item.OwnerID == 0)
                {
                    var id = Guid.NewGuid().ToString();
                    identifiers[id] = item.GetIdentifier();
                    item.UpdateIdentifier(id);                 
                    
                }
               
            }
           
        }

        void ResetServerCCTVnames()
        {
            foreach (var item in GetCCTVs())
            {
                if (item.OwnerID == 0)
                {
                    item.UpdateIdentifier(identifiers[item.GetIdentifier()]);
                    item._name = identifiers[item.GetIdentifier()];
                }

            }

        }
        #endregion Methods

    }
}
