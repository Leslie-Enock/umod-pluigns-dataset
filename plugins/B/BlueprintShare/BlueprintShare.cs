using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Blueprint Share", "c_creep", "1.4.1")]
    [Description("Allows players to share researched blueprints with their friends, clan or team")]
    class BlueprintShare : RustPlugin
    {
        #region Fields

        [PluginReference]
        private Plugin Clans, Friends;

        private StoredData storedData;

        private enum ShareType
        {
            Teams,
            Friends,
            Clans
        }

        private const string usePermission = "blueprintshare.use";
        private const string togglePermission = "blueprintshare.toggle";
        private const string sharePermission = "blueprintshare.share";
        private const string showPermission = "blueprintshare.show";
        private const string bypassPermission = "blueprintshare.bypass";

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            LoadData();

            permission.RegisterPermission(usePermission, this);
            permission.RegisterPermission(togglePermission, this);
            permission.RegisterPermission(sharePermission, this);
            permission.RegisterPermission(showPermission, this);
            permission.RegisterPermission(bypassPermission, this);

            if (!config.TeamsEnabled)
            {
                Unsubscribe(nameof(OnTeamAcceptInvite));
                Unsubscribe(nameof(OnTeamKick));
                Unsubscribe(nameof(OnTeamLeave));
            }

            if (!config.ClansEnabled)
            {
                Unsubscribe(nameof(OnClanMemberJoined));
                Unsubscribe(nameof(OnClanMemberGone));
            }

            if (!config.FriendsEnabled)
            {
                Unsubscribe(nameof(OnFriendAdded));
                Unsubscribe(nameof(OnFriendRemoved));
            }
        }

        private void OnNewSave(string filename)
        {
            if (!config.ClearDataOnWipe) return;

            CreateData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            EnsurePlayerDataExists(player.UserIDString);
        }

        private void OnItemAction(Item item, string action, BasePlayer player)
        {
            if (!config.PhysicalSharingEnabled) return;
            if (player == null || item == null) return;
            if (action != "study") return;

            if (TryShareBlueprint(item.blueprintTargetDef, player))
            {
                item.Remove();
            }
        }

        private void OnTechTreeNodeUnlocked(Workbench workbench, TechTreeData.NodeInstance node, BasePlayer player)
        {
            if (!config.TechTreeSharingEnabled) return;
            if (workbench == null || node == null || player == null) return;

            TryShareBlueprint(node.itemDef, player);
        }

        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (!config.ShareBlueprintsOnJoin && !config.ShareBlueprintsWithNewMember) return;
            if (team == null || player == null) return;

            timer.Once(1f, () =>
            {
                var teamMemberIds = team.members;

                if (teamMemberIds.Count == 0) return;

                var teamLeader = team.GetLeader();

                if (teamLeader == null) return;

                if (config.ShareBlueprintsOnJoin)
                {
                    ShareWithPlayer(teamLeader, player);
                }

                if (config.ShareBlueprintsWithNewMember)
                {
                    var teamMembers = FindPlayersFromIds(teamMemberIds, teamLeader.userID);

                    if (teamMembers.Count == 0) return;

                    foreach (var teamMember in teamMembers)
                    {
                        if (player == null || teamMember == null) continue;

                        ShareWithPlayer(player, teamMember);
                    }
                }
            });
        }

        private void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            PlayerLeftTeam(player);
        }

        private void OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player, ulong target)
        {
            PlayerLeftTeam(RustCore.FindPlayerById(target));
        }

        #endregion

        #region External Plugins

        #region Friends

        private bool HasFriends(ulong playerId)
        {
            if (Friends == null || !Friends.IsLoaded) return false;

            var friendsList = Friends.Call<ulong[]>("GetFriends", playerId);

            return friendsList != null && friendsList.Length != 0;
        }

        private List<ulong> GetFriends(ulong playerId)
        {
            if (Friends == null || !Friends.IsLoaded) return new List<ulong>();

            var friends = Friends.Call<ulong[]>("GetFriends", playerId);

            return friends.ToList();
        }

        private bool AreFriends(ulong playerId, ulong targetId)
        {
            return Friends != null && Friends.IsLoaded && Friends.Call<bool>("AreFriends", playerId, targetId);
        }

        #endregion

        #region Clan

        private bool InClan(ulong playerId)
        {
            if (Clans == null) return false;

            var clanName = Clans?.Call<string>("GetClanOf", playerId);

            return clanName != null;
        }

        private List<ulong> GetClanMembers(ulong playerId)
        {
            var clanTag = Clans?.Call<string>("GetClanOf", playerId);

            var clanInfo = Clans?.Call<JObject>("GetClan", clanTag);

            return clanInfo["members"].ToObject<List<ulong>>();
        }

        private bool SameClan(ulong playerId, ulong targetId)
        {
            var playerClanTag = Clans?.Call<string>("GetClanOf", playerId);

            if (string.IsNullOrEmpty(playerClanTag)) return false;

            var targetClanTag = Clans?.Call<string>("GetClanOf", targetId);

            if (string.IsNullOrEmpty(targetClanTag)) return false;

            return playerClanTag == targetClanTag;
        }

        #endregion

        #region Team

        private bool InTeam(ulong playerId)
        {
            if (!RelationshipManager.TeamsEnabled()) return false;

            var playersTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerId);

            return playersTeam != null && playersTeam.members.Count > 1;
        }

        private List<ulong> GetTeamMembers(ulong playerId)
        {
            var playersTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerId);

            return playersTeam?.members;
        }

        private bool SameTeam(ulong playerId, ulong targetId)
        {
            var playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerId);

            if (playerTeam == null) return false;

            var targetTeam = RelationshipManager.ServerInstance.FindPlayersTeam(targetId);

            if (targetTeam == null) return false;

            return playerTeam == targetTeam;
        }

        #endregion

        #endregion

        #region External Plugin Hooks

        private void OnFriendAdded(string playerId, string friendId)
        {
            if (!config.ShareBlueprintsOnJoin) return;

            var player = RustCore.FindPlayerByIdString(playerId);

            if (player == null) return;

            var friend = RustCore.FindPlayerByIdString(friendId);

            if (friend == null) return;

            ShareWithPlayer(player, friend);

            if (!config.ShareBlueprintsWithNewMember) return;

            ShareWithPlayer(friend, player);
        }

        private void OnFriendRemoved(string playerId, string friendId)
        {
            if (!config.LoseBlueprintsOnLeave) return;

            var learntBlueprints = GetFriendBlueprints(playerId, friendId);

            if (learntBlueprints.Count == 0) return;

            var player = RustCore.FindPlayerByIdString(playerId);

            if (player == null) return;

            RemoveBlueprints(player, learntBlueprints, ShareType.Friends, friendId);
        }

        private void OnClanMemberJoined(ulong playerId, string clanName)
        {
            if (!config.ShareBlueprintsOnJoin) return;

            var player = RustCore.FindPlayerById(playerId);

            var clanMemberIds = GetClanMembers(playerId);

            var clanMembers = FindPlayersFromIds(clanMemberIds, playerId);

            if (clanMembers.Count == 0) return;
            
            foreach (var clanMember in clanMembers)
            {
                if (player == null || clanMember == null) continue;

                ShareWithPlayer(clanMember, player);

                if (config.ShareBlueprintsWithNewMember)
                {
                    ShareWithPlayer(player, clanMember);
                }
            }
        }

        private void OnClanMemberGone(ulong playerId, string tag)
        {
            if (!config.LoseBlueprintsOnLeave) return;

            var learntBlueprints = GetClanBlueprints(playerId.ToString());

            if (learntBlueprints.Count == 0) return;

            var player = RustCore.FindPlayerById(playerId);

            if (player == null) return;

            RemoveBlueprints(player, learntBlueprints, ShareType.Clans);
        }

        #endregion

        #region Core

        private bool TryShareBlueprint(ItemDefinition item, BasePlayer player)
        {
            if (item == null || player == null) return false;
            if (!permission.UserHasPermission(player.UserIDString, usePermission)) return false;

            if (BlueprintBlocked(item))
            {
                SendMessage("BlueprintBlocked", player, true, item.displayName.translated);

                return false;
            }

            var playerId = player.userID;

            if (SharingEnabled(player.UserIDString) && HasSocialConnections(playerId) && SomeoneWillLearnBlueprint(playerId, item))
            {
                ShareWithPlayers(player, item);
                ShareAdditionalBlueprints(player, item);

                return true;
            }

            return false;
        }

        private bool HasSocialConnections(ulong playerId)
        {
            return InTeam(playerId) || InClan(playerId) || HasFriends(playerId);
        }

        private void ShareAdditionalBlueprints(BasePlayer player, ItemDefinition item)
        {
            var additionalBlueprints = item.Blueprint.additionalUnlocks;

            if (additionalBlueprints.Count == 0) return;

            foreach (var blueprint in additionalBlueprints)
            {
                UnlockBlueprint(player, null, blueprint.itemid);
                ShareWithPlayers(player, blueprint);
            }
        }

        private bool UnlockBlueprint(BasePlayer player, BasePlayer sharer, int blueprint)
        {
            if (player == null) return false;

            var playerInfo = player.PersistantPlayerInfo;

            if (playerInfo == null) return false;
            if (playerInfo.unlockedItems.Contains(blueprint)) return false;

            playerInfo.unlockedItems.Add(blueprint);

            player.PersistantPlayerInfo = playerInfo;
            player.SendNetworkUpdateImmediate();
            player.ClientRPC(RpcTarget.Player("UnlockedBlueprint", player), blueprint);
            player.stats.Add("blueprint_studied", 1);

            PlaySoundEffect(player);

            if (config.LoseBlueprintsOnLeave && sharer != null)
            {
                AddBlueprintToDatabase(player.userID, sharer.userID, blueprint);
            }

            return true;
        }

        private int UnlockBlueprints(BasePlayer player, BasePlayer sharer, List<int> blueprints)
        {
            if (player == null) return 0;

            var playerInfo = player.PersistantPlayerInfo;

            var successfulUnlocks = 0;

            foreach (var blueprint in blueprints)
            {
                if (playerInfo.unlockedItems.Contains(blueprint)) continue;

                playerInfo.unlockedItems.Add(blueprint);
                
                player.stats.Add("blueprint_studied", 1);

                if (config.LoseBlueprintsOnLeave && sharer != null)
                {
                    AddBlueprintToDatabase(player.userID, sharer.userID, blueprint);
                }

                successfulUnlocks++;
            }

            if (successfulUnlocks > 0)
            {
                PlaySoundEffect(player);

                player.PersistantPlayerInfo = playerInfo;
                player.SendNetworkUpdateImmediate();
                player.ClientRPC(RpcTarget.Player("UnlockedBlueprint", player), 0);

                if (config.LoseBlueprintsOnLeave)
                {
                    SaveData();
                }
            }

            return successfulUnlocks;
        }

        private void ShareWithPlayers(BasePlayer player, ItemDefinition item)
        {
            if (player == null || item == null || !SharingEnabled(player.UserIDString)) return;

            var successfulUnlocks = 0;

            foreach (var target in GetPlayersToShareWith(player.userID))
            {
                if (target == null || !SharingEnabled(target.UserIDString)) return;

                if (UnlockBlueprint(target, player, item.itemid))
                {
                    successfulUnlocks++;

                    if (config.ReceiveMessagesEnabled)
                    {
                        SendMessage("TargetLearntBlueprint", target, true, player.displayName, item.displayName.translated);
                    }
                }
            }

            if (successfulUnlocks > 0)
            {
                if (config.ShareMessagesEnabled)
                {
                    SendMessage("PlayerSharedBlueprint", player, true, item.displayName.translated, successfulUnlocks);
                }

                if (config.LoseBlueprintsOnLeave)
                {
                    SaveData();
                }
            }
        }

        private void ShareWithPlayer(BasePlayer player, BasePlayer target)
        {
            if (player == null || target == null) return;

            var playerId = player.userID;
            var targetId = target.userID;

            if (!SharingEnabled(target.UserIDString))
            {
                SendMessage("TargetSharingDisabled", player, true, target.displayName);

                return;
            }

            if (!SameTeam(playerId, targetId) && !SameClan(playerId, targetId) && !AreFriends(playerId, targetId) && !permission.UserHasPermission(player.UserIDString, bypassPermission))
            {
                SendMessage("CannotShare", player, true);

                return;
            }

            var filteredBlueprints = RemoveBlockedBlueprints(player.PersistantPlayerInfo.unlockedItems);

            if (filteredBlueprints.Count == 0)
            {
                SendMessage("NoBlueprintsToShare", player, true, target.displayName);

                return;
            }

            var learntBlueprints = UnlockBlueprints(target, player, filteredBlueprints);

            if (learntBlueprints > 0)
            {
                if (config.ShareMessagesEnabled)
                {
                    SendMessage("PlayerSharedBlueprints", player, true, learntBlueprints, target.displayName);
                }
                
                if (config.ReceiveMessagesEnabled)
                {
                    SendMessage("TargetLearntBlueprints", target, true, player.displayName, learntBlueprints);
                }
            }
            else
            {
                SendMessage("NoBlueprintsToShare", player, true, target.displayName);
            }
        }

        private List<int> RemoveBlockedBlueprints(List<int> blueprints)
        {
            return blueprints.Where(blueprint =>
            {
                var item = ItemManager.FindItemDefinition(blueprint);
                return item != null && !BlueprintBlocked(item);
            }).ToList();
        }

        private bool SomeoneWillLearnBlueprint(ulong playerId, ItemDefinition item)
        {
            if (item == null) return false;

            var targets = GetPlayersToShareWith(playerId);

            if (targets.Count == 0) return false;

            var countUnlocked = targets.Count(target => target != null && !target.blueprints.HasUnlocked(item));

            return countUnlocked > 0;
        }

        private List<BasePlayer> GetPlayersToShareWith(ulong playerId)
        {
            var ids = new HashSet<ulong>();

            if (config.ClansEnabled && Clans != null && InClan(playerId))
            {
                ids.UnionWith(GetClanMembers(playerId));
            }

            if (config.FriendsEnabled && HasFriends(playerId))
            {
                ids.UnionWith(GetFriends(playerId));
            }

            if (config.TeamsEnabled && InTeam(playerId))
            {
                ids.UnionWith(GetTeamMembers(playerId));
            }

            return FindPlayersFromIds(ids.ToList(), playerId);
        }

        private void AddBlueprintToDatabase(ulong playerId, ulong sharerId, int blueprint)
        {
            if (config.TeamsEnabled && SameTeam(playerId, sharerId))
            {
                AddTeamShare(playerId.ToString(), blueprint);
            }

            if (config.ClansEnabled && SameClan(playerId, sharerId))
            {
                AddClanShare(playerId.ToString(), blueprint);
            }

            if (config.FriendsEnabled && AreFriends(playerId, sharerId))
            {
                AddFriendShare(playerId.ToString(), sharerId.ToString(), blueprint);
            }
        }

        private void RemoveBlueprintsFromDatabase(ShareType type, string playerId, string friendId)
        {
            switch (type)
            {
                case ShareType.Teams:
                    {
                        RemoveTeamShare(playerId);

                        break;
                    }
                case ShareType.Clans:
                    {
                        RemoveClanShare(playerId);

                        break;
                    }
                case ShareType.Friends:
                    {
                        RemoveFriendShare(playerId, friendId);

                        break;
                    }
                default:
                    {
                        throw new ArgumentException(nameof(type));
                    }
            }

            SaveData();
        }

        private void RemoveBlueprints(BasePlayer player, List<int> blueprints, ShareType type, string friendId = "")
        {
            if (player == null || blueprints.Count == 0) return;

            var playerInfo = player.PersistantPlayerInfo;
            var blueprintsRemoved = 0;

            foreach (var blueprint in blueprints)
            {
                if (!playerInfo.unlockedItems.Contains(blueprint)) continue;

                playerInfo.unlockedItems.Remove(blueprint);

                blueprintsRemoved++;
            }

            if (blueprintsRemoved == 0) return;

            player.PersistantPlayerInfo = playerInfo;
            player.SendNetworkUpdateImmediate();
            player.ClientRPC(RpcTarget.Player("UnlockedBlueprint", player), 0);

            RemoveBlueprintsFromDatabase(type, player.UserIDString, friendId);
            SendMessage("BlueprintsRemoved", player, true, blueprintsRemoved);
        }

        private void PlayerLeftTeam(BasePlayer player)
        {
            if (player == null || !config.LoseBlueprintsOnLeave) return;

            var learntBlueprints = GetTeamBlueprints(player.UserIDString);

            if (learntBlueprints.Count == 0) return;

            RemoveBlueprints(player, learntBlueprints, ShareType.Teams);
        }

        #endregion

        #region Utility

        private void PlaySoundEffect(BasePlayer player)
        {
            if (player == null) return;

            var soundEffect = new Effect("assets/prefabs/deployable/research table/effects/research-success.prefab", player.transform.position, Vector3.zero);

            if (soundEffect == null) return;

            EffectNetwork.Send(soundEffect, player.net.connection);
        }

        private List<BasePlayer> FindPlayersFromIds(List<ulong> ids, ulong playerId) => ids.Where(id => id != playerId).Select(id => RustCore.FindPlayerById(id)).Where(target => target != null).Distinct().ToList();
        
        private bool BlueprintBlocked(ItemDefinition item) => config.BlockedItems.Contains(item.shortname);

        private HashSet<int> GetSharedBlueprints(string playerId, ShareType type, string friendId)
        {
            var learntBlueprints = new HashSet<int>();

            switch (type)
            {
                case ShareType.Teams:
                    {
                        learntBlueprints.UnionWith(GetTeamBlueprints(playerId));

                        break;
                    }

                case ShareType.Clans:
                    {
                        learntBlueprints.UnionWith(GetClanBlueprints(playerId));

                        break;
                    }

                case ShareType.Friends:
                    {
                        learntBlueprints.UnionWith(GetFriendBlueprints(playerId, friendId));

                        break;
                    }
            }

            return learntBlueprints;
        }

        private void SortBlueprintsByWorkbenchLevel(HashSet<int> learntBlueprints, ref Dictionary<int, List<string>> workbenchTiers)
        {
            foreach (var blueprint in learntBlueprints)
            {
                var blueprintItem = ItemManager.FindItemDefinition(blueprint);

                if (blueprintItem == null) continue;

                var blueprintLevel = blueprintItem.Blueprint.workbenchLevelRequired;

                if (!workbenchTiers.ContainsKey(blueprintLevel)) continue;

                workbenchTiers[blueprintLevel].Add(blueprintItem.displayName.translated);
            }
        }

        private void DisplayLearntBlueprints(BasePlayer player, ShareType type, string friendId = "")
        {
            var playerId = player.UserIDString;
            var sharedBlueprints = GetSharedBlueprints(playerId, type, friendId);

            if (sharedBlueprints.Count == 0)
            {
                SendMessage("NoSharedBlueprints", player, true);
                return;
            }

            var workbenchTiers = new Dictionary<int, List<string>>();
            var availableTiers = sharedBlueprints.Select(bp => GetWorkbenchTierForBlueprint(bp)).Distinct();

            foreach (var tier in availableTiers)
            {
                workbenchTiers[tier] = Pool.Get<List<string>>();
            }

            SortBlueprintsByWorkbenchLevel(sharedBlueprints, ref workbenchTiers);

            var sb = new StringBuilder();

            foreach (var kvp in workbenchTiers.OrderBy(x => x.Key))
            {
                var tier = kvp.Key;
                var blueprints = kvp.Value;

                if (blueprints.Count > 0)
                {
                    sb.Append(GetLangValue($"ShowWorkBench{tier}Blueprints", playerId, string.Join(", ", blueprints)));
                    sb.AppendLine();
                }
            }

            player.ChatMessage(sb.ToString());

            SendMessage("ShowTotalBlueprintsShared", player, true, workbenchTiers.Values.Sum(tier => tier.Count));

            for (var i = 0; i < workbenchTiers.Count; i++)
            {
                var tierExists = workbenchTiers.TryGetValue(i, out var workbenchTier);

                if (tierExists)
                {
                    Pool.FreeUnmanaged(ref workbenchTier);
                }
            }
        }

        private int GetWorkbenchTierForBlueprint(int itemId)
        {
            var item = ItemManager.FindItemDefinition(itemId);

            return item != null ? item.Blueprint.workbenchLevelRequired : 0;
        }

        #endregion

        #region Chat Commands

        [ChatCommand("bs")]
        private void ChatCommands(BasePlayer player, string command, string[] args)
        {
            var playerId = player.UserIDString;

            if (args.Length == 0)
            {
                SendMessage("Help", player, false);

                return;
            }

            switch (args[0].ToLower())
            {
                case "help":
                    {
                        SendMessage("Help", player, false);

                        break;
                    }

                case "toggle":
                    {
                        ToggleCommand(player, playerId);

                        break;
                    }

                case "share":
                    {
                        ShareCommand(player, playerId, args);

                        break;
                    }

                case "show":
                    {
                        ShowCommand(player, playerId, args);

                        break;
                    }

                default:
                    {
                        SendMessage("ArgumentsError", player, true);

                        break;
                    }
            }
        }

        private void ToggleCommand(BasePlayer player, string playerId)
        {
            if (!permission.UserHasPermission(playerId, togglePermission))
            {
                SendMessage("NoPermission", player, true);

                return;
            }

            EnsurePlayerDataExists(playerId);

            SendMessage(SharingEnabled(playerId) ? "ToggleOff" : "ToggleOn", player, true);

            storedData.Players[playerId].SharingEnabled = !storedData.Players[playerId].SharingEnabled;

            SaveData();
        }

        private void ShareCommand(BasePlayer player, string playerId, string[] args)
        {
            if (!permission.UserHasPermission(playerId, sharePermission))
            {
                SendMessage("NoPermission", player, true);

                return;
            }

            if (args.Length != 2)
            {
                SendMessage("NoTarget", player, true);

                return;
            }

            var target = RustCore.FindPlayerByName(args[1]);

            if (target == null)
            {
                SendMessage("PlayerNotFound", player, true);

                return;
            }

            if (target == player)
            {
                SendMessage("TargetEqualsPlayer", player, true);

                return;
            }

            ShareWithPlayer(player, target);
        }

        private void ShowCommand(BasePlayer player, string playerId, string[] args)
        {
            if (!config.LoseBlueprintsOnLeave)
            {
                SendMessage("LoseBlueprintsDisabled", player, true);

                return;
            }

            if (!permission.UserHasPermission(playerId, showPermission))
            {
                SendMessage("NoPermission", player, true);

                return;
            }

            if (args.Length < 2)
            {
                SendMessage("ShowMissingArgument", player, true);

                return;
            }

            switch (args[1])
            {
                case "clan":
                    {
                        DisplayLearntBlueprints(player, ShareType.Clans);

                        break;
                    }

                case "team":
                    {
                        DisplayLearntBlueprints(player, ShareType.Teams);

                        break;
                    }

                case "friend":
                    {
                        if (args.Length != 3)
                        {
                            SendMessage("ShowFriendArgumentMissing", player, true);

                            return;
                        }

                        var friend = RustCore.FindPlayerByName(args[2]);

                        if (friend == null)
                        {
                            SendMessage("PlayerNotFound", player, true);

                            return;
                        }

                        if (friend == player)
                        {
                            SendMessage("FriendEqualsPlayer", player, true);

                            return;
                        }

                        if (!AreFriends(friend.userID, player.userID))
                        {
                            SendMessage("NotFriends", player, true);

                            return;
                        }

                        DisplayLearntBlueprints(player, ShareType.Friends, friend.UserIDString);

                        break;
                    }

                default:
                    {
                        SendMessage("ShowMissingArgument", player, true);

                        return;
                    }
            }
        }

        #endregion

        #region Configuration File

        private Configuration config;

        private class Configuration
        {
            [JsonProperty("Teams Sharing Enabled")]
            public bool TeamsEnabled = true;

            [JsonProperty("Clans Sharing Enabled")]
            public bool ClansEnabled = true;

            [JsonProperty("Friends Sharing Enabled")]
            public bool FriendsEnabled = true;

            [JsonProperty("Share Blueprint Items")]
            public bool PhysicalSharingEnabled = true;

            [JsonProperty("Share Tech Tree Blueprints")]
            public bool TechTreeSharingEnabled = true;

            [JsonProperty("Share Blueprints on Join")]
            public bool ShareBlueprintsOnJoin;

            [JsonProperty("Share Blueprints with New Member or Friend")]
            public bool ShareBlueprintsWithNewMember;

            [JsonProperty("Lose Blueprints on Leave")]
            public bool LoseBlueprintsOnLeave;

            [JsonProperty("Clear Data File on Wipe")]
            public bool ClearDataOnWipe = true;

            [JsonProperty("Receive Messages Enabled")]
            public bool ReceiveMessagesEnabled = true;

            [JsonProperty("Share Messages Enabled")]
            public bool ShareMessagesEnabled = true;

            [JsonProperty("Items Blocked from Sharing")]
            public List<string> BlockedItems = new List<string>();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating new configuration file");

            config = new Configuration();
        }

        protected override void SaveConfig()
        {
            PrintWarning("Configuration file has been saved");

            Config.WriteObject(config, true);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<Configuration>();

                if (config == null)
                {
                    throw new Exception();
                }
            }
            catch
            {
                PrintError($"An error occurred while parsing the configuration file {Name}.json; Resetting configuration to default values.");
                LoadDefaultConfig();
            }
        }

        #endregion

        #region Data File

        private class StoredData
        {
            public Dictionary<string, PlayerData> Players = new Dictionary<string, PlayerData>();
        }

        private class PlayerData
        {
            public bool SharingEnabled;

            public ShareData LearntBlueprints;
        }

        private class ShareData
        {
            public List<int> Team = new List<int>();
            public List<int> Clan = new List<int>();
            public Dictionary<string, List<int>> Friends = new Dictionary<string, List<int>>();
        }

        private void AddClanShare(string playerId, int blueprint)
        {
            EnsurePlayerDataExists(playerId);

            if (storedData.Players[playerId].LearntBlueprints.Clan.Contains(blueprint)) return;

            storedData.Players[playerId].LearntBlueprints.Clan.Add(blueprint);
        }

        private void AddTeamShare(string playerId, int blueprint)
        {
            EnsurePlayerDataExists(playerId);

            if (storedData.Players[playerId].LearntBlueprints.Team.Contains(blueprint)) return;

            storedData.Players[playerId].LearntBlueprints.Team.Add(blueprint);
        }

        private void AddFriendShare(string playerId, string friendId, int blueprint)
        {
            EnsurePlayerDataExists(playerId);

            if (!storedData.Players[playerId].LearntBlueprints.Friends.ContainsKey(friendId))
            {
                storedData.Players[playerId].LearntBlueprints.Friends.Add(friendId, new List<int>());
            }

            if (storedData.Players[playerId].LearntBlueprints.Friends[friendId].Contains(blueprint)) return;

            storedData.Players[playerId].LearntBlueprints.Friends[friendId].Add(blueprint);
        }

        private void RemoveClanShare(string playerId)
        {
            EnsurePlayerDataExists(playerId);

            storedData.Players[playerId].LearntBlueprints.Clan.Clear();
        }

        private void RemoveTeamShare(string playerId)
        {
            EnsurePlayerDataExists(playerId);

            storedData.Players[playerId].LearntBlueprints.Team.Clear();
        }

        private void RemoveFriendShare(string playerId, string friendId)
        {
            EnsurePlayerDataExists(playerId);

            if (!storedData.Players[playerId].LearntBlueprints.Friends.ContainsKey(friendId)) return;

            storedData.Players[playerId].LearntBlueprints.Friends.Remove(friendId);
        }

        private List<int> GetClanBlueprints(string playerId)
        {
            EnsurePlayerDataExists(playerId);

            return storedData.Players[playerId].LearntBlueprints.Clan.ToList();
        }

        private List<int> GetTeamBlueprints(string playerId)
        {
            EnsurePlayerDataExists(playerId);

            return storedData.Players[playerId].LearntBlueprints.Team.ToList();
        }

        private List<int> GetFriendBlueprints(string playerId, string friendId)
        {
            EnsurePlayerDataExists(playerId);

            return storedData.Players[playerId].LearntBlueprints.Friends[friendId].ToList();
        }

        private void CreateData()
        {
            storedData = new StoredData();

            SaveData();
        }

        private void LoadData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile(Name))
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            else
            {
                CreateData();
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);

        private void EnsureDataFileExists()
        {
            if (storedData == null)
            {
                CreateData();
            }
        }

        private void EnsurePlayerDataExists(string playerId)
        {
            EnsureDataFileExists();

            if (!storedData.Players.ContainsKey(playerId))
            {
                CreatePlayerData(playerId);
            }
        }

        private void CreatePlayerData(string playerId)
        {
            storedData.Players.Add(playerId, new PlayerData
            {
                SharingEnabled = true,
                LearntBlueprints = new ShareData()
            });

            SaveData();
        }

        #endregion

        #region Localization File

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Prefix"] = "<color=#D85540>[Blueprint Share] </color>",
                ["ArgumentsError"] = "Error, incorrect arguments. Try /bs help.",
                ["Help"] = "<color=#D85540>Blueprint Share Help:</color>\n\n<color=#D85540>/bs toggle</color> - Toggles the sharing of blueprints.\n<color=#D85540>/bs share <player></color> - Shares your blueprints with other player.\n<color=#D85540>/bs show <type></color> - Displays blueprints that have been shared to you.",
                ["ToggleOn"] = "You have <color=#00ff00>enabled</color> blueprint sharing.",
                ["ToggleOff"] = "You have <color=#ff0000>disabled</color> blueprint sharing.",
                ["NoPermission"] = "You don't have permission to use this command!",
                ["CannotShare"] = "You cannot share blueprints with this player because they aren't a friend or in the same clan or team!",
                ["NoTarget"] = "You didn't specify a player to share with!",
                ["TargetEqualsPlayer"] = "You cannot share blueprints with yourself!",
                ["PlayerNotFound"] = "Could not find a player with that name!",
                ["PlayerSharedBlueprints"] = "You shared <color=#ffff00>{0}</color> blueprint(s) with <color=#ffff00>{1}</color>.",
                ["TargetLearntBlueprints"] = "<color=#ffff00>{0}</color> has shared <color=#ffff00>{1}</color> blueprint(s) with you.",
                ["NoBlueprintsToShare"] = "You don't have any new blueprints to share with <color=#ffff00>{0}</color>.",
                ["PlayerSharedBlueprint"] = "You have learned the <color=#ffff00>{0}</color> blueprint and have shared it with <color=#ffff00>{1}</color> player(s)!",
                ["TargetLearntBlueprint"] = "<color=#ffff00>{0}</color> has shared the <color=#ffff00>{1}</color> blueprint with you!",
                ["BlueprintBlocked"] = "The server administrator has blocked the <color=#ffff00>{0}</color> blueprint from being shared but you will still learn the blueprint.",
                ["TargetSharingDisabled"] = "Unable to share blueprints with <color=#ffff00>{0}</color> because they have disabled their sharing.",
                ["BlueprintsRemoved"] = "You have lost access to <color=#ffff00>{0}</color> blueprint(s).",
                ["ShowMissingArgument"] = "You didn't specify which learnt blueprints you want to view! Please choose from the following options: clan, team or friend",
                ["ShowFriendArgumentMissing"] = "You didn't specify a friend!",
                ["FriendEqualsPlayer"] = "You cannot be friends with yourself!",
                ["NotFriends"] = "You are not friends with this player!",
                ["NoSharedBlueprints"] = "No blueprints have been shared with you!",
                ["ShowWorkBench0Blueprints"] = "{0}",
                ["ShowWorkBench1Blueprints"] = "{0}",
                ["ShowWorkBench2Blueprints"] = "<color=#ADD8E6>{0}</color>",
                ["ShowWorkBench3Blueprints"] = "<color=#90EE90>{0}</color>",
                ["ShowTotalBlueprintsShared"] = "In total you have been shared <color=#ffff00>{0}</color> blueprint(s).",
                ["LoseBlueprintsDisabled"] = "This feature has been disabled by the server administrator."
            }, this);
        }

        private string GetLangValue(string key, string id = null, params object[] args)
        {
            var msg = lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(msg, args) : msg;
        }

        private void SendMessage(string key, BasePlayer player, bool prefix, params object[] args)
        {
            if (player == null || string.IsNullOrEmpty(key)) return;

            var sb = new StringBuilder(GetLangValue(key, player.UserIDString, args));

            if (prefix)
            {
                sb.Insert(0, GetLangValue("Prefix", player.UserIDString, args));
            }

            player.ChatMessage(sb.ToString());
        }

        #endregion

        #region API

        private bool SharingEnabled(string playerId)
        {
            EnsurePlayerDataExists(playerId);

            return storedData.Players[playerId].SharingEnabled;
        }

        #endregion
    }
}