//Requires: ImageLibrary

using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Game.Rust.Cui;

namespace Oxide.Plugins
{
    [Info("SimpleLogo", "Sami37", "1.2.8")]
    [Description("Place your own logo to your player screen.")]
    public class SimpleLogo : RustPlugin
    {
        #region config
        [PluginReference]
        ImageLibrary ImageLibrary;

        private string Perm = "simplelogo.display", NoDisplay = "simplelogo.nodisplay";
        List<object> _urlList = new List<object>();
        private int _currentlySelected, _intervals;
        private Dictionary<ulong, bool> playerHide = new Dictionary<ulong, bool>();

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            LoadConfig();
        }

        string ListToString<T>(List<T> list, int first = 0, string seperator = ", ") => string.Join(seperator, (from val in list select val.ToString()).Skip(first).ToArray());
        void SetConfig(params object[] args) { List<string> stringArgs = (from arg in args select arg.ToString()).ToList(); stringArgs.RemoveAt(args.Length - 1); if (Config.Get(stringArgs.ToArray()) == null) Config.Set(args); }
        T GetConfig<T>(T defaultVal, params object[] args) { List<string> stringArgs = (from arg in args select arg.ToString()).ToList(); if (Config.Get(stringArgs.ToArray()) == null) { PrintError($"The plugin failed to read something from the config: {ListToString(stringArgs, 0, "/")}{Environment.NewLine}Please reload the plugin and see if this message is still showing. If so, please post this into the support thread of this plugin."); return defaultVal; } return (T)Convert.ChangeType(Config.Get(stringArgs.ToArray()), typeof(T)); }

        private string GetImage(string shortname) => ImageLibrary.GetImage(shortname);

        void LoadConfig()
        {
            List<object> listUrl = new List<object> { "http://i.imgur.com/KVmbhyB.png" };
            SetConfig("UI", "GUIAnchorMin", "0.01 0.02");
            SetConfig("UI", "GUIAnchorMax", "0.15 0.1");
            SetConfig("UI", "BackgroundMainColor", "0 0 0 0");
            SetConfig("UI", "BackgroundMainURL", listUrl);
            SetConfig("UI", "IntervalBetweenImage", 30);

            SaveConfig();

            _intervals = GetConfig(30, "UI", "IntervalBetweenImage");
            _urlList = (List<object>)Config["UI", "BackgroundMainURL"];

            Dictionary<string, string> newLoadOrder = new Dictionary<string, string>();
            int i = 0;
            foreach (var url in _urlList)
            {
                newLoadOrder.Add("SimpleLogo"+i, url.ToString());
                i++;
            }

            LoadOrder(Title, newLoadOrder, 0, true);
        }

        private void LoadOrder(string title, Dictionary<string, string> importImageList, ulong skin, bool force) => ImageLibrary?.Call("ImportImageList", title, importImageList, skin, force);

        #endregion

        #region data_init

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                GUIDestroy(player);
            }
            if(playerHide != null)
                Interface.Oxide.DataFileSystem.WriteObject(Name, playerHide);
        }
        #endregion

        private CuiElement CreateImage(string panelName)
        {
            var element = new CuiElement();
            var url = GetImage($"SimpleLogo{_currentlySelected}");
            var image = new CuiRawImageComponent
            {
                Png = url
            };

            var rectTransform = new CuiRectTransformComponent
            {
                AnchorMin = "0 0",
                AnchorMax = "1 1"
            };
            element.Components.Add(image);
            element.Components.Add(rectTransform);
            element.Name = CuiHelper.GetGuid();
            element.Parent = panelName;
            return element;
        }

        void GUIDestroy(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "containerSimpleUI");
        }

        void CreateUi(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, Perm) && !permission.UserHasPermission(player.UserIDString, NoDisplay))
            {
                var panel = new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image =
                            {
                                Color = Config["UI", "BackgroundMainColor"].ToString()
                            },
                            RectTransform =
                            {
                                AnchorMin = Config["UI", "GUIAnchorMin"].ToString(),
                                AnchorMax = Config["UI", "GUIAnchorMax"].ToString()
                            },
                            CursorEnabled = false
                        },
                        "Hud", "containerSimpleUI"
                    }
                };
                var backgroundImageWin = CreateImage("containerSimpleUI");
                panel.Add(backgroundImageWin);
                CuiHelper.AddUi(player, panel);
            }
        }

        void RefreshUi()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                GUIDestroy(player);
                if (playerHide == null || !playerHide.ContainsKey(player.userID))
                    CreateUi(player);
                else if(playerHide.ContainsKey(player.userID) && !playerHide[player.userID])
                    CreateUi(player);
            }
            timer.In(_intervals, () =>
            {
                if (_currentlySelected >= _urlList.Count)
                    _currentlySelected = 0;
                RefreshUi();
                _currentlySelected += 1;
            });
        }

        void OnServerInitialized()
        {
            LoadConfig();
            NextTick(RefreshUi);
            permission.RegisterPermission(Perm, this);
            permission.RegisterPermission(NoDisplay, this);

            playerHide = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, bool>>(Name);
        }

        [ChatCommand("SL")]
        void chatCmd(BasePlayer player, string command, string[] args)
        {
            if(playerHide == null)
                playerHide = new Dictionary<ulong, bool>();
            if (!playerHide.ContainsKey(player.userID))
            {
                playerHide.Add(player.userID, true);
            }
            else
            {
                playerHide[player.userID] = !playerHide[player.userID];
            }
        }
    }
}