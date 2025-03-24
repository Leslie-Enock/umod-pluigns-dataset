using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Simple Sort", "birthdates", "1.0.7")]
    [Description("A UI supported sorting system for storage, based for performance and simplicity")]
    public class SimpleSort : RustPlugin
    {
        #region Variables

        private const string permission_use = "simplesort.use";
        private CuiElementContainer cuiElements;

        #endregion

        #region Core

        private void Init()
        {
            LoadConfig();
            permission.RegisterPermission(permission_use, this);
            cuiElements = new CuiElementContainer
            {
                {
                    new CuiButton
                    {
                        Text =
                        {
                            Text = _config.Ui.Text,
                            Color = ToUnityColor(_config.Ui.TextColor),
                            FontSize = _config.Ui.FontSize,
                            Align = TextAnchor.MiddleCenter,
                            FadeIn = 0.15f
                        },
                        Button =
                        {
                            Color = ToUnityColor(_config.Ui.Color),
                            FadeIn = 0.15f,
                            Command = "SimpleSort:Sort"
                        },
                        RectTransform =
                        {
                            AnchorMin = _config.Ui.AnchorMin,
                            AnchorMax = _config.Ui.AnchorMax
                        }
                    },
                    "Overlay",
                    "SimpleSortUI"
                }
            };
        }

        private void OnLootEntity(BasePlayer player, StorageContainer entity)
        {
            if (!(entity is BoxStorage) || _config.Blocked.Contains(entity.ShortPrefabName) || _config.Blocked.Contains(entity.PrefabName)) return;
            OpenUI(player);
        }

        private void OnLootEntityEnd(BasePlayer player)
        {
            CloseUI(player);
        }

        private static void CloseUI(BasePlayer Player)
        {
            CuiHelper.DestroyUi(Player, "SimpleSortUI");
        }

        private void OpenUI(BasePlayer Player)
        {
            if (!Player.IPlayer.HasPermission(permission_use)) return;
            CuiHelper.AddUi(Player, cuiElements);
        }

        [ConsoleCommand("SimpleSort:Sort")]
        private void ConsoleCommand(ConsoleSystem.Arg Arg)
        {
            var Player = Arg.Player();
            if (!Player || !Player.IPlayer.HasPermission(permission_use) ||
                Player.inventory.loot.containers.Count < 1) return;
            var Container = Player.inventory.loot.containers[0];
            if (Container == null || Container.itemList.Count < 1) return;
            var a = Container.itemList;
            a.Sort((x, y) => x.info.itemid.CompareTo(y.info.itemid) + x.skin.CompareTo(y.skin));
            a = a.OrderBy(b => b.info.category).ToList();
            while (Container.itemList.Count > 0) Container.itemList[0].RemoveFromContainer();

            foreach (var c in a) c.MoveToContainer(Container);
        }

        private ConfigFile _config;

        public class UI
        {
            [JsonProperty("Anchor Max")] public string AnchorMax;

            [JsonProperty("Anchor Min")] public string AnchorMin;

            [JsonProperty("Hex Color")] public string Color;

            [JsonProperty("Font Size")] public int FontSize;

            [JsonProperty("Text")] public string Text;

            [JsonProperty("Text Color")] public string TextColor;
        }

        public class ConfigFile
        {
            [JsonProperty("UI Settings")] public UI Ui;

            [JsonProperty("Blocked Items (short or long prefabs accepted)")] public List<string> Blocked;
            public static ConfigFile Default()
            {
                return new ConfigFile
                {
                    Ui = new UI
                    {
                        AnchorMin = "0.6564 0.1",
                        AnchorMax = "0.7 0.15",
                        Color = "#6f8344",
                        FontSize = 13,
                        Text = "Sort",
                        TextColor = "#A5BA7A"
                    },
                    Blocked = new List<string>()
                    {
                        "locker.deployed"
                    }
                };
            }
        }

        private static string ToUnityColor(string hexColor)
        {
            var BaseColor = new Color();
            return !ColorUtility.TryParseHtmlString(hexColor, out BaseColor)
                ? "1 1 1 1"
                : $"{BaseColor.r} {BaseColor.g} {BaseColor.b} {BaseColor.a}";
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigFile>();
            if (_config == null) LoadDefaultConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = ConfigFile.Default();
            PrintWarning("Default configuration has been loaded.");
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion
    }
}
//Generated with birthdates' Plugin Maker