//Requires: Coroutines

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Better Health", "birthdates", "2.2.3")]
    [Description("Ability to customize the max health")]
    public class BetterHealth : RustPlugin
    {
        #region Variables

        private const string PermissionUse = "betterhealth.use";

        #endregion

        #region Helpers

        /// <summary>
        ///     Get the player's max health based on config permissions
        /// </summary>
        /// <param name="player">Target player</param>
        /// <returns>The default max health or the highest max health they have permission to</returns>
        private float GetMaxHealth(BasePlayer player)
        {
            //filter permissions & sort
            var healthPermission = _config.Permissions.Where(p => player.IPlayer.HasPermission($"betterhealth.{p.Key}"))
                .OrderBy(entry => entry.Value).FirstOrDefault();
            return string.IsNullOrEmpty(healthPermission.Key) ? _config.MaxHealth : healthPermission.Value;
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
            LoadConfig();

            foreach (var perm in _config.Permissions.Keys.Select(p => $"betterhealth.{p}")
                .Where(perm => !permission.PermissionExists(perm, this))) permission.RegisterPermission(perm, this);
        }

        private void OnServerInitialized()
        {
            CheckAllPlayers();
            StartChecking();
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            SetHealth(player);
        }

        /// <summary>
        ///     On consume of tea, if add booster is on, add it on to the current booster, if not, cancel
        /// </summary>
        /// <param name="item">Target item</param>
        /// <param name="action">Item action</param>
        /// <param name="player">Target player</param>
        /// <returns>Null if we should not cancel, true if we should cancel</returns>
        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (!action.Equals("consume") || !item.info.shortname.StartsWith("maxhealthtea")) return null;
            if (!_config.AddOldBooster) return true; //stop the tea from being consumed

            /*
             * Use next tick to wait for tea benefits
             * Set the health with the new booster
             */
            NextTick(() => SetHealth(player));
            return null;
        }

        #endregion

        #region Health

        /// <summary>
        ///     Check all players for health changes in a coroutine
        /// </summary>
        private void CheckAllPlayers()
        {
            const string id = "Check All Players";
            if (Coroutines.Instance.IsCoroutineRunning(id)) return;
            Coroutines.Instance.LoopListAsynchronously(this, new Action<BasePlayer>(SetHealth),
                new List<BasePlayer>(BasePlayer.allPlayerList), 0.2f, id: id, reverse: true, completePerTick: 5);
        }

        /// <summary>
        ///     Start a timer every 40 minutes to reset the boost timer
        /// </summary>
        private void StartChecking()
        {
            timer.Every(2400f /*40 minutes*/, CheckAllPlayers);
        }

        /// <summary>
        ///     Set the max health of a player
        /// </summary>
        /// <param name="player">Target player</param>
        private void SetHealth(BasePlayer player)
        {
            if (player == null || player.modifiers == null || !player.IPlayer.HasPermission(PermissionUse)) return;
            var startHealth = player.StartMaxHealth();
            var maxHealth = GetMaxHealth(player);
            var healthMultiplier = (maxHealth - startHealth) /
                                   startHealth; //get multiplier needed (i.e 150 max health should be 0.5)
            
            //add old booster on to multiplier if exists & not the same booster
            float healthBooster;
            if (_config.AddOldBooster &&
                (healthBooster = player.modifiers.GetValue(Modifier.ModifierType.Max_Health, -1000f)) > -1000f &&
                Math.Abs(healthBooster - healthMultiplier) > 0.1f) healthMultiplier += healthBooster;

            player.modifiers.Add(new List<ModifierDefintion>
            {
                new ModifierDefintion
                {
                    type = Modifier.ModifierType.Max_Health,
                    value = healthMultiplier, //the equation is startHealth * (1f + modifier)
                    duration = 999999f, //don't use float.MaxValue (will kick player) (max time seems to be ~45 minutes)
                    source = Modifier.ModifierSource.Tea
                }
            });
            player.modifiers.SendChangesToClient(); //update client
        }

        #endregion

        #region Configuration, Language & Data

        private ConfigFile _config;

        public class ConfigFile
        {
            [JsonProperty("Add booster if already has one?")]
            public bool AddOldBooster = true;

            [JsonProperty("Default Max Health")] public float MaxHealth = 150f;

            [JsonProperty("Max Health Permissions")]
            public Dictionary<string, float> Permissions = new Dictionary<string, float>
            {
                {"vip", 300f}
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null) LoadDefaultConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = new ConfigFile();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion
    }
}
//Generated with birthdates' Plugin Maker