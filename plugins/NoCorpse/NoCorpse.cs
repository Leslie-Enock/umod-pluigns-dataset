namespace Oxide.Plugins
{
    [Info("No Corpse", "Erik", "1.0.2")]
    [Description("Simple plugin that will allow players with set permission to not spawn their corpse when they die.")]
    public class NoCorpse : RustPlugin
    {
        private const string permNoCorpse = "NoCorpse.include";

        private void Init()
        {
            permission.RegisterPermission(permNoCorpse, this);
        }

        object OnPlayerCorpseSpawn(BasePlayer player)
        {
            if (player == null || player.IPlayer == null)
            {
                return null;
            }

            if (player.IPlayer.HasPermission(permNoCorpse))
            {
                return false;
            }

            return null;
        }

    }
}