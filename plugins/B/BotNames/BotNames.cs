using System.Collections.Generic;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Bot Names", "birthdates", "1.0.6")]
    [Description("Ability to change bot names")]
    public class BotNames : RustPlugin
    {
        #region Hooks

        private void Init() => LoadConfig();

        private void OnEntitySpawned(BasePlayer Player)
        {
            if (!Player.IsNpc && Player.userID.IsSteamId()) return;
            Player.displayName = _config.Names.GetRandom();
            Player.EnablePlayerCollider();
        }

        #endregion

        #region Configuration

        private ConfigFile _config;

        private class ConfigFile
        {
            public readonly List<string> Names = new List<string>()
            {
                ":(",
                ":D"
            };
        }
        
        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if(_config == null)
            {
                LoadDefaultConfig();
            }
        }
    
        protected override void LoadDefaultConfig()
        {
            _config = new ConfigFile();
            PrintWarning("Default configuration has been loaded.");
        }
    
        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion
    }
}
//Generated with birthdates' Plugin Maker
