using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Bullet Impact", "birthdates", "1.0.3")]
    [Description("Give a more realistic impact of a bullet on death")]
    public class BulletImpact : RustPlugin
    {

        private readonly IDictionary<ulong, HitData> _directions = new Dictionary<ulong, HitData>(); 
     
        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (info == null || !info.IsProjectile()) return;
            var direction = (info.PointEnd - info.PointStart).normalized;
            _directions[player.userID] = new HitData {Direction = direction, Point = info.HitPositionWorld};
        }

        private struct HitData
        {
            public Vector3 Direction { get; set; }
            public Vector3 Point { get; set; }
        }
        
        private void OnPlayerCorpseSpawned(BasePlayer player, BaseCorpse corpse)
        {
            HitData hitData;
            if (!_directions.TryGetValue(player.userID, out hitData)) return;
            var rigidBody = corpse.GetComponent<Rigidbody>();
            if (rigidBody == null) return;
            rigidBody.velocity = default(Vector3);
            rigidBody.angularVelocity = default(Vector3);
            rigidBody.AddForceAtPosition(hitData.Direction*2.5f, hitData.Point, ForceMode.VelocityChange);
        }
        
    }
}