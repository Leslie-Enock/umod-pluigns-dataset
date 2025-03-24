using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Orange Lift", "Orange", "1.0.2")]
    [Description("Allows you to stop lifts at any level")]
    public class OrangeLift : RustPlugin
    {
        #region Oxide Hooks

        private void Init()
        {
            cmd.AddChatCommand("lift", this, nameof(cmdControlChat));
            cmd.AddChatCommand("lifts", this, nameof(cmdControlChat));
            //cmd.AddConsoleCommand("lifts.control", this, nameof(cmdControlConsole));
        }

        private void OnServerInitialized()
        {
            LoadLifts();
        }

        private void Unload()
        {
            DestroyLifts();
        }

        private object OnLiftUse(ProceduralLift entity, BasePlayer player)
        {
            var obj = entity.GetComponent<LiftExtended>();
            if (obj != null)
            {
                SendMessage(player, MessageType.OnLiftUse);
                obj.SetLastPassenger(player);
                obj.MoveToNextFloor();
                return true;
            }

            return null;
        }

        private void OnEntitySpawned(ProceduralLift entity)
        {
            var def = config.lifts.FirstOrDefault(x => x.Match(entity));
            if (def != null && def.floors?.Count > 0)
            {
                var lift = entity.gameObject.AddComponent<LiftExtended>();
                lift.definition = def;
            }
        }

        #endregion

        #region Commands

        private void cmdControlChat(BasePlayer player, string commands, string[] args)
        {
            if (args == null || args.Length < 1 || player.IsAdmin == false)
            {
                SendMessage(player, MessageType.Usage);
                return;
            }

            var action = args[0].ToLower();
            var lift = GetClosestLift(player);
            var component = lift?.GetComponent<LiftExtended>();
            var definition = component?.definition;
            var y = player.transform.position.y + 3.5f;

            switch (action)
            {
                default:
                    SendMessage(player, MessageType.Usage);
                    return;

                case "add":
                    if (lift == null)
                    {
                        SendMessage(player, MessageType.NoLift);
                        return;
                    }

                    if (definition != null)
                    {
                        SendMessage(player, MessageType.LiftNearby, "{name}", definition.name);
                        return;
                    }

                    definition = new LiftDefinition();
                    var position = lift.transform.position;
                    definition.position = position;
                    config.lifts.Add(definition);
                    component = lift.gameObject.AddComponent<LiftExtended>();
                    component.definition = definition;
                    SendMessage(player, MessageType.AddedLift, "{position}", definition.position);
                    break;

                case "remove":
                    if (definition == null)
                    {
                        SendMessage(player, MessageType.NoLift);
                        return;
                    }

                    config.lifts.Remove(definition);
                    UnityEngine.Object.Destroy(component);
                    SendMessage(player, MessageType.RemovedLift, "{position}", definition.position, "{name}",
                        definition.name);
                    break;

                case "addfloor":
                    if (definition == null)
                    {
                        SendMessage(player, MessageType.NoLift);
                        return;
                    }

                    y -= definition.position.y;
                    definition.floors.Add(y);
                    SendMessage(player, MessageType.AddedFloor, "{position}", $"{y:0.0}", "{name}", definition.name);
                    break;

                case "removefloor":
                    if (definition == null)
                    {
                        SendMessage(player, MessageType.NoLift);
                        return;
                    }

                    y -= definition.position.y;
                    definition.floors.RemoveAll(x => Math.Abs(x - y) < 1f);
                    SendMessage(player, MessageType.RemovedFloor, "{position}", $"{y:0.0}", "{name}", definition.name);
                    break;
            }

            SaveConfig();
        }

        private void cmdControlConsole(ConsoleSystem.Arg arg)
        {
            if (arg.Args == null || arg.Args.Length < 2)
            {
                return;
            }

            var player = arg.Player();
            var levelStr = arg.Args[0];
            var levelInt = 0;
            var idStr = arg.Args[1];
            var id = 0u;

            if (uint.TryParse(idStr, out id) == false || int.TryParse(levelStr, out levelInt))
            {
                return;
            }

            var entity = BaseNetworkable.serverEntities.Find(id);
            if (entity != null && player != null &&  Vector3.Distance(player.transform.position, entity.transform.position) < 3f)
            {
                var obj = entity.GetComponent<LiftExtended>();
                if (obj != null)
                {
                    obj.SelectFloor(levelInt);
                    player.ConsoleMessage($"Selecting floor {levelInt} for {id}");
                }
            }
        }

        #endregion

        #region Core

        private void LoadLifts()
        {
            timer.Once(1f, () =>
            {
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<ProceduralLift>())
                {
                    OnEntitySpawned(entity);
                }
            });
        }

        private void DestroyLifts()
        {
            foreach (var obj in UnityEngine.Object.FindObjectsOfType<LiftExtended>())
            {
                UnityEngine.Object.Destroy(obj);
            }
        }

        private static ProceduralLift GetClosestLift(BasePlayer player)
        {
            var lifts = new List<ProceduralLift>();
            Vis.Entities(player.transform.position, 50f, lifts);
            return lifts.FirstOrDefault();
        }

        #endregion

        #region Configuration | 24.05.2020

        private static ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Lifts")] public List<LiftDefinition> lifts = new List<LiftDefinition>();
        }

        private class LiftDefinition
        {
            [JsonProperty(PropertyName = "Name")] public string name = "Lift";

            [JsonProperty(PropertyName = "Position")]
            public Vector3 position;

            [JsonProperty(PropertyName = "Return delay time")]
            public float returnDelay = 10;

            [JsonProperty(PropertyName = "Floors")]
            public List<float> floors = new List<float>();

            public bool Match(BaseEntity entity)
            {
                return Vector3.Distance(entity.transform.position, position) < 10f;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                for (var i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }

                LoadDefaultConfig();
                return;
            }

            ValidateConfig();
            SaveConfig();
        }

        private void ValidateConfig()
        {
            if (ConVar.Server.hostname.Contains("[DEBUG]") == true)
            {
                PrintWarning("Using default configuration on debug server");
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Language | 24.05.2020

        private Dictionary<object, string> langMessages = new Dictionary<object, string>
        {
            {MessageType.OnLiftUse, "Thanks for using smart lifts!"},
            {
                MessageType.Usage, "Usage:\n" +
                                   "/lift add - add nearby lift as extended\n" +
                                   "/lift remove - remove nearby lift as extended\n" +
                                   "/lift addfloor - add floor to nearby lift\n" +
                                   "/lift removefloor - remove floor at nearby lift"
            },
            {MessageType.NoLift, "Can't find extended lift nearby!"},
            {MessageType.AddedLift, "Added extended lift on position {position}"},
            {MessageType.RemovedLift, "Removed extended lift on position {position} ({name})"},
            {MessageType.AddedFloor, "Added floor at level {position} for {name}"},
            {MessageType.RemovedFloor, "Removed floor at level {position} for {name}"},
            {MessageType.LiftNearby, "There are already extended lift nearby! ({name})"},
        };

        private enum MessageType
        {
            OnLiftUse,
            Usage,
            NoLift,
            AddedLift,
            RemovedLift,
            AddedFloor,
            RemovedFloor,
            LiftNearby,
        }

        protected override void LoadDefaultMessages()
        {
            var dictionary = new Dictionary<string, string>();
            foreach (var pair in langMessages)
            {
                var key = pair.Key.ToString();
                var value = pair.Value;
                dictionary.TryAdd(key, value);
            }

            lang.RegisterMessages(dictionary, this);
        }

        private string GetMessage(MessageType key, string playerID = null, params object[] args)
        {
            var keyString = key.ToString();
            var message = lang.GetMessage(keyString, this, playerID);
            if (message == keyString)
            {
                return $"{keyString} is not defined in lang!";
            }

            var organized = OrganizeArgs(args);
            message = ReplaceArgs(message, organized);
            return message;
        }

        private static Dictionary<string, object> OrganizeArgs(object[] args)
        {
            var dic = new Dictionary<string, object>();
            for (var i = 0; i < args.Length; i += 2)
            {
                var value = args[i].ToString();
                var nextValue = i + 1 < args.Length ? args[i + 1] : null;
                dic.TryAdd(value, nextValue);
            }

            return dic;
        }

        private static string ReplaceArgs(string message, Dictionary<string, object> args)
        {
            if (args == null || args.Count < 1)
            {
                return message;
            }

            foreach (var pair in args)
            {
                var s0 = "{" + pair.Key + "}";
                var s1 = pair.Key;
                var s2 = pair.Value != null ? pair.Value.ToString() : "null";
                message = message.Replace(s0, s2, StringComparison.InvariantCultureIgnoreCase);
                message = message.Replace(s1, s2, StringComparison.InvariantCultureIgnoreCase);
            }

            return message;
        }

        private void SendMessage(object receiver, MessageType key, params object[] args)
        {
            var userID = (receiver as BasePlayer)?.UserIDString;
            var message = GetMessage(key, userID, args);
            SendMessage(receiver, message);
        }

        private void SendMessage(object receiver, string message)
        {
            if (receiver == null)
            {
                Puts(message);
                return;
            }

            var console = receiver as ConsoleSystem.Arg;
            if (console != null)
            {
                SendReply(console, message);
                return;
            }

            var player = receiver as BasePlayer;
            if (player != null)
            {
                player.ChatMessage(message);
                return;
            }
        }

        #endregion

        #region Scripts

        private class LiftExtended : MonoBehaviour
        {
            public ProceduralLift entity;
            public int lastIndex = -1;
            public Vector3 movePosition;
            public LiftDefinition definition;
            public int maxFloors => definition.floors.Count;
            public BasePlayer lastPassenger;
            public bool paused = true;

            private void Awake()
            {
                entity = GetComponent<ProceduralLift>();
                OnDestroy();
            }

            private void OnDestroy()
            {
                if (definition != null)
                {
                    entity.transform.position = definition.position;
                    entity.SetFlag(BaseEntity.Flags.Busy, false);
                    entity.SendNetworkUpdateImmediate();
                }
            }

            public void SetLastPassenger(BasePlayer player)
            {
                lastPassenger = player;
            }

            public void MoveToNextFloor()
            {
                SelectFloor(++lastIndex);
            }

            public void SelectFloor(int floor)
            {
                if (paused == false)
                {
                    return;
                }

                if (floor >= maxFloors || floor < 0)
                {
                    floor = 0;
                    lastIndex = -1;
                    movePosition = definition.position;
                }
                else
                {
                    movePosition = GetFloor(floor);
                    lastIndex = floor;
                }

                OnStartedMoving();
            }

            public Vector3 GetFloor(int index)
            {
                return definition.position + new Vector3(0, definition.floors[index], 0);
            }

            public void MoveToStart()
            {
                // TODO: Add check for players inside
                SelectFloor(-1);
            }

            public void Update()
            {
                if (paused == true)
                {
                    return;
                }

                if (entity.transform.position == movePosition)
                {
                    OnFinishedMoving();
                    return;
                }

                entity.transform.position =
                    Vector3.MoveTowards(entity.transform.position, movePosition, 1 * Time.deltaTime);
                entity.SendNetworkUpdateImmediate();
            }

            private void OnStartedMoving()
            {
                entity.SetFlag(BaseEntity.Flags.Busy, true);
                entity.SendNetworkUpdate();

                if (lastPassenger != null && lastPassenger.IsAdmin)
                {
                    lastPassenger.ConsoleMessage(
                        $"Moving to index = {lastIndex}, floor = {lastIndex + 1}, y = {movePosition.y}");
                }

                paused = false;
            }

            private void OnFinishedMoving()
            {
                paused = true;
                entity.SetFlag(BaseEntity.Flags.Busy, false);
                entity.SendNetworkUpdate();
                if (movePosition != definition.position && definition.returnDelay > 0)
                {
                    CancelInvoke(nameof(MoveToStart));
                    Invoke(nameof(MoveToStart), definition.returnDelay);
                }
            }
        }

        #endregion
    }
}