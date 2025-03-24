using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("Group Migrate", "Wulf", "1.0.0")]
    [Description("Migrate players and permissions between groups")]
    class GroupMigrate : CovalencePlugin
    {
        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CommandMigrate"] = "migrate",
                ["GroupCreated"] = "Created a new group named '{0}'",
                ["GroupNotFound"] = "Could not find a group named '{0}'",
                ["GroupRemoved"] = "Removed the old group named '{0}'",
                ["MigratedPlayers"] = "Migrated {0} players from '{1}' to '{2}'",
                ["MigratedPerms"] = "Migrated {0} permissions from '{1}' to '{2}': {3}",
                ["NotAllowed"] = "You are not allowed to use the '{0}' command",
                ["UsageMigrate"] = "Usage: {0} <oldgroup> <newgroup>",
            }, this);
        }

        #endregion Localization

        #region Initialization

        private const string permUse = "groupmigrate.use";

        private void Init()
        {
            AddLocalizedCommand(nameof(CommandMigrate));

            permission.RegisterPermission(permUse, this);
        }

        #endregion Initialization

        #region Commands

        private void CommandMigrate(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permUse))
            {
                Message(player, "NotAllowed", command);
                return;
            }

            if (args.Length != 2)
            {
                Message(player, "UsageMigrate", command);
                return;
            }

            string oldGroup = args[0].ToLower();
            string newGroup = args[1].ToLower();

            // Check if old group exists or not
            if (!permission.GroupExists(oldGroup))
            {
                Message(player, "GroupNotFound", oldGroup);
                return;
            }

            // Check if new group exists or not
            if (!permission.GroupExists(newGroup))
            {
                permission.CreateGroup(newGroup, newGroup, 0);
                Message(player, "GroupCreated", newGroup);
            }

            // Migrate players from old group to new
            int targetCount = 0;
            foreach (IPlayer target in players.All)
            {
                if (target.BelongsToGroup(oldGroup) && !target.BelongsToGroup(newGroup))
                {
                    target.AddToGroup(newGroup);
                    target.RemoveFromGroup(oldGroup);
                    targetCount++;
                }
            }
            Message(player, "MigratedPlayers", targetCount, oldGroup, newGroup);

            // Migrate permissions from old group to new
            string[] oldPerms = permission.GetGroupPermissions(oldGroup);
            foreach (string perm in oldPerms)
            {
                permission.GrantGroupPermission(newGroup, perm, null);
                permission.RevokeGroupPermission(oldGroup, perm);
            }
            Message(player, "MigratedPerms", oldPerms.Length, oldGroup, newGroup, string.Join(", ", oldPerms));

            // Remove group if it is empty and not a default group
            if (permission.GetUsersInGroup(oldGroup).Length == 0 && !Interface.Oxide.Config.Options.DefaultGroups.Contains(oldGroup))
            {
                permission.RemoveGroup(oldGroup);
                Message(player, "GroupRemoved", oldGroup);
            }

            // Save group and player data
            permission.SaveData();
        }

        #endregion Commands

        #region Helpers

        private void AddLocalizedCommand(string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages)
                {
                    if (message.Key.Equals(command))
                    {
                        if (!string.IsNullOrEmpty(message.Value))
                        {
                            AddCovalenceCommand(message.Value, command);
                        }
                    }
                }
            }
        }

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }

        private void Message(IPlayer player, string langKey, params object[] args)
        {
            if (player.IsConnected)
            {
                player.Message(GetLang(langKey, player.Id, args));
            }
        }

        #endregion Helpers
    }
}
