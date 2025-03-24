using System;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core;
using Oxide.Core.Configuration;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Code Reset", "Default", "1.0.3")]
    [Description("Resets codelock code on the targeted door")]
    class CodeReset : RustPlugin
    {
        #region variables

        public bool Changed = false;
        private readonly string usePerm = "codereset.use";
        private string codecommand = "resetcode";
        private string setcodecommand = "setcode";
        private float autodisable = 30f;
        private bool clearusers = false;
        private System.Random _random = new System.Random();
        private HashSet<ulong> _coding = new HashSet<ulong>();
        static CodeReset _pluginInstance;
        SavedData _pluginData;

        #endregion

        #region Commands


        private void CmdResetCode(BasePlayer player, string command, string[] args)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, usePerm))
            {
                PrintToChat(player, string.Format(lang.GetMessage("NoPermission", this, player.UserIDString)));
                return;
            }

            if (_coding.Contains(player.userID))
            {
                _coding.Remove(player.userID);
                PrintToChat(player, string.Format(lang.GetMessage("TurnedOff", this, player.UserIDString)));
                return;
            }
            _coding.Add(player.userID);
            PrintToChat(player, string.Format(lang.GetMessage("TurnedOn", this, player.UserIDString), autodisable));
            timer.Once(autodisable, () =>
            {
                if (_coding.Contains(player.userID))
                {
                    _coding.Remove(player.userID);
                    PrintToChat(player, string.Format(lang.GetMessage("AutoOff", this, player.UserIDString)));
                    return;
                }
                else return;
            });
        }


        void CmdSetCode(BasePlayer player, string command, string[] args)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, usePerm))
            {
                PrintToChat(player, string.Format(lang.GetMessage("NoPermission", this, player.UserIDString)));
                return;
            }

            //string randomcode = GenerateRandomNo();

            //TODO Add function to allow for a randomly generated code for the admin to use
            if (args.Length == 0 || args[0].Length != 4)
            {
                PrintToChat(player, string.Format(lang.GetMessage("NoArgs", this, player.UserIDString)));
                return;
            }

            /*else if (args[0].Length != 4) 
            {
                if (!_pluginData.PlayerNumbers.ContainsKey(player.userID))
                {
                    _pluginData.addCode(player.userID, randomcode);
                    _pluginData.Save();
                    PrintToChat(player, string.Format(lang.GetMessage("SetCode", this, player.UserIDString), randomcode));
                    return;
                }
                else
                {
                    _pluginData.PlayerNumbers[player.userID] = randomcode;
                    _pluginData.Save();
                    PrintToChat(player, string.Format(lang.GetMessage("SetCode", this, player.UserIDString), randomcode));
                    return;
                }
            }*/

            if (!_pluginData.PlayerNumbers.ContainsKey(player.userID))
            {
                _pluginData.addCode(player.userID, args[0]);
                _pluginData.Save();
                PrintToChat(player, string.Format(lang.GetMessage("SetCode", this, player.UserIDString), args[0]));
                return;
            }
            else 
            {
                _pluginData.PlayerNumbers[player.userID] = args[0];
                _pluginData.Save();
                PrintToChat(player, string.Format(lang.GetMessage("SetCode", this, player.UserIDString), args[0]));
                return;
            }
        }

        #endregion

        #region Hooks

        void Init()
        {
            LoadVariables();
            lang.RegisterMessages(Messages, this);
            permission.RegisterPermission(usePerm, this);
            cmd.AddChatCommand(codecommand, this, nameof(CmdResetCode));
            cmd.AddChatCommand(setcodecommand, this, nameof(CmdSetCode));
            _pluginInstance = this;
            _pluginData = SavedData.Load();
            _coding.Clear();
        }


        /*void OnServerInitialized(bool initial)
        {
            if (initial && clearusers) 
            {
                int i = 0;
                foreach (var admin in _pluginData.PlayerNumbers.Keys) 
                {
                    BasePlayer user = FindPlayer(admin);
                    if (!user.IsAdmin) 
                    {
                        permission.RevokeUserPermission(admin.ToString(), usePerm);
                        _pluginData.PlayerNumbers.Remove(admin);
                        i++;
                    }
                }
                Log("CodeReset", $"Removed {i} users no longer under admin status");
            }

            
        }*/

        void Unload() 
        {
            _pluginInstance = null;
            _coding.Clear();
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!input.WasJustPressed(BUTTON.FIRE_PRIMARY) || player == null || !permission.UserHasPermission(player.UserIDString, usePerm)) { return; }
            handleCodeLock(player);
            return;
        }

        void handleCodeLock(BasePlayer player) 
        {
            if (!_coding.Contains(player.userID)) return;
            RaycastHit RayHit;
            var flag1 = Physics.Raycast(player.eyes.HeadRay(), out RayHit, 20f);
            BaseEntity baseEntity = flag1 ? RayHit.GetEntity() : null;
            if (baseEntity == null) return;
            CodeLock codelock = (CodeLock)baseEntity.GetSlot(BaseEntity.Slot.Lock);
            if (!_pluginData.PlayerNumbers.ContainsKey(player.userID))
            {
                PrintToChat(player, string.Format(lang.GetMessage("NoCode", this, player.UserIDString), setcodecommand));
                return;
            }
            if (codelock.code == _pluginData.PlayerNumbers[player.userID])
            {
                PrintToChat(player, string.Format(lang.GetMessage("AlreadyUsed", this, player.UserIDString), codelock.code));
                return;
            }
            codelock.code = _pluginData.PlayerNumbers[player.userID];
            BasePlayer ownerplayer = FindPlayer(codelock.OwnerID);
            Log("CodeReset", $"Player: {player.displayName} has reset a codelock to {_pluginData.PlayerNumbers[player.userID]} for {ownerplayer.displayName} | {ownerplayer.userID}");
            codelock.whitelistPlayers = new List<ulong>();
            codelock.guestPlayers = new List<ulong>();
            PrintToChat(player, string.Format(lang.GetMessage("ChangedCode", this, player.UserIDString), _pluginData.PlayerNumbers[player.userID]));
        }


        #endregion

        #region Helpers

        public string GenerateRandomNo()
        {

            return _random.Next(0, 9999).ToString("0000");
        }


        private void Log(string filename, string text)
        {
            LogToFile(filename, $"[{DateTime.Now}] {text.Replace("{", "").Replace("}", "")}", this);
        }

        private static BasePlayer FindPlayer(ulong userId)
        {
            foreach (var activePlayer in BasePlayer.activePlayerList)
            {
                if (activePlayer.userID == userId)
                    return activePlayer;
            }
            foreach (var sleepingPlayer in BasePlayer.sleepingPlayerList)
            {
                if (sleepingPlayer.userID == userId)
                    return sleepingPlayer;
            }
            return null;
        }

        #endregion

        #region Config
        object GetConfig(string menu, string datavalue, object defaultValue)
        {
            var data = Config[menu] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[menu] = data;
                Changed = true;
            }
            object value;
            if (!data.TryGetValue(datavalue, out value))
            {
                value = defaultValue;
                data[datavalue] = value;
                Changed = true;
            }
            return value;
        }

        void LoadVariables()
        {

            codecommand = Convert.ToString(GetConfig("Code Reset", "Enable tool command", "resetcode"));
            setcodecommand = Convert.ToString(GetConfig("Code Reset", "Set code command", "setcode"));
            autodisable = Convert.ToSingle(GetConfig("Code Reset", "Auto disable time", 30f));
            //clearusers = Convert.ToBoolean(GetConfig("Code Reset", "Automatically purge users no longer an admin? (On server startup)", false));

            if (!Changed) return;
            SaveConfig();
            Changed = false;
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadVariables();
        }
        #endregion

        #region Data/Messages

        Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"NoPermission", "You lack <color=#FF0000>permission</color> to use this command."},
            {"TurnedOn", "You have <color=#00FBFF>enabled</color> the code reset tool.\nThe code reset tool will <color=#FF0000>disable</color> after {0} seconds."},
            {"TurnedOff", "You have <color=#FF0000>disabled</color> the code reset tool."},
            {"AutoOff", "The code reset tool has been <color=#FF0000>disabled</color> automatically."},
            {"NoArgs", "You require a <color=#FF0000>4</color> digit code." },
            {"SetCode", "You have set your code to <color=#00FBFF>{0}</color>."},
            {"ChangedCode", "You have changed the code to this lock to <color=#00FBFF>{0}</color>." },
            {"NoCode", "You do not have a code set. Use the command <color=#FF0000>/{0}</color> to set your code." },
            {"AlreadyUsed", "Please use <color=#FF0000>another</color> code as <color=#00FBFF>{0}</color> is already used on this door." }

        };
        class SavedData
        {
            [JsonProperty("PlayerNumbers")]
            public Dictionary<ulong, string> PlayerNumbers = new Dictionary<ulong, string>();
            //[JsonProperty("CR Users")]
            //public HashSet<ulong> CRUsers = new HashSet<ulong>();

            public static SavedData Load() =>
                Interface.Oxide.DataFileSystem.ReadObject<SavedData>(_pluginInstance.Name) ?? new SavedData();

            public void Save() =>
                Interface.Oxide.DataFileSystem.WriteObject<SavedData>(_pluginInstance.Name, this);

            public void addCode(ulong a, string b) 
            {
                PlayerNumbers.Add(a, b);
                Save();
            }

            public void removeCode(ulong a) 
            {
                PlayerNumbers.Remove(a);
                Save();
            }

        }


        #endregion
    }
}
