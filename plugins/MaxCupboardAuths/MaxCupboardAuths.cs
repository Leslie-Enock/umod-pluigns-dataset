using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Facepunch;

#region Changelogs and ToDo
/**********************************************************************
*   2.0.0   :   Complete rewrite to have a better functionality
*               - Removed Chatcommands
*               - New permissions
*               - Restricions now only on each Toolcupboard
*               - No more datafile system
*               - Builded in the checker for TC with independant auths
*               - Added the restrictions for total auths
*   2.0.1   :   - Added extra bypas checks (thx @Yzarul)
*               
**********************************************************************/
#endregion

namespace Oxide.Plugins
{
    [Info("MaxCupboardAuths", "Krungh Crow", "2.0.1")]
    [Description("Limit cupboard max authing")]

    class MaxCupboardAuths : RustPlugin
    {
        #region Variables

        bool debug = false;

        const ulong chaticon = 0;
        const string prefix = "<color=yellow>[M.C.A.]</color> ";

        const string Max_Perm = "maxcupboardauths.restrict";            //  Restricts authing limit for every tc
        const string MaxTC_Perm = "maxcupboardauths.tcrestrict";        //  Restricts players personal max authing limit
        const string MaxTCDef_Perm = "maxcupboardauths.tcdefault";      //  Default perm max TC to auth on
        const string MaxTCVip_Perm = "maxcupboardauths.tcvip";          //  Vip perm max TC to auth on
        const string Bypass_Perm = "maxcupboardauths.bypass";           //  Perm to bypass restrictions

        #endregion

        #region Configuration
        void Init()
        {
            if (!LoadConfigVariables())
            {
            Puts("Config file issue detected. Please delete file, or check syntax and fix.");
            return;
            }
            if (configData.UseDebug)
            {
                debug = true;
            }
            permission.RegisterPermission(Max_Perm, this);
            permission.RegisterPermission(MaxTC_Perm, this);
            permission.RegisterPermission(Bypass_Perm, this);
            permission.RegisterPermission(MaxTCDef_Perm, this);
            permission.RegisterPermission(MaxTCVip_Perm, this);
        }

        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Put Debug to console")]
            public bool UseDebug = false;
            [JsonProperty(PropertyName = "Settings ToolCupboard")]
            public SettingsTCAuth TCAuths = new SettingsTCAuth();
            [JsonProperty(PropertyName = "Settings Players")]
            public SettingsPlayerAuth PlayerAuths = new SettingsPlayerAuth();
        }

        class SettingsTCAuth
        {
            [JsonProperty(PropertyName = "Max total Auths for each TC")]
            public int MaxTCAuth = 2;
        }

