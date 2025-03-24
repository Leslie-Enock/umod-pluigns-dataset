using Newtonsoft.Json;
using Oxide.Core.Plugins;
using System;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Other Building Health", "Judess69er", "1.6.3")]
    [Description("Multiply the health of other building materials")]
    class OtherBuildingHealth : RustPlugin
    {
        [PluginReference] Plugin RaidableBases;

        private const uint HIGH_EXTERNAL_ICE_WALL = 921229511;
        private const uint DISCO_FLOOR = 286648290;
        private const uint DISCO_FLOOR_LARGE = 1735402444;

        #region Config
        private ConfigData config;

        private class ConfigData
        {
            [JsonProperty("Health Multiplier")]
            public float Multiplier { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData
            {
                Multiplier = 2.0f
            };
            SaveConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<ConfigData>();
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }
        #endregion Config
		#region Load
        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));            
        }

        private void OnServerInitialized(bool isStartup)
        {
            foreach (Door door in BaseNetworkable.serverEntities.OfType<Door>().Where(door => isViable(door)))
            {
                door._maxHealth *= config.Multiplier;
                door.health *= config.Multiplier;
                door.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                if(config.Multiplier == 0) return;
            }
			
            foreach (SimpleBuildingBlock block in BaseNetworkable.serverEntities.OfType<SimpleBuildingBlock>().Where(block => isViable(block)))
            {
            block._maxHealth *= config.Multiplier;
            block.health *= config.Multiplier;
            block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            if(config.Multiplier == 0) return;
            }

            foreach (Barricade barricade in BaseNetworkable.serverEntities.OfType<Barricade>().Where(barricade => isViable(barricade)))
            {
            barricade._maxHealth *= config.Multiplier;
            barricade.health *= config.Multiplier;
            barricade.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            if(config.Multiplier == 0) return;
            }
			
            foreach (StorageContainer container in BaseNetworkable.serverEntities.OfType<StorageContainer>().Where(container => isViable(container)))
            {
            container._maxHealth *= config.Multiplier;
            container.health *= config.Multiplier;
            container.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            if(config.Multiplier == 0) return;
            }
			
            foreach (IceFence fence in BaseNetworkable.serverEntities.OfType<IceFence>().Where(fence => isViable(fence)))
            {
            if (fence.prefabID != HIGH_EXTERNAL_ICE_WALL) return;
            fence._maxHealth *= config.Multiplier;
            fence.health *= config.Multiplier;
            fence.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            if(config.Multiplier == 0) return;
		    }
            Subscribe(nameof(OnEntitySpawned));
        }
		#endregion Load
		#region Unload
        private void Unload()
        {
            foreach (Door door in BaseNetworkable.serverEntities.OfType<Door>().Where(door => isViable(door)))
            {
                door._maxHealth /= config.Multiplier;
                door.health /= config.Multiplier;
                door.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                if(config.Multiplier == 0) return;
            }

            foreach (SimpleBuildingBlock block in BaseNetworkable.serverEntities.OfType<SimpleBuildingBlock>().Where(block => isViable(block)))
            {
            block._maxHealth /= config.Multiplier;
            block.health /= config.Multiplier;
            block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            if(config.Multiplier == 0) return;
            }

            foreach (Barricade barricade in BaseNetworkable.serverEntities.OfType<Barricade>().Where(barricade => isViable(barricade)))
            {
            barricade._maxHealth /= config.Multiplier;
            barricade.health /= config.Multiplier;
            barricade.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            if(config.Multiplier == 0) return;
            }
			
            foreach (StorageContainer container in BaseNetworkable.serverEntities.OfType<StorageContainer>().Where(container => isViable(container)))
            {
            container._maxHealth /= config.Multiplier;
            container.health /= config.Multiplier;
            container.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            if(config.Multiplier == 0) return;
            }
			
            foreach (IceFence fence in BaseNetworkable.serverEntities.OfType<IceFence>().Where(fence => isViable(fence)))
            {
            if (fence.prefabID != HIGH_EXTERNAL_ICE_WALL) return;
            fence._maxHealth /= config.Multiplier;
            fence.health /= config.Multiplier;
            fence.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            if(config.Multiplier == 0) return;
		    }
        }
		#endregion Unload
		#region Spawn
        private void OnEntitySpawned(Door door)
        {
            if (door != null && isViable(door))
            {
                door._maxHealth *= config.Multiplier;
                door.health *= config.Multiplier;
                door.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                if(config.Multiplier == 0) return;
            }
        }

        private void OnEntitySpawned(SimpleBuildingBlock block)
        {
            block._maxHealth *= config.Multiplier;
            block.health *= config.Multiplier;
            block.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            if(config.Multiplier == 0) return;
        }

        private void OnEntitySpawned(Barricade barricade)
        {
            barricade._maxHealth *= config.Multiplier;
            barricade.health *= config.Multiplier;
            barricade.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            if(config.Multiplier == 0) return;
        }
		
        private void OnEntitySpawned(StorageContainer container)
        {
            container._maxHealth *= config.Multiplier;
            container.health *= config.Multiplier;
            container.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            if(config.Multiplier == 0) return;
        }

        private void OnEntitySpawned(IceFence fence)
        {
            if (fence.prefabID != HIGH_EXTERNAL_ICE_WALL) return;
            fence._maxHealth *= config.Multiplier;
            fence.health *= config.Multiplier;
            fence.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
            if(config.Multiplier == 0) return;
		}

        private bool isViable(BaseEntity entity)
        {
            if (entity.OwnerID != 0) return true;
            return RaidableBases != null && Convert.ToBoolean(RaidableBases?.Call("EventTerritory", entity.transform.position));
        }
		#endregion Spawn
    }
}