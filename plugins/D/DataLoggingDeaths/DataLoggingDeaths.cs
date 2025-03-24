// Requires: DataLogging

using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Data Logging: Kills / Deaths", "Rustoholics", "0.1.3")]
    [Description("Log all kills and deaths")]

    public class DataLoggingDeaths : DataLogging
    {
        #region Object
        public class DeathObject
        {
            public bool Naked;
            public string KillerType;
            public ulong KillerId;
            public string VictimType = "";
            public ulong VictimId;
            public int WeaponId;
            public DateTime Date;
            
            [JsonIgnore]
            public bool Animal { get { return IsAnimal();} }
            [JsonIgnore]
            public bool Npc { get { return IsNpc();} }
             

            [CanBeNull]
            public DeathObject SetHitInfo(HitInfo hitinfo, bool naked)
            {
                Date = DateTime.Now;
                
                Naked = naked;

                if(hitinfo == null) return null;

                if(hitinfo.InitiatorPlayer == null && (hitinfo.HitEntity == null || hitinfo.HitEntity.ToPlayer() == null)) return null;

                if(hitinfo.InitiatorPlayer != null) KillerId = hitinfo.InitiatorPlayer.userID;

                if (hitinfo.HitEntity.ToPlayer() != null) VictimId = hitinfo.HitEntity.ToPlayer().userID;
                
                if (hitinfo.Weapon != null) WeaponId = hitinfo.Weapon.GetItem().info.itemid;

                if (hitinfo.InitiatorPlayer != null)
                {
                    if (hitinfo.InitiatorPlayer.userID.IsSteamId())
                    {
                        KillerType = "player";
                    }
                    else
                    {
                        KillerType = "npc";
                    }
                }
                else if (hitinfo.Initiator != null && hitinfo.Initiator.ShortPrefabName != null)
                {
                    KillerType = hitinfo.HitEntity.ShortPrefabName;
                }

                if (hitinfo.HitEntity != null)
                {
                    if (VictimId.IsSteamId())
                    {
                        VictimType = "player";
                        
                    }else if (hitinfo.HitEntity.ShortPrefabName != null)
                    {
                        VictimType = hitinfo.HitEntity.ShortPrefabName;
                    }
                }

                return this;
            }

            private bool IsAnimal()
            {
                var animals = new List<string>
                {
                    "boar","stag","bear","chicken","wolf","horse"
                };
                return (animals.Contains(VictimType)) ;
            }

            private bool IsNpc()
            {
                if (VictimType == "player") return false;
                if (Animal) return false;
                return true;
            }

            public string WeaponName()
            {
                if (WeaponId != 0)
                {
                    var def = ItemManager.FindItemDefinition(WeaponId);
                    if (def != null)
                    {
                        return def.displayName.english;
                    }
                }

                return "";
            }
        }

        public class PvpData
        {
            public KillData Kills = new KillData();
            public KillData Deaths = new KillData();
            public int NakedKills = 0;
            public int KilLStreak = 0;
            
            public class KillData
            {
                public int Player = 0;
                public int Animal = 0;
                public int Npc = 0;
            }
        }
        
        #endregion
        
        #region Setup
        
        DataManager<DeathObject> _deathData;
        DataManager<DeathObject> _killData;
        private Dictionary<string, bool> _hadWeapon = new Dictionary<string, bool>();

        private void OnServerInitialized()
        {
            _deathData = new DataManager<DeathObject>("death");
            _killData = new DataManager<DeathObject>("kill");
        }

        private void Unload()
        {
            _deathData.Save();
            _killData.Save();
        }
        
        private void OnServerSave() { timer.Once(UnityEngine.Random.Range(0f, 60f), () =>
        {
            _deathData.Save();
            _killData.Save();
        });}
        
        #endregion
        
        #region Language
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["TotalPlayerKills"] = "Total Player Kills {0}",
                ["TotalPlayerDeaths"] = "Total Player Deaths {0}",
                ["PlayerKilLStreak"] = "Player Kill Streak {0}",
                ["NakedPercentage"] = "Naked Percentage {0}",
                ["AnimalKills"] = "Animal Kills {0}",
                ["NPCKills"] = "NPC Kills {0}",
                ["KillsForPlayer"] = "Kills for {0}",
                ["HasNoKills"] = "{0} has no kills",
                ["KillDetails"] = "{0}: Killed {1} using a {2}",
                ["InvalidPlayer"] = "Invalid Player",
                ["HasNoDeaths"] = "{0} has no deaths",
                ["DeathsForPlayer"] = "Deaths for {0}",
                ["DeathDetails"] = "{0}: Died to {1} using a {2}",
            }, this);
        }
        
        #endregion
        
        #region Hooks
        
        void OnEntityDeath(BasePlayer entity, HitInfo info)
        {
            if (entity == null || info == null) return;
            if (!(entity.GetEntity() is BasePlayer) && (info.InitiatorPlayer == null)) return; // No players involved

            var naked = false;
            if (entity.GetEntity() is BasePlayer)
            {
                if (entity.GetEntity().ToPlayer().userID.IsSteamId())
                {
                    naked = IsNaked(entity.GetEntity() as BasePlayer);
                    
                    var death = new DeathObject().SetHitInfo(info, naked);

                    if (death == null || (death.KillerId == 0 && death.KillerType == "player")) return;
                    
                    _deathData.AddData(entity.GetEntity().ToPlayer().UserIDString, death);
                }
            }
            


            if (info.InitiatorPlayer != null && info.InitiatorPlayer.userID.IsSteamId()) // There is a kill
            {
                // Record player kill
                if (info.HitEntity.IsNpc || (info.HitEntity.ToPlayer() != null)) // && info.HitEntity.ToPlayer().userID.IsSteamId()))
                {
                    if (info.HitEntity.ToPlayer() == null || info.InitiatorPlayer.UserIDString != info.HitEntity.ToPlayer().UserIDString)
                    {
                        _killData.AddData(info.InitiatorPlayer.UserIDString, new DeathObject().SetHitInfo(info, naked));
                    }
                }
            }
        }
        
        #endregion
        
        #region Data Analysis
        
        private int GetKillStreak(string playerId, DateTime startDate = default(DateTime), DateTime endDate = default(DateTime))
        {
            var recentDeath = _deathData.GetDataLast(playerId);
            var kills = _killData.GetData(playerId).Where(kill => kill.VictimType == "player");
            if (startDate != default(DateTime)) kills = kills.Where(kill => kill.Date > startDate);
            if (endDate != default(DateTime)) kills = kills.Where(kill => kill.Date < endDate);
            
            if (recentDeath == null)
            {
                return kills.Count();
            }

            return kills.Count(kill => kill.Date > recentDeath.Date);
        }

        private int GetTotalKills(string playerId, string victimType="player")
        {
            var kills = _killData.GetData(playerId);
            if (victimType != "")
            {
                if (victimType == "animal")
                {
                    return kills.Count(kill => kill.Animal);
                }else if (victimType == "npc")
                {
                    return kills.Count(kill => kill.Npc);
                }
                return kills.Count(kill => kill.VictimType == victimType);
            }
            return kills.Count;
        }
        
        private int GetTotalDeaths(string playerId, string killerType="player", DateTime startDate = default(DateTime), DateTime endDate = default(DateTime))
        {
            var deaths = _deathData.GetData(playerId).Where(d => d.KillerId > 0 && d.KillerId != d.VictimId);
            if (deaths == null || !deaths.Any()) return 0;

            if (startDate != default(DateTime)) deaths = deaths.Where(d => d.Date > startDate);
            if (endDate != default(DateTime)) deaths = deaths.Where(d => d.Date < endDate);
            
            if (killerType != "" ) deaths = deaths.Where(d => d.KillerType == killerType);

            try
            {
                return deaths.Count();
            }
            catch (Exception e)
            {
                return 0;
            }
        }

        private double PercentageNakedKills(string playerId, int lastNKills=10, int secondsAgo=0)
        {
            var kills = _killData.GetData(playerId);
            if (kills.Count == 0) return 0d;

            var results = kills
                .Where(kill => kill.VictimType == "player")
                .OrderByDescending(kill => kill.Date).Take(lastNKills);
            
            if (secondsAgo > 0) results = results.Where(kill => kill.Date > DateTime.Now.AddSeconds(-secondsAgo));

            if (!results.Any())
            {
                return 0;
            }

            var naked = 0d;
            foreach (var kill in kills)
            {
                if (kill.Naked) naked++;
            }
            return naked / results.Count();
        }
        
        public Dictionary<string, PvpData> AllPVPData(DateTime startDate=default(DateTime), DateTime endDate=default(DateTime))
        {
            Dictionary<string, PvpData> result = new Dictionary<string, PvpData>();
            foreach (var data in _killData.GetAllData())
            {
                if (!result.ContainsKey(data.Key))
                {
                    result.Add(data.Key, new PvpData());
                }

                var killdata = data.Value.GetData().AsEnumerable();
                if (startDate != default(DateTime)) killdata = killdata.Where(k => k.Date > startDate);
                if (endDate != default(DateTime)) killdata = killdata.Where(k => k.Date < endDate);

                foreach (var kill in killdata)
                {
                    if (kill.Animal)
                    {
                        result[data.Key].Kills.Animal++;
                    }else if (kill.Npc)
                    {
                        result[data.Key].Kills.Npc++;
                    }
                    else
                    {
                        result[data.Key].Kills.Player++;
                    }

                    if (kill.Naked)
                    {
                        result[data.Key].NakedKills++;
                    }
                }

                result[data.Key].KilLStreak = GetKillStreak(data.Key, startDate, endDate);
            }
            
            foreach (var uid in _deathData.GetKeys())
            {
                if (!result.ContainsKey(uid))
                {
                    result.Add(uid, new PvpData());
                }

                result[uid].Deaths.Player = GetTotalDeaths(uid, "player", startDate, endDate);
            }

            return result;
        }
        
        #endregion
        
        #region Commands

        [Command("datalogging.kills")]
        private void KillsCommand(IPlayer iplayer, string command, string[] args)
        {
            var player = GetCommandPlayer(iplayer, args);
            
            if (player == null) {
                iplayer.Reply(Lang("InvalidPlayer", iplayer.Id));
                return;
            }
            
            var kills = _killData.GetData(player.Id);
            if (kills.Count == 0)
            {
                iplayer.Reply(Lang("HasNoKills", iplayer.Id, player.Name));
                return;
            }
            
            iplayer.Reply(Lang("KillsForPlayer", iplayer.Id, player.Name));
            
            foreach (var kill in kills)
            {
                iplayer.Reply(Lang("KillDetails", iplayer.Id, kill.Date, kill.VictimType, kill.WeaponName()));                
            }
        }
        
        [Command("datalogging.deaths")]
        private void DeathsCommand(IPlayer iplayer, string command, string[] args)
        {
            var player = GetCommandPlayer(iplayer, args);
            
            if (player == null) {
                iplayer.Reply(Lang("InvalidPlayer", iplayer.Id));
                return;
            }

            var deaths = _deathData.GetData(player.Id);
            if (deaths.Count == 0)
            {
                iplayer.Reply(Lang("HasNoDeaths", iplayer.Id, player.Name));
                return;
            }
            
            iplayer.Reply(Lang("DeathsForPlayer", iplayer.Id, player.Name));
            
            foreach (var death in deaths)
            {
                iplayer.Reply(Lang("DeathDetails", iplayer.Id, death.Date, death.KillerType, death.WeaponName()));                
            }
        }
        
        [Command("datalogging.pvp")]
        private void PVPCommand(IPlayer iplayer, string command, string[] args)
        {
            var player = GetCommandPlayer(iplayer, args);
            
            if (player == null) {
                iplayer.Reply(Lang("InvalidPlayer", iplayer.Id));
                return;
            }

            iplayer.Reply(Lang("TotalPlayerKills", iplayer.Id, GetTotalKills(player.Id)));
            iplayer.Reply(Lang("TotalPlayerDeaths", iplayer.Id, GetTotalDeaths(player.Id)));
            iplayer.Reply(Lang("PlayerKilLStreak", iplayer.Id, GetKillStreak(player.Id)));
            iplayer.Reply(Lang("NakedPercentage", iplayer.Id, PercentageNakedKills(player.Id)));
            iplayer.Reply(Lang("AnimalKills", iplayer.Id, GetTotalKills(player.Id, "animal")));
            iplayer.Reply(Lang("NPCKills", iplayer.Id, GetTotalKills(player.Id, "npc")));
        }
        
        #endregion
        
        #region Helpers
        private bool IsNaked(BasePlayer bp)
        {
            if (bp.inventory.containerWear.itemList.Count > 0)
            {
                return false;
            }

            return (!HasWeapon(bp));
        }
        
        private bool HasWeapon(BasePlayer player)
        {
            bool weapon;
            if (_hadWeapon.TryGetValue(player.userID.ToString(), out weapon))
            {
                if (weapon)
                {
                    return true;
                }
            }
            
            foreach (Item obj in player.inventory.containerBelt.itemList.ToArray())
            {
                if (isWeapon(obj))
                {
                    return true;
                }
            }
            
            foreach (Item obj in player.inventory.containerMain.itemList.ToArray())
            {
                if (isWeapon(obj))
                {
                    return true;
                }
            }

            return false;
        }

        private bool isWeapon(Item item)
        {
            var name = item.info.shortname;
            if (name == "rock" || name == "torch")
            {
                return false;
            }
            
            var cat = item.info.category.ToString("G");
            if (cat == "Weapon" || cat == "Tool")
            {
                return true;
            }
            
            return false;
        }
        
        private void SetHasWeapon(string userid, bool weapon)
        {
            _hadWeapon[userid] = weapon;
        }
        
        #endregion

        #region API
        
        private JObject API_AllPVPData(DateTime startDate=default(DateTime), DateTime endDate=default(DateTime))
        {
            return JObject.FromObject(AllPVPData(startDate, endDate));
        }

        private int API_TotalKills(string playerId)
        {
            return GetTotalKills(playerId);
        }

        private int API_GetKillStreak(string playerId)
        {
            return GetKillStreak(playerId);
        }

        private double API_PercentageNakedKills(string playerid)
        {
            return PercentageNakedKills(playerid);
        }

        private bool API_Helper_IsNaked(BasePlayer player)
        {
            return IsNaked(player);
        }

        private JObject API_GetPlayerKills(string playerId)
        {
            return JObject.FromObject(_killData.GetData(playerId));
        }
        
        private JObject API_GetPlayerDeaths(string playerId)
        {
            return JObject.FromObject(_deathData.GetData(playerId));
        }

        #endregion
    }
}