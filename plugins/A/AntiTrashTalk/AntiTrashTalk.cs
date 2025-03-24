using System;
using System.Collections.Generic;

using UnityEngine;

using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Anti Trash-Talk", "0x89A", "1.0.0")]
    [Description("Mutes players after they kill or are killed by another player")]
    class AntiTrashTalk : RustPlugin
    {
        private Configuration config;
        private Dictionary<ulong, float> lastKillOrDeathTime = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> lastVoiceMessageTime = new Dictionary<ulong, float>();

        private const string exemptionPerm = "antitrashtalk.bypass";

        #region -Oxide Hooks-

        private void Init() => permission.RegisterPermission(exemptionPerm, this);

        private void OnPlayerDeath(BasePlayer player, HitInfo info) => DoBlock(player, info);

        private void OnPlayerWound(BasePlayer player, HitInfo info) => DoBlock(player, info);

        private object OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel) => CanSpeak(player);

        private object OnPlayerVoice(BasePlayer player, Byte[] data) => CanSpeak(player, true);

        #endregion

        #region -Helpers-

        private object CanSpeak(BasePlayer player, bool isVoice = false)
        {
            float value;
            if (lastKillOrDeathTime.TryGetValue(player.userID, out value) && Time.time - value < config.blockDuration)
            {
                if (isVoice)
                {
                    float lastTime;
                    if (lastVoiceMessageTime.TryGetValue(player.userID, out lastTime) && Time.time - lastTime < 3f) return true;
                    else if (!lastVoiceMessageTime.ContainsKey(player.userID)) lastVoiceMessageTime.Add(player.userID, Time.time);

                    lastVoiceMessageTime[player.userID] = Time.time;
                }
                
                player.ChatMessage(string.Format(lang.GetMessage("CannotSpeak", this, player.UserIDString), Mathf.Round(config.blockDuration - (Time.time - value))));

                return true;
            }
            
            return null;
        }

        private void DoBlock(BasePlayer victim, HitInfo info)
        {
            BasePlayer other = info?.InitiatorPlayer;
            if (other == null || (config.blockSelfInflicted && victim == other)) return;

            if (config.blockForVictim) BlockChat(victim);
            BlockChat(other);
        }

        private void BlockChat(BasePlayer player)
        {
            if (player == null || permission.UserHasPermission(player.UserIDString, exemptionPerm)) return;

            if (lastKillOrDeathTime.ContainsKey(player.userID)) lastKillOrDeathTime[player.userID] = Time.time;
            else lastKillOrDeathTime.Add(player.userID, Time.time);
        }

        #endregion

        #region -Configuration-

        private class Configuration
        {
            [JsonProperty("Block duration (seconds)")]
            public float blockDuration = 30f;

            [JsonProperty("Block for dead/wounded player")]
            public bool blockForVictim = false;

            [JsonProperty("Block when self inflicted")]
            public bool blockSelfInflicted = false;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Failed to load config, using default values");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region -Localisation-

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CannotSpeak"] = "You cannot speak right now, please wait {0} more seconds"
            }, this);
        }

        #endregion
    }
}
