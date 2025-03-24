using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Data Logging", "Rustoholics", "0.1.1")]
    [Description("Record stats from the server")]

    public class DataLogging : CovalencePlugin
    {

        #region Caching

        private Dictionary<string, CacheItem> _cache = new Dictionary<string, CacheItem>();

        public object GetCache(string key)
        {
            if (_cache.ContainsKey(key))
            {
                if (!_cache[key].IsExpired())
                {
                    return _cache[key].Value;
                }
                _cache.Remove(key);
            }

            return null;
        }

        public void AddCache(string key, object value, int ttl=60)
        {
            if (ttl <= 0)
            {
                return;
            }
            _cache[key] = new CacheItem()
            {
                Expires =  DateTime.Now.AddSeconds(ttl),
                Value = value
            };
        }

        class CacheItem
        {
            public DateTime Expires;
            public object Value;

            public bool IsExpired()
            {
                return Expires < DateTime.Now;
            }
        }
        
        private void OnServerInitialized()
        {
            SetupConfig(ref _config);
            timer.Every(120f, () =>
            {
                var delete = new List<string>();
                // Clean up expired cache so that it doesn't sit in memory forever
                foreach (var cache in _cache)
                {
                    if (cache.Value.IsExpired())
                    {
                        delete.Add(cache.Key);
                    }
                }

                foreach (var d in delete)
                {
                    _cache.Remove(d);
                }
            });
        }

        #endregion
        
        #region Config

        protected void SaveConfig<T>(T obj) => Config.WriteObject(obj);

        private class Configuration
        {
            public bool Debug = false;
        }

        public void SetupConfig<T>(ref T config) where T : new()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<T>();
                if (config == null) throw new Exception();

                SaveConfig<T>(config);
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                config = new T();
            }
        }

        private Configuration _config;
        #endregion
        
        public class DataManager<T>
        {
            private Dictionary<string, DataList<T>> _data = new Dictionary<string, DataList<T>>();

            private List<string> _needsWriting = new List<string>();

            public string _fileName;

            public string _filePattern = @".*\/([\d]+)_([a-zA-Z]+)";


            public DataManager(string filename="", string filepattern="")
            {
                _fileName = filename;
                if (filepattern != "") _filePattern = filepattern;
                Load();
            }

            public List<string> GetKeys(){
                return new List<string>(_data.Keys);
            }

            public void Load()
            {
                try
                {
                    foreach (string file in Interface.Oxide.DataFileSystem.GetFiles("DataLogger",
                        "*_" + GetFileExt() + ".json"))
                    {
                        Match match = Regex.Match(file, _filePattern);
                        if (match.Success)
                        {
                            var uid = match.Groups[1].Value;
                            if (!_data.ContainsKey(uid))
                            {
                                _data.Add(uid, new DataList<T>(uid, GetFileExt()));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            private string GetFileExt()
            {
                if (!string.IsNullOrEmpty(_fileName))
                {
                    return _fileName;
                }
                return typeof(T).Name.ToLower();
            }

            public List<T> GetData(string uid)
            {
                if (!_data.ContainsKey(uid))
                {
                    return new List<T>();
                }
                return _data[uid].GetData();
            }

            public Dictionary<string, DataList<T>> GetAllData()
            {
                return _data;
            }


            public void NeedsWriting(string uid)
            {
                if (!_needsWriting.Contains(uid))
                {
                    _needsWriting.Add(uid);
                }
            }
            
            public void AddData(string uid, T obj)
            {
                if (!_data.ContainsKey(uid))
                {
                    _data.Add(uid, new DataList<T>(uid, GetFileExt()));
                }
                _data[uid].AddData(obj);
                NeedsWriting(uid);
            }
            
            public void Save()
            {
                foreach (var uid in _needsWriting)
                {
                    if (_data.ContainsKey(uid))
                    {
                        _data[uid].Save();
                    }
                }
                _needsWriting.Clear();
            }

            public T GetDataLast(string playerId)
            {
                var data = GetData(playerId);
                if (data.Count == 0)
                {
                    return default(T);
                }

                return data[data.Count - 1];
            }
        }
        
        public class DataList<T>
        {
            private string _uid;

            private List<T> _list;

            private string _fileExt;

            public DataList(string userId, string filename)
            {
                _uid = userId;
                _fileExt = filename;
                Load();
            }

            private string Filename()
            {
                return "DataLogger\\" + _uid + "_" + _fileExt;
            }

            public void Load()
            {
                _list = Interface.Oxide.DataFileSystem.ReadObject<List<T>>(Filename());
            }

            public void Save()
            {
                Interface.Oxide.DataFileSystem.WriteObject(Filename(), _list);
            }

            public List<T> GetData()
            {
                return _list;
            }

            public void AddData(T obj)
            {
                _list.Add(obj);
            }

            public T GetDataLast()
            {
                if (_list.Count == 0)
                {
                    return default(T);
                }

                return _list[_list.Count - 1];
            }
        }
        
        #region Helpers
        
        public void Debug(string txt)
        {
            if (_config.Debug)
            {
                Puts(txt);
            }
        }

        
        [CanBeNull]
        public IPlayer GetCommandPlayer(IPlayer iplayer, string[] args)
        {
            if (args.ElementAtOrDefault(0) == null) return iplayer;

            var player = players.FindPlayer(args[0]);
            if (player == null)
            {
                return null;
            }

            return player;
        }
        
        #endregion

    }
}