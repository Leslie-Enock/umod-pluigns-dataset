// Requires: EventManager
using Newtonsoft.Json;
using Oxide.Plugins.EventManagerEx;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("TeamDeathmatch", "k1lly0u", "0.4.2"), Description("Team Deathmatch event mode for EventManager")]
    class TeamDeathmatch : RustPlugin, IEventPlugin
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
        public bool InitializeEvent(EventManager.EventConfig config) => EventManager.InitializeEvent<TeamDeathmatchEvent>(this, config);

        public bool CanUseClassSelector => true;

        public bool RequireTimeLimit => true;

        public bool RequireScoreLimit => false;

        public bool UseScoreLimit => true;

        public bool UseTimeLimit => true;

        public bool IsTeamEvent => true;

        public void FormatScoreEntry(EventManager.ScoreEntry scoreEntry, ulong langUserId, out string score1, out string score2)
        {
            score1 = string.Format(Message("Score.Kills", langUserId), scoreEntry.value1);
            score2 = string.Format(Message("Score.Deaths", langUserId), scoreEntry.value2);
        }

        public List<EventManager.EventParameter> AdditionalParameters { get; } = new List<EventManager.EventParameter>
        {            
            new EventManager.EventParameter
            {
                DataType = "bool",
                Field = "closeOnStart",
                Input = EventManager.EventParameter.InputType.Toggle,
                IsRequired = false,
                Name = "Close Event On Start",
                DefaultValue = false
            }
        };

        public string ParameterIsValid(string fieldName, object value) => null;
        #endregion

        #region Event Classes
        public class TeamDeathmatchEvent : EventManager.BaseEventGame
        {
            public EventManager.Team winningTeam;

            private int teamAScore;
            private int teamBScore;

            internal override void PrestartEvent()
            {
                if (Config.GetParameter<bool>("closeEventOnStart"))
                    CloseEvent();

                base.PrestartEvent();
            }

            protected override void StartEvent()
            {                
                BalanceTeams();
                base.StartEvent();
            }

            protected override EventManager.Team GetPlayerTeam(BasePlayer player)
            {
                if (GetTeamCount(EventManager.Team.A) > GetTeamCount(EventManager.Team.B))
                    return EventManager.Team.B;
                return EventManager.Team.A;
            }

            internal override int GetTeamScore(EventManager.Team team) => team == EventManager.Team.B ? teamBScore : teamAScore;
            
            internal override void OnPlayerTakeDamage(EventManager.BaseEventPlayer eventPlayer, HitInfo hitInfo)
            {
                EventManager.BaseEventPlayer attacker = EventManager.GetUser(hitInfo.InitiatorPlayer);
                if (attacker == null || eventPlayer.Team != attacker.Team)
                {
                    base.OnPlayerTakeDamage(eventPlayer, hitInfo);
                    return;
                }

                if (GodmodeEnabled || eventPlayer.IsDead || eventPlayer.IsInvincible)
                {
                    EventManager.ClearDamage(hitInfo);
                    return;
                }

                if (Configuration.FriendlyFireModifier != 1f)
                    hitInfo.damageTypes.ScaleAll(Configuration.FriendlyFireModifier);

                eventPlayer.OnTakeDamage(attacker.Player.userID);
            }

            internal override void OnEventPlayerDeath(EventManager.BaseEventPlayer victim, EventManager.BaseEventPlayer attacker = null, HitInfo info = null)
            {
                if (victim == null)
                    return;

                victim.OnPlayerDeath(attacker, Configuration.RespawnTime);

                if (attacker != null && victim != attacker && victim.Team != attacker.Team)
                {
                    int score;
                    if (attacker.Team == EventManager.Team.B)
                        score = teamBScore += 1;
                    else score = teamAScore += 1;

                    attacker.OnKilledPlayer(info);

                    if (Config.ScoreLimit > 0 && score >= Config.ScoreLimit)
                    {
                        winningTeam = attacker.Team;
                        InvokeHandler.Invoke(this, EndEvent, 0.1f);
                        return;
                    }
                }

                UpdateScoreboard();
                base.OnEventPlayerDeath(victim, attacker);
            }

            protected override void GetWinningPlayers(ref List<EventManager.BaseEventPlayer> winners)
            {
                if (winningTeam < EventManager.Team.None)
                {
                    if (eventPlayers.Count > 0)
                    {                        
                        for (int i = 0; i < eventPlayers.Count; i++)
                        {
                            EventManager.BaseEventPlayer eventPlayer = eventPlayers[i];
                            if (eventPlayer == null)
                                continue;

                            if (eventPlayer.Team == winningTeam)
                                winners.Add(eventPlayer);                            
                        }
                    }
                }
            }

            #region Scoreboards
            protected override void BuildScoreboard()
            {
                scoreContainer = EMInterface.CreateScoreboardBase(this);

                int index = -1;
                EMInterface.CreatePanelEntry(scoreContainer, string.Format(GetMessage("Score.Team", 0UL), teamAScore, TeamAColor, TeamBColor, teamBScore), index += 1);

                if (Config.ScoreLimit > 0)
                    EMInterface.CreatePanelEntry(scoreContainer, string.Format(GetMessage("Score.Limit", 0UL), Config.ScoreLimit), index += 1);

                EMInterface.CreateScoreEntry(scoreContainer, string.Empty, "K", "D", index += 1);

                for (int i = 0; i < Mathf.Min(scoreData.Count, 15); i++)
                {
                    EventManager.ScoreEntry score = scoreData[i];
                    EMInterface.CreateScoreEntry(scoreContainer, $"<color={(score.team == EventManager.Team.A ? TeamAColor : TeamBColor)}>{score.displayName}</color>", ((int)score.value1).ToString(), ((int)score.value2).ToString(), i + index + 1);
                }
            }

            protected override float GetFirstScoreValue(EventManager.BaseEventPlayer eventPlayer) => eventPlayer.Kills;

            protected override float GetSecondScoreValue(EventManager.BaseEventPlayer eventPlayer) => eventPlayer.Deaths;

            protected override void SortScores(ref List<EventManager.ScoreEntry> list)
            {
                list.Sort(delegate (EventManager.ScoreEntry a, EventManager.ScoreEntry b)
                {
                    int primaryScore = a.value1.CompareTo(b.value1);

                    if (primaryScore == 0)
                        return a.value2.CompareTo(b.value2) * -1;

                    return primaryScore;
                });
            }
            #endregion
        }
        #endregion

        #region Config        
        private static ConfigData Configuration;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Respawn time (seconds)")]
            public int RespawnTime { get; set; }

            [JsonProperty(PropertyName = "Friendly fire damage modifier (0.0 is no damage, 1.0 is normal damage)")]
            public float FriendlyFireModifier { get; set; }

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
                FriendlyFireModifier = 1.0f,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(Configuration, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (Configuration.Version < new Core.VersionNumber(0, 4, 1))
                Configuration.FriendlyFireModifier = baseConfig.FriendlyFireModifier;

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
            ["Score.Deaths"] = "Deaths: {0}",
            ["Score.Name"] = "Kills",
            ["Score.Limit"] = "Score Limit : {0}",
            ["Score.Team"] = "{0} : <color={1}>Team A</color> | <color={2}>Team B</color> : {3}"
        };
        #endregion
    }
}
