using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text;
using System;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Timed Command Blocker", "MON@H", "1.3.1")]
    [Description("Block commands temporarily or permanently")]
    public class TimedCommandBlocker : RustPlugin
    {
        #region Variables

        private const string PermissionUse = "timedcommandblocker.use";
        private const string PermissionImmunity = "timedcommandblocker.immunity";
        private readonly List<BlockedCommand> _blockedCommands = new List<BlockedCommand>();
        private readonly StringBuilder _sb = new StringBuilder();

        private class BlockedCommand
        {
            public string Command { get; set; }
            public double BlockDuration { get; set; }
            public TimeSpan GetTimeUntilUnlock()
            {
                return SaveRestore.SaveCreatedTime.AddSeconds(BlockDuration) - DateTime.Now;
            }
        }

        #endregion Variables

        #region Initialization

        private void Init()
        {
            UnsubscribeHooks();

            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionImmunity, this);

            LoadDefaultMessages();
        }

        private void OnServerInitialized()
        {
            if (_configData.BlockedCommands.Count > 0)
            {
                foreach (BlockedCommand blockedCommand in _configData.BlockedCommands)
                {
                    if (blockedCommand.BlockDuration < 0 || blockedCommand.GetTimeUntilUnlock().TotalSeconds > 0)
                    {
                        _blockedCommands.Add(blockedCommand);
                    }
                }

                if (_blockedCommands.Count > 0)
                {
                    _blockedCommands.Sort((a, b) => a.Command.CompareTo(b.Command));

                    SubscribeHooks();
                }
            }
        }

        #endregion Initialization

        #region Configuration

        private ConfigData _configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Enable logging")]
            public bool LoggingEnabled = false;

            [JsonProperty(PropertyName = "Chat steamID icon")]
            public ulong SteamIDIcon = 0;

            [JsonProperty(PropertyName = "Сommand color")]
            public string ChatCommandColor = "#FFFF00";

            [JsonProperty(PropertyName = "Remaining blocking time color")]
            public string ChatCommandArgumentColor = "#FFA500";

            [JsonProperty(PropertyName = "Blocked commands (command: seconds)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<BlockedCommand> BlockedCommands = new List<BlockedCommand>()
            {
                new BlockedCommand {
                    Command = "Blocked for 1 day",
                    BlockDuration = 86400
                },
                new BlockedCommand {
                    Command = "Blocked permanently",
                    BlockDuration = -1
                },
                new BlockedCommand {
                    Command = "Not blocked",
                    BlockDuration = 0
                }
            };
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
            public static class Format
            {
                private const string Base = nameof(Format) + ".";
                public const string Days = Base + nameof(Days);
                public const string Hours = Base + nameof(Hours);
                public const string Minutes = Base + nameof(Minutes);
                public const string Day = Base + nameof(Day);
                public const string Hour = Base + nameof(Hour);
                public const string Minute = Base + nameof(Minute);
                public const string Prefix = Base + nameof(Prefix);
                public const string Second = Base + nameof(Second);
                public const string Seconds = Base + nameof(Seconds);
            }
            
            public static class Info
            {
                private const string Base = nameof(Info) + ".";
                public const string Blocked = Base + nameof(Blocked);
                public const string Timed = Base + nameof(Timed);
            }
        }


        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Format.Day] = "day",
                [LangKeys.Format.Days] = "days",
                [LangKeys.Format.Hour] = "hour",
                [LangKeys.Format.Hours] = "hours",
                [LangKeys.Format.Minute] = "minute",
                [LangKeys.Format.Minutes] = "minutes",
                [LangKeys.Format.Prefix] = "<color=#00FF00>[Timed Command Blocker]</color>: ",
                [LangKeys.Format.Second] = "second",
                [LangKeys.Format.Seconds] = "seconds",
                [LangKeys.Info.Blocked] = "Command <color={0}>{1}</color> is blocked.",
                [LangKeys.Info.Timed] = "Command <color={0}>{1}</color> is blocked.\nUnblocking in {2}.",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                [LangKeys.Format.Day] = "день",
                [LangKeys.Format.Days] = "дней",
                [LangKeys.Format.Hour] = "час",
                [LangKeys.Format.Hours] = "часов",
                [LangKeys.Format.Minute] = "минуту",
                [LangKeys.Format.Minutes] = "минут",
                [LangKeys.Format.Prefix] = "<color=#00FF00>[Временная блокировка команд]</color>: ",
                [LangKeys.Format.Second] = "сукунду",
                [LangKeys.Format.Seconds] = "секунд",
                [LangKeys.Info.Blocked] = "Команда <color={0}>{1}</color> заблокирована.",
                [LangKeys.Info.Timed] = "Команда <color={0}>{1}</color> заблокирована.\nРазблокировка через: {2}.",
            }, this, "ru");
        }

        #endregion Localization

        #region Oxide Hooks

        private object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (player != null)
            {
                if (args != null && args.Length != 0)
                {
                    foreach (string arg in args)
                    {
                        command += $" {arg}";
                    }
                }

                return HandleCommand(player, command);
            }

            return null;
        }

        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();

            if (player != null && arg.cmd.FullName != "chat.say")
            {
                string command = arg.cmd.Name;
                string fullCommand = arg.cmd.FullName;

                if (!string.IsNullOrEmpty(arg.FullString))
                {
                    command += $" {arg.FullString}";
                    fullCommand += $" {arg.FullString}";
                }

                return HandleCommand(player, command) ?? HandleCommand(player, fullCommand);
            }

            return null;
        }

        #endregion Oxide Hooks

        #region Core

        private object HandleCommand(BasePlayer player, string command)
        {
            if (permission.UserHasPermission(player.UserIDString, PermissionUse)
            && !permission.UserHasPermission(player.UserIDString, PermissionImmunity))
            {
                for (int i = 0; i < _blockedCommands.Count; i++)
                {
                    BlockedCommand blockedCommand = _blockedCommands[i];

                    if (command.StartsWith(blockedCommand.Command, StringComparison.OrdinalIgnoreCase))
                    {
                        if (blockedCommand.BlockDuration < 0)
                        {
                            Log($"{player.userID} {player.displayName} command blocked: {command}");
                            PlayerSendMessage(player, Lang(LangKeys.Info.Blocked, player.UserIDString, _configData.ChatCommandColor, command));
                            return true;
                        }

                        TimeSpan timeRemaining = blockedCommand.GetTimeUntilUnlock();

                        if (timeRemaining.TotalSeconds > 0)
                        {
                            Log($"{player.userID} {player.displayName} command blocked: {command} for: {timeRemaining.TotalSeconds.ToString("F0")} seconds");
                            PlayerSendMessage(player, Lang(LangKeys.Info.Timed, player.UserIDString, _configData.ChatCommandColor, command, GetFormattedDurationTime(timeRemaining, player.UserIDString)));
                            return true;
                        }

                        _blockedCommands.Remove(blockedCommand);
                        break;
                    }
                }

                if (_blockedCommands.Count == 0)
                {
                    UnsubscribeHooks();
                }
            }

            return null;
        }

        #endregion Core

        #region Helpers

        private void UnsubscribeHooks()
        {
            Unsubscribe(nameof(OnPlayerCommand));
            Unsubscribe(nameof(OnServerCommand));
        }

        private void SubscribeHooks()
        {
            Subscribe(nameof(OnPlayerCommand));
            Subscribe(nameof(OnServerCommand));
        }


        private string GetFormattedDurationTime(TimeSpan time, string id = null)
        {
            _sb.Clear();

            if (time.Days > 0)
            {
                BuildTime(_sb, time.Days == 1 ? LangKeys.Format.Day : LangKeys.Format.Days, id, time.Days);
            }

            if (time.Hours > 0)
            {
                BuildTime(_sb, time.Hours == 1 ? LangKeys.Format.Hour : LangKeys.Format.Hours, id, time.Hours);
            }

            if (time.Minutes > 0)
            {
                BuildTime(_sb, time.Minutes == 1 ? LangKeys.Format.Minute : LangKeys.Format.Minutes, id, time.Minutes);
            }

            BuildTime(_sb, time.Seconds == 1 ? LangKeys.Format.Second : LangKeys.Format.Seconds, id, time.Seconds);

            return _sb.ToString();
        }

        private void BuildTime(StringBuilder sb, string lang, string playerId, int value)
        {
            sb.Append("<color=#FFA500>");
            sb.Append(value);
            sb.Append("</color> ");
            sb.Append(Lang(lang, playerId));
            sb.Append(" ");
        }

        private void PlayerSendMessage(BasePlayer player, string message)
        {
            player.SendConsoleCommand("chat.add", 2, _configData.SteamIDIcon, string.IsNullOrEmpty(Lang(LangKeys.Format.Prefix, player.UserIDString)) ? message : Lang(LangKeys.Format.Prefix, player.UserIDString) + message);
        }

        private void Log(string text)
        {
            if (_configData.LoggingEnabled)
            {
                LogToFile("log", $"{DateTime.Now.ToString("HH:mm:ss")} {text}", this);                
            }
        }

        #endregion Helpers
    }
}