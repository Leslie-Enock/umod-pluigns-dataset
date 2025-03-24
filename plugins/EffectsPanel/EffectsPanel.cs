using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Effects Panel", "Mevent", "1.1.0")]
    [Description("Displaying effects in the interface with the ability to play it and output it to the console")]
    public class EffectsPanel : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin Notify;

        private const string Layer = "UI.Effects";

        private readonly List<string> _effects = new List<string>();

        private const string PermUse = "effects.use";

        #endregion

        #region Config

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] Commands = { "effects", "efui" };

            [JsonProperty(PropertyName = "Work with Notify?")]
            public bool UseNotify = true;

            [JsonProperty(PropertyName = "Interface Settings")]
            public InterfaceConf UI = new InterfaceConf
            {
                AmountOnPage = 8,
                ItemHeight = 50f,
                ItemsMargin = 5f
            };
        }

        private class InterfaceConf
        {
            [JsonProperty(PropertyName = "Amount on page")]
            public int AmountOnPage;

            [JsonProperty(PropertyName = "Item Height")]
            public float ItemHeight;

            [JsonProperty(PropertyName = "Items Margin")]
            public float ItemsMargin;
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

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            permission.RegisterPermission(PermUse, this);
            
            foreach (var asset in GameManifest.Current.pooledStrings.Where(asset =>
                (asset.str.StartsWith("assets/content/") || asset.str.StartsWith("assets/bundled/") ||
                 asset.str.StartsWith("assets/prefabs/")) && asset.str.EndsWith(".prefab") &&
                asset.str.Contains("/fx/")))
                _effects.Add(asset.str);

            AddCovalenceCommand(_config.Commands, nameof(CmdOpenEffects));
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
            }
        }

        #endregion

        #region Commands

        private void CmdOpenEffects(IPlayer cov, string command, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            if (!cov.HasPermission(PermUse))
            {
                SendNotify(player, NoPermission, 1);
                return;
            }

            MainUi(player, first: true);
        }

        [ConsoleCommand("UI_Effects")]
        private void CmdConsoleEffects(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs()) return;

            switch (arg.Args[0])
            {
                case "page":
                {
                    int page;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;

                    var search = string.Empty;
                    if (arg.HasArgs(3))
                        search = string.Join(" ", arg.Args.Skip(2));

                    MainUi(player, page, search);
                    break;
                }

                case "play":
                {
                    var effect = string.Join(" ", arg.Args.Skip(1));
                    if (string.IsNullOrEmpty(effect)) return;

                    SendEffect(player, effect);
                    break;
                }

                case "debug":
                {
                    var effect = string.Join(" ", arg.Args.Skip(1));
                    if (string.IsNullOrEmpty(effect)) return;

                    PrintWarning(Msg(player, ShowEffect, effect));
                    break;
                }
            }
        }

        #endregion

        #region Interface

        private void MainUi(BasePlayer player, int page = 0, string search = "", bool first = false)
        {
            var container = new CuiElementContainer();

            #region Background

            if (first)
            {
                CuiHelper.DestroyUi(player, Layer);

                container.Add(new CuiPanel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Image =
                    {
                        Color = "0 0 0 0.9",
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    },
                    CursorEnabled = true
                }, "Overlay", Layer);

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text = { Text = "" },
                    Button =
                    {
                        Color = "0 0 0 0",
                        Close = Layer
                    }
                }, Layer);
            }

            #endregion

            #region Main

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                    OffsetMin = "-310 -250",
                    OffsetMax = "310 250"
                },
                Image =
                {
                    Color = HexToCuiColor("#0E0E10")
                }
            }, Layer, Layer + ".Main");

            #region Header

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = "0 -50",
                    OffsetMax = "0 0"
                },
                Image = { Color = HexToCuiColor("#161617") }
            }, Layer + ".Main", Layer + ".Header");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "30 0",
                    OffsetMax = "0 0"
                },
                Text =
                {
                    Text = Msg(player, TitleMenu),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 14,
                    Color = HexToCuiColor("#FFFFFF")
                }
            }, Layer + ".Header");

            float xSwitch = -25;
            float width = 25;
            float margin = 5;

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = $"{xSwitch - width} -37.5",
                    OffsetMax = $"{xSwitch} -12.5"
                },
                Text =
                {
                    Text = Msg(player, CloseButton),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-bold.ttf",
                    FontSize = 10,
                    Color = HexToCuiColor("#FFFFFF")
                },
                Button =
                {
                    Close = Layer,
                    Color = HexToCuiColor("#4B68FF")
                }
            }, Layer + ".Header");

            xSwitch = xSwitch - margin - width;
            width = 25;

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = $"{xSwitch - width} -37.5",
                    OffsetMax = $"{xSwitch} -12.5"
                },
                Text =
                {
                    Text = Msg(player, BtnNext),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = HexToCuiColor("#FFFFFF")
                },
                Button =
                {
                    Color = HexToCuiColor("#4B68FF"),
                    Command = _effects.Count > (page + 1) * _config.UI.AmountOnPage
                        ? $"UI_Effects page {page + 1} {search}"
                        : ""
                }
            }, Layer + ".Header");

            xSwitch = xSwitch - margin - width;

            container.Add(new CuiButton
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = $"{xSwitch - width} -37.5",
                    OffsetMax = $"{xSwitch} -12.5"
                },
                Text =
                {
                    Text = Msg(player, BtnBack),
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = HexToCuiColor("#FFFFFF")
                },
                Button =
                {
                    Color = HexToCuiColor("#4B68FF", 33),
                    Command = page != 0 ? $"UI_Effects page {page - 1} {search}" : ""
                }
            }, Layer + ".Header");

            #region Search

            xSwitch = xSwitch - margin - width;
            width = 140;

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = $"{xSwitch - width} -37.5",
                    OffsetMax = $"{xSwitch} -12.5"
                },
                Image =
                {
                    Color = HexToCuiColor("#000000")
                }
            }, Layer + ".Header", Layer + ".Header.Search");

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 1",
                    OffsetMin = "10 0", OffsetMax = "-10 0"
                },
                Text =
                {
                    Text = string.IsNullOrEmpty(search) ? Msg(player, SearchTitle) : $"{search}",
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = "1 1 1 0.65"
                }
            }, Layer + ".Header.Search");

            container.Add(new CuiElement
            {
                Parent = Layer + ".Header.Search",
                Components =
                {
                    new CuiInputFieldComponent
                    {
                        FontSize = 12,
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        Command = $"UI_Effects page {page} ",
                        Color = "1 1 1 0.95",
                        CharsLimit = 32
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1"
                    }
                }
            });

            #endregion

            #endregion

            #region List

            var ySwitch = -60f;

            foreach (var effect in _effects.Skip(page * _config.UI.AmountOnPage).Take(_config.UI.AmountOnPage))
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                        OffsetMin = $"-300 {ySwitch - _config.UI.ItemHeight}",
                        OffsetMax = $"300 {ySwitch}"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#161617")
                    }
                }, Layer + ".Main", Layer + $".Effect.{ySwitch}");

                #region Name

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = "10 0", OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = $"{effect}",
                        Align = TextAnchor.MiddleLeft,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Color = "1 1 1 1"
                    }
                }, Layer + $".Effect.{ySwitch}");

                #endregion

                #region Buttons

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                        OffsetMin = "-110 -15", OffsetMax = "-10 15"
                    },
                    Text =
                    {
                        Text = Msg(player, DebugTitle),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = HexToCuiColor("#B43D3D"),
                        Command = $"UI_Effects debug {effect}"
                    }
                }, Layer + $".Effect.{ySwitch}");

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                        OffsetMin = "-220 -15", OffsetMax = "-120 15"
                    },
                    Text =
                    {
                        Text = Msg(player, PlayTitle),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 14,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = HexToCuiColor("#4B68FF"),
                        Command = $"UI_Effects play {effect}"
                    }
                }, Layer + $".Effect.{ySwitch}");

                #endregion

                ySwitch = ySwitch - _config.UI.ItemHeight - _config.UI.ItemsMargin;
            }

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Main");
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Utils

        private static string HexToCuiColor(string hex, float alpha = 100)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";

            var str = hex.Trim('#');
            if (str.Length != 6) throw new Exception(hex);
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {alpha / 100f}";
        }

        private static void SendEffect(BasePlayer player, string effect)
        {
            EffectNetwork.Send(new Effect(effect, player, 0, new Vector3(), new Vector3()), player.Connection);
        }

        #endregion

        #region Lang

        private const string
            ShowEffect = "ShowEffect",
            NoPermission = "NoPermission",
            PlayTitle = "PlayTitle",
            DebugTitle = "DebugTitle",
            SearchTitle = "SearchTitle",
            BtnBack = "BtnBack",
            BtnNext = "BtnNext",
            CloseButton = "CloseButton",
            TitleMenu = "TitleMenu";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [NoPermission] = "You don't have the required permission",
                [TitleMenu] = "Effects",
                [CloseButton] = "✕",
                [BtnBack] = "◀",
                [BtnNext] = "▶",
                [SearchTitle] = "Search...",
                [DebugTitle] = "DEBUG",
                [PlayTitle] = "PLAY",
                [ShowEffect] = "Effect: {0}"
            }, this);
        }

        private string Msg(BasePlayer player, string key, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
        }

        private void Reply(BasePlayer player, string key, params object[] obj)
        {
            SendReply(player, Msg(player, key, obj));
        }

        private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
        {
            if (Notify.IsLoaded && _config.UseNotify)
                Notify?.Call("SendNotify", player, type, Msg(player, key, obj));
            else
                Reply(player, key, obj);
        }

        #endregion
    }
}