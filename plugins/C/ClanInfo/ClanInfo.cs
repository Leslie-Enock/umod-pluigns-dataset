// Requires: Clans

using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Core;
using System.Linq;
using System.Collections.Generic;
 
namespace Oxide.Plugins
{
    [Info("Clan Info", "Bazz3l", "1.0.5")]
    [Description("List all clan members in a given clan")]
    class ClanInfo : CovalencePlugin
    {
        #region Plugins
        [PluginReference] Plugin Clans;
        #endregion

        #region Props
        private const string Perm = "claninfo.use";
        #endregion

        #region Oxide
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Message"] = "Clan: [#999999]{0}[/#] Members:\n{1}",
                ["NotFound"] = "No clan found",
                ["NoTag"] = "No clan tag specified"
            }, this);
        }

        private void Init() => permission.RegisterPermission(Perm, this);
        #endregion

        #region Clan
        public JObject GetClan(string tag) => Clans?.Call<JObject>("GetClan", new object[] { tag });
        public JArray GetClanMembers(string tag) => (JArray)GetClan(tag)?.SelectToken("members");
        #endregion

        #region Chat Commands
        [Command("cinfo")]
        private void cmdCinfo(IPlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, Perm)) return;
            if (args.Length < 1)
            {
                player.Reply(Lang("NoTag", player.Id));
                return;
            }

            JArray clanMembers = GetClanMembers(args[0]);
            if (clanMembers == null)
            {
                player.Reply(Lang("NotFound", player.Id));
                return;
            }

            List<string> members = new List<string>();
            foreach(JToken member in clanMembers)
            {
                var mPlayer = covalence.Players?.FindPlayerById((string) member)?.Name;
                if (mPlayer != null)
                    members.Add(mPlayer);
            }

            player.Reply(Lang("Message", player.Id, args[0], string.Join("\n", members.ToArray())));
        }
        #endregion

        #region Helpers
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        #endregion
    }
}