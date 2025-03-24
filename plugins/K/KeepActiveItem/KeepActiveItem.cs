using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Keep Active Item", "Ryz0r", "2.1.0")]
    [Description("Restores a player's held item in their corpse's hotbar when they die.")]
    
    class KeepActiveItem : RustPlugin
    {
        const string PermissionName = "keepactiveitem.use";
        Dictionary<string, Item> _playerHeldItem = new Dictionary<string, Item>();

        private void Init() => permission.RegisterPermission(PermissionName, this);
        
        private void OnActiveItemChange(BasePlayer player, Item oldItem, ItemId newItemId)
        {
            var item = new Item().FindItem(newItemId);
            if (item == null) return;
            
            if (_playerHeldItem.ContainsKey(player.UserIDString))
            {
                _playerHeldItem[player.UserIDString] = item;
            }
            else
            {
                _playerHeldItem.Add(player.UserIDString, item);
            }
        }

        private void OnPlayerWound(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionName)) return;
            
            if (_playerHeldItem.ContainsKey(player.UserIDString)) _playerHeldItem[player.UserIDString].MoveToContainer(player.inventory.containerBelt);
        }

        private void OnPlayerRecover(BasePlayer player)
        {
            if (_playerHeldItem.ContainsKey(player.UserIDString)) _playerHeldItem.Remove(player.UserIDString);
        }

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionName)) return;
            if (!_playerHeldItem.ContainsKey(player.UserIDString)) return;

            Item item;
            _playerHeldItem.TryGetValue(player.UserIDString, out item);
            if (item == null) return;
            
            item.MoveToContainer(player.inventory.containerBelt);
            _playerHeldItem.Remove(player.UserIDString);
        }
    }
}
