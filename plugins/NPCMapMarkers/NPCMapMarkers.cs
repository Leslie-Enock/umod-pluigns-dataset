using System.Collections.Generic;
using UnityEngine;
using CompanionServer.Handlers;
using Newtonsoft.Json;


namespace Oxide.Plugins
{
    [Info("NPC Map Markers", "AK", "1.0.0")]
    [Description("Shows custom map markers for NPCs and animals on the server")]
    internal class NPCMapMarkers : CovalencePlugin
    {
        #region Vars

        private const string permAdmin = "npcmapmarkers.admin";
        public List<MapMarkerGenericRadius> npcMarkers = new List<MapMarkerGenericRadius>();

        #endregion

        #region Config       

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "NPC Map Marker Options")]
            public NPCMapMarkerOptions NPCMapMarker { get; set; }

            [JsonProperty(PropertyName = "Human NPC Markers Options")]
            public NPCMarkersOptions NPCMarkers { get; set; }

            [JsonProperty(PropertyName = "Animal Markers Options")]
            public AnimalMarkersOptions AnimalMarkers { get; set; }

            public class NPCMapMarkerOptions
            {
                [JsonProperty(PropertyName = "Prefab Path")]
                public string PREFAB_MARKER { get; set; }

                [JsonProperty(PropertyName = "Update frequency")]
                public float updateFreq { get; set; }

                [JsonProperty(PropertyName = "Show to all? (true/false)")]
                public bool visibleToAll { get; set; }

                [JsonProperty(PropertyName = "Show Human NPC on map? (true/false)")]
                public bool showNPC { get; set; }

                [JsonProperty(PropertyName = "Show Animals on map? (true/false)")]
                public bool showAnimals { get; set; }

            }

            public class NPCMarkersOptions
            {
                [JsonProperty(PropertyName = "Color1 (hex)")]
                public string NPCColor1 { get; set; }

                [JsonProperty(PropertyName = "Color2 (hex)")]
                public string NPCColor2 { get; set; }

                [JsonProperty(PropertyName = "Alpha")]
                public float NPCAlpha { get; set; }

                [JsonProperty(PropertyName = "Radius")]
                public float NPCRadius { get; set; }
            }

            public class AnimalMarkersOptions
            {
                [JsonProperty(PropertyName = "Color1 (hex)")]
                public string AnimalColor1 { get; set; }

                [JsonProperty(PropertyName = "Color2 (hex)")]
                public string AnimalColor2 { get; set; }

                [JsonProperty(PropertyName = "Alpha")]
                public float AnimalAlpha { get; set; }

                [JsonProperty(PropertyName = "Radius")]
                public float AnimalRadius { get; set; }
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();
            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                NPCMapMarker = new ConfigData.NPCMapMarkerOptions
                {
                    PREFAB_MARKER = "assets/prefabs/tools/map/genericradiusmarker.prefab",
                    updateFreq = 5f,
                    visibleToAll = true,
                    showNPC = true,
                    showAnimals = true
                },
                NPCMarkers = new ConfigData.NPCMarkersOptions
                {
                    NPCColor1 = "#00FF00",
                    NPCColor2 = "#00FF00",
                    NPCAlpha = 1f,
                    NPCRadius = 0.08f
                },
                AnimalMarkers = new ConfigData.AnimalMarkersOptions
                {
                    AnimalColor1 = "#FF0000",
                    AnimalColor2 = "#FF0000",
                    AnimalAlpha = 1f,
                    AnimalRadius = 0.08f
                }
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        #endregion Config

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(permAdmin, this);
        }

        private void OnServerInitialized(bool initial)
        {
            LoadNPCMapMarkers();
            InvokeHandler.Instance.InvokeRepeating(UpdateMarkers, 5f, configData.NPCMapMarker.updateFreq);
        }

        private void Unload()
        {
            InvokeHandler.Instance.CancelInvoke(UpdateMarkers);
            RemoveNPCMapMarkers();         
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                timer.Once(1f, () => OnPlayerConnected(player));
                return;
            }
            foreach (var marker in npcMarkers)
            {
                if (marker != null)
                {
                    marker.SendUpdate();
                }
            }
        }

        object CanNetworkTo(MapMarkerGenericRadius marker, BasePlayer player)
        {
            if (!npcMarkers.Contains(marker)) return null;

            if (marker.name == "npc" && configData.NPCMapMarker.showNPC && (configData.NPCMapMarker.visibleToAll || player.IPlayer.HasPermission(permAdmin)))
            {
                return null;
            }
            else if(marker.name == "animal" && configData.NPCMapMarker.showAnimals && (configData.NPCMapMarker.visibleToAll || player.IPlayer.HasPermission(permAdmin)))
            {
                return null;
            }
            else
            {
                return false;
            }
        }

        #endregion

        #region Core

        private void UpdateMarkers()
        {
            RemoveNPCMapMarkers();
            LoadNPCMapMarkers();
        }

        private void LoadNPCMapMarkers()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if ((entity is NPCPlayer))
                {
                    var npc = (NPCPlayer)entity;
                    CreateNPCMapMarker(npc, "npc", configData.NPCMarkers.NPCColor1, configData.NPCMarkers.NPCColor2, configData.NPCMarkers.NPCAlpha, configData.NPCMarkers.NPCRadius);
                }

                if ((entity is BaseAnimalNPC))
                {
                    var animal = (BaseAnimalNPC)entity;
                    CreateNPCMapMarker(animal, "animal", configData.AnimalMarkers.AnimalColor1, configData.AnimalMarkers.AnimalColor2, configData.AnimalMarkers.AnimalAlpha, configData.AnimalMarkers.AnimalRadius);
                }
            }
        }

        private void RemoveNPCMapMarkers()
        {
            foreach (var marker in npcMarkers)
            {
                if (marker != null)
                {
                    marker.Kill();
                    marker.SendUpdate();
                }
            }
            npcMarkers.Clear();
        }

        private void CreateNPCMapMarker(BaseNetworkable entity, string type, string color1, string color2, float alpha, float radius)
        {
            MapMarkerGenericRadius mapMarker = GameManager.server.CreateEntity(configData.NPCMapMarker.PREFAB_MARKER, entity.transform.position) as MapMarkerGenericRadius;

            if (mapMarker != null)
            {
                mapMarker.alpha = alpha;
                if (!ColorUtility.TryParseHtmlString(color1, out mapMarker.color1))
                {
                    mapMarker.color1 = Color.black;
                    PrintError($"Invalid map marker color1: {color1}");
                }

                if (!ColorUtility.TryParseHtmlString(color2, out mapMarker.color2))
                {
                    mapMarker.color2 = Color.white;
                    PrintError($"Invalid map marker color2: {color2}");
                }

                mapMarker.name = type;
                mapMarker.radius = radius;
                npcMarkers.Add(mapMarker);
                mapMarker.Spawn();
                mapMarker.SendUpdate();
            }
        }

        #endregion

    }

}