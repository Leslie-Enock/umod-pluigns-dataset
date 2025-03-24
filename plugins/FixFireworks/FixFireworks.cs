using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Fix Fireworks", "Clearshot", "1.1.0")]
    [Description("Fix lit fireworks never firing")]
    class FixFireworks : CovalencePlugin
    {
        private PluginConfig _config;
        private HashSet<BaseFirework> _fireworkList = new HashSet<BaseFirework>();

        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnEntityKill));
        }

        private void OnServerInitialized()
        {
            foreach (var firework in BaseNetworkable.serverEntities.OfType<BaseFirework>())
                OnEntitySpawned(firework);

            foreach (var firework in BaseFirework._activeFireworks)
            {
                if (firework != null)
                {
                    firework.SetFlag(BaseEntity.Flags.On, false, false, false);
                    firework.SetFlag(BaseEntity.Flags.OnFire, false, false, true);
                }
            }

            BaseFirework._activeFireworks = new HashSet<BaseFirework>();
            timer.Every(_config.checkFireworksInterval, () => {
                foreach(var firework in _fireworkList)
                    FixFirework(firework);
            });

            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnEntityKill));
        }

        private void OnEntitySpawned(BaseFirework firework) => _fireworkList.Add(firework);
        private void OnEntityKill(BaseFirework firework) => _fireworkList.Remove(firework);

        private void FixFirework(BaseFirework firework)
        {
            var isActive = BaseFirework._activeFireworks.Contains(firework);
            var isLitBroken = firework.IsLit() && !isActive;
            var isLitActiveBroken = firework.IsLit() && isActive && !firework.IsInvoking(firework.Begin) && !firework.HasFlag(BaseEntity.Flags.On);
            if (isLitBroken || isLitActiveBroken)
            {
                if (isLitActiveBroken)
                    BaseFirework._activeFireworks.Remove(firework);

                firework.SetFlag(BaseEntity.Flags.OnFire, false, false, false);
                firework.StaggeredTryLightFuse();
            }
        }

        [Command("fixfireworks")]
        private void FixFireworksCommand(IPlayer player, string command, string[] args)
        {
            if (!player.IsServer && !player.IsAdmin) return;

            if (args == null || args.Length < 1)
            {
                foreach (var firework in _fireworkList)
                    FixFirework(firework);
                return;
            }

            switch(args[0].ToLower())
            {
                case "fireall":
                    foreach (var firework in _fireworkList)
                        firework.Begin();
                    break;
                case "stats":
                    int exhausted = 0;
                    int broken = 0;
                    int activeBroken = 0;
                    foreach(var firework in _fireworkList)
                    {
                        var isActive = BaseFirework._activeFireworks.Contains(firework);
                        var isLitBroken = firework.IsLit() && !isActive;
                        var isLitActiveBroken = firework.IsLit() && isActive && !firework.IsInvoking(firework.Begin) && !firework.HasFlag(BaseEntity.Flags.On);
                        if (firework.IsExhausted()) exhausted++;
                        if (isLitBroken) broken++;
                        if (isLitActiveBroken) activeBroken++;
                    }

                    Puts($"\nFirework Stats:" +
                            $"\n\tTotal fireworks: {_fireworkList.Count}" +
                            $"\n\tActive fireworks: {BaseFirework._activeFireworks.Count}" +
                            $"\n\tExhausted fireworks: {exhausted}" +
                            $"\n\tBroken fireworks: {broken}" +
                            $"\n\tBroken active fireworks: {activeBroken}");
                    break;
            }
        }

        #region Config
        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        private class PluginConfig
        {
            public float checkFireworksInterval = 5f;
        }
        #endregion
    }
}