using System;
using System.Collections.Generic;
using System.Linq;
using Network;
using Network.Visibility;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Player Hider", "birthdates", "1.3.0")]
    [Description("Don't network players that are out of view")]
    public class PlayerHider : RustPlugin
    {
        #region Behaviour

        /// <summary>
        ///     Behaviour that handles the viewing of players
        /// </summary>
        private class ViewBehaviour : FacepunchBehaviour
        {
            /// <summary>
            ///     A set of our hidden players
            /// </summary>
            private readonly HashSet<BasePlayer> _hiddenPlayers = new HashSet<BasePlayer>();

            private readonly List<Connection> _list = new List<Connection>();

            private bool _lastChangeable;
            private Vector3 _lastPos;

            /// <summary>
            ///     Our parent player
            /// </summary>
            private BasePlayer _player;

            public Group Group { get; set; }

            private void Awake()
            {
                _player = GetComponent<BasePlayer>();
                _list.Add(_player.Connection);
                Group = new Group(Net.sv.visibility, Net.sv.TakeUID());
                foreach (var networkable in BaseNetworkable.GlobalNetworkGroup.networkables)
                    Group.networkables.Add(networkable);
                _player.net.subscriber.UnsubscribeAll();
                _player.net.subscriber.Subscribe(Group);
                _player.net.SwitchGroup(Group);
                _player.SendNetworkGroupChange();
                _player.SendNetworkUpdateImmediate();
                Instance._idToBehaviour[_player.userID] = this;
            }

            public void OnDestroy()
            {
                foreach (var player in _hiddenPlayers) ShowPlayer(player);
                Instance._idToBehaviour.Remove(_player.userID);
                _player.UpdateNetworkGroup();
                Net.sv.ReturnUID(Group.ID);
            }

            /// <summary>
            ///     Called when our parent player ticks
            /// </summary>
            public void Tick()
            {
                foreach (var player in BasePlayer.sleepingPlayerList)
                {
                    // If this is us, continue
                    if (player.userID == _player.userID ||
                        _lastChangeable && _player.transform.position.Equals(_lastPos)) continue;
                    // Retrieve the distance between us and the target player
                    var distance = _player.Distance(player.transform.position);
                    // If the distance is out of the render distance (max distance) or is not above the minimum distance, continue
                    if (distance >= MaxDistance || distance <= Instance._config.MinimumDistance) continue;

                    // Calculate the direction and execute a raycast with an obstruction mask at the distance between the players to see if anything is blocking the view
                    var direction = (_player.transform.position - player.transform.position).normalized;
                    RaycastHit hit;
                    if (!Physics.Raycast(_player.eyes.position, direction, out hit, distance,
                        Instance.ObstructionMask,
                        QueryTriggerInteraction.Ignore) || IsFiring(player))
                    {
                        // If not obstructed, continue or show the player if hidden
                        if (!_hiddenPlayers.Remove(player)) continue;
                        ShowPlayer(player);
                        continue;
                    }

                    // Set if the object we hit can be modified
                    _lastChangeable = hit.transform.gameObject.IsOnLayer(Instance.ModifiableObstructionMask);
                    _lastPos = _player.transform.position;

                    // If obstructed and hidden, continue. Otherwise, hide the player.
                    if (!_hiddenPlayers.Add(player)) continue;
                    player.OnNetworkSubscribersLeave(_list);
                }
            }

            /// <summary>
            ///     Show this player
            /// </summary>
            /// <param name="player">Target player</param>
            public void ShowPlayer(BasePlayer player)
            {
                if (player.IsDestroyed || !player.IsConnected) return;
                Group.networkables.Add(player.net);
                player.SendAsSnapshot(_player.Connection);
                player.GetHeldEntity()?.SendAsSnapshot(_player.Connection);
                _player.SendNetworkGroupChange();
                _player.SendNetworkUpdateImmediate();
            }

            /// <summary>
            ///     Hide this player
            /// </summary>
            /// <param name="player">Target player</param>
            public void HidePlayer(BasePlayer player)
            {
                Group.Leave(player.net);
                player.OnNetworkSubscribersLeave(_list);
            }

            /// <summary>
            ///     Can our parent player see this player?
            /// </summary>
            /// <param name="player">Target player</param>
            /// <returns>True if our parent player can see this player. False, otherwise.</returns>
            public bool CanSee(BasePlayer player)
            {
                return !_hiddenPlayers.Contains(player);
            }

            /// <summary>
            ///     Kill this behaviour and show all players
            /// </summary>
            public void Kill()
            {
                DestroyImmediate(this);
            }
        }

        #endregion

        #region Variables

        /// <summary>
        ///     Max render distance
        /// </summary>
        private const float MaxDistance = 500.0f;

        /// <summary>
        ///     Layer mask that will obstruct a player
        /// </summary>
        private int ObstructionMask { get; } = LayerMask.GetMask("Construction", "World", "Terrain");

        /// <summary>
        ///     The layer mask from <see cref="ObstructionMask" /> that are modifiable
        /// </summary>
        private int ModifiableObstructionMask { get; } = LayerMask.GetMask("Construction");

        /// <summary>
        ///     Plugin instance
        /// </summary>
        private static PlayerHider Instance { get; set; }

        private readonly IDictionary<ulong, ViewBehaviour> _idToBehaviour = new Dictionary<ulong, ViewBehaviour>();

        #endregion

        #region Hooks

        [HookMethod("CanSee")]
        private bool CanSee(BasePlayer player, BasePlayer target)
        {
            if (_config.ShowOnFire && IsFiring(target)) return true;
            var component = target.GetComponent<ViewBehaviour>();
            return component == null || component.CanSee(player);
        }

        private void OnNetworkGroupLeft(BasePlayer player, Group group)
        {
            // If is not limit networking (limbo) group, return
            if (group.ID != 1U) return;
            ViewBehaviour viewBehaviour;
            if (!_idToBehaviour.TryGetValue(player.userID, out viewBehaviour)) return;
            player.net.SwitchGroup(viewBehaviour.Group);
        }

        private static bool IsFiring(BasePlayer player)
        {
            var projectile = player.GetHeldEntity() as BaseProjectile;
            return projectile != null && projectile.NextAttackTime - Time.time >= -1;
        }

        private void Init()
        {
            Instance = this;
            foreach (var player in BasePlayer.activePlayerList) OnPlayerRespawned(player);
        }

        private void OnServerInitialized()
        {
            timer.Every(_config.CheckInterval, CheckPlayers);

            // Map to new list
            MapGroupSubscribers(new UpdateListHashSet<Networkable>(OnNetworkableChanged));
        }

        private void OnNetworkableChanged(Networkable networkable, bool removed)
        {
            var foundPlayer =
                BasePlayer.activePlayerList.FirstOrDefault(basePlayer => basePlayer.net.ID == networkable.ID);
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                ViewBehaviour viewBehaviour;
                if (!_idToBehaviour.TryGetValue(basePlayer.userID, out viewBehaviour) ||
                    foundPlayer != null && !viewBehaviour.CanSee(foundPlayer)) continue;
                var group = viewBehaviour.Group;
                if (removed) group.networkables.Remove(networkable);
                else group.networkables.Add(networkable);
                basePlayer.SendNetworkUpdateImmediate();
            }
        }

        private static void CheckPlayers()
        {
            foreach (var player in BasePlayer.activePlayerList) player.GetComponent<ViewBehaviour>()?.Tick();
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList) player.GetComponent<ViewBehaviour>()?.Kill();
            Instance = null;
            MapGroupSubscribers(new ListHashSet<Networkable>());
        }

        private static void MapGroupSubscribers(ListHashSet<Networkable> newValue)
        {
            var old = BaseNetworkable.GlobalNetworkGroup.networkables;
            BaseNetworkable.GlobalNetworkGroup.networkables = newValue;
            foreach (var networkable in old) newValue.Add(networkable);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            player.gameObject.AddComponent<ViewBehaviour>();
        }

        private class UpdateListHashSet<T> : ListHashSet<T>
        {
            private readonly Action<T, bool> _callback;

            public UpdateListHashSet(Action<T, bool> callback)
            {
                _callback = callback;
            }

            public new void Add(T value)
            {
                base.Add(value);
                _callback.Invoke(value, false);
            }

            public new bool Remove(T value)
            {
                var ret = base.Remove(value);
                if (ret) _callback.Invoke(value, true);
                return ret;
            }
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
            ///     The minimum distance a player needs to be before being hidden
            /// </summary>
            [JsonProperty("Minimum Distance")]
            public float MinimumDistance { get; set; }

            /// <summary>
            ///     Show a player if they're firing?
            /// </summary>
            [JsonProperty("Show a Player if Firing")]
            public bool ShowOnFire { get; set; }

            /// <summary>
            ///     Check interval for hiding & showing
            /// </summary>
            [JsonProperty("Check Interval (seconds)")]
            public float CheckInterval { get; set; }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    MinimumDistance = 100f,
                    ShowOnFire = true,
                    CheckInterval = 0.5f
                };
            }
        }

        #endregion
    }
}