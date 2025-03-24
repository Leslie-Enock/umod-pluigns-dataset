using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Plugins;
using Rust;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AntiAmbush", "Cortex Network ~ Infamy", "1.0.0")]
    [Description("Notifies a player when they are being aimed at after a specified time")]

    class AntiAmbush : RustPlugin
    {
        private Dictionary<ulong, float> playerAimingTimestamps = new Dictionary<ulong, float>();
        private Dictionary<ulong, BasePlayer> aimedPlayers = new Dictionary<ulong, BasePlayer>();

        private float AimingDelay => Config.Get<float>("AimingDelay");

        protected override void LoadDefaultConfig()
        {
            Config["AimingDelay"] = 3.0f;
            SaveConfig();
        }

        private void Init()
        {
            timer.Every(0.1f, () =>
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    CheckPlayerAiming(player);
                }
            });
        }

        private void CheckPlayerAiming(BasePlayer player)
        {
            if (player.isMounted || player.IsDead() || player.GetHeldEntity() == null)
            {
                return;
            }

            bool isAiming = player.serverInput.IsDown(BUTTON.FIRE_SECONDARY);
            BasePlayer aimedPlayer = GetAimedPlayer(player);

            if (isAiming && aimedPlayer != null)
            {
                if (!playerAimingTimestamps.ContainsKey(player.userID))
                {
                    playerAimingTimestamps[player.userID] = Time.time;
                    aimedPlayers[player.userID] = aimedPlayer;
                }
            }

            if (playerAimingTimestamps.ContainsKey(player.userID) && Time.time - playerAimingTimestamps[player.userID] >= AimingDelay)
            {
                aimedPlayer = aimedPlayers[player.userID];
                NotifyAimedPlayer(aimedPlayer, player);
                playerAimingTimestamps.Remove(player.userID);
                aimedPlayers.Remove(player.userID);
            }
        }

        private BasePlayer GetAimedPlayer(BasePlayer player)
        {
            RaycastHit hit;
            if (Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out hit, Mathf.Infinity, LayerMask.GetMask("Player (Server)")))
            {
                BasePlayer aimedPlayer = hit.collider.GetComponentInParent<BasePlayer>();
                if (aimedPlayer != null && aimedPlayer != player)
                {
                    return aimedPlayer;
                }
            }

            return null;
        }

        private void NotifyAimedPlayer(BasePlayer aimedPlayer, BasePlayer aimer)
        {
            PrintToChat(aimedPlayer, "Warning! " + aimer.displayName + " is aiming at you!");
        }
    }
}
