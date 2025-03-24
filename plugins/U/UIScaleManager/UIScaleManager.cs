using System;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("UI Scale Manager", "noname", "2.2.2")]
    [Description("User's UIScale data is stored and provided by API")]
    class UIScaleManager : CovalencePlugin
    {
        #region Field

        private delegate string LangDelegate(string key, string id = null, params object[] args);
        private static LangDelegate _langGlobal;
        private static VersionNumber _pluginVersion;

        private const float _defaultUIScale = 1.0f;
        private const int _defaultAspectRatioIndicator = 2;
        private static AspectRatioRaw[] _ratioPresets;
        private static int _defaultAspectRatioX;
        private static int _defaultAspectRatioY;

        private DataManager _dataManager;
        private UIController _uiController;
        private UIGenerator.UIScaler _uiScaler;

        private Dictionary<string, float[]> _playerUIInfoCache;

        private struct AspectRatioRaw
        {
            public int X { get; set; }
            public int Y { get; set; }

            public AspectRatioRaw(int x, int y)
            {
                X = x;
                Y = y;
            }
        }

        #endregion

        #region Hooks

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");

            Config.WriteObject(GetDefaultConfig(), true);
        }

        private void Init()
        {
            _langGlobal = Lang;
            _pluginVersion = this.Version;

            _ratioPresets = new AspectRatioRaw[]
            {
                new AspectRatioRaw(4,3),
                new AspectRatioRaw(5,4),
                new AspectRatioRaw(16,9),
                new AspectRatioRaw(17,9),
                new AspectRatioRaw(16,10),
                new AspectRatioRaw(21,9),
                new AspectRatioRaw(32,9),
                new AspectRatioRaw(32,10)
            };

            _defaultAspectRatioX = _ratioPresets[_defaultAspectRatioIndicator].X;
            _defaultAspectRatioY = _ratioPresets[_defaultAspectRatioIndicator].Y;

            _dataManager = new DataManager();
            _uiController = new UIController();
            _uiScaler = _uiController.UIScaler;

            _playerUIInfoCache = new Dictionary<string, float[]>();

            LoadConfig();

            var Result = _dataManager.LoadPlayersData();
            if (Result == DataManager.LoadResult.Updated)
                Puts("DataFile version has been updated");
        }

        private void OnServerInitialized()
        {
            if (config.InitPlayer)
            {
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    if (!_dataManager.Data.Players.ContainsKey(player.UserIDString))
                        _uiController.OpenUI(player, _defaultAspectRatioX, _defaultAspectRatioY, _defaultUIScale);
                }
            }
        }

        void OnServerSave()
        {
            _dataManager.SavePlayersData();
            Puts("Saving UIScaleManager PlayersDataFile...");
        }

        void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                _uiController.CloseUI(player);

            _dataManager.SavePlayersData();
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (config.InitPlayer)
            {
                timer.Once(1f, () =>
                {
                    if (!_dataManager.Data.Players.ContainsKey(player.UserIDString))
                        _uiController.OpenUI(player, _defaultAspectRatioX, _defaultAspectRatioY, _defaultUIScale);
                });
            }
        }

        private void OnUserDisconnected(IPlayer player)
        {
            _uiController.CloseUI(player.Object as BasePlayer);
        }

        #endregion

        #region ConfigManage

        private PluginConfig config;

        private void LoadConfig()
        {
            config = Config.ReadObject<PluginConfig>();

            if (config == null)
                config = GetDefaultConfig();

            VersionUpdate(config);
        }

        private void VersionUpdate(PluginConfig config)
        {
            //    if (config.ConfigVersion < new VersionNumber(2, 1, 0))
            //    {
            //        config.ConsoleFilter = GetDefaultConsoleFilter();
            //    }

            if (config.ConfigVersion < this.Version)
            {
                config.ConfigVersion = this.Version;
                Config.WriteObject(config, true);
                Puts("Config version has been updated");
            }
        }

        private class PluginConfig
        {
            public bool InitPlayer;
            public VersionNumber ConfigVersion;
        }

        private PluginConfig GetDefaultConfig()
        {
            return new PluginConfig
            {
                InitPlayer = true,
                ConfigVersion = _pluginVersion
            };
        }

        #endregion

        #region LangManage

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI Scale Data Changed"] = "Your UI data is setted to {0} {1}:{2}.",
                ["Cancel"] = "Canceled.",
                ["Error"] = "Unexpected Parameter.",
                ["YourCurrentAspectRatio"] = "Your Current Aspect Ratio Is",
                ["YourCurrentUISize"] = "Your Current UI Size Is",
                ["FitBelt"] = "<color=yellow>Please fit this rectangle to the item belt</color>",
                ["FitStatusW"] = "<color=yellow>Please fit this rectangle to the status window</color>"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["UI Scale Data Changed"] = "UI 데이터가 사이즈 {0}, 화면비율 {1}:{2} 로 저장되었습니다.",
                ["Cancel"] = "취소되었습니다.",
                ["Error"] = "예상치 못한 파라미터입니다.",
                ["YourCurrentAspectRatio"] = "화면비율 설정",
                ["YourCurrentUISize"] = "UI 사이즈 설정",
                ["FitBelt"] = "<color=yellow>이 사각형을 아이템벨트에 맞춰주세요</color>",
                ["FitStatusW"] = "<color=yellow>이 사각형을 상태창에 맞춰주세요</color>"
            }, this, "ko");
        }

        private string Lang(string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, id), args);
        }

        #endregion

        #region DataManage

        public class DataManager
        {
            public DataFile Data;
            private DynamicConfigFile _dynamicConfigFile;

            public DataManager()
            {
                _dynamicConfigFile = Interface.Oxide.DataFileSystem.GetDatafile("UIScaleManagerPlayerData");
            }

            public LoadResult LoadPlayersData()
            {
                Data = _dynamicConfigFile.ReadObject<DataFile>();

                if (Data == null)
                    Data = new DataFile();

                return VersionUpdate();
            }

            public void SavePlayersData()
            {
                _dynamicConfigFile.WriteObject<DataFile>(Data);
            }

            private LoadResult VersionUpdate()
            {
                if (Data.DataVersion < _pluginVersion)
                {
                    Data = new DataFile();
                    _dynamicConfigFile.WriteObject<DataFile>(Data);
                    return LoadResult.Updated;
                }
                return LoadResult.Ok;
            }

            public enum LoadResult
            {
                Ok,
                Updated
            }

            public class DataFile
            {
                public Dictionary<string, UIStatus> Players;//is readonly but Jsonserialize...
                public VersionNumber DataVersion;

                public DataFile()
                {
                    Players = new Dictionary<string, UIStatus>();
                    Players = Players;
                    DataVersion = _pluginVersion;
                }

                public void AddPlayerData(IPlayer player)
                {
                    var uiStatus = new UIStatus(player);
                    Players.Add(player.Id, uiStatus);
                }

                public void AddPlayerData(IPlayer player, float uiscale, int RatioIndicator)
                {
                    var uiStatus = new UIStatus(player, uiscale, RatioIndicator);
                    Players.Add(player.Id, uiStatus);
                }

                public class UIStatus
                {
                    public string Name { get; set; }

                    [JsonIgnore]
                    public float UIScale
                    {
                        get { return _uiScale; }
                        set
                        {
                            if (value < 0.5)
                                value = 0.5f;
                            else if (1.0f < value)
                                value = 1.0f;

                            value = (float)Math.Round(value, 7);
                            _uiScale = value;
                        }
                    }
                    /// <summary>
                    /// DO NOT USE IT
                    /// </summary>
                    [JsonProperty("UIScale")]
                    public float _uiScale;

                    [JsonProperty("AspectRatio")]
                    public AspectRatio Ratio { get; set; }

                    /// <summary>
                    /// DO NOT USE IT
                    /// </summary>
                    public UIStatus()
                    {
                        //for json serialize
                    }

                    public UIStatus(IPlayer player)
                    {
                        Name = player.Name;
                        UIScale = _defaultUIScale;
                        Ratio = new AspectRatio(_defaultAspectRatioIndicator);
                    }

                    public UIStatus(IPlayer player, float uiscale, int AspectRatioIndicator)
                    {
                        Name = player.Name;
                        UIScale = uiscale;
                        Ratio = new AspectRatio(AspectRatioIndicator);
                    }

                    public class AspectRatio
                    {
                        [JsonIgnore]
                        public int Indicator
                        {
                            get { return _indicator; }
                            set
                            {
                                if (value < 0)
                                {
                                    value = Mathf.Abs(value) % _ratioPresets.Length;
                                    value = _ratioPresets.Length - 1;
                                    if (value == _ratioPresets.Length)
                                        value -= 1;
                                }
                                else
                                    value %= _ratioPresets.Length;
                                _indicator = value;
                                AspectRatioRaw aspectRatioRaw = _ratioPresets[_indicator];
                                X = aspectRatioRaw.X;
                                Y = aspectRatioRaw.Y;
                            }
                        }
                        /// <summary>
                        /// DO NOT USE IT
                        /// </summary>
                        [JsonProperty("Indicator")]
                        public int _indicator;

                        /// <summary>
                        /// DO NOT SET IT
                        /// </summary>
                        public int X;
                        /// <summary>
                        /// DO NOT SET IT
                        /// </summary>
                        public int Y;

                        public AspectRatio()
                        {
                            Indicator = _defaultAspectRatioIndicator;
                        }

                        public AspectRatio(int indicator)
                        {
                            Indicator = indicator;
                        }
                    }
                }
            }
        }

        #endregion

        #region Command

        [Command("UIScaleManager.control")]
        void SetUIScaleCommand(IPlayer player, string command, string[] args)
        {
            if (player.IsServer) return;

            var bPlayer = player.Object as BasePlayer;

            if (args.Length >= 1)
            {
                if (args[0] == "ratio")
                {
                    if (args.Length >= 2)
                    {
                        int parsedResult;
                        if (int.TryParse(args[1], out parsedResult) == false)
                            player.Message(Lang("Error", player.Id));
                        else
                        {
                            DataManager.DataFile.UIStatus playerinfo;
                            if (!_dataManager.Data.Players.ContainsKey(player.Id))
                            {
                                _dataManager.Data.AddPlayerData(player, _defaultUIScale, _defaultAspectRatioIndicator + parsedResult);
                                playerinfo = _dataManager.Data.Players[player.Id];
                            }
                            else
                            {
                                playerinfo = _dataManager.Data.Players[player.Id];
                                playerinfo.Ratio.Indicator += parsedResult;
                            }
                            _uiController.UpdateDisplayAspectRatio(bPlayer, playerinfo.Ratio.X, playerinfo.Ratio.Y, playerinfo.UIScale);
                            player.Message(Lang("UI Scale Data Changed", player.Id, playerinfo.UIScale, playerinfo.Ratio.X, playerinfo.Ratio.Y));
                            if (_playerUIInfoCache.ContainsKey(player.Id))
                                _playerUIInfoCache.Remove(player.Id);
                            Interface.CallHook("OnUIScaleChanged", player, playerinfo.UIScale, playerinfo.Ratio.X, playerinfo.Ratio.Y);
                        }
                    }
                    else
                        player.Message(Lang("Error", player.Id));
                }
                else if (args[0] == "size")
                {
                    if (args.Length >= 2)
                    {
                        float parsedResult;

                        if (float.TryParse(args[1], out parsedResult) == false)
                        {
                            player.Message(Lang("Error", player.Id));
                        }
                        else
                        {
                            DataManager.DataFile.UIStatus playerinfo;
                            if (!_dataManager.Data.Players.ContainsKey(player.Id))
                            {
                                _dataManager.Data.AddPlayerData(player, _defaultUIScale + parsedResult, _defaultAspectRatioIndicator);
                                playerinfo = _dataManager.Data.Players[player.Id];
                            }
                            else
                            {
                                playerinfo = _dataManager.Data.Players[player.Id];
                                playerinfo.UIScale += parsedResult;
                            }
                            _uiController.UpdateDisplayUISize(bPlayer, playerinfo.Ratio.X, playerinfo.Ratio.Y, playerinfo.UIScale);
                            player.Message(Lang("UI Scale Data Changed", player.Id, playerinfo.UIScale, playerinfo.Ratio.X, playerinfo.Ratio.Y));
                            if (_playerUIInfoCache.ContainsKey(player.Id))
                                _playerUIInfoCache.Remove(player.Id);
                            Interface.CallHook("OnUIScaleChanged", player, playerinfo.UIScale, playerinfo.Ratio.X, playerinfo.Ratio.Y);
                        }
                    }
                    else
                        player.Message(Lang("Error", player.Id));
                }
                else
                    player.Message(Lang("Error", player.Id));
            }
            else
            {
                if (!_dataManager.Data.Players.ContainsKey(player.Id))
                {
                    _dataManager.Data.AddPlayerData(player);
                    player.Message(Lang("UI Scale Data Changed", player.Id, _defaultUIScale, _defaultAspectRatioX, _defaultAspectRatioY));
                    if (_playerUIInfoCache.ContainsKey(player.Id))
                        _playerUIInfoCache.Remove(player.Id);
                    Interface.CallHook("OnUIScaleChanged", player, _defaultUIScale, _defaultAspectRatioX, _defaultAspectRatioY);
                    _uiController.CloseUI(bPlayer);
                }
                else
                {
                    _uiController.CloseUI(bPlayer);
                }
            }
        }

        [Command("setui")]
        void SetUIScaleOpenCommand(IPlayer player, string command, string[] args)
        {
            if (player.IsServer) return;

            var bPlayer = player.Object as BasePlayer;
            if (!_dataManager.Data.Players.ContainsKey(player.Id))
                _uiController.OpenUI(bPlayer, _defaultAspectRatioX, _defaultAspectRatioY, _defaultUIScale);
            else
            {
                var playerinfo = _dataManager.Data.Players[player.Id];
                _uiController.OpenUI(bPlayer, playerinfo.Ratio.X, playerinfo.Ratio.Y, playerinfo.UIScale);
            }
        }

        #endregion

        #region API

        private float[] API_CheckPlayerUIInfo(string playerID)
        {
            if (_playerUIInfoCache.ContainsKey(playerID))
                return _playerUIInfoCache[playerID];
            else if (_dataManager.Data.Players.ContainsKey(playerID))
            {
                var uIStatus = _dataManager.Data.Players[playerID];
                float[] playerinfo =  new float[] {
                    uIStatus.Ratio.X,
                    uIStatus.Ratio.Y,
                    uIStatus.UIScale
                };
                _playerUIInfoCache.Add(playerID, playerinfo);
                return playerinfo;
            }
            else
                return null;
        }

        private float[] API_GetItemBeltAnchor(int ratioX, int ratioY, float uiSize)
        {
            var dscalingParameter =  new UIGenerator.UIScaler.DScalingParameter(ratioX, ratioY, uiSize);

            if (_uiController.UIScaler.ItemBeltResultCache.ContainsKey(dscalingParameter))
                return _uiScaler.ItemBeltResultCache[dscalingParameter].Result;
            else
            {
                _uiScaler.ItemBeltScaleCaching(ratioX, ratioY, uiSize);
                return _uiScaler.ItemBeltResultCache[dscalingParameter].Result;
            }
        }
        
        private float[] API_GetItemBeltAnchor(string playerID)
        {
            if (_dataManager.Data.Players.ContainsKey(playerID))
            {
                var uIStatus = _dataManager.Data.Players[playerID];
                return API_GetItemBeltAnchor(uIStatus.Ratio.X, uIStatus.Ratio.Y, uIStatus.UIScale);
            }
            else
                return null;
        }

        private float[] API_GetStatusWindowAnchor(int ratioX, int ratioY, float uiSize)
        {
            var dscalingParameter = new UIGenerator.UIScaler.DScalingParameter(ratioX, ratioY, uiSize);

            if (_uiController.UIScaler.StatusWindowResultCache.ContainsKey(dscalingParameter))
                return _uiScaler.StatusWindowResultCache[dscalingParameter].Result;
            else
            {
                _uiScaler.StatusWindowScaleCaching(ratioX, ratioY, uiSize);
                return _uiScaler.StatusWindowResultCache[dscalingParameter].Result;
            }
        }

        private float[] API_GetStatusWindowAnchor(string playerID)
        {
            if (_dataManager.Data.Players.ContainsKey(playerID))
            {
                var uIStatus = _dataManager.Data.Players[playerID];
                return API_GetStatusWindowAnchor(uIStatus.Ratio.X, uIStatus.Ratio.Y, uIStatus.UIScale);
            }
            else
                return null;
        }

        private float[] API_AutoAnchorScaling(float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY, int center, int parent, int ratioX, int ratioY, float uiSize)
        {
            var scalingResult = _uiScaler.UniversalPanelScaling(new UIGenerator.StackRectAnchor(anchorMinX, anchorMinY, anchorMaxX, anchorMaxY), (UIGenerator.RootRectAnchor.RectCenter)center, (UIGenerator.UIScaler.ScalingParent)parent, ratioX, ratioY, uiSize);
            return scalingResult.Result;
        }

        private float[] API_AutoAnchorScaling(float anchorMinX, float anchorMinY, float anchorMaxX, float anchorMaxY, int center, int parent, string playerID)
        {
            if (_dataManager.Data.Players.ContainsKey(playerID))
            {
                var uiStatus = _dataManager.Data.Players[playerID];
                return API_AutoAnchorScaling(anchorMinX, anchorMinY, anchorMaxX, anchorMaxY, center, parent, uiStatus.Ratio.X, uiStatus.Ratio.Y, uiStatus.UIScale);
            }
            else
                return null;
        }

        #endregion

        #region UI

        public class UIController
        {
            public UIGenerator.UIScaler UIScaler { get { return _uiGenerator.Scaler; } }

            private UIGenerator _uiGenerator;
            private List<string> _uiUsers;

            public UIController()
            {
                _uiGenerator = new UIGenerator();
                _uiUsers = new List<string>();
            }

            public void OpenUI(BasePlayer player, int ratioX, int ratioY, float uiSize)
            {
                if (_uiUsers.Contains(player.UserIDString))
                    return;

                _uiUsers.Add(player.UserIDString);
                _uiGenerator.UIInit(player);
                _uiGenerator.DrawAspectRatioLabel(player, ratioX, ratioY);
                _uiGenerator.DrawSizeLabel(player, uiSize);
                _uiGenerator.DrawUIGuidePanel(player, ratioX, ratioY, uiSize);
            }

            public void CloseUI(BasePlayer player)
            {
                if (!_uiUsers.Contains(player.UserIDString))
                    return;

                _uiGenerator.DestroyMainUI(player);
                _uiUsers.Remove(player.UserIDString);
            }

            public void UpdateDisplayAspectRatio(BasePlayer player, int ratioX, int ratioY, float uiSize)
            {
                if (!_uiUsers.Contains(player.UserIDString))
                    return;

                _uiGenerator.DestroyAspectRatioLabel(player);
                _uiGenerator.DrawAspectRatioLabel(player, ratioX, ratioY);
                _uiGenerator.DestoryUIGuidePanel(player);
                _uiGenerator.DrawUIGuidePanel(player, ratioX, ratioY, uiSize);
            }

            public void UpdateDisplayUISize(BasePlayer player, int ratioX, int ratioY, float uiSize)
            {
                if (!_uiUsers.Contains(player.UserIDString))
                    return;

                _uiGenerator.DestroySizeLabel(player);
                _uiGenerator.DrawSizeLabel(player, uiSize);
                _uiGenerator.DestoryUIGuidePanel(player);
                _uiGenerator.DrawUIGuidePanel(player, ratioX, ratioY, uiSize);
            }
        }

        public class UIGenerator
        {
            public UIScaler Scaler;

            private const string _main = "USM_Main";
            private const string _mainInner = "USM_MainInner";
            private const string _innerUp = "USM_InnerUp";
            private const string _innerDown = "USM_InnerDown";
            private const string _ratioLabel = "USM_RatioLabel";
            private const string _sizeLabel = "USM_SizeLabel";
            private const string _rButtonRatioPanel = "USM_RButtonRatioPanel";
            private const string _lButtonRatioPanel = "USM_LButtonRatioPanel";
            private const string _rButtonSizePanel = "USM_RButtonSizePanel";
            private const string _lButtonSizePanel = "USM_LButtonSizePanel";
            private const string _itemBeltPanel = "USM_ItemBeltPanel";
            private const string _statusWindowPanel = "USM_StatusWindowPanel";

            public const float UnitCorrection = 0.0031f;
            private static float _xunit;
            private static float _yunit;
            private readonly UniversalRectAnchor _mainInnerPanelAnchor;
            private readonly UniversalRectAnchor _innerUpPanelAnchor;
            private readonly UniversalRectAnchor _innerDownPanelAnchor;
            private readonly UniversalRectAnchor _cLabelAnchor;
            private readonly UniversalRectAnchor _valueLabelAnchor;
            private readonly UniversalRectAnchor _rButtonPanel;
            private readonly UniversalRectAnchor _lButtonPanel;
            private readonly UniversalRectAnchor _buttonAnchor;

            private static UniversalRectAnchor _itemBeltAnchor;
            private static UniversalRectAnchor _statusWindowAnchor;
            private readonly UniversalRectAnchor _textPadding;

            public UIGenerator()
            {
                Scaler = new UIScaler();

                _xunit = _defaultAspectRatioY * 0.01f;
                _yunit = _defaultAspectRatioX * 0.01f;

                _mainInnerPanelAnchor = new UniversalRectAnchor((_xunit * 5) - UnitCorrection, (_yunit * 4) - UnitCorrection);
                _mainInnerPanelAnchor.AlignCenterX();
                _mainInnerPanelAnchor.AlignCenterY();
                _mainInnerPanelAnchor.MoveY(_yunit / 2);
                
                _innerUpPanelAnchor = new UniversalRectAnchor(0,                  0.5f,
                                                              1 - UnitCorrection, 1.0f - UnitCorrection);
                _innerDownPanelAnchor = new UniversalRectAnchor(0,                  0,
                                                              1 - UnitCorrection, 0.5f - UnitCorrection);

                _cLabelAnchor = new UniversalRectAnchor(0, 0, 1, 0.2f - UnitCorrection);
                _cLabelAnchor.MoveYEnd();

                _valueLabelAnchor = new UniversalRectAnchor(0, 0, 0.601f, 0.8f - UnitCorrection);
                _valueLabelAnchor.AlignCenterX();

                _rButtonPanel = new UniversalRectAnchor(0, 0, 0.2f - UnitCorrection, 0.8f - UnitCorrection);
                _rButtonPanel.MoveXEnd();
                _lButtonPanel = new UniversalRectAnchor(0, 0, 0.2f - UnitCorrection, 0.8f - UnitCorrection);

                _buttonAnchor = new UniversalRectAnchor(0.9f, 0.9f);
                _buttonAnchor.AlignCenterX();
                _buttonAnchor.AlignCenterY();

                _itemBeltAnchor = new UniversalRectAnchor(0.312f, 0.082f);
                _statusWindowAnchor = new UniversalRectAnchor(0.149f, 0.112f);

                _textPadding = new UniversalRectAnchor(0.9f, 0.9f);
                _textPadding.AlignCenterX();
                _textPadding.AlignCenterY();
            }

            public void UIInit(BasePlayer player)
            {
                CuiElementContainer UIInstance = new CuiElementContainer();
                string CRatio = _langGlobal("YourCurrentAspectRatio", player.UserIDString);
                string CUISize = _langGlobal("YourCurrentUISize", player.UserIDString);

                //////////////////////////////////////////////////////////mainUI

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "0 0 0 0"
                        },

                    RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        },
                }, "Overlay", _main);

                UIInstance.Add(new CuiButton
                {
                    RectTransform =
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1"
                        },
                    Button =
                        {
                            Color = "0 0 0 0",
                            Command = "UIScaleManager.control"
                        },
                    Text =
                        {
                            Text = "",
                            FontSize = 13,
                            Align = TextAnchor.MiddleCenter
                        }
                }, _main);
                
                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "0 0 0 0.5"
                        },

                    RectTransform =
                        {
                            AnchorMin = _mainInnerPanelAnchor.AnchorMin,
                            AnchorMax = _mainInnerPanelAnchor.AnchorMax
                        },
                }, _main, _mainInner);

                ////////////////////////////////////////InnerUp
                
                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "0 0 0 0"
                        },

                    RectTransform =
                        {
                            AnchorMin = _innerUpPanelAnchor.AnchorMin,
                            AnchorMax = _innerUpPanelAnchor.AnchorMax
                        },
                }, _mainInner, _innerUp);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "0 0 0 0.5"
                        },

                    RectTransform =
                        {
                            AnchorMin = _cLabelAnchor.AnchorMin,
                            AnchorMax = _cLabelAnchor.AnchorMax
                        },
                }, _innerUp);

                UIInstance.Add(new CuiLabel
                {
                    Text =
                        {
                            Text = CRatio,
                            FontSize = 25,
                            Align = TextAnchor.MiddleCenter
                        },
                    RectTransform =
                        {
                            AnchorMin = _cLabelAnchor.AnchorMin,
                            AnchorMax = _cLabelAnchor.AnchorMax
                        }
                }, _innerUp);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "0 0 0 0"
                        },

                    RectTransform =
                        {
                            AnchorMin = _lButtonPanel.AnchorMin,
                            AnchorMax = _lButtonPanel.AnchorMax
                        },
                }, _innerUp, _lButtonRatioPanel);
                
                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "0 0 0 0"
                        },

                    RectTransform =
                        {
                            AnchorMin = _rButtonPanel.AnchorMin,
                            AnchorMax = _rButtonPanel.AnchorMax
                        },
                }, _innerUp, _rButtonRatioPanel);

                UIInstance.Add(new CuiButton
                {
                    RectTransform =
                        {
                            AnchorMin = _buttonAnchor.AnchorMin,
                            AnchorMax = _buttonAnchor.AnchorMax
                        },
                    Button =
                        {
                            Color = "0 0 0 0",
                            Command = "UIScaleManager.control ratio -1"
                        },
                    Text =
                        {
                            Text = "<",
                            FontSize = 50,
                            Align = TextAnchor.MiddleCenter
                        }
                }, _lButtonRatioPanel);

                UIInstance.Add(new CuiButton
                {
                    RectTransform =
                        {
                            AnchorMin = _buttonAnchor.AnchorMin,
                            AnchorMax = _buttonAnchor.AnchorMax
                        },
                    Button =
                        {
                            Color = "0 0 0 0",
                            Command = "UIScaleManager.control ratio 1"
                        },
                    Text =
                        {
                            Text = ">",
                            FontSize = 50,
                            Align = TextAnchor.MiddleCenter
                        }
                }, _rButtonRatioPanel);

                ////////////////////////////////////////InnerUp

                ////////////////////////////////////////InnerDown

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "0 0 0 0"
                        },

                    RectTransform =
                        {
                            AnchorMin = _innerDownPanelAnchor.AnchorMin,
                            AnchorMax = _innerDownPanelAnchor.AnchorMax
                        },
                }, _mainInner, _innerDown);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "0 0 0 0.5"
                        },

                    RectTransform =
                        {
                            AnchorMin = _cLabelAnchor.AnchorMin,
                            AnchorMax = _cLabelAnchor.AnchorMax
                        },
                }, _innerDown);

                UIInstance.Add(new CuiLabel
                {
                    Text =
                        {
                            Text = CUISize,
                            FontSize = 25,
                            Align = TextAnchor.MiddleCenter
                        },
                    RectTransform =
                        {
                            AnchorMin = _cLabelAnchor.AnchorMin,
                            AnchorMax = _cLabelAnchor.AnchorMax
                        }
                }, _innerDown);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "0 0 0 0"
                        },

                    RectTransform =
                        {
                            AnchorMin = _lButtonPanel.AnchorMin,
                            AnchorMax = _lButtonPanel.AnchorMax
                        },
                }, _innerDown, _lButtonSizePanel);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "0 0 0 0"
                        },

                    RectTransform =
                        {
                            AnchorMin = _rButtonPanel.AnchorMin,
                            AnchorMax = _rButtonPanel.AnchorMax
                        },
                }, _innerDown, _rButtonSizePanel);

                UIInstance.Add(new CuiButton
                {
                    RectTransform =
                        {
                            AnchorMin = _buttonAnchor.AnchorMin,
                            AnchorMax = _buttonAnchor.AnchorMax
                        },
                    Button =
                        {
                            Color = "0 0 0 0",
                            Command = "UIScaleManager.control size -0.1"
                        },
                    Text =
                        {
                            Text = "<",
                            FontSize = 50,
                            Align = TextAnchor.MiddleCenter
                        }
                }, _lButtonSizePanel);

                UIInstance.Add(new CuiButton
                {
                    RectTransform =
                        {
                            AnchorMin = _buttonAnchor.AnchorMin,
                            AnchorMax = _buttonAnchor.AnchorMax
                        },
                    Button =
                        {
                            Color = "0 0 0 0",
                            Command = "UIScaleManager.control size 0.1"
                        },
                    Text =
                        {
                            Text = ">",
                            FontSize = 50,
                            Align = TextAnchor.MiddleCenter
                        }
                }, _rButtonSizePanel);

                ////////////////////////////////////////InnerDown

                ////////////////////////////////////////////////////////mainUI

                CuiHelper.AddUi(player, UIInstance);
            }

            public void DestroyMainUI(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, _main);
            }

            public void DrawAspectRatioLabel(BasePlayer player, int ratioX, int ratioY)
            {
                CuiElementContainer UIInstance = new CuiElementContainer();

                UIInstance.Add(new CuiLabel
                {
                    Text =
                        {
                            Text = $"{ratioX}:{ratioY}",
                            FontSize = 140,
                            Align = TextAnchor.MiddleCenter
                        },
                    RectTransform =
                        {
                            AnchorMin = _valueLabelAnchor.AnchorMin,
                            AnchorMax = _valueLabelAnchor.AnchorMax
                        }
                }, _innerUp, _ratioLabel);

                CuiHelper.AddUi(player, UIInstance);
            }

            public void DestroyAspectRatioLabel(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, _ratioLabel);
            }

            public void DrawSizeLabel(BasePlayer player, float uiSize)
            {
                CuiElementContainer UIInstance = new CuiElementContainer();

                UIInstance.Add(new CuiLabel
                {
                    Text =
                        {
                            Text = uiSize.ToString("0.0"),
                            FontSize = 140,
                            Align = TextAnchor.MiddleCenter
                        },
                    RectTransform =
                        {
                            AnchorMin = _valueLabelAnchor.AnchorMin,
                            AnchorMax = _valueLabelAnchor.AnchorMax
                        }
                }, _innerDown, _sizeLabel);

                CuiHelper.AddUi(player, UIInstance);
            }

            public void DestroySizeLabel(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, _sizeLabel);
            }

            public void DrawUIGuidePanel(BasePlayer player, int ratioX, int ratioY, float uiSize)
            {
                string fitBelt = _langGlobal("FitBelt", player.UserIDString);
                string fitWindow = _langGlobal("FitStatusW", player.UserIDString);

                CuiElementContainer uiInstance = new CuiElementContainer();

                UniversalRectAnchor scaledBeltAnchor = _itemBeltAnchor.Clone();
                Scaler.ItemBeltScaling(ref scaledBeltAnchor, ratioX, ratioY, uiSize);

                uiInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "0 0 0 0.5"
                        },

                    RectTransform =
                        {
                            AnchorMin = scaledBeltAnchor.AnchorMin,
                            AnchorMax = scaledBeltAnchor.AnchorMax
                        },
                }, _main, _itemBeltPanel);

                uiInstance.Add(new CuiLabel
                {
                    Text =
                        {
                            Text = fitBelt,
                            FontSize = (int)(18 * uiSize),
                            Align = TextAnchor.MiddleCenter
                        },
                    RectTransform =
                        {
                            AnchorMin = _textPadding.AnchorMin,
                            AnchorMax = _textPadding.AnchorMax
                        }
                }, _itemBeltPanel);
                uiInstance.AddRange(GetOutline(_itemBeltPanel, 0.009f, 0.06f));

                UniversalRectAnchor ScaledWindowAnchor = _statusWindowAnchor.Clone();
                Scaler.StatusWindowScaling(ref ScaledWindowAnchor, ratioX, ratioY, uiSize);

                uiInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "0 0 0 0.5"
                        },

                    RectTransform =
                        {
                            AnchorMin = ScaledWindowAnchor.AnchorMin,
                            AnchorMax = ScaledWindowAnchor.AnchorMax
                        },
                }, _main, _statusWindowPanel);

                uiInstance.Add(new CuiLabel
                {
                    Text =
                        {
                            Text = fitWindow,
                            FontSize = (int)(18 * uiSize),
                            Align = TextAnchor.MiddleCenter
                        },
                    RectTransform =
                        {
                            AnchorMin = _textPadding.AnchorMin,
                            AnchorMax = _textPadding.AnchorMax
                        }
                }, _statusWindowPanel);
                uiInstance.AddRange(GetOutline(_statusWindowPanel, 0.015f, 0.049f));

                #region test code
                /*
                CuiHelper.DestroyUi(player, "TestPanel");
                StackRectAnchor TestPanelAnchor = new StackRectAnchor(0.1f, 0.12f, 0.34f, 0.2f);
                UIScaler.ScalingResult scalingResult = uiScaler.UniversalPanelScaling(TestPanelAnchor, RootRectAnchor.RectCenter.LowerRight, UIScaler.ScalingParent.ItemBeltUpperLeft, RatioX, RatioY, UISize);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "1 1 0 1"
                        },

                    RectTransform =
                        {
                            AnchorMin = scalingResult.AnchorMinX + " " + scalingResult.AnchorMinY,
                            AnchorMax = scalingResult.AnchorMaxX + " " + scalingResult.AnchorMaxY
                        },
                }, Main, "TestPanel");

                CuiHelper.DestroyUi(player, "TestPanel2");
                StackRectAnchor TestPanelAnchor2 = new StackRectAnchor(0.65f, 0.12f, 0.8f, 0.2f);
                UIScaler.ScalingResult scalingResult2 = uiScaler.UniversalPanelScaling(TestPanelAnchor2, RootRectAnchor.RectCenter.LowerLeft, UIScaler.ScalingParent.ItemBeltUpperRight, RatioX, RatioY, UISize);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "1 1 0 1"
                        },

                    RectTransform =
                        {
                            AnchorMin = scalingResult2.AnchorMinX + " " + scalingResult2.AnchorMinY,
                            AnchorMax = scalingResult2.AnchorMaxX + " " + scalingResult2.AnchorMaxY
                        },
                }, Main, "TestPanel2");
                CuiHelper.DestroyUi(player, "TestPanel3");
                StackRectAnchor TestPanelAnchor3 = new StackRectAnchor(0.4f, 0.11f, 0.6f, 0.2f);
                UIScaler.ScalingResult scalingResult3 = uiScaler.UniversalPanelScaling(TestPanelAnchor3, RootRectAnchor.RectCenter.LowerCenter, UIScaler.ScalingParent.ItemBeltUpperCenter, RatioX, RatioY, UISize);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "1 1 0 1"
                        },

                    RectTransform =
                        {
                            AnchorMin = scalingResult3.AnchorMinX + " " + scalingResult3.AnchorMinY,
                            AnchorMax = scalingResult3.AnchorMaxX + " " + scalingResult3.AnchorMaxY
                        },
                }, Main, "TestPanel3");
                CuiHelper.DestroyUi(player, "TestPanel4");
                StackRectAnchor TestPanelAnchor4 = new StackRectAnchor(0.1f, 0.04f, 0.32f, 0.09f);
                UIScaler.ScalingResult scalingResult4 = uiScaler.UniversalPanelScaling(TestPanelAnchor4, RootRectAnchor.RectCenter.MiddleRight, UIScaler.ScalingParent.ItemBeltMiddleLeft, RatioX, RatioY, UISize);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "1 1 0 1"
                        },

                    RectTransform =
                        {
                            AnchorMin = scalingResult4.AnchorMinX + " " + scalingResult4.AnchorMinY,
                            AnchorMax = scalingResult4.AnchorMaxX + " " + scalingResult4.AnchorMaxY
                        },
                }, Main, "TestPanel4");
                CuiHelper.DestroyUi(player, "TestPanel5");
                StackRectAnchor TestPanelAnchor5 = new StackRectAnchor(0.45f, 0.04f, 0.556f, 0.09f);
                UIScaler.ScalingResult scalingResult5 = uiScaler.UniversalPanelScaling(TestPanelAnchor5, RootRectAnchor.RectCenter.MiddleCenter, UIScaler.ScalingParent.ItemBeltMiddleCenter, RatioX, RatioY, UISize);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "1 1 0 1"
                        },

                    RectTransform =
                        {
                            AnchorMin = scalingResult5.AnchorMinX + " " + scalingResult5.AnchorMinY,
                            AnchorMax = scalingResult5.AnchorMaxX + " " + scalingResult5.AnchorMaxY
                        },
                }, Main, "TestPanel5");
                CuiHelper.DestroyUi(player, "TestPanel6");
                StackRectAnchor TestPanelAnchor6 = new StackRectAnchor(0.7f, 0.04f, 0.8f, 0.09f);
                UIScaler.ScalingResult scalingResult6 = uiScaler.UniversalPanelScaling(TestPanelAnchor6, RootRectAnchor.RectCenter.MiddleLeft, UIScaler.ScalingParent.ItemBeltMiddleRight, RatioX, RatioY, UISize);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "1 1 0 1"
                        },

                    RectTransform =
                        {
                            AnchorMin = scalingResult6.AnchorMinX + " " + scalingResult6.AnchorMinY,
                            AnchorMax = scalingResult6.AnchorMaxX + " " + scalingResult6.AnchorMaxY
                        },
                }, Main, "TestPanel6");

                CuiHelper.DestroyUi(player, "TestPanel7");
                StackRectAnchor TestPanelAnchor7 = new StackRectAnchor(0.1f, 0.01f, 0.32f, 0.02f);
                UIScaler.ScalingResult scalingResult7 = uiScaler.UniversalPanelScaling(TestPanelAnchor7, RootRectAnchor.RectCenter.UpperRight, UIScaler.ScalingParent.ItemBeltMiddleLeft, RatioX, RatioY, UISize);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "1 1 0 1"
                        },

                    RectTransform =
                        {
                            AnchorMin = scalingResult7.AnchorMinX + " " + scalingResult7.AnchorMinY,
                            AnchorMax = scalingResult7.AnchorMaxX + " " + scalingResult7.AnchorMaxY
                        },
                }, Main, "TestPanel7");
                CuiHelper.DestroyUi(player, "TestPanel8");
                StackRectAnchor TestPanelAnchor8 = new StackRectAnchor(0.45f, 0.01f, 0.556f, 0.02f);
                UIScaler.ScalingResult scalingResult8 = uiScaler.UniversalPanelScaling(TestPanelAnchor8, RootRectAnchor.RectCenter.UpperCenter, UIScaler.ScalingParent.ItemBeltMiddleCenter, RatioX, RatioY, UISize);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "1 1 0 1"
                        },

                    RectTransform =
                        {
                            AnchorMin = scalingResult8.AnchorMinX + " " + scalingResult8.AnchorMinY,
                            AnchorMax = scalingResult8.AnchorMaxX + " " + scalingResult8.AnchorMaxY
                        },
                }, Main, "TestPanel8");
                CuiHelper.DestroyUi(player, "TestPanel9");
                StackRectAnchor TestPanelAnchor9 = new StackRectAnchor(0.7f, 0.01f, 0.8f, 0.02f);
                UIScaler.ScalingResult scalingResult9 = uiScaler.UniversalPanelScaling(TestPanelAnchor9, RootRectAnchor.RectCenter.UpperLeft, UIScaler.ScalingParent.ItemBeltMiddleRight, RatioX, RatioY, UISize);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "1 1 0 1"
                        },

                    RectTransform =
                        {
                            AnchorMin = scalingResult9.AnchorMinX + " " + scalingResult9.AnchorMinY,
                            AnchorMax = scalingResult9.AnchorMaxX + " " + scalingResult9.AnchorMaxY
                        },
                }, Main, "TestPanel9");

                CuiHelper.DestroyUi(player, "TestPanel10");
                StackRectAnchor TestPanelAnchor10 = new StackRectAnchor(0 + 0.42f, 0 + 0.22f, Xunit + 0.42f, Yunit + 0.22f);
                UIScaler.ScalingResult scalingResult10 = uiScaler.UniversalPanelScaling(TestPanelAnchor10, RootRectAnchor.RectCenter.LowerLeft, UIScaler.ScalingParent.MainMiddleLeft, RatioX, RatioY, UISize);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "1 1 0 1"
                        },

                    RectTransform =
                        {
                            AnchorMin = scalingResult10.AnchorMinX + " " + scalingResult10.AnchorMinY,
                            AnchorMax = scalingResult10.AnchorMaxX + " " + scalingResult10.AnchorMaxY
                        },
                }, Main, "TestPanel10");
                */
                #endregion

                CuiHelper.AddUi(player, uiInstance);
            }

            public void DestoryUIGuidePanel(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, _itemBeltPanel);
                CuiHelper.DestroyUi(player, _statusWindowPanel);
            }

            public CuiElementContainer GetOutline(string parent, float xsize, float ysize)
            {
                CuiElementContainer UIInstance = new CuiElementContainer();

                UniversalRectAnchor LeftBarRectAnchor = new UniversalRectAnchor( 0,         0,         xsize - UnitCorrection, 1 - 0.02f);
                UniversalRectAnchor RightBarRectAnchor = new UniversalRectAnchor(1 - xsize, 0,         1 - UnitCorrection,     1 - 0.02f);
                UniversalRectAnchor UpBarRectAnchor = new UniversalRectAnchor(   0,         1 - ysize, 1 - 0.003f,             1 - 0.02f);
                UniversalRectAnchor DownBarRectAnchor = new UniversalRectAnchor( 0,         0,         1 - 0.003f,             ysize - 0.02f);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "1 1 0 1"
                        },

                    RectTransform =
                        {
                            AnchorMin = LeftBarRectAnchor.AnchorMin,
                            AnchorMax = LeftBarRectAnchor.AnchorMax
                        },
                }, parent);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "1 1 0 1"
                        },

                    RectTransform =
                        {
                            AnchorMin = RightBarRectAnchor.AnchorMin,
                            AnchorMax = RightBarRectAnchor.AnchorMax
                        },
                }, parent);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "1 1 0 1"
                        },

                    RectTransform =
                        {
                            AnchorMin = UpBarRectAnchor.AnchorMin,
                            AnchorMax = UpBarRectAnchor.AnchorMax
                        },
                }, parent);

                UIInstance.Add(new CuiPanel
                {
                    CursorEnabled = true,
                    Image =
                        {
                            Color = "1 1 0 1"
                        },

                    RectTransform =
                        {
                            AnchorMin = DownBarRectAnchor.AnchorMin,
                            AnchorMax = DownBarRectAnchor.AnchorMax
                        },
                }, parent);

                return UIInstance;
            }

            public class UniversalRectAnchor
            {
                public float AnchorCenterX { get { return AnchorMinX + ((AnchorMaxX - AnchorMinX) / 2); } }
                public float AnchorCenterY { get { return AnchorMinY + ((AnchorMaxY - AnchorMinY) / 2); } }

                public float AnchorMinX { get; set; }
                public float AnchorMinY { get; set; }
                public float AnchorMaxX { get; set; }
                public float AnchorMaxY { get; set; }

                public string AnchorMin { get { return $"{AnchorMinX} {AnchorMinY}"; } }

                public string AnchorMax { get { return $"{AnchorMaxX} {AnchorMaxY}"; } }

                public UniversalRectAnchor(float maxX, float maxY)
                {
                    AnchorMinX = 0;
                    AnchorMinY = 0;
                    AnchorMaxX = maxX;
                    AnchorMaxY = maxY;
                }

                public UniversalRectAnchor(float minX, float minY, float maxX, float maxY)
                {
                    AnchorMinX = minX;
                    AnchorMinY = minY;
                    AnchorMaxX = maxX;
                    AnchorMaxY = maxY;
                }

                public UniversalRectAnchor(UIScaler.ScalingResult scalingResult)
                {
                    AnchorMinX = scalingResult.AnchorMinX;
                    AnchorMinY = scalingResult.AnchorMinY;
                    AnchorMaxX = scalingResult.AnchorMaxX;
                    AnchorMaxY = scalingResult.AnchorMaxY;
                }

                public void MoveX(float value)
                {
                    AnchorMinX += value;
                    AnchorMaxX += value;
                }

                public void MoveY(float value)
                {
                    AnchorMinY += value;
                    AnchorMaxY += value;
                }

                public void MoveXStart()
                {
                    MoveX(AnchorMinX * -1f);
                }

                public void MoveXEnd()
                {
                    MoveX(AnchorMinX * -1f);
                    MoveX(1.0f - AnchorMaxX - AnchorMinX);
                }

                public void MoveYStart()
                {
                    MoveY(AnchorMinY * -1f);
                }

                public void MoveYEnd()
                {
                    MoveY(AnchorMinY * -1f);
                    MoveY(1.0f - AnchorMaxY - AnchorMinY);
                }

                public void AlignCenterX()
                {
                    float MoveValue = (1.0f - (AnchorMaxX - AnchorMinX)) / 2;
                    MoveX(MoveValue);
                }

                public void AlignCenterY()
                {
                    float MoveValue = (1.0f - (AnchorMaxY - AnchorMinY)) / 2;
                    MoveY(MoveValue);
                }

                public UniversalRectAnchor Clone() => new UniversalRectAnchor(AnchorMinX, AnchorMinY, AnchorMaxX, AnchorMaxY);
            }

            public class RootRectAnchor
            {
                public float AnchorMinX
                {
                    get
                    {
                        switch (rectCenterPreset)
                        {
                            case RectCenter.UpperLeft:
                                return PositionX;
                            case RectCenter.UpperCenter:
                                return PositionX - ScaleX / 2;
                            case RectCenter.UpperRight:
                                return PositionX - ScaleX;

                            case RectCenter.MiddleLeft:
                                return PositionX;
                            case RectCenter.MiddleCenter:
                                return PositionX - ScaleX / 2;
                            case RectCenter.MiddleRight:
                                return PositionX - ScaleX;

                            case RectCenter.LowerLeft:
                                return PositionX;
                            case RectCenter.LowerCenter:
                                return PositionX - ScaleX / 2;
                            case RectCenter.LowerRight:
                                return PositionX - ScaleX;

                            default:
                                return PositionX;
                        }
                    }
                }
                public float AnchorMinY 
                {
                    get
                    {
                        switch (rectCenterPreset)
                        {
                            case RectCenter.UpperLeft:
                                return PositionY - ScaleY;
                            case RectCenter.UpperCenter:
                                return PositionY - ScaleY;
                            case RectCenter.UpperRight:
                                return PositionY - ScaleY;

                            case RectCenter.MiddleLeft:
                                return PositionY - ScaleY / 2;
                            case RectCenter.MiddleCenter:
                                return PositionY - ScaleY / 2;
                            case RectCenter.MiddleRight:
                                return PositionY - ScaleY / 2;

                            case RectCenter.LowerLeft:
                                return PositionY;
                            case RectCenter.LowerCenter:
                                return PositionY;
                            case RectCenter.LowerRight:
                                return PositionY;

                            default:
                                return PositionY;
                        }
                    }
                }
                public float AnchorMaxX
                {
                    get
                    {
                        switch (rectCenterPreset)
                        {
                            case RectCenter.UpperLeft:
                                return PositionX + ScaleX;
                            case RectCenter.UpperCenter:
                                return PositionX + ScaleX / 2;
                            case RectCenter.UpperRight:
                                return PositionX;

                            case RectCenter.MiddleLeft:
                                return PositionX + ScaleX;
                            case RectCenter.MiddleCenter:
                                return PositionX + ScaleX / 2;
                            case RectCenter.MiddleRight:
                                return PositionX;

                            case RectCenter.LowerLeft:
                                return PositionX + ScaleX;
                            case RectCenter.LowerCenter:
                                return PositionX + ScaleX / 2;
                            case RectCenter.LowerRight:
                                return PositionX;

                            default:
                                return PositionX;
                        }
                    }
                }
                public float AnchorMaxY
                {
                    get
                    {
                        switch (rectCenterPreset)
                        {
                            case RectCenter.UpperLeft:
                                return PositionY;
                            case RectCenter.UpperCenter:
                                return PositionY;
                            case RectCenter.UpperRight:
                                return PositionY;

                            case RectCenter.MiddleLeft:
                                return PositionY + ScaleY / 2;
                            case RectCenter.MiddleCenter:
                                return PositionY + ScaleY / 2;
                            case RectCenter.MiddleRight:
                                return PositionY + ScaleY / 2;

                            case RectCenter.LowerLeft:
                                return PositionY + ScaleY;
                            case RectCenter.LowerCenter:
                                return PositionY + ScaleY;
                            case RectCenter.LowerRight:
                                return PositionY + ScaleY;

                            default:
                                return PositionY;
                        }
                    }
                }

                public RectCenter rectCenterPreset { get; private set; }
                public float PositionX { get; set; }
                public float PositionY { get; set; }
                public float ScaleX { get; set; }
                public float ScaleY { get; set; }

                public RootRectAnchor(float minX, float minY, float maxX, float maxY, RectCenter rectcenterPreset)
                {
                    ScaleX = maxX - minX;
                    ScaleY = maxY - minY;
                    rectCenterPreset = rectcenterPreset;
                    PositionX = minX;
                    PositionY = minY;

                    switch (rectcenterPreset)
                    {
                        case RectCenter.UpperLeft:
                            PositionY += ScaleY;
                            break;
                        case RectCenter.UpperCenter:
                            PositionX += ScaleX / 2;
                            PositionY += ScaleY;
                            break;
                        case RectCenter.UpperRight:
                            PositionX += ScaleX;
                            PositionY += ScaleY;
                            break;
                        case RectCenter.MiddleLeft:
                            PositionY += ScaleY / 2;
                            break;
                        case RectCenter.MiddleCenter:
                            PositionX += ScaleX / 2;
                            PositionY += ScaleY / 2;
                            break;
                        case RectCenter.MiddleRight:
                            PositionX += ScaleX;
                            PositionY += ScaleY / 2;
                            break;
                        case RectCenter.LowerLeft:
                            //default
                            break;
                        case RectCenter.LowerCenter:
                            PositionX += ScaleX / 2;
                            break;
                        case RectCenter.LowerRight:
                            PositionX += ScaleX;
                            break;
                        default:
                            break;
                    }
                }

                public enum RectCenter
                {
                    UpperLeft,
                    UpperCenter,
                    UpperRight,
                    MiddleLeft,
                    MiddleCenter,
                    MiddleRight,
                    LowerLeft,
                    LowerCenter,
                    LowerRight
                }
            }

            public struct StackRectAnchor : IEquatable<StackRectAnchor>
            {
                public float AnchorMinX { get; set; }
                public float AnchorMinY { get; set; }
                public float AnchorMaxX { get; set; }
                public float AnchorMaxY { get; set; }

                public StackRectAnchor(float minX, float minY, float maxX, float maxY)
                {
                    AnchorMinX = minX;
                    AnchorMinY = minY;
                    AnchorMaxX = maxX;
                    AnchorMaxY = maxY;
                }

                public bool Equals(StackRectAnchor other)
                {
                    return (AnchorMinX == other.AnchorMinX) &&
                        (AnchorMinY == other.AnchorMinY) && 
                        (AnchorMaxX == other.AnchorMaxX) && 
                        (AnchorMaxY == other.AnchorMaxY);
                }

                public static bool operator ==(StackRectAnchor a, StackRectAnchor b) => a.Equals(b);

                public static bool operator !=(StackRectAnchor a, StackRectAnchor b) => !(a.Equals(b));
            }

            public class UIScaler
            {
                public IReadOnlyDictionary<DScalingParameter, ScalingResult> ItemBeltResultCache => itemBeltResultCache;
                public IReadOnlyDictionary<DScalingParameter, ScalingResult> StatusWindowResultCache => statusWindowResultCache;
                public IReadOnlyDictionary<ScalingParameter, ScalingResult> UniversalResultCache => universalResultCache;
                private Dictionary<DScalingParameter, ScalingResult> itemBeltResultCache;
                private Dictionary<DScalingParameter, ScalingResult> statusWindowResultCache;
                private Dictionary<ScalingParameter, ScalingResult> universalResultCache;

                public UIScaler()
                {
                    itemBeltResultCache = new Dictionary<DScalingParameter, ScalingResult>();
                    statusWindowResultCache = new Dictionary<DScalingParameter, ScalingResult>();
                    universalResultCache = new Dictionary<ScalingParameter, ScalingResult>();
                }

                public void ItemBeltScaling(ref UniversalRectAnchor rectAnchor, int ratioX, int ratioY, float uiSize)
                {
                    DScalingParameter dScalingParameter = new DScalingParameter(ratioX, ratioY, uiSize);
                    if (itemBeltResultCache.ContainsKey(dScalingParameter))
                    {
                        ScalingResult scalingResult = itemBeltResultCache[dScalingParameter];
                        rectAnchor.AnchorMaxX = scalingResult.AnchorMaxX;
                        rectAnchor.AnchorMinX = scalingResult.AnchorMinX;
                        rectAnchor.AnchorMaxY = scalingResult.AnchorMaxY;
                        rectAnchor.AnchorMinY = scalingResult.AnchorMinY;
                        return;
                    }

                    float ratioXf = ratioX;
                    float ratioYf = ratioY;

                    rectAnchor.AnchorMaxX *= uiSize;
                    rectAnchor.AnchorMaxY *= uiSize;

                    if (ratioX * _defaultAspectRatioY > ratioY * _defaultAspectRatioX)
                    {
                        float gap = _defaultAspectRatioX / ratioXf;
                        ratioXf *= gap;
                        ratioYf *= gap;
                    }
                    else if (ratioX * _defaultAspectRatioY < ratioY * _defaultAspectRatioX)
                    {
                        float gap = _defaultAspectRatioY / ratioYf;
                        ratioXf *= gap;
                        ratioYf *= gap;
                    }

                    rectAnchor.AnchorMaxX /= _xunit;
                    rectAnchor.AnchorMaxY /= _yunit;
                    rectAnchor.AnchorMaxX *= ratioYf * 0.01f;
                    rectAnchor.AnchorMaxY *= ratioXf * 0.01f;

                    rectAnchor.AlignCenterX();
                    float moveYpos = 0.025f * uiSize;
                    moveYpos /= _yunit;
                    moveYpos *= ratioXf * 0.01f;
                    rectAnchor.MoveY(moveYpos);
                    float AnchorMaxXScaleValue = 0.0163f * uiSize;
                    AnchorMaxXScaleValue /= _xunit;
                    AnchorMaxXScaleValue *= ratioYf * 0.01f;
                    rectAnchor.AnchorMaxX -= AnchorMaxXScaleValue;

                    itemBeltResultCache.Add(dScalingParameter, new ScalingResult(rectAnchor.AnchorMinX, rectAnchor.AnchorMinY, rectAnchor.AnchorMaxX, rectAnchor.AnchorMaxY));
                }

                public void StatusWindowScaling(ref UniversalRectAnchor rectAnchor, int ratioX, int ratioY, float uiSize)
                {
                    DScalingParameter dScalingParameter = new DScalingParameter(ratioX, ratioY, uiSize);
                    if (statusWindowResultCache.ContainsKey(dScalingParameter))
                    {
                        ScalingResult scalingResult = statusWindowResultCache[dScalingParameter];
                        rectAnchor.AnchorMaxX = scalingResult.AnchorMaxX;
                        rectAnchor.AnchorMinX = scalingResult.AnchorMinX;
                        rectAnchor.AnchorMaxY = scalingResult.AnchorMaxY;
                        rectAnchor.AnchorMinY = scalingResult.AnchorMinY;
                        return;
                    }

                    float ratioXf = ratioX;
                    float ratioYf = ratioY;

                    rectAnchor.AnchorMaxX *= uiSize;
                    rectAnchor.AnchorMaxY *= uiSize;

                    if (ratioX * _defaultAspectRatioY > ratioY * _defaultAspectRatioX)
                    {
                        float gap = _defaultAspectRatioX / ratioXf;
                        ratioXf *= gap;
                        ratioYf *= gap;
                    }
                    else if (ratioX * _defaultAspectRatioY < ratioY * _defaultAspectRatioX)
                    {
                        float gap = _defaultAspectRatioY / ratioYf;
                        ratioXf *= gap;
                        ratioYf *= gap;
                    }

                    rectAnchor.AnchorMaxX /= _xunit;
                    rectAnchor.AnchorMaxY /= _yunit;
                    rectAnchor.AnchorMaxX *= ratioYf * 0.01f;
                    rectAnchor.AnchorMaxY *= ratioXf * 0.01f;

                    rectAnchor.MoveXEnd();
                    float moveXpos = -0.0141f * uiSize;
                    moveXpos /= _xunit;
                    moveXpos *= ratioYf * 0.01f;
                    rectAnchor.MoveX(moveXpos);
                    float moveYpos = 0.022f * uiSize;
                    moveYpos /= _yunit;
                    moveYpos *= ratioXf * 0.01f;
                    rectAnchor.MoveY(moveYpos);

                    statusWindowResultCache.Add(dScalingParameter, new ScalingResult(rectAnchor.AnchorMinX, rectAnchor.AnchorMinY, rectAnchor.AnchorMaxX, rectAnchor.AnchorMaxY));
                }

                public ScalingResult UniversalPanelScaling(StackRectAnchor rectAnchor, RootRectAnchor.RectCenter rectCenter, ScalingParent scalingParent, int ratioX, int ratioY, float uiSize)
                {
                    ScalingParameter scalingParameter = new ScalingParameter(rectAnchor, ratioX, ratioY, uiSize);
                    if (universalResultCache.ContainsKey(scalingParameter))
                        return universalResultCache[scalingParameter];

                    RootRectAnchor rootRectAnchor = new RootRectAnchor(rectAnchor.AnchorMinX, rectAnchor.AnchorMinY, rectAnchor.AnchorMaxX, rectAnchor.AnchorMaxY, rectCenter);

                    float ratioXf = ratioX;
                    float ratioYf = ratioY;

                    rootRectAnchor.ScaleX *= uiSize;
                    rootRectAnchor.ScaleY *= uiSize;

                    if (ratioX * _defaultAspectRatioY > ratioY * _defaultAspectRatioX)
                    {
                        float gap = _defaultAspectRatioX / ratioXf;
                        ratioXf *= gap;
                        ratioYf *= gap;
                    }
                    else if (ratioX * _defaultAspectRatioY < ratioY * _defaultAspectRatioX)
                    {
                        float gap = _defaultAspectRatioY / ratioYf;
                        ratioXf *= gap;
                        ratioYf *= gap;
                    }

                    rootRectAnchor.ScaleX /= _xunit;
                    rootRectAnchor.ScaleY /= _yunit;
                    rootRectAnchor.ScaleX *= ratioYf * 0.01f;
                    rootRectAnchor.ScaleY *= ratioXf * 0.01f;

                    DScalingParameter defaultDScalingParameter = new DScalingParameter(_defaultAspectRatioX, _defaultAspectRatioY, _defaultUIScale);
                    DScalingParameter dScalingParameter = new DScalingParameter(ratioX, ratioY, uiSize);
                    float moveXValue;
                    float moveYValue;
                    UniversalRectAnchor defaultResult;
                    UniversalRectAnchor parameterResult;

                    float distanceX;
                    float distanceY;

                    switch (scalingParent)
                    {
                        case ScalingParent.MainUpperLeft:
                            distanceX = rootRectAnchor.PositionX;
                            distanceX = distanceX / _xunit * ratioYf * 0.01f;
                            distanceX = distanceX * uiSize;
                            rootRectAnchor.PositionX = distanceX;
                            distanceY = 1.0f - rootRectAnchor.PositionY;
                            distanceY = distanceY / _yunit * ratioXf * 0.01f;
                            distanceY = distanceY * uiSize;
                            rootRectAnchor.PositionY = 1.0f - distanceY;
                            break;

                        case ScalingParent.MainUpperCenter:
                            distanceX = 0.5f - rootRectAnchor.PositionX;
                            distanceX = distanceX / _xunit * ratioYf * 0.01f;
                            distanceX = distanceX * uiSize;
                            rootRectAnchor.PositionX = 0.5f - distanceX;
                            distanceY = 1.0f - rootRectAnchor.PositionY;
                            distanceY = distanceY / _yunit * ratioXf * 0.01f;
                            distanceY = distanceY * uiSize;
                            rootRectAnchor.PositionY = 1.0f - distanceY;
                            break;

                        case ScalingParent.MainUpperRight:
                            distanceX = 1.0f - rootRectAnchor.PositionX;
                            distanceX = distanceX / _xunit * ratioYf * 0.01f;
                            distanceX = distanceX * uiSize;
                            rootRectAnchor.PositionX = 1.0f - distanceX;
                            distanceY = 1.0f - rootRectAnchor.PositionY;
                            distanceY = distanceY / _yunit * ratioXf * 0.01f;
                            distanceY = distanceY * uiSize;
                            rootRectAnchor.PositionY = 1.0f - distanceY;
                            break;

                        case ScalingParent.MainMiddleLeft:
                            distanceX = rootRectAnchor.PositionX;
                            distanceX = distanceX / _xunit * ratioYf * 0.01f;
                            distanceX = distanceX * uiSize;
                            rootRectAnchor.PositionX = distanceX;
                            distanceY = 0.5f - rootRectAnchor.PositionY;
                            distanceY = distanceY / _yunit * ratioXf * 0.01f;
                            distanceY = distanceY * uiSize;
                            rootRectAnchor.PositionY = 0.5f - distanceY;
                            break;

                        case ScalingParent.MainMiddleCenter:
                            distanceX = 0.5f - rootRectAnchor.PositionX;
                            distanceX = distanceX / _xunit * ratioYf * 0.01f;
                            distanceX = distanceX * uiSize;
                            rootRectAnchor.PositionX = 0.5f - distanceX;
                            distanceY = 0.5f - rootRectAnchor.PositionY;
                            distanceY = distanceY / _yunit * ratioXf * 0.01f;
                            distanceY = distanceY * uiSize;
                            rootRectAnchor.PositionY = 0.5f - distanceY;
                            break;

                        case ScalingParent.MainMiddleRight:
                            distanceX = 1.0f - rootRectAnchor.PositionX;
                            distanceX = distanceX / _xunit * ratioYf * 0.01f;
                            distanceX = distanceX * uiSize;
                            rootRectAnchor.PositionX = 1.0f - distanceX;
                            distanceY = 0.5f - rootRectAnchor.PositionY;
                            distanceY = distanceY / _yunit * ratioXf * 0.01f;
                            distanceY = distanceY * uiSize;
                            rootRectAnchor.PositionY = 0.5f - distanceY;
                            break;

                        case ScalingParent.MainLowerLeft:
                            distanceX = rootRectAnchor.PositionX;
                            distanceX = distanceX / _xunit * ratioYf * 0.01f;
                            distanceX = distanceX * uiSize;
                            rootRectAnchor.PositionX = distanceX;
                            distanceY = rootRectAnchor.PositionY;
                            distanceY = distanceY / _yunit * ratioXf * 0.01f;
                            distanceY = distanceY * uiSize;
                            rootRectAnchor.PositionY = distanceY;
                            break;

                        case ScalingParent.MainLowerCenter:
                            distanceX = 0.5f - rootRectAnchor.PositionX;
                            distanceX = distanceX / _xunit * ratioYf * 0.01f;
                            distanceX = distanceX * uiSize;
                            rootRectAnchor.PositionX = 0.5f - distanceX;
                            distanceY = rootRectAnchor.PositionY;
                            distanceY = distanceY / _yunit * ratioXf * 0.01f;
                            distanceY = distanceY * uiSize;
                            rootRectAnchor.PositionY = distanceY;
                            break;

                        case ScalingParent.MainLowerRight:
                            distanceX = 1.0f - rootRectAnchor.PositionX;
                            distanceX = distanceX / _xunit * ratioYf * 0.01f;
                            distanceX = distanceX * uiSize;
                            rootRectAnchor.PositionX = 1.0f - distanceX;
                            distanceY = rootRectAnchor.PositionY;
                            distanceY = distanceY / _yunit * ratioXf * 0.01f;
                            distanceY = distanceY * uiSize;
                            rootRectAnchor.PositionY = distanceY;
                            break;

                        case ScalingParent.ItemBeltUpperLeft:
                            if (!ItemBeltResultCache.ContainsKey(defaultDScalingParameter))
                                ItemBeltScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!ItemBeltResultCache.ContainsKey(dScalingParameter))
                                ItemBeltScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(ItemBeltResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(ItemBeltResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorMinX - defaultResult.AnchorMinX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMinX - rootRectAnchor.PositionX) - (parameterResult.AnchorMinX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMinX - rootRectAnchor.PositionX) - (parameterResult.AnchorMinX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorMaxY - defaultResult.AnchorMaxY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) - (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) - (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.ItemBeltUpperCenter:
                            if (!ItemBeltResultCache.ContainsKey(defaultDScalingParameter))
                                ItemBeltScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!ItemBeltResultCache.ContainsKey(dScalingParameter))
                                ItemBeltScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(ItemBeltResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(ItemBeltResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorCenterX - defaultResult.AnchorCenterX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) - (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) - (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorMaxY - defaultResult.AnchorMaxY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) - (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) - (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.ItemBeltUpperRight:
                            if (!ItemBeltResultCache.ContainsKey(defaultDScalingParameter))
                                ItemBeltScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!ItemBeltResultCache.ContainsKey(dScalingParameter))
                                ItemBeltScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(ItemBeltResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(ItemBeltResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorMaxX - defaultResult.AnchorMaxX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) - (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) - (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorMaxY - defaultResult.AnchorMaxY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) - (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) - (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.ItemBeltMiddleLeft:
                            if (!ItemBeltResultCache.ContainsKey(defaultDScalingParameter))
                                ItemBeltScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!ItemBeltResultCache.ContainsKey(dScalingParameter))
                                ItemBeltScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(ItemBeltResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(ItemBeltResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorMinX - defaultResult.AnchorMinX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMinX - rootRectAnchor.PositionX) - (parameterResult.AnchorMinX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMinX - rootRectAnchor.PositionX) - (parameterResult.AnchorMinX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorCenterY - defaultResult.AnchorCenterY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) - (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) - (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.ItemBeltMiddleCenter:
                            if (!ItemBeltResultCache.ContainsKey(defaultDScalingParameter))
                                ItemBeltScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!ItemBeltResultCache.ContainsKey(dScalingParameter))
                                ItemBeltScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(ItemBeltResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(ItemBeltResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorCenterX - defaultResult.AnchorCenterX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) - (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) - (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorCenterY - defaultResult.AnchorCenterY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) - (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) - (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.ItemBeltMiddleRight:
                            if (!ItemBeltResultCache.ContainsKey(defaultDScalingParameter))
                                ItemBeltScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!ItemBeltResultCache.ContainsKey(dScalingParameter))
                                ItemBeltScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(ItemBeltResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(ItemBeltResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorMaxX - defaultResult.AnchorMaxX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) - (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) - (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorCenterY - defaultResult.AnchorCenterY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) - (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) - (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.ItemBeltLowerLeft:
                            if (!ItemBeltResultCache.ContainsKey(defaultDScalingParameter))
                                ItemBeltScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!ItemBeltResultCache.ContainsKey(dScalingParameter))
                                ItemBeltScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(ItemBeltResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(ItemBeltResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorMinX - defaultResult.AnchorMinX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMinX - rootRectAnchor.PositionX) - (parameterResult.AnchorMinX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMinX - rootRectAnchor.PositionX) - (parameterResult.AnchorMinX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorMinY - defaultResult.AnchorMinY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMinY - rootRectAnchor.PositionY) - (parameterResult.AnchorMinY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMinY - rootRectAnchor.PositionY) - (parameterResult.AnchorMinY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.ItemBeltLowerCenter:
                            if (!ItemBeltResultCache.ContainsKey(defaultDScalingParameter))
                                ItemBeltScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!ItemBeltResultCache.ContainsKey(dScalingParameter))
                                ItemBeltScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(ItemBeltResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(ItemBeltResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorCenterX - defaultResult.AnchorCenterX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) - (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) - (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorMinY - defaultResult.AnchorMinY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMinY - rootRectAnchor.PositionY) - (parameterResult.AnchorMinY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMinY - rootRectAnchor.PositionY) - (parameterResult.AnchorMinY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.ItemBeltLowerRight:
                            if (!ItemBeltResultCache.ContainsKey(defaultDScalingParameter))
                                ItemBeltScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!ItemBeltResultCache.ContainsKey(dScalingParameter))
                                ItemBeltScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(ItemBeltResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(ItemBeltResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorMaxX - defaultResult.AnchorMaxX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) - (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) - (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorMinY - defaultResult.AnchorMinY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMinY - rootRectAnchor.PositionY) - (parameterResult.AnchorMinY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMinY - rootRectAnchor.PositionY) - (parameterResult.AnchorMinY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.StatusWindowUpperLeft:
                            if (!StatusWindowResultCache.ContainsKey(defaultDScalingParameter))
                                StatusWindowScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!StatusWindowResultCache.ContainsKey(dScalingParameter))
                                StatusWindowScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(StatusWindowResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(StatusWindowResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorMinX - defaultResult.AnchorMinX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMinX - rootRectAnchor.PositionX) - (parameterResult.AnchorMinX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMinX - rootRectAnchor.PositionX) - (parameterResult.AnchorMinX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorMaxY - defaultResult.AnchorMaxY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) - (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) - (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.StatusWindowUpperCenter:
                            if (!StatusWindowResultCache.ContainsKey(defaultDScalingParameter))
                                StatusWindowScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!StatusWindowResultCache.ContainsKey(dScalingParameter))
                                StatusWindowScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(StatusWindowResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(StatusWindowResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorCenterX - defaultResult.AnchorCenterX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) - (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) - (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorMaxY - defaultResult.AnchorMaxY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) - (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) - (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.StatusWindowUpperRight:
                            if (!StatusWindowResultCache.ContainsKey(defaultDScalingParameter))
                                StatusWindowScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!StatusWindowResultCache.ContainsKey(dScalingParameter))
                                StatusWindowScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(StatusWindowResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(StatusWindowResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorMaxX - defaultResult.AnchorMaxX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) - (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) - (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorMaxY - defaultResult.AnchorMaxY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) - (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) - (parameterResult.AnchorMaxY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.StatusWindowMiddleLeft:
                            if (!StatusWindowResultCache.ContainsKey(defaultDScalingParameter))
                                StatusWindowScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!StatusWindowResultCache.ContainsKey(dScalingParameter))
                                StatusWindowScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(StatusWindowResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(StatusWindowResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorMinX - defaultResult.AnchorMinX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMinX - rootRectAnchor.PositionX) - (parameterResult.AnchorMinX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMinX - rootRectAnchor.PositionX) - (parameterResult.AnchorMinX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorCenterY - defaultResult.AnchorCenterY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) - (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) - (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.StatusWindowMiddleCenter:
                            if (!StatusWindowResultCache.ContainsKey(defaultDScalingParameter))
                                StatusWindowScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!StatusWindowResultCache.ContainsKey(dScalingParameter))
                                StatusWindowScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(StatusWindowResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(StatusWindowResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorCenterX - defaultResult.AnchorCenterX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) - (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) - (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorCenterY - defaultResult.AnchorCenterY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) - (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) - (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.StatusWindowMiddleRight:
                            if (!StatusWindowResultCache.ContainsKey(defaultDScalingParameter))
                                StatusWindowScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!StatusWindowResultCache.ContainsKey(dScalingParameter))
                                StatusWindowScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(StatusWindowResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(StatusWindowResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorMaxX - defaultResult.AnchorMaxX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) - (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) - (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorCenterY - defaultResult.AnchorCenterY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) - (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) - (parameterResult.AnchorCenterY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.StatusWindowLowerLeft:
                            if (!StatusWindowResultCache.ContainsKey(defaultDScalingParameter))
                                StatusWindowScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!StatusWindowResultCache.ContainsKey(dScalingParameter))
                                StatusWindowScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(StatusWindowResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(StatusWindowResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorMinX - defaultResult.AnchorMinX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMinX - rootRectAnchor.PositionX) - (parameterResult.AnchorMinX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMinX - rootRectAnchor.PositionX) - (parameterResult.AnchorMinX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorMinY - defaultResult.AnchorMinY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMinY - rootRectAnchor.PositionY) - (parameterResult.AnchorMinY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMinY - rootRectAnchor.PositionY) - (parameterResult.AnchorMinY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.StatusWindowLowerCenter:
                            if (!StatusWindowResultCache.ContainsKey(defaultDScalingParameter))
                                StatusWindowScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!StatusWindowResultCache.ContainsKey(dScalingParameter))
                                StatusWindowScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(StatusWindowResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(StatusWindowResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorCenterX - defaultResult.AnchorCenterX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) - (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) - (parameterResult.AnchorCenterX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorMinY - defaultResult.AnchorMinY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMinY - rootRectAnchor.PositionY) - (parameterResult.AnchorMinY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMinY - rootRectAnchor.PositionY) - (parameterResult.AnchorMinY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        case ScalingParent.StatusWindowLowerRight:
                            if (!StatusWindowResultCache.ContainsKey(defaultDScalingParameter))
                                StatusWindowScaleCaching(_defaultAspectRatioX, _defaultAspectRatioX, _defaultUIScale);
                            if (!StatusWindowResultCache.ContainsKey(dScalingParameter))
                                StatusWindowScaleCaching(ratioX, ratioY, uiSize);
                            defaultResult = new UniversalRectAnchor(StatusWindowResultCache[defaultDScalingParameter]);
                            parameterResult = new UniversalRectAnchor(StatusWindowResultCache[dScalingParameter]);
                            moveXValue = parameterResult.AnchorMaxX - defaultResult.AnchorMaxX;
                            rootRectAnchor.PositionX += moveXValue;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) - (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) / _xunit * ratioYf * 0.01f;
                            rootRectAnchor.PositionX += (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) - (parameterResult.AnchorMaxX - rootRectAnchor.PositionX) * uiSize;
                            moveYValue = parameterResult.AnchorMinY - defaultResult.AnchorMinY;
                            rootRectAnchor.PositionY += moveYValue;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMinY - rootRectAnchor.PositionY) - (parameterResult.AnchorMinY - rootRectAnchor.PositionY) / _yunit * ratioXf * 0.01f;
                            rootRectAnchor.PositionY += (parameterResult.AnchorMinY - rootRectAnchor.PositionY) - (parameterResult.AnchorMinY - rootRectAnchor.PositionY) * uiSize;
                            break;

                        default:
                            break;
                    }

                    ScalingResult scalingResult = new ScalingResult(rootRectAnchor.AnchorMinX, rootRectAnchor.AnchorMinY, rootRectAnchor.AnchorMaxX, rootRectAnchor.AnchorMaxY);
                    universalResultCache.Add(scalingParameter, scalingResult);
                    return scalingResult;
                }

                public void ItemBeltScaleCaching(int ratioX, int ratioY, float uiSize)
                {
                    UniversalRectAnchor scaledBeltAnchor = _itemBeltAnchor.Clone();
                    ItemBeltScaling(ref scaledBeltAnchor, ratioX, ratioY, uiSize);
                }
                
                public void StatusWindowScaleCaching(int ratioX, int ratioY, float uiSize)
                {
                    UniversalRectAnchor ScaledWindowAnchor = _statusWindowAnchor.Clone();
                    ItemBeltScaling(ref ScaledWindowAnchor, ratioX, ratioY, uiSize);
                }

                public struct DScalingParameter : IEquatable<DScalingParameter>
                {
                    public int RatioX { get; set; }
                    public int RatioY { get; set; }
                    public float UISize { get; set; }

                    public DScalingParameter(int ratioX, int ratioY, float uiSize)
                    {
                        RatioX = ratioX;
                        RatioY = ratioY;
                        UISize = uiSize;
                    }

                    public bool Equals(DScalingParameter other) => (RatioX == other.RatioX) && (RatioY == other.RatioY) && (UISize == other.UISize);

                    public static bool operator ==(DScalingParameter a, DScalingParameter b) => a.Equals(b);

                    public static bool operator !=(DScalingParameter a, DScalingParameter b) => !(a.Equals(b));
                }

                public struct ScalingParameter : IEquatable<ScalingParameter>
                {
                    public StackRectAnchor StackRectAnchor { get; set; }
                    public int RatioX { get; set; }
                    public int RatioY { get; set; }
                    public float UISize { get; set; }

                    public ScalingParameter(StackRectAnchor stackRectAnchor, int ratioX, int ratioY, float uiSize)
                    {
                        StackRectAnchor = stackRectAnchor;
                        RatioX = ratioX;
                        RatioY = ratioY;
                        UISize = uiSize;
                    }

                    public bool Equals(ScalingParameter other)
                    {
                        return (StackRectAnchor == other.StackRectAnchor) &&
                            (RatioX == other.RatioX) && 
                            (RatioY == other.RatioY) && 
                            (UISize == other.UISize);
                    }

                    public static bool operator ==(ScalingParameter a, ScalingParameter b) => a.Equals(b);

                    public static bool operator !=(ScalingParameter a, ScalingParameter b) => !(a.Equals(b));
                }

                public struct ScalingResult
                {
                    public float[] Result { get; private set; }
                    public float AnchorMinX
                    {
                        get { return Result[0]; }
                        set { Result[0] = value; }
                    }
                    public float AnchorMinY
                    {
                        get { return Result[1]; }
                        set { Result[1] = value; }
                    }
                    public float AnchorMaxX
                    {
                        get { return Result[2]; }
                        set { Result[2] = value; }
                    }
                    public float AnchorMaxY
                    {
                        get { return Result[3]; }
                        set { Result[3] = value; }
                    }
                    
                    public ScalingResult(float minX, float minY, float maxX, float maxY)
                    {
                        Result = new float[4];

                        AnchorMinX = minX;
                        AnchorMinY = minY;
                        AnchorMaxX = maxX;
                        AnchorMaxY = maxY;
                    }
                }

                public enum ScalingParent
                {
                    MainUpperLeft,
                    MainUpperCenter,
                    MainUpperRight,
                    MainMiddleLeft,
                    MainMiddleCenter,
                    MainMiddleRight,
                    MainLowerLeft,
                    MainLowerCenter,
                    MainLowerRight,

                    ItemBeltUpperLeft,
                    ItemBeltUpperCenter,
                    ItemBeltUpperRight,
                    ItemBeltMiddleLeft,
                    ItemBeltMiddleCenter,
                    ItemBeltMiddleRight,
                    ItemBeltLowerLeft,
                    ItemBeltLowerCenter,
                    ItemBeltLowerRight,

                    StatusWindowUpperLeft,
                    StatusWindowUpperCenter,
                    StatusWindowUpperRight,
                    StatusWindowMiddleLeft,
                    StatusWindowMiddleCenter,
                    StatusWindowMiddleRight,
                    StatusWindowLowerLeft,
                    StatusWindowLowerCenter,
                    StatusWindowLowerRight
                }
            }
        }

        #endregion
    }
}