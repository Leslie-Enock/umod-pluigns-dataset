using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Plugins;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Family Share Detect", "AVOCoder", "1.2.0")]
    [Description("Checks players if using Family Sharing")]
    class FamilyShareDetect : CovalencePlugin
    {
    	#region Config

        private ConfigData cfg;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Broadcast Check in Console")]
            public bool broadcastConsoleCheck { get; set; }
            [JsonProperty(PropertyName = "Kick Family Sharing Player")]
            public bool kick { get; set; }
            [JsonProperty(PropertyName = "Kick only if App Owner in Server Players list")]
            public bool kickIfOwnerIsPlayer { get; set; }
            [JsonProperty(PropertyName = "Broadcast Kick in Console")]
            public bool broadcastConsoleKick { get; set; }
            [JsonProperty(PropertyName = "Log detects")]
            public bool logDetects { get; set; }
            [JsonProperty(PropertyName = "Whitelist")]
            public List<string> whitelist { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            cfg = Config.ReadObject<ConfigData>();

            var defaultCfg = GetDefaultConfigData();
            foreach (var prop in defaultCfg.GetType().GetProperties()) {
			    if (cfg.GetType().GetProperty(prop.Name).GetValue(cfg, null) == null) {
			    	cfg.GetType().GetProperty(prop.Name).SetValue(cfg, defaultCfg.GetType().GetProperty(prop.Name).GetValue(defaultCfg, null));
			    }
			}

			Config.WriteObject(cfg, true);
        }

        protected override void LoadDefaultConfig()
        {
        	cfg = GetDefaultConfigData();

            Config.WriteObject(cfg, true);
        }

        private ConfigData GetDefaultConfigData()
        {
        	return new ConfigData
            {
            	broadcastConsoleCheck = true,
            	kick = true,
            	kickIfOwnerIsPlayer = true,
            	broadcastConsoleKick = true,
            	logDetects = false,
            	whitelist = new List<string>(){
            		"76561190000000000"
            	}
            };
        }
        
        #endregion Config

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["SharedDetected"] = "Family Share Detected! for steamid {0}, ownerid {1}",
                ["OwnerFound"] = "Ownerid {0} found in server player list",
                ["PlayerKick"] = "Kicking steamid {0}",
                ["KickReason"] = "Family Share is blocked",
            }, this, "en");
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion Lang

        #region Broadcasting
        private void BroadcastInConsole(string message)
        {
        	if (cfg.broadcastConsoleCheck)
        		Puts(message);
        }

        private void BroadcastKickInConsole(string message)
        {
        	if (cfg.broadcastConsoleKick)
        		Puts(message);
        }

        private void Log(string name, string message)
        {
        	if (cfg.logDetects)
            	LogToFile(name, $"[{DateTime.Now}] {message}", this);
        }
        #endregion Broadcasting

        #region Hooks

        private void OnUserConnected(IPlayer player)
        {
        	if (cfg.whitelist.Contains(player.Id))
        		return;

            BasePlayer pl = player.Object as BasePlayer;
            if (pl == null || pl.net.connection.ownerid == 0 || pl.userID == pl.net.connection.ownerid)
                return;

            ulong ownerId = pl.net.connection.ownerid;

            BroadcastInConsole(Lang("SharedDetected", null, player.Id, ownerId));

            Log("detects", Lang("SharedDetected", null, player.Id, ownerId));

            object obj = Interface.CallHook("OnFamilyShareDetected", player, ownerId.ToString());
            if (obj != null)
                return;

            IPlayer owner = covalence.Players.FindPlayerById(ownerId.ToString());
            if (owner != null)
                BroadcastInConsole(Lang("OwnerFound", null, ownerId));

            if (!cfg.kick)
                return;

            if (cfg.kickIfOwnerIsPlayer && owner == null)
                return;

            BroadcastKickInConsole(Lang("PlayerKick", null, player.Id, ownerId));

            player.Kick(Lang("KickReason", player.Id));

        }

        #endregion Hooks

    }
}