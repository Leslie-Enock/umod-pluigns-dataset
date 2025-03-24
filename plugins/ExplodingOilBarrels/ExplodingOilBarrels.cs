using System.Collections.Generic;
using Rust;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Exploding Oil Barrel", "Bazz3l", "1.1.0")]
    [Description("Exploding oil barrels with explosion force, player damage and ground shake effect")]
    public class ExplodingOilBarrels : RustPlugin
    {
        #region Fields

        private const string EXPLOSION_EFFECT = "assets/bundled/prefabs/fx/explosions/explosion_03.prefab";
        private const string FIRE_EFFECT = "assets/bundled/prefabs/fx/gas_explosion_small.prefab";
        private const string SHAKE_EFFECT = "assets/content/weapons/_gestures/effects/eat_2hand_chewymeat.prefab";

        private readonly int _playerMask = LayerMask.GetMask("Player (Server)");

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
            }
            catch
            {
                PrintWarning("Loaded default config.");

                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Screen shake effect for explosion")]
            public bool EnableShakeScreen;

            [JsonProperty(PropertyName = "Moves items in range of explosion")]
            public bool EnableExplosionForce;

            [JsonProperty(PropertyName = "Deal damage to players in distance of explosion")]
            public bool EnablePlayerDamage;

            [JsonProperty(PropertyName = "ditance to deal damage to players")]
            public float PlayerDamageDistance;

            [JsonProperty(PropertyName = "amount of damage delt to players in range")]
            public float PlayerDamage;

            [JsonProperty(PropertyName = "distance shake will effect players from explosion")]
            public float ShakeDistance;

            [JsonProperty(PropertyName = "amount of force delt to object in range")]
            public float ExplosionForce;

            [JsonProperty(PropertyName = "distance to find object near explosion")]
            public float ExplosionItemDistance;

            [JsonProperty(PropertyName = "distance to find targets near explosion")]
            public float ExplosionRange;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig
                {
                    EnableShakeScreen = true,
                    EnableExplosionForce = true,
                    EnablePlayerDamage = true,
                    PlayerDamageDistance = 2f,
                    PlayerDamage = 10f,
                    ShakeDistance = 20f,
                    ExplosionItemDistance = 20f,
                    ExplosionRange = 50f,
                    ExplosionForce = 50f
                };
            }
        }

        #endregion

        #region Oxide

        private void OnEntityDeath(LootContainer entity, HitInfo info)
        {
            if (entity == null || info == null)
            {
                return;
            }
            
            if (!(entity.ShortPrefabName == "oil_barrel" && info.damageTypes.GetMajorityDamageType() == DamageType.Bullet))
            {
                return;
            }

            DoExplosion(entity.ServerPosition);
        }

        #endregion

        #region Core
        
        private void DoExplosion(Vector3 position)
        {
            PlayExplosion(position);
            PlayerInRange(position);
            
            if (!_config.EnableExplosionForce)
            {
                return;
            }
            
            MoveItems(position);
        }

        private void MoveItems(Vector3 position)
        {
            List<DroppedItem> items = Facepunch.Pool.GetList<DroppedItem>();

            Vis.Entities(position, _config.ExplosionItemDistance, items);

            foreach (DroppedItem item in items)
            {
                if (item == null || item.IsDestroyed || !item.IsVisible(position)) continue;

                item.GetComponent<Rigidbody>()
                    ?.AddExplosionForce(_config.ExplosionForce, position, _config.ExplosionItemDistance);
            }

            Facepunch.Pool.FreeList(ref items);
        }

        private void PlayerInRange(Vector3 position)
        {
            List<BasePlayer> targets = Facepunch.Pool.GetList<BasePlayer>();

            Vis.Entities(position, _config.ExplosionRange, targets, _playerMask, QueryTriggerInteraction.Ignore);

            foreach (BasePlayer player in targets)
            {
                if (_config.EnablePlayerDamage &&
                    InDistance(player.ServerPosition, position, _config.PlayerDamageDistance))
                {
                    DamagePlayer(player);
                }

                if (_config.EnableShakeScreen && 
                    InDistance(player.ServerPosition, position, _config.ShakeDistance))
                {
                    PlayerShake(player);
                }
            }

            Facepunch.Pool.FreeList(ref targets);
        }

        private void DamagePlayer(BasePlayer player)
            => player.Hurt(_config.PlayerDamage, DamageType.Explosion);

        private static bool InDistance(Vector3 target, Vector3 position, float distance)
            => Vector3Ex.Distance2D(target, position) <= distance;

        private static void PlayerShake(BasePlayer player)
            => Effect.server.Run(SHAKE_EFFECT, player.transform.position);

        private static void PlayExplosion(Vector3 position)
        {
            Effect.server.Run(EXPLOSION_EFFECT, position);
            Effect.server.Run(FIRE_EFFECT, position);
        }

        #endregion
    }
}