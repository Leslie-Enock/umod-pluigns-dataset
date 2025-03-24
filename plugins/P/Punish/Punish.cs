using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Punish", "Wulf", "1.1.1")]
    [Description("Punish players for various actions/events")]
    public class Punish : CovalencePlugin
    {
        #region Configuration

        private Configuration config;

        public class Punishment
        {
            [JsonProperty("Enable punishment")]
            public bool Enable = true;

            [JsonProperty("Ban as punishment")]
            public bool Ban = false;

            /*[JsonProperty("Freeze as punishment")] // TODO: Finish implementing
            public bool Freeze = false;

            [JsonProperty("Amount of time to freeze")] // TODO: Finish implementing
            public bool FreezeTime = false;*/

            [JsonProperty("Hurt as punishment")]
            public float Hurt = 0f;

            [JsonProperty("Kick as punishment")]
            public bool Kick = false;

#if RUST
            [JsonProperty("Jail as punishment")]
            public bool Jail = false;

            [JsonProperty("Name of jail/prison to use")]
            public string JailName = "";

            [JsonProperty("Amount of time to jail")]
            public int JailTime = 10;
#endif

            /*[JsonProperty("Mute as punishment")] // TODO: Finish implementing
            public bool Mute = false;

            [JsonProperty("Amount of time to mute")] // TODO: Finish implementing
            public bool MuteTime = false;*/

            [JsonProperty("Slap as punishment")]
            public bool Slap = false;

            [JsonProperty("Amount of damage per slap")]
            public int SlapDamage = 10;

            [JsonProperty("Intensity of each slap")]
            public int SlapIntensity = 5;

            [JsonProperty("Number of times to slap")]
            public int SlapAmount = 1;

            [JsonProperty("Economics withdrawl")]
            public bool Economics = false;

            [JsonProperty("Economics amount")]
            public int EconomicsAmount = 1000;

            [JsonProperty("Server Rewards withdrawl")]
            public bool ServerRewards = false;

            [JsonProperty("Server Rewards amount")]
            public int ServerRewardsAmount = 100;
        }

        public class Configuration
        {
            [JsonProperty("Punish for dying")]
            public Punishment PunishmentDeath = new Punishment();

            //[JsonProperty("Punish for damaging other players")]
            //public Punishment PunishmentPvPDamage = new Punishment(); // TODO: Finish implementing

            [JsonProperty("Punish for killing other players")]
            public Punishment PunishmentPvPDeath = new Punishment();

            [JsonProperty("Include actions from NPCs")]
            public bool IncludeNPCs = true;

            [JsonProperty("Use permission system")]
            public bool UsePermissions = false;

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
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["PunishmentBan"] = "You were banned for {0}",
                ["PunishmentHurt"] = "You were hurt for {0}",
                ["PunishmentKick"] = "You were kicked for {0}",
                ["PunishmentJail"] = "You were jailed for {0}",
                ["PunishmentSlap"] = "You were slapped for {0}",
                ["PunishmentEconomics"] = "You lost {0:C} for {1}",
                ["PunishmentServerRewards"] = "You lost {0} RP for {1}",
                ["ReasonDeath"] = "dying",
                //["ReasonPvPDamage"] = "damaging a player",
                ["ReasonPvPDeath"] = "killing a player"
            }, this);
        }

        #endregion Localization

        #region Initialization

        [PluginReference]
        private Plugin Economics, ServerRewards, Jail, Slap;

        private const string permExclude = "punish.exclude";

        private void Init()
        {
            permission.RegisterPermission(permExclude, this);
        }

        #endregion Initialization

        #region Punishment

        private void PunishPlayer(Punishment punishment, IPlayer player, string reason)
        {
            if (config.UsePermissions && player.HasPermission(permExclude))
            {
                return;
            }

            if (punishment.Economics && Economics != null && Economics.IsLoaded)
            {
                if (Economics.Call<bool>("Withdraw", player.Id, (double)(punishment.EconomicsAmount > 0 ? -punishment.EconomicsAmount : punishment.EconomicsAmount)))
                {
                    Message(player, "PunishmentEconomics", punishment.EconomicsAmount, reason);
                }
            }

            if (punishment.ServerRewards && ServerRewards != null && ServerRewards.IsLoaded)
            {
                if (ServerRewards.Call<bool>("TakePoints", player.Id, punishment.ServerRewardsAmount > 0 ? -punishment.ServerRewardsAmount : punishment.ServerRewardsAmount))
                {
                    Message(player, "PunishmentServerRewards", punishment.ServerRewardsAmount, reason);
                }
            }

#if RUST

            if (punishment.Jail && !string.IsNullOrEmpty(punishment.JailName) && Jail != null && Jail.IsLoaded)
            {
                Jail.Call("SendToPrison", player.Object as BasePlayer, punishment.JailName, (double)punishment.JailTime);
                Message(player, "PunishmentJail", reason);
            }

#endif

            if (punishment.Slap && Slap != null && Slap.IsLoaded)
            {
                Slap.Call("SlapPlayer", player.Id, punishment.SlapDamage, punishment.SlapIntensity, punishment.SlapAmount);
                Message(player, "PunishmentSlap", reason);
            }

            if (punishment.Hurt > 0)
            {
                player.Hurt(punishment.Hurt);
                Message(player, "PunishmentHurt", reason);
            }

            if (punishment.Ban)
            {
                player.Ban(GetLang("PunishmentBan", player.Id, reason));
            }

            if (punishment.Kick)
            {
                player.Kick(GetLang("PunishmentKick", player.Id, reason));
            }
        }

        #endregion Punishment

        #region Actions/Events

