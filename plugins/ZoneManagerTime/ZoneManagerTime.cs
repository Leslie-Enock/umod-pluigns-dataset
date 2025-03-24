using System;
using System.Collections.Generic;
using Network;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Zone Manager Time", "misticos", "1.0.1")]
    [Description("Set specific time for your zones")]
    class ZoneManagerTime : CovalencePlugin
    {
        #region Variables
        
        [PluginReference]
        // ReSharper disable once InconsistentNaming
        private Plugin ZoneManager = null;
        
        private Dictionary<string, string> _playerZones = new Dictionary<string, string>();
        
        #endregion
        
        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Zone ID Time", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, TimeSpan> ZoneTime = new Dictionary<string, TimeSpan>
                {{"Zone ID", new TimeSpan(10, 5, 37)}};

            [JsonProperty(PropertyName = "Update Frequency")]
            public float UpdateFrequency = 5f;
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

        #endregion
        
        #region Hooks

        private void OnServerInitialized()
        {
            new GameObject().AddComponent<DateController>().PluginInstance = this;

            if (ZoneManager != null && ZoneManager.IsLoaded)
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    var zones = ZoneManager.Call<string[]>("GetPlayerZoneIDs", player);
                    if (zones == null || zones.Length == 0)
                        continue;
                    
                    _playerZones[player.UserIDString] = zones[zones.Length - 1];
                }
            }
        }

        private void Unload()
        {
            UnityEngine.Object.DestroyImmediate(DateController.Instance.gameObject);
        }

        private void OnEnterZone(string zoneId, BasePlayer player)
        {
            _playerZones[player.UserIDString] = zoneId;
        }

        private void OnExitZone(string zoneId, BasePlayer player)
        {
            _playerZones.Remove(player.UserIDString);
        }

        #endregion
        
        #region Controller

        private class DateController : SingletonComponent<DateController>
        {
            public ZoneManagerTime PluginInstance = null;
            
            private EnvSync _timeEntity;

            private void Start()
            {
                _timeEntity = FindObjectOfType<EnvSync>();
                _timeEntity.limitNetworking = true;
                
                InvokeRepeating(DoUpdate, PluginInstance._config.UpdateFrequency, PluginInstance._config.UpdateFrequency);
            }

            protected override void OnDestroy()
            {
                base.OnDestroy();
                _timeEntity.limitNetworking = false;
            }

            private void DoUpdate()
            {
                var saveInfo = new BaseNetworkable.SaveInfo
                {
                    forDisk = false
                };

                using (saveInfo.msg = Facepunch.Pool.Get<ProtoBuf.Entity>())
                {
                    _timeEntity.Save(saveInfo);
                    var initialDateTime = DateTime.FromBinary(saveInfo.msg.environment.dateTime);

                    for (var i = 0; i < BasePlayer.activePlayerList.Count; i++)
                    {
                        var connection = BasePlayer.activePlayerList[i].net?.connection;
                        if (connection == null)
                            continue;

                        var write = Net.sv.StartWrite();

                        saveInfo.forConnection = connection;

                        connection.validate.entityUpdates += 1u;

                        write.PacketID(Message.Type.Entities);
                        write.UInt32(connection.validate.entityUpdates);

                        string zoneId;
                        TimeSpan offset;
                        if (PluginInstance._playerZones.TryGetValue(BasePlayer.activePlayerList[i].UserIDString,
                            out zoneId) && PluginInstance._config.ZoneTime.TryGetValue(zoneId, out offset))
                        {
                            saveInfo.msg.environment.dateTime = (initialDateTime.Date + offset).ToBinary();
                        }
                        else
                        {
                            saveInfo.msg.environment.dateTime = initialDateTime.ToBinary();
                        }

                        saveInfo.msg.ToProto(write);

                        write.Send(new SendInfo(connection));
                    }
                }
            }
        }
        
        #endregion
    }
}