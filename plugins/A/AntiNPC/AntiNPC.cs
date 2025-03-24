using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Anti NPC", "birthdates", "1.0.4")]
    [Description("Disables the spawning of NPCs")]
    public class AntiNPC : RustPlugin
    {

        #region Hooks
        private void Init() => LoadConfig();

        private void OnServerInitialized() => Cleanup();

        private void Cleanup()
        {
            foreach (var Entity in BaseNetworkable.serverEntities.ToList().Where(Entity => (Entity as BaseNpc != null || Entity as NPCPlayer != null) && !_config.Whitelist.Contains(Entity.PrefabName)))
            {
                Entity.Kill();
            }
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!(entity is BaseNpc) && !(entity is NPCPlayer) || _config.Whitelist.Contains(entity.PrefabName))
            {
                return;
            }
            //Next Tick for parenting, e.t.c
            NextTick(() =>
            {
                entity.Kill();
            });
        }
        #endregion

        #region Configuration & Language

        private ConfigFile _config;

        public class ConfigFile
        {
            [JsonProperty("Whitelisted NPCS (Prefab)")]
            public List<string> Whitelist;
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    Whitelist = new List<string>
                    {
                        "assets/prefabs/npc/scientist/scientistpeacekeeper.prefab"
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null)
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
        #endregion
    }
}
//Generated with birthdates' Plugin Maker
