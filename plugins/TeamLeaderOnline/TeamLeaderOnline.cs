/* MIT License

Copyright (c) 2024 PureForce

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. */

using Oxide.Core;
using System.Collections.Generic;
using ProtoBuf;
using UnityEngine;
using System.Linq;
using CompanionServer.Handlers;

namespace Oxide.Plugins
{
    [Info("Team Leader Online", "PureForce", "1.0.0")]
    [Description("Ensures the team leader position is always held by an online team member.")]
    internal class TeamLeaderOnline : RustPlugin
    {
        private Dictionary<ulong, ulong> teamPendingLeader = new Dictionary<ulong, ulong>();
        private Dictionary<ulong, ulong> teamLeaderPriority = new Dictionary<ulong, ulong>();


        #region Messages
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Admin.Promote.Fail"] = "Failed to promote user {0}.",
                ["Admin.Promote.Success"] = "User {0} now has leader priority.",
                ["Admin.Promote.Syntax"] = "Syntax: /tlo.promote <steamId>",
                ["Claim.Fail"] = "Failed to claim team.",
                ["Claim.LeaderExists"] = "Your team already has a priority leader.",
                ["Claim.Success"] = "You now have team leader priority.",
                ["Claim.Suggest"] = "Claim your team's priority leader position with <color=#AAFF00>/tlo.claim</color> this grants you leader position on login regardless of who currently has it. Only the priority leader can promote another team member to the priority leader position.",
                ["InvalidSteamId"] = "Invalid SteamId.",
                ["NoActivePlayerFound"] = "No active player found.",
                ["NoPermission"] = "You do not have permission.",
                ["NoPlayerTeam"] = "No player team found.",
            }, this);
        }

        private string GetMessage(string key, BasePlayer player = null)
        {
            string userId = player?.UserIDString;
            return lang.GetMessage(key, this, userId);
        }
        #endregion


        #region Saved Data
        private bool dataChanged = false;
        private const string FILE_LEADER_PRIORITY = "TeamLeaderOnline/LeaderPriority";

        private void SaveData()
        {
            dataChanged = false;
            WriteObject(FILE_LEADER_PRIORITY, teamLeaderPriority);
        }

        private void LoadData()
        {
            teamLeaderPriority = ReadObject<Dictionary<ulong, ulong>>(FILE_LEADER_PRIORITY);
        }

        private void OnServerSave()
        {
            if (!dataChanged)
                return;

            SaveData();
        }

        private void WriteObject<T>(string name, T value)
        {
            Interface.Oxide.DataFileSystem.WriteObject<T>(name, value);
        }

        private T ReadObject<T>(string name)
        {
            return Interface.Oxide.DataFileSystem.ReadObject<T>(name);
        }
        #endregion


        #region Permissions
        private const string PERM_ADMIN = "teamleaderonline.admin";
        private const string PERM_CLAIM = "teamleaderonline.claim";

        private bool HasAdminPermission(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, PERM_ADMIN);
        }

        private bool HasPermissionToClaim(BasePlayer player)
        {
            return permission.UserHasPermission(player.UserIDString, PERM_CLAIM);
        }
        #endregion


        #region Commands
        [ChatCommand("tlo.claim")]
        private void ChatCommand_ClaimLeader(BasePlayer player, string command, string[] args)
        {
            if (player == null)
                return;

            if (!HasPermissionToClaim(player))
            {
                PrintToChat(player, GetMessage("NoPermission", player));
                return;
            }

            if (player.Team == null)
            {
                PrintToChat(player, GetMessage("NoPlayerTeam", player));
                return;
            }

            ulong teamId = player.Team.teamID;
            if (TeamHasPriorityLeader(teamId))
            {
                PrintToChat(player, GetMessage("Claim.LeaderExists", player));
                return;
            }

            bool success = SetTeamLeader(player.Team, player.userID, true);
            string msg = success ? "Claim.Success" : "Claim.Fail";
            PrintToChat(player, GetMessage(msg, player));
        }

        [ChatCommand("tlo.promote")]
        private void ChatCommand_Promote(BasePlayer player, string command, string[] args)
        {
            if (player == null)
                return;

            if (!HasAdminPermission(player))
            {
                PrintToChat(player, GetMessage("NoPermission", player));
                return;
            }

            if (args == null || args.Length != 1)
            {
                PrintToChat(player, GetMessage("Admin.Promote.Syntax", player));
                return;
            }

            ulong targetId;
            if (!ulong.TryParse(args[0], out targetId) || !targetId.IsSteamId())
            {
                PrintToChat(player, GetMessage("InvalidSteamId", player));
                return;
            }

            BasePlayer target = BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == targetId);
            if (target == null)
            {
                PrintToChat(player, GetMessage("NoActivePlayerFound", player));
                return;
            }

            if (target.Team == null)
            {
                PrintToChat(player, GetMessage("NoPlayerTeam", player));
                return;
            }

            bool success = SetTeamLeader(target.Team, target.userID, true);
            string msg = success ? "Admin.Promote.Success" : "Admin.Promote.Fail";
            PrintToChat(player, GetMessage(msg, player), target.userID);
        }

        [ConsoleCommand("tlo.promote")]
        private void ConsoleCommand_Promote(ConsoleSystem.Arg arg)
        {
            if (arg == null)
                return;

            if (!arg.IsRcon)
            {
                BasePlayer player = arg.Player();
                if (player != null && !HasAdminPermission(player)) 
                {
                    arg.ReplyWith(GetMessage("NoPermission"));
                    return;
                }
            }

            if (arg.Args == null || arg.Args.Length != 1)
            {
                arg.ReplyWith(GetMessage("Admin.Promote.Syntax"));
                return;
            }

            ulong targetId;
            if (!ulong.TryParse(arg.Args[0], out targetId) || !targetId.IsSteamId())
            {
                arg.ReplyWith(GetMessage("InvalidSteamId"));
                return;
            }

            BasePlayer target = BasePlayer.activePlayerList.FirstOrDefault(x => x.userID == targetId);
            if (target == null)
            {
                arg.ReplyWith(GetMessage("NoActivePlayerFound"));
                return;
            }

            if (target.Team == null)
            {
                arg.ReplyWith(GetMessage("NoPlayerTeam"));
                return;
            }

            bool success = SetTeamLeader(target.Team, target.userID, true);
            string msg = success ? "Admin.Promote.Success" : "Admin.Promote.Fail";
            arg.ReplyWith(string.Format(GetMessage(msg), target.userID));
        }
        #endregion


        #region Initialization
        private void Init()
        {
            LoadData();
        }

        private void OnServerInitialized(bool initial)
        {
            permission.RegisterPermission(PERM_ADMIN, this);
            permission.RegisterPermission(PERM_CLAIM, this);

            if (ShouldWipeTeamData())
            {
                teamLeaderPriority.Clear();
                teamPendingLeader.Clear();
                SaveData();
            }

            if (teamPendingLeader.Count == 0)
            {
                Unsubscribe("OnTeamUpdated");
            }

            if (!initial)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    OnPlayerConnected(player);
                }
            }
        }
        #endregion


        #region Team-Related Hooks
        private void OnPlayerConnected(BasePlayer player)
        {
            var team = player?.Team;
            if (team == null || player.userID == team.teamLeader)
                return;

            if (!TeamHasPriorityLeader(team.teamID) && HasPermissionToClaim(player))
                PrintToChat(player, GetMessage("Claim.Suggest", player));

            if (!IsLeaderOnline(team) || HasLeaderPriority(team, player.userID))
                SetTeamLeader(team, player.userID);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            var team = player?.Team;
            if (team == null || player.userID != team.teamLeader)
                return;

            var teammate = GetEligibleOnlineTeammate(team);
            if (teammate == null)
                return;

            SetTeamLeader(team, teammate.userid);
        }

        private void OnTeamCreated(BasePlayer player, RelationshipManager.PlayerTeam team)
        {
            if (player == null || team == null)
                return;

            SetLeaderPriority(team.teamID, player.userID);
        }

        private void OnTeamDisbanded(RelationshipManager.PlayerTeam team)
        {
            if (team == null)
                return;

            teamLeaderPriority.Remove(team.teamID);
            teamPendingLeader.Remove(team.teamID);
        }

        private void OnTeamPromote(RelationshipManager.PlayerTeam team, BasePlayer candidate)
        {
            if (team == null || candidate == null)
                return;

            if (!HasLeaderPriority(team, team.teamLeader))
                return;

            teamPendingLeader[team.teamID] = candidate.userID;
            Subscribe("OnTeamUpdated");
        }

        private void OnTeamUpdated(ulong teamId, PlayerTeam team, BasePlayer player)
        {
            ulong pendingLeader;
            if (!teamPendingLeader.TryGetValue(teamId, out pendingLeader) || team.teamLeader != pendingLeader)
                return;

            SetLeaderPriority(team.teamID, team.teamLeader);
            teamPendingLeader.Remove(teamId);
            if (teamPendingLeader.Count == 0)
                Unsubscribe("OnTeamUpdated");
        }

        private object OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player, ulong target)
        {
            if (HasLeaderPriority(team, target))
                return true;

            return null;
        }

        private void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (HasLeaderPriority(team, player.userID))
                RemoveLeaderPriority(team.teamID);
        }
        #endregion


        #region Team Methods
        private bool ShouldWipeTeamData()
        {
            RelationshipManager manager = RelationshipManager.ServerInstance;
            bool noTeams = manager != null && manager.teams.Count == 0;
            return noTeams && teamLeaderPriority.Count > 0;
        }

        private bool TeamHasPriorityLeader(ulong teamId)
        {
            return teamLeaderPriority.ContainsKey(teamId);
        }

        private bool HasLeaderPriority(RelationshipManager.PlayerTeam team, ulong userId)
        {
            ulong leaderPriority;
            teamLeaderPriority.TryGetValue(team.teamID, out leaderPriority);
            return userId == leaderPriority;
        }

        private bool IsLeaderOnline(RelationshipManager.PlayerTeam team)
        {
            var members = team.GetOnlineMemberConnections();
            return members != null && members.Exists((x) => x.userid == team.teamLeader && x.connected);
        }

        private Network.Connection GetEligibleOnlineTeammate(RelationshipManager.PlayerTeam team)
        {
            var members = team.GetOnlineMemberConnections();
            if (members == null)
                return null;

            return members.Find((x) => x.userid != team.teamLeader && x.connected);
        }

        private bool SetTeamLeader(RelationshipManager.PlayerTeam team, ulong userid, bool setPriority = false)
        {
            if (!team.members.Contains(userid))
                return false;

            team.SetTeamLeader(userid);
            if (setPriority)
                SetLeaderPriority(team.teamID, userid);

            return true;
        }

        private void SetLeaderPriority(ulong teamId, ulong playerId)
        {
            teamLeaderPriority[teamId] = playerId;
            dataChanged = true;
        }

        private void RemoveLeaderPriority(ulong teamId)
        {
            teamLeaderPriority.Remove(teamId);
            dataChanged = true;
        }
        #endregion
    }
}