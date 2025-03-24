// Requires: EventManager
using Newtonsoft.Json;
using Oxide.Plugins.EventManagerEx;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("LastManStanding", "k1lly0u", "3.0.1"), Description("Last man standing event mode for EventManager")]
    class LastManStanding : RustPlugin, IEventPlugin
    {
        #region Oxide Hooks
        private void OnServerInitialized()
        {
            EventManager.RegisterEvent(Title, this);

            GetMessage = Message;
        }

        protected override void LoadDefaultMessages() => lang.RegisterMessages(Messages, this);

        private void Unload()
        {
            if (!EventManager.IsUnloading)
                EventManager.UnregisterEvent(Title);

            Configuration = null;
        }
        #endregion

        #region Event Checks
        public bool InitializeEvent(EventManager.EventConfig config) => EventManager.InitializeEvent<LastManStandingEvent>(this, config);

        public bool CanUseClassSelector => true;

        public bool RequireTimeLimit => true;

        public bool RequireScoreLimit => false;

        public bool UseScoreLimit => false;

        public bool UseTimeLimit => true;

        public bool IsTeamEvent => false;

        public void FormatScoreEntry(EventManager.ScoreEntry scoreEntry, ulong langUserId, out string score1, out string score2)
        {
            score1 = string.Empty;
            score2 = string.Format(Message("Score.Kills", langUserId), scoreEntry.value2);
        }

        public List<EventManager.EventParameter> AdditionalParameters { get; } = null;

        public string ParameterIsValid(string fieldName, object value) => null;
        #endregion

        #region Functions
        private static string ToOrdinal(int i) => (i + "th").Replace("1th", "1st").Replace("2th", "2nd").Replace("3th", "3rd");
        #endregion

        #region Event Classes
        public class LastManStandingEvent : EventManager.BaseEventGame
        {
            public EventManager.BaseEventPlayer winner;

            protected override EventManager.BaseEventPlayer AddPlayerComponent(BasePlayer player) => player.gameObject.AddComponent<LastManStandingPlayer>();

            internal override void PrestartEvent()
            {
                CloseEvent();
                base.PrestartEvent();
            }

            internal override void OnEventPlayerDeath(EventManager.BaseEventPlayer victim, EventManager.BaseEventPlayer attacker = null, HitInfo info = null)
            {
                if (victim == null)
                    return;

                victim.OnPlayerDeath(attacker, Configuration.RespawnTime);

                if (attacker != null && victim != attacker)
                {
                    attacker.OnKilledPlayer(info);

                    if (GetAlivePlayerCount() <= 1)
                    {
                        winner = attacker;
                        InvokeHandler.Invoke(this, EndEvent, 0.1f);
                        return;
                    }
                }

                UpdateScoreboard();

                base.OnEventPlayerDeath(victim, attacker);
            }

            protected override void GetWinningPlayers(ref List<EventManager.BaseEventPlayer> winners)
            {
                if (winner == null)
                {
                    if (eventPlayers.Count > 0)
                    {
                        int kills = 0;

                        for (int i = 0; i < eventPlayers.Count; i++)
                        {
                            EventManager.BaseEventPlayer eventPlayer = eventPlayers[i];
                            if (eventPlayer == null)
                                continue;

                            if (eventPlayer.Kills > kills)
                            {
                                winner = eventPlayer;
                                kills = eventPlayer.Kills;
                            }                            
                        }
                    }
                }

                if (winner != null)
                    winners.Add(winner);
            }

            #region Scoreboards
            protected override void BuildScoreboard()
            {
                scoreContainer = EMInterface.CreateScoreboardBase(this);

                int index = -1;

                if (Config.ScoreLimit > 0)
                    EMInterface.CreatePanelEntry(scoreContainer, string.Format(GetMessage("Score.Remaining", 0UL), eventPlayers.Count), index += 1);

                EMInterface.CreateScoreEntry(scoreContainer, string.Empty, string.Empty, "K", index += 1);

                for (int i = 0; i < Mathf.Min(scoreData.Count, 15); i++)
                {
                    EventManager.ScoreEntry score = scoreData[i];
                    EMInterface.CreateScoreEntry(scoreContainer, score.displayName, string.Empty, ((int)score.value2).ToString(), i + index + 1);
                }
            }

            protected override float GetFirstScoreValue(EventManager.BaseEventPlayer eventPlayer) => 0;

            protected override float GetSecondScoreValue(EventManager.BaseEventPlayer eventPlayer) => eventPlayer.Kills;

            protected override void SortScores(ref List<EventManager.ScoreEntry> list)
            {
                list.Sort(delegate (EventManager.ScoreEntry a, EventManager.ScoreEntry b)
                {                    
                    return a.value2.CompareTo(b.value2);
                });
            }
            #endregion
        }

        private class LastManStandingPlayer : EventManager.BaseEventPlayer
        {
            internal override void OnPlayerDeath(EventManager.BaseEventPlayer attacker = null, float respawnTime = 5)
            {
                AddPlayerDeath(attacker);

                DestroyUI();

                int position = Event.GetAlivePlayerCount();

                string message = attacker != null ? string.Format(GetMessage("Death.Killed", Player.userID), attacker.Player.displayName, ToOrdinal(position + 1), position) :
                                 IsOutOfBounds ? string.Format(GetMessage("Death.OOB", Player.userID), ToOrdinal(position + 1), position) :
                                 string.Format(GetMessage("Death.Suicide", Player.userID), ToOrdinal(position + 1), position);

                EMInterface.DisplayDeathScreen(this, message, false);
            }
        }
        #endregion

        #region Config        
        private static ConfigData Configuration;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Respawn time (seconds)")]
            public int RespawnTime { get; set; }

            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Configuration = Config.ReadObject<ConfigData>();

            if (Configuration.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(Configuration, true);
        }

        protected override void LoadDefaultConfig() => Configuration = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                RespawnTime = 5,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(Configuration, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            Configuration.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Localization
        public string Message(string key, ulong playerId = 0U) => lang.GetMessage(key, this, playerId != 0U ? playerId.ToString() : null);

        private static Func<string, ulong, string> GetMessage;

        private readonly Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["Score.Kills"] = "Kills: {0}",
            ["Score.Name"] = "Kills",
            ["Score.Remaining"] = "Players Remaining : {0}",
            ["Death.Killed"] = "You were killed by {0}\nYou placed {1}\n{2} players remain",
            ["Death.Suicide"] = "You died...\nYou placed {0}\n{1} players remain",
            ["Death.OOB"] = "You left the playable area\nYou placed {0}\n{1} players remain",
        };
        #endregion
    }
}
