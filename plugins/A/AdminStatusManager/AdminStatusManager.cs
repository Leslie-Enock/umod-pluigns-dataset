using System;
using System.Collections.Generic;
using System.Linq;
using Facepunch.Models.Database;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Admin Status Manager", "Ryz0r", "1.1.1")]
    [Description("Give players with permission admin permissions upon connecting. Players may also toggle it with a different permission using a command.")]
    public class AdminStatusManager : RustPlugin
    {
        private List<BasePlayer> _adminList = new List<BasePlayer>();
        private const string AutoPerm = "adminstatusmanager.auto";
        private const string CommandPerm = "adminstatusmanager.use";
        
        private static AdminStatusManager _aa;
        
        #region Hooks/Functions

        private void ChangeStatus(BasePlayer bp, bool status)
        {
            switch (status)
            {
                case true:
                    permission.AddUserGroup(bp.UserIDString, "admin");
                    bp.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                    bp.net.connection.authLevel = 2;
                    bp.Command("admintime " + _config.DefaultTime);
                
                    _adminList.Add(bp);
                    bp.SendNetworkUpdateImmediate();
            
                    bp.ChatMessage(lang.GetMessage("ToggledOn", this, bp.UserIDString).Replace("{commands}", String.Join(", ", _config.Commands)));
                    break;
                
                case false:
                    permission.RemoveUserGroup(bp.UserIDString, "admin");
                    bp.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                    bp.net.connection.authLevel = 0;
                    bp.Command("admintime -1");
                
                    _adminList.Remove(bp);
                    bp.SendNetworkUpdateImmediate();
            
                    bp.ChatMessage(lang.GetMessage("ToggledOff", this, bp.UserIDString).Replace("{commands}", String.Join(", ", _config.Commands)));

                    break;
            }
        }

        private void Init()
        {
            _aa = this;

            AddCovalenceCommand(_config.Commands, nameof(ToggleCommand));
            AddCovalenceCommand("noclip", nameof(NoclipCommand));
            permission.RegisterPermission(AutoPerm, this);
            permission.RegisterPermission(CommandPerm, this);
        }

        private void Unload() { _aa = null; }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            if (!_config.AutoAdmin && !permission.UserHasPermission(player.UserIDString, AutoPerm)) return;
            
            permission.AddUserGroup(player.UserIDString, "admin");
            player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
            player.net.connection.authLevel = 1;
            player.Command("admintime " + _config.DefaultTime);
            
            player.SendNetworkUpdateImmediate();
            _adminList.Add(player);

            player.ChatMessage(lang.GetMessage("ToggledOff", this, player.UserIDString).Replace("{commands}", String.Join(", ", _config.Commands)));
        }
        
        #endregion
        #region Configuration

        private Configuration _config;
        protected override void SaveConfig() => Config.WriteObject(_config);
        protected override void LoadDefaultConfig() => _config = new Configuration();

        private class Configuration
        {
            [JsonProperty(PropertyName = "Valid Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] Commands = {"toggle", "admin"};
            
            [JsonProperty(PropertyName = "Automatically Give Admin")]
            public bool AutoAdmin = false;

            [JsonProperty(PropertyName = "Default Time for Admins When Joining")]
            public float DefaultTime = 12f;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ToggledOn"] = "Admin permissions toggled <color=green>ON</color>. Turn off with ({commands})",
                ["ToggledOff"] = "Admin permissions toggled <color=red>OFF</color>. Turn on with ({commands})",
                ["NotClient"] = "This command is meant to be run from a client, not the console.",
                ["NoPerm"] = "You don't have the permissions to use this command."
            }, this); 
        }
        #endregion

        private void ToggleCommand(IPlayer player, string command, string[] args)
        {
            var bp = player.Object as BasePlayer;
            if (bp == null)
            {
                player.Reply(lang.GetMessage("NotClient", this, player.Id));
                return;
            }

            if (!permission.UserHasPermission(player.Id,CommandPerm))
            {
                player.Reply(lang.GetMessage("NoPerm", this, player.Id));
                return;
            }

            ChangeStatus(bp, !_adminList.Contains(bp));
        }
        
       
        private void NoclipCommand(IPlayer player, string command, string[] args)
        {
            var bp = player.Object as BasePlayer;
            if (bp == null)
            {
                player.Reply(lang.GetMessage("NotClient", this, player.Id));
                return;
            }

            if (!permission.UserHasPermission(player.Id,CommandPerm))
            {
                player.Reply(lang.GetMessage("NoPerm", this, player.Id));
                return;
            }
            
            if (bp.net.connection?.authLevel < 2 || !_adminList.Contains(bp))
            {
                player.Reply(lang.GetMessage("NoPerm", this, player.Id));
                return;
            }
            
            rust.RunClientCommand(bp, "noclip");
        }
    }
}