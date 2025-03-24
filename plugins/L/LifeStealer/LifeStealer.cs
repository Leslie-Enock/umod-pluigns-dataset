// #define DEBUG

using System;
using System.Linq;
using System.Collections.Generic;
using Rust;
using Oxide.Core;
using Newtonsoft.Json;

// Thanks to redBDGR the original creator of this plugin
// Thanks to Krungh Crow previous maintainer

namespace Oxide.Plugins
{
    [Info("LifeStealer", "beee", "1.2.0")]
    [Description("Applies lifesteal based on player damage")]

    class LifeStealer : RustPlugin
    {
        private static PluginConfig _config;

        private Dictionary<ulong, string> PlayersPermissionsCache = new Dictionary<ulong, string>();

        void OnServerInitialized()
        {
            RegisterPermissions();
        }

        void Unload()
        {
            _config = null;
        }

        #region Hooks

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            var attacker = info.InitiatorPlayer as BasePlayer;
            if (attacker == null || !attacker.IsConnected || !attacker.userID.IsSteamId())
                return;

            string cachedPerm = "";

            KeyValuePair<string, PermissionSettings> attackerLFSettings;
            if(!PlayersPermissionsCache.TryGetValue(attacker.userID, out cachedPerm))
            {
                attackerLFSettings = GetPlayerPermission(attacker);
                if(attackerLFSettings.Equals(default(KeyValuePair<string, PermissionSettings>)))
                    return;

                PlayersPermissionsCache.Add(attacker.userID, attackerLFSettings.Key);
#if DEBUG
                PrintWarning($"Added to cache [{attacker.userID}, {attackerLFSettings.Key}]");
#endif
            }
            else
            {
                attackerLFSettings = new KeyValuePair<string, PermissionSettings>(cachedPerm, _config.Permissions[cachedPerm]);
#if DEBUG
                PrintWarning($"Found in cache [{attacker.userID}, {attackerLFSettings.Key}]");
#endif
            }

            if(attackerLFSettings.Value.VictimPrefabs != null && attackerLFSettings.Value.VictimPrefabs.Contains(entity.ShortPrefabName))
                goto skip;
            
            BasePlayer victimPlayer = entity as BasePlayer;
            if (victimPlayer != null)
            {
                if(victimPlayer.IsNpc || !victimPlayer.userID.IsSteamId())
                {
                    if(attackerLFSettings.Value.FromScientists)
                        goto skip;
                    else
                        return;
                }
                else
                {
                    if(attackerLFSettings.Value.FromPlayers)
                        goto skip;
                    else
                        return;
                }
            }
            else if (entity is BaseAnimalNPC)
            {
                if(attackerLFSettings.Value.FromAnimals)
                    goto skip;
                else
                    return;
            }
            else if (entity is PatrolHelicopter)
            {
                if(attackerLFSettings.Value.FromHelicopters)
                    goto skip;
                else
                    return;
            }
            else if (entity is BradleyAPC)
            {
                if(attackerLFSettings.Value.FromBradley)
                    goto skip;
                else
                    return;
            }
            else
                return;

            skip:

            if (info.damageTypes.GetMajorityDamageType() == DamageType.Bleeding)
                return;
                
            NextFrame(() =>
            {
                float totalDmg = info.damageTypes.Total();
                float lifestealTotal = attackerLFSettings.Value.LifestealPercent > 0 ? totalDmg * (attackerLFSettings.Value.LifestealPercent / 100f) : 0;
                float staticHeal = attackerLFSettings.Value.StaticHeal > 0 ? attackerLFSettings.Value.StaticHeal : 0;
                
                float healAmount = lifestealTotal + staticHeal;
                if (healAmount < 1)
                    return;

#if DEBUG
                PrintWarning($"totalDmg: {totalDmg}, lifestealTotal: {lifestealTotal}, staticHeal: {staticHeal}, healAmount: {healAmount}");
#endif
                attacker.Heal(healAmount);
            });
        }

        void OnUserPermissionGranted(string userId, string permName)
        {
            if(_config.Permissions.ContainsKey(permName.Replace($"{Name.ToLower()}.", "")) && PlayersPermissionsCache.ContainsKey(Convert.ToUInt64(userId)))
            {
                PlayersPermissionsCache.Remove(Convert.ToUInt64(userId));
#if DEBUG
                PrintWarning($"Removed from cache {userId}");
#endif
            }
        }

