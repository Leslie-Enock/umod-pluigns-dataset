using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Join Message Plus", "MisterPixie", "1.0.54")]
    [Description("Advanced join/leave messages")]
    class JoinMessagePlus : CovalencePlugin
    {
        private System.Random _rnd = new System.Random();
        private const string _permission = "joinmessageplus.allow";

        #region Data
        private List<string> _joinMessagePlusData = new List<string>();

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("JoinMessagePlusData", _joinMessagePlusData);
        }
        #endregion

        #region Lang
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoCommandAccess"] = "You don't have access to this command.",
                ["ToggleOn"] = "You have turned your Join/Leave Messages [#50D703]ON![/#].",
                ["ToggleOff"] = "You have turned your Join/Leave Messages [#D60000]OFF![/#]."
            }, this);
        }
        #endregion

        #region Hooks
        private void Init()
        {
            LoadVariables();

            _joinMessagePlusData = Interface.Oxide.DataFileSystem.ReadObject<List<string>>(Name);

            permission.RegisterPermission(_permission, this);

            if (!configData.EnableJoinMsg)
                Unsubscribe("OnUserConnected");

            if (!configData.EnableLeaveMsg)
                Unsubscribe("OnUserDisconnected");

            AddCovalenceCommand(configData.VIPToggleCommand, "VIPToggle");
                
        }

        private void OnUserConnected(IPlayer player)
        {

            if (configData.IsAdminJoin && player.IsAdmin)
            {
                return;
            }

            if (!_joinMessagePlusData.Contains(player.Id))
            {
                int randomQuote = _rnd.Next(configData.JoinMessages.Count);
                string randomString = configData.JoinMessages[randomQuote];

                server.Broadcast(string.Format(randomString, player.Name));
            }

        }

        private void OnUserDisconnected(IPlayer player)
        {

            if (configData.IsAdminLeave && player.IsAdmin)
            {
                return;
            }

            if (!_joinMessagePlusData.Contains(player.Id))
            {
                int randomQuote = _rnd.Next(configData.LeaveMessages.Count);
                string randomString = configData.LeaveMessages[randomQuote];

                server.Broadcast(string.Format(randomString, player.Name));
            }

        }

        private void Unload()
        {
            SaveData();
        }

        private void OnServerSave()
        {
            SaveData();
        }
        #endregion

        #region Methods
        private void VIPToggle(IPlayer player, string command, string[] arg)
        {
            if (player == null || player.IsServer)
            {
                return;
            }

            if (configData.UseVIPTogglePerm == true && !permission.UserHasPermission(player.Id, _permission))
            {
                player.Reply(Lang("NoCommandAccess", player.Id));
                return;
            }

            if (!_joinMessagePlusData.Contains(player.Id))
            {
                _joinMessagePlusData.Add(player.Id);
                player.Reply(Lang("ToggleOff", player.Id));
            }
            else
            {
                _joinMessagePlusData.Remove(player.Id);
                player.Reply(Lang("ToggleOn", player.Id));
            }
        }
        #endregion

        #region Config
        private ConfigData configData;
        private class ConfigData
        {
            public bool EnableJoinMsg;
            public bool EnableLeaveMsg;
            public bool IsAdminJoin;
            public bool IsAdminLeave;
            public bool UseVIPTogglePerm;
            public string VIPToggleCommand;
            public List<string> JoinMessages;
            public List<string> LeaveMessages;
        }

        private void LoadVariables()
        {
            LoadConfigVariables();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData
            {
                EnableJoinMsg = false,
                EnableLeaveMsg = false,
                IsAdminJoin = false,
                IsAdminLeave = false,
                UseVIPTogglePerm = false,
                VIPToggleCommand = "joinmessage",
                JoinMessages = new List<string>
                {
                    "{0} Joined the game!!!",
                    "Look out... its <color=red>{0}</color>",
                    "{0} Has joined the server!"
                },
                LeaveMessages = new List<string>
                {
                    "{0} Left the game!!!",
                    "It's sad to see you go <color=red>{0}</color>"
                }
            };
            SaveConfig(config);
        }
        private void LoadConfigVariables() => configData = Config.ReadObject<ConfigData>();
        void SaveConfig(ConfigData config) => Config.WriteObject(config, true);
        #endregion
    }
}