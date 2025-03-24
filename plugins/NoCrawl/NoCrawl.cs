namespace Oxide.Plugins
{
    [Info("No Crawl", "Bushhy", "1.0.1")]
    [Description("A simple plugin to disable the crawling state.")]
    public class NoCrawl : RustPlugin
    {
        object OnPlayerWound(BasePlayer player, HitInfo hitInfo)
        {
        	NextFrame(() => {
            	if (player == null) return;
            	if (player.HasPlayerFlag(BasePlayer.PlayerFlags.Wounded))
            	{
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.Incapacitated, b: true);
            	}
            });
            return null;            
        }
    }
}