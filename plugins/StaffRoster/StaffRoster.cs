using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Staff Roster", "Mr. Blue", "2.0.1")]
    [Description("Shows staff roster and availability")]

    class StaffRoster : CovalencePlugin
    {
        #region Variables
        private Dictionary<IPlayer, StaffMember> staffMembers = new Dictionary<IPlayer, StaffMember>();
        private PluginConfig config = null;
        #endregion

        #region Config
        private void Init()
        {
            config = Config.ReadObject<PluginConfig>();
        }

        protected override void LoadDefaultConfig()
        {
            config = new PluginConfig()
            {
                staffStatuses = new List<StaffStatus>()
                {
                    new StaffStatus("Available", "available", "green"),
                    new StaffStatus("Off-Duty", "od", "#FF0000"),
                    new StaffStatus("Busy", "busy", "yellow"),
                    new StaffStatus("Afk", "afk", "orange")
                },
                staffGroups = new List<StaffGroup>()
                {
                    new StaffGroup("admin", 1, "<color=red>Admin</color>"),
                    new StaffGroup("mod", 2, "<color=orange>Mod</color>"),
                    new StaffGroup("helper", 3, "<color=yellow>Helper</color>")
                }
            };
            Config.WriteObject(config, true);
        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                {"StaffList","<color=orange>StaffRoster</color>: Current Staff online are:"},
                {"StaffEntry","{Title} <color=orange>{Player}</color> Status: {Status}"},
                {"NoStaff","<color=orange>StaffRoster</color>: Current there is no staff online."},
                {"NoPermission","<color=orange>StaffRoster</color>: Only staff members are able to use this command."},
                {"StatusChanged","<color=orange>StaffRoster</color>: {Title} {Player} has changed their status to: {Status}"},
                {"SameStatus","<color=orange>StaffRoster</color>: You already have that status."},
                {"StatusInvalid","<color=orange>StaffRoster</color>: Invalid status, available statuses:\n{Statuses}"}
            };
            lang.RegisterMessages(messages, this);
        }

        string Msg(string msg, string SteamId = null) => lang.GetMessage(msg, this, SteamId);
        #endregion

        #region Classes
        class PluginConfig
        {
            [JsonProperty("Statuses")]
            public List<StaffStatus> staffStatuses = new List<StaffStatus>();
            [JsonProperty("Groups")]
            public List<StaffGroup> staffGroups = new List<StaffGroup>();
        }

        class StaffMember
        {
            public StaffGroup Group;
            public StaffStatus Status;

            public StaffMember(StaffGroup group, StaffStatus status)
            {
                Group = group;
                Status = status;
            }
        }

        class StaffGroup
        {
            [JsonProperty("Group (Oxide group name)")]
            public string GroupName;
            public int Priority;
            public string Title;
            
            public StaffGroup(string groupName, int priority, string title)
            {
                GroupName = groupName;
                Priority = priority;
                Title = title;
            }
        }

        class StaffStatus
        {
            public string Title;
            public string Command;
            public string Color;

            public StaffStatus(string title, string command, string color)
            {
                Title = title;
                Command = command;
                Color = color;
            }
        }
        #endregion

        #region Helpers
        private StaffGroup GetStaffGroup(IPlayer player)
        {
            StaffGroup selectedStaffGroup = null;
            foreach (StaffGroup staffGroup in config.staffGroups)
                if (permission.UserHasGroup(player.Id, staffGroup.GroupName) && (selectedStaffGroup == null || selectedStaffGroup.Priority > staffGroup.Priority))
                    selectedStaffGroup = staffGroup;
            
            return selectedStaffGroup;
        }
        #endregion

        #region Hooks
        void OnServerInitialized()
        {
            foreach (IPlayer p in players.Connected)
            {
                StaffGroup staffGroup = GetStaffGroup(p);
                if (staffGroup != null)
                    staffMembers.Add(p, new StaffMember(staffGroup, config.staffStatuses.First()));
            }
        }

        void OnUserConnected(IPlayer player)
        {
            StaffGroup staffGroup = GetStaffGroup(player);
            if (staffGroup != null)
                staffMembers.Add(player, new StaffMember(staffGroup, config.staffStatuses.First()));
        }

        void OnUserDisconnected(IPlayer player)
        {
            if (staffMembers.ContainsKey(player))
                staffMembers.Remove(player);
        }
        #endregion

        #region Commands
        [Command("staff")]
        private void StaffCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                if (staffMembers.Count == 0)
                {
                    player.Reply(Msg("NoStaff", player.Id));
                    return;
                }

                player.Reply(Msg("StaffList", player.Id));

                IOrderedEnumerable<KeyValuePair<IPlayer, StaffMember>> sortedStaff = staffMembers.OrderBy(o => o.Value.Group.Priority);

                foreach (KeyValuePair<IPlayer, StaffMember> staffMember in sortedStaff)
                {
                    player.Reply(Msg("StaffEntry", player.Id)
                        .Replace("{Title}", staffMember.Value.Group.Title)
                        .Replace("{Player}", staffMember.Key.Name)
                        .Replace("{Status}", $"<color={staffMember.Value.Status.Color}>{staffMember.Value.Status.Title}</color>"));    
                }
            }
            if (args.Length == 1)
            {
                IEnumerable<StaffStatus> newStatuses = config.staffStatuses.Where(s => s.Command.ToLower() == args[0].ToLower());
                if (newStatuses == null || newStatuses.Count() < 1)
                {
                    List<string> Statuses = new List<string>();

                    foreach (StaffStatus staffStatus in config.staffStatuses)
                        Statuses.Add($"<color={staffStatus.Color}>{staffStatus.Title}</color> ({staffStatus.Command})");

                    string statuses = string.Join(", ", Statuses.ToArray());

                    player.Reply(Msg("StatusInvalid", player.Id)
                        .Replace("{Statuses}", statuses));
                    return;
                }

                StaffStatus newStatus = newStatuses.First();
                StaffMember staffMember = staffMembers[player];
                if (newStatus == staffMember.Status)
                {
                    player.Reply(Msg("SameStatus", player.Id));
                    return;
                }

                staffMember.Status = newStatus;

                string statusString = $"<color={newStatus.Color}>{newStatus.Title}</color>";
                foreach (IPlayer p in players.Connected)
                {
                    p.Message(Msg("StatusChanged", p.Id)
                        .Replace("{Title}", staffMember.Group.Title)
                        .Replace("{Player}", player.Name)
                        .Replace("{Status}", statusString));
                }
                return;
            }
        }
        #endregion
    }
}