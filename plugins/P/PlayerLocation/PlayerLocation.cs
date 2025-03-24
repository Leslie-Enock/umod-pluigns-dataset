using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Player Location", "seacowz", "0.1.7")]
    [Description("Find the location of players on the server")]
    public class PlayerLocation : CovalencePlugin
    {
        #region Variables

        private class PlayerData
        {
            public DateTime lastawaketime { get; set; }
            public DateTime lastdeathtime { get; set; }
            public Vector3 lastdeathloc { get; set; }

            public PlayerData()
            {
                lastawaketime = DateTime.MinValue;
                lastdeathtime = DateTime.MinValue;
                lastdeathloc = Vector3.zero;
            }
        }
        
        private Dictionary<string, PlayerData> playerdict = new Dictionary<string, PlayerData>();

        private const string permAdmin = "playerlocation.admin";

        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoPermission"] = "You do not have permission to use that command.",
                ["NotFound"] = "No players found.",
                ["Usage"] = "Usage: location [-d] [name|id]"
            }, this);
        }

        #endregion Localization

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(permAdmin, this);
            
            LoadDataFile();
            SaveDataFile();
        }

        private void OnPlayerSleep(BasePlayer player)
        {
            PlayerData playerdata;
            if (playerdict.TryGetValue(player.UserIDString, out playerdata))
            {
                playerdata.lastawaketime = DateTime.Now;
            }
            else
            {
                playerdata = new PlayerData();
                playerdata.lastawaketime = DateTime.Now;
                playerdict[player.UserIDString] = playerdata;
            }
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            PlayerData playerdata;
            if (playerdict.TryGetValue(player.UserIDString, out playerdata))
            {
                playerdata.lastawaketime = DateTime.MinValue;
            }
            else
            {
                playerdata = new PlayerData();
                playerdata.lastawaketime = DateTime.MinValue;
                playerdict[player.UserIDString] = playerdata;
            }
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            PlayerData playerdata;
            Vector3 location = player.transform.position;
            if (playerdict.TryGetValue(player.UserIDString, out playerdata))
            {
                playerdata.lastdeathtime = DateTime.Now;
                playerdata.lastdeathloc = location;
            }
            else
            {
                playerdata = new PlayerData();
                playerdata.lastdeathtime = DateTime.Now;
                playerdata.lastdeathloc = location;
                playerdict[player.UserIDString] = playerdata;
            }
        }

        void Unload()
        {
            SaveDataFile();
        }

        #endregion hooks

        #region Commands

        [Command("location")]
        private void LocationCmd(IPlayer player, string command, string[] args)
        {
            if (!player.HasPermission(permAdmin))
            {
                player.Reply(lang.GetMessage("NoPermission", this, player.Id));
                return;
            }

            List<Tuple<double, string>> locationlist = new List<Tuple<double, string>>();
            switch (args.Length)
            {
                case 0:
                    FindLocation(player, string.Empty, ref locationlist);
                    break;
                case 1:
                    if (args[0] == "-d")
                    {
                        FindDLocation(player, string.Empty, ref locationlist);
                    }
                    else
                    {
                        FindLocation(player, args[0], ref locationlist);
                    }
                    break;
                case 2:
                    if (args[0] == "-d")
                    {
                        FindDLocation(player, args[1], ref locationlist);
                    }
                    else
                    {
                        player.Reply(lang.GetMessage("Usage", this, player.Id));
                    }
                    break;
                default:
                    player.Reply(lang.GetMessage("Usage", this, player.Id));
                    break;
            }

            if (locationlist.IsEmpty())
            {
                player.Reply(lang.GetMessage("NotFound", this, player.Id));
            }
            else
            {
                foreach (Tuple<double, string> item in locationlist.OrderByDescending(i => i.Item1))
                {
                    player.Reply(item.Item2);
                }
            }
        }

        #endregion Commands

        #region Datafile

        private void LoadDataFile()
        {
            DynamicConfigFile file = Interface.Oxide.DataFileSystem.GetDatafile(Name);
            try
            {
                playerdict = file.ReadObject<Dictionary<string, PlayerData>>();
            }
            catch
            {
                playerdict = new Dictionary<string, PlayerData>();
            }
        }

        private void SaveDataFile()
        {
            DynamicConfigFile file = Interface.Oxide.DataFileSystem.GetDatafile(Name);
            file.WriteObject<Dictionary<string, PlayerData>>(playerdict);
        }

        #endregion Datafile

        #region Helper

        private void FindLocation(IPlayer callingplayer, string search, ref List<Tuple<double, string>> locationlist)
        {
            bool console = false;

            if (callingplayer.LastCommand == CommandType.Console)
            {
                console = true;
            }
            
            foreach (BasePlayer player in BasePlayer.allPlayerList)
            {
                string options = string.Empty;
                string name = player.displayName;
                string id = player.UserIDString;
                PlayerData playerdata;
                double sleeptime = 0;

                if (player.IsNpc)
                {
                    continue;
                }

                if (search.Length > 0 && name.Contains(search, System.Globalization.CompareOptions.IgnoreCase) == false && id.Contains(search) == false)
                {
                    continue;
                }

                if (!playerdict.TryGetValue(id, out playerdata))
                {
                    playerdata = new PlayerData();
                    playerdict[id] = playerdata;
                }

                if (player.IsSleeping())
                {
                    if (playerdata.lastawaketime != DateTime.MinValue)
                    {
                        TimeSpan diff = DateTime.Now - playerdata.lastawaketime;
                        sleeptime = diff.TotalSeconds;
                    }

                    if (!console || sleeptime <= 0)
                    {
                        options += " (sleeping)";
                    }
                    else if (sleeptime < 3600) // hour
                    {
                        options += " (sleeping " + Math.Floor(sleeptime / 60) + " minutes)";
                    }
                    else if (sleeptime < 86400) // day
                    {
                        options += " (sleeping " + Math.Floor(sleeptime / 3600) + " hours)";
                    }
                    else
                    {
                        options += " (sleeping " + Math.Floor(sleeptime / 86400) + " days)";
                    }
                }
                else if (player.IsDead())
                {
                    options += " (dead)";
                }
                else if (player.IsFlying)
                {
                    options += " (flying)";
                }

                if (player.IsAdmin)
                {
                    options += " (admin)";
                }

                if (player.IsGod())
                {
                    options += " (god)";
                }

                Vector3 location = player.transform.position;

                string pos = MapPosition(location);
                string locationstr;

                if (console)
                {
                    locationstr = string.Concat(id, " ", name, options, ": ", pos, ", ", location.x, ", ", location.z);
                }
                else
                {
                    locationstr = string.Concat(name, options, ": ", pos, ", ", location.x, ", ", location.z);
                }

                locationlist.Add(Tuple.Create(sleeptime, locationstr));
            }
        }

        private void FindDLocation(IPlayer callingplayer, string search, ref List<Tuple<double, string>> locationlist)
        {
            bool console = false;

            if (callingplayer.LastCommand == CommandType.Console)
            {
                console = true;
            }

            foreach (BasePlayer player in BasePlayer.allPlayerList)
            {
                string options = string.Empty;
                string name = player.displayName;
                string id = player.UserIDString;
                PlayerData playerdata;
                double deathtime = 0;

                if (player.IsNpc)
                {
                    continue;
                }

                if (search.Length > 0 && name.Contains(search, System.Globalization.CompareOptions.IgnoreCase) == false && id.Contains(search) == false)
                {
                    continue;
                }

                if (!playerdict.TryGetValue(id, out playerdata))
                {
                    playerdata = new PlayerData();
                    playerdict[id] = playerdata;
                }

                if (playerdata.lastdeathtime == DateTime.MinValue)
                {
                    continue;
                }

                TimeSpan diff = DateTime.Now - playerdata.lastdeathtime;
                deathtime = diff.TotalSeconds;

                if (!console || deathtime <= 0)
                {
                    options += " (died)";
                }
                else if (deathtime < 3600) // hour
                {
                    options += " (died " + Math.Floor(deathtime / 60) + " minutes ago)";
                }
                else if (deathtime < 86400) // day
                {
                    options += " (died " + Math.Floor(deathtime / 3600) + " hours ago)";
                }
                else
                {
                    options += " (died " + Math.Floor(deathtime / 86400) + " days ago)";
                }

                Vector3 location = playerdata.lastdeathloc;

                string pos = MapPosition(location);
                string locationstr;

                if (console)
                {
                    locationstr = string.Concat(id, " ", name, options, ": ", pos, ", ", location.x, ", ", location.z);
                }
                else
                {
                    locationstr = string.Concat(name, options, ": ", pos, ", ", location.x, ", ", location.z);
                }

                locationlist.Add(Tuple.Create(deathtime, locationstr));
            }
        }

        private string MapPosition (Vector3 position)
        {
            string[] chars = new string[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M", "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z", "AA", "AB", "AC", "AD", "AE", "AF", "AG", "AH", "AI", "AJ", "AK", "AL", "AM", "AN", "AO", "AP", "AQ", "AR", "AS", "AT", "AU", "AV", "AW", "AX", "AY", "AZ" };

            const float block = 146;

            float size = ConVar.Server.worldsize;
            float offset = size / 2;

            float xpos = position.x + offset;
            float zpos = position.z + offset;

            int maxgrid = (int)(size / block);

            float xcoord = Mathf.Clamp(xpos / block, 0, maxgrid - 1);
            float zcoord = Mathf.Clamp(maxgrid - (zpos / block), 0, maxgrid - 1);

            string pos = string.Concat(chars[(int)xcoord], (int)zcoord);

            return (pos);
        }

        #endregion Helper
    }
}
