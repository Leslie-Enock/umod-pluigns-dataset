namespace Oxide.Plugins
{
    [Info("Scrap Helicopter Fix", "Orange", "1.0.4")]
    [Description("Reduces lags of destroying scrap helicopter by removing effects of it")]
    public class ScrapHelicopterFix : RustPlugin
    {
        private void OnServerInitialized()
        {
            foreach (var entity in UnityEngine.Object.FindObjectsOfType<ScrapTransportHelicopter>())
            {
                if (entity == null) {continue;}
                OnEntitySpawned(entity);
            }
        }
        
        private void OnEntitySpawned(ScrapTransportHelicopter entity)
        {
            entity.explosionEffect.guid = null;
            entity.serverGibs.guid = null;
            entity.fireBall.guid = null;
        }
    }
}