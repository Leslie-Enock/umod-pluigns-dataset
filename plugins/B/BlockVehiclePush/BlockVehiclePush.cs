using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Block Vehicle Push", "Clearshot", "1.1.0")]
    [Description("Block players from pushing vehicles under certain conditions")]
    class BlockVehiclePush : CovalencePlugin
    {
        private PluginConfig _config;
        private Game.Rust.Libraries.Player _rustPlayer = Interface.Oxide.GetLibrary<Game.Rust.Libraries.Player>("Player");

        private void SendChatMsg(BasePlayer pl, string msg, string prefix = null) =>
            _rustPlayer.Message(pl, msg, prefix != null ? prefix : lang.GetMessage("ChatPrefix", this, pl.UserIDString), Convert.ToUInt64(_config.chatIconID), Array.Empty<object>());

        [PluginReference]
        private Plugin Clans, Friends;

        private object OnVehiclePush(BaseVehicle vehicle, BasePlayer pl)
        {
            if (_config.requireBuildingPrivilegeToPush && pl.IsBuildingBlocked())
            {
                if (_config.chatMessage)
                    SendChatMsg(pl, lang.GetMessage("PushBlockedBuildingPriv", this, pl.UserIDString));
                return false;
            }

            foreach (BaseVehicle.MountPointInfo mountPointInfo in vehicle.allMountPoints)
            {
                BasePlayer mounted = mountPointInfo?.mountable?.GetMounted();
                if (mounted == null) continue;

                bool blocked = true;
                if (mounted.Team != null && mounted.Team.members.Contains(pl.userID))
                    blocked = false;
                else if (_config.allowClanMemberOrAllyToPush && Clans?.Call<bool>("IsMemberOrAlly", mounted.UserIDString, pl.UserIDString) == true)
                    blocked = false;
                else if (_config.allowFriendsToPush && Friends?.Call<bool>("HasFriend", mounted.userID, pl.userID) == true)
                    blocked = false;

                if (blocked)
                {
                    if (_config.chatMessage)
                        SendChatMsg(pl, string.Format(lang.GetMessage("PushBlocked", this, pl.UserIDString), mounted.displayName));
                    return false;
                }
            }
            return null;
        }

        #region Config

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ChatPrefix"] = $"<color=#00a7fe>[{Title}]</color>",
                ["PushBlocked"] = "Push has been blocked, you have no association to <color=#00a7fe>{0}</color>!",
                ["PushBlockedBuildingPriv"] = "Push has been blocked, you have no building privilege!"
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        private class PluginConfig
        {
            public string chatIconID = "0";
            public bool requireBuildingPrivilegeToPush = false;
            public bool allowClanMemberOrAllyToPush = false;
            public bool allowFriendsToPush = false;
            public bool chatMessage = true;
        }

        #endregion
    }
}