#if HURTWORLD

        private void OnPlayerDeath(PlayerSession session, EntityEffectSourceData sourceData)
        {
            if (sourceData.EntitySource != null)
            {
                EntityReferenceCache referenceCache = RefTrackedBehavior<EntityReferenceCache>.GetByTransform(sourceData.EntitySource.transform);
                if (referenceCache?.Stats != null)
                {
                    PlayerSession attacker = GameManager.Instance.GetSession(referenceCache.Stats.networkView.owner);
                    PunishPlayer(config.PunishmentPvPDeath, attacker.IPlayer, GetLang("ReasonPvPDeath", attacker.IPlayer.Id));
                }
            }

            PunishPlayer(config.PunishmentDeath, session.IPlayer, GetLang("ReasonDeath", session.IPlayer.Id));
        }

#elif REIGNOFKINGS

        private void OnEntityDeath(CodeHatch.Networking.Events.Entities.EntityDeathEvent evt)
        {
            if (evt.Sender != null)
            {
                PunishPlayer(config.PunishmentPvPDeath, evt.Sender.IPlayer, GetLang("ReasonPvPDeath", evt.Sender.IPlayer.Id));
            }

            PunishPlayer(config.PunishmentDeath, evt.Entity.Owner.IPlayer, GetLang("ReasonDeath", evt.Entity.Owner.IPlayer.Id));
        }

#elif RUST

        private void OnPlayerDeath(BasePlayer basePlayer, HitInfo hitInfo)
        {
            if (basePlayer.IsNpc || (!config.IncludeNPCs && hitInfo.InitiatorPlayer.IsValid() && hitInfo.InitiatorPlayer.IsNpc))
            {
                return;
            }

            if (hitInfo.InitiatorPlayer != null && !hitInfo.InitiatorPlayer.IsNpc)
            {
                BasePlayer attacker = hitInfo.InitiatorPlayer;
                PunishPlayer(config.PunishmentPvPDeath, attacker.IPlayer, GetLang("ReasonPvPDeath", attacker.IPlayer.Id));
            }

            PunishPlayer(config.PunishmentDeath, basePlayer.IPlayer, GetLang("ReasonDeath", basePlayer.IPlayer.Id));
        }

#elif SEVENDAYSTODIE

        private void OnEntityDeath(EntityAlive entity)
        {
            ClientInfo clientInfo = ConnectionManager.Instance.Clients.ForEntityId(entity.entityId);
            if (clientInfo != null)
            {
                if (entity.entityThatKilledMe != null)
                {
                    ClientInfo attackerInfo = ConnectionManager.Instance.Clients.ForEntityId(entity.entityThatKilledMe.entityId);
                    if (attackerInfo != null)
                    {
                        PunishPlayer(config.PunishmentPvPDeath, attackerInfo.IPlayer, GetLang("ReasonPvPDeath", attackerInfo.IPlayer.Id));
                    }
                }

                PunishPlayer(config.PunishmentDeath, clientInfo.IPlayer, GetLang("ReasonDeath", clientInfo.IPlayer.Id));
            }
        }

#endif

        #endregion Actions/Events

        #region Helpers

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
