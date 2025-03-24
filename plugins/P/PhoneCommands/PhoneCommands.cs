using Facepunch;
using Oxide.Core;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Phone Commands", "marcuzz", "0.0.10")]
    [Description("Bind commands to phone numbers")]
    public class PhoneCommands : CovalencePlugin
    {
        private static PluginConfig _config;

        private static ListDictionary<int, PhoneCommand> _phoneCommands;

        private static StoredData _storedData;

        private static Regex _chatCommandRegex;

        private static Dictionary<string, int> _blockedChatCommands;

        private static Dictionary<ulong, int> _allowedCommands;

        #region Hooks

        void Init() 
        {
            _storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            _phoneCommands = new ListDictionary<int, PhoneCommand>();
            _blockedChatCommands = new Dictionary<string, int>();
            _chatCommandRegex = new Regex("(?<=\\A^/)(\\w+)", RegexOptions.Compiled);
            _allowedCommands = new Dictionary<ulong, int>();
        }

        void OnServerInitialized()
        {
            if (!_config.BlockChatCommandsBoundToPhoneNumber)
                Unsubscribe(nameof(OnPlayerCommand));

            RegisterPhoneComands();
        }

        void Unload()
        {
            if (_phoneCommands != null)
            {
                foreach (var phoneCommand in _phoneCommands.Values)
                {
                    var phone = TelephoneManager.GetTelephone(phoneCommand.Config.PhoneNumber);
                    if (phone == null)
                        continue;

                    TelephoneManager.DeregisterTelephone(phone);
                    if (phoneCommand.Telephone.IsDestroyed == false)
                    {
                        phoneCommand.Telephone.Kill();
                    }

                    _storedData.UsedNumbers.Remove(phoneCommand.Config.PhoneNumber);
                }
            }

            SaveData();

            _config = null;
            _phoneCommands = null;
            _storedData = null;
            _blockedChatCommands = null;
            _chatCommandRegex = null;
            _allowedCommands = null;
        }

        object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (player == null)
                return null;

            if (_allowedCommands.ContainsKey(player.userID) && _allowedCommands[player.userID] > 0)
            {
                _allowedCommands[player.userID]--;
                return null;
            }

            if (_blockedChatCommands.ContainsKey(command))
            {
                var translatedMessage = lang.GetMessage("ChatCommandBlocked", this, player.UserIDString);
                player.ChatMessage(string.Format(translatedMessage, _blockedChatCommands[command]));
                return false;
            }

            return null;
        }

        object OnPhoneDial(PhoneController callerPhone, PhoneController receiverPhone, BasePlayer player)
        {
            if (player == null)
                return null;

            if (!_phoneCommands.Keys.Contains(receiverPhone.PhoneNumber))
                return null;

            var command = _phoneCommands[receiverPhone.PhoneNumber];

            var prefab = callerPhone.ParentEntity.PrefabName;
            if (prefab == "assets/prefabs/voiceaudio/mobilephone/mobilephone.weapon.prefab" 
                && command.Config.RestrictMobilePhones)
            {
                RestrictedDevice(player);
                return false;
            }
            
            if (prefab == "assets/bundled/prefabs/autospawn/phonebooth/phonebooth.static.prefab" 
                && command.Config.RestrictPublicPhones)
            {
                RestrictedDevice(player);
                return false;
            }

            if (prefab == "assets/prefabs/voiceaudio/telephone/telephone.deployed.prefab" 
                && command.Config.RestrictLandlinePhones)
            {
                RestrictedDevice(player);
                return false;
            }

            callerPhone.serverState = Telephone.CallState.Idle;
            callerPhone.activeCallTo = null;

            string message;
            if (!CheckPermission(command, player))
            {
                message = lang.GetMessage("PermissionRequired", this, player.UserIDString);
                player.ChatMessage(message);
                PlayEffect(player, true);

                return false;
            }

            if (!CheckCooldown(command, player, out message))
            {
                player.ChatMessage(message);
                PlayEffect(player, true);

                return false;
            }

            RunCommand(player, command);

            return true;
        }

        void OnEntitySpawned(MobilePhone phone)
        {
            AddCommandsToContactList(phone.Controller);
        }

        void OnEntitySpawned(Telephone phone)
        {
            if (phone.ShortPrefabName != "phonebooth.static.prefab")
                AddCommandsToContactList(phone.Controller);
        }

        void OnNewSave()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, new StoredData());
        }

        #endregion

        #region Command Registration

        private void RegisterPhoneComands()
        {
            var createdPhones = new Dictionary<int, PhoneController>();
            foreach (var phoneCommand in _config.PhoneCommands)
            {
                if (!IsConfigurationValid(phoneCommand))
                {
                    Puts($"Phone command {phoneCommand.PhoneNumber} ({phoneCommand.PhoneName}) has invalid configuration, skipping");
                    continue;
                }

                if (TelephoneManager.allTelephones.ContainsKey(phoneCommand.PhoneNumber))
                {
                    if (_storedData.UsedNumbers.Contains(phoneCommand.PhoneNumber))
                    {
                        var phone = TelephoneManager.GetTelephone(phoneCommand.PhoneNumber);
                        TelephoneManager.DeregisterTelephone(phone);
                        var existing = phone.ParentEntity as Telephone;
                        _storedData.UsedNumbers.Remove(phoneCommand.PhoneNumber);

                        RegisterCommand(phoneCommand, existing, createdPhones);
                        continue;
                    }
                    else
                    {
                        Puts($"Phone number {phoneCommand.PhoneNumber} ({phoneCommand.PhoneName}) is already in use, skipping");
                        continue;
                    }
                }

                Telephone telephone;
                if (!SpawnPhone(out telephone))
                {
                    Puts($"Failed to register phone number {phoneCommand.PhoneNumber} ({phoneCommand.PhoneName}), skipping");
                    continue;
                }

                RegisterCommand(phoneCommand, telephone, createdPhones);
            }

            foreach (var item in TelephoneManager.allTelephones)
            {
                createdPhones[item.Key] = item.Value;
            }

            TelephoneManager.allTelephones = createdPhones;
        }

        private void RegisterCommand(PhoneCommandConfig command, Telephone telephone, Dictionary<int, PhoneController> phones)
        {
            telephone.Controller.PhoneNumber = command.PhoneNumber;
            telephone.Controller.PhoneName = command.PhoneName;

            _phoneCommands.Add(command.PhoneNumber, new PhoneCommand
            {
                Telephone = telephone,
                Config = command
            });

            if (command.Permission != null && !permission.PermissionExists(command.Permission))
                permission.RegisterPermission(command.Permission, this);

            if (_config.BlockChatCommandsBoundToPhoneNumber)
                SetBlockedCommands(command);

            phones.Add(telephone.Controller.PhoneNumber, telephone.Controller);
            _storedData.UsedNumbers.Add(telephone.Controller.PhoneNumber);
            SaveData();
        }

        private void SetBlockedCommands(PhoneCommandConfig command)
        {
            foreach (var action in command.PhoneActions)
            {
                if (action.Type == CommandActionType.PlayerChat)
                {
                    var match = _chatCommandRegex.Match(action.Command);
                    if (match.Success)
                        _blockedChatCommands[match.ToString()] = command.PhoneNumber;
                }
            }
        }

        private static bool SpawnPhone(out Telephone telephone)
        {
            telephone = GameManager.server.CreateEntity("assets/bundled/prefabs/autospawn/phonebooth/phonebooth.static.prefab") as Telephone;
            if (telephone == null)
                return false;

            telephone.Spawn();
            TelephoneManager.DeregisterTelephone(telephone.Controller);

            return true;
        }

        private static bool IsConfigurationValid(PhoneCommandConfig command)
        {
            if (command.PhoneName.Length <= 0 || command.PhoneName.Length > 20)
                return false;
            

            if (command.PhoneNumber < 10000000 || command.PhoneNumber >= 100000000)
                return false;

            return true;
        }

        //Not using Pool.Free as it is exact logic copy of PhoneController.Server_AddSavedNumber
        private void AddCommandsToContactList(PhoneController phone)
        {
            if (_phoneCommands == null || !_config.AddCommandsToContactList)
                return;

            var count = 0;
            foreach (var command in _phoneCommands.Values)
            {
                AddCommandToContactList(phone, command.Config);
                count++;
            }

            if (count > 0)
            {
                phone.savedNumbers.ShouldPool = false;
                phone.baseEntity.SendNetworkUpdate();
            }
        }

        //Not using Pool.Free as it is exact logic copy of PhoneController.Server_AddSavedNumber
        private static void AddCommandToContactList(PhoneController controller, PhoneCommandConfig command)
        {
            if (controller.savedNumbers == null)
                controller.savedNumbers = Pool.Get<PhoneDirectory>();

            if (controller.savedNumbers.entries == null)
                controller.savedNumbers.entries = Pool.GetList<PhoneDirectory.DirectoryEntry>();

            if (controller.IsSavedContactValid(command.PhoneName, command.PhoneNumber))
            {
                var directoryEntry = Pool.Get<PhoneDirectory.DirectoryEntry>();
                directoryEntry.phoneName = command.PhoneName;
                directoryEntry.phoneNumber = command.PhoneNumber;
                directoryEntry.ShouldPool = false;
                controller.savedNumbers.entries.Add(directoryEntry);
            }
        }

        #endregion

        #region Command Execution

        private void RestrictedDevice(BasePlayer player) 
        {
            var message = lang.GetMessage("DeviceRestricted", this, player.UserIDString);
            player.ChatMessage(message);
            PlayEffect(player, true);
        }

        private bool CheckCooldown(PhoneCommand command, BasePlayer player, out string message)
        {
            message = null;

            if (command.Config.Cooldown == 0 || player.IsAdmin)
                return true;

            if (!_storedData.PlayerCooldowns.ContainsKey(player.userID))
                _storedData.PlayerCooldowns.Add(player.userID, new Dictionary<int, long>());

            var playerCooldwns = _storedData.PlayerCooldowns[player.userID];
            if (!playerCooldwns.ContainsKey(command.Config.PhoneNumber))
            {
                playerCooldwns.Add(command.Config.PhoneNumber, DateTime.Now.ToBinary());
                SaveData();
                return true;
            }

            var dt = DateTime.FromBinary(playerCooldwns[command.Config.PhoneNumber]);
            if (dt.AddSeconds(command.Config.Cooldown) > DateTime.Now)
            {
                var diff = dt.AddSeconds(command.Config.Cooldown) - DateTime.Now;
                var cooldownTranslatedMessage = lang.GetMessage("CommandCooldown", this, player.UserIDString);
                message = string.Format(cooldownTranslatedMessage, (int)diff.TotalMinutes);

                return false;
            }

            playerCooldwns[command.Config.PhoneNumber] = DateTime.Now.ToBinary();
            SaveData();

            return true;
        }

        private bool CheckPermission(PhoneCommand command, BasePlayer player)
        {
            if (command.Config.Permission != null)
            {
                var hasPerm = permission.UserHasPermission(player.UserIDString, command.Config.Permission);
                if (!hasPerm && !player.IsAdmin)
                    return false;
            }

            return true;
        }

        private void RunCommand(BasePlayer player, PhoneCommand command)
        {
            foreach (var action in command.Config.PhoneActions)
            {
                var grid = PhoneController.PositionToGridCoord(player.activeTelephone.transform.position);
                var formated = string.Format(
                    action.Command,
                    player.UserIDString,
                    player.displayName,
                    player.transform.position.x,
                    player.transform.position.y,
                    player.transform.position.z,
                    grid,
                    command.Config.PhoneNumber
                );

                switch (action.Type)
                {
                    case CommandActionType.ServerCommand:
                        server.Command(formated);
                        break;
                    case CommandActionType.Broadcast:
                        server.Broadcast(formated);
                        break;
                    case CommandActionType.Reply:
                        player.ChatMessage(formated);
                        break;
                    case CommandActionType.PlayerChat:
                        if (_config.BlockChatCommandsBoundToPhoneNumber)
                            if (_allowedCommands.ContainsKey(player.userID))
                                _allowedCommands[player.userID]++;
                            else
                                _allowedCommands.Add(player.userID, 1);
                        player.Command($"chat.say \"{formated}\"");
                        break;
                    default:
                        continue;
                }
            }

            PlayEffect(player);
        }

        private void PlayEffect(BasePlayer player, bool failed = false)
        {
            NextFrame(() =>
            {
                if (player.HasActiveTelephone)
                    player.activeTelephone.ClearCurrentUser();
            });

            if (!_config.EffectEnabled)
                return;

            if (!failed)
                Effect.server.Run(_config.EffectSuccess, player.transform.position, Vector3.zero);
            else
                Effect.server.Run(_config.EffectFailed, player.transform.position, Vector3.zero);
        }

        private static class CommandActionType 
        {
            public const string ServerCommand = "command";
            public const string PlayerChat = "playerchat";
            public const string Broadcast = "broadcast";
            public const string Reply = "reply";
        }

        #endregion

        #region Data and Configuration

        private struct PhoneCommand
        {
            public Telephone Telephone { get; set; }
            public PhoneCommandConfig Config { get; set; }
        }

        private static PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                EffectEnabled = true,
                EffectFailed = "assets/prefabs/npc/scientist/sound/death.prefab",
                EffectSuccess = "assets/prefabs/npc/scientist/sound/respondok.prefab",
                AddCommandsToContactList = true,
                BlockChatCommandsBoundToPhoneNumber = false,
                PhoneCommands = new List<PhoneCommandConfig>
                {
                   new PhoneCommandConfig
                   {
                       PhoneNumber = 12345678,
                       PhoneName = "Hourly gift",
                       Cooldown = 3600,
                       Permission = "phonecommands.vip",
                       RestrictLandlinePhones = false,
                       RestrictMobilePhones = false,
                       RestrictPublicPhones = false,
                       PhoneActions = new List<PhoneAction>
                       {
                           new PhoneAction
                           {
                               Type = CommandActionType.ServerCommand,
                               Command = "inventory.giveto {0} xmas.present.large 1"
                           },
                           new PhoneAction
                           {
                               Type = CommandActionType.Broadcast,
                               Command = "{1} just recieved large gift! Call {6:#### ####} to get it too."
                           }
                       }
                   },
                   new PhoneCommandConfig
                   {
                       PhoneNumber = 87654321,
                       PhoneName = "Another example",
                       Cooldown = 0,
                       Permission = null,
                       RestrictLandlinePhones = false,
                       RestrictMobilePhones = false,
                       RestrictPublicPhones = false,
                       PhoneActions = new List<PhoneAction>
                       {
                           new PhoneAction
                           {
                               Type = CommandActionType.Reply,
                               Command = "Command executed - player id: {0}, player name: {1}, position x,y,z: {2},{3},{4}, grid coord: {5}, called number: {6:#### ####}"
                           }
                       }
                   }
                }
            };
        }

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            Config.WriteObject(GetDefaultConfig(), true);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
        }

        private void UpdateConfig()
        {
            Config.WriteObject(_config, true);
        }

        private class PluginConfig
        {
            public bool EffectEnabled { get; set; }
            public string EffectSuccess { get; set; }
            public string EffectFailed { get; set; }
            public bool AddCommandsToContactList { get; set; }
            public bool BlockChatCommandsBoundToPhoneNumber { get; set; }
            public List<PhoneCommandConfig> PhoneCommands { get; set; }
        }

        private struct PhoneCommandConfig
        {
            public int PhoneNumber { get; set; }
            public string PhoneName { get; set; }
            public int Cooldown { get; set; }
            public string Permission { get; set; }
            public bool RestrictMobilePhones { get; set; }
            public bool RestrictLandlinePhones { get; set; }
            public bool RestrictPublicPhones { get; set; }
            public List<PhoneAction> PhoneActions { get; set; }
        }

        private struct PhoneAction
        {
            public string Type { get; set; }
            public string Command { get; set; }
        }

        private class StoredData
        {
            public List<int> UsedNumbers = new List<int>();
            public Dictionary<ulong, Dictionary<int, long>> PlayerCooldowns = new Dictionary<ulong, Dictionary<int, long>>();
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _storedData);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandCooldown"] = "Cooldown remaining: {0} minutes",
                ["PermissionRequired"] = "Not allowed!",
                ["ChatCommandBlocked"] = "This chat command is disabled. Call {0:#### ####} instead.",
                ["DeviceRestricted"] = "This number cannot be called from your device. Try another.",
            }, this);
        }
    }

    #endregion
}