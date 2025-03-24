using System;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Spin Drop", "misticos", "1.0.7")]
    [Description("Spin around dropped items")]
    class SpinDrop : RustPlugin
    {
        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Speed Modifier")]
            public float SpeedModifier = 125f;

            [JsonProperty(PropertyName = "Move Item UP On N")]
            public float HeightOnDrop = 0.4f;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            foreach (var entity in BaseNetworkable.serverEntities.OfType<WorldItem>())
            {
                // Destroy the old spin drop controller since it will spam NREs.
                foreach (var component in entity.GetComponents<MonoBehaviour>())
                {
                    if (component.GetType().Name == "SpinDropControl")
                    {
                        UnityEngine.Object.Destroy(component);
                        break;
                    }
                }
            }
        }

        private void OnItemDropped(Item item, BaseEntity entity)
        {
            var worldEntity = item?.GetWorldEntity();
            if (worldEntity == null)
                return;

            if (Interface.CallHook("CanSpinDrop", item, entity) != null)
                return;

            SpinDropController.AddToEntity(entity, _config);
        }

        #endregion

        #region Controller

        private class SpinDropController : MonoBehaviour
        {
            public static void AddToEntity(BaseEntity entity, Configuration config)
            {
                var component = entity.gameObject.AddComponent<SpinDropController>();
                
                component.Config = config;
            }

            public Configuration Config;
            private bool _triggered = false;

            private void OnCollisionEnter(Collision collision)
            {
                if (_triggered || collision.gameObject.ToBaseEntity()?.GetItem() != null)
                    return;

                var rigidbody = gameObject.GetComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;

                var transform = this.transform;
                var position = transform.position;

                transform.rotation = Quaternion.identity;
                transform.position = new Vector3(position.x, position.y + Config.HeightOnDrop, position.z);

                _triggered = true;
            }

            private void FixedUpdate()
            {
                if (!_triggered)
                   return;

                gameObject.transform.Rotate(Time.deltaTime * Config.SpeedModifier * Vector3.down);
            }
        }

        #endregion
    }
}