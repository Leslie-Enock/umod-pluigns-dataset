
using Oxide.Core;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;



namespace Oxide.Plugins
{
    [Info("Mount Logger", "BaronVonFinchus", "1.6")]
    [Description("A simple plugin to track the location of mounted entities. Logs player name, ID and position to a file. Useful for PvE servers.")]
    public class MountLogger : RustPlugin
    {

        private PluginConfig _config;

        void Init()
        {
            _config = Config.ReadObject<PluginConfig>();
            permission.RegisterPermission("mountlogger.use", this);
            permission.RegisterPermission("mountlogger.scrapheli.log", this);
            permission.RegisterPermission("mountlogger.miniheli.log", this);
            permission.RegisterPermission("mountlogger.modularcar.log", this);
            permission.RegisterPermission("mountlogger.ridablehorse.log", this);
            permission.RegisterPermission("mountlogger.smallboat.log", this);
            permission.RegisterPermission("mountlogger.rhibboat.log", this);
            permission.RegisterPermission("mountlogger.cranedriver.log", this);
            permission.RegisterPermission("mountlogger.trackpassengers.log", this);
        }

        void OnServerInitialized()
        {
            //Puts("MountLogger Loaded");
        }

        private void Unload()
        {
            //Puts("MountLogger Unloaded");
        }

        private void PrintToConsole(string message)
        {
            if (message == null)
            {
                return;
            }
            Puts(message);
        }

        private void PrintToFile(string message)
        {
            if (message == null)
            {
                return;
            }
            var timestamp = DateTime.Now.ToString("HH:mm:ss");
            message = $"{timestamp} | {message}";
            LogToFile("common", message, this);
        }




        string getGrid(Vector3 pos) { //Credit: yetzt
			char letter = 'A';
			var x = Mathf.Floor((pos.x+(ConVar.Server.worldsize/2)) / 146.3f)%26;
			var z = (Mathf.Floor(ConVar.Server.worldsize/146.3f))-Mathf.Floor((pos.z+(ConVar.Server.worldsize/2)) / 146.3f);
			letter = (char)(((int)letter)+x);
			return $"{letter}{z}";
		}





        #region Mount hooks

