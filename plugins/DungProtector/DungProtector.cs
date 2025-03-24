using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Dung Protector", "KajWithAJ", "1.4.0")]
    [Description("Prevent players from stealing horse dung.")]

    class DungProtector : RustPlugin
    {
        private const string PermissionUse = "dungprotector.use";
        private const string PermissionExclude = "dungprotector.exclude";
        

        private void Init() {
            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionExclude, this);
        }

        protected override void LoadDefaultMessages() {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BuildingBlocked"] = "You cannot pickup this item because you are buildingblocked.",
                ["PickupFailed"] = "{0} - ({1}) tried to picking up {2} but was buildingblocked at location {3} - {4}."
            }, this);
        }

        object OnItemPickup(Item item, BasePlayer player) {
            if (item.info.shortname == "horsedung" && player.IsBuildingBlocked()) {
                if (!permission.UserHasPermission(player.UserIDString, PermissionExclude) && permission.UserHasPermission(player.UserIDString, PermissionUse)) {
                    var location = player.transform.position;
                    var gridLocation = MapHelper.PositionToGrid(location);
                    
                    var message = lang.GetMessage("PickupFailed", this);
                    Puts(string.Format(message, player.displayName, player.userID, item.info.shortname, gridLocation, location.ToString("F1")));

                    player.ChatMessage(lang.GetMessage("BuildingBlocked", this, player.UserIDString));
                    return false;
                }
            }
            return null;
        }
    }
}
