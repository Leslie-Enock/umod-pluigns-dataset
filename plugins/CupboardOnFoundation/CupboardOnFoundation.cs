using System.Collections.Generic;
using UnityEngine;
using System.Linq;

#region Changelogs and ToDo
/**********************************************************************
 * 
 * v1.1.0   :   Started to Maintain (Krungh Crow)
 *          :   Preventive Fix for RPC error
 * 
 **********************************************************************/
#endregion

namespace Oxide.Plugins
{
    [Info("Cupboard On Foundation", "BuzZ/Krungh Crow", "1.1.1")]
    [Description("Authorize cupboard to be placed only on foundation")]

    #region Changelogs and ToDo
    /**********************************************************************
     * 
     * v1.1.1   :   Fixed duping
     * 
     **********************************************************************/
    #endregion


    public class CupboardOnFoundation : RustPlugin
    {

        bool debug = false;
        const string FoundationOnly = "cupboardonfoundation.only"; 

        void Init()
        {
            permission.RegisterPermission(FoundationOnly, this);

        }

        #region LanguageAPI

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MayNotPlaceMsg", "You may not place a tool cupboard on anything but a foundation !"},

            }, this, "en");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"MayNotPlaceMsg", "Vous ne pouvez placer cet objet ailleurs que sur une fondation !"},

            }, this, "fr");
        }

        #endregion


        void OnEntitySpawned(BaseEntity entity, UnityEngine.GameObject gameObject)
        {
            if(entity.ShortPrefabName.Contains("cupboard.tool"))
            {
                if (debug)Puts($"a cupboard is spawning in world");
                if(entity.OwnerID == null) return;
                BasePlayer player = BasePlayer.FindByID(entity.OwnerID);
                if (player == null) return;
                if(player.IsSleeping() == true || player.IsConnected == false) return;
                if (debug)Puts($"sleep check");
                bool isauth = permission.UserHasPermission(player.UserIDString, FoundationOnly);
                if (!isauth) return;
                if (debug)Puts($"wotsaround");
                List<BaseEntity> wotsaround = new List<BaseEntity>();
                Vis.Entities(entity.transform.position, 1, wotsaround);
                foreach (BaseEntity foundentity in wotsaround)
                {
                    if(foundentity.ShortPrefabName.Contains("cupboard") && wotsaround.Count == 1)
                    {
                        if (debug)Puts($"alone cup");
                        CancelThisCupboard(entity, player);
                        break;
                    }

                    if(foundentity.ShortPrefabName.Contains("foundation"))
                    {
                        if (debug)Puts($"foundation");
                        CheckWherePlaced(entity, foundentity, player, null);
                        break;
                    }

                    if(foundentity.ShortPrefabName.Contains("floor"))
                    {
                        if (debug)Puts($"floor");
                        CheckWherePlaced(entity, foundentity, player, "floor");
                    }
                }
            }
        }

        void CheckWherePlaced (BaseEntity cupboard, BaseEntity entity, BasePlayer player, string found)
        {
            if (debug)Puts($"CheckWherePlaced");
            List<BaseEntity> wotsaround = new List<BaseEntity>();
            Vis.Entities(new Vector3(entity.transform.position.x, entity.transform.position.y + 0.5f, entity.transform.position.z), 1, wotsaround);
            if (debug)Puts($"processing one foundation entity");
            foreach (BaseEntity foundentity in wotsaround)
            {
                if (foundentity.ShortPrefabName.Contains("cupboard"))
                {
                    if (found == "floor")
                    {
                        NextTick(() =>
                        {
                            CancelThisCupboard(cupboard, player);
                            return;
                        });
                    }
                    if (debug)Puts($"placing a cupboard");
                    return;
                }
            }
        }

        void CancelThisCupboard (BaseEntity entity, BasePlayer player)
        {
            if (debug)Puts($"cancelling a cupboard");
            SendReply(player, lang.GetMessage("MayNotPlaceMsg", this));  
            entity.KillMessage();
            NextTick(() =>
            {
                var itemtogive = ItemManager.CreateByItemID(-97956382, 1);
                if (itemtogive != null) player.inventory.GiveItem(itemtogive);
                return;
            });
        }
    }
}