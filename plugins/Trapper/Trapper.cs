using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

//Trapper created with PluginMerge v(1.0.6.0) by MJSU @ https://github.com/dassjosh/Plugin.Merge
namespace Oxide.Plugins
{
    [Info("Trapper", "Shady14u", "1.1.1")]
    [Description("Control who sets off traps")]
    public partial class Trapper : RustPlugin
    {
        #region 0.Trapper.cs
        [PluginReference]
        private Plugin Friends, RustIO, Clans;
        
        private static BasePlayer FindPlayer(string nameOrId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.UserIDString == nameOrId)
                return activePlayer;
                if (activePlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                return activePlayer;
            }
            foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.UserIDString == nameOrId)
                return sleepingPlayer;
                if (sleepingPlayer.displayName.Contains(nameOrId, CompareOptions.OrdinalIgnoreCase))
                return sleepingPlayer;
            }
            return null;
        }
        #endregion

        #region 1.Trapper.Config.cs
        private static Configuration _config;
        
        public class Configuration
        {
            [JsonProperty(PropertyName = "Reset Time")]
            public float ResetTime { get; set; } = 5f;
            [JsonProperty(PropertyName = "Trigger for Owner")]
            public bool HurtOwner { get; set; } = false;
            [JsonProperty(PropertyName = "Trigger for Friends")]
            public bool HurtFriends { get; set; } = false;
            [JsonProperty(PropertyName = "Trigger for Clan Mates")]
            public bool HurtClanMates { get; set; } = false;
            [JsonProperty(PropertyName = "Trigger for Authorized")]
            public bool HurtAuthed { get; set; } = false;
            [JsonProperty(PropertyName = "Ignore Projectiles")]
            public bool IgnoreProjectiles { get; set; } = false;
            
            
            public static Configuration DefaultConfig()
            {
                return new Configuration();
            }
        }
        
        
        #region BoilerPlate
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }
        
        protected override void LoadDefaultConfig() => _config = Configuration.DefaultConfig();
        
        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion
        #endregion

        #region 3.Trapper.Permissions.cs
        public static class PluginPermissions
        {
            public const string PermissionNameAdmin = "trapper.admin";
            public const string PermissionName = "trapper.auto";
            public const string PermissionNameOwner = "trapper.owner";
            public const string PermissionNameFriends = "trapper.friends";
            public const string PermissionNameClan = "trapper.clan";
            public const string PermissionNameAuth = "trapper.auth";
        }
        
        private void LoadPermissions()
        {
            permission.RegisterPermission(PluginPermissions.PermissionName, this);
            permission.RegisterPermission(PluginPermissions.PermissionNameOwner, this);
            permission.RegisterPermission(PluginPermissions.PermissionNameFriends, this);
            permission.RegisterPermission(PluginPermissions.PermissionNameAdmin, this);
            permission.RegisterPermission(PluginPermissions.PermissionNameClan, this);
            permission.RegisterPermission(PluginPermissions.PermissionNameAuth, this);
        }
        #endregion

        #region 5.Trapper.Hooks.cs
        private void Init()
        {
            LoadPermissions();
        }
        
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!_config.IgnoreProjectiles) return null;
            var trap = entity.GetComponent<BaseTrap>();
            if (!trap) return null;
            if (!info.IsProjectile()) return null;
            var bearTrap = entity.GetComponent<BearTrap>();
            if (bearTrap)
            bearTrap.Arm();
            return true;
        }
        
        private object OnTrapTrigger(BaseTrap trap, GameObject obj)
        {
            if (!(trap is BearTrap) && !(trap is Landmine))
            return null;
            var target = obj.GetComponent<BasePlayer>();
            if (target != null)
            {
                if (permission.UserHasPermission(target.UserIDString, PluginPermissions.PermissionNameAdmin))
                return false;
            }
            var player = FindPlayer(trap.OwnerID.ToString());
            if (target != null && player != null)
            {
                // Owner protection
                if (!_config.HurtOwner)
                if (target == player){
                    if (permission.UserHasPermission(target.UserIDString, PluginPermissions.PermissionNameOwner)){
                        return false;
                    }
                }
                // Building auth protection
                if (!_config.HurtAuthed)
                if (permission.UserHasPermission(target.UserIDString, PluginPermissions.PermissionNameAuth))
                if (target.IsBuildingAuthed()){
                    return false;
                }
                
                // Friends protection
                if (!_config.HurtFriends)
                {
                    if (target.Team.members.Contains(trap.OwnerID)) return false;
                    
                    if (Friends!=null && Friends.IsLoaded)
                    if (Friends.Call<bool>("AreFriends", target.userID, player.userID))
                    if (permission.UserHasPermission(target.UserIDString, PluginPermissions.PermissionNameFriends))
                    return false;
                    
                    if (RustIO!=null && RustIO.IsLoaded)
                    if (RustIO.Call<bool>("HasFriend", target.UserIDString, player.UserIDString))
                    if (permission.UserHasPermission(target.UserIDString, PluginPermissions.PermissionNameFriends))
                    return false;
                }
                
                // ClanMates protection
                if (!_config.HurtClanMates)
                {
                    if (Clans)
                    {
                        var targetClan = Clans?.Call<string>("GetClanTag", target.UserIDString);
                        var playerClan = Clans?.Call<string>("GetClanTag", player.UserIDString);
                        if (targetClan == playerClan && !string.IsNullOrEmpty(playerClan ))
                        {
                            if (permission.UserHasPermission(target.UserIDString, PluginPermissions.PermissionNameClan))
                            return false;
                        }
                        else if (Clans?.Call<string>("GetClanOf", target.UserIDString) == Clans?.Call<string>("GetClanOf", player.UserIDString)){
                            if (permission.UserHasPermission(target.UserIDString, PluginPermissions.PermissionNameClan))
                            return false;
                        }
                    }
                }
            }
            
            // Automatic re-arming
            if (!(trap is BearTrap))
            return null;
            if (player == null)
            return null;
            if (permission.UserHasPermission(player.UserIDString, PluginPermissions.PermissionName))
            timer.Once(_config.ResetTime, () =>
            {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (trap != null)
                ((BearTrap)trap).Arm();
            });
            return null;
        }
        #endregion

    }

}
