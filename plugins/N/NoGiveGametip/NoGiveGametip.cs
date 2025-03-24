using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("No Give Gametip", "noname", "1.0.1")]
    [Description("Clears the GameTip from using the giveid command.")]
    class NoGiveGametip : CovalencePlugin
    {
        private void OnServerCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null)
                return;

            if (arg.cmd.Name == "giveid")
            {
                NextTick(() => (arg.Connection.player as BasePlayer).SendConsoleCommand("gametip.hidegametip"));
            }
        }
    }
}