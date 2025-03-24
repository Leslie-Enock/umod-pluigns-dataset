using System.Collections.Generic;
using System.Text;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

/*********************************************************************************************
 * Thx to Kappasaurus the creator of this plugin upto v1.0.2
 *********************************************************************************************
 * 1.0.4    :   Fixed some formatting
 *          :   Optimised usage of Covalance in the cupowner command
 *          :   Updated the language file ("Player List" is now "AuthedPlayers")
 *          :   Added cfg to change prefix and chaticon instead of hardcoded
 *          :   Added support for the new TC skin
 ********************************************************************************************/



namespace Oxide.Plugins
{
    [Info("CupboardList", "Krungh Crow", "1.0.4")]

    class CupboardList : RustPlugin
    {
        private const string Prefab = "cupboard.tool.deployed";
        private const string Prefab2 = "cupboard.tool.retro.deployed";
        private string msg(string key, string id = null) => lang.GetMessage(key, this, id);
        private string prefix;
        private ulong chaticon;

        private CupboardListConfig config;

        void Init()
        {
            permission.RegisterPermission("cupboardlist.able" , this);
            LoadDefaultConfig();
            LoadConfigValues();
            LoadDefaultMessages();
            prefix = config.Prefix;
            chaticon = config.ChatIcon;
        }

        void LoadConfigValues()
        {
            config = Config.ReadObject<CupboardListConfig>();
        }

        protected override void LoadDefaultConfig()
        {
            config = new CupboardListConfig();
            Config.WriteObject(config , true);
        }

        class CupboardListConfig
        {
            public string Prefix { get; set; } = "<color=yellow>[Cupboard List]</color>";
            public ulong ChatIcon { get; set; } = 0;
        }

        [ChatCommand("cupauth")]
        void AuthCmd(BasePlayer player , string command , string[] args)
        {
            if (!permission.UserHasPermission(player.userID.ToString() , "cupboardlist.able"))
            {
                Player.Message(player , prefix + string.Format(msg("No Permission" , player.UserIDString)) , chaticon);
                return;
            }

            var targetEntity = GetViewEntity(player);

            if (!IsCupboardEntity(targetEntity))
            {
                Player.Message(player , prefix + string.Format(msg("Not a Cupboard" , player.UserIDString)) , chaticon);
                return;
            }

            var cupboard = targetEntity.gameObject.GetComponentInParent<BuildingPrivlidge>();

            if (cupboard.authorizedPlayers.Count == 0)
            {
                Player.Message(player , prefix + string.Format(msg("No Players" , player.UserIDString)) , chaticon);
                return;
            }

            var playerList = new StringBuilder();
            foreach (ProtoBuf.PlayerNameID playerNameOrID in cupboard.authorizedPlayers)
            {
                playerList.AppendLine($"<color=green>{playerNameOrID.username}</color> ({playerNameOrID.userid})");
            }

            Player.Message(player , $"{prefix}{string.Format(msg("AuthedPlayers" , player.UserIDString).Replace("{authList}" , playerList.ToString()))}" , chaticon);
        }

        [ChatCommand("cupowner")]
        void OwnerCmd(BasePlayer player , string command , string[] args)
        {
            if (!permission.UserHasPermission(player.userID.ToString() , "cupboardlist.able"))
            {
                Player.Message(player , prefix + string.Format(msg("No Permission" , player.UserIDString)) , chaticon);
                return;
            }

            var targetEntity = GetViewEntity(player);

            if (!IsCupboardEntity(targetEntity))
            {
                Player.Message(player , prefix + string.Format(msg("Not a Cupboard" , player.UserIDString)) , chaticon);
                return;
            }

            var cupboard = targetEntity.gameObject.GetComponentInParent<BuildingPrivlidge>();

            IPlayer owner = covalence.Players.FindPlayerById(cupboard.OwnerID.ToString());

            Player.Message(player , prefix + msg("Owner" , player.UserIDString).Replace("{player}" , $"\n<color=green>{owner.Name}</color> ({owner.Id})") , chaticon);
        }

        #region Helpers

        private BaseEntity GetViewEntity(BasePlayer player)
        {
            RaycastHit hit;
            bool didHit = Physics.Raycast(player.eyes.HeadRay(), out hit, 5);

            return didHit ? hit.GetEntity() : null;
        }

        private bool IsCupboardEntity(BaseEntity entity) => entity != null && (entity.ShortPrefabName == Prefab || entity.ShortPrefabName == Prefab2);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["No Permission"] = "Sorry, no permission.",
                ["Not a Cupboard"] = "Sorry, that's not a cupboard.",
                ["No Players"] = "Sorry, no players authorized.",
                ["AuthedPlayers"] = "The following player(s) are authorized:\n{authList}",
                ["Owner"] = "Tool cupboard owner: {player}."
            }, this);
        }
        #endregion
    }
}