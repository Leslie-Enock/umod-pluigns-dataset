using UnityEngine;
using System.Collections.Generic;
using System.Linq;
namespace Oxide.Plugins
{
    [Info("No Coil Drain", "Lincoln/Orange", "1.0.5")]
    [Description("Prevent Tesla coils from damaging themselves while in use.")]
    public class NoCoilDrain : RustPlugin
    {
        private const string PermissionNoCoilDrainUnlimited = "NoCoilDrain.unlimited";

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermissionNoCoilDrainUnlimited, this);

            var teslaCoils = new List<TeslaCoil>();
            Vis.Entities<TeslaCoil>(Vector3.zero, 10000f, teslaCoils);
            foreach (var entity in teslaCoils)
            {
                OnEntitySpawned(entity);
            }
        }

        private void OnEntitySpawned(TeslaCoil entity)
        {
            var ownerId = entity.OwnerID.ToString();
            if (permission.UserHasPermission(ownerId, PermissionNoCoilDrainUnlimited))
            {
                entity.maxDischargeSelfDamageSeconds = 0f;
            }
        }

        private void Unload()
        {
            var teslaCoils = new List<TeslaCoil>();
            Vis.Entities<TeslaCoil>(Vector3.zero, 10000f, teslaCoils);
            foreach (var entity in teslaCoils)
            {
                entity.maxDischargeSelfDamageSeconds = 120f;
            }
        }
    }
}
