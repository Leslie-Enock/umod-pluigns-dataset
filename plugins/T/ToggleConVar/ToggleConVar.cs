using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Toggle ConVar", "birthdates", "1.0.3")]
    [Description("Toggle ConVars between two values")]
    public class ToggleConVar : RustPlugin
    {
        #region Core Logic

        private const string UsePermission = "toggleconvar.use";

        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
        }

        /// <summary>
        ///     This variable handles the active timers and handles any overwrites by resetting the <see cref="Timer" />
        /// </summary>
        private readonly IDictionary<ulong, Timer> _timers =
            new Dictionary<ulong, Timer>();

        /// <summary>
        ///     Reply to a console command
        /// </summary>
        /// <param name="arg">Target command</param>
        /// <param name="msg">Message to reply with</param>
        /// <param name="error">Is this an error?</param>
        private void Reply(ConsoleSystem.Arg arg, string msg, bool error = true)
        {
            arg.ReplyWith(msg);
            if (!_config.ReplyWithGameTips) return;
            var player = arg.Player();
            if (player == null) return;
            ShowGameTip(player, msg, error);
            HideGameTipLater(player, _config.GameTipTime);
        }

        /// <summary>
        ///     The main command to toggle a <see cref="ConVar" />
        /// </summary>
        /// <param name="arg">Given arguments</param>
        [ConsoleCommand("server.toggle")]
        private void ToggleCommand(ConsoleSystem.Arg arg)
        {
            if (arg == null) return;
            var player = arg.Player();
            if (player && !player.IPlayer.HasPermission(UsePermission)) return;
            if (arg.Args == null || arg.Args.Length < 3)
            {
                Reply(arg, "Invalid args");
                return;
            }

            var conVar = arg.Args[0];
            var firstValue = arg.Args[1];
            var currentValue = RunSilentCommand(conVar);
            if (currentValue == null)
            {
                Reply(arg, "Invalid ConVar!");
                return;
            }

            var currentValueStr = currentValue.cmd.String;
            var change = !currentValueStr.Equals(firstValue) ? firstValue : arg.Args[2];
            RunSilentCommand(conVar, change);
            Reply(arg, $"{currentValue.cmd.FullName} has been changed to {change}", false);
        }

        #endregion

        #region Helpers

        /// <summary>
        ///     Run a console command without any output into the console
        /// </summary>
        /// <param name="strCommand">Target command</param>
        /// <param name="args">Target args</param>
        /// <returns>A <see cref="ConsoleSystem.Arg" /> with the outcome of the command</returns>
        private static ConsoleSystem.Arg RunSilentCommand(string strCommand, params object[] args)
        {
            var command = ConsoleSystem.BuildCommand(strCommand, args);
            var arg = new ConsoleSystem.Arg(ConsoleSystem.Option.Unrestricted, command);
            if (arg.Invalid || !arg.cmd.Variable) return null;

            var oldArgs = ConsoleSystem.CurrentArgs;
            ConsoleSystem.CurrentArgs = arg;
            arg.cmd.Call(arg);
            ConsoleSystem.CurrentArgs = oldArgs;
            return arg;
        }

        /// <summary>
        ///     Show a game tip to <paramref name="player" /> with the message <seealso cref="msg" />
        /// </summary>
        /// <param name="player">Target player</param>
        /// <param name="msg">Target message</param>
        /// <param name="error">Should we color this red?</param>
        private static void ShowGameTip(BasePlayer player, string msg, bool error = true)
        {
            var style = error ? "1" : "0";
            player.SendConsoleCommand($"gametip.showtoast_translated {style} _ \"{msg}\"");
        }

        /// <summary>
        ///     Hide a game tip sent to a player in a later time
        /// </summary>
        /// <param name="player">Target player</param>
        /// <param name="time">Target time (seconds)</param>
        private void HideGameTipLater(BasePlayer player, float time)
        {
            Timer playerTimer;
            if (!_timers.TryGetValue(player.userID, out playerTimer))
            {
                var id = player.userID;
                _timers[id] = timer.In(time, () =>
                {
                    HideGameTip(player);
                    _timers.Remove(id);
                });

                return;
            }

            playerTimer.Reset(time);
        }

        /// <summary>
        ///     Hide a game tip
        /// </summary>
        /// <param name="player">Target player</param>
        private static void HideGameTip(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;
            player.SendConsoleCommand("gametip.hidegametip");
        }

        #endregion

        #region Configuration

        private ConfigFile _config;

        public class ConfigFile
        {
            [JsonProperty("Reply with Game Tips")] public bool ReplyWithGameTips { get; set; }

            [JsonProperty("Game Tip Time")] public float GameTipTime { get; set; }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    ReplyWithGameTips = true,
                    GameTipTime = 3f
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null) LoadDefaultConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion
    }
}