        class SettingsPlayerAuth
        {
            [JsonProperty(PropertyName = "Max Auths for default Player")]
            public int MaxAuth = 2;
            [JsonProperty(PropertyName = "Max Auths for vip Player")]
            public int MaxAuthVip = 3;
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
                ["Denied"] = "You cannot authorise to this cupboard! there are already the maximum amount of people authed to it!",
                ["DeniedPlayer"] = "You reached your personal Tc auth limit",
                ["DeniedPlacement"] = "You cannot place a tc u have reached your limit of Toolcupboards to auth to!",
            }, this);
        }

        #endregion

        #region Methods
        // Added to avoid players placing the tc when allready on max auths (global)
        void OnEntitySpawned(BaseEntity entity, UnityEngine.GameObject gameObject)
        {
            if (entity.ShortPrefabName.Contains("cupboard.tool"))
            {
                if (entity.OwnerID == null) return;
                BasePlayer player = BasePlayer.FindByID(entity.OwnerID);
                if (debug) Puts($"{player} placed a Toolcupboard");
                if (player == null) return;
                if (player.IsSleeping() == true || player.IsConnected == false) return;
                if (debug) Puts($"sleep|offline check");

                if (permission.UserHasPermission(player.UserIDString, Bypass_Perm))
                {
                    if (debug) Puts($"{player} has bypass privilege and placed a cupboard");
                    return;
                }

                if (permission.UserHasPermission(player.UserIDString, MaxTCVip_Perm))
                {
                    if (TCcount(player) >= configData.PlayerAuths.MaxAuthVip)
                    {
                        Player.Message(player, prefix + string.Format(msg($"DeniedPlacement", player.UserIDString)), chaticon);

                        NextTick(() =>
                        {
                            entity.Kill();
                            var itemtogive = ItemManager.CreateByItemID(-97956382, 1);
                            if (itemtogive != null) player.inventory.GiveItem(itemtogive);
                            entity.SendNetworkUpdate();
                        });
                    }
                }
                else if (permission.UserHasPermission(player.UserIDString, MaxTCDef_Perm))
                {
                    if (TCcount(player) >= configData.PlayerAuths.MaxAuth)
                    {
                        Player.Message(player, prefix + string.Format(msg($"DeniedPlacement", player.UserIDString)), chaticon);

                        NextTick(() =>
                        {
                            entity.Kill();
                            var itemtogive = ItemManager.CreateByItemID(-97956382, 1);
                            if (itemtogive != null) player.inventory.GiveItem(itemtogive);
                            entity.SendNetworkUpdate();
                        });
                    }
                }
            }
            return;
        }

        object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            // Restricts authing limit for players personal limits
            if (permission.UserHasPermission(player.UserIDString, Bypass_Perm))
            {
                if (debug) Puts($"{player} has bypass privilege and authed on a cupboard");
                return null;
            }

            if (permission.UserHasPermission(player.UserIDString, MaxTC_Perm))
            {
                if (permission.UserHasPermission(player.UserIDString, MaxTCVip_Perm))
                {
                    if (TCcount(player) >= configData.PlayerAuths.MaxAuthVip)
                    {
                        //int maxauth = configData.PlayerAuths.MaxAuthVip;
                        Player.Message(player, prefix + string.Format(msg($"DeniedPlayer", player.UserIDString)), chaticon);

                        if (debug) Puts($"{player} has allready authed to {TCcount(player)} cupboard(s) and authing was denied");
                        return true;
                    }
                }

                else if (permission.UserHasPermission(player.UserIDString, MaxTCDef_Perm))
                {
                    if (TCcount(player) >= configData.PlayerAuths.MaxAuth)
                    {
                        //int maxauth = configData.PlayerAuths.MaxAuth;
                        Player.Message(player, prefix + string.Format(msg($"DeniedPlayer", player.UserIDString)), chaticon);

                        if (debug) Puts($"{player} has allready authed to {TCcount(player)} cupboard(s) and authing was denied");
                        return true;
                    }
                }
            }

            // Restricts authing limits for each TC
            if (permission.UserHasPermission(player.UserIDString, Max_Perm))
            {
                if (privilege.authorizedPlayers.Count >= configData.TCAuths.MaxTCAuth)
                {
                    Player.Message(player, prefix + string.Format(msg("Denied", player.UserIDString)), chaticon);

                    if (debug)
                    {
                        int Maxauth = configData.TCAuths.MaxTCAuth;
                        Puts($"{player} was denied authing on a cupboard since its limit of {Maxauth} was reached!");
                    }
                    return true;
                }

                if (debug) Puts($"{player} was allowed to auth");
                return null;
            }
                if (debug) Puts($"{player} has no auth restrictions and authed on {TCcount(player)} cupboard(s)");
            return null;
        }

        private int TCcount(BasePlayer player)
        {
            List<BaseEntity> playercups = new List<BaseEntity>();
            int count = 0;
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<BaseEntity>())
            {
                if (entity.ShortPrefabName.Contains("cupboard.tool"))
                {
                    var cupboard = entity.gameObject.GetComponentInParent<BuildingPrivlidge>();
                    foreach (ProtoBuf.PlayerNameID playerNameOrID in cupboard.authorizedPlayers)
                    if (player) playercups.Add(entity);
                }
            }
            if (playercups != null)
            {
                count = playercups.Count();
            }
            return count;
        }
        #endregion

        #region msg helper

        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);

        #endregion
    }
}