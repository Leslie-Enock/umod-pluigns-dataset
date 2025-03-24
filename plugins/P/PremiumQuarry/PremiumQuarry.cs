using System.Collections.Generic;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Premium Quarry", "Mevent", "1.0.1")]
    [Description("Use of quarries by permission")]
    public class PremiumQuarry : CovalencePlugin
    {
        #region Fields

        [PluginReference] private Plugin UINotify;
        
        private const string Permission = "premiumquarry.use";

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(Permission, this);
        }
        
        private void OnQuarryToggled(MiningQuarry quarry, BasePlayer player)
        {
            if (quarry == null || player == null || player.IPlayer.HasPermission(Permission)) return;
            
            quarry.SetOn(!quarry.IsOn());
            
            SendNotify(player, NoPermission, 1);
        }

        #endregion

        #region Lang

        private const string
            NoPermission = "NoPermission";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [NoPermission] = "You can't use the quarry, you don't have permission!"
            }, this);
        }
        
        private string Msg(BasePlayer player, string key, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
        }

        private void Reply(BasePlayer player, string key, params object[] obj)
        {
            player.ChatMessage(Msg(player, key, obj));
        }

        private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
        {
            if (UINotify)
                UINotify?.Call("SendNotify", player, type, Msg(player, key, obj));
            else
                Reply(player, key, obj);
        }

        #endregion
    }
}