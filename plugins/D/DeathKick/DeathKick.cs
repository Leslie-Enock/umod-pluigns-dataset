using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Death Kick", "Wulf", "1.0.1")]
    [Description("Kicks players for a specified amount of time upon death")]
    class DeathKick : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Death by animals")]
            public bool DeathByAnimals = true;

            [JsonProperty("Death by autoturrets")]
            public bool DeathByAutoturrets = true;

            [JsonProperty("Death by barricades")]
            public bool DeathByBarricades = true;

            [JsonProperty("Death by beartraps")]
            public bool DeathByBeartraps = true;

            [JsonProperty("Death by fall")]
            public bool DeathByFall = true;

            [JsonProperty("Death by floorspikes")]
            public bool DeathByFloorspikes = true;

            [JsonProperty("Death by helicopters")]
            public bool DeathByHelicopters = true;

            [JsonProperty("Death by landmines")]
            public bool DeathByLandmines = true;

            [JsonProperty("Death by players")]
            public bool DeathByPlayers = true;

            [JsonProperty("Death by suicide")]
            public bool DeathBySuicide = true;

            [JsonProperty("Deaths a player is limited to")]
            public int DeathLimit = 1;

            [JsonProperty("Time before reconnection (minutes)")]
            public int KickCooldown = 30;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            LogWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Minutes"] = "minutes",
                ["Seconds"] = "seconds",
                ["YouDied"] = "You died and must wait {0} {1} before reconnecting",
                ["YouMustWait"] = "You must wait another {0} {1} before reconnecting"
            }, this);
        }

        #endregion Localization

        #region Initialization

        private readonly Dictionary<string, double> deadPlayers = new Dictionary<string, double>();
        private readonly Dictionary<string, int> deathCounts = new Dictionary<string, int>();
        private readonly List<Timer> cooldowns = new List<Timer>();

        private const string permExempt = "deathkick.exempt";

        private void Init()
        {
            permission.RegisterPermission(permExempt, this);
        }

        private void Unload() => ClearData();

        #endregion Initialization

        #region Death Handling

        private void OnEntityDeath(BasePlayer player, HitInfo hitInfo)
        {
            ProcessDeath(player.IPlayer, hitInfo);
        }

        private void ProcessDeath(IPlayer player, HitInfo hitInfo)
        {
            if (permission.UserHasPermission(player.Id, permExempt) || !GetDeathType(hitInfo))
            {
                return;
            }

            if (!deathCounts.ContainsKey(player.Id))
            {
                deathCounts.Add(player.Id, 0);
            }
            deathCounts[player.Id]++;

            if (deathCounts[player.Id] >= config.DeathLimit)
            {
                deadPlayers.Add(player.Id, GetCurrentTime() + (config.KickCooldown * 60));
                cooldowns.Add(timer.Once(config.KickCooldown * 60, () => deadPlayers.Remove(player.Id)));

                if (player.IsConnected)
                {
                    player.Kick(GetLang("YouDied", player.Id, config.KickCooldown, GetLang("Minutes", player.Id)));
                }

                deathCounts.Remove(player.Id);
            }
        }

        private bool GetDeathType(HitInfo hitInfo)
        {
            if (hitInfo == null || config.DeathByFall)
            {
                return true;
            }

            BaseEntity attacker = hitInfo.Initiator;
            if (attacker == null)
            {
                return false;
            }

            if (attacker.ToPlayer() != null)
            {
                if (hitInfo.damageTypes.GetMajorityDamageType().ToString() == "Suicide" && config.DeathBySuicide)
                {
                    return true;
                }

                if (config.DeathByPlayers)
                {
                    return true;
                }
            }
            else if (attacker.name.Contains("patrolhelicopter.pr") && config.DeathByHelicopters)
            {
                return true;
            }
            else if (attacker.name.Contains("animals/") && config.DeathByAnimals)
            {
                return true;
            }
            else if (attacker.name.Contains("beartrap.prefab") && config.DeathByBeartraps)
            {
                return true;
            }
            else if (attacker.name.Contains("landmine.prefab") && config.DeathByLandmines)
            {
                return true;
            }
            else if (attacker.name.Contains("spikes.floor.prefab") && config.DeathByFloorspikes)
            {
                return true;
            }
            else if (attacker.name.Contains("autoturret_deployed.prefab") && config.DeathByAutoturrets)
            {
                return true;
            }
            else if ((attacker.name.Contains("deployable/barricades") || attacker.name.Contains("wall.external.high")) && config.DeathByBarricades)
            {
                return true;
            }

            return false;
        }

        private object CanUserLogin(string playerName, string playerId)
        {
            if (deadPlayers.ContainsKey(playerId))
            {
                int remaining = (int)deadPlayers[playerId] - (int)GetCurrentTime();
                int timeLeft = remaining / 60;
                string timeFormat = GetLang("Minutes", playerId);

                if (remaining <= 90)
                {
                    timeLeft = remaining;
                    timeFormat = GetLang("Seconds", playerId);
                }

                return GetLang("YouMustWait", playerId, timeLeft, timeFormat);
            }

            return null;
        }

        private void ClearData()
        {
            foreach (Timer cooldown in cooldowns)
            {
                cooldown.Destroy();
            }
            deadPlayers.Clear();
        }

        #endregion Death Handling

        #region Helpers

        private static double GetCurrentTime()
        {
            return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        }

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }

        private void Message(IPlayer player, string textOrLang, params object[] args)
        {
            if (player.IsConnected)
            {
                string message = GetLang(textOrLang, player.Id, args);
                player.Reply(message != textOrLang ? message : textOrLang);
            }
        }

        #endregion Helpers
    }
}
