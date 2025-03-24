using Newtonsoft.Json;
using Oxide.Core.Libraries;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Oil Rig Wipe Protection", "Strrobez", "1.0.2")]
    [Description("Block people from activating oil rig X amount of minutes after a map wipe.")]
    class OilRigWipeProtection : RustPlugin
    {
        private const string CratePrefab = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate_oilrig.prefab";
        private const string permEnabled = "oilrigwipeprotection.enabled";
        private const string permBypass = "oilrigwipeprotection.bypass";
        private DateTime _cachedWipeTime;

        #region Config & Localization
        protected override void LoadDefaultConfig()
        {
            Config["ShowBlockMessage"] = true;
            Config["WipeBlockMinutes"] = 600;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["BlockMessage"] = "You can't start the crate yet, please wait another {0} minutes.",
            }, this);
        }
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            permission.RegisterPermission(permEnabled, this);
            permission.RegisterPermission(permBypass, this);
        }

        private void OnServerInitialized()
        {
            _cachedWipeTime = SaveRestore.SaveCreatedTime;
        }

        private object CanHackCrate(BasePlayer player, HackableLockedCrate crate)
        {

            if (crate.PrefabName == CratePrefab)
            {
                if (!permission.UserHasPermission(player.UserIDString, permEnabled)) return null;

                if (permission.UserHasPermission(player.UserIDString, permBypass)) return null;

                DateTime wipeBlockOver = _cachedWipeTime.AddMinutes((int)Config["WipeBlockMinutes"]);
                int timeLeft = (int)(DateTime.UtcNow - wipeBlockOver).TotalMinutes;

                if ((wipeBlockOver > DateTime.UtcNow)) return null;

                if ((bool)Config["ShowBlockMessage"])
                {
                    string message = lang.GetMessage("BlockMessage", this, player.UserIDString);
                    player.ChatMessage(string.Format(message, timeLeft.ToString()));
                }

                return true;
            }

            return null;
        }
        #endregion
    }
}
