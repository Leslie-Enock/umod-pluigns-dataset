using Oxide.Core.Libraries;
using UnityEngine;

namespace Oxide.Plugins
{

    [Info("Building Block Safe Zone", "Ryz0r", "1.0.3")]
    [Description("Prevents access to items in the hotbar while in a safe zone.")]
    public class BuildingBlockSafeZone : RustPlugin
    {
        private GameObject _privController = new GameObject();
        private static BuildingBlockSafeZone _plugin;
        private const string BypassPerm = "buildingblocksafezone.bypass";

        private void OnServerInitialized()
        {
            _privController.AddComponent<PrivilegeUpdater>();
            permission.RegisterPermission(BypassPerm, this);
        }
        
        private void Init() { _plugin = this; }
        
        private void Unload()
        {
            UnityEngine.Object.Destroy(_privController);

            _privController = null;
            _plugin = null;
        }
        
        private class PrivilegeUpdater : MonoBehaviour
        {
            private void Awake()
            {
                InvokeRepeating(nameof(OnTick), 0.90f, 0.90f);
            }

            private void OnTick()
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player == null || player.IsNpc) continue;
                    if (_plugin.permission.UserHasPermission(player.UserIDString, BypassPerm)) continue;
                    
                    player.inventory.containerBelt.capacity = player.IsBuildingBlocked() ? 0 : 6;
                    player.SendNetworkUpdateImmediate();
                }
            }
        }
    }
}