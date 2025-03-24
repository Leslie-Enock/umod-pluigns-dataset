using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Oxide.Plugins
{
    [Info("Loot Protection", "Wulf", "1.0.1")]
    [Description("Protects lootables from being looted by other players")]
    class LootProtection : CovalencePlugin
    {
        #region Initialization

        private const string permBypass = "lootprotection.bypass";
        private const string permEnable = "lootprotection.enable";

        private void Init()
        {
            permission.RegisterPermission(permBypass, this);
            permission.RegisterPermission(permEnable, this);
            MigratePermission("lootprotection.corpse", permEnable);
            MigratePermission("lootprotection.sleeper", permEnable);
        }

        #endregion Initialization

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LootProtection"] = "{0} has loot protection enabled"
            }, this);
        }

        #endregion Localization

        #region Loot Protection

        private object ProtectLoot(BasePlayer looter, BaseEntity entity, string entityId, string entityName)
        {
            if (!permission.UserHasPermission(looter.UserIDString, permBypass))
            {
                if (entity != null && looter.userID != entity.OwnerID && permission.UserHasPermission(entityId, permEnable))
                {
                    NextFrame(looter.EndLooting);
                    looter.ChatMessage(string.Format(lang.GetMessage("LootProtection", this, looter.UserIDString), entityName));
                    return true;
                }
            }

            return null;
        }

        private object OnLootEntity(BasePlayer looter, BasePlayer sleeper)
        {
            return ProtectLoot(looter, sleeper, sleeper.UserIDString, sleeper.displayName);
        }

        private object OnLootEntity(BasePlayer looter, LootableCorpse corpse)
        {
            return ProtectLoot(looter, corpse, corpse.playerSteamID.ToString(), corpse.playerName);
        }

        private object OnLootEntity(BasePlayer looter, DroppedItemContainer container)
        {
            return ProtectLoot(looter, container, container.playerSteamID.ToString(), container.playerName);
        }

        #endregion Loot Protection

        #region Helpers

        private void MigratePermission(string oldPerm, string newPerm)
        {
            foreach (string groupName in permission.GetPermissionGroups(oldPerm))
            {
                permission.GrantGroupPermission(groupName, newPerm, null);
                permission.RevokeGroupPermission(groupName, oldPerm);
            }

            foreach (string playerId in permission.GetPermissionUsers(oldPerm))
            {
                permission.GrantUserPermission(Regex.Replace(playerId, "[^0-9]", ""), newPerm, null);
                permission.RevokeUserPermission(Regex.Replace(playerId, "[^0-9]", ""), oldPerm);
            }
        }

        #endregion Helpers
    }
}
