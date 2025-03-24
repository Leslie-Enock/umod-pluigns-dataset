using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Team Offline Doors", "Gargoyle", "0.8.5")]
    [Description("Closes team doors and stashes when the last team member goes offline")]
    class TeamOfflineDoors : RustPlugin
    {
        #region Configuration

        private bool CloseDoors = true;
        private bool HideStash  = true;

        #endregion

        #region Hooks

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (player.currentTeam != 0UL)
            {
                RelationshipManager.PlayerTeam theTeam = RelationshipManager.ServerInstance.FindTeam(player.currentTeam);
                int online = 0;

                foreach (var member in theTeam.members)
                {
                    if (member == player.userID) continue;
                
                    foreach (BasePlayer ingame in BasePlayer.activePlayerList)
                    {
                        if (ingame.userID == member)
                        {
                            online++;
                            break;
                        }
                    }

                    if (online > 0) break;
                }

                if (online == 0)
                {
                    foreach (var member in theTeam.members)
                    {
                        if (CloseDoors) DoClose(member);
                        if (HideStash) DoHide(member);
                    }
                }
            }
            else
            {
                if (CloseDoors) DoClose(player.userID);   
                if (HideStash) DoHide(player.userID);   
            }
        }
        
        #endregion

        #region Commands

        private void DoClose(ulong playerId)
        {
            List<Door> list = Resources.FindObjectsOfTypeAll<Door>().Where(x => x.OwnerID == playerId).ToList();
            if (list.Count == 0) return;

            foreach (var item in list)
            {
                if (item.IsOpen()) item.CloseRequest();    
            }
        }

        private void DoHide(ulong playerId)
        {
            List<StashContainer> list = Resources.FindObjectsOfTypeAll<StashContainer>().Where(x => x.OwnerID == playerId).ToList();
            if (list.Count == 0) return;

            foreach (var item in list)
            {
                if (!item.IsHidden()) item.SetHidden(true);
            }
        }

        #endregion
    }
}
