namespace Oxide.Plugins
{
    [Info("Wounded Drop", "birthdates", "1.0.0")]
    [Description("Players who are wounded are no longer able to drop items in their hotbar.")]
    public class WoundedDrop : RustPlugin
    {
        #region Hooks
        object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (player.IsWounded()) return false;
            return null;
        }
        #endregion
    }
}
//Generated with birthdates' Plugin Maker
