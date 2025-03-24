using System.Collections.Generic;
using System;

namespace Oxide.Plugins
{
    [Info("Admin Decay Hammer", "Bazz3l", "0.0.5")]
    [Description("Hit a building block to start a faster decay.")]
    class AdminDecayHammer : RustPlugin
    {
        private const string Perm = "admindecayhammer.use";
        private List<ulong> Users = new List<ulong>();

        #region Config
        private PluginConfig configData;

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(GetDefaultConfig(), true);
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                DecayVariance = 100f
            };
        }

        private class PluginConfig
        {
            public float DecayVariance;
        }
        #endregion

        #region Lang
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoKeyPress"]      = "To use the decay hammer, please hold shift while hitting a building block.",
                ["NoBuildingBlock"] = "No building block found.",
                ["NoDecayEntity"]   = "No decay entity found.",
                ["NoPermission"]    = "No permission.",
                ["DecayStarted"]    = "Decay will start on next decay tick.",
                ["ToggleEnabled"]   = "Decay hammer is now Enabled.",
                ["ToggleDisabled"]  = "Decay hammer Disabled."
            }, this);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion

        #region Oxide
        private void Init()
        {
            permission.RegisterPermission(Perm, this);

            configData = Config.ReadObject<PluginConfig>();
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player != null && Users.Contains(player.userID))
            {
                Users.Remove(player.userID);
            }
        }

        private void OnHammerHit(BasePlayer player, HitInfo info)
        {
            if (player == null || info == null || !Users.Contains(player.userID) || !permission.UserHasPermission(player.UserIDString, Perm))
            {
                return;
            }

            if (!player.serverInput.IsDown(BUTTON.SPRINT))
            {
                player.ChatMessage(Lang("NoKeyPress", player.UserIDString));
                return;
            }

            BuildingBlock block = info?.HitEntity as BuildingBlock;
            if (block == null)
            {
                player.ChatMessage(Lang("NoBuildingBlock", player.UserIDString));
                return;
            }

            DecayEntity decayEntity = block as DecayEntity;
            if (decayEntity == null)
            {
                player.ChatMessage(Lang("NoDecayEntity", player.UserIDString));
                return;
            }

            BuildingDecay(decayEntity.buildingID);

            player.ChatMessage(Lang("DecayStarted", player.UserIDString));
        }
        #endregion

        #region Core
        private void BuildingDecay(uint buildingID)
        {
            BuildingManager.Building buildManager = BuildingManager.server.GetBuilding(buildingID);
            if (buildManager == null)
            {
                return;
            }

            foreach(DecayEntity decayEnt in buildManager.decayEntities)
            {
                decayEnt.ResetUpkeepTime();
                decayEnt.decayVariance = configData.DecayVariance;
            }
        }
        #endregion

        #region Commands
        [ChatCommand("decayhammer")]
        void DecayHammerCommand(BasePlayer player, string cmd, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Perm))
            {
                player.ChatMessage(Lang("NoPermission", player.UserIDString));
                return;
            }

            if (!Users.Contains(player.userID))
            {
                Users.Add(player.userID);
            }
            else
            {
                Users.Remove(player.userID);
            }

            player.ChatMessage(Users.Contains(player.userID) ? Lang("ToggleEnabled", player.UserIDString) : Lang("ToggleDisabled", player.UserIDString));
        }
        #endregion
    }
}