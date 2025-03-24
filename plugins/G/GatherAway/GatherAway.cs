using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins;

[Info("Gather Away", "Notchu", "1.1.2")]
[Description("Automatically hits weak spot while gathering resources")]
internal class GatherAway : CovalencePlugin
{
    private class Configuration
    {
        [JsonProperty("Automatically gather X Markers")] 
        public bool XMarkerGather = true;

        [JsonProperty("Automatically gather Ore Weak spots")] 
        public bool OreWeakSpotsGather = true;

        [JsonProperty("BlackList - false, whitelist - true")]
        public bool ItemList = false;

        [JsonProperty("BlackList(false)")] 
        public List<string> BlackList = new List<string>();
        
        [JsonProperty("Whitelist(true)")] 
        public List<string> WhiteList = new List<string>();

        [JsonProperty("ignore blacklist during period")]
        public bool IgnoreBlackListDuringPeriod  = true;

        [JsonProperty("Give players ability to turn off plugin for themselves")]
        public bool TogglePluginByPlayer = true;

        [JsonProperty("period start hour")]
        public int PeriodStart  = 19;

        [JsonProperty("period end hour")]
        public int PeriodEnd  = 7;
    }
    private Configuration _config;
    
    private HashSet<string> disabledPlayers = new HashSet<string>();
    private const string DataFile = "GatherAwayDisabledPlayers";
    
    private void LoadData()
    {
        disabledPlayers = Interface.Oxide.DataFileSystem.ReadObject<HashSet<string>>(DataFile) ?? new HashSet<string>();
    }

    private void SaveData()
    {
        Interface.Oxide.DataFileSystem.WriteObject(DataFile, disabledPlayers);
    }
        
    protected override void LoadConfig()
    {
        base.LoadConfig();
        try
        {
            _config = Config.ReadObject<Configuration>();
            if (_config == null) throw new Exception();
            SaveConfig();
        }
        catch
        {
            PrintError("Your configuration file contains an error. Using default configuration values.");
            LoadDefaultConfig();
        }
    }
    protected override void SaveConfig() => Config.WriteObject(_config);

    protected override void LoadDefaultConfig() => _config = new Configuration();
    
    protected override void LoadDefaultMessages()
    {
        lang.RegisterMessages(new Dictionary<string, string>
        {
            { "GatherAwayOn", "Now you will hit weak spots of ores and trees" },
            {"GatherAwayOff", "Now you will NOT hit weak spots of ores and trees"},
            {"NoRights", "You don't have rights to use this command." },
        }, this);
    }
    
    void Init()
    {
        if (_config.TogglePluginByPlayer)
        {
            Puts("Enabled plugin by player");
            AddCovalenceCommand("gatheraway", "ToggleGatherAway");
            LoadData(); 
        }
        if (!_config.OreWeakSpotsGather)
        {
            Unsubscribe(nameof(OnPlayerAttack));
        }
        if (!_config.XMarkerGather)
        {
            Unsubscribe(nameof(OnTreeMarkerHit));
        }
        permission.RegisterPermission("gatheraway.use", this);
    }

    void Unload()
    {
        SaveData();
        disabledPlayers.Clear();
    }
    void OnServerSave()
    {
        SaveData();
    }
    object  OnPlayerAttack(BasePlayer player, HitInfo info)
    {
        if (info == null || info.IsProjectile()) return null;
        
        if (player == null || info.HitEntity is not OreResourceEntity ore || info.InitiatorPlayer.IsBot) return null;
        
        if (_config.TogglePluginByPlayer && disabledPlayers.Contains(player.UserIDString)) return null;
        
        if (ore._hotSpot == null) return null;
        
        var userID = player.UserIDString;
        
        switch (_config.ItemList)
        {
            case true:
                if (IsWeaponWhitelisted(info)) break;
                return null;
            case false:
                if (IsWeaponBlacklisted(info))
                {
                    if (!_config.IgnoreBlackListDuringPeriod) return null;
            
                    if (IsInPeriod()) HitWeakSpotOnOre(info, userID, ore);
            
                    return null;
                }
                break;
        }
        
        HitWeakSpotOnOre(info, userID, ore);    

        return null;
    }
    bool? OnTreeMarkerHit(TreeEntity tree, HitInfo info)
    {
        var initiator = info.InitiatorPlayer;
        
        if (initiator.IsBot) return null;
        
        if (_config.TogglePluginByPlayer && disabledPlayers.Contains(initiator.UserIDString)) return null;

        switch (_config.ItemList)
        {
            case true:
                if (IsWeaponWhitelisted(info)) break;
                return null;
            case false:
                if (IsWeaponBlacklisted(info))
                {
                    if (!_config.IgnoreBlackListDuringPeriod) return null;

                    if (IsInPeriod()) break;

                    return null;
                }

                break;
        }
       
        
        if (!permission.UserHasPermission(initiator.UserIDString, "gatheraway.use") ) return null;
        
        return true;
    }

    private bool IsWeaponBlacklisted(HitInfo info)
    {
        return _config.BlackList.Contains(info.Weapon.ShortPrefabName);
    }
    private bool IsWeaponWhitelisted(HitInfo info)
    {
        return _config.WhiteList.Contains(info.Weapon.ShortPrefabName);
    }

    void HitWeakSpotOnOre(HitInfo info, string playerId, OreResourceEntity ore)
    {
        if (!permission.UserHasPermission(playerId, "gatheraway.use")) return;
        
        info.HitPositionWorld = ore._hotSpot.transform.position;
    }

    bool IsInPeriod()
    {
        var time = TOD_Sky.Instance.Cycle.DateTime;

        var hours = time.Hour;

        return hours >= _config.PeriodStart || hours <= _config.PeriodEnd;
    }

    void ToggleGatherAway(IPlayer player, string command, string[] args)
    {
        var userId = player.Id;
        if (_config.TogglePluginByPlayer && permission.UserHasPermission(userId, "gatheraway.use"))
        {
            if (disabledPlayers.Contains(userId))
            {
                disabledPlayers.Remove(userId);
                player.Reply(lang.GetMessage("GatherAwayOn", this));
            }
            else
            {
                disabledPlayers.Add(userId);
                player.Reply(lang.GetMessage("GatherAwayOff", this));
            }

        }
        else
        {
            player.Reply(lang.GetMessage("NoRights", this));
        }
    }
}