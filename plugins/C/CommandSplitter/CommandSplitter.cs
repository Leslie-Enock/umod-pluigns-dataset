using System;
using Network;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Command Splitter", "birthdates", "1.0.1")]
    [Description("Split one client command into multiple")]
    public class CommandSplitter : CovalencePlugin
    {
        #region Variables

        private const string UsePermission = "commandsplitter.use";

        #endregion

        #region Helpers

        /// <summary>
        ///     Send console reply to <paramref name="connection" />
        /// </summary>
        /// <param name="connection">Target connection</param>
        /// <param name="reply">String reply</param>
        private static void SendReply(Connection connection, string reply)
        {
            if (string.IsNullOrEmpty(reply) || !Net.sv.IsConnected()) return;

            using (NetWrite write = Net.sv.StartWrite())
            {
                write.PacketID(Message.Type.ConsoleMessage);
                write.String(reply);
                write.Send(new SendInfo(connection));
            }
        }

        #endregion

        #region Hooks

        /// <summary>
        ///     On client command, split it into multiple
        /// </summary>
        /// <param name="connection">Target connection</param>
        /// <param name="command">Target command(s)</param>
        /// <returns>True, if we should cancel. Null, if we shouldn't</returns>
        private object OnClientCommand(Connection connection, string command)
        {
            var player = connection.player as BasePlayer;
            if (player == null || !player.IPlayer.HasPermission(UsePermission)) return null;
            var commands = command.Split(_config.Separator.ToCharArray());
            foreach (var cmd in commands)
            {
                var trim = cmd.Trim();
                if (string.IsNullOrEmpty(trim)) continue;
                var reply = ConsoleSystem.Run(ConsoleSystem.Option.Server.FromConnection(connection).Quiet(), trim,
                    Array.Empty<object>());
                SendReply(connection, reply);
            }

            return true;
        }

        private void Init()
        {
            permission.RegisterPermission(UsePermission, this);
        }

        #endregion

        #region Configuration

        private ConfigFile _config;

        public class ConfigFile
        {
            [JsonProperty("Command Separator")] public string Separator { get; set; }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    Separator = ";"
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