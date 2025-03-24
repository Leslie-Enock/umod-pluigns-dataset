using UnityEngine;
using Rust;
using System;

namespace Oxide.Plugins
{
    [Info("Buoyant Helicopter Crates", "Tacman", "1.1.2")]
    [Description("Makes helicopter crates buoyant")]
    class BuoyantHelicopterCrates : RustPlugin
    {
        #region Config
        public PluginConfig _config;

        public PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                DetectionRate = 1,
            };
        }

        public class PluginConfig
        {
            public int DetectionRate;
        }
        #endregion

        #region Oxide
        protected override void LoadDefaultConfig() => Config.WriteObject(GetDefaultConfig(), true);

        void Init()
        {
            _config = Config.ReadObject<PluginConfig>();
        }

        void OnEntitySpawned(BaseEntity entity)
        {
            if (entity == null || entity.ShortPrefabName != "heli_crate") return;

            NextTick(() =>
            {
                StorageContainer crate = entity.GetComponent<StorageContainer>();
                if (crate == null) return;

                Rigidbody rb = crate.GetComponent<Rigidbody>();
                if (rb == null) rb = crate.gameObject.AddComponent<Rigidbody>();
                rb.useGravity = true;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                rb.mass = 2f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.angularVelocity = Vector3Ex.Range(-2.75f, 2.75f);
                rb.drag = 0.5f * rb.mass;

                MakeBuoyant buoyancy = crate.GetComponent<MakeBuoyant>();
                if (buoyancy == null) buoyancy = crate.gameObject.AddComponent<MakeBuoyant>();
                buoyancy.buoyancyScale = 1;
                buoyancy.detectionRate = _config?.DetectionRate ?? 1;
            });
        }
        #endregion

        #region Classes
        class MakeBuoyant : MonoBehaviour
        {
            public float buoyancyScale;
            public int detectionRate;
            private BaseEntity _entity;

            void Awake()
            {
                _entity = GetComponent<BaseEntity>();
                if (_entity == null) Destroy(this);
            }

            void FixedUpdate()
            {
                if (_entity == null)
                {
                    Destroy(this);
                    return;
                }
                if (UnityEngine.Time.frameCount % detectionRate == 0 && WaterLevel.Factor(_entity.WorldSpaceBounds().ToBounds(), true, true) > 0.65f)
                {
                    BuoyancyComponent();
                    Destroy(this);
                }
            }

            void BuoyancyComponent()
            {
                Buoyancy buoyancy = gameObject.AddComponent<Buoyancy>();
                buoyancy.buoyancyScale = buoyancyScale;
                buoyancy.rigidBody = gameObject.GetComponent<Rigidbody>();
                buoyancy.SavePointData(true);
            }
        }
        #endregion
    }
}