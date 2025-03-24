using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Core;
using System.Collections.Generic;
using System;

namespace Oxide.Plugins
{
    [Info("Life Support", "OG61", "1.5.0")]
    [Description("Use reward points to prevent player from dying")]

    //Change log
    //1.4.0 Moved code from OnPlayerDeath to SaveLife and call from OnPlayerDeath and OnPlayerWound (when health below 1)
    //1.5.0 Added ability to suspend LifeSupport in DangerousTreasures zones and added support for zone manager.
    public class LifeSupport : CovalencePlugin
    {
        #region Plugin References
        [PluginReference]
        private Plugin RaidableBases, ServerRewards, DangerousTreasures, ZoneManager;
        
        #endregion

        #region Config
        private const string PERMISSION_BLOCKED = "lifesupport.blocked";

        private class Perms
        {
            public string Permission { get; set; }
            public int Cost { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Use Zone Manager (true/false)")]
            public bool UseZoneManager = false;

            [JsonProperty(PropertyName = "Use Server Rewards (true/false)")]
            public bool UseServerRewards = false;

            [JsonProperty(PropertyName = "Disable LifeSupport in RaidableBases Zones (true/false)")]
            public bool UseRaidableBases = false;

            [JsonProperty(PropertyName = "Disable LifeSupport in DangerousTreasures Zones (true/false)")]
            public bool UseDangerousTreasures = false;

            [JsonProperty(PropertyName = "Enable Log file (true/false)")]
            public bool LogToFile = true;

            [JsonProperty(PropertyName = "Log output to console (true/false)")]
            public bool LogToConsole = true;

            [JsonProperty(PropertyName = "Permissions and cost", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<Perms> perms = new List<Perms>()
            {
                new Perms() {Permission = "lifesupport.default", Cost = 400 },
                new Perms() {Permission = "lifesupport.vip", Cost = 200},
                new Perms() {Permission = "lifesupport.admin", Cost = 0}
            };
            
        }

        private PluginConfig config;

        protected override void LoadConfig()
        {
             base.LoadConfig();
            try
            {
                config = Config.ReadObject<PluginConfig>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
                Logger("ConfigError");
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = new PluginConfig();

        protected override void SaveConfig() => Config.WriteObject(config);

        private void OnPluginLoaded(Plugin plugin)
        {
            switch (plugin.Title)
            {
                case "RaidableBases":
                    {
                        RaidableBases = plugin;
                        break;
                    }
                case "DangerousTreasures":
                    {
                        DangerousTreasures = plugin;
                        break;
                    }
                case "ServerRewards":
                    {
                        ServerRewards = plugin;
                        break;
                    }
                case "ZoneManager":
                    {
                        ZoneManager = plugin;
                        break;
                    }
            }
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            switch (plugin.Title)
            {
                case "RaidableBases":
                    {
                        RaidableBases = null;
                        break;
                    }
                case "DangerousTreasures":
                    {
                        DangerousTreasures = null;
                        break;
                    }
                case "ServerRewards":
                    {
                        ServerRewards = null;
                        break;
                    }
                case "ZoneManager":
                    {
                        ZoneManager = null;
                        break;
                    }
            }
        }

        #endregion //Config

        #region Data
        private Data data;

        private class Data
        {
            public List<string> activatedIDs = new List<string>();
            public List<string> excludedZones = new List<string>(); 
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, data);


        #endregion //Data

        #region Oxide Hooks
        private void Loaded()
        {
            data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);

            config.perms.ForEach(p =>
            {
                    permission.RegisterPermission(p.Permission, this);
            });
            permission.RegisterPermission(PERMISSION_BLOCKED, this);
        }
        
        //Prevent player from entering wounded state
        private object OnPlayerWound(BasePlayer player)
        {
           if (player == null) return null;
           if (permission.UserHasPermission(player.UserIDString, PERMISSION_BLOCKED)) return null;
           if (config.UseRaidableBases)
           {
               if (RaidableBases != null && RaidableBases.IsLoaded)
               {
                    if (RaidableBases.Call<bool>("EventTerritory", player.transform.position))
                    {
                        Message("ExcludedZone", player.IPlayer);
                        Logger("RBZone", player.IPlayer);
                        return null;
                    }
               }
            }

            if (config.UseDangerousTreasures)
            {
                if (DangerousTreasures != null && DangerousTreasures.IsLoaded)
                {
                    if (DangerousTreasures.Call<bool>("EventTerritory", player.transform.position))
                    {
                        Message("ExcludedZone", player.IPlayer);
                        Logger("DTZone",player.IPlayer);
                        return null;
                    }
                }
            }

            if (config.UseZoneManager)
            {
                if (ZoneManager != null && ZoneManager.IsLoaded)
                {
                    foreach (string s in data.excludedZones)
                        if (ZoneManager.Call<bool>("IsPlayerInZone", s, player))
                        {
                            Message("ExcludedZone", player.IPlayer);
                            Logger("ZMZone",player.IPlayer);
                            return null;
                        }
                }
            }

            bool preventWounded = false;
            
            config.perms.ForEach(p =>
            {
                 if (permission.UserHasPermission(player.UserIDString, p.Permission)&&
                     data.activatedIDs.Contains(player.UserIDString)) 
                {
                    preventWounded = true;
                }
            });

            if (preventWounded) 
            {
                if (player.health < 1) return SaveLife(player); else return true;
               
            }
            else return null;
        }

       
        private object OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            return (SaveLife(player));
        }

        bool CanDropActiveItem(BasePlayer player)
        {
            try
            {
                if (player == null || player.IsNpc) return true;

                if (config.UseRaidableBases)
                {
                    if (RaidableBases != null && RaidableBases.IsLoaded)
                    {
                        if (RaidableBases.Call<bool>("EventTerritory", player.transform.position)) return true;
                    }
                }

                if (config.UseDangerousTreasures)
                {
                    if (DangerousTreasures != null && DangerousTreasures.IsLoaded)
                    {
                        if (DangerousTreasures.Call<bool>("EventTerritory", player.transform.position)) return true;
                    }
                }

                if (config.UseZoneManager)
                {
                    if (ZoneManager != null && ZoneManager.IsLoaded)
                    {
                        foreach (string s in data.excludedZones)
                            if (ZoneManager.Call<bool>("IsPlayerInZone", s, player)) return true;
                    }
                }
                return data.activatedIDs.Contains(player.UserIDString) ? false : true;
            }
            catch (System.Exception ex)
            {
                Puts("CanDropActiveItem trew Exception: " + ex) ;
                throw;
            }
        }

        #endregion //Oxide Hooks  

        #region Helpers

        private object SaveLife(BasePlayer player)
        {
            if (player == null || player.IsNpc || player.IsSleeping()) return null;
            if (permission.UserHasPermission(player.UserIDString, PERMISSION_BLOCKED)) return null;
            if (Interface.CallHook("OnLifeSupportSavingLife", player) != null)
            {
                Logger("CanceledByPlugin", player.IPlayer);
                return null;
            }
            bool preventDeath = false;
            int costOfLife = int.MaxValue;
            if (config.UseRaidableBases)
            {
                if (RaidableBases != null && RaidableBases.IsLoaded)
                {
                    if (RaidableBases.Call<bool>("EventTerritory", player.transform.position)) return null;
                }
            }
            if (config.UseDangerousTreasures)
            {
                if (DangerousTreasures != null && DangerousTreasures.IsLoaded)
                {
                    if (DangerousTreasures.Call<bool>("EventTerritory", player.transform.position)) return null;
                }
            }
            if (config.UseZoneManager)
            {
                if (ZoneManager != null && ZoneManager.IsLoaded)
                {
                    foreach(string s in data.excludedZones)
                    if (ZoneManager.Call<bool>("IsPlayerInZone", s, player)) return null;
                }
            }
            if (data.activatedIDs.Contains(player.UserIDString))
            {
                config.perms.ForEach(p =>
                {
                    if (permission.UserHasPermission(player.UserIDString, p.Permission))
                    {
                        preventDeath = true;
                        if (p.Cost < costOfLife) costOfLife = p.Cost;
                    }
                });
                if (!preventDeath) return null; //Player does not have permission so exit
                if (config.UseServerRewards)
                {
                    if (ServerRewards == null || !ServerRewards.IsLoaded)
                    {//Server Rewards enabled but not present. Log error and return
                        Message("ServerRewardsNull", player.IPlayer);
                        Logger("ServerRewardsNull");
                        return null;
                    }
                    if (costOfLife > 0)
                    {//object object CheckPoints(ulong ID) // Returns int, or null if no data is saved
                        var rp = ServerRewards.Call("CheckPoints", player.userID);
                        if ((rp is int ? (int)rp : 0) >= costOfLife)
                        {
                            ServerRewards.Call("TakePoints", player.UserIDString, costOfLife);
                        }
                        else
                        {
                            Message("CantAfford", player.IPlayer);
                            Logger("DiedCouldntAfford", player.IPlayer);
                            return null; //Player can't afford so exit
                        }
                    }
                    Message("SavedYourLifeCost", player.IPlayer, costOfLife);
                    Logger("SavedLife", player.IPlayer, costOfLife);
                }
                else //Not using ServerRewards
                {
                    Message("SavedYourLife", player.IPlayer);
                    Logger("ServerRewardsInactiveSavedLife", player.IPlayer);
                }
                player.health = 100f;
                if (player.IsWounded()) player.StopWounded();
                return true;
            }
            Logger("DiedNotActive", player.IPlayer);
            return null; //Life Support not activated for this player so exit
        }
        private string GetLang(string key, string id = "", params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        private void Logger(string key, IPlayer player = null, params object[] args)
        {
            string s = GetLang(key, player != null ? player.Id : "", args);
            string ps = "";
            if (player != null) ps = $"{player.Name} ({player.Id}) ";
            s = $"[{DateTime.Now}] {ps} {s}";
            if (config.LogToFile) LogToFile("LifeSupport", s, this);
            if (config.LogToConsole) Puts(s);
        }

        private void Message(string key, IPlayer player, params object[] args)
        {
            if (player != null)
            {
                player.Reply(GetLang(key, player.Id, args));
            }
        }

        #endregion //Helpers
            
        #region Commands
        [Command("lifesupport")]
        private void LifeSupportToggle(IPlayer player, string msg, string[] args)
        {
            if (permission.UserHasPermission(player.Id, PERMISSION_BLOCKED))
            {
                Message("NoPermission", player);
                return;
            }
            int costOfLife = int.MaxValue;
            bool hasPermission = false;

            if (args.Length > 0)
            {
                if (args.Length == 1 && args[0].ToLower() == "help")
                {
                    Message("Help", player);
                    return;
                }
                Message("DontUnderstand", player);
                return;
            }
            config.perms.ForEach(p =>
            {
                if (permission.UserHasPermission(player.Id, p.Permission))
                {
                    hasPermission = true;
                    if (p.Cost < costOfLife) costOfLife = p.Cost; //Get the lowest cost if player has multiple permissions
                }
            });

            if (data.activatedIDs.Contains(player.Id))
            {
                data.activatedIDs.Remove(player.Id);
                Message("Deactivated", player);
                Logger("Deactivated" , player);
                SaveData();
            }
            else if(hasPermission) 
            {
                data.activatedIDs.Add(player.Id);
                Message("Activated", player,  config.UseServerRewards ? costOfLife: 0);
                Logger("Activated", player, costOfLife);
                SaveData();
            }
            else Message("NoPermission", player);
        }

        [Command("lsZones")]
        private void LsZones(IPlayer player, string msg, string[] args)
        {
            //ToDo Limit to admins
            if (permission.UserHasPermission(player.Id, "lifesupport.admin"))
                {

                if (args.Length > 0)
                {
                    switch (args[0].ToLower())
                    {
                        case "add":
                            for (int i = 1; i < args.Length; i++)
                            {
                                data.excludedZones.Add(args[i]);
                            }
                            SaveData();
                            Message("ZoneAdded", player);
                            for (int i = 1; i < args.Length; i++)
                            {
                                Message(args[i], player);
                            }
                            break;
                        case "remove":
                            for (int i = 1; i < args.Length; i++)
                            {
                                data.excludedZones.Remove(args[i]);
                            }
                            SaveData();
                            Message("ZonesRemoved", player);
                            for (int i = 1; i < args.Length; i++)
                            {
                                Message(args[i], player);
                            }
                            break;
                        case "clear":
                            data.excludedZones.Clear();
                            SaveData();
                            Message("ZonesCleared", player);
                            break;
                        case "list":
                            Message("ListZones", player);
                            foreach (string s in data.excludedZones)
                            {
                                Message(s, player);
                            }
                            break;

                        default:
                            Message("DontUnderstand", player);
                            
                            break;
                    }
                    return;
                } 
            }else
            {
                Message("NoPermission", player);
                Logger("NoPermission", player);
                return;
            }
        }
        #endregion
        
        #region Localization
     
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ZoneAdded"] = "Added the following zone(s) to LifeSupport exclusionary list",
                ["ZoneRemoved"] = "Removed the following zone(s) from LifeSupport exclusionary list",
                ["ZonesCleared"] = "Cleared all zones from LifeSupport exclusionary list",
                ["ListZones"] = "LifeSupport is disabled in the following zones",
                ["ExcludedZone"] = "LifeSupport is disabled in this zone",
                ["RBZone"] = "LifeSupport is disabled in Raidable Bases zone",
                ["DTZone"] = "LifeSupport disabled in Dangerous Treasures zone",
                ["ZMZone"] = "LifeSupport disabled in Zone Manager zone",
                ["CanceledByPlugin"] = "LifeSupport canceled by another plugin",
                ["ServerRewardsNull"] = "LifeSupport could not save your life. \n"+
                "ServerRewards is enabled but the ServerRewards plugin is not available.",
                ["DontUnderstand"] = "Don't Understand.",
                ["DiedNotActive"] = "Player died. LifeSupport not active.",
                ["ConfigError"] = "Error reading config file. Defaut configuration used.",
                ["NoPermission"] = "You do not have permission to use this command.",
                ["CantAfford"] = "Sorry, insufficent reward points to use LifeSupport.",
                ["DiedCouldntAfford"] = "Player died. Could not afford LifeSupport.",
                ["Deactivated"] = "Life Support de-activated.",
                ["Activated"] = "Life Support activated.  Cost per life {0} RP",
                ["SavedYourLifeCost"] = "Life Support saved your life. Cost: {0} RP",
                ["SavedYourLife"] = "Life Support saved your life.",
                ["SavedLife"] = "Prevented death. Cost: {0} RP",
                ["ServerRewardsInactiveSavedLife"] = "Prevented death. Server Rewards inactive",
                ["Help"] = "When active LifeSupport will prevent a player's death if\n" +
                "they have permission and a sufficent amount of reward points \n" +
                "or if Server Rewards is turned off. \n" +
                "It also prevents dropping their active item.\n" +
                "Type /LifeSupport in chat to toggle on and off."
             }, this);
        }
        #endregion
    }
}