
using System;

namespace Oxide.Plugins
{
    [Info("Lockable Shutters", "OG61", "1.1.1")]
    [Description("Allows players to place locks on shutters")]
    public class LockableShutters : CovalencePlugin
    {
        const string _perm = "lockableshutters.use";
        #region Hooks
        private void OnServerInitialized()
        {
            CheckDoors();
        }

        private void CheckDoors()
        {
            var doors = UnityEngine.Object.FindObjectsOfType<Door>();
            foreach (var door in doors)
            {
                if (door.ShortPrefabName == "shutter.wood.a")
                {
                    if (permission.UserHasPermission(door.OwnerID.ToString(), _perm)) door.canTakeLock = true;
                    else door.canTakeLock = false;
                }
            }
        }

        private void Init()
        {
            permission.RegisterPermission(_perm, this);
        }

        private void OnEntitySpawned(Door door)
        {
            if (!permission.UserHasPermission(door.OwnerID.ToString(), _perm)) return;
            if (door.ShortPrefabName == "shutter.wood.a") door.canTakeLock = true;
        }

        void OnUserPermissionGranted(string id, string permName)
        {
            if (permName == _perm) CheckDoors();
        }

        void OnUserPermissionRevoked(string id, string permName)
        {
           if (permName == _perm) CheckDoors();
        }

        #endregion //Hooks  
    }
}
