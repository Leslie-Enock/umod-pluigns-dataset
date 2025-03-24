// Requires: ZoneManager

using System.Collections.Generic;
using System.Linq;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Zone Chat Prefix", "BuzZ", "0.0.5")]
    [Description("Adds a zone prefix to player chat")]
    public class ZoneChatPrefix : RustPlugin
    {
        [PluginReference]     
        Plugin ZoneManager;
        
        bool debug = false;

        public List<BasePlayer> blabla = new List<BasePlayer>();

#region ZONEMANAGER HOOKS

        string StringZonesPlayerIsIn(BasePlayer player)
        {
            string[] array = (string[]) ZoneManager.Call("GetPlayerZoneIDs", player);
            string message = string.Empty;
            if (array == null)
            {
                return "NOPE";
            }
            else
            {
                if (debug) Puts($"Count {array.Count()} ZONE(s)");
                int round = 1;
                int Round = 1;
                for (Round = 1; round <= array.Count() ; Round++)            
                {
                    string zone_name = GetThatZoneNamePlease(array[round-1]);
                    if (string.IsNullOrEmpty(message))
                    {
                        if (zone_name == "NOTFOUND")
                        {
                            message = $"[{array[round-1]}]";
                        }
                        else message = $"[{zone_name}]";
                    }
                    else
                    {
                        if (zone_name == "NOTFOUND")
                        {
                            message = $"{message} [{array[round-1]}]";
                        }
                        else message = $"{message} [{zone_name}] ";
                    }
                    if (debug) Puts($"{player.userID} - {player.displayName}");
                    if (debug) Puts($"round {round}");
                    round = round + 1;
                }
                return message;
            }
        }

        string GetThatZoneNamePlease (string zone_id)
        {
            string zone_name = (string)ZoneManager.Call("GetZoneName", zone_id);
            if (debug) Puts($"zone_name {zone_name}");
            if (string.IsNullOrEmpty(zone_name)) return "NOTFOUND";
            else return zone_name;
        }

#endregion
#region PLAYERCHAT
        object OnPlayerChat(BasePlayer player, string message)
        {
            if (debug) Puts("OnPlayerChat");
            if (blabla.Contains(player)) return null;
            string zones = StringZonesPlayerIsIn(player);
            if (zones != "NOPE")
            {
                string playername = player.displayName + " ";
                if (player.net.connection.authLevel == 2) playername = $"<color=green>{playername}</color>";
                if (debug) Puts("OnPlayerChat - is in zone(s) !");
                Server.Broadcast(message, zones +" " + playername , player.userID);
                blabla.Add(player);
                timer.Once(1f, () =>
                {
                    blabla.Remove(player);                    
                });
                return true;
            }
            else
            {
                if (debug) Puts("Not in Zone");
                return null;
            }
        }
#endregion
    }
}
