namespace Oxide.Plugins
{

    [Info("Connect Respawn", "Tryhard", "1.0.2")]
    [Description("Automatically respawns players upon connection")]
    public class ConnectRespawn : RustPlugin
    {

        private void Init() => permission.RegisterPermission("ConnectRespawn.allowed", this);


        private void OnPlayerConnected(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, "ConnectRespawn.allowed") && player.IsDead())
               player.Respawn();
        }
      
    }
}
