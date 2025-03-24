using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Upgrade Permissions", "Wulf", "2.0.0")]
    [Description("Allows players to upgrade structures based on permissions")]
    class UpgradePermissions : CovalencePlugin
    {
        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Not allowed to upgrade"] = "You are not allowed to upgrade this structure to {0}",
            }, this);
        }

        #endregion Localization

        #region Handling

        private void Init()
        {
            foreach (BuildingGrade.Enum grade in Enum.GetValues(typeof(BuildingGrade.Enum)))
            {
                permission.RegisterPermission($"{Name}.{grade}", this);
            }
        }

        private object CanChangeGrade(BasePlayer basePlayer, BuildingBlock block, BuildingGrade.Enum grade)
        {
            IPlayer player = basePlayer.IPlayer;
            if (!player.HasPermission($"{Name.ToLower()}.{grade}"))
            {
                Message(player, "Not allowed to upgrade", grade);
                return false;
            }

            return null;
        }

        #endregion Handling

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