        void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, "mountlogger.use"))
            {
                if (!player.IsConnected)
                {
                    return;
                }
                //Discarded logs for mounted entities - Chairs, Kyaks, Instruments, Summer DLC items, Christmas Sled, Computer station
                if ((entity.ShortPrefabName == "chair.invisible.static") || (entity.ShortPrefabName == "chair.deployed") || (entity.ShortPrefabName == "xylophone.deployed") || (entity.ShortPrefabName == "xylophone.deployed.static") || (entity.ShortPrefabName == "sofaseat") || (entity.ShortPrefabName == "piano.deployed") || (entity.ShortPrefabName == "piano.deployed.static") || (entity.ShortPrefabName == "drumkit.deployed") || (entity.ShortPrefabName == "drumkit.deployed.static") || (entity.ShortPrefabName == "sledseatfront") || (entity.ShortPrefabName == "sledseatrear") || (entity.ShortPrefabName == "kayakseat") || (entity.ShortPrefabName == "boogieboard.deployed") || (entity.ShortPrefabName == "innertube.deployed") || (entity.ShortPrefabName == "computerstation.deployed") || (entity.ShortPrefabName == "beachchair.deployed") || (entity.ShortPrefabName == "cardtableseat") || (entity.ShortPrefabName == "workcartdriver") || (entity.ShortPrefabName == "arcadeuser") || (entity.ShortPrefabName == "chair.static"))
                {
                    return;
                }
                //Passengers
                if ((entity.ShortPrefabName == "modularcarpassengerseatlesslegroomleft") || (entity.ShortPrefabName == "modularcarpassengerseatlesslegroomright") || (entity.ShortPrefabName == "modularcarpassengerseatright") || (entity.ShortPrefabName == "modularcarpassengerseatlesslegroomright") || (entity.ShortPrefabName == "transporthelicopilot") || (entity.ShortPrefabName == "minihelipassenger") || (entity.ShortPrefabName == "smallboatpassenger") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.trackpassengers.log")))
                {
                    return;
                }
                //Scrap Heli
                if ((entity.ShortPrefabName == "transporthelipilot") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.scrapheli.log")))
                {
                    return;
                }
                //Minicopter
                if ((entity.ShortPrefabName == "miniheliseat") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.miniheli.log")))
                {
                    return;
                }
                //Modular Cars
                if ((entity.ShortPrefabName == "modularcardriverseat") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.modularcar.log")))
                {
                    return;
                }
                //Horses
                if ((entity.ShortPrefabName == "saddletest") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.ridablehorse.log")))
                {
                    return;
                }
                if ((entity.ShortPrefabName == "craneoperator") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.cranedriver.log")))
                {
                    return;
                }
                //Rowboat
                if ((entity.ShortPrefabName == "smallboatdriver") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.smallboat.log")))
                {
                    return;
                }
                //RHIB
                if ((entity.ShortPrefabName == "standingdriver") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.rhibboat.log")))
                {
                    return;
                }
                string GridPos = getGrid(entity.transform.position);
                LogKey(
                    "OnEntityMount",
                    entity.ShortPrefabName,
                    player,
                    GridPos,
                    entity.transform.position);
            }
            else
            {
                return;
            }
        }


        void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, "mountlogger.use"))
            {
                if (!player.IsConnected)
                {
                    return;
                }
                //Discarded logs for mounted entities - Chairs, Kyaks, Instruments, Summer DLC items, Christmas Sled, Computer station
                if ((entity.ShortPrefabName == "chair.invisible.static") || (entity.ShortPrefabName == "chair.deployed") || (entity.ShortPrefabName == "xylophone.deployed") || (entity.ShortPrefabName == "xylophone.deployed.static") || (entity.ShortPrefabName == "sofaseat") || (entity.ShortPrefabName == "piano.deployed") || (entity.ShortPrefabName == "piano.deployed.static") || (entity.ShortPrefabName == "drumkit.deployed") || (entity.ShortPrefabName == "drumkit.deployed.static") || (entity.ShortPrefabName == "sledseatfront") || (entity.ShortPrefabName == "sledseatrear") || (entity.ShortPrefabName == "kayakseat") || (entity.ShortPrefabName == "boogieboard.deployed") || (entity.ShortPrefabName == "innertube.deployed") || (entity.ShortPrefabName == "computerstation.deployed") || (entity.ShortPrefabName == "beachchair.deployed") || (entity.ShortPrefabName == "cardtableseat") || (entity.ShortPrefabName == "workcartdriver") || (entity.ShortPrefabName == "arcadeuser") || (entity.ShortPrefabName == "chair.static"))
                {
                    return;
                }
                //Passengers
                if ((entity.ShortPrefabName == "modularcarpassengerseatlesslegroomleft") || (entity.ShortPrefabName == "modularcarpassengerseatlesslegroomright") || (entity.ShortPrefabName == "modularcarpassengerseatright") || (entity.ShortPrefabName == "modularcarpassengerseatlesslegroomright") || (entity.ShortPrefabName == "transporthelicopilot") || (entity.ShortPrefabName == "minihelipassenger") || (entity.ShortPrefabName == "smallboatpassenger") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.trackpassengers.log")))
                {
                    return;
                }
                //Scrap Heli
                if ((entity.ShortPrefabName == "transporthelipilot") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.scrapheli.log")))
                {
                    return;
                }
                //Minicopter
                if ((entity.ShortPrefabName == "miniheliseat") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.miniheli.log")))
                {
                    return;
                }
                //Modular Cars
                if ((entity.ShortPrefabName == "modularcardriverseat") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.modularcar.log")))
                {
                    return;
                }
                //Horses
                if ((entity.ShortPrefabName == "saddletest") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.ridablehorse.log")))
                {
                    return;
                }
                if ((entity.ShortPrefabName == "craneoperator") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.cranedriver.log")))
                {
                    return;
                }
                //Rowboat
                if ((entity.ShortPrefabName == "smallboatdriver") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.smallboat.log")))
                {
                    return;
                }
                //RHIB
                if ((entity.ShortPrefabName == "standingdriver") && (!permission.UserHasPermission(player.UserIDString, "mountlogger.rhibboat.log")))
                {
                    return;
                }
                string GridPos = getGrid(entity.transform.position);
                LogKey(
                    "OnEntityDismount",
                    entity.ShortPrefabName,
                    player,
                    GridPos,
                    entity.transform.position);
            }
            else
            {
                return;
            }
        }



        #endregion

        #region Messages

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new RuLocalization(), this, "ru");
            lang.RegisterMessages(new DefaultLocalization(), this);
        }
        protected override void LoadDefaultConfig() => Config.WriteObject(new PluginConfig(), true);

        private void LogKey(string key, params object[] args) =>
            LogMessage(this[key, args]);

        private void LogMessage(string message)
        {
            if (_config.PrintToConsole) PrintToConsole(message);
            if (_config.PrintToFile) PrintToFile(message);
        }

        private string this[string key, params object[] args] => args?.Any() == true
            ? string.Format(lang.GetMessage(key, this), args)
            : lang.GetMessage(key, this);

        #endregion
    
        #region MountLogger.Localization

        private class DefaultLocalization : Dictionary<string, string>
        {
            public DefaultLocalization()
            {
                this["OnEntityMount"] = "A {0} was mounted by '{1}' at Grid {2}";
                this["OnEntityDismount"] = "A {0} was dismounted by '{1}' at Grid {2}";
            }
        }
    
        private class RuLocalization : Dictionary<string, string>
        {
            public RuLocalization()
            {
                this["OnEntityMount"] = "{0} mounted by {1} at {2)";
                this["OnEntityDismount"] = "{0} dismounted by {1} at {2}";
            }
        }
    
        #endregion
    
        #region MountLogger.Models

        private class PluginConfig
        {
            [JsonProperty("Print logs to console")]
            public bool PrintToConsole { get; set; } = true;
    
            [JsonProperty("Print logs to file")]
            public bool PrintToFile { get; set; } = true;
    
        }
    
        private class PluginData
        {
            public bool FirstStart { get; set; } = true;
        }
    
        #endregion
    }
}
