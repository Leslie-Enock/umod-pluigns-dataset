namespace Oxide.Plugins
{
    [Info("Craft Queue Saver", "Jake_Rich", "1.1.6")]
    [Description("Saves player crafting queues on disconnect and on server shutdown")]
    class CraftQueueSaver : RustPlugin
    {
        private void Init()
        {
            Puts("This plugins functionality is part of the game.");
            Puts("The game saves the crafting queue when a player disconnects or when the server restarts.");
            Puts("The game puts the crafting materials back into the inventory of the player when they die.");
            Puts("There is no need for this plugin anymore. Delete it.");
        }
    }
}
