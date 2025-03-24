using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Anti Helibombing", "ZEODE", "1.1.11")]
    [Description("Prevent malicious collision damage to players helicopters within safe zones.")]
    public class AntiHelibombing: CovalencePlugin
    {
        private static System.Random random = new System.Random();
        private const string permAdmin = "antihelibombing.admin";
        private const string permBlacklist = "antihelibombing.blacklist";
        private const string permKickProtect = "antihelibombing.kickprotect";

        private Dictionary<string, Cooldown> CooldownDelay = new Dictionary<string, Cooldown>();
        private Dictionary<ulong, SpawnProtection> SpawnDelay = new Dictionary<ulong, SpawnProtection>();

        private class Cooldown
        {
            public Timer CooldownTimer;
        }

        private class SpawnProtection
        {
            public Timer SpawnTimer;
        }

        #region Config

        private ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Options")]
            public Options options;

            public class Options
            {
                [JsonProperty(PropertyName = "Block ALL heli damage in Safe Zones (overrides spawn protection)")]
                public bool blockAllDamage;
                [JsonProperty(PropertyName = "Use collision cooldown timer (prevent damage outside Safe Zone due to collision inside)")]
                public bool useCooldown;
                [JsonProperty(PropertyName = "Collision cooldown time (seconds)")]
                public int cooldownTime;
                [JsonProperty(PropertyName = "Use heli spawn protection cooldown timer")]
                public bool useSpawnProtection;
                [JsonProperty(PropertyName = "Spawn protection cooldown time (seconds)")]
                public int spawnCooldownTime;
                [JsonProperty(PropertyName = "Kick players who break the Safe Zone collision threshold")]
                public bool useKick;
                [JsonProperty(PropertyName = "Safe Zone collision threshold (example: 30)")]
                public int collisionLimit;
                [JsonProperty(PropertyName = "Block crush damage to players from Scrap Heli in safe zones")]
                public bool noCrush;
                [JsonProperty(PropertyName = "Clear player collision data on wipe")]
                public bool clearDataOnWipe;
                [JsonProperty(PropertyName = "Use chat prefix")]
                public bool useChatPrefix;
                [JsonProperty(PropertyName = "Chat prefix")]
                public string chatPrefix;
            }
            public VersionNumber Version { get; set; }
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                options = new ConfigData.Options
                {
                    blockAllDamage = false,
                    useCooldown = true,
                    cooldownTime = 20,
                    useSpawnProtection = true,
                    spawnCooldownTime = 45,
                    useKick = false,
                    collisionLimit = 30,
                    noCrush = true,
                    clearDataOnWipe = true,
                    useChatPrefix = true,
                    chatPrefix = "[Anti Helibombing]: "
                },
                Version = Version
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
                else
                {
                    UpdateConfigValues();
                }
            }
            catch (Exception ex)
            {
                if (ex is JsonSerializationException || ex is NullReferenceException || ex is JsonReaderException)
                {
                    Puts($"Exception Type: {ex.GetType()}");
                    LoadDefaultConfig();
                    return;
                }
                throw;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts($"Configuration file missing or corrupt, creating default config file.");
            config = GetDefaultConfig();
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private void UpdateConfigValues()
        {
            ConfigData defaultConfig = GetDefaultConfig();
            if (config.Version < Version)
            {
                Puts("Config update detected! Updating config file...");
                if (config.Version < new VersionNumber(1, 1, 0))
                {
                    config.options.useCooldown = defaultConfig.options.useCooldown;
                    config.options.cooldownTime = defaultConfig.options.cooldownTime;
                    config.options.clearDataOnWipe = defaultConfig.options.clearDataOnWipe;
                    config.options.useChatPrefix = defaultConfig.options.useChatPrefix;
                    config.options.chatPrefix = defaultConfig.options.chatPrefix;
                }
                if (config.Version < new VersionNumber(1, 1, 1))
                {
                    config.options.blockAllDamage = defaultConfig.options.blockAllDamage;
                    config.options.useSpawnProtection = defaultConfig.options.useSpawnProtection;
                    config.options.spawnCooldownTime = defaultConfig.options.spawnCooldownTime;
                }
                if (config.Version < new VersionNumber(1, 1, 5))
                {
                    config.options.useKick = defaultConfig.options.useKick;
                    config.options.collisionLimit = defaultConfig.options.collisionLimit;
                }
                if (config.Version < new VersionNumber(1, 1, 8))
                {
                    config.options.noCrush = defaultConfig.options.noCrush;
                }
                Puts("Config update completed!");
            }
            config.Version = Version;
            SaveConfig();
        }

        #endregion

        #region Stored Data

        private StoredData storedData;

        private class StoredData
        {
            public Dictionary<string, HeliBombData> CollisionData = new Dictionary<string, HeliBombData>();
        }

        private class HeliBombData
        {
            public string PlayerName;
            public int Collisions;
            public bool OnCooldown;
        }

        #endregion

        #region Language

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Permission"] = "You do not have permission to do that!",
                ["Usage"] = "Usage:\n - /ahb <Steam64Id> (Remove the / prefix for console)",
                ["PlayerNotFound"] = "No data for player with ID: {0}.",
                ["PlayerData"] = "Player Information:\nName: {0} ({1})\nSafe Zone Collisions: {2}\nBlacklisted: {3}\nKick Protected: {4}",
                ["ClearCmdUsage"] = "Usage:\n - /ahb.clear (clear ALL player data)\n - /ahb.clear <Steam64Id> (clear data for player)\nRemove the / prefix for console.",
                ["DataCleared"] = "ALL collision data cleared.",
                ["UserCleared"] = "Collision data cleared for: {0} ({1}).",
                ["Kicked"] = "You have been kicked for Helibombing!"
            }, this);
        }

        public string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        public void Message(IPlayer player, string key, params object[] args)
        {
            if (player == null) return;
            var message = Lang(key, player.Id, args);
            if (config.options.useChatPrefix)
            {
                player.Reply(config.options.chatPrefix + message);
            }
            else
            {
                player.Reply(message);
            }
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            if (!config.options.clearDataOnWipe) Unsubscribe(nameof(OnNewSave));
            permission.RegisterPermission(permAdmin, this);
            permission.RegisterPermission(permBlacklist , this);
            permission.RegisterPermission(permKickProtect , this);
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
                if (storedData == null)
                {
                    Puts("Data file is blank. Creating default data file...");
                    storedData = new StoredData();
                    SaveData();
                }
            }
            catch (Exception ex)
            {
                if (ex is JsonSerializationException || ex is NullReferenceException || ex is JsonReaderException)
                {
                    Puts("Data file invalid. Creating default data file...");
                    storedData = new StoredData();
                    SaveData();
                    return;
                }
                throw;
            }
        }

        private void OnNewSave()
        {
            Puts("Server wipe detected, ALL player data cleared.");
            storedData = new StoredData();
            SaveData();
        }

        private void Unload()
        {
            SaveData();
        }

        private void OnServerSave()
        {
            int delay  = random.Next(5, 10);
            timer.Once(delay, () =>
            {
                SaveData();
            });
        }

        private object OnEntitySpawned(Minicopter heli)
        {
            timer.Once(0.2f, () =>
            {
                if (heli == null || heli.IsDestroyed) return;
                if (heli.InSafeZone())
                {
                    SpawnProtection sp;
                    if (!SpawnDelay.TryGetValue(heli.net.ID.Value, out sp))
                    {
                        SpawnDelay[heli.net.ID.Value] = sp = new SpawnProtection
                        {
                            SpawnTimer = timer.Once(config.options.spawnCooldownTime , () =>
                            {
                                if (heli == null || heli.IsDestroyed) return;
                                SpawnDelay.Remove(heli.net.ID.Value);
                            }),
                        };
                    }
                }
            });
            return null;
        }

        private object OnEntityTakeDamage(BasePlayer player, HitInfo info)
        {
            if (info == null) return null;

            if (config.options.noCrush && player.InSafeZone())
            {
                var damageType = info.damageTypes.GetMajorityDamageType();
                if (damageType == DamageType.Fall && IsHeliCrushed(player))
                {
                    return true;
                }
            }
            return null;
        }


        private object OnEntityTakeDamage(Minicopter heli, HitInfo info)
        {
            if (info == null) return null;

            var pilot = heli.GetPlayerDamageInitiator() as BasePlayer;
            if (pilot != null && pilot.IPlayer.HasPermission(permBlacklist))
            {
                // Blacklisted players will always get damage in safe zones
                // with this permission because they're very naughty boys ;)
                return null;
            }

            var damageType = info.damageTypes.GetMajorityDamageType();
            if (config.options.blockAllDamage && heli.InSafeZone())
            {
                if (damageType == DamageType.Decay)
                {
                    return null;
                }
                else if (pilot != null)
                {
                    AddPlayerData(pilot, config.options.useCooldown);
                }
                return true;
            }
            else if (config.options.useSpawnProtection && heli.InSafeZone())
            {
                if (damageType == DamageType.Decay)
                {
                    return null;
                }

                if (pilot != null)
                {
                    AddPlayerData(pilot, config.options.useCooldown);
                }
                if (HasSpawnDelay(heli.net.ID.Value))
                {
                    return true;
                }
            }
            else
            {
                if (pilot != null && config.options.useCooldown)
                {
                    if (damageType == DamageType.Decay)
                    {
                        return null;
                    }
                    else if (HasCollisionDelay(pilot.UserIDString))
                    {
                        return true;
                    }
                }
            }
            return null;
        }

        #endregion

        #region Helpers

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        private void AddPlayerData(BasePlayer player, bool cooldown)
        {
            string steamId = player.UserIDString;
            if(!storedData.CollisionData.ContainsKey(steamId))
            {
                storedData.CollisionData.Add(steamId, new HeliBombData());
                storedData.CollisionData[steamId].PlayerName = player.displayName;
                storedData.CollisionData[steamId].Collisions = 1;
                storedData.CollisionData[steamId].OnCooldown = cooldown;
            }
            else
            {
                var cols = storedData.CollisionData[steamId].Collisions;
                storedData.CollisionData[steamId].PlayerName = player.displayName;
                storedData.CollisionData[steamId].Collisions = cols + 1;
                storedData.CollisionData[steamId].OnCooldown = cooldown;
            }
            if (config.options.useCooldown)
            {
                Cooldown cd;
                if (!CooldownDelay.TryGetValue(steamId, out cd))
                {
                    CooldownDelay[steamId] = cd = new Cooldown
                    {
                        CooldownTimer = timer.Once(config.options.cooldownTime, () =>
                        {
                            RemoveCooldown(steamId);
                        }),
                    };
                }
                else
                {
                    cd.CooldownTimer.Reset();
                }
            }
            if (config.options.useKick) ProcessKick(player.IPlayer);
        }

        private void RemoveCooldown(string steamId)
        {
            if (storedData.CollisionData.ContainsKey(steamId))
            {
                CooldownDelay.Remove(steamId);
                storedData.CollisionData[steamId].OnCooldown = false;
            }
        }

        private bool HasCollisionDelay(string steamId)
        {
            if(storedData.CollisionData.ContainsKey(steamId))
            {
                return storedData.CollisionData[steamId].OnCooldown;
            }
            return false;
        }

        private bool HasSpawnDelay(ulong heliId)
        {
            SpawnProtection sp;
            if (SpawnDelay.TryGetValue(heliId, out sp))
            {
                return true;
            }
            return false;
        }

        private bool IsHeliCrushed(BasePlayer player)
        {
            RaycastHit hit;
            var heightOffset = new Vector3(0, 5.0f, 0);
            if (Physics.Raycast(player.transform.position + heightOffset, Vector3.down, out hit, 10.0f, -5, QueryTriggerInteraction.UseGlobal))
            {
                if (hit.GetEntity() is ScrapTransportHelicopter)
                {
                    return true;
                }
            }
            return false;
        }

        public void ProcessKick(IPlayer player)
        {
            if (player.HasPermission(permKickProtect))
            {
                return;
            }
            if(storedData.CollisionData.ContainsKey(player.Id))
            {
                if (storedData.CollisionData[player.Id].Collisions >= config.options.collisionLimit)
                {
                    if (player.IsConnected)
                    {
                        player.Kick(Lang("Kicked"));
                        Puts($"{player} was kicked for HeliBombing after {storedData.CollisionData[player.Id].Collisions} safe zone collisions.");
                        storedData.CollisionData[player.Id].Collisions = 0;
                    }
                }
            }
        }

        #endregion

        #region Commands

        [Command("ahb", "antihelibombing")]
        private void CmdViewPlayerLog(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                Message(player, "Permission");
                return;
            }
            if (args?.Length < 1 || args?.Length > 1)
            {
                Message(player, "Usage");
                return;
            }

            ulong formatCheck;
            if (UInt64.TryParse(args[0], out formatCheck))
            {
                if (!storedData.CollisionData.ContainsKey(args[0]))
                {
                    Message(player, "PlayerNotFound", args[0]);
                    return;
                }
                else
                {
                    string name = storedData.CollisionData[args[0]].PlayerName;
                    string cols = Convert.ToString(storedData.CollisionData[args[0]].Collisions);
                    bool blacklist = player.HasPermission(permBlacklist);
                    bool kickprotect = player.HasPermission(permKickProtect);
                    Message(player, "PlayerData", name, args[0], cols, blacklist, kickprotect);
                    return;
                }
            }
            else
            {
                Message(player, "Usage");
                return;
            }
        }

        [Command("ahb.clear", "antihelibombing.clear")]
        private void CmdClearPlayerLog(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                Message(player, "Permission");
                return;
            }
            if (args?.Length > 1)
            {
                Message(player, "ClearCmdUsage");
                return;
            }
            else if (args?.Length < 1)
            {
                storedData.CollisionData.Clear();
                SaveData();
                Message(player, "DataCleared");
                return;
            }
            else if (args?.Length == 1)
            {
                ulong formatCheck;
                if (UInt64.TryParse(args[0], out formatCheck))
                {
                    if (!storedData.CollisionData.ContainsKey(args[0]))
                    {
                        Message(player, "PlayerNotFound", args[0]);
                        return;
                    }
                    else
                    {
                        string name = storedData.CollisionData[args[0]].PlayerName;
                        storedData.CollisionData.Remove(args[0]);
                        SaveData();
                        Message(player, "UserCleared", name, args[0]);
                        return;
                    }
                }
                else
                {
                    Message(player, "ClearCmdUsage");
                    return;
                }
            }
        }

        #endregion
    }
}