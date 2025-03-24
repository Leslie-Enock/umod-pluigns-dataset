using Oxide.Core;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Prefab Limit", "HoverCatz", "1.0.2")]
    [Description("Limit spawning of prefabs using percentages")]
    class PrefabLimit : RustPlugin
    {

        readonly string PluginName = "PrefabLimit";
        Dictionary<string, int> spawnChances = new Dictionary<string, int>();

        void OnServerInitialized()
        {
            ReLoadConfig();
            Puts($"Successfully initialized. Loaded {spawnChances.Count} config values.");
        }

        void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject(PluginName, spawnChances);
        }

        void ReLoadConfig()
        {
            spawnChances = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, int>>(PluginName) ?? spawnChances;

            if (spawnChances == null)
                spawnChances = new Dictionary<string, int>();

            if (spawnChances.Count <= 0)
                AddDefaultConfig();

            Interface.Oxide.DataFileSystem.WriteObject(PluginName, spawnChances);
        }

        private void AddDefaultConfig()
        {
            spawnChances.Add("assets/bundled/prefabs/autospawn/resource/ores/metal-ore.prefab", 100);       // 100% chance to spawn
            spawnChances.Add("assets/bundled/prefabs/autospawn/resource/ores/stone-ore.prefab", 100);       // 100% chance to spawn
            spawnChances.Add("assets/bundled/prefabs/autospawn/resource/ores/sulfur-ore.prefab", 100);      // 100% chance to spawn
            spawnChances.Add("assets/bundled/prefabs/autospawn/resource/ores_sand/metal-ore.prefab", 100);  // 100% chance to spawn
            spawnChances.Add("assets/bundled/prefabs/autospawn/resource/ores_sand/stone-ore.prefab", 100);  // 100% chance to spawn
            spawnChances.Add("assets/bundled/prefabs/autospawn/resource/ores_sand/sulfur-ore.prefab", 100); // 100% chance to spawn
            spawnChances.Add("assets/bundled/prefabs/autospawn/resource/ores_snow/metal-ore.prefab", 100);  // 100% chance to spawn
            spawnChances.Add("assets/bundled/prefabs/autospawn/resource/ores_snow/stone-ore.prefab", 100);  // 100% chance to spawn
            spawnChances.Add("assets/bundled/prefabs/autospawn/resource/ores_snow/sulfur-ore.prefab", 100); // 100% chance to spawn

            // Add more prefabs here if you want
            // https://www.corrosionhour.com/rust-prefab-list/
        }

        [ConsoleCommand("prefablimitreload")] /* /prefabLimitReload */
        void ReloadConfigCommand(ConsoleSystem.Arg arg)
        {
            ReLoadConfig();
            arg.ReplyWith($"Successfully reloaded {spawnChances.Count} config values.");
        }

        void OnEntitySpawned(BaseNetworkable entity)
        {

            int intChance;
            if (!spawnChances.TryGetValue(entity.name, out intChance))
                return; // Doesn't exist in our list? 100% chance to spawn

            if (intChance == 100)
                return; // 100% chance, spawn.
            else
            if (intChance == 0)
            {
                entity.AdminKill(); // Destroy the Prefab! No chance check.
                return;
            }

            float fChance = intChance / 100f;
            float rnd = UnityEngine.Random.value;

            if (rnd > fChance)
                entity.AdminKill(); // Destroy the Prefab!

        }

    }
}