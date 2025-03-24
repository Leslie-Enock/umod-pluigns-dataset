using System;
using System.Diagnostics;
using Newtonsoft.Json;

namespace Oxide.Plugins;

[Info("Sleep Protection", "Notchu", "1.0.5")]
[Description("Protect players while sleeping")]
public class SleepProtection : CovalencePlugin
{

    private class Configuration
    {
        [JsonProperty("trigger animal on sleeping player")]
        public bool TriggerAnimalOnPlayer { get; set; } = false;
        
        [JsonProperty("trigger npc player on sleeping player")]
        public bool TriggerNpcOnPlayer { get; set; } = false;

        [JsonProperty("remove sleeping players from animal targeting")]
        public bool RemoveTargetFromAnimal { get; set; } = true;

        [JsonProperty("remove sleeping players from npc players targeting")]
        public bool RemoveTargetFromNpcPlayer { get; set; } = true;

        [JsonProperty("cancel damage to sleeping player")]
        public bool CancelDamageFromPlayersToSleepers { get; set; } = true;

        [JsonProperty("cancel looting of sleeping player")]
        public bool CancelLootingSleepers { get; set; } = true;
    }
    private Configuration _config;

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

    void Init()
    {
        permission.RegisterPermission("sleepprotection.use", this);
        permission.RegisterPermission("sleepprotection.ignore", this);
        if (!_config.CancelLootingSleepers)
        {
            Unsubscribe(nameof(CanLootPlayer));
        }

        if (!_config.RemoveTargetFromAnimal && !_config.CancelDamageFromPlayersToSleepers)
        {
            Unsubscribe(nameof(OnEntityTakeDamage));
        } 
    }
    
    object OnNpcTarget(BaseEntity npc, BasePlayer player)
    {
        if (!player.IsSleeping()) return null;
        
        switch (npc)
        {
            case HumanNPC humanNpc:
                
                if (_config.TriggerNpcOnPlayer && !_config.RemoveTargetFromNpcPlayer)
                    return null;

                if (!CheckPermission(player.UserIDString))
                    return null;
                
                if (_config.RemoveTargetFromNpcPlayer)
                    RemovePlayerFromTarget(humanNpc.Brain.Events, player);
      
                if (!_config.TriggerNpcOnPlayer)
                    return false;
         
                return null;

            case BaseAnimalNPC animalNpc:
                if (_config.TriggerAnimalOnPlayer && !_config.RemoveTargetFromAnimal)
                    return null;

                if (!CheckPermission(player.UserIDString))
                    return null;

                if (_config.RemoveTargetFromAnimal)
                    RemovePlayerFromTarget(animalNpc.brain.Events, player);

                if (!_config.TriggerAnimalOnPlayer)
                    return false;

                return null;
            default:
                return null;
        }
    }


    object OnEntityTakeDamage(BasePlayer player, HitInfo info)
    {
        if (!player.IsSleeping()) return null;

        switch (info.Initiator)
        {
            case BaseAnimalNPC animal:
                if (!CheckPermission(player.UserIDString)) return null;
                
                RemovePlayerFromTarget(animal.brain.Events, player);
                return false;
            case BasePlayer initiator:
                if (!_config.CancelDamageFromPlayersToSleepers || !CheckPermission(player.UserIDString)) return null;
                
                if (CheckPermission(initiator.UserIDString, "ignore"))
                    return null;

                return false;
            default:
                return null;
        }
    }
    
    object CanLootPlayer(BasePlayer target, BasePlayer looter)
    {
        if (target.IsSleeping() && CheckPermission(target.UserIDString) 
                                && !CheckPermission(looter.UserIDString, "ignore")) return false;
        
        return null;
    }
    
    void RemovePlayerFromTarget(AIEvents events, BasePlayer player)
    {
        for (int i = 0; i < 8; i++)
        {
            var entity = events.Memory.Entity.Get(i);
            if (entity != player) continue;
            events.Memory.Entity.Remove(i);
        }
    }
    bool CheckPermission(string playerId, string type = "use")
    {
        return permission.UserHasPermission(playerId, $"sleepprotection.{type}");
    }
}