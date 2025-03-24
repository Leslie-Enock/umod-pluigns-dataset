// Requires: DeathNotes

using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Death Notes Toggle", "0x2422", "0.0.7")]
    [Description("Allows players to toggle DeathNotes")]
    class DeathNotesToggle : RustPlugin
    {
        [PluginReference] private Plugin DeathNotes;
        
        private bool ShowInConsole => DeathNotes.Config.Get<bool>("Show Kills in Console");
        private bool ShowInChat => DeathNotes.Config.Get<bool>("Show Kills in Chat");
        private string ChatFormat => DeathNotes.Config.Get<string>("Chat Format");
        private string ChatIcon => DeathNotes.Config.Get<string>("Chat Icon (SteamID)");
        private int MessageRadius => DeathNotes.Config.Get<int>("Message Broadcast Radius (in meters)");
        private bool RequirePermission => DeathNotes.Config.Get<bool>("Require Permission (deathnotes.cansee)");

        #region Hooks

        private void Init() => LoadData();
        private void Unload() => SaveData(); 
        private void OnServerSave() => timer.Once(UnityEngine.Random.Range(0f, 60f), SaveData);
        
        private void Loaded()
        {
            if (DeathNotes == null)
                Interface.Oxide.LogError("Death Notes is not loaded!");
        }

        #endregion
        
        #region Commands

        [ChatCommand("dnt")]
        private void CmdToggleDeathNotes(BasePlayer player, string command, string[] args)
        {
            if (_storedData.PlayerData.ContainsKey(player.userID))
                _storedData.PlayerData[player.userID] = !_storedData.PlayerData[player.userID];
            else
                _storedData.PlayerData.Add(player.userID, false);

            var statusKey = _storedData.PlayerData[player.userID] ? "On" : "Off";
            var status = GetMessage(player, statusKey);
            Player.Message(player, GetMessage(player, "ToggleStatus", status));
        }
        #endregion
        
        #region DeathNotesHook

        object OnDeathNotice(Dictionary<string, object> data, string message)
        {
            var subscribers = new Dictionary<ulong, bool>();
            foreach (var player in _storedData.PlayerData)
                subscribers.Add(player.Key, player.Value);

            if (ShowInChat)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (subscribers.Keys.Contains(player.userID) && !subscribers[player.userID])
                        continue;

                    if (RequirePermission && !permission.UserHasPermission(player.UserIDString, DeathNotesCopy.Permission))
                        continue;

                    if (MessageRadius != -1 && player.Distance(data["VictimEntity"] as BaseCombatEntity) > MessageRadius)
                        continue;

                    var formattedMessage = ChatFormat == null ? message : ChatFormat.Replace("{message}", message);
                    Player.Message(player, formattedMessage, ulong.Parse(ChatIcon));
                }
            }

            if (ShowInConsole)
                DeathNotesCopy.Puts((string)DeathNotes.Call("StripRichText", message));
            
            return false;
        }

        private static class DeathNotesCopy
        {
            internal const string Permission = "deathnotes.cansee";

            public static void Puts(string format, params object[] args) => 
                Interface.Oxide.LogInfo("[{0}] {1}", "Death Notes", args.Length != 0 ? string.Format(format, args) : format);
        }

        #endregion
        
        #region Data

        private StoredData _storedData;

        private class StoredData
        {
            public readonly Dictionary<ulong, bool> PlayerData = new Dictionary<ulong, bool>();
        }

        private void LoadData()
        {
            try { _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name); }
            catch { _storedData = null; }
            finally { if (_storedData == null) ClearData(); }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);

        private void ClearData()
        {
            _storedData = new StoredData();
            SaveData();
        }

        #endregion

        #region l18n
        
        private string GetMessage(BasePlayer player, string key, params object[] args) => 
            string.Format(lang.GetMessage(key, this, player.UserIDString), args);

        protected override void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                ["ToggleStatus"] = "Death Notes are now {0}.",
                ["Off"] = "off",
                ["On"] = "on",
            };
            lang.RegisterMessages(messages, this);
        }

        #endregion
    }
}