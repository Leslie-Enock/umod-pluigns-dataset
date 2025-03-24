using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
     [Info("Heli Support", "Vault Boy", "1.0.0")]
     [Description("Call a support from heli forces")]
     public class HeliSupport : RustPlugin
     {
         #region Vars

         private const string nocdperm = "helisupport.callnocd";

         private const string heliprefab = "assets/prefabs/npc/patrol helicopter/patrolhelicopter.prefab";

         #endregion
         
         #region Oxide hooks
         
         private void OnServerInitialized()
         {
             permission.RegisterPermission(nocdperm, this);
         }

         private bool CanHelicopterTarget(PatrolHelicopterAI heli, BasePlayer player)
         {
             var id = heli.GetComponent<BaseEntity>().net.ID;

             if (!helis.ContainsKey(id))
             {
                 return true;
             }

             if (helis[id] == player.userID) Server.Broadcast("Cant");

             return helis[id] != player.userID;
         }
         
         private bool CanHelicopterUseNapalm(PatrolHelicopterAI heli)
         {
             return !helis.ContainsKey(heli.GetComponent<BaseEntity>().net.ID);
         }

         #endregion

         #region Data

         private Dictionary<uint, ulong> helis = new Dictionary<uint, ulong>(); // Heli id - Heli owner

         #endregion
         
         #region Commands

         [ChatCommand("heli")]
         private void CmdCall(BasePlayer player)
         {
             if (permission.UserHasPermission(player.UserIDString, nocdperm))
             {
                 CallHeli(player);
                 return;
             }
             
             player.ChatMessage("You dont have acess to user this command!");
         }

         #endregion

         #region Helpers

         private void CallHeli(BasePlayer player)
         {
             var entity = GameManager.server.CreateEntity(heliprefab);
             entity.Spawn();

             helis.Add(entity.net.ID, player.userID);

             var heliai = entity.GetComponent<PatrolHelicopterAI>();
             heliai.SetInitialDestination(player.transform.position + new Vector3(0.0f, 50f, 0.0f));
             
             player.ChatMessage("Heli is coming for you!");

             timer.Every(10f, () =>
             {
                 if(heliai.isDead) return;
                 heliai.ExitCurrentState();
                 heliai.State_Strafe_Enter(player.transform.position);
             });
         }

         #endregion
         
     }
}