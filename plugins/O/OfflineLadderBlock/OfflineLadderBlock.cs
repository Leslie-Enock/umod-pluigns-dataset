using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using System.Linq;
using Oxide.Core.Plugins;
using UnityEngine;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("Offline Ladder Block", "Chepzz", "1.0.1")]
    [Description("Prevents ladders being placed if the entity owner is offline.")]
    public class OfflineLadderBlock : RustPlugin
    {
        #region Fields

        const string bypassPerm = "offlineladderblock.bypass";

        #endregion

        #region Configuration

        Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "OfflineLadderBlock Options")]
            public PluginOptions POptions = new PluginOptions();
        }

        public class PluginOptions
        {
            [JsonProperty(PropertyName = "Chat Icon (Steam64ID)")]
            public ulong chatIcon = 0;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Localization

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TargetOffline"] = "<color=#C0C0C0>The owner of the base you are trying to ladder in to is offline.</color>"
            }, this);
        }

        #endregion

        #region Core Methods

        private void Loaded()
        {
            permission.RegisterPermission(bypassPerm, this);
        }


        object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (planner == null || prefab == null || target.entity == null) return null;

            BasePlayer player = planner.GetOwnerPlayer();

            var targetPlayer = BasePlayer.Find(target.entity.OwnerID.ToString());

            if (targetPlayer == null || !targetPlayer.IsConnected)
            {
                PrintMsg(player, Lang("TargetOffline", player.UserIDString));
                return true;
            }

            return null;
        }



        #endregion

        #region Helpers

        private void PrintMsg(BasePlayer player, string message) => Player.Message(player, message, config.POptions.chatIcon);

        string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        #endregion
    }
}
