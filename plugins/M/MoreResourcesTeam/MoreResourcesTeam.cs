using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("More Resources Team", "Krungh Crow", "1.1.1")]
    [Description("Get more resources when you're farming close to a team member")]

    class MoreResourcesTeam : CovalencePlugin
    {
        #region Variables
        const string Use_Perm = "moreresourcesteam.use";
        const string Vip_Perm = "moreresourcesteam.vip";
        #endregion

        #region Configuration
        void Init()
        {
            if (!LoadConfigVariables())
            {
            Puts("Config file issue detected. Please delete file, or check syntax and fix.");
            return;
            }
            permission.RegisterPermission(Use_Perm, this);
            permission.RegisterPermission(Vip_Perm, this);
        }

        private ConfigData configData;

        class ConfigData
        {
            [JsonProperty("Distance between you and your mates (in feet)")]
            public float distanceMate = 32f;
            [JsonProperty("Bonus percentage Default")]
            public int bonusMate = 10;
            [JsonProperty("Bonus percentage Vip")]
            public int bonusMateVip = 50;
        }

        private bool LoadConfigVariables()
        {
            try
            {
            configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
            return false;
            }
            SaveConf();
            return true;
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Fresh install detected Creating a new config file.");
            configData = new ConfigData();
            SaveConf();
        }

        void SaveConf() => Config.WriteObject(configData, true);
        #endregion

        #region LanguageAPI
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GatherMoreMessage"] = "You got {0}% more because you're close to a member of your team"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GatherMoreMessage"] = "Vous avez re�u {0}% de ressources en plus car vous �tes proche d'un membre de votre �quipe"
            }, this, "fr");
        }

        #endregion

        #region Helpers

        string GetMessage(string key, string steamId = null) => lang.GetMessage(key, this, steamId);

        private void notifyPlayer(IPlayer player, string text)
        {
            text = Formatter.ToPlaintext(text);
            player.Command("gametip.hidegametip");
            player.Command("gametip.showgametip", text);
            timer.In(5, () => player?.Command("gametip.hidegametip"));
        }
        #endregion

        #region Hooks

        void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (!permission.UserHasPermission(player.UserIDString, Use_Perm))
            {

                var currentTeam = player.currentTeam;
                if (currentTeam != 0)
                {
                    RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindTeam(currentTeam);
                    if (team != null)
                    {
                        var players = team.members;
                        if (team.members.Count > 1)
                        {
                            int totalPercentage = 0;
                            foreach (var teamMember in players)
                            {
                                if (teamMember != player.userID)
                                {
                                    BasePlayer member = RelationshipManager.FindByID(teamMember);
                                    if (member != null)
                                    {
                                        if (Vector3.Distance(player.transform.position, member.transform.position) <= configData.distanceMate)
                                        {
                                            if (permission.UserHasPermission(player.UserIDString, Vip_Perm))
                                            {
                                                totalPercentage = totalPercentage + configData.bonusMateVip;
                                                item.amount = item.amount + (item.amount * configData.bonusMateVip / 100);
                                            }
                                            else if (!permission.UserHasPermission(player.UserIDString, Vip_Perm))
                                            {
                                                totalPercentage = totalPercentage + configData.bonusMate;
                                                item.amount = item.amount + (item.amount * configData.bonusMate / 100);
                                            }
                                        }
                                    }
                                }
                            }
                            notifyPlayer(player.IPlayer, string.Format(GetMessage("GatherMoreMessage", player.UserIDString), totalPercentage.ToString()));
                        }
                    }
                }
            }
            return;
        }

        #endregion
    }
}