using ConVar;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("ConsoleChat", "Death", "1.0.5")]
    [Description("Prints all player chat to console.")]
    class ConsoleChat : RustPlugin
    {
        #region Declarations
        const string perm = "consolechat.exclude";
        BasePlayer cachedTeamPlayer;
        #endregion

        #region Hooks
        void Init()
        {
            LoadConfigVariables();
            permission.RegisterPermission(perm, this);
        }

        void OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (channel == Chat.ChatChannel.Team)
            {
                foreach (var teamMate in player.Team.members)
                {
                    if (!RelationshipManager.ServerInstance.cachedPlayers.TryGetValue(teamMate, out cachedTeamPlayer) || permission.UserHasPermission(cachedTeamPlayer.UserIDString, perm))
                    {
                        continue;
                    }

                    cachedTeamPlayer.SendConsoleCommand($"echo [Team] <color={configData.Options.Output_Color}>[{DateTime.UtcNow.ToString(configData.Options.Time_Format)} UTC] {player.displayName}: {message}</color>");
                }
            }
            else
            {
                foreach (var aPlayer in BasePlayer.activePlayerList)
                {
                    if (aPlayer == null || permission.UserHasPermission(aPlayer.UserIDString, perm))
                    {
                        continue;
                    }

                    aPlayer.SendConsoleCommand($"echo [Global] <color={configData.Options.Output_Color}>[{DateTime.UtcNow.ToString(configData.Options.Time_Format)} UTC] {player.displayName}: {message}</color>");
                }
            }
        }
        #endregion

        #region Functions

        #region Commands
        [ConsoleCommand("consolechat.toggle")]
        void ConsoleCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection?.player as BasePlayer;

            if (player == null)
            {
                return;
            }

            if (!configData.Options.Enable_Toggle)
            {
                player.SendConsoleCommand($"echo {lang.GetMessage("disabled", this, player.UserIDString)}");
                return;
            }

            var m = string.Empty;

            if (permission.UserHasPermission(player.UserIDString, perm))
            {
                m = "enabled";
                permission.RevokeUserPermission(player.UserIDString, perm);
            }
            else
            {
                m = "disabled";
                permission.GrantUserPermission(player.UserIDString, perm, null);
            }

            player.SendConsoleCommand($"echo <color={configData.Options.Output_Color}>{lang.GetMessage("toggle", this, player.UserIDString).Replace("{0}", m)}</color>");
        }
        #endregion

        #region Config
        private ConfigData configData;

        class ConfigData
        {
            public Options Options = new Options();
        }

        class Options
        {
            public bool Enable_Toggle = true;
            public string Output_Color = "#fff";
            public string Time_Format = "hh:mm tt";
        }

        private void LoadConfigVariables()
        {
            configData = Config.ReadObject<ConfigData>();
            SaveConfig(configData);
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData();
            SaveConfig(config);
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"toggle", "ConsoleChat is now {0}" },
                {"disabled", "ConsoleChat toggle is disabled." }
            }, this, "en");
        }
        #endregion

        #endregion
    }
}