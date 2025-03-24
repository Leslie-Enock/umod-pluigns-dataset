using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("No Heli Fire", "Tryhard", "1.2.6")]
    [Description("Optionally removes the explosion sound, gibs and fire effect from mini- and scrap helicopters")]
    public class NoHeliFire : RustPlugin
    {
        private ConfigData configData = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Disable minicopter gibs")]
            public bool mGibs = true;

            [JsonProperty(PropertyName = "Disable minicopter fire")]
            public bool mFire = true;

            [JsonProperty(PropertyName = "Disable minicopter explosion sound")]
            public bool mExplo = true;



            [JsonProperty(PropertyName = "Disable scraphelicopter gibs")]
            public bool sGibs = true;

            [JsonProperty(PropertyName = "Disable scraphelicopter explosion sound")]
            public bool sExplo = true;

            [JsonProperty(PropertyName = "Disable scraphelicopter fire ")]
            public bool sFire = true;



            [JsonProperty(PropertyName = "Disable attackhelicopter gibs")]
            public bool aGibs = true;

            [JsonProperty(PropertyName = "Disable attackhelicopter explosion sound")]
            public bool aExplo = true;

            [JsonProperty(PropertyName = "Disable attackhelicopter fire ")]
            public bool aFire = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                configData = Config.ReadObject<ConfigData>();

                if (configData == null) LoadDefaultConfig();
            }

            catch
            {
                PrintError("Configuration file is corrupt, check your config file at https://jsonlint.com/!");
                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => configData = new ConfigData();

        protected override void SaveConfig() => Config.WriteObject(configData);


        private void OnEntitySpawned(ScrapTransportHelicopter entity)
        {

            if (configData.sExplo) entity.explosionEffect.guid = null;

            if (configData.sFire) entity.fireBall.guid = null;

            if (configData.sGibs) entity.serverGibs.guid = null;
        }

        private void OnEntitySpawned(Minicopter entity)
        {
            if (configData.mExplo) entity.explosionEffect.guid = null;

            if (configData.mFire) entity.fireBall.guid = null;

            if (configData.mGibs) entity.serverGibs.guid = null;
        }

        private void OnEntitySpawned(AttackHelicopter entity)
        {
            if (configData.aExplo) entity.explosionEffect.guid = null;

            if (configData.aFire) entity.fireBall.guid = null;

            if (configData.aGibs) entity.serverGibs.guid = null;
        }
    }
}