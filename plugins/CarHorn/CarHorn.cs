using System.Collections.Generic;
using UnityEngine;
using System;

namespace Oxide.Plugins
{
    [Info("Car Horn", "dFxPhoeniX", "1.1.6")]
    [Description("Adds an FX similar to that of a car horn to the driver in the sedan.")]
    class CarHorn : RustPlugin
    {
        #region variables
        private bool Changed = false;
        private string hornPrefab = "assets/prefabs/instruments/bass/effects/guitardeploy.prefab";
        private const string permissionUse = "carhorn.use";
        #endregion
            
        #region Main plugin
        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            LoadVariables();
        }

        private void OnPlayerInput(BasePlayer player, InputState input, BaseEntity entity)
        {
            if (!permission.UserHasPermission(player.UserIDString, permissionUse))
            {
                return;
            }

            if (player.GetMounted() == null)
                return;

            if (!input.IsDown(BUTTON.FIRE_SECONDARY))
                return;

            if (player.isMounted && player.GetMounted().name.Contains("driverseat"))
            {
                Effect.server.Run(hornPrefab, player.transform.position);
            }

        }



        void Init()
        {
            LoadVariables();
            permission.RegisterPermission(permissionUse, this);
        }

        #endregion 

        #region Config
        void LoadVariables()
        {
            hornPrefab = Convert.ToString(GetConfig("Horn Prefab", "FX used", "assets/prefabs/instruments/bass/effects/guitardeploy.prefab"));
            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }


        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }
        #endregion
    }
}