using System;
using System.Collections.Generic;
using Network;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Flashbang", "birthdates", "1.0.5")]
    [Description("Throw a flashbang to temporarily blind your enemies")]
    public class Flashbang : RustPlugin
    {
        #region Command

        [ConsoleCommand("flashbang")]
        private void FlashbangCommand(ConsoleSystem.Arg arg)
        {
            if (arg.Player() != null && !arg.Player().IPlayer.HasPermission(GivePermission) || arg.Args == null ||
                arg.Args.Length < 1) return;
            var amount = 1;
            var count = arg.Args.Length;
            if (arg.Args.Length > 1)
            {
                if (!int.TryParse(arg.Args[arg.Args.Length - 1], out amount)) amount = 1;
                else count--;
            }

            var player = BasePlayer.Find(string.Join(" ", arg.Args, 0, count));
            if (player == null) return;
            var item = ItemManager.CreateByName("grenade.smoke", amount, _config.SkinID);
            item.name = "Flashbang";
            player.GiveItem(item);
        }

        #endregion

        #region Variables

        /// <summary>
        ///     Layer mask containing players
        /// </summary>
        private int PlayerMask { get; } = LayerMask.GetMask("Player (Server)");

        /// <summary>
        ///     Layer mask that will obstruct a flashbang
        /// </summary>
        private int ObstructionMask { get; } = LayerMask.GetMask("Construction", "World", "Terrain");

        /// <summary>
        ///     Intense flash UI
        /// </summary>
        private CuiElementContainer FlashCui { get; } = new CuiElementContainer();

        /// <summary>
        ///     Small flash UI
        /// </summary>
        private CuiElementContainer SmallFlashCui { get; } = new CuiElementContainer();

        /// <summary>
        ///     Flashbang expiry cache
        /// </summary>
        private IDictionary<ulong, long> FlashExpiry { get; } = new Dictionary<ulong, long>();

        /// <summary>
        ///     Main CUI name (for both <see cref="Flashbang.FlashCui" /> & <see cref="Flashbang.SmallFlashCui" />)
        /// </summary>
        private const string CuiName = "Flashed";

        /// <summary>
        ///     The permission for <see cref="Flashbang.FlashbangCommand" />
        /// </summary>
        private const string GivePermission = "flashbang.give";

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(GivePermission, this);
            GenerateUI(FlashCui, "1 1 1 1");
            GenerateUI(SmallFlashCui, "1 1 1 0.6");

            // Unsub if default behaviour
            if (_config.Stack == 3) Unsubscribe(nameof(OnMaxStackable));
        }

        private void OnServerInitialized()
        {
            timer.Every(1f, CheckForExpired);
        }

        private object OnMaxStackable(Item item)
        {
            return item.skin != _config.SkinID ? (object) null : _config.Stack;
        }

        private void OnExplosiveThrown(BasePlayer player, TimedExplosive entity, ThrownWeapon item)
        {
            OnThrown(player, entity, item);
        }

        private void OnExplosiveDropped(BasePlayer player, TimedExplosive entity, ThrownWeapon item)
        {
            OnThrown(player, entity, item);
        }

        private void Unload()
        {
            // Un-flash everyone
            foreach (var entry in FlashExpiry) UnFlash(BasePlayer.FindByID(entry.Key));
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            FlashExpiry.Remove(player.userID);
        }

        #endregion

        #region Core Logic

        /// <summary>
        ///     Generate flashbang UI
        /// </summary>
        /// <param name="container">Target UI container</param>
        /// <param name="color">Flashbang color</param>
        private void GenerateUI(CuiElementContainer container, string color)
        {
            container.Add(
                new CuiPanel
                {
                    FadeOut = _config.FadeOut, Image = {Color = color},
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
                }, "Overlay", CuiName);
        }

        /// <summary>
        ///     Check for expired flashes and clear the player's UI
        /// </summary>
        private void CheckForExpired()
        {
            foreach (var entry in new Dictionary<ulong, long>(FlashExpiry))
            {
                if (entry.Value > DateTime.UtcNow.Ticks) continue;
                var player = BasePlayer.FindByID(entry.Key);
                if (player == null)
                {
                    FlashExpiry.Remove(entry.Key);
                    continue;
                }
                UnFlash(player);
            }
        }

        /// <summary>
        ///     Is this item a flashbang?
        /// </summary>
        /// <param name="item">Target item</param>
        /// <returns>True if this item is a flashbang. False, otherwise.</returns>
        [HookMethod("IsFlashbang")]
        private bool IsFlashbang(Item item)
        {
            return item != null && item.skin.Equals(_config.SkinID);
        }

        /// <summary>
        ///     Is this player flashed?
        /// </summary>
        /// <param name="player">Target player</param>
        /// <returns>True, if this player is flashed. False, otherwise.</returns>
        [HookMethod("IsFlashed")]
        private bool IsFlashed(BasePlayer player)
        {
            return IsFlashed(player.userID);
        }

        /// <summary>
        ///     Is this player flashed?
        /// </summary>
        /// <param name="id">Target player's ID</param>
        /// <returns>True, if this player is flashed. False, otherwise.</returns>
        [HookMethod("IsFlashed")]
        private bool IsFlashed(ulong id)
        {
            return FlashExpiry.ContainsKey(id);
        }

        /// <summary>
        ///     Called on explosive thrown
        /// </summary>
        /// <param name="player">Player who threw the explosive</param>
        /// <param name="entity">Explosive entity</param>
        /// <param name="weapon">The weapon</param>
        private void OnThrown(BasePlayer player, TimedExplosive entity, ThrownWeapon weapon)
        {
            var item = weapon.GetItem();
            if (!IsFlashbang(item))
                return;
            var collider = entity.GetComponent<Collider>();
            var rigidBody = collider.attachedRigidbody;
            entity.SetVelocity(rigidBody.velocity * _config.VelocityMultiplier);
            entity.CancelInvoke(entity.Explode);
            entity.Invoke(() => Flash(entity), _config.DeployTime);
        }

        /// <summary>
        ///     Flash a player from a source (this takes the ignore angle into account)
        /// </summary>
        /// <param name="player">Target player</param>
        /// <param name="flash">Source flashbang</param>
        [HookMethod("Flash")]
        private void Flash(BasePlayer player, Component flash)
        {
            if (!player.userID.IsSteamId()) return;
            var angle = AngleTo(player, flash);
            if (angle >= _config.IgnoreAngle) return;
            Flash(player, angle >= _config.SmallAngle);
        }

        /// <summary>
        ///     Flash a player
        /// </summary>
        /// <param name="player">Target player</param>
        /// <param name="small">Small flash?</param>
        private void Flash(BasePlayer player, bool small = false)
        {
            FlashExpiry[player.userID] =
                DateTime.UtcNow.AddSeconds(small ? _config.SmallBlindTime : _config.BlindTime).Ticks;
            CuiHelper.DestroyUi(player, CuiName);
            CuiHelper.AddUi(player, small ? SmallFlashCui : FlashCui);
            if (!small)
                PlayPrefab(_config.ScreamPrefab, player.transform.position);
        }

        /// <summary>
        ///     Un-flash a player
        /// </summary>
        /// <param name="player">Target player</param>
        [HookMethod("UnFlash")]
        private void UnFlash(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, CuiName);
            FlashExpiry.Remove(player.userID);
        }

        /// <summary>
        ///     Is this player obstructed from this flashbang?
        /// </summary>
        /// <param name="basePlayer">Target player</param>
        /// <param name="flash">Source flashbang</param>
        /// <returns>True if something is obstructing the view of the player's eyes from the flashbang. False, otherwise.</returns>
        private bool IsObstructed(BasePlayer basePlayer, BaseEntity flash)
        {
            var transform = flash.transform;
            var position = transform.position;
            var dir = (basePlayer.eyes.position - position).normalized;
            return Physics.Raycast(position, dir, basePlayer.Distance(flash), ObstructionMask,
                QueryTriggerInteraction.Ignore);
        }

        /// <summary>
        ///     Produce a flashbang effect from a flashbang entity (flash entities around it and destroy the entity)
        /// </summary>
        /// <param name="flash">Target entity</param>
        [HookMethod("Flash")]
        private void Flash(BaseEntity flash)
        {
            flash.Kill();
            var players = new List<BasePlayer>();
            var position = flash.transform.position;
            Vis.Entities(position, _config.Radius, players, PlayerMask, QueryTriggerInteraction.Ignore);
            PlayPrefab(_config.SmokePrefab, position, Vector3.up);
            foreach (var basePlayer in players)
            {
                // Play directionally and close because the default sound is really quiet/mute at distances
                var playerPos = basePlayer.transform.position;
                var dir = (flash.transform.position - playerPos).normalized;
                PlayPrefab(_config.DeployPrefab, playerPos + dir, source: basePlayer.Connection);

                if (!IsObstructed(basePlayer, flash))
                    Flash(basePlayer, flash);
            }
        }

        /// <summary>
        ///     Play a certain prefab at a position (and direction)
        /// </summary>
        /// <param name="prefab">Target prefab</param>
        /// <param name="position">Prefab source position</param>
        /// <param name="direction">Prefab direction</param>
        /// <param name="source">Source connection</param>
        private static void PlayPrefab(string prefab, Vector3 position, Vector3 direction = default(Vector3),
            Connection source = null)
        {
            if (string.IsNullOrEmpty(prefab)) return;
            if (source != null)
            {
                var effect = new Effect();
                effect.Init(Effect.Type.Generic, position, direction);
                effect.pooledString = prefab;
                EffectNetwork.Send(effect, source);
                return;
            }

            Effect.server.Run(prefab, position, direction);
        }

        /// <summary>
        ///     Get an angle in degrees from the player's rotation to a target component
        /// </summary>
        /// <param name="player">Target player</param>
        /// <param name="target">Target component</param>
        /// <returns>An angle in degrees (less than 180, greater than 0)</returns>
        private static float AngleTo(BasePlayer player, Component target)
        {
            var pos = new Vector3(player.eyes.position.x, 0, player.eyes.position.z);
            var targetDir = (pos - target.transform.position).normalized;
            targetDir.y = 0;
            var ray = player.eyes.GetLookRotation() * Vector3.forward;
            ray.y = 0;
            return Math.Abs(180f - Vector3.Angle(ray, targetDir));
        }

        #endregion

        #region Configuration & Localization

        private ConfigFile _config;

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null) LoadDefaultConfig();
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

        private class ConfigFile
        {
            /// <summary>
            ///     The smoke/flash skin ID (See <see href="https://www.youtube.com/watch?v=Bz8RNnM7Bgo" /> for custom icons)
            /// </summary>
            [JsonProperty("Smoke Skin ID")]
            public ulong SkinID { get; set; }

            /// <summary>
            ///     Time from throw until the flashbang will do it's effects
            /// </summary>
            [JsonProperty("Flashbang Deploy Time (seconds)")]
            public float DeployTime { get; set; }

            /// <summary>
            ///     The flashbang's deploy radius
            /// </summary>
            [JsonProperty("Flashbang Radius (metres)")]
            public float Radius { get; set; }

            /// <summary>
            ///     Flashbang velocity multiplier (speed)
            /// </summary>
            [JsonProperty("Flashbang Velocity Multiplier")]
            public float VelocityMultiplier { get; set; }

            /// <summary>
            ///     The time an intense flash will blind you for (seconds)
            /// </summary>
            [JsonProperty("Flashbang Blind Time (seconds)")]
            public float BlindTime { get; set; }

            /// <summary>
            ///     The time a small flash will blind you for (seconds)
            /// </summary>
            [JsonProperty("Flashbang Small Blind Time (seconds)")]
            public float SmallBlindTime { get; set; }

            /// <summary>
            ///     The minimum angle required for a small flash
            /// </summary>
            [JsonProperty("Flashbang Small Angle (degrees)")]
            public float SmallAngle { get; set; }

            /// <summary>
            ///     The minimum angle at which we don't flash at all
            /// </summary>
            [JsonProperty("Ignore Angle (degrees)")]
            public float IgnoreAngle { get; set; }

            /// <summary>
            ///     The fadeout time for a flash
            /// </summary>
            [JsonProperty("Flashbang Fadeout Time (seconds)")]
            public float FadeOut { get; set; }

            /// <summary>
            ///     The maximum stack for flashbangs
            /// </summary>
            [JsonProperty("Flashbang Stack")]
            public int Stack { get; set; }

            /// <summary>
            ///     The flashbang deploy prefab
            /// </summary>
            [JsonProperty("Flashbang Deploy Prefab (leave blank for disable)")]
            public string DeployPrefab { get; set; }

            /// <summary>
            ///     The flashbang scream prefab (only played on full flash)
            /// </summary>
            [JsonProperty("Scream Prefab (leave blank for disable)")]
            public string ScreamPrefab { get; set; }

            /// <summary>
            ///     The smoke deploy prefab (called on flash on the flashbang)
            /// </summary>
            [JsonProperty("Smoke Prefab (leave blank for disable)")]
            public string SmokePrefab { get; set; }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    SkinID = 2555437667UL,
                    DeployTime = 3f,
                    DeployPrefab = "assets/prefabs/tools/smoke grenade/effects/ignite.prefab",
                    SmokePrefab = "assets/prefabs/weapons/rocketlauncher/effects/rocket_launch_fx.prefab",
                    ScreamPrefab = "assets/bundled/prefabs/fx/player/gutshot_scream.prefab",
                    Radius = 35f,
                    VelocityMultiplier = 1.5f,
                    BlindTime = 4f,
                    SmallBlindTime = 1f,
                    SmallAngle = 60f,
                    IgnoreAngle = 150f,
                    FadeOut = 1f,
                    Stack = 1
                };
            }
        }

        #endregion
    }
}