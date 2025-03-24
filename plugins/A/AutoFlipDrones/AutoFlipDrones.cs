using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Auto Flip Drones", "WhiteThunder", "1.1.0")]
    [Description("Automatically flips upside-down RC drones when hit with a hammer or taken control of at a computer station.")]
    internal class AutoFlipDrones : CovalencePlugin
    {
        [PluginReference]
        private Plugin DroneScaleManager;

        private const string PermissionUse = "autoflipdrones.use";

        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
        }

        private void OnBookmarkControlStarted(ComputerStation computerStation, BasePlayer player, string bookmarkName, Drone drone)
        {
            MaybeFlipDrone(player, drone);
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            var drone = info.HitEntity as Drone;
            if (drone == null)
                return;

            MaybeFlipDrone(player, drone);
        }

        private bool AutoFlipWasBlocked(Drone drone, BasePlayer player)
        {
            object hookResult = Interface.CallHook("OnDroneAutoFlip", drone, player);
            return hookResult is bool && (bool)hookResult == false;
        }

        private BaseEntity GetRootEntity(Drone drone)
        {
            return drone.HasParent()
                ? DroneScaleManager?.Call("API_GetRootEntity", drone) as BaseEntity ?? drone
                : drone;
        }

        private void MaybeFlipDrone(BasePlayer player, Drone drone)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
                return;

            var rootEntity = GetRootEntity(drone);
            var rootTransform = rootEntity.transform;

            if (Vector3.Dot(Vector3.up, rootTransform.up) > 0.1f)
                return;

            if (AutoFlipWasBlocked(drone, player))
                return;

            if (drone != rootEntity)
            {
                // Special handling for resized drones.
                rootTransform.position -= rootTransform.InverseTransformPoint(drone.transform.position) * 2;
            }
            rootTransform.rotation = Quaternion.identity;
        }
    }
}
