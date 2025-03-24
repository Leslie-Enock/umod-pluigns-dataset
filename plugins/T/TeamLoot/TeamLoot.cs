using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Team Loot", "Synvy", "1.1.1")]
    [Description("Prevents players outside of your team from looting your body.")]
    public class TeamLoot : RustPlugin
    {
        #region Initialize

        private const string bypass = "teamloot.bypass";

        private void Init()
        {
            permission.RegisterPermission(bypass, this);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["WarningMessage"] = "You do not have permission to loot this player!"

            }, this);
        }

        #endregion Initialize

        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty("Show warning message")]
            public bool showWarningMessage = true;

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => _config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<Configuration>();

                if (_config == null)
                {
                    throw new JsonException();
                }

                if (!_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    SaveConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        #endregion Configuration

        #region Hooks

        private object CanLootPlayer(BasePlayer target, BasePlayer looter)
        {
            if (!HasPerm(looter.UserIDString, bypass))
            {
                if (target is not NPCPlayer)
                {
                    if (looter.userID != target.userID)
                    {
                        if (looter.currentTeam == 0UL)
                        {
                            if (_config.showWarningMessage)
                            {
                                PrintWarningMessage(looter, "WarningMessage");
                            }

                            return false;
                        }
                    }
                    else if ((looter.currentTeam != 0UL) && !looter.Team.members.Contains(target.userID))
                    {
                        if (_config.showWarningMessage)
                        {
                            PrintWarningMessage(looter, "WarningMessage");
                        }

                        return false;
                    }
                }         
            }

            return null;
        }

        private object CanLootEntity(BasePlayer player, DroppedItemContainer container)
        {
            var target = BasePlayer.FindAwakeOrSleeping(container.playerSteamID.ToString());

            if (target == null || target is NPCPlayer)
            {
                return null;
            }

            if (!HasPerm(player.UserIDString, bypass))
            {
                if (player.userID != target.userID)
                {
                    if (player.currentTeam == 0UL)
                    {
                        if (_config.showWarningMessage)
                        {
                            PrintWarningMessage(player, "WarningMessage");
                        }

                        return false;
                    }
                    else if ((player.currentTeam != 0UL) && !player.Team.members.Contains(target.userID))
                    {
                        if (_config.showWarningMessage)
                        {
                            PrintWarningMessage(player, "WarningMessage");
                        }

                        return false;
                    }
                }
            }

            return null;
        }

        private object CanLootEntity(BasePlayer player, LootableCorpse corpse)
        {
            var target = BasePlayer.FindAwakeOrSleeping(corpse.playerSteamID.ToString());

            if (target == null || target is NPCPlayer)
            {
                return null;
            }

            if (!HasPerm(player.UserIDString, bypass))
            {
                if (player.userID != target.userID)
                {
                    if (player.currentTeam == 0UL)
                    {
                        if (_config.showWarningMessage)
                        {
                            PrintWarningMessage(player, "WarningMessage");
                        }

                        return false;
                    }
                    else if ((player.currentTeam != 0UL) && !player.Team.members.Contains(target.userID))
                    {
                        if (_config.showWarningMessage)
                        {
                            PrintWarningMessage(player, "WarningMessage");
                        }

                        return false;
                    }
                }
            }

            return null;
        }

        #endregion Hooks

        #region Helpers

        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);

        private string GetLang(string langKey, string playerId = null, params object[] args) => string.Format(lang.GetMessage(langKey, this, playerId), args);

        private void PrintWarningMessage(BasePlayer player, string message)
        {
            PrintToChat(player, GetLang(message, player.UserIDString));
        }

        #endregion Helpers
    }
}