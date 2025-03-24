using Oxide.Core;
using Oxide.Core.Configuration;

using Newtonsoft.Json;

using System;
using System.IO;

using System.Diagnostics;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Timed Workbench", "DizzasTeR", "1.0.4")]
    [Description("Unlocks Workbenches level 2 and 3 later in the wipe")]
    class TimedWorkbench : CovalencePlugin
    {
        #region Vars
        private Configuration config;

        private Timer timer_WB2, timer_WB3;

        private long ID_WORKBENCH_LEVEL_2 = -41896755;
        private long ID_WORKBENCH_LEVEL_3 = -1607980696;

        #region Permission Strings
        private string PERMISSION_ADMIN     = "timedworkbench.admin";
        private string PERMISSION_SKIPLOCK  = "timedworkbench.skiplock";
        private string PERMISSION_INFO      = "timedworkbench.info";
        private string PERMISSION_TOGGLE    = "timedworkbench.toggle";
        private string PERMISSION_MODIFY    = "timedworkbench.modifytime";
        private string PERMISSION_RELOAD    = "timedworkbench.reload";
        private string PERMISSION_WIPE      = "timedworkbench.wipe";
        #endregion Permission Strings
        #endregion Vars

        #region Localization
        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "No access",
                ["SyntaxError"] = "Syntax Error!",
                ["RequestPBInfo"] = "Timed Mode {0}. WB2 {1}. WB3 {2}",
                ["ToggleResponse"] = "TimedWorkbench is now {0}",
                //
                ["ModifiedWorkbench"] = "WB {0} now unlocks in {1} seconds after wipe",
                ["InvalidWorkbench"] = "Invalid workbench specified!",
                //["CannotCraft"] = "Cannot craft this item ({0} seconds remaining)",
                //
                ["ReloadConfig"] = "Config has been reloaded",
                ["PluginWipe"] = "Plugin wiped",

            }, this);
        }

        private void SendMessage(IPlayer player, string langCode, params string[] args)
        {
            string msg = string.Format(lang.GetMessage(langCode, this, player.IsServer ? null : player.Id), args);
            if (player.IsServer)
                msg = msg.Replace("<color=red>", "").Replace("<color=green>", "").Replace("</color>", "");

            player.Reply(msg);
        }
        #endregion Localization

        #region Hooks
        private void Init()
        {
            // Permissions
            permission.RegisterPermission(PERMISSION_ADMIN, this);
            permission.RegisterPermission(PERMISSION_SKIPLOCK, this);
            permission.RegisterPermission(PERMISSION_INFO, this);
            permission.RegisterPermission(PERMISSION_TOGGLE, this);
            permission.RegisterPermission(PERMISSION_MODIFY, this);
            permission.RegisterPermission(PERMISSION_RELOAD, this);
            permission.RegisterPermission(PERMISSION_WIPE, this);

            permission.GrantGroupPermission("admin", PERMISSION_ADMIN, this);

            foreach (ItemBlueprint bp in ItemManager.GetBlueprints())
            {
                if (bp.targetItem.itemid == ID_WORKBENCH_LEVEL_2 || bp.targetItem.itemid == ID_WORKBENCH_LEVEL_3)
                {
                    bp.defaultBlueprint = true;
                    bp.userCraftable = true;
                }
            }
        }

        private void OnNewSave(string filename)
        {
            // Update the LastWipe in config as a new wipe was detected.
            config.LastWipe = DateTime.Now;
            SaveConfig();
        }

        private bool CanCraft(PlayerBlueprints playerBlueprints, ItemDefinition itemDefinition, int skinItemId)
        {
            if (config.TimedMode && itemDefinition.itemid == ID_WORKBENCH_LEVEL_2 || itemDefinition.itemid == ID_WORKBENCH_LEVEL_3)
            {
                IPlayer player = playerBlueprints._baseEntity.IPlayer;
                if (player != null && !player.HasPermission(PERMISSION_SKIPLOCK))
                {
                    double secondsPassed = (DateTime.Now - config.LastWipe).TotalSeconds;
                    double secondsLeft = (itemDefinition.itemid == ID_WORKBENCH_LEVEL_2 ? config.WB2Seconds : config.WB3Seconds) - secondsPassed;
                    if (secondsLeft > 0)
                        return false;
                }
            }
            return true;
        }

        #endregion Hooks

        #region Commands

        [Command("twinfo")]
        private void CMD_PassiveBenchInfo(IPlayer player, string command, string[] args)
        {
            if (!player.IsServer && !player.HasPermission(PERMISSION_ADMIN) && !player.HasPermission(PERMISSION_INFO))
            {
                SendMessage(player, "NoPermission");
                return;
            }

            string PassiveMode = config.TimedMode ? "<color=green>on</color>" : "<color=red>off</color>";
            string WB2Status = (DateTime.Now - config.LastWipe).TotalSeconds < config.WB2Seconds ? "<color=red>locked</color>" : "<color=green>unlocked</color>";
            string WB3Status = (DateTime.Now - config.LastWipe).TotalSeconds < config.WB3Seconds ? "<color=red>locked</color>" : "<color=green>unlocked</color>";
            SendMessage(player, "RequestPBInfo", PassiveMode, WB2Status, WB3Status);
        }

        [Command("twtoggle")]
        private void CMD_Toggle(IPlayer player, string command, string[] args)
        {
            if (!player.IsServer && !player.HasPermission(PERMISSION_ADMIN) && !player.HasPermission(PERMISSION_TOGGLE))
            {
                SendMessage(player, "NoPermission");
                return;
            }

            config.TimedMode = !config.TimedMode;
            SaveConfig();

            string status = config.TimedMode ? "<color=green>enabled</color>" : "<color=red>disabled</color>";
            SendMessage(player, "ToggleResponse", status);
        }

        [Command("setwbtime")]
        private void CMD_SetWBTime(IPlayer player, string command, string[] args)
        {
            if (!player.IsServer && !player.HasPermission(PERMISSION_ADMIN) && !player.HasPermission(PERMISSION_MODIFY))
            {
                SendMessage(player, "NoPermission");
                return;
            }

            if (args.Length < 2)
            {
                player.Reply(string.Format(lang.GetMessage("SyntaxError", this, player.Id), command));
                return;
            }

            if (args[0] != "2" && args[0] != "3")
            {
                player.Reply(string.Format(lang.GetMessage("InvalidWorkbench", this, player.Id), command));
                return;
            }

            long seconds = Convert.ToInt64(args[1]);
            if (seconds <= 0)
                seconds = 1;

            if (args[0] == "2")
                config.WB2Seconds = seconds;
            else // if its not 2, then its 3.
                config.WB3Seconds = seconds;

            SaveConfig();
            SendMessage(player, "ModifiedWorkbench", args[0], args[1]);
        }

        [Command("twreload")]
        private void CMD_Reload(IPlayer player, string command, string[] args)
        {
            if (!player.IsServer && !player.HasPermission(PERMISSION_ADMIN) && !player.HasPermission(PERMISSION_RELOAD))
            {
                SendMessage(player, "NoPermission");
                return;
            }

            LoadConfig();
            SendMessage(player, "ReloadConfig");
        }

        [Command("twwipe")]
        private void CMD_Wiped(IPlayer player, string command, string[] args)
        {
            if (!player.IsServer && !player.HasPermission(PERMISSION_ADMIN) && !player.HasPermission(PERMISSION_WIPE))
            {
                SendMessage(player, "NoPermission");
                return;
            }

            // update LastWipe for plugin
            config.LastWipe = DateTime.Now;
            SaveConfig();

            SendMessage(player, "PluginWipe");
        }

        #endregion Commands

        #region Configuration
        public class Configuration
        {
            [JsonProperty(PropertyName = "Timed Mode")]
            public bool TimedMode { get; set; } = true;

            [JsonProperty(PropertyName = "Last Wipe")]
            public DateTime LastWipe { get; set; } = DateTime.Now;

            [JsonProperty(PropertyName = "How many seconds after wipe to unlock WB2")]
            public long WB2Seconds { get; set; } = 60 * 60 * 24 * 2;

            [JsonProperty(PropertyName = "How many seconds after wipe to unlock WB3")]
            public long WB3Seconds { get; set; } = 60 * 60 * 24 * 3;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            string configPath = $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Name}.json";
            Puts($"Config file not found, creating a new configuration file at {configPath}");
            config = new Configuration();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion Configuration
    }
}