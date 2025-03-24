using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Hackable Lock", "Ryz0r", "2.3.0")]
    [Description("Locks Hackable Crate to person who started hack process")]
    public class HackableLock : CovalencePlugin
    {
        #region Class Fields

        [PluginReference]
        private Plugin DiscordEvents, Clans, Friends;

        private const string PermissionUse = "hackablelock.use";

        #endregion Class Fields

        #region Initialization

        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
            LoadData();

            if (!_configData.GlobalSettings.Enabled)
            {
                Unsubscribe(nameof(CanHackCrate));
                Unsubscribe(nameof(CanLootEntity));
                Unsubscribe(nameof(OnEntityKill));
            }
        }

        private void OnServerSave() => timer.Once(UnityEngine.Random.Range(0f, 60f), SaveData);

        private void Unload() => SaveData();

        #endregion Initialization

        #region Configuration

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Global settings")]
            public GlobalConfiguration GlobalSettings = new GlobalConfiguration();

            [JsonProperty(PropertyName = "Chat settings")]
            public ChatConfiguration ChatSettings = new ChatConfiguration();

            [JsonProperty(PropertyName = "Discord settings")]
            public DiscordConfiguration DiscordSettings = new DiscordConfiguration();

            public class GlobalConfiguration
            {
                [JsonProperty(PropertyName = "Use permissions")]
                public bool UsePermission = true;

                [JsonProperty(PropertyName = "Allow admins to use without permission")]
                public bool AdminsAllowed = true;

                [JsonProperty(PropertyName = "Enabled?")]
                public bool Enabled = true;

                [JsonProperty(PropertyName = "Lock time (seconds)")]
                public float LockTime = 1200f;
                
                [JsonProperty(PropertyName = "Use Clans")]
                public bool EnableClans = false;
                
                [JsonProperty(PropertyName = "Use Teams")]
                public bool EnableTeams = false;
                
                [JsonProperty(PropertyName = "Use Friends")]
                public bool EnableFriends = false;
            }

            public class ChatConfiguration
            {
                [JsonProperty(PropertyName = "Chat steamID icon")]
                public ulong SteamIDIcon = 0;

                [JsonProperty(PropertyName = "Notifications Enabled?")]
                public bool Notifications = true;
                
                [JsonProperty(PropertyName = "Player Console Notifications only")]
                public bool SendConsoleOnly = true;
            }
            
            public class DiscordConfiguration
            {
                [JsonProperty(PropertyName = "WebhookURL")]
                public string WebhookURL = "";

                [JsonProperty(PropertyName = "Enabled?")]
                public bool Enabled = false;
            }
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

        #region DataFile

        private StoredData _storedData;

        private class StoredData
        {
            public Dictionary<ulong, LockInfo> _lockedCrates = new Dictionary<ulong, LockInfo>();
        }

        private class LockInfo
        {          
            public ulong PlayerID;
            public string PlayerName;
            public float UnlockTime;
        }

        private void LoadData()
        {
            try
            {
                _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                _storedData = null;
            }
            finally
            {
                if (_storedData == null)
                {
                    ClearData();
                }
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);

        private void ClearData()
        {
            _storedData = new StoredData();
            SaveData();
        }

        #endregion DataFile

        #region Localization

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Prefix"] = "<color=#00FFFF>[Hackable Crate Lock]</color>: ",
                ["Hacking"] = "<color=#FFA500>{0}</color> started locked crate hack at <color=#FFA500>{1}</color>. This crate will be locked for others for <color=#FFA500>{2}</color> more seconds",
                ["Locked"] = "This crate was hacked by <color=#FFA500>{0}</color> and protected for <color=#FFA500>{1}</color> more seconds",
                ["Discord"] = ":lock: `{0}` started locked crate hack at `{1}`. This crate will be locked for others for `{2}` more seconds",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Prefix"] = "<color=#00FFFF>[Блокировка закрытого ящика]</color>: ",
                ["Hacking"] = "<color=#00FFFF>{0}</color> начал процесс взлома запертого ящика на <color=#FFA500>{1}</color>. Этот ящик будет защищён от других игроков ещё <color=#FFA500>{2}</color> секунд",
                ["Locked"] = "Этот ящик был взломан игроком <color=#FFA500>{0}</color> и защищён от других ещё <color=#FFA500>{1}</color> секунд",
                ["Discord"] = ":lock: `{0}` начал процесс взлома запертого ящика на `{1}`. Этот ящик будет защищён от других игроков ещё `{2}` секунд",
            }, this, "ru");
        }

        #endregion Localization

        #region Oxide Hooks

        private void CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {
            if (!_configData.GlobalSettings.Enabled || player == null || crate == null)
            {
                return;
            }

            if (_configData.GlobalSettings.UsePermission && !permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                if (!_configData.GlobalSettings.AdminsAllowed || !player.IsAdmin)
                {
                    return;
                }
            }

            LockInfo lockInfo = new LockInfo()
            {
                PlayerID = player.userID,
                PlayerName = player.displayName,
                UnlockTime = Time.realtimeSinceStartup + _configData.GlobalSettings.LockTime,
            };

            if (!_storedData._lockedCrates.ContainsKey(crate.net.ID.Value))
            {
                _storedData._lockedCrates.Add(crate.net.ID.Value, lockInfo);
                SaveData();

                if (_configData.ChatSettings.Notifications)
                {
                    foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
                    {
                        Print(basePlayer.IPlayer, Lang("Hacking", basePlayer.UserIDString, player.displayName, GetGridPosition(crate.transform.position), (lockInfo.UnlockTime - Time.realtimeSinceStartup).ToString("F0")));
                    }
                }

                if (_configData.DiscordSettings.Enabled)
                {
                    PrintDiscord(Lang("Discord", player.UserIDString, player.displayName, GetGridPosition(crate.transform.position), (lockInfo.UnlockTime - Time.realtimeSinceStartup).ToString("F0")));
                }
            }
        }
        
        private void OnEntityKill(HackableLockedCrate crate)
        {
            if (crate == null || !_storedData._lockedCrates.ContainsKey(crate.net.ID.Value))
            {
                return;
            }

            _storedData._lockedCrates.Remove(crate.net.ID.Value);
            SaveData();
        }

        private object CanLootEntity(BasePlayer player, HackableLockedCrate crate)
        {
            if (!_configData.GlobalSettings.Enabled)
            {
                return null;
            }

            float unlockTime = GetCrateLockTime(crate.net.ID.Value);

            if (unlockTime > 0 && !IsOwner(player.userID, _storedData._lockedCrates[crate.net.ID.Value].PlayerID))
            {
                Print (player.IPlayer, Lang("Locked", player.UserIDString, _storedData._lockedCrates[crate.net.ID.Value].PlayerName, unlockTime.ToString("F0")));
                return true;
            }

            return null;
        }

        #endregion Oxide Hooks

        #region API Methods

        private void LockCrateToPlayer(BasePlayer player, HackableLockedCrate crate)
        {
            if (player == null || crate == null || _storedData._lockedCrates.ContainsKey(crate.net.ID.Value))
            {
                return;
            }

            LockInfo lockInfo = new LockInfo()
            {
                PlayerID = player.userID,
                PlayerName = player.displayName,
                UnlockTime = Time.realtimeSinceStartup + _configData.GlobalSettings.LockTime,
            };

            if (!_storedData._lockedCrates.ContainsKey(crate.net.ID.Value))
            {
                _storedData._lockedCrates.Add(crate.net.ID.Value, lockInfo);
                SaveData();
            }
        }

        #endregion API Methods

        #region Helpers

        private float GetCrateLockTime(ulong crateID)
        {
            if (_configData.GlobalSettings.Enabled)
            {
                Dictionary<ulong, LockInfo> actualized = new Dictionary<ulong, LockInfo>();

                foreach (KeyValuePair<ulong,Oxide.Plugins.HackableLock.LockInfo> lockedCrate in _storedData._lockedCrates)
                {
                    if (lockedCrate.Value.UnlockTime > Time.realtimeSinceStartup)
                    {
                        actualized.Add(lockedCrate.Key, lockedCrate.Value);
                    }
                }

                _storedData._lockedCrates = actualized;

                if (_storedData._lockedCrates.ContainsKey(crateID))
                {
                    return _storedData._lockedCrates[crateID].UnlockTime - Time.realtimeSinceStartup;
                }
            }

            return 0;
        }

        private string GetGridPosition(Vector3 pos)
        {
            const float gridCellSize = 146.3f;

            int maxGridSize = Mathf.FloorToInt(World.Size / gridCellSize) - 1;
            float halfWorldSize = World.Size / 2f;
            int xGrid = Mathf.Clamp(Mathf.FloorToInt((pos.x + halfWorldSize) / gridCellSize),0, maxGridSize);
            int zGrid = Mathf.Clamp(maxGridSize - Mathf.FloorToInt((pos.z + halfWorldSize) / gridCellSize),0, maxGridSize);

            string extraA = string.Empty;
            if (xGrid > 26)
            {
                extraA = $"{(char) ('A' + (xGrid / 26 - 1))}";
            }

            return $"{extraA}{(char) ('A' + xGrid % 26)}{zGrid.ToString()}";
        }

        private void PrintDiscord(string message)
        {
            if (!_configData.DiscordSettings.Enabled)
            {
                return;
            }

            if (DiscordEvents == null || !DiscordEvents.IsLoaded)
            {
                PrintError("Prints to Discord enabled, but DiscordEvents is null or empty!");
                return;
            }

            if (string.IsNullOrEmpty(_configData.DiscordSettings.WebhookURL))
            {
                PrintError("Prints to Discord enabled, but WebhookURL is null or empty!");
                return;
            }

            DiscordEvents?.Call("SendMessage", message, _configData.DiscordSettings.WebhookURL);
        }

        private void Print(IPlayer player, string message)
        {
            string text;
            if (string.IsNullOrEmpty(Lang("Prefix", player.Id)))
            {
                text = message;
            }
            else
            {
                text = Lang("Prefix", player.Id) + message;
            }

            if (_configData.ChatSettings.SendConsoleOnly)
            {
                ((BasePlayer)player.Object).SendConsoleCommand ("chat.add", 2, _configData.ChatSettings.SteamIDIcon, text);
                return;
            }

            player.Message(text);
        }

        private bool IsOwner(ulong userID, ulong owner)
        {
            if (userID == owner)
            {
                return true;
            }
            
            if (_configData.GlobalSettings.EnableTeams && SameTeam(userID, owner))
            {
                return true;
            }

            if (_configData.GlobalSettings.EnableClans && SameClan(userID, owner))
            {
                return true;
            }

            if (_configData.GlobalSettings.EnableFriends && AreFriends(userID, owner))
            {
                return true;
            }

            return false;
        }
        
        private bool SameTeam(ulong playerID, ulong friendID)
        {
            if (!RelationshipManager.TeamsEnabled())
            {
                return false;
            }

            RelationshipManager.PlayerTeam playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerID);
            if (playerTeam == null) 
            {
                return false;
            }

            RelationshipManager.PlayerTeam friendTeam = RelationshipManager.ServerInstance.FindPlayersTeam(friendID);
            if (friendTeam == null)
            {
                return false;
            }

            return playerTeam == friendTeam;
        }

        private bool AreFriends(ulong playerID, ulong friendID)
        {
            // Friends plugin
            return Friends != null && Friends.Call<bool>("AreFriends", playerID, friendID);
        }

        private bool SameClan(ulong playerID, ulong friendID)
        {
            // Clans plugin
            bool isMember = Clans != null && Clans.Call<bool>("IsClanMember", playerID.ToString(), friendID.ToString());
            if (isMember != null) return isMember;

            // Rust:IO Clans plugin
            string playerClan = Clans?.Call<string>("GetClanOf", playerID);
            if (playerClan == null) return false;
            
            string friendClan = Clans?.Call<string>("GetClanOf", friendID);
            if (friendClan == null) return false;

            return playerClan == friendClan;
        }

        #endregion Helpers
    }
}
