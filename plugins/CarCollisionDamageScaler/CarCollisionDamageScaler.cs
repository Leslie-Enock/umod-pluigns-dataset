using Oxide.Core.Plugins;

namespace Oxide.Plugins
{

    [Info("Car Collision Damage Scaler", "Zugzwang", "1.0.2")]
    [Description("Scales collision damage on the new modular vehicles.")]	
	
    public class CarCollisionDamageScaler : CovalencePlugin
    {
		#region Permissions and Config

		private const string permissionGod = "carcollisiondamagescaler.god";
		private const string permissionScale = "carcollisiondamagescaler.scale";

		private void Init()
		{
			permission.RegisterPermission(permissionGod, this);
			permission.RegisterPermission(permissionScale, this);
			config = Config.ReadObject<PluginConfig>();
		}	
		
		private PluginConfig config;
		
		private class PluginConfig
		{
			public bool requireDriver;
			public bool requirePermission;
			public float scale;
			
			public PluginConfig()
			{
				requireDriver = false; 
				requirePermission = false; 
				scale = 0.5f;
			}
		}	
		
		protected override void LoadDefaultConfig() 
		{
			Config.WriteObject(new PluginConfig(), true);
		}

		#endregion Permissions and Config

		
		void OnEntityTakeDamage(BaseVehicleModule entity, HitInfo info)
		{
			if (info?.damageTypes.Has(Rust.DamageType.Collision) != true)
				return;

			BasePlayer driver = entity.Vehicle?.GetDriver();

			if (driver != null && permission.UserHasPermission(driver.UserIDString, permissionGod))
			{
				info?.damageTypes.Scale(Rust.DamageType.Collision, 0f);	
				return;
			}

			if (config.requireDriver)
			{
				if (driver == null || (config.requirePermission && !permission.UserHasPermission(driver.UserIDString, permissionScale))) 
					return;
			}
			
			info?.damageTypes.Scale(Rust.DamageType.Collision, config.scale);
		}
		
		
	}
}