using System;
using System.Collections.Generic;
using Facepunch;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using UnityEngine;

namespace Oxide.Plugins;

[Info("Marker Distance", "Notchu", "1.2.1")]
[Description("Shows distance to placed marker")]

internal class MarkerDistance : RustPlugin
{
    private class Configuration
    {
        [JsonProperty("Text Color")]
        public string TextColor = "1.0 1.0 1.0 1.0";
        
        [JsonProperty("Font Size")] 
        public int FontSize = 14;

        [JsonProperty("Background Color")] 
        public string BackgroundColor = "0.50 0.50 0.50 0.5";

        [JsonProperty("Anchor Min")]
        public string AnchorMin = "0.47 0.94";

        [JsonProperty("Anchor Max")]
        public string AnchorMax = "0.53 0.96";

        [JsonProperty("Refresh Interval")]
        public float Interval = 1f;

        [JsonProperty("Offset Min")] 
        public string OffsetMin = "0 -15";
        
        [JsonProperty("Offset Max")] 
        public string OffsetMax = "0 -5";
    }
    
        
    private Configuration _config;

    private Timer _distanceTimer = null;
    
    private HashSet<BasePlayer> _users = new HashSet<BasePlayer>();
    
     private readonly Dictionary<int, string> _colorCodes = new Dictionary<int, string>()
        {
            { 0, "0.906 0.918 0.373 0.8" },
            { 1, "0.196 0.49 0.851 0.8" },
            { 2, "0.502 0.71 0.239 0.8" },
            { 3, "0.784 0.231 0.231 0.8" },
            { 4, "0.745 0.376 0.804 0.8" }
        };
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
            {"UnitOfMeasure", "{distance} meters" }

        }, this);
    }
    void Init()
    {
        permission.RegisterPermission("markerDistance.use", this);
            
    }
    void Unload()
    {
        if (_users == null) return;
        foreach (var user in _users)
        {
            if (user == null) continue;
            CuiHelper.DestroyUi(user, "markerDistanceBackground");
        }
        _users.Clear();
    }

    void OnPlayerDisconnected(BasePlayer player, string reason)
    {
        CuiHelper.DestroyUi(player, "markerDistanceBackground");
        _users.Remove(player);
        if (_users.Count == 0 && _distanceTimer != null && !_distanceTimer.Destroyed )
        {
            _distanceTimer.Destroy();
        }
    }
    
    void OnMapMarkerAdded(BasePlayer player, MapNote note)
    {
        if (!DoesHavePermission(player.UserIDString)) return;

        if (_distanceTimer == null || _distanceTimer.Destroyed)
        {
            _distanceTimer = timer.Every(_config.Interval, ShowDistance);
        }

        _users.Add(player);
    }
        
    /* object OnMapMarkerRemove(BasePlayer player, MapNote note)
    {
        if (!_users.Contains(player))
        {
            return null;
        }

        if (player.State.pointsOfInterest.Count == 1) 
        {
            if(_users.Remove(player)) CuiHelper.DestroyUi(player, "markerDistanceBackground");
        }

        if (_users.Count == 0 && _distanceTimer != null && !_distanceTimer.Destroyed )
        {
            _distanceTimer.Destroy();
        }

        return null;

    } */
    private void ShowDistance()
    {
        if (_users.Count == 0)
        {
            _distanceTimer.Destroy();
            return;
        }

        var usersToDelete = new List<BasePlayer>();
        foreach (var u in _users)
        {
            if (u.State.pointsOfInterest.Count == 0)
            {
                usersToDelete.Add(u);
                continue;
            }
            var note = u.State.pointsOfInterest[^1];
            
            var distance = (int)(note.worldPosition - u.transform.position).magnitude;
            
            var container = new CuiElementContainer();
            
            var guiBackground = container.Add(new CuiPanel()
            {
                Image =
                {
                    Color = _config.BackgroundColor
                },
                RectTransform =
                {
                    AnchorMin = _config.AnchorMin,
                    AnchorMax = _config.AnchorMax,
                    OffsetMin = _config.OffsetMin,
                    OffsetMax = _config.OffsetMax
                },
                CursorEnabled = false
            }, "Under", "markerDistanceBackground");
            
            container.Add(new CuiLabel()
            {
                Text =
                {
                    Text = lang.GetMessage("UnitOfMeasure", this, u.UserIDString).Replace("{distance}", distance.ToString()),
                    FontSize = _config.FontSize,
                    Color = _config.TextColor,
                    Align = TextAnchor.MiddleCenter
                        
                },
                    
            }, guiBackground);
            container.Add(new CuiPanel()
            {
                Image = { Color = _colorCodes[note.colourIndex] },
                RectTransform =
                {
                    AnchorMin = "0.0 0.99",
                    AnchorMax = "0.99 1.0"
                }
            }, guiBackground);

            CuiHelper.DestroyUi(u, "markerDistanceBackground");
            CuiHelper.AddUi(u, container);
        }

        if (usersToDelete.Count == 0) return;
        
        foreach (var u in usersToDelete)
        {
            CuiHelper.DestroyUi(u, "markerDistanceBackground");
            _users.Remove(u);
        }
    }

    private bool DoesHavePermission(string id)
    {
        return permission.UserHasPermission(id, "markerDistance.use");
    }
    

    
    
    
}