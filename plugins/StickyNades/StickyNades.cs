using System.Collections.Generic;
using System.Linq;
using Facepunch.Extend;
using Newtonsoft.Json;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Sticky Nades", "Bazz3l", "1.0.7")]
    [Description("Stick grenades to players, and watch them go boom.")]
    public class StickyNades : RustPlugin
    {
        #region Fields

        private const string PERM_USE = "stickynades.use";

        private PluginConfig _config;

        #endregion

        #region Config

        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new JsonException();
                }

                if (_config.ToDictionary().Keys
                    .SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys)) return;

                PrintWarning("Loaded updated config.");

                SaveConfig();
            }
            catch
            {
                PrintWarning("Default config loaded.");

                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private class PluginConfig
        {
            public List<string> AllowedItems;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    AllowedItems = new List<string>
                    {
                        "grenade.beancan.deployed",
                        "grenade.f1.deployed",
                    }
                };
            }
        }

        #endregion

        #region Oxide

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PERM_USE, this);
        }

        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon item)
        {
            if (!permission.UserHasPermission(player.UserIDString, PERM_USE)) return;

            if (!_config.AllowedItems.Contains(entity.ShortPrefabName)) return;

            entity.gameObject.AddComponent<StickyExplosiveComponent>().player = player;
        }

        private object OnExplosiveDud(DudTimedExplosive explosive)
        {
            if (explosive == null || !explosive.IsStuck()) return null;

            explosive.GetComponent<StickyExplosiveComponent>()
                ?.UnStickExplosive();

            return null;
        }

        #endregion

        #region Components

        private class StickyExplosiveComponent : MonoBehaviour
        {
            public BaseEntity player;
            private SphereCollider _collider;
            private TimedExplosive _explosive;

            private void Awake()
            {
                gameObject.layer = (int) Layer.Reserved1;

                _explosive = GetComponent<TimedExplosive>();

                _collider = gameObject.AddComponent<SphereCollider>();
                _collider.isTrigger = true;
                _collider.radius = 0.01f;
            }

            private void OnCollisionEnter(Collision collision)
            {
                BasePlayer target = collision.gameObject.ToBaseEntity() as BasePlayer;

                if (target == null || target == player) return;

                StickExplosive(target, collision.GetContact(0));
            }

            public void StickExplosive(BaseEntity entity, ContactPoint contact)
            {
                if (_explosive == null) return;

                _explosive.DoStick(contact.point, contact.normal, entity, contact.otherCollider);

                Destroy(_collider);
            }

            public void UnStickExplosive()
            {
                if (_explosive == null || !_explosive.GetParentEntity()) return;

                _explosive.SetParent(null, true, true);
                _explosive.SetMotionEnabled(true);
                _explosive.SetCollisionEnabled(true);
                _explosive.gameObject.transform.GetOrAddComponent<EntityCollisionMessage>();

                Destroy(this);
            }
        }

        #endregion
    }
}