        void OnUserPermissionRevoked(string userId, string permName)
        {
            if(_config.Permissions.ContainsKey(permName.Replace($"{Name.ToLower()}.", "")) && PlayersPermissionsCache.ContainsKey(Convert.ToUInt64(userId)))
            {
                PlayersPermissionsCache.Remove(Convert.ToUInt64(userId));
#if DEBUG
                PrintWarning($"Removed from cache {userId}");
#endif
            }
        }

        private void OnUserGroupAdded(string userId, string groupName)
        {
            if(PlayersPermissionsCache.ContainsKey(Convert.ToUInt64(userId)))
            {
                PlayersPermissionsCache.Remove(Convert.ToUInt64(userId));
#if DEBUG
                PrintWarning($"Removed from cache {userId}");
#endif
            }
        }

        private void OnUserGroupRemoved(string userId, string groupName)
        {
            if(PlayersPermissionsCache.ContainsKey(Convert.ToUInt64(userId)))
            {
                PlayersPermissionsCache.Remove(Convert.ToUInt64(userId));
#if DEBUG
                PrintWarning($"Removed from cache {userId}");
#endif
            }
        }

        #endregion

        #region Functions

        private void RegisterPermissions()
        {
            if(_config == null || _config.Permissions == null)
                return;

            foreach(var perm in _config.Permissions)
                permission.RegisterPermission($"{Name.ToLower()}.{perm.Key.ToLower()}", this);
        }

        private KeyValuePair<string, PermissionSettings> GetPlayerPermission(BasePlayer player)
        {
            if(_config == null || _config.Permissions == null)
                return default(KeyValuePair<string, PermissionSettings>);

            foreach(var perm in _config.Permissions.Reverse())
            {
                if(permission.UserHasPermission(player.UserIDString, $"{Name.ToLower()}.{perm.Key}"))
                    return perm;
            }
            
            return default(KeyValuePair<string, PermissionSettings>);
        }

        #endregion

        #region Config

        private class PluginConfig
        {
            public Oxide.Core.VersionNumber Version;

            [JsonProperty("Permissions")]
            public Dictionary<string, PermissionSettings> Permissions { get; set; }
        }

        private class PermissionSettings
        {
            [JsonProperty("Lifesteal % (of damage dealt)")]
            public int LifestealPercent { get; set; }

            [JsonProperty("Static Heal")]
            public int StaticHeal { get; set; }

            [JsonProperty("Heal from Players?")]
            public bool FromPlayers { get; set; }

            [JsonProperty("Heal from Scientists?")]
            public bool FromScientists { get; set; }

            [JsonProperty("Heal from Animals?")]
            public bool FromAnimals { get; set; }

            [JsonProperty("Heal from Patrol Helicopter?")]
            public bool FromHelicopters { get; set; }

            [JsonProperty("Heal from Bradley APC?")]
            public bool FromBradley { get; set; }

            [JsonProperty("Custom Victims ShortPrefabName (Optional)")]
            public List<string> VictimPrefabs { get; set; }
        }

        private PluginConfig GetDefaultConfig()
        {
            PluginConfig result = new PluginConfig
            {
                Version = Version,
                Permissions = new Dictionary<string, PermissionSettings>
                {
                    ["default"] = new PermissionSettings()
                    {
                        LifestealPercent = 50,
                        StaticHeal = 0,
                        FromPlayers = true,
                        FromScientists = true,
                        VictimPrefabs = new List<string>()
                    },
                    ["vip1"] = new PermissionSettings()
                    {
                        LifestealPercent = 60,
                        StaticHeal = 0,
                        FromPlayers = true,
                        FromScientists = true,
                        FromAnimals = true,
                        VictimPrefabs = new List<string>()
                    },
                    ["vip2"] = new PermissionSettings()
                    {
                        LifestealPercent = 70,
                        StaticHeal = 5,
                        FromPlayers = true,
                        FromScientists = true,
                        FromAnimals = true,
                        FromHelicopters = true,
                        FromBradley = true,
                        VictimPrefabs = new List<string>()
                        {
                            "autoturret_deployed", "guntrap.deployed"
                        }
                    }
                }
            };

            return result;
        }

        private void CheckForConfigUpdates()
        {
            bool changes = false;

            if (_config == null || _config.Permissions == null)
            {
                PluginConfig tmpDefaultConfig = GetDefaultConfig();
                _config = tmpDefaultConfig;
                changes = true;
            }

            if(_config.Version != Version)
                changes = true;

            if (changes)
            {
                _config.Version = Version;

                PrintWarning("Config updated");
                SaveConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();

            CheckForConfigUpdates();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion
    }
}