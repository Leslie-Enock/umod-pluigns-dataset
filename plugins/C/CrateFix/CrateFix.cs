using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Crate Fix", "birthdates", "1.0.2")]
    [Description("A fix for players being able to swap items in crates thus not letting them despawn.")]
    public class CrateFix : RustPlugin
    {
        #region Hooks
        private void Init()
        {
            LoadConfig();
        }

        object CanAcceptItem(ItemContainer container, Item item, int targetPos)
        {
            var Entity = container.entityOwner;
            if (Entity == null) return null;
            if (!Entity.PrefabName.Contains("crate") && !Entity.PrefabName.Contains("radtown")) return null;
            if (_config.DisabledCrates.Contains(Entity.PrefabName)) return null;
            var Player = item.GetRootContainer()?.GetOwnerPlayer();
            if (Player == null) return null;
            Player.ChatMessage(lang.GetMessage("CannotSwap", this, Player.UserIDString));
            return ItemContainer.CanAcceptResult.CannotAcceptRightNow;
        }
        #endregion

        #region Configuration & Language
        public ConfigFile _config;

        public class ConfigFile
        {
            [JsonProperty("Disabled Crates (Prefab)")]
            public List<string> DisabledCrates;
            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    DisabledCrates = new List<string>
                    {
                        "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab"
                    }
                };
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"CannotSwap", "You cannot swap that item."},
            }, this);
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