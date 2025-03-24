namespace Oxide.Plugins
{
    [Info("NoSupplySignal", "Wulf/lukespragg, Whispers88", 0.2, ResourceId = 2375)]
    [Description("Prevents supply drops triggering from supply signals")]

    class NoSupplySignal : CovalencePlugin
    {
        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (entity.name.Contains("signal")) entity.KillMessage();
        }
		
		void OnExplosiveDropped(BasePlayer player, BaseEntity entity, ThrownWeapon item)
		{
			if (entity.name.Contains("signal")) entity.KillMessage();
		}
    }
}