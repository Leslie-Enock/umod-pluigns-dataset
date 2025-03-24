using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Custom UI", "birthdates", "1.1.3")]
    [Description("Create your custom user interface without code!")]
    public class CustomUI : RustPlugin
    {
        #region Variables

        private readonly Dictionary<string, string> StoredMedia = new Dictionary<string, string>();
        [PluginReference] private Plugin ImageLibrary;
        private const string PermissionOpenUI = "customui.openui";
        private const string PermissionCloseUI = "customui.closeui";

        #endregion

        #region Hooks

        private void Init()
        {
            LoadConfig();
            RegisterUICommands();

            permission.RegisterPermission(PermissionOpenUI, this);
            permission.RegisterPermission(PermissionCloseUI, this);
        }

        private void OnServerInitialized()
        {
            if (!LoadImages())
            {
                CacheUIs();
                return;
            }

            timer.In(Config.TimeToLoadUI, () =>
            {
                PrintWarning("Caching UIs...");
                CacheUIs();
            });
        }

        #endregion

        #region Commands

        [ConsoleCommand("CustomUI.OpenUI")]
        private void OpenUICommand(ConsoleSystem.Arg Arg)
        {
            var Player = Arg.Player();
            if (Player == null || !Player.IPlayer.HasPermission(PermissionOpenUI) ||
                (Arg.Args?.Length ?? 0) == 0) return;
            OpenUI(Player, Arg.Args[0]);
        }

        [ConsoleCommand("CustomUI.CloseUI")]
        private void CloseUICommand(ConsoleSystem.Arg Arg)
        {
            var Player = Arg.Player();
            if (Player|| !Player.IPlayer.HasPermission(PermissionCloseUI) ||
                Arg.Args?.Length == 0) return;
            CuiHelper.DestroyUi(Player, Arg.Args[0]);
        }

        #endregion

        #region Helpers

        #region Image Library

        /// <summary>
        ///     Load all of the UI images via ImageLibrary
        /// </summary>
        /// <returns>If ImageLibrary is installed</returns>
        private bool LoadImages()
        {
            if (ImageLibrary == null)
            {
                PrintWarning("Image Library isn't installed, if you're using images you might get an error!");
                return false;
            }

            PrintWarning("Loading Images...");
            foreach (var UI in Config.UIs.Values)
            {
                if (!IsHexColor(UI.Media)) AddImage(UI.Media);
                foreach (var Element in AllElements(UI).Where(Ui => !IsHexColor(Ui.Media))) AddImage(Element.Media);
            }

            return true;
        }

        /// <summary>
        ///     Store an image's PNG data
        /// </summary>
        /// <param name="Media">The image and key</param>
        private void AddImage(string Media)
        {
            if (ImageLibrary == null)
            {
                PrintError("Attemtping to load an image and Image Library is not installed!");
                return;
            }

            if (string.IsNullOrEmpty(Media)) return;
            ImageLibrary.Call("AddImage", Media, Media, 0);
        }

        /// <summary>
        ///     Get PNG data from Media using ImageLibrary
        /// </summary>
        /// <param name="Media">The URL (the key)</param>
        /// <returns></returns>
        private string GetImage(string Media)
        {
            if (ImageLibrary == null)
            {
                PrintError("Attemtping to get an image and Image Library is not installed!");
                return string.Empty;
            }

            string PNG;
            if (StoredMedia.TryGetValue(Media, out PNG)) return PNG;
            PNG = ImageLibrary.Call<string>("GetImage", Media);
            StoredMedia.Add(Media, PNG);
            return PNG;
        }

        #endregion

        #region UI

        /// <summary>
        ///     Cache all custom UIs instead of creating a new container each call
        /// </summary>
        private void CacheUIs()
        {
            foreach (var UIPair in Config.UIs.Where(UIPair => UIPair.Value.CachedElements == null))
                CacheUI(UIPair.Key, UIPair.Value);
            PrintWarning("UIs have been cached.");
        }

        /// <summary>
        ///     Cache an individual UI
        /// </summary>
        /// <param name="Name">UI Name</param>
        /// <param name="UI">UI Object</param>
        private void CacheUI(string Name, UI UI)
        {
            var H = GetImageFromMedia(UI.Media);
            UI.CachedElements = new CuiElementContainer
            {
                new CuiElement
                {
                    Name = Name,
                    Parent = UI.Parent,
                    Components =
                    {
                        H,
                        new CuiRectTransformComponent
                        {
                            AnchorMin = UI.Position.X + " " + UI.Position.Y,
                            AnchorMax = UI.Position.X + UI.Position.Width + " " + (UI.Position.Y +
                                                                                   UI.Position.Height)
                        }
                    }
                }
            };

            if (UI.Cursor)
                UI.CachedElements.Add(new CuiElement
                {
                    Parent = Name,
                    Components = {new CuiNeedsCursorComponent()}
                });

            foreach (var Element in AllElements(UI))
            {
                var ElementName = Element.Name;
                var AnchorMin = Element.Position.X + " " + Element.Position.Y;
                var AnchorMax = Element.Position.X + Element.Position.Width + " " + (Element.Position.Y +
                                                                                     Element.Position.Height);
                var Image = GetImageFromMedia(Element.Media);
                var Button = Element as Button;
                if (Button != null)
                {
                    UI.CachedElements.Add(new CuiElement
                    {
                        Parent = Element.Parent,
                        Name = ElementName + "Background",
                        Components =
                        {
                            Image,
                            new CuiRectTransformComponent
                            {
                                AnchorMax = AnchorMax,
                                AnchorMin = AnchorMin
                            }
                        }
                    });
                    UI.CachedElements.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMax = "1 1",
                                AnchorMin = "0 0"
                            },
                            Button =
                            {
                                Command = Button.Command
                            },
                            Text =
                            {
                                Text = Button.Text.Value,
                                FontSize = Button.Text.Size,
                                Align = Button.Text.Anchor,
                                Color = "1 1 1 1"
                            }
                        }, ElementName + "Background", ElementName);
                    continue;
                }

                var Text = Element as Text;
                if (Text == null)
                {
                    UI.CachedElements.Add(new CuiElement
                    {
                        Name = ElementName,
                        Parent = Element.Parent,
                        Components =
                        {
                            Image,
                            new CuiRectTransformComponent
                            {
                                AnchorMax = AnchorMax,
                                AnchorMin = AnchorMin
                            }
                        }
                    });
                    continue;
                }

                UI.CachedElements.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMax = AnchorMax,
                        AnchorMin = AnchorMin
                    },
                    Text =
                    {
                        Text = Text.Value,
                        FontSize = Text.Size,
                        Align = Text.Anchor,
                        Color = HexToRGB(Text.Color)
                    }
                }, Element.Parent, ElementName);
            }
        }

        /// <summary>
        ///     Get all UI elements (button, text, panel) in one list
        /// </summary>
        /// <param name="UI">Target UI</param>
        /// <returns></returns>
        private static IEnumerable<UIElement> AllElements(UI UI)
        {
            return UI.UIElements.Buttons
                .Union(UI.UIElements.Panels)
                .Union(UI.UIElements.Text);
        }

        /// <summary>
        ///     Convert Image (media) to CuiRawImageComponent (needed because Image can be color or image url)
        /// </summary>
        /// <param name="Image">Media (Image/Color)</param>
        /// <returns>CuiRawImageComponent that has either a color or png</returns>
        private ICuiComponent GetImageFromMedia(string Media)
        {
            if (IsHexColor(Media)) return new CuiImageComponent {Color = HexToRGB(Media)};
            return new CuiRawImageComponent {Png = GetImage(Media)};
        }

        /// <summary>
        ///     Convert HEX to RGB (0-1)
        /// </summary>
        /// <param name="hexColor">Hex Color</param>
        /// <returns>RGB Color (0-1)</returns>
        private static string HexToRGB(string hexColor)
        {
            if (hexColor.StartsWith("#")) hexColor = hexColor.TrimStart('#');

            var red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
            var green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
            var blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);
            return $"{(double) red / 255} {(double) green / 255} {(double) blue / 255} 1";
        }

        private static bool IsHexColor(string Media)
        {
            return !Media.Contains("//") && !Media.Contains(".");
        }

        /// <summary>
        ///     Open a custom UI for given player
        /// </summary>
        /// <param name="Target">Target Player</param>
        /// <param name="UIName">UI Name</param>
        /// <param name="UI">Used if you've already found UI (saves performance)</param>
        private void OpenUI(BasePlayer Target, string UIName, UI UI = null)
        {
            if (UI == null && !Config.UIs.TryGetValue(UIName, out UI)) return;
            if (UI.CachedElements == null)
            {
                Target.ChatMessage(lang.GetMessage("UINotReady", this, Target.UserIDString));
                return;
            }

            CuiHelper.DestroyUi(Target, UIName); //Ensures you will not open the UI twice (forcing you to re-log)
            CuiHelper.AddUi(Target, UI.CachedElements);
            //Hook for developers
            Interface.Oxide.CallHook("OnUIOpened", Target, UIName);
        }

        #endregion

        #region Other

        /// <summary>
        ///     Register Custom UI chat commands and their respected permissions
        /// </summary>
        private void RegisterUICommands()
        {
            foreach (var Pair in Config.UIs)
            {
                var UI = Pair.Value;
                if (string.IsNullOrEmpty(UI.ChatCommand)) return;

                var UIPermission = $"customui.{UI.Permission}";
                permission.RegisterPermission(UIPermission, this);

                cmd.AddChatCommand(UI.ChatCommand, this, (Player, Label, Args) =>
                {
                    if (!Player.IPlayer.HasPermission(UIPermission))
                    {
                        Player.ChatMessage(lang.GetMessage("NoPermission", this, Player.UserIDString));
                        return;
                    }

                    Player.ChatMessage(string.Format(lang.GetMessage("OpeningUI", this, Player.UserIDString),
                        Pair.Key));
                    OpenUI(Player, Pair.Key, UI);
                });
            }
        }

        #endregion

        #endregion

        #region Configuration & Language

        private new ConfigFile Config;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"OpeningUI", "Opening the <color=#32a852>{0}</color> UI..."},
                {"NoPermission", "You don't have permission to open this UI!"},
                {"UINotReady", "The UI you're trying to use hasn't been constructed yet, try again later."}
            }, this);
        }

        public class ConfigFile
        {
            [JsonProperty("Max Time to Load UI (Turn up/down depending on how fast images download)")]
            public float TimeToLoadUI;

            public Dictionary<string, UI> UIs;

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile
                {
                    TimeToLoadUI = 15f,
                    UIs = new Dictionary<string, UI>
                    {
                        {
                            "TestUI", new UI
                            {
                                Permission = "testui",
                                Parent = "Hud",
                                ChatCommand = "test",
                                Cursor = false,
                                Media = "#000000",
                                Position = new Position
                                {
                                    X = 0.25f,
                                    Y = 0.25f,
                                    Width = 0.5f,
                                    Height = 0.5f
                                },
                                UIElements = new UIElements
                                {
                                    Buttons = new List<Button>
                                    {
                                        new Button
                                        {
                                            Name = "TestButton",
                                            Command = "CloseUI TestUI",
                                            Text = new Text
                                            {
                                                Color = "#ffffff",
                                                Value = "EXIT",
                                                Size = 15,
                                                Anchor = TextAnchor.MiddleCenter,
                                                Media = "#000000",
                                                Parent = "TestButton",
                                                Name = "TestButtonText"
                                            },
                                            Media = "#ffffff",
                                            Position = new Position
                                            {
                                                X = 0f,
                                                Y = 0f,
                                                Width = 0.125f,
                                                Height = 0.125f
                                            },
                                            Parent = "TestUI"
                                        }
                                    },
                                    Panels = new List<UIElement>
                                    {
                                        new UIElement
                                        {
                                            Name = "TestPanel",
                                            Position = new Position
                                            {
                                                X = 0.5f,
                                                Y = 0f,
                                                Width = 0.125f,
                                                Height = 0.125f
                                            },
                                            Media = "#787878",
                                            Parent = "TestUI"
                                        }
                                    },
                                    Text = new List<Text>
                                    {
                                        new Text
                                        {
                                            Name = "TestText",
                                            Position = new Position
                                            {
                                                X = 0f,
                                                Y = 0f,
                                                Width = 1f,
                                                Height = 1f
                                            },
                                            Value = "TEST",
                                            Parent = "TestPanel",
                                            Media = "#ffffff",
                                            Color = "#ffffff",
                                            Size = 15,
                                            Anchor = TextAnchor.MiddleCenter
                                        }
                                    }
                                }
                            }
                        }
                    }
                };
            }
        }

        #region UI Classes

        public class UI
        {
            [JsonIgnore] public CuiElementContainer CachedElements;

            [JsonProperty("Chat Command")] public string ChatCommand;

            [JsonProperty("Cursor Enabled?")] public bool Cursor;

            [JsonProperty("Background Media")] public string Media;

            public string Parent;
            public string Permission;

            public Position Position;

            [JsonProperty("UI Elements")] public UIElements UIElements;
        }

        public class UIElements
        {
            public List<Button> Buttons;
            public List<UIElement> Panels;
            public List<Text> Text;
        }

        /// <summary>
        ///     Base UI class
        /// </summary>
        public class UIElement
        {
            [JsonProperty("Media (URL/Hex Color)")]
            public string Media;

            public string Name;
            public string Parent;
            public Position Position;
        }

        public class Button : UIElement
        {
            public string Command;
            public Text Text;
        }

        public class Text : UIElement
        {
            [JsonProperty("Text Alignment")] public TextAnchor Anchor;

            [JsonProperty("Hex Color")] public string Color;

            [JsonProperty("Font Size")] public int Size;

            public string Value;
        }

        public class Position
        {
            [JsonProperty("Height (0-1)")] public float Height; //MaxY

            [JsonProperty("Width (0-1)")] public float Width; //MaxX

            [JsonProperty("X (0-1)")] public float X; //MinX

            [JsonProperty("Y (0-1)")] public float Y; //MinY
        }

        #endregion

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config = ((Plugin) this).Config.ReadObject<ConfigFile>();
            if (Config != null) return;
            LoadDefaultConfig();
        }

        protected override void LoadDefaultConfig()
        {
            Config = ConfigFile.DefaultConfig();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig()
        {
            ((Plugin) this).Config.WriteObject(Config);
        }

        #endregion
    }
}
//Generated with birthdates' Plugin Maker