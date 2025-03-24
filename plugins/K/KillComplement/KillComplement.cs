using Oxide.Core;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Kill Complement", "ProCelle", "1.0.5")]
    [Description("Get health and reload your gun for killing another player")]
    public class KillComplement : RustPlugin
    {
        private PluginConfig config;
        void OnServerInitialized(bool initial)
        {
		config = Config.ReadObject<PluginConfig>();
        permission.RegisterPermission(USE, this);
        }     
        private const string USE = "killcomplement.use";           
        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            var starter = info?.InitiatorPlayer;
            var projectile = info.Weapon as BaseProjectile; 
            if (starter == null ||  projectile == null || player.userID.IsSteamId() == false || starter?.userID.IsSteamId() == false && !permission.UserHasPermission(player.UserIDString, USE))
            {
                return;
            }            
            starter.Heal(config.Heal);       
            projectile.primaryMagazine.contents = projectile.primaryMagazine.capacity;
            projectile.SendNetworkUpdateImmediate(); 
            
            
        }
        private class PluginConfig
		{
			[JsonProperty("Healed ammoun on kill")] public float Heal { get; set; }
		}

		private PluginConfig GetDefaultConfig()
		{
			return new PluginConfig
			{
				Heal = 100f
			};
		}

		protected override void LoadDefaultConfig()
		{
			Config.WriteObject(GetDefaultConfig(), true);
		}
        

    }



}