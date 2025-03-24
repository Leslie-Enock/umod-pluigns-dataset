using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Data.SQLite;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using Facepunch.Models;
using ConVar;
using System.Collections.Specialized;


namespace Oxide.Plugins
{


    [Info("Group Bans", "Zeeuss", "0.1.4")]
    [Description("Plugin that allows you to temporarily or permamently ban/unban players from certain oxide groups.")]
    public class GroupBans : CovalencePlugin
    {

        #region Initialization
        const string groupBanUse = "groupbans.use";
        const string groupBanProtect = "groupbans.protect";
        static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

        void Init()
        {
            // Register permissions
            permission.RegisterPermission(groupBanUse, this);
            permission.RegisterPermission(groupBanProtect, this);

        }
        #endregion

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPerms"] = "You don't have permission to use this command!",
                ["BanSyntax"] = "Syntax: /gban oxide_group time",
                ["UnbanSyntax"] = "Syntax: /gunban oxide_group",
                ["GroupNotExist"] = "Group with this name does not exist!",
                ["BanReason"] = "Your group has been banned!",
                ["ReplyPerma"] = "You have permabanned: {0}!",
                ["ReplyTempBan"] = "You have banned {0} for {1} seconds!",
                ["ReplyUnban"] = "You have unbanned: {0}!"

            }, this);
        }
        #endregion

        #region Commands
        [Command("gban")]
        void chatBanComm(IPlayer player, string command, string[] args)
        {

            if (!permission.UserHasPermission(player.Id, groupBanUse))
            {
                Message(player, Lang("NoPerms", player.Id));
                return;
            }


            // arg 0 = group
            // arg 1 = time - in secs
            if (args.Length == 0)
            {
                Message(player, Lang("BanSyntax", player.Id));
                return;
            }
            else if (args[0] == null)
            {
                Message(player, Lang("BanSyntax", player.Id));
                return;
            }

            if (!permission.GroupExists(args[0]))
            {
                Message(player, Lang("GroupNotExist", player.Id));
                return;
            }



            List<string> idsToBan = new List<string>();
            foreach (var plInGroup in permission.GetUsersInGroup(args[0]))
            {
                string idOfPly = plInGroup.Remove(17);
                idsToBan.Add(idOfPly.ToString());
            }

            foreach(var playerInList in idsToBan)
            {
                if (permission.UserHasPermission(playerInList, groupBanProtect))
                    continue;

                BasePlayer tokick = BasePlayer.FindByID(Convert.ToUInt64(playerInList));

                if (args.Count() <= 1)
                {
                    timer.Once(0.2f, () =>
                    {
                        server.Ban(playerInList, Lang("BanReason", playerInList));
                        if (tokick != null)
                        {
                            tokick.Kick(Lang("BanReason", tokick.UserIDString));
                        }
                    });
                    
                }
                else if (args[1].Length >= 1)
                {
                    timer.Once(0.2f, () =>
                    {
                        ServerUsers.Set(Convert.ToUInt64(playerInList), ServerUsers.UserGroup.Banned, "Group Bans", Lang("BanReason", playerInList), (long)GrabCurrentTime() + Convert.ToUInt32(args[1]));
                        ServerUsers.Save();

                        if (tokick != null)
                        {
                            tokick.Kick(Lang("BanReason", tokick.UserIDString));
                        }
                    });
                    
                }

            }

            if (player != null)
            {

                if (args.Count() <= 1)
                {

                    Message(player, String.Format(lang.GetMessage("ReplyPerma", this, player.Id), args[0]));

                }
                else
                {
                    Message(player, String.Format(lang.GetMessage("ReplyTempBan", this, player.Id), args[0], args[1]));
                }

            }


        }


        [Command("gunban")]
        void chatUnbanComm(IPlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, groupBanUse))
            {
                Message(player, Lang("NoPerms", player.Id));
                return;
            }


            // arg 0 = group

            if (args.Length == 0)
            {
                Message(player, Lang("UnbanSyntax", player.Id));
                return;
            }
            else if (args == null)
            {
                Message(player, Lang("UnbanSyntax", player.Id));
                return;
            }
            else if (args.Length > 1)
            {
                Message(player, Lang("UnbanSyntax", player.Id));
                return;
            }

            if (!permission.GroupExists(args[0]))
            {
                Message(player, Lang("GroupNotExist", player.Id));
                return;
            }

                foreach (var pltounban in permission.GetUsersInGroup(args[0]))
                {

                    timer.Once(0.2f, () =>
                    {
                        string pltounbanID = pltounban.Remove(17); // Removes everything after 17characters == steamID    FORMAT: STEAMID (name)      <----- it's for the ban input string

                        server.Unban(pltounbanID);
                    });
                    

                }

            Message(player, String.Format(lang.GetMessage("ReplyUnban", this, player.Id), args[0]));

        }

        #endregion

        #region Helpers
        private void Message(IPlayer player, string message)
        {
            player.Message(message);
        }

        private string Lang(string key, string id = null)
        {
            return string.Format(lang.GetMessage(key, this, id));
        }

        string GetMsg(string key, params object[] args)
        {

            if (args.Length != 0)
            {
                return string.Format(lang.GetMessage(key, this));
            }
            else
            {
                return string.Format(lang.GetMessage(key, this));
            }
        }

        #endregion





    }
}