using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info( "Auth Level Fix", "Mr. Blue", "0.0.1" )]
    [Description( "Stop moderators being able to add moderator or owners" )]
    class AuthLevelFix : CovalencePlugin
    {
        private Configuration _config;

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages( new Dictionary<string, string>
            {
                ["Command Blocked"] = "Command '{command}' is blocked for non owners.",
                ["Command Blocked Warning"] = "User {player} ran '{command}' but we blocked it!"
            }, this );
        }

        private string GetMessage( string key, string steamId = null ) => lang.GetMessage( key, this, steamId );
        #endregion

        #region Configuration
        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration
            {
                BlockedCommands = new HashSet<string>
                {
                    "moderatorid",
                    "ownerid",
                    "removeowner",
                    "removemoderator",
                },
                LogWarning = true,
                MessageUser = true
            };
        }
        protected override void SaveConfig() => Config.WriteObject( _config );

        private class Configuration
        {
            [JsonProperty( "Log when user tries to run commands" )]
            public bool LogWarning { get; set; }
            
            [JsonProperty( "Show blocked message to user trying to run commands" )]
            public bool MessageUser { get; set; }

            [JsonProperty( "Blocked Commands" )]
            public HashSet<string> BlockedCommands { get; set; }
        }
        #endregion

        private object OnServerCommand( ConsoleSystem.Arg arg )
        {
            if ( _config.BlockedCommands.Contains( arg.cmd.Name ) )
            {
                BasePlayer player = arg.Player();
                if ( player != null && !ServerUsers.Is( player.userID, ServerUsers.UserGroup.Owner ) )
                {
                    if ( _config.LogWarning )
                    {
                        LogWarning( GetMessage( "Command Blocked Warning" ).Replace("{player}", player.displayName).Replace("{command}", $"{arg.cmd.Name} {arg.FullString}" ) );
                    }
                    if ( _config.MessageUser )
                    {
                        player.ConsoleMessage( GetMessage( "Command Blocked", player.UserIDString ).Replace( "{command}", arg.cmd.Name ) );
                    }
                    return true;
                }
            }

            return null;
        }
    }
}
