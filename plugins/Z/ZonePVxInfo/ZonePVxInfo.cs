using System;
using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

// ** Thanks to previous mantainers BuzZ[PHOQUE]/Arainrr (upto - v1.1.5) ** \\

namespace Oxide.Plugins
{
    [Info("Zone PVx Info", "Mabel", "1.1.9")]
    [Description("HUD on PVx name defined Zones")]
    public class ZonePVxInfo : RustPlugin
    {
        #region Fields

        [PluginReference] private readonly Plugin ZoneManager, DynamicPVP, RaidableBases, AbandonedBases, TruePVE, DangerousTreasures;

        private const string UinameMain = "ZonePVxInfoUI";
        private bool _pvpAll;

        private enum PVxType { PVE, PVP, PVPDelay }

        #endregion Fields

        #region Oxide Hooks

        private void Init()
        {
            AddCovalenceCommand("pvpall", nameof(CmdServerPVx));
            if (configData.defaultType == PVxType.PVPDelay)
            {
                configData.defaultType = PVxType.PVE;
            }
        }

        private void OnServerInitialized()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyUI(player);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;
            if (_pvpAll)
            {
                CreatePVxUI(player, PVxType.PVP);
            }
            else
            {
                CheckPlayerZone(player);
            }
        }

        #endregion Oxide Hooks

        #region ZoneManager

        private string GetZoneName(string zoneId)
        {
            return (string)ZoneManager.Call("GetZoneName", zoneId);
        }

        private string[] GetPlayerZoneIDs(BasePlayer player)
        {
            return (string[])ZoneManager.Call("GetPlayerZoneIDs", player);
        }

        private float GetZoneRadius(string zoneId)
        {
            var obj = ZoneManager.Call("GetZoneRadius", zoneId); ;
            if (obj is float)
            {
                return (float)obj;
            }
            return 0f;
        }

        private Vector3 GetZoneSize(string zoneId)
        {
            var obj = ZoneManager.Call("GetZoneSize", zoneId); ;
            if (obj is Vector3)
            {
                return (Vector3)obj;
            }
            return Vector3.zero;
        }

        private void OnEnterZone(string zoneId, BasePlayer player)
        {
            NextTick(() => CheckPlayerZone(player));
        }

        private void OnExitZone(string zoneId, BasePlayer player)
        {
            NextTick(() => CheckPlayerZone(player));
        }

        private void CheckPlayerZone(BasePlayer player, bool checkPVPDelay = true)
        {
            if (_pvpAll || player == null || !player.IsConnected || !player.userID.IsSteamId()) return;
            if (checkPVPDelay && IsPlayerInPVPDelay(player.userID))
            {
                CreatePVxUI(player, PVxType.PVPDelay);
                return;
            }
            if (ZoneManager != null)
            {
                var zoneName = GetSmallestZoneName(player);
                if (!string.IsNullOrEmpty(zoneName))
                {
                    if (zoneName.Contains("pvp", CompareOptions.IgnoreCase))
                    {
                        CreatePVxUI(player, PVxType.PVP);
                        return;
                    }

                    if (zoneName.Contains("pve", CompareOptions.IgnoreCase))
                    {
                        CreatePVxUI(player, PVxType.PVE);
                        return;
                    }
                }
            }

            if (configData.showDefault)
            {
                CreatePVxUI(player, configData.defaultType);
            }
            else
            {
                DestroyUI(player);
            }
        }

        private string GetSmallestZoneName(BasePlayer player)
        {
            float radius = float.MaxValue;
            string smallest = null;
            var zoneIDs = GetPlayerZoneIDs(player);
            foreach (var zoneId in zoneIDs)
            {
                var zoneName = GetZoneName(zoneId);
                if (string.IsNullOrEmpty(zoneName)) continue;
                float zoneRadius;
                var zoneSize = GetZoneSize(zoneId);
                if (zoneSize != Vector3.zero)
                {
                    zoneRadius = (zoneSize.x + zoneSize.z) / 2;
                }
                else
                {
                    zoneRadius = GetZoneRadius(zoneId);
                }
                if (zoneRadius <= 0f)
                {
                    continue;
                }
                if (radius >= zoneRadius)
                {
                    radius = zoneRadius;
                    smallest = zoneName;
                }
            }
            return smallest;
        }

        #endregion ZoneManager

        #region RaidableBases

        private void OnPlayerEnteredRaidableBase(BasePlayer player, Vector3 location, bool allowPVP)
        {
            if (_pvpAll) return;
            CreatePVxUI(player, allowPVP ? PVxType.PVP : PVxType.PVE);
        }

