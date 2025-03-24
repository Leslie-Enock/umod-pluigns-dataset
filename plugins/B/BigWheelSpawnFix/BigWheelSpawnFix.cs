using UnityEngine;

namespace Oxide.Plugins
{
    // Creation date: 11-04-2021
    // Last update date: 27-08-2022
    [Info("Big Wheel Spawn Fix", "Orange and Smallo", "1.1.0")]
    [Description("Fixes big wheels spawned faced down")]
    public class BigWheelSpawnFix : RustPlugin
    {
        private void Init()
        {
            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            Subscribe(nameof(OnEntitySpawned));
        }

        private void OnEntitySpawned(BigWheelGame entity)
        {
            var transform = entity.transform;
            var old = transform.eulerAngles;
			var space = transform;
            old.x = 90;
            transform.eulerAngles = old;
			transform.Rotate(0.0f, -90.0f, 0.0f, Space.Self);
        }
    }
}