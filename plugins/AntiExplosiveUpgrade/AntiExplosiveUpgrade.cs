using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Anti Explosive Upgrade", "birthdates", "1.0.0")]
    [Description("Deny upgrading a building block when an explosive is stuck to it")]
    public class AntiExplosiveUpgrade : RustPlugin
    {

        #region Hook

        private object OnStructureUpgrade(Component entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (entity.GetComponentInChildren<TimedExplosive>() == null) return null;
            player.ChatMessage(lang.GetMessage("CannotUpgrade", this, player.UserIDString));
            return false;
        }

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"CannotUpgrade", "You cannot upgrade this as an explosive is stuck to it!"}
            }, this);
        }

        #endregion
    }
}