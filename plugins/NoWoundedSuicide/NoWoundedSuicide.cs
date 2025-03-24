using UnityEngine;

namespace Oxide.Plugins
{
    [Info("No Wounded Suicide", "Orange", "1.0.1")]
    [Description("Blocks suicide when player is wounded")]
    public class NoWoundedSuicide : RustPlugin
    {
        private object OnServerCommand(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            
            if (player == null)
            {
                return null;
            }

            if (arg.cmd?.FullName == "global.kill" && player.IsWounded())
            {
                return true;
            }
            
            return null;
        }
    }
}