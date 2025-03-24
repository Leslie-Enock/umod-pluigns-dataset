//#define DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Zone Command", "misticos", "1.0.0")]
    [Description("Execute commands when player enters a zone")]
    class ZoneCommand : CovalencePlugin
    {
        #region Variables

        private const string PermissionCommand = "zonecommand.command";

        [PluginReference("PlaceholderAPI")]
        private Plugin _placeholders = null;

        private Action<IPlayer, StringBuilder, bool> _placeholderProcessor = null;
        private StringBuilder _builder = new StringBuilder();

        #endregion

        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty("Commands")]
            public string[] Commands = { "zone", "zonecommand", "zc" };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion

        #region Work with Data

        #region ZoneData

        private Dictionary<string, HashSet<ZoneAction>> _actionsPerZone = new Dictionary<string, HashSet<ZoneAction>>();
        private Dictionary<string, ZoneData> _loadedData = new Dictionary<string, ZoneData>();

        private string[] GetAllData()
        {
            try
            {
                return Interface.Oxide.DataFileSystem.GetFiles(nameof(ZoneCommand));
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        private void SaveData(string filename)
        {
            ZoneData data;
            if (!_loadedData.TryGetValue(filename, out data))
                return;

            Interface.Oxide.DataFileSystem.WriteObject(nameof(ZoneCommand) + Path.DirectorySeparatorChar + filename,
                data);
        }

        private ZoneData GetOrLoadData(string filename)
        {
            ZoneData data;
            if (_loadedData.TryGetValue(filename, out data))
                return data;

            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<ZoneData>(nameof(ZoneCommand) +
                                                                           Path.DirectorySeparatorChar + filename);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            // 300 IQ move xd
            _loadedData[filename] = data = data ?? new ZoneData();

            HashSet<ZoneAction> actions;
            if (!_actionsPerZone.TryGetValue(data.Zone, out actions))
                _actionsPerZone[data.Zone] = actions = new HashSet<ZoneAction>();

            foreach (var action in data.Actions)
            {
                actions.Add(action);
            }

            return data;
        }

        private class ZoneData
        {
            [JsonIgnore]
            public string Filename = string.Empty;

            [JsonProperty("Zone ID")] // array?
            public string Zone = string.Empty;

            [JsonProperty("Actions", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ZoneAction> Actions = new List<ZoneAction> { new ZoneAction() };
        }

        private class ZoneAction
        {
            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty("Frequency")]
            public FrequencyMode Frequency = FrequencyMode.Always;

            [JsonProperty("Time Frequency")]
            public TimeSpan? Time = null;

            [JsonProperty("Use Game Time")]
            public bool GameTime = false;

            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty("Run On")]
            public RunMode RunOn = RunMode.Enter;

            [JsonProperty("Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ZoneCommand> Commands = new List<ZoneCommand> { new ZoneCommand() };

            public class ZoneCommand
            {
                [JsonProperty("Command")]
                public string Command = string.Empty;

                [JsonProperty("Arguments", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public object[] Arguments = Array.Empty<object>();

                [JsonProperty("Clientside")]
                public bool Clientside = false;

                [JsonProperty("Chat Command")]
                public bool Chat = false;

                public void Execute(BasePlayer player, Oxide.Plugins.ZoneCommand instance)
                {
                    var command = Command;
                    if (Chat && Clientside)
                        command = '/' + command;

                    command = ConsoleSystem.BuildCommand(command, Arguments);

                    // TODO: own BuildCommand with placeholders per argument? for better performance with string builder and such
                    if (instance._placeholderProcessor != null)
                    {
                        instance._builder.Append(command);
                        instance._placeholderProcessor.Invoke(player.IPlayer, instance._builder, false);

                        command = instance._builder.ToString();
                        instance._builder.Clear();
                    }

                    if (Chat && Clientside)
                        command = ConsoleSystem.BuildCommand("chat.say", command);

#if DEBUG
                    instance.Puts($"Executing (Clientside: {Clientside} / For: {player.UserIDString}): {command}");
#endif

                    if (Clientside)
                    {
                        player.SendConsoleCommand(command);
                    }
                    else
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, command);
                }
            }

            public enum FrequencyMode
            {
                Once,
                Always,
                PerPlayer
            }

            public enum RunMode
            {
                Enter,
                Exit
            }
        }

        #endregion

        #region ZoneMeta

        private Dictionary<string, ZoneActionMeta> _metas = new Dictionary<string, ZoneActionMeta>();

        private string[] GetAllMeta()
        {
            try
            {
                return Interface.Oxide.DataFileSystem.GetFiles(nameof(ZoneCommand) + "Meta");
            }
            catch (Exception)
            {
                return Array.Empty<string>();
            }
        }

        private void SaveMeta(string filename)
        {
            ZoneActionMeta data;
            if (!_metas.TryGetValue(filename, out data))
                return;

            Interface.Oxide.DataFileSystem.WriteObject(
                nameof(ZoneCommand) + "Meta" + Path.DirectorySeparatorChar + filename, data);
        }

        private ZoneActionMeta GetOrLoadMeta(string filename)
        {
            ZoneActionMeta data;
            if (_metas.TryGetValue(filename, out data))
                return data;

            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<ZoneActionMeta>(nameof(ZoneCommand) + "Meta" +
                    Path.DirectorySeparatorChar + filename);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            return _metas[filename] = data ?? new ZoneActionMeta();
        }

        private class ZoneActionMeta
        {
            public Dictionary<ulong, List<DateTime>> TriggerTime = new Dictionary<ulong, List<DateTime>>();
            public Dictionary<ulong, List<DateTime>> TriggerGameTime = new Dictionary<ulong, List<DateTime>>();

            public List<DateTime> GetTriggerTime(ulong id)
            {
                List<DateTime> times;
                if (TriggerTime.TryGetValue(id, out times))
                    return times;

                return TriggerTime[id] = new List<DateTime>();
            }

            public List<DateTime> GetTriggerGameTime(ulong id)
            {
                List<DateTime> times;
                if (TriggerGameTime.TryGetValue(id, out times))
                    return times;

                return TriggerGameTime[id] = new List<DateTime>();
            }
        }

        #endregion

        #endregion

        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "No Permission", "You do not have enough permissions" },
                {
                    "Command: Syntax", "Syntax:\n" +
                                       "create [Zone ID] - Create a datafile"
                },
                {
                    "Command: Create: Done",
                    "Created a datafile: /oxide/data/ZoneCommand/{filename}.json (You can rename it)"
                }
            }, this);
        }

        private void Init()
        {
            permission.RegisterPermission(PermissionCommand, this);

            AddCovalenceCommand(_config.Commands, nameof(CommandZone));

            var json = ".json".Length;
            foreach (var filename in GetAllData())
            {
                var slashIndex = filename.LastIndexOf(Path.DirectorySeparatorChar);
                var name = filename.Substring(slashIndex + 1, filename.Length - slashIndex - 1 - json);
#if DEBUG
                Puts($"Found file: {filename}\nCharacter: {Path.DirectorySeparatorChar}, Index: {slashIndex}; Name: {name}");
#endif

                GetOrLoadData(name);
            }
        }

        private void OnServerSave()
        {
            var i = 1;
            foreach (var filename in _metas.Keys)
            {
                timer.Once(i++, () => SaveMeta(filename));
            }
        }

        private void OnEnterZone(string zoneId, BasePlayer player)
        {
            OnZone(zoneId, player, ZoneAction.RunMode.Enter);
        }

        private void OnExitZone(string zoneId, BasePlayer player)
        {
            OnZone(zoneId, player, ZoneAction.RunMode.Exit);
        }

        private void OnPlaceholderAPIReady()
        {
            _placeholderProcessor =
                _placeholders.Call<Action<IPlayer, StringBuilder, bool>>("GetProcessPlaceholders", 1);
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            if (plugin?.Name != "PlaceholderAPI")
                return;

            _placeholderProcessor = null;
        }

        #endregion

        #region Handling

        private void OnZone(string id, BasePlayer player, ZoneAction.RunMode mode)
        {
#if DEBUG
            Puts($"OnZone called: {id} for {player.displayName}: {mode}");
#endif

            HashSet<ZoneAction> actions;
            if (!_actionsPerZone.TryGetValue(id, out actions))
            {
#if DEBUG
                Puts("No actions were found");
#endif
                return;
            }

            var meta = GetOrLoadMeta(id);

            var now = DateTime.UtcNow;
            var gameNow = TOD_Sky.Instance.Cycle.DateTime;

            var triggerTime = meta.GetTriggerTime(player.userID);
            var triggerGameTime = meta.GetTriggerGameTime(player.userID);

            foreach (var action in actions)
            {
#if DEBUG
                Puts($"Proceeding with Action.RunOn: {action.RunOn} / {action.Frequency}");
#endif
                if (action.RunOn != mode)
                    continue;

                switch (action.Frequency)
                {
                    case ZoneAction.FrequencyMode.Always:
                    {
                        break;
                    }

                    case ZoneAction.FrequencyMode.Once:
                    {
                        var list = action.GameTime ? meta.TriggerGameTime : meta.TriggerTime;
                        if (action.Time == null)
                        {
                            if (list.Count == 0)
                                break;

#if DEBUG
                            Puts("Once ded :/");
#endif
                            continue;
                        }

                        var flag = false;
                        foreach (var kvp in list)
                        {
                            if (kvp.Value.Count == 0)
                                continue;

                            if (now - kvp.Value[0] < action.Time.Value)
                                continue;

#if DEBUG
                            Puts("Once ded :/ but with time");
#endif
                            flag = true;
                            break;
                        }

                        if (flag)
                            continue;

                        break;
                    }

                    case ZoneAction.FrequencyMode.PerPlayer:
                    {
                        var list = action.GameTime
                            ? meta.GetTriggerGameTime(player.userID)
                            : meta.GetTriggerTime(player.userID);

                        if (action.Time == null)
                        {
                            if (list.Count == 0)
                                break;

#if DEBUG
                            Puts("PerPlayer ded :/");
#endif
                            continue;
                        }

                        if (list.Count != 0 && now - list[0] > action.Time.Value)
                        {
#if DEBUG
                            Puts("PerPlayer ded :/ but with time");
#endif
                            continue;
                        }

                        break;
                    }
                }

                foreach (var command in action.Commands)
                {
                    command.Execute(player, this);
                }
            }

            if (mode == ZoneAction.RunMode.Enter)
            {
                triggerTime.Add(now);
                triggerGameTime.Add(gameNow);
            }
        }

        #endregion

        #region Commands

        private void CommandZone(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(PermissionCommand))
            {
                player.Reply(GetMsg("No Permission", player.Id));
                return;
            }

            if (args.Length == 0)
                goto syntax;

            switch (args[0].ToLower())
            {
                case "create":
                {
                    var filename = Guid.NewGuid().ToString("N");
                    var zone = GetOrLoadData(filename);
                    if (args.Length >= 2)
                        zone.Zone = string.Join(" ", args.Skip(1));

                    SaveData(filename);
                    player.Reply(GetMsg("Command: Create: Done", player.Id).Replace("{filename}", filename));
                    return;
                }
            }

            syntax:
            player.Reply(GetMsg("Command: Syntax", player.Id));
        }

        #endregion

        #region Helpers

        private string GetMsg(string key, string id) => lang.GetMessage(key, this, id);

        #endregion
    }
}