        private void OnPlayerExitedRaidableBase(BasePlayer player, Vector3 location, bool allowPVP)
        {
            NextTick(() => CheckPlayerZone(player));
        }

        private void OnRaidableBaseEnded(Vector3 raidPos, int mode, float loadingTime)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CheckPlayerZone(player);
            }
        }

        #endregion RaidableBases

        #region AbandonedBases

        void OnPlayerEnteredAbandonedBase(BasePlayer player, Vector3 eventPos, float radius, bool allowPVP)
        {
            if (_pvpAll) return;
            CreatePVxUI(player, allowPVP ? PVxType.PVP : PVxType.PVE);
        }

        void OnPlayerExitedAbandonedBase(BasePlayer player)
        {
            NextTick(() => CheckPlayerZone(player));
        }

        void OnAbandonedBaseEnded()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CheckPlayerZone(player);
            }
        }

        #endregion AbandonedBases

        #region DangerousTreasures

        void OnPlayerEnteredDangerousEvent(BasePlayer player, Vector3 containerPos, bool allowPVP)
        {
            if (_pvpAll) return;
            CreatePVxUI(player, allowPVP ? PVxType.PVP : PVxType.PVE);
        }

        void OnPlayerExitedDangerousEvent(BasePlayer player)
        {
            NextTick(() => CheckPlayerZone(player));
        }

        #endregion DangerousTreasures

        #region Adem Events

        void OnPlayerEnterConvoy(BasePlayer player)
        {
            if (configData.AdemEventSettings.convoyEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitConvoy(BasePlayer player)
        {
            if (configData.AdemEventSettings.convoyEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterArmoredTrain(BasePlayer player)
        {
            if (configData.AdemEventSettings.armoredTrainEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitArmoredTrain(BasePlayer player)
        {
            if (configData.AdemEventSettings.armoredTrainEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterCaravan(BasePlayer player)
        {
            if (configData.AdemEventSettings.caravanEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitCaravan(BasePlayer player)
        {
            if (configData.AdemEventSettings.caravanEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterSputnik(BasePlayer player)
        {
            if (configData.AdemEventSettings.sputnikEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitSputnik(BasePlayer player)
        {
            if (configData.AdemEventSettings.sputnikEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterSpace(BasePlayer player)
        {
            if (configData.AdemEventSettings.spaceEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitSpace(BasePlayer player)
        {
            if (configData.AdemEventSettings.spaceEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterShipwreck(BasePlayer player)
        {
            if (configData.AdemEventSettings.shipwreckEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitShipwreck(BasePlayer player)
        {
            if (configData.AdemEventSettings.shipwreckEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }
        #endregion Adem Events

        #region KpucTaJl Events
        void OnPlayerEnterPowerPlantEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.powerPlantEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitPowerPlantEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.powerPlantEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterFerryTerminalEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.ferryTerminalEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitFerryTerminalEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.ferryTerminalEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterSupermarketEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.supermarketEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitSupermarketEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.supermarketEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterJunkyardEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.junkyardEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitJunkyardEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.junkyardEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterArcticBaseEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.arcticEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitArcticBaseEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.arcticEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterGasStationEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.gasStationEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitGasStationEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.gasStationEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterAirEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.airEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitAirEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.airEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterHarborEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.harborEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitHarborEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.harborEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterSatDishEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.satDishEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitSatDishEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.satDishEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterWaterEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.waterEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitWaterEvent(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.waterEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterTriangulation(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.triangulationEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitTriangulation(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.triangulationEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        void OnPlayerEnterDefendableHomes(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.defendableHomesEvent)
            {
                if (_pvpAll) return;
                CreatePVxUI(player, PVxType.PVP);
            }
        }

        void OnPlayerExitDefendableHomes(BasePlayer player)
        {
            if (configData.KpucTaJlEventSettings.defendableHomesEvent)
            {
                NextTick(() => CheckPlayerZone(player));
            }
        }

        #endregion KpucTaJl Events

        #region CargoTrainTunnel

        private void OnPlayerEnterPVPBubble(TrainEngine trainEngine, BasePlayer player)
        {
            if (_pvpAll) return;
            CreatePVxUI(player, PVxType.PVP);
        }

        private void OnPlayerExitPVPBubble(TrainEngine trainEngine, BasePlayer player)
        {
            NextTick(() => CheckPlayerZone(player));
        }

        private void OnTrainEventEnded(TrainEngine trainEngine)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CheckPlayerZone(player);
            }
        }

        #endregion CargoTrainTunnel

        #region PVPDelay

        private void OnPlayerRemovedFromPVPDelay(ulong playerId, string zoneId) // DynamicPVP
        {
            var player = BasePlayer.FindByID(playerId);
            if (player == null) return;
            CheckPlayerZone(player, false);
        }

        private void OnPlayerPvpDelayExpired(BasePlayer player) // RaidableBases
        {
            if (player == null) return;
            CheckPlayerZone(player, false);
        }

        void OnPlayerPvpDelayExpiredII(BasePlayer player) // AbandonedBases
        {
            if (player == null) return;
            CheckPlayerZone(player, false);
        }

        private bool IsPlayerInPVPDelay(ulong playerID)
        {
            if (DynamicPVP != null && Convert.ToBoolean(DynamicPVP?.Call("IsPlayerInPVPDelay", playerID))) { return true; } // DynamicPVP

            if (RaidableBases != null && Convert.ToBoolean(RaidableBases?.Call("HasPVPDelay", playerID))) { return true; } // RaidableBases

            return false;
        }

        void OnPlayerPvpDelayStart(BasePlayer player) // AbandonedBases
        {
            CreatePVxUI(player, PVxType.PVPDelay);
        }
        #endregion PVPDelay

        #region Commands

        private void CmdServerPVx(IPlayer iPlayer, string command, string[] args)
        {
            if (!iPlayer.IsAdmin) return;
            if (args == null || args.Length < 1) return;
            switch (args[0].ToLower())
            {
                case "0":
                case "off":
                case "false":
                    _pvpAll = false;
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        CheckPlayerZone(player);
                    }
                    return;

                case "1":
                case "on":
                case "true":
                    _pvpAll = true;
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        CreatePVxUI(player, PVxType.PVP);
                    }
                    return;
            }
        }

        #endregion Commands

        #region UI

        private void CreatePVxUI(BasePlayer player, PVxType type)
        {
            UiSettings settings;
            if (!configData.UISettings.TryGetValue(type, out settings) || string.IsNullOrEmpty(settings.Json))
            {
                return;
            }
            CuiHelper.DestroyUi(player, UinameMain);
            CuiHelper.AddUi(player, settings.Json);
        }

        private static void DestroyUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UinameMain);
        }

        private static string GetCuiJson(UiSettings settings)
        {
            return new CuiElementContainer
            {
                {
                    new CuiPanel
                    {
                        Image = {Color = settings.BackgroundColor, FadeIn = settings.FadeIn,},
                        RectTransform = {AnchorMin = settings.MinAnchor, AnchorMax = settings.MaxAnchor, OffsetMin = settings.MinOffset, OffsetMax = settings.MaxOffset,},
                        FadeOut = settings.FadeOut,
                    },
                    settings.Layer, UinameMain
                },
                {
                    new CuiLabel
                    {
                        Text = {Text = settings.Text, FontSize = settings.TextSize, Align = TextAnchor.MiddleCenter, Color = settings.TextColor, FadeIn = settings.FadeIn,},
                        RectTransform = {AnchorMin = "0.05 0.05", AnchorMax = "0.95 0.95"}, FadeOut = settings.FadeOut,
                    },
                    UinameMain, CuiHelper.GetGuid()
                }
            }.ToJson();
        }

        #endregion UI

        #region ConfigurationFile

        private ConfigData configData;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Show Default PVx UI")] public bool showDefault = true;

            [JsonConverter(typeof(StringEnumConverter))]
            [JsonProperty(PropertyName = "Server Default PVx (pvp or pve)")] public PVxType defaultType = PVxType.PVE;

            [JsonProperty(PropertyName = "Pvx UI Settings")]
            public Dictionary<PVxType, UiSettings> UISettings { get; set; } = new Dictionary<PVxType, UiSettings>
            {
                [PVxType.PVE] = new UiSettings
                {
                    Text = "PVE",
                    TextSize = 14,
                    TextColor = "1 1 1 1",
                    BackgroundColor = "0.3 0.8 0.1 0.8"
                },
                [PVxType.PVP] = new UiSettings
                {
                    Text = "PVP",
                    TextSize = 14,
                    TextColor = "1 1 1 1",
                    BackgroundColor = "0.8 0.2 0.2 0.8"
                },
                [PVxType.PVPDelay] = new UiSettings
                {
                    Text = "PVP Delay",
                    TextSize = 12,
                    TextColor = "1 1 1 1",
                    BackgroundColor = "0.8 0.5 0.1 0.8"
                }
            };

            [JsonProperty(PropertyName = "Adem Event Settings")]
            public AdemEventSettings AdemEventSettings { get; set; } = new AdemEventSettings
            {
                convoyEvent = false,
                armoredTrainEvent = false,
                caravanEvent = false,
                sputnikEvent = false,
                spaceEvent = false,
                shipwreckEvent = false,
            };

            [JsonProperty(PropertyName = "KpucTaJl Event Settings")]
            public KpucTaJlEventSettings KpucTaJlEventSettings { get; set; } = new KpucTaJlEventSettings
            {
                powerPlantEvent = false,
                ferryTerminalEvent = false,
                supermarketEvent = false,
                junkyardEvent = false,
                arcticEvent = false,
                gasStationEvent = false,
                airEvent = false,
                harborEvent = false,
                satDishEvent = false,
                waterEvent = false,
                triangulationEvent = false,
                defendableHomesEvent = false,
            };
        }

        private class UiSettings
        {
            [JsonProperty(PropertyName = "Min Anchor")] public string MinAnchor { get; set; } = "0.5 0";

            [JsonProperty(PropertyName = "Max Anchor")] public string MaxAnchor { get; set; } = "0.5 0";

            [JsonProperty(PropertyName = "Min Offset")] public string MinOffset { get; set; } = "190 30";

            [JsonProperty(PropertyName = "Max Offset")] public string MaxOffset { get; set; } = "250 60";

            [JsonProperty(PropertyName = "Layer")] public string Layer { get; set; } = "Hud";

            [JsonProperty(PropertyName = "Text")] public string Text { get; set; } = "PVP";

            [JsonProperty(PropertyName = "Text Size")] public int TextSize { get; set; } = 12;

            [JsonProperty(PropertyName = "Text Color")] public string TextColor { get; set; } = "1 1 1 1";

            [JsonProperty(PropertyName = "Background Color")] public string BackgroundColor { get; set; } = "0.8 0.5 0.1 0.8";

            [JsonProperty(PropertyName = "Fade In")] public float FadeIn { get; set; } = 0.25f;

            [JsonProperty(PropertyName = "Fade Out")] public float FadeOut { get; set; } = 0.25f;

            private string _json;

            [JsonIgnore]
            public string Json
            {
                get
                {
                    if (string.IsNullOrEmpty(_json))
                    {
                        _json = GetCuiJson(this);
                    }
                    return _json;
                }
            }
        }

        private class AdemEventSettings
        {
            [JsonProperty(PropertyName = "Convoy Event")] public bool convoyEvent { get; set; }

            [JsonProperty(PropertyName = "Armored Train Event")] public bool armoredTrainEvent { get; set; }

            [JsonProperty(PropertyName = "Caravan Event")] public bool caravanEvent { get; set; }

            [JsonProperty(PropertyName = "Sputnik Event")] public bool sputnikEvent { get; set; }

            [JsonProperty(PropertyName = "Space Event")] public bool spaceEvent { get; set; }

            [JsonProperty(PropertyName = "Shipwreck Event")] public bool shipwreckEvent { get; set; }
        }

        private class KpucTaJlEventSettings
        {
            [JsonProperty(PropertyName = "Power Plant Event")] public bool powerPlantEvent { get; set; }

            [JsonProperty(PropertyName = "Ferry Terminal Event")] public bool ferryTerminalEvent { get; set; }

            [JsonProperty(PropertyName = "Supermarket Event")] public bool supermarketEvent { get; set; }

            [JsonProperty(PropertyName = "Junkyard Event")] public bool junkyardEvent { get; set; }

            [JsonProperty(PropertyName = "Arctic Base Event")] public bool arcticEvent { get; set; }

            [JsonProperty(PropertyName = "Gas Station Event")] public bool gasStationEvent { get; set; }

            [JsonProperty(PropertyName = "Air Event")] public bool airEvent { get; set; }

            [JsonProperty(PropertyName = "Harbor Event")] public bool harborEvent { get; set; }

            [JsonProperty(PropertyName = "Satellite Dish Event")] public bool satDishEvent { get; set; }

            [JsonProperty(PropertyName = "Water Event")] public bool waterEvent { get; set; }

            [JsonProperty(PropertyName = "Triangulation Event")] public bool triangulationEvent { get; set; }

            [JsonProperty(PropertyName = "Defendable Homes Event")] public bool defendableHomesEvent { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                configData = Config.ReadObject<ConfigData>();
                if (configData == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch (Exception ex)
            {
                PrintError($"The configuration file is corrupted. \n{ex}");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            configData = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(configData);
        }
        #endregion ConfigurationFile
    }
}