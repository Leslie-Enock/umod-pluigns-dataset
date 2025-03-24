using Newtonsoft.Json;
using Oxide.Core;
using System.Collections.Generic;
using Random = Oxide.Core.Random;

namespace Oxide.Plugins
{
    [Info("Scientist Bleed", "birthdates", "1.0.0")]
    [Description("Scientist shots now make you bleed")]
    public class ScientistBleed : RustPlugin
    {
        #region Variables

        #endregion

        #region Hooks
        private void Init()
        {
            LoadConfig();
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (!info.hasDamage) return null;
            var p = entity as BasePlayer;
            if (p == null || info.Initiator == null)
            {
                return null;
            }

            var sci = info.Initiator as NPCPlayer;
            if (sci == null)
            {
                return null;
            }
            var bone = info.boneName;

            var itemName = sci.GetHeldEntity().GetItem().info.shortname;
            Bleed bleed;
            if (!_config.bleeds.TryGetValue(itemName, out bleed))
            {
                bleed = _config.defaultBleed;
            }
            try
            {
                p.metabolism.bleeding.value += Random.Range(bleed.minBleed, bleed.maxBleed);
                Interface.CallHook("OnRunPlayerMetabolism", p.metabolism);
            }
            catch
            {
                PrintError("You have your bleed minimum is higher than your maximum!");
            }
            return null;
        }
        #endregion

        #region Configuration & Language
        public ConfigFile _config;

        public class Bleed
        {
            public float minBleed;
            public float maxBleed;
        }

        public class ConfigFile
        {
            [JsonProperty("Indiviual Item Bleeds (Item shortnames)")]
            public Dictionary<string, Bleed> bleeds;
            [JsonProperty("Default Bleed for any item not found")]
            public Bleed defaultBleed;
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    bleeds = new Dictionary<string, Bleed>
                    {
                        {"shotgun.spas12", new Bleed
                        {
                            minBleed = 5f,
                            maxBleed = 6f
                        }}
                    },
                    defaultBleed = new Bleed
                    {
                        minBleed = 3f,
                        maxBleed = 5f
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null)
            {
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }
        #endregion
    }
}
//Generated with birthdates' Plugin Maker
