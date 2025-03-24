using System.Collections.Generic;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using Rust;


namespace Oxide.Plugins
{
    [Info("No MLRS Mount", "Lincoln", "1.0.4")]
    [Description("Disallows people from using the MLRS.")]

    class NoMLRSMount : RustPlugin
    {
        private const string permBypass = "nomlrsmount.bypass";

        private void Init()
        {
            permission.RegisterPermission(permBypass, this);
            {
                    foreach (var mlrs in BaseMountable.serverEntities.OfType<MLRS>().ToArray())
                    {
                        if (mlrs == null) continue;

                        BasePlayer player = mlrs.GetMounted() ?? null;

                        if (player != null && !permission.UserHasPermission(player.UserIDString, permBypass))
                        {
                            Puts("Found player " + player.displayName + " mounted on MLRS, dismounting...");
                            mlrs.DismountAllPlayers();
                        }
                        return; 
                    }
            }
        }
        object CanMountEntity(BasePlayer player, MLRS entity)
        {
            if (!player.IsNpc && !permission.UserHasPermission(player.UserIDString, permBypass))
            {
                ChatMessage(player, "Disabled");
                return false;
            }
            return null;
        }
        #region Localization

        private void ChatMessage(BasePlayer player, string messageName, params object[] args) =>
            player.ChatMessage(string.Format(GetMessage(player.IPlayer, messageName), args));

        private string GetMessage(IPlayer player, string messageName, params object[] args) =>
            GetMessage(player.Id, messageName, args);

        private string GetMessage(string playerId, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, playerId);
            return args.Length > 0 ? string.Format(message, args) : message;
        }
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Disabled"] = "MLRS has been <color=red>disabled</color> on this server."

            }, this, "en");
        }
        #endregion
    }
}