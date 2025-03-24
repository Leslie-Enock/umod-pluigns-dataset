using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Prevent Quarry Toggle", "Rezx", "1.0.2")]
    [Description("This plugin prevents players that are not in a team with the owner from turning quarry/pumpjacks off")]
    internal class PreventQuarryToggle : CovalencePlugin
    {
        private static bool SameTeam(ulong playerId, ulong friendId)
        {
            if (!RelationshipManager.TeamsEnabled())
            {
                return false;
            }

            var playerTeam = RelationshipManager.ServerInstance.FindPlayersTeam(playerId);
            if (playerTeam == null)
            {
                return false;
            }

            var friendTeam = RelationshipManager.ServerInstance.FindPlayersTeam(friendId);
            if (friendTeam == null)
            {
                return false;
            }

            return playerTeam == friendTeam;
        }

        void OnQuarryToggled(MiningQuarry quarry, BasePlayer player)
        {
            if (quarry.OwnerID == 0)
            {
                // Ignore default quarry
                return;
            }

            if (quarry.OwnerID == player.userID)
            {
                // Player is the owner, allow the quarry to be toggled
            }
            else if (SameTeam(player.userID, quarry.OwnerID))
            {
                // Player is on the same team as the owner, allow the quarry to be toggled
            }
            else
            {
                // Turn the engine back on if it was turned off by an unauthorized player
                if (!quarry.IsEngineOn())
                {

                    if (!Config.Get<bool>("DisableChatMessage"))
                    {
                        var ownerName = covalence.Players.FindPlayerById(quarry.OwnerID.ToString())?.Name ?? quarry.OwnerID.ToString();
                        var unauthorizedMessage = string.Format(lang.GetMessage("UnauthorizedMessage", this), ownerName);
                        player.ChatMessage(unauthorizedMessage);
                    }

                    quarry.EngineSwitch(true);
                }

            }
        }

        


        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UnauthorizedMessage"] = "You are not authorized to toggle this, it is owned by {0}"
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            if (Config.Get("DisableChatMessage") == null)
            {
                Config["DisableChatMessage"] = false;
            }
            SaveConfig();
        }
    }
}
