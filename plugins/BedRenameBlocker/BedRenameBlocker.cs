using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System;

namespace Oxide.Plugins
{
    [Info("Bed Rename Blocker", "MON@H", "2.0.1")]
    [Description("Blocks people of renaming a bed/sleeping bag")]
    class BedRenameBlocker : RustPlugin
    {
        #region Variables

        [PluginReference] private Plugin Clans, Friends;

        private const string PermissionImmunity = "bedrenameblocker.immunity";

        #endregion Variables

        #region Initialization

        private void Init()
        {
            Unsubscribe(nameof(CanRenameBed));

            permission.RegisterPermission(PermissionImmunity, this);
        }

        private void OnServerInitialized()
        {
            Subscribe(nameof(CanRenameBed));
        }

        #endregion Initialization

        #region Configuration

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Block only bags owned by the players (false = block all)")]
            public bool PlayerOwnedOnly = true;

            [JsonProperty(PropertyName = "Use Clans")]
            public bool UseClans = true;

            [JsonProperty(PropertyName = "Use Friends")]
            public bool UseFriends = true;

            [JsonProperty(PropertyName = "Use Teams")]
            public bool UseTeams = true;

            [JsonProperty(PropertyName = "Chat steamID icon")]
            public ulong SteamIDIcon = 0;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _configData = Config.ReadObject<ConfigData>();
                if (_configData == null)
                {
                    LoadDefaultConfig();
                    SaveConfig();
                }
            }
            catch
            {
                PrintError("The configuration file is corrupted");
                LoadDefaultConfig();
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            _configData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(_configData);

        #endregion Configuration

        #region Localization

        private string Lang(string key, string userIDString = null, params object[] args)
        {
            try
            {
                return string.Format(lang.GetMessage(key, this, userIDString), args);
            }
            catch (Exception ex)
            {
                PrintError($"Lang Key '{key}' threw exception:\n{ex}");
                throw;
            }
        }

        private static class LangKeys
        {
            public static class Error
            {
                private const string Base = nameof(Error) + ".";
                public const string NoPermission = Base + nameof(NoPermission);
            }

            public static class Format
            {
                private const string Base = nameof(Format) + ".";
                public const string Prefix = Base + nameof(Prefix);
            }
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Error.NoPermission] = "You do not have permission to use this command",
                [LangKeys.Format.Prefix] = "<color=#00FF00>[Bed Rename Blocker]</color>: ",
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Error.NoPermission] = "У вас нет разрешения на использование этой команды",
                [LangKeys.Format.Prefix] = "<color=#00FF00>[Блокировка переименнования]</color>: ",
            }, this, "ru");
        }

        #endregion Localization

        #region Oxide Hooks

        private object CanRenameBed(BasePlayer player, SleepingBag bed, string bedName)
        {
            if (player != null && bed != null && !permission.UserHasPermission(player.UserIDString, PermissionImmunity))
            {
                if (!bed.OwnerID.IsSteamId() && !_configData.PlayerOwnedOnly)
                {
                    PlayerSendMessage(player, Lang(LangKeys.Error.NoPermission, player.UserIDString));
                    return true;
                }

                if (bed.OwnerID.IsSteamId() && !IsAlly(player.userID, bed.OwnerID))
                {
                    PlayerSendMessage(player, Lang(LangKeys.Error.NoPermission, player.UserIDString));
                    return true;
                }
            }

            return null;
        }

        #endregion Oxide Hooks

        #region Helpers

        private bool IsPluginLoaded(Plugin plugin) => plugin != null && plugin.IsLoaded;

        public bool IsAlly(ulong playerId, ulong targetId)
        {
            return playerId == targetId || IsOnSameTeam(playerId, targetId) || IsClanMemberOrAlly(playerId.ToString(), targetId.ToString()) || IsFriend(playerId, targetId);
        }

        public bool IsClanMemberOrAlly(string playerId, string targetId)
        {
            if (_configData.UseClans)
            {
                if (IsPluginLoaded(Clans))
                {
                    return Clans.Call<bool>("IsMemberOrAlly", playerId, targetId);
                }
                else
                {
                    PrintError("UseClans is set to true, but Clans plugin is not loaded!");
                }
            }

            return false;
        }

        public bool IsFriend(ulong playerId, ulong targetId)
        {
            if (_configData.UseFriends)
            {
                if (IsPluginLoaded(Friends))
                {
                    return Friends.Call<bool>("HasFriend", targetId, playerId);
                }
                else
                {
                    PrintError("UseFriends is set to true, but Friends plugin is not loaded!");
                }
            }

            return false;
        }

        public bool IsOnSameTeam(ulong playerId, ulong targetId)
        {
            if (!_configData.UseTeams)
            {
                return false;
            }

            RelationshipManager.PlayerTeam playerTeam;
            if (!RelationshipManager.ServerInstance.playerToTeam.TryGetValue(playerId, out playerTeam))
            {
                return false;
            }

            RelationshipManager.PlayerTeam targetTeam;
            if (!RelationshipManager.ServerInstance.playerToTeam.TryGetValue(targetId, out targetTeam))
            {
                return false;
            }

            return playerTeam.teamID == targetTeam.teamID;
        }

        private void PlayerSendMessage(BasePlayer player, string message)
        {
            player.SendConsoleCommand("chat.add", 2, _configData.SteamIDIcon, string.IsNullOrEmpty(Lang(LangKeys.Format.Prefix, player.UserIDString)) ? message : Lang(LangKeys.Format.Prefix, player.UserIDString) + message);
        }

        #endregion Helpers
    }
}