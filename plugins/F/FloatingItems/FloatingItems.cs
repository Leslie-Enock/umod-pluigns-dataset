using Oxide.Core;
using System.Collections.Generic;
using System.Linq;
using System;
using Oxide.Core.Libraries;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("FloatingItems", "Diametric", "1.0.1")]
    [Description("Adds buoyancy to items dropped, causing them to float in water")]
    class FloatingItems : RustPlugin
    {
        class ItemFloater : MonoBehaviour
        {
            public BaseEntity entity;
            public float buoyancyScale;
            public int waterDetectionRate;

            private void AddBuoyancyComponent()
            {
                Buoyancy buoyancy = GetComponent<Buoyancy>();

                if (buoyancy != null)
                {
                    if (buoyancy.rigidBody == null)
                    {
                        buoyancy.rigidBody = entity.gameObject.GetComponent<Rigidbody>();
                    }
                }
                else
                {
                    buoyancy = entity.gameObject.AddComponent<Buoyancy>();
                    buoyancy.rigidBody = entity.gameObject.GetComponent<Rigidbody>();
                }

                // Sets the velocity/angularVelocity to 0 so thrown items
                // don't go pond skipping like crazy.
                buoyancy.rigidBody.velocity = Vector3.zero;
                buoyancy.rigidBody.angularVelocity = Vector3.zero;

                buoyancy.buoyancyScale = buoyancyScale;
            }

            void FixedUpdate()
            {
                if (UnityEngine.Time.frameCount % waterDetectionRate == 0)
                {
                    if (WaterLevel.Factor(entity.WorldSpaceBounds().ToBounds()) > 0.65f)
                    {
                        try
                        {
                            AddBuoyancyComponent();
                        }
                        finally
                        {
                            Destroy(this);
                        }
                    }
                }
            }
        }

        private void AddFloaterComponent(BaseEntity entity, float buoyancyScale)
        {
            // Bail here so we're not allocating too much trash in the hook.
            if (buoyancyScale < 0)
                return;

            ItemFloater floater = entity.gameObject.AddComponent<ItemFloater>();
            floater.buoyancyScale = buoyancyScale;
            floater.entity = entity;
            floater.waterDetectionRate = config.waterDetectionRate;
        }

        #region Hooks

        void OnItemDropped(Item item, BaseEntity entity)
        {
                if (item?.info == null)
                    return;

                if (entity == null)
                    return;

                object val;
                if (config.ItemBuoyancy.TryGetValue(item.info.shortname.ToLower(), out val) || config.CategoryBuoyancy.TryGetValue(item.info.category.ToString().ToLower(), out val))
                {
                    try
                    {
                        AddFloaterComponent(entity, Convert.ToSingle(val));
                    }
                    catch (FormatException)
                    {
                        PrintWarning($"Invalid configuration for {item.info.shortname}, item buoyancy values must be floats.");
                    }
                }
                else
                {
                    AddFloaterComponent(entity, config.globalBuoyancy);
                }
        }

        #endregion
        #region Configuration

        private class FloatingItemsConfig
        {
            public int waterDetectionRate = 5;
            public float globalBuoyancy = -1f;
            public Dictionary<string, object> ItemBuoyancy = new Dictionary<string, object>
            {
                {"stones",      0f},
                {"crude.oil",   0.6f},
                {"waterjug",    0.6f},
                {"bow.hunting", 0.5f}
            };

            public Dictionary<string, object> CategoryBuoyancy = new Dictionary<string, object>
            {
                {"weapon", 0.8f}
            };

            private FloatingItems plugin;

            public FloatingItemsConfig(FloatingItems plugin)
            {
                this.plugin = plugin;

                GetConfig(ref ItemBuoyancy, "Item Buoyancy");
                GetConfig(ref CategoryBuoyancy, "Category Buoyancy");
                GetConfig(ref globalBuoyancy, "Global Buoyancy");
                GetConfig(ref waterDetectionRate, "Water Detection Rate");
                plugin.SaveConfig();
            }

            private void GetConfig<T>(ref T variable, params string[] path)
            {
                if (path.Length == 0)
                    return;

                if (plugin.Config.Get(path) == null)
                {
                    SetConfig(ref variable, path);
                    plugin.PrintWarning($"Added field to config: {string.Join("/", path)}");
                }

                variable = (T)Convert.ChangeType(plugin.Config.Get(path), typeof(T));
            }

            private void SetConfig<T>(ref T variable, params string[] path) => plugin.Config.Set(path.Concat(new object[] { variable }).ToArray());
        }

        protected override void LoadDefaultConfig() => PrintWarning("Generating new configuration file.");

        private FloatingItemsConfig config;

        private void Init()
        {
            config = new FloatingItemsConfig(this);
        }

        #endregion
    }
}
