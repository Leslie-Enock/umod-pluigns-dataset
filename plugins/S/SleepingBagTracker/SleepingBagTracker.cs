using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using UnityEngine.Networking;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Sleeping Bag Tracker", "Rogder Dodger", "1.0.3")]
    [Description("Tracks Sleeping bags assigned to other players (with team information)")]
    
    internal class SleepingBagTracker : CovalencePlugin
    {
        #region Configuration
        [PluginReference] private readonly Plugin Clans;
        private const string DefaultWebhookURL = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";
        private Configuration _config;
        private class Configuration
        {
            [JsonProperty(PropertyName = "Log To Console")]
            public bool LogToConsole;

            [JsonProperty(PropertyName = "Log To Discord")]
            public bool LogToDiscord;

            [JsonProperty(PropertyName = "Discord Webhook URL")]
            public string DiscordWebhookUrl = DefaultWebhookURL;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }
        protected override void LoadDefaultConfig()
        {
            PrintWarning("A new configuration file is being generated.");
            _config = new Configuration
            {
                LogToConsole = true,
                LogToDiscord = true,
                DiscordWebhookUrl = DefaultWebhookURL,
            };
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["EmbedTitle"] = "Bag Assigned",
                ["EmbedBagDetailsWithBattlemetrics"] = "**Sleeping Bag** Owned by\n{ownerName}\n{ownerId}\n[Steam Profile](https://steamcommunity.com/profiles/{ownerId})\n [Battlemetrics](https://www.battlemetrics.com/rcon/players?filter%5Bsearch%5D={ownerId}&filter%5Bservers%5D=false&filter%5BplayerFlags%5D=&sort=score&showServers=true)\n\n **Assigned By**\n{assignerName}\n{assignerId}\nTeam: {assignerTeamId}\n[Steam Profile](https://steamcommunity.com/profiles/{assignerId})\n[Battlemetrics](https://www.battlemetrics.com/rcon/players?filter%5Bsearch%5D={assignerId}&filter%5Bservers%5D=false&filter%5BplayerFlags%5D=&sort=score&showServers=true)\n\n**Assigned To**\n{assigneeName}\n{assigneeId}\nTeam: {assigneeTeamId}\n[Steam Profile](https://steamcommunity.com/profiles/{assigneeId})\n[Battlemetrics](https://www.battlemetrics.com/rcon/players?filter%5Bsearch%5D={assigneeId}&filter%5Bservers%5D=false&filter%5BplayerFlags%5D=&sort=score&showServers=true)\n\n**Teleport**\n{teleportPos}\n\n**Server:\n**{serverName}",
                ["ConsoleMessage"] = "Sleeping Bag Assigned by {assignerName}/{assignerId} - Team {assignerTeamId} to {assigneeName}/{assigneeId} - Team {assigneeTeamId}"
            }, this);
        }

        private string FormatMessage(string key, BagInfo bagInfo)
        {
            return lang.GetMessage(key, this)
                .Replace("{ownerName}", bagInfo.BagDeployer?.displayName ?? "Unknown")
                .Replace("{ownerId}", bagInfo.BagDeployer?.UserIDString ?? "Unknown")
                .Replace("{assignerName}", bagInfo.BagAssigner?.displayName ?? "Unknown")
                .Replace("{assignerId}", bagInfo.BagAssigner?.UserIDString ?? "Unknown")
                .Replace("{assignerTeamId}", bagInfo.DeployerTeamId.ToString())
                .Replace("{assigneeName}", bagInfo.BagAssignee?.displayName ?? "Unknown")
                .Replace("{assigneeId}", bagInfo.AssigneeId.ToString())
                .Replace("{assigneeTeamId}", bagInfo.AssigneeTeamId.ToString())
                .Replace("{teleportPos}", bagInfo.TeleportPos)
                .Replace("{serverName}", covalence.Server.Name);
        }

        #endregion

        #region hooks

        object CanAssignBed(BasePlayer player, SleepingBag bag, ulong targetPlayerId)
        {
            var assignerTeamId = GetPlayerTeamId(player.userID);
            var assigneeTeamId = GetPlayerTeamId(targetPlayerId);
            var bagOwner = getPlayerById(bag.OwnerID);
            var assignee = getPlayerById(targetPlayerId);

            if (IsAssignedToNonTeamMember(assignerTeamId, assigneeTeamId) && !AreClanMembers(assignerTeamId, assigneeTeamId))
            {
                var bagInfo = new BagInfo(bagOwner, player, assignee, targetPlayerId, bag, assignerTeamId, assigneeTeamId);
                if (_config.LogToDiscord)
                {
                    SendDiscordEmbed(bagInfo);
                }
                if (_config.LogToConsole)
                {
                    Puts(FormatMessage("ConsoleMessage", bagInfo));
                }
            }


            return null;
        }

        #endregion

        #region Webhook
        private void SendDiscordEmbed(BagInfo bagInfo)
        {
            var title = FormatMessage("EmbedTitle", bagInfo);
            var description = FormatMessage("EmbedBagDetailsWithBattlemetrics", bagInfo);
            var payload = new
            {
                embeds = new[]
                {
                    new
                    {
                        title,
                        description,
                        color = 9109504,
                        timestamp = DateTime.Now,
                    }
                }
            };

            var form = new WWWForm();
            form.AddField("payload_json", JsonConvert.SerializeObject(payload));
            ServerMgr.Instance.StartCoroutine(PostToDiscord(_config.DiscordWebhookUrl, form));
        }

        private IEnumerator PostToDiscord(string url, WWWForm data)
        {
            var www = UnityWebRequest.Post(url, data);
            yield return www.SendWebRequest();

            if (www.isNetworkError || www.isHttpError)
            {
                Puts($"Failed to post to discord: {www.error}");
            }
            
        }

        #endregion

        #region models

        private class BagInfo
        {
            public BasePlayer BagDeployer { get; set; }
            public BasePlayer BagAssigner { get; set; }
            public ulong AssigneeId { get; set; }
            public BasePlayer BagAssignee { get; set; }
            public SleepingBag SleepingBag { get; set; }
            public ulong DeployerTeamId { get; set; }
            public ulong AssigneeTeamId { get; set; }
            public string TeleportPos { get; set; }
            public string BagName { get; set; }

            public BagInfo(BasePlayer bagDeployer, BasePlayer assigner, BasePlayer assignee, ulong assigneeId, SleepingBag bag, ulong deployerTeamId, ulong assigneeTeamId)
            {
                BagDeployer = bagDeployer;
                BagAssigner = assigner;
                BagAssignee = assignee;
                AssigneeId = assigneeId;
                AssigneeTeamId = assigneeTeamId;
                DeployerTeamId = deployerTeamId;
                TeleportPos = "teleportpos " + Math.Round(bag.transform.position.x, 2) + "," + Math.Round(bag.transform.position.y, 2) + "," + Math.Round(bag.transform.position.z, 2) + "";
                BagName = bag.name;
            }
        }

        #endregion

        #region helpers
        private ulong GetPlayerTeamId(ulong steamId)
        {
            var teamId = default(ulong);
            var playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(steamId);

            if (playerTeam == null)
            {
                return teamId;
            }
            else
            {
                teamId = playerTeam.teamID;
            }

            return teamId;
        }

        private BasePlayer getPlayerById(ulong playerId)
        {
            var player = covalence.Players.FindPlayerById(playerId.ToString());
            if (player != null)
            {
                return (BasePlayer)(player).Object;
            }
            var sleeper = BasePlayer.FindSleeping(playerId);
            if (sleeper != null)
            {
                return sleeper;
            }
            return null;
        }

        private bool IsAssignedToNonTeamMember(ulong assigner, ulong assignee)
        {
            // both players are not in a team
            if (assigner == default(ulong) && assignee == default(ulong))
            {
                return true;
            }
            // both players are in a team return true if it's not the same team 
            if (assigner != default(ulong) && assignee != default(ulong))
            {
                return assigner != assignee;
            }
            // one player is in a team and the other is not
            return true;
        }
        
        private bool AreClanMembers(ulong playerID, ulong assignee)
        {
            if (Clans == null) 
                return false;

            string playerIdStr = playerID.ToString();
            string assigneeIdStr = assignee.ToString();

            var isSameClanMember = Clans.Call("IsClanMember", playerIdStr, assigneeIdStr);

            if (isSameClanMember != null)
                return (bool)isSameClanMember;

            var playerClan = Clans.Call("GetClanOf", playerIdStr);
            var assigneeClan = Clans.Call("GetClanOf", assigneeIdStr);

            if (playerClan == null || assigneeClan == null)
                return false;

            return (string)playerClan == (string)assigneeClan;
        }

        #endregion
    }
}
