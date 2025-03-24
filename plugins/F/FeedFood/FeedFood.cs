using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Feed Food", "Lorddy", "0.0.1")]
    [Description("Feeds dropped food to players you're looking at")]
    public class FeedFood : RustPlugin
    {
        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Fed"] = "You fed {0} a {1}",
                ["CannotConsume"] = "Feeding target can't consume anymore"

            }, this); ;
        }
        #endregion Localization

        #region Initialization
        private const string PERMISSION_FEEDER = "feedfood.feeder";
        private const string PERMISSION_AFFECT = "feedfood.affect";
        private void Init()
        {
            permission.RegisterPermission(PERMISSION_FEEDER, this);
            permission.RegisterPermission(PERMISSION_AFFECT, this);
        }

        #endregion Initialization

        #region Oxide Hooks
        void OnItemDropped(Item item, BaseEntity entity)
        {
            if (item == null || entity == null) return;
            var player = item.GetOwnerPlayer();
            if (player == null) return;
            if (!permission.UserHasPermission(player.UserIDString, PERMISSION_FEEDER)) return;
            BasePlayer target = RelationshipManager.GetLookingAtPlayer(player);
            if (target == null || target.userID == player.userID) return;
            if (target.IsNpc) return;
            if (!permission.UserHasPermission(target.UserIDString, PERMISSION_AFFECT)) return;
            ItemModConsume component = item.info.GetComponent<ItemModConsume>();
            if (!(component == null))
            {
                if(component.CanDoAction(item, target))
                {
                    component.DoAction(item, target);
                    Message(player.IPlayer, "Fed", target.displayName, item.info.displayName.english);
                }
                else
                {
                    Message(player.IPlayer, "CannotConsume");
                }
               
                return;
            }
        }
        #endregion

        #region Core Methods

        #endregion Core Methods

        #region Helpers

        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }

        private void Message(IPlayer player, string textOrLang, params object[] args)
        {
            if (player.IsConnected)
            {
                string message = GetLang(textOrLang, player.Id, args);
                player.Reply(message != textOrLang ? message : textOrLang);
            }
        }
        #endregion Helpers
    }
}

