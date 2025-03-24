using System.Collections.Generic;
using System.Text;
using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

#region Changelogs and ToDo

/**********************************************************************
1.0.1   :   Blocked TC clearlist
**********************************************************************/

#endregion
namespace Oxide.Plugins
{
    [Info("No Deauth", "Krungh Crow", "1.0.1")]
    [Description("Prevent Deauthing from TC's")]

    class NoDeauth : RustPlugin
    {
        #region Variables

        const string Bypass_Perm = "nodeauth.bypass";

        #endregion

        #region Configuration
        void Init()
        {
            if (!LoadConfigVariables())
            {
                Puts("Config file issue detected. Please delete file, or check syntax and fix.");
                return;
            }
            permission.RegisterPermission(Bypass_Perm, this);
        }

        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Global")]
            public SettingsGlobal CFG = new SettingsGlobal();
        }

        class SettingsGlobal
        {
            [JsonProperty(PropertyName = "Prevent deauthing")]
            public bool GlobalDA = true;
        }

        private bool LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConf();
            return true;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Fresh install detected Creating a new config file.");
            configData = new ConfigData();
            SaveConf();
        }

        void SaveConf() => Config.WriteObject(configData, true);
        #endregion

        #region LanguageAPI
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoDeauthMSG"] = "<color=red>Deauthing is Prohibited on this server!</color>",
                ["Prefix"] = "[<color=green>No Deauth</color>] ",
            }, this);
        }

        #endregion

        #region Hooks

        object OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (player != null)
            if (permission.UserHasPermission(player.UserIDString, Bypass_Perm) || configData.CFG.GlobalDA == false) return null;
            {
                string prefix = lang.GetMessage("Prefix", this, player.UserIDString);
                player.ChatMessage(prefix + msg("NoDeauthMSG", player.UserIDString));
            }
            return true;
        }

        object OnCupboardClearList(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (player != null)
            if (permission.UserHasPermission(player.UserIDString, Bypass_Perm) || configData.CFG.GlobalDA == false) return null;
            {
                string prefix = lang.GetMessage("Prefix", this, player.UserIDString);
                player.ChatMessage(prefix + msg("NoDeauthMSG", player.UserIDString));
            }
            return true;
        }
        #endregion

        #region Helpers

        string msg(string key, string id = null) => lang.GetMessage(key, this, id);

        #endregion
    }
}
