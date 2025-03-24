using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Backpack Upgrader", "waayne", "1.0.2")]
    [Description("Allows players to upgrade their backpacks.")]
    internal class BackpackUpgrader : CovalencePlugin
    {
        private const string UPGRADE_PERMISSION = "backpackupgrader.upgrade";
        private const string SET_PERMISSION = "backpackupgrader.set";
        private const string BACKPACKS_PERMISSION = "backpacks.use";
        
        private const string UPGRADE_COMMAND = "bpupgrade";
        private const string SET_COMMAND = "bpset";
        
        private const int BACKPACKS_ROWS = 7;

        [PluginReference] private Plugin Backpacks;

        private void Init()
        {
            permission.RegisterPermission(UPGRADE_PERMISSION, this);
            permission.RegisterPermission(SET_PERMISSION, this);

            AddCovalenceCommand(UPGRADE_PERMISSION, nameof(UpgradeCommand), UPGRADE_PERMISSION);
            AddCovalenceCommand(SET_PERMISSION, nameof(SetCommand), SET_PERMISSION);

            AddCovalenceCommand(UPGRADE_COMMAND, nameof(UpgradeCommand), UPGRADE_PERMISSION);
            AddCovalenceCommand(SET_COMMAND, nameof(SetCommand), SET_PERMISSION);
        }

        private void OnServerInitialized(bool initial)
        {
            if (Backpacks == null || !Backpacks.IsLoaded)
                LogError(lang.GetMessage("BackpacksNotFound", this));
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BackpacksNotFound"] = "Backpacks is not loaded, get it at https://umod.org/plugins/backpacks",
                ["HelpSet"] = "Use backpackupgrader.set <name/id> <rows> to set a player's backpack to x rows.",
                ["HelpUpgrade"] = "Use backpackupgrader.upgrade <name/id> to upgrade a player's backpack.",
                ["Set"] = "Set {0}'s backpack to {1}x rows.",
                ["Upgraded"] = "Upgraded {0}'s backpack to {1}x rows."
            }, this);
        }

        private void OnNewSave(string filename)
        {
            foreach (IPlayer player in players.All)
            {
                for (var row = 1; row <= BACKPACKS_ROWS; row++)
                {
                    permission.RevokeUserPermission(player.Id, GetPermissionFromLevel(row));
                }
            }
        }

        private void UpgradeCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length <= 0)
            {
                player.Reply(lang.GetMessage("HelpUpgrade", this, player.Id));
                return;
            }

            IPlayer target = players.FindPlayer(args[0]);

            if (target == null)
                return;

            for (var row = 1; row <= BACKPACKS_ROWS; row++)
            {
                string perm = GetPermissionFromLevel(row);
                if (!target.HasPermission(perm))
                {
                    target.GrantPermission(perm);
                    player.Reply(string.Format(lang.GetMessage("Upgraded", this, player.Id), target.Name, row));
                    return;
                }
            }
        }

        private void SetCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length <= 1)
            {
                player.Reply(lang.GetMessage("HelpSet", this, player.Id));
                return;
            }

            IPlayer target = players.FindPlayer(args[0]);

            if (target == null)
                return;

            int newRows = int.Parse(args[1]);

            for (var row = 1; row <= BACKPACKS_ROWS; row++)
            {
                string perm = GetPermissionFromLevel(row);
                if (row <= newRows)
                {
                    if (!target.HasPermission(perm))
                        target.GrantPermission(perm);
                }
                else
                {
                    if (target.HasPermission(perm))
                        target.RevokePermission(perm);
                }
            }

            player.Reply(string.Format(lang.GetMessage("Set", this, player.Id), target.Name, newRows));
        }

        private static string GetPermissionFromLevel(int row)
        {
            return row == 1 ? BACKPACKS_PERMISSION : BACKPACKS_PERMISSION + "." + row;
        }
    }
}