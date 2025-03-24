using Network;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Auth Limiter", "supreme", "1.0.2")]
    [Description("Limits the moderators from using ownerid/removeownerid commands")]
    public class AuthLimiter : RustPlugin
    {
        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null || !IsLimited(player))
                return null;

            var command = arg.cmd.FullName;
            return arg.cmd.FullName == "global.ownerid" || arg.cmd.FullName == "global.removeowner" ? false : (object) null;
        }
        private bool IsLimited(BasePlayer player)
        {
            if (player == null || player.net?.connection == null)
                return false;

            if (player.net.connection.authLevel == 1)
                return true;

            return false;
        }
    }
}