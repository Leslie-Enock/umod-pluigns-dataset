using System;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("NPC Health", "Rustoholics", "0.1.1")]
    [Description("Boost or decrease the health of all NPC players by scaling damage dealt to them")]

    public class NPCHealth : CovalencePlugin
    {
        #region Config
        private Configuration _config;
        protected override void SaveConfig() => Config.WriteObject(_config);
        protected override void LoadDefaultConfig() => _config = new Configuration();

        private class Configuration
        {
            [JsonProperty("How strong are NPCs (1.0 = normal, 0.5 = half as strong, 2 = twice as strong, 10 = epic")]
            public double NPCHealthMultiplier = 2;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();

                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }
        #endregion
        
        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || info.HitEntity == null || !(info.HitEntity is NPCPlayer))
            {
                return;
            }

            var dmgModifier = (float)(1 / _config.NPCHealthMultiplier);

            info.damageTypes.ScaleAll(dmgModifier);
        }
    }
}