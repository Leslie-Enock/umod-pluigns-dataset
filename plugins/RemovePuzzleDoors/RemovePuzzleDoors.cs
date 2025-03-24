using UnityEngine;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Remove Puzzle Doors", "ziptie", 1.1)]
    [Description("Removes puzzle doors.")]
    public class RemovePuzzleDoors : CovalencePlugin
    {
        #region Config
        public RemovePuzzleDoorsConfig config;
        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private RemovePuzzleDoorsConfig GetDefaultConfig()
        {
            return new RemovePuzzleDoorsConfig();
        }
        #endregion

        #region Logic
        private void OnServerInitialized(bool initial)
        {
            Puts("Killing puzzle doors, this may take a few seconds.");
            config = Config.ReadObject<RemovePuzzleDoorsConfig>();
            if(config == null)
            {
                LogWarning("Config is null, aborting...");
                return;
            }
            foreach (var door in BaseNetworkable.serverEntities.OfType<Door>())
            {
                if((door.name == config.PrefabSettings.GreenDoorPrefab && config.RemovalSettings.RemoveGreenDoors) || (door.name == config.PrefabSettings.BlueDoorPrefab && config.RemovalSettings.RemoveBlueDoors) || (door.name == config.PrefabSettings.RedDoorPrefab && config.RemovalSettings.RemoveRedDoors))
                {
                    door.AdminKill();
                    Puts($"Killed door '{door.name}' at {door.transform.position}.");
                }
            }
        }
        #endregion
    }
    #region Config Classes
    public class RemovePuzzleDoorsConfig
    {
        public RemovalSettings RemovalSettings = new RemovalSettings();
        public PrefabSettings PrefabSettings = new PrefabSettings();
    }
    [System.Serializable]
    public class RemovalSettings
    {
        public bool RemoveRedDoors = true;
        public bool RemoveBlueDoors = true;
        public bool RemoveGreenDoors = true;
    }
    [System.Serializable]
    public class PrefabSettings
    {
        public string RedDoorPrefab = "assets/bundled/prefabs/static/door.hinged.security.red.prefab";
        public string BlueDoorPrefab = "assets/bundled/prefabs/static/door.hinged.security.blue.prefab";
        public string GreenDoorPrefab = "assets/bundled/prefabs/static/door.hinged.security.green.prefab";
    }
    #endregion
}