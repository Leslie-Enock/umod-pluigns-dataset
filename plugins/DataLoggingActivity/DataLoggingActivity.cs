// Requires: DataLogging

using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Data Logging: Activity", "Rustoholics", "0.1.0")]
    [Description("Log when some is connected/disconnect and AFK")]

    public class DataLoggingActivity : DataLogging
    {
        #region Object
        public class Activity
        {
            public Types Type;
            
            public DateTime Date = DateTime.Now;

            public string Notes;
            
            public enum Types
            {
                Connected,
                Disconnected,
                Active,
                Afk,
            }
        }
        
        public class ActivityData
        {
            public int PlayTime;
            public int AfkTime;
            public int ConnectedTime;

        }
        
        #endregion
        
        #region Language
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);
        
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["InvalidPlayer"] = "Invalid Player",
                ["ActivityForPlayer"] = "Activity for {0}",
                ["ActivityAtTime"] = "{0} at {1}",
                ["SecondsConnected"] = "{0} seconds connected",
                ["SecondsActivity"] = "{0} seconds active",
                ["ActivityAndConnected"] = "{0} active for {1} seconds and connected for {2} seconds",
            }, this);
        }
        
        #endregion
        
        #region Setup / Config
        
        private class Configuration
        {
            public bool Debug = false;
        }

        private Configuration config;
        
        DataManager<Activity> _data;
        
        private Dictionary<string, Vector3> _positions = new Dictionary<string, Vector3>();

        private void OnServerInitialized()
        {
            _data = new DataManager<Activity>();
            
            SetupConfig(ref config);

            timer.Every(30f, () =>
            {
                List<string> connected = new List<string>();
                foreach (var player in covalence.Players.Connected)
                {
                    if (player.Id == null) continue;

                    connected.Add(player.Id);

                    var last = _data.GetDataLast(player.Id);
                    
                    if (last == null) continue;
                    var afk = IsAfk((BasePlayer) player.Object);
                    
                    if (last.Type == Activity.Types.Connected || (last.Type == Activity.Types.Active && afk) || last.Type == Activity.Types.Afk && !afk)
                    {
                        _data.AddData(player.Id, new Activity()
                        {
                            Type = afk ? Activity.Types.Afk : Activity.Types.Active
                        });
                    }
                }
                
                // Check that "connected" players are actually still connected
                foreach (var uid in _data.GetKeys())
                {
                    if (connected.Contains(uid)) continue;
                    
                    var most_recent = _data.GetDataLast(uid);
                    if (most_recent.Type != Activity.Types.Disconnected)
                    {
                        _data.AddData(uid, new Activity()
                        {
                            Type = Activity.Types.Disconnected,
                            Notes = "Non-hook disconnect"
                        });
                    }
                }
            });
        }

        private void Unload()
        {
            _data.Save();
        }
        
        private void OnServerSave() { 
            timer.Once(UnityEngine.Random.Range(0f, 60f), () =>
            {
            _data.Save();
            });
            
        }
        
        #endregion
        
        #region Hooks
        
        void OnPlayerConnected(BasePlayer player)
        {
            _data.AddData(player.UserIDString, new Activity()
            {
                Type = Activity.Types.Connected
            });
            IsAfk(player); // Set the position
        }
        
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            _data.AddData(player.UserIDString, new Activity()
            {
                Type = Activity.Types.Disconnected,
                Notes = reason
            });
        }
        
        #endregion
        
        #region Data Analysis
        

        public int TimeConnected(string playerId, DateTime startTime=default(DateTime))
        {
            var cachekey = "TimeConnected_" + playerId+(startTime != default(DateTime) ? "_"+startTime.ToString() : "");
            var cache = GetCache(cachekey);
            if (cache != null)
            {
                return (int)cache;
            }
            
            var data = _data.GetData(playerId).AsEnumerable();
            data = data.OrderBy(act => act.Date);

            DateTime? clock = null;
            var timer = 0d;
            foreach (var activity in data)
            {
                if (activity.Type == Activity.Types.Connected)
                {
                    clock = activity.Date;
                }else if (activity.Type == Activity.Types.Disconnected)
                {
                    if (clock != null)
                    {
                        if (startTime == default(DateTime) || activity.Date > startTime)
                        {
                            if (startTime != default(DateTime) && clock< startTime) clock = startTime;
                            
                            timer += (activity.Date - (DateTime) clock).TotalSeconds;
                        }

                        clock = null;
                    }
                }
            }

            if (clock != null)
            {
                if (startTime != default(DateTime) && clock< startTime) clock = startTime;
                timer += (DateTime.Now - (DateTime) clock).TotalSeconds;
            }
            var intval = (int)Math.Floor(timer);
            AddCache(cachekey, intval, 60);

            return intval;
        }
        
        public int TimeActive(string playerId, DateTime startTime = default(DateTime))
        {
            var cachekey = "TimeActive" + playerId+(startTime != default(DateTime) ? "_"+startTime.ToString() : "");
            var cache = GetCache(cachekey);
            if (cache != null)
            {
                return (int)cache;
            }
            
            var data = _data.GetData(playerId).AsEnumerable();
            if (startTime != default(DateTime))
            {
                data = data.Where(act => act.Date > startTime);
            }
            data = data.OrderBy(act => act.Date);

            DateTime? clock = null;
            var timer = 0d;
            Activity.Types state = Activity.Types.Disconnected;
            
            foreach (var activity in data)
            {
                if (activity.Type == Activity.Types.Connected || activity.Type == Activity.Types.Active)
                {
                    if(clock == null || state == Activity.Types.Active) clock = activity.Date;
                }else if (activity.Type == Activity.Types.Disconnected || activity.Type == Activity.Types.Afk)
                {
                    if (clock != null)
                    {
                        if (startTime == default(DateTime) || activity.Date > startTime)
                        {
                            if (startTime != default(DateTime) && clock < startTime) clock = startTime;
                            
                            timer += (activity.Date - (DateTime) clock).TotalSeconds;
                        }

                        clock = null;
                    }
                }
                state = activity.Type;
            }

            if (clock != null && (state == Activity.Types.Active || state == Activity.Types.Connected))
            {
                if (startTime != default(DateTime) && clock< startTime) clock = startTime;
                timer += (DateTime.Now - (DateTime) clock).TotalSeconds;
            }
            var intval = (int)Math.Floor(timer);
            AddCache(cachekey, intval, 60);

            return intval;
        }

        public Dictionary<string, ActivityData> GetAllActivity(DateTime startDate = default(DateTime))
        {
            Dictionary<string, ActivityData> results = new Dictionary<string, ActivityData>();
            foreach (var uid in _data.GetKeys())
            {
                var playtime = TimeActive(uid, startDate);
                var connectedtime = TimeConnected(uid, startDate);
                if (playtime > 0 || connectedtime > 0)
                {
                    results.Add(uid, new ActivityData()
                    {
                        PlayTime = playtime,
                        ConnectedTime = connectedtime
                    });
                }
            }

            return results;
        }

        #endregion
        
        #region Commands

        [Command("datalogging.activity")]
        private void ActivityCommand(IPlayer iplayer, string command, string[] args)
        {
            var player = GetCommandPlayer(iplayer, args);
            
            if (player == null) {
                iplayer.Reply(Lang("InvalidPlayer", iplayer.Id));
                return;
            }
            
            iplayer.Reply(Lang("ActivityForPlayer", iplayer.Id, player.Name));
            var data = _data.GetData(player.Id);
            foreach (var activity in data)
            {
                iplayer.Reply(Lang("ActivityAtTime", iplayer.Id, activity.Type.ToString(), activity.Date));
            }

        }
        
        [Command("datalogging.activetime")]
        private void TimeActiveCommand(IPlayer iplayer, string command, string[] args)
        {
            var player = GetCommandPlayer(iplayer, args);
            
            if (player == null) {
                iplayer.Reply(Lang("InvalidPlayer", iplayer.Id));
                return;
            }
            
            iplayer.Reply(Lang("ActivityForPlayer", iplayer.Id, player.Name));
            iplayer.Reply(Lang("SecondsConnected",iplayer.Id, TimeConnected(player.Id)));
            iplayer.Reply(Lang("SecondsActivity",iplayer.Id, TimeActive(player.Id)));
        }
        
        [Command("datalogging.allactivity")]
        private void AllActivityCommand(IPlayer iplayer, string command, string[] args)
        {

            foreach (var allactivity in GetAllActivity().OrderByDescending(act => act.Value.PlayTime))
            {
                var playername = players.FindPlayer(allactivity.Key);
                if (playername == null) continue;
                iplayer.Reply(Lang("ActivityAndConnected", iplayer.Id, playername.Name, allactivity.Value.PlayTime, allactivity.Value.ConnectedTime));                
            }
        }

        
        #endregion
        
        #region Helpers

        private bool IsAfk(BasePlayer player)
        {
            var pos = player.transform.position;

            if (!_positions.ContainsKey(player.UserIDString))
            {
                _positions.Add(player.UserIDString, pos);
                return false;
            }
            
            if (pos.Equals(_positions[player.UserIDString]))
            {
                return true;
            }

            _positions[player.UserIDString] = pos;
            
            return false;
        }
        
        #endregion

        #region API

        private JObject API_GetAllActivity(DateTime startDate = default(DateTime))
        {
            return JObject.FromObject(GetAllActivity(startDate));
        }

        private int API_GetActiveTime(string playerId)
        {
            return TimeActive(playerId);
        }

        private int API_GetConnectedTime(string playerId)
        {
            return TimeConnected(playerId);
        }

        #endregion
    }
}