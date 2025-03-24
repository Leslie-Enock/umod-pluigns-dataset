namespace Oxide.Plugins
{
    [Info("Wake Up", "Orange", "1.0.2")]
    [Description("Automatically wakes players up from sleep")]
    public class WakeUp : RustPlugin
    {
        private const string permUse = "wakeup.use";

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
        }
        
        private void OnPlayerConnected(BasePlayer player)
        {
            OnPlayerSleep(player);
        }
        
        private void OnPlayerSleep(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permUse) == true)
            {
                Wakeup(player);
            }
        }

        private void Wakeup(BasePlayer player)
        {
            if (player.IsConnected == false)
            {
                return;
            }
            
            if (player.IsReceivingSnapshot == true)
            {
                timer.Once(1f, () => Wakeup(player));
                return;
            }
            
            player.EndSleeping();
        }
    }
}