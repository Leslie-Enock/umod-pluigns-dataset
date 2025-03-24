using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Free Light String", "mspeedie", "0.1.2")]
    [Description("Allows lightstrings to operate without electricity.")]
    
    class FreeLightString : RustPlugin
    {
        #region Initialization
        void OnServerInitialized()
        {
            ChangePower(999999);
        }
        #endregion
        
        #region lightstring
        void OnEntitySpawned(AdvancedChristmasLights lightstring)
        {
            lightstring.UpdateHasPower(999999, 1);
            lightstring.SendNetworkUpdateImmediate();
        }
         
        void ChangePower(int amt)
        {
            foreach (var lightstring in UnityEngine.Object.FindObjectsOfType<AdvancedChristmasLights>())
            {
                lightstring.UpdateHasPower(amt, 1);
				lightstring.SendNetworkUpdateImmediate();
            }
        }
        void Unload()
        {
            ChangePower(0);
        }
        #endregion
    }
}