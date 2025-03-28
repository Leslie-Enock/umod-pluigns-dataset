using UnityEngine;
using Newtonsoft.Json;
using System.Linq;
using System;

namespace Oxide.Plugins
{
    [Info("Xmas Mini", "The Friendly Chap", "1.0.4")]
    [Description("Spawns Christmas lights on the minicopter. Merry X-Mas.")]
    class XmasMini : RustPlugin
    {
        #region Defines
        const string prefabName = "assets/prefabs/misc/xmas/christmas_lights/xmas.lightstring.deployed.prefab";
        private static readonly Vector3 prefabPosition = new Vector3(0.08f, 0.21f, 0.6f);
        private static readonly Quaternion prefabRotation = Quaternion.Euler(180, 90, 180);
        private static readonly Vector3 prefabPosition2 = new Vector3(0.0f, 0.65f, -1.2f);
        private static readonly Quaternion prefabRotation2 = Quaternion.Euler(180, 90, 178);
        #endregion

        #region Hooks
        void OnEntitySpawned(BaseEntity entity)
        {
            if (entity is Minicopter)
            {
                Setup(entity as Minicopter);
            }
        }
        #endregion

        #region Functions
        public void Setup(Minicopter minicopter)
        {
            SpawnLights(minicopter, prefabPosition, prefabRotation);
            SpawnLights(minicopter, prefabPosition2, prefabRotation2);
        }

void SpawnLights(Minicopter minicopter, Vector3 position, Quaternion rotation)
{
    // Calculate the final rotation by combining the Minicopter's rotation with the prefabRotation
    Quaternion finalRotation = minicopter.transform.rotation * rotation;

    // Create the lights entity
    BaseEntity lightsEntity = GameManager.server.CreateEntity(prefabName, minicopter.transform.position, finalRotation);
    if (lightsEntity == null) return;

    // Set the Minicopter as the parent
    lightsEntity.SetParent(minicopter, true);

    // Set the local position relative to the Minicopter
    lightsEntity.transform.localPosition = position;

    // Spawn the entity
    lightsEntity.Spawn();
}
        #endregion
    }
}
