using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Remote Detonated Explosives", "birthdates", "2.0.2")]
    [Description("Allows players to detonate explosives remotely with a RF Transmitter")]
    public class RemoteDetonatedExplosives : RustPlugin
    {

        #region Variables

        private const string DetonatePermission = "remotedetonatedexplosives.use"; 
        private static float Time => UnityEngine.Time.realtimeSinceStartup;
        private IDictionary<ulong, Queue<Explosive>> ActiveExplosives { get; } =
            new Dictionary<ulong, Queue<Explosive>>();

        /// <summary>
        ///     A class that stores a reference to <see cref="TimedExplosive"/> & an expiry of the initial throw delay
        /// </summary>
        private class Explosive
        {
            public TimedExplosive Entity { get; set; }
            public float Expiry { get; set; }
            public ExplosiveSettings Settings { get; set; }
        }

        #endregion
        
        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(DetonatePermission, this);
        }

        
        private void OnExplosiveThrown(BasePlayer player, TimedExplosive timedExplosive, BaseNetworkable item)
        {
            ExplosiveSettings explosiveSettings;
            if (!_config.AllExplosiveSettings.TryGetValue(item.ShortPrefabName, out explosiveSettings))
            {
                return;
            }
            UpdateCollider(explosiveSettings, timedExplosive);
            if (!player.IPlayer.HasPermission(DetonatePermission)) return;
            CancelExplode(timedExplosive);
            AddActiveExplosive(explosiveSettings, player.userID, timedExplosive);
        }

        /// <summary>
        ///     Cleanup the infinitely timed C4
        /// </summary>
        private void Unload()
        {
            foreach (var allExplosives in ActiveExplosives.Values)
            foreach (var timedExplosive in allExplosives)
            {
                timedExplosive.Entity.Kill();
            }
        }
        
        /// <summary>
        ///     <para>
        ///         First, we check if the <paramref name="frequency"/> is equal to <see cref="ExplosiveSettings.Frequency"/>, if applicable.
        ///     </para>
        ///     <para>
        ///         Second, we get all the active <see cref="Explosive"/> associated with the player
        ///     </para>
        ///     <para>
        ///         Finally, we explode & remove all the <see cref="TimedExplosive"/> that is not on cooldown
        ///     </para>
        /// </summary>
        /// <param name="detonator"><see cref="Detonator"/> used to get the player who triggered this hook</param>
        /// <param name="frequency">Used to check against <see cref="ExplosiveSettings.Frequency"/> if applicable</param>
        private void OnRfBroadcasterAdded(Detonator detonator, int frequency)
        {
            var ownerPlayer = detonator.GetOwnerPlayer();
            if (ownerPlayer == null) return;
            var owner = ownerPlayer.userID;
            var explosives = GetExplosives(owner);
            if (explosives == null) return;
            var count = 0;
            var max = explosives.Count-1;
            do
            {
                var explosive = explosives.Peek();
                if (explosive.Settings.Frequency >= 0 && explosive.Settings.Frequency != frequency || explosive.Expiry > Time || 
                    explosive.Entity.Distance2D(ownerPlayer.transform.position) > explosive.Settings.MaxDistance) continue;
                CancelExplode(explosive.Entity);
                try
                {
                    explosive.Entity.Explode();
                }
                catch (Exception exception)
                {
                    PrintError("Failed to explode C4: {0}\n{1}", exception.Message, exception.StackTrace);
                }

                explosives.Dequeue();
                if (count++ == explosive.Settings.MaxExplosions) break;

            } while (max-- > 0);

            if (explosives.Count == 0) ActiveExplosives.Remove(owner);
        }

        #endregion

        #region Helpers

        /// <summary>
        ///     Track a <see cref="TimedExplosive"/> for <see cref="OnRfBroadcasterAdded"/>
        /// </summary>
        /// <param name="settings"><paramref name="timedExplosive"/> settings</param>
        /// <param name="id">ID of player who threw this explosive</param>
        /// <param name="timedExplosive">Target <see cref="TimedExplosive"/></param>
        private void AddActiveExplosive(ExplosiveSettings settings, ulong id, TimedExplosive timedExplosive)
        {
            var explosives = GetExplosives(id, true);
            var explosive = new Explosive {Entity = timedExplosive, Expiry = Time + settings.InitialDelay, Settings = settings};
            explosives.Enqueue(explosive);
            if(settings.MaxLifespan > 0f) timedExplosive.Invoke(() => LifespanExplode(id, explosives, explosive), settings.MaxLifespan);
        }

        /// <summary>
        ///     A method to explode once a <see cref="TimedExplosive"/> lifespan is over
        /// </summary>
        /// <param name="id">Target id</param>
        /// <param name="explosives">All explosives from <paramref name="id"/></param>
        /// <param name="explosive">Target <see cref="TimedExplosive"/></param>
        private void LifespanExplode(ulong id, IEnumerable<Explosive> explosives, Explosive explosive)
        {
            if (explosive.Entity == null) return;
            explosive.Entity.Explode();
            ActiveExplosives[id] = new Queue<Explosive>(explosives.Where(ex => ex != explosive));
        }
        
        /// <summary>
        ///     Cancel the <see cref="MonoBehaviour.Invoke"/> that calls <see cref="TimedExplosive.Explode"/> from <paramref name="timedExplosive"/>
        /// </summary>
        /// <param name="timedExplosive">Target explosive</param>
        private static void CancelExplode(TimedExplosive timedExplosive)
        {
            timedExplosive.CancelInvoke(timedExplosive.Explode);
        }
        
        /// <summary>
        ///     Get all the explosives associated with <paramref name="id"/>
        /// </summary>
        /// <param name="id">Target id</param>
        /// <param name="create">Do we create an entry if it doesn't exist?</param>
        /// <returns>A <see cref="IList{T}"/> of <see cref="Explosive"/></returns>
        private Queue<Explosive> GetExplosives(ulong id, bool create = false)
        {
            Queue<Explosive> timedExplosives;
            if (ActiveExplosives.TryGetValue(id, out timedExplosives)) return timedExplosives;
            if (!create) return null;
            ActiveExplosives[id] = timedExplosives = new Queue<Explosive>();
            return timedExplosives;
        }

        /// <summary>
        ///     Update a collider's physics
        /// </summary>
        /// <param name="explosiveSettings"><paramref name="obj"/> settings</param>
        /// <param name="obj">Target <see cref="Component"/> with a <see cref="Collider"/> component</param>
        private static void UpdateCollider(ExplosiveSettings explosiveSettings, Component obj)
        {
            var useMass = explosiveSettings.PhysicsSettings.Mass > 0f;
            var useFriction = explosiveSettings.PhysicsSettings.Friction > 0f;
            if (!useFriction && !useMass) return;
            var collider = obj.GetComponent<Collider>();
            var material = collider.material;
            var rigidBody = collider.attachedRigidbody;
            if(useMass) rigidBody.mass = explosiveSettings.PhysicsSettings.Mass;
            if(useFriction) material.dynamicFriction = material.staticFriction = explosiveSettings.PhysicsSettings.Friction;
        }

        #endregion
        
        #region Configuration

        private ConfigFile _config;

        /// <summary>
        ///     C4 collider physics settings
        /// </summary>
        private class PhysicsSettings
        {
            public float Friction { get; set; }
            public float Mass { get; set; }
        }

        private class ConfigFile
        {
            [JsonProperty("Explosive Settings")]
            public IDictionary<string, ExplosiveSettings> AllExplosiveSettings { get; set; }
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    AllExplosiveSettings = new Dictionary<string, ExplosiveSettings>
                    {
                        {"explosive.timed.entity", new ExplosiveSettings{
                            InitialDelay = 3f,
                            MaxExplosions = 2,
                            MaxLifespan = 15f,
                            MaxDistance = 100f,
                            PhysicsSettings = new PhysicsSettings {Mass = 0.5f, Friction = 1f},
                            Frequency = -1
                        }}
                    }
                };
            }
        }

        private class ExplosiveSettings
        {
            [JsonProperty("Collision Settings")]
            public PhysicsSettings PhysicsSettings { get; set; }
            [JsonProperty("Max Time Before it Automatically Explodes (-1 to disable)")]
            public float MaxLifespan { get; set; }
            [JsonProperty("Max Detonation Distance")]
            public float MaxDistance { get; set; }
            [JsonProperty("Max Explosions with One Click")]
            public int MaxExplosions { get; set; }
            [JsonProperty("Initial Delay (time before you can explode in seconds)")]
            public float InitialDelay { get; set; }
            [JsonProperty("Required Frequency (-1 for all)")]
            public int Frequency;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null)
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion
    }
}