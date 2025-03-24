namespace Oxide.Plugins
{
    [Info("Reset Hostile On Death", "WhiteThunder", "1.0.0")]
    [Description("Resets player hostile status on death.")]
    public class ResetHostileOnDeath : CovalencePlugin
    {
        private void OnPlayerDeath(BasePlayer player)
        {
            if (player.IsNpc || !player.userID.IsSteamId() || !player.IsHostile())
                return;

            player.State.unHostileTimestamp = Network.TimeEx.currentTimestamp;
            player.MarkHostileFor(0);
        }
    }
}