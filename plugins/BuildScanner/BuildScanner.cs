using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Build Scanner", "Mevent", "1.0.5")]
    [Description("Allows to scan entities (codelocks/autoturrets/cupboards and house)")]
    public class BuildScanner : CovalencePlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary;

        private const string Layer = "UI.Scanner";

        private static BuildScanner _instance;

        private const string PermUse = "buildscanner.use";

        private const string PermUnlimited = "buildscanner.unlimited";

        private const string PermCupboard = "buildscanner.cupboard";
        private const string PermCodeLock = "buildscanner.codelock";
        private const string PermAutoTurret = "buildscanner.autoturret";

        private const string PermHome = "buildscanner.home";

        private readonly Dictionary<BasePlayer, ScanData> _scanByPlayer = new Dictionary<BasePlayer, ScanData>();

        private class ScanData
        {
            public ScanPlayerData OwnerId;

            public List<ScanPlayerData> Members = new List<ScanPlayerData>();

            public static ScanData Get(IPlayer player, BaseEntity entity)
            {
                var cupboard = entity as BuildingPrivlidge;
                if (cupboard != null && player.HasPermission(PermCupboard))
                    return new ScanData
                    {
                        OwnerId = new ScanPlayerData(cupboard.OwnerID),
                        Members = cupboard.authorizedPlayers.Select(x => new ScanPlayerData(x)).ToList()
                    };

                var turret = entity as AutoTurret;
                if (turret != null && player.HasPermission(PermAutoTurret))
                    return new ScanData
                    {
                        OwnerId = new ScanPlayerData(turret.OwnerID),
                        Members = turret.authorizedPlayers.Select(x => new ScanPlayerData(x)).ToList()
                    };

                var codeLock = entity as CodeLock;
                if (codeLock != null && player.HasPermission(PermCodeLock))
                    return new ScanData
                    {
                        OwnerId = new ScanPlayerData(codeLock.OwnerID),
                        Members = codeLock.whitelistPlayers.Select(x => new ScanPlayerData(x)).ToList()
                    };

                var buildingPrivilege = entity.GetBuildingPrivilege();
                if (buildingPrivilege != null && player.HasPermission(PermHome))
                    return new ScanData
                    {
                        OwnerId = new ScanPlayerData(buildingPrivilege.OwnerID),
                        Members = buildingPrivilege.authorizedPlayers.Select(x => new ScanPlayerData(x)).ToList()
                    };

                return null;
            }
        }

        private class ScanPlayerData
        {
            public readonly string Name;

            public readonly ulong UserID;

            public readonly bool Online;

            public ScanPlayerData(PlayerNameID data)
            {
                var player = _instance.covalence.Players.FindPlayerById(data.userid.ToString());

                Name = data.username;
                UserID = data.userid;

                Online = player != null && player.IsConnected;
            }

            public ScanPlayerData(ulong member)
            {
                var player = _instance.covalence.Players.FindPlayerById(member.ToString());

                Name = player != null ? player.Name : "UNKNOWN";
                UserID = member;
                Online = player != null && player.IsConnected;
            }
        }

        #region Colors

        private string _color1;
        private string _color2;
        private string _color3;
        private string _color4;
        private string _color5;

        #endregion

        #endregion

        #region Config

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] Commands = { "scan" };

            [JsonProperty(PropertyName = "Cooldown between checks (seconds)")]
            public int Cooldown = 30;

            [JsonProperty(PropertyName = "Colors")]
            public Colors Colors = new Colors
            {
                Color1 = "#0E0E10",
                Color2 = "#161617",
                Color3 = "#4B68FF",
                Color4 = "#74884A",
                Color5 = "#B43D3D"
            };
        }

        private class Colors
        {
            [JsonProperty(PropertyName = "Color 1")]
            public string Color1;

            [JsonProperty(PropertyName = "Color 2")]
            public string Color2;

            [JsonProperty(PropertyName = "Color 3")]
            public string Color3;

            [JsonProperty(PropertyName = "Color 4")]
            public string Color4;

            [JsonProperty(PropertyName = "Color 5")]
            public string Color5;
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
            catch (Exception ex)
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
                Debug.LogException(ex);
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

        #region Data

        private PluginData _data;

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
        }

        private void LoadData()
        {
            try
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            if (_data == null) _data = new PluginData();
        }

        private class PluginData
        {
            [JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<ulong, PlayerData> Players = new Dictionary<ulong, PlayerData>();
        }

        private class PlayerData
        {
            [JsonProperty(PropertyName = "Last Time")]
            public DateTime LastTime = new DateTime(1970, 1, 1, 0, 0, 0);

            public int GetTime()
            {
                return (int)DateTime.UtcNow.Subtract(LastTime).TotalSeconds;
            }
        }

        private PlayerData GetPlayerData(BasePlayer player)
        {
            return GetPlayerData(player.userID);
        }

        private PlayerData GetPlayerData(ulong member)
        {
            if (!_data.Players.ContainsKey(member))
                _data.Players.Add(member, new PlayerData());

            return _data.Players[member];
        }

        private bool HasCooldown(BasePlayer player)
        {
            var data = GetPlayerData(player);
            if (data == null) return true;

            if (permission.UserHasPermission(player.UserIDString, PermUnlimited))
                return false;

            var time = data.GetTime();
            return time < _config.Cooldown;
        }

        private void UpdateCooldown(BasePlayer player)
        {
            var data = GetPlayerData(player);
            if (data == null) return;

            data.LastTime = DateTime.UtcNow;
        }

        private int GetLeftTime(BasePlayer player)
        {
            var data = GetPlayerData(player);
            if (data == null) return 0;

            return _config.Cooldown - data.GetTime();
        }

        #endregion

        #region Hooks

        private void Init()
        {
            _instance = this;

            LoadData();

            RegisterPermissions();

            LoadColors();
        }

        private void OnServerInitialized()
        {
            AddCovalenceCommand(_config.Commands, nameof(CmdChatOpen));

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        private void OnServerSave()
        {
            timer.In(Random.Range(2, 7), SaveData);
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);

            SaveData();

            _instance = null;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            GetAvatar(player.userID,
                avatar => ImageLibrary?.Call("AddImage", avatar, $"avatar_{player.UserIDString}"));
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            _scanByPlayer.Remove(player);
        }

        #endregion

        #region Commands

        private void CmdChatOpen(IPlayer cov, string command, string[] args)
        {
            var player = cov.Object as BasePlayer;
            if (player == null) return;

            if (!cov.HasPermission(PermUse))
            {
                Reply(cov, NoPermission);
                return;
            }

            if (HasCooldown(player))
            {
                Reply(cov, CooldownMsg, GetLeftTime(player));
                return;
            }

            RaycastHit hit;
            if (!Physics.Raycast(player.eyes.position, player.eyes.HeadForward(), out hit, 5f))
            {
                Reply(cov, EntityNotFound);
                return;
            }

            var entity = hit.GetEntity();
            if (entity == null)
            {
                Reply(cov, EntityNotFound);
                return;
            }

            var door = entity as Door;
            if (door != null)
            {
                entity = entity.GetSlot(BaseEntity.Slot.Lock) as CodeLock;
                if (entity == null)
                {
                    Reply(cov, EntityNotFound);
                    return;
                }
            }

            var hasPermission = HasPermission(cov, entity);
            if (hasPermission == null)
            {
                Reply(cov, EntityNotFound);
                return;
            }

            if (hasPermission == false)
            {
                Reply(cov, NoPermission);
                return;
            }

            var scan = ScanData.Get(cov, entity);
            if (scan == null)
            {
                Reply(cov, EntityNotFound);
                return;
            }

            _scanByPlayer[player] = scan;

            MainUi(player, first: true);

            UpdateCooldown(player);
        }

        [ConsoleCommand("UI_Scanner")]
        private void CmdConsole(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs()) return;

            switch (arg.Args[0])
            {
                case "close":
                {
                    _scanByPlayer.Remove(player);
                    break;
                }

                case "page":
                {
                    int page;
                    if (!arg.HasArgs(2) || !int.TryParse(arg.Args[1], out page)) return;

                    MainUi(player, page);
                    break;
                }
            }
        }

        #endregion

        #region Interface

        private void MainUi(BasePlayer player, int page = 0, bool first = false)
        {
            ScanData scan;
            if (!_scanByPlayer.TryGetValue(player, out scan) || scan == null) return;

            var amountOnString = 3;
            var lines = 4;
            var amountOnPage = amountOnString * lines;

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
                        Close = Layer,
                        Command = "UI_Scanner close"
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
                    OffsetMin = "-330 -230",
                    OffsetMax = "330 230"
                },
                Image =
                {
                    Color = _color1
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
                Image = { Color = _color2 }
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
                    Color = "1 1 1 1"
                }
            }, Layer + ".Header");

            float xSwitch = -25;
            float width = 25;

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
                    Color = "1 1 1 1"
                },
                Button =
                {
                    Close = Layer,
                    Color = _color3,
                    Command = "UI_Scanner close"
                }
            }, Layer + ".Header");

            #endregion

            #region Owner

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = "10 -80",
                    OffsetMax = "200 -60"
                },
                Text =
                {
                    Text = Msg(player, OwnerTitle),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 14,
                    Color = "1 1 1 1"
                }
            }, Layer + ".Main");

            PlayerUi(ref container, player, scan.OwnerId, "10 -145", "220 -85", true);

            #endregion

            #region List of Authorized

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = "10 -185",
                    OffsetMax = "200 -165"
                },
                Text =
                {
                    Text = Msg(player, ListTitle),
                    Align = TextAnchor.MiddleLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 14,
                    Color = "1 1 1 1"
                }
            }, Layer + ".Main");

            #region Pages

            if (scan.Members.Count > amountOnPage)
            {
                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "175 -185",
                        OffsetMax = "195 -165"
                    },
                    Text =
                    {
                        Text = Msg(player, BackBtn),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = HexToCuiColor(_config.Colors.Color3, 33),
                        Command = page != 0 ? $"UI_Scanner page {page - 1}" : ""
                    }
                }, Layer + ".Main");

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1",
                        OffsetMin = "200 -185",
                        OffsetMax = "220 -165"
                    },
                    Text =
                    {
                        Text = Msg(player, NextBtn),
                        Align = TextAnchor.MiddleCenter,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 10,
                        Color = "1 1 1 1"
                    },
                    Button =
                    {
                        Color = _color3,
                        Command = scan.Members.Count > (page + 1) * amountOnPage ? $"UI_Scanner page {page + 1}" : ""
                    }
                }, Layer + ".Main");
            }

            #endregion

            #region List

            var height = 60f;
            var margin = 5f;
            width = 210f;

            var ySwitch = -190f;
            xSwitch = 10f;

            var i = 1;
            foreach (var playerData in scan.Members.Skip(page * amountOnPage).Take(amountOnPage))
            {
                PlayerUi(ref container, player, playerData, $"{xSwitch} {ySwitch - height}",
                    $"{xSwitch + width} {ySwitch}",
                    playerData.UserID == scan.OwnerId.UserID);

                if (i % amountOnString == 0)
                {
                    xSwitch = 10f;
                    ySwitch = ySwitch - height - margin;
                }
                else
                {
                    xSwitch += width + margin;
                }

                i++;
            }

            #endregion

            #endregion

            #endregion

            CuiHelper.DestroyUi(player, Layer + ".Main");
            CuiHelper.AddUi(player, container);
        }

        private void PlayerUi(ref CuiElementContainer container, BasePlayer player, ScanPlayerData playerData,
            string offMin,
            string offMax, bool isOwner)
        {
            var guid = CuiHelper.GetGuid();

            container.Add(new CuiPanel
            {
                RectTransform =
                {
                    AnchorMin = "0 1", AnchorMax = "0 1",
                    OffsetMin = offMin, OffsetMax = offMax
                },
                Image =
                {
                    Color = _color2
                }
            }, Layer + ".Main", guid);

            if (ImageLibrary)
                container.Add(new CuiElement
                {
                    Parent = guid,
                    Components =
                    {
                        new CuiRawImageComponent
                            { Png = ImageLibrary.Call<string>("GetImage", $"avatar_{playerData.UserID}") },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "0 0",
                            OffsetMin = "5 5", OffsetMax = "55 55"
                        }
                    }
                });

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0.5", AnchorMax = "1 1",
                    OffsetMin = "60 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = $"{playerData.Name}",
                    Align = TextAnchor.LowerLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 14,
                    Color = "1 1 1 1"
                }
            }, guid);

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "0 0", AnchorMax = "1 0.5",
                    OffsetMin = "60 0", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = $"{playerData.UserID}",
                    Align = TextAnchor.UpperLeft,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 12,
                    Color = "1 1 1 0.5"
                }
            }, guid);

            container.Add(new CuiLabel
            {
                RectTransform =
                {
                    AnchorMin = "1 1", AnchorMax = "1 1",
                    OffsetMin = "-20 -20", OffsetMax = "0 0"
                },
                Text =
                {
                    Text = "•",
                    Align = TextAnchor.MiddleCenter,
                    Font = "robotocondensed-regular.ttf",
                    FontSize = 16,
                    Color = playerData.Online ? _color4 : _color5
                }
            }, guid);

            if (isOwner)
                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 0",
                        OffsetMin = "0 5", OffsetMax = "-5 20"
                    },
                    Text =
                    {
                        Text = Msg(player, OwnerSecondTitle),
                        Align = TextAnchor.LowerRight,
                        Font = "robotocondensed-regular.ttf",
                        FontSize = 12,
                        Color = "1 1 1 0.3"
                    }
                }, guid);
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

        private static bool? HasPermission(IPlayer player, BaseEntity entity)
        {
            if (entity is BuildingPrivlidge) return player.HasPermission(PermCupboard);

            if (entity is AutoTurret) return player.HasPermission(PermAutoTurret);

            if (entity is CodeLock) return player.HasPermission(PermCodeLock);

            return player.HasPermission(PermHome);
        }

        private void RegisterPermissions()
        {
            permission.RegisterPermission(PermUse, this);
            permission.RegisterPermission(PermCupboard, this);
            permission.RegisterPermission(PermCodeLock, this);
            permission.RegisterPermission(PermAutoTurret, this);
            permission.RegisterPermission(PermHome, this);
            permission.RegisterPermission(PermUnlimited, this);
        }

        private void LoadColors()
        {
            _color1 = HexToCuiColor(_config.Colors.Color1);
            _color2 = HexToCuiColor(_config.Colors.Color2);
            _color3 = HexToCuiColor(_config.Colors.Color3);
            _color4 = HexToCuiColor(_config.Colors.Color4);
            _color5 = HexToCuiColor(_config.Colors.Color5);
        }

        #region Avatar

        private readonly Regex _regex = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");

        private void GetAvatar(ulong userId, Action<string> callback)
        {
            if (callback == null) return;

            webrequest.Enqueue($"http://steamcommunity.com/profiles/{userId}?xml=1", null, (code, response) =>
            {
                if (code != 200 || response == null)
                    return;

                var avatar = _regex.Match(response).Groups[1].ToString();
                if (string.IsNullOrEmpty(avatar))
                    return;

                callback.Invoke(avatar);
            }, this);
        }

        #endregion

        #endregion

        #region Lang

        private const string
            OwnerSecondTitle = "OwnerSecondTitle",
            NextBtn = "NextBtn",
            BackBtn = "BackBtn",
            ListTitle = "ListTitle",
            OwnerTitle = "OwnerTitle",
            EntityNotFound = "EntityNotFound",
            CooldownMsg = "CooldownMsg",
            CloseButton = "CloseButton",
            TitleMenu = "TitleMenu",
            NoPermission = "NoPermission";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [NoPermission] = "You don't have the required permission",
                [TitleMenu] = "Scanner",
                [CloseButton] = "✕",
                [CooldownMsg] = "You will be able to scan after {0} sec.",
                [EntityNotFound] = "Entity to scan not found!",
                [OwnerTitle] = "Owner",
                [ListTitle] = "List of Authorized",
                [NextBtn] = "▼",
                [BackBtn] = "▲",
                [OwnerSecondTitle] = "owner"
            }, this);
        }

        private string Msg(string key, string userid = null, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, userid), obj);
        }

        private string Msg(BasePlayer player, string key, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, player.UserIDString), obj);
        }

        private string Msg(IPlayer player, string key, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, player.Id), obj);
        }

        private void Reply(BasePlayer player, string key, params object[] obj)
        {
            player.ChatMessage(Msg(player, key, obj));
        }

        private void Reply(IPlayer player, string key, params object[] obj)
        {
            player.Reply(Msg(player, key, obj));
        }

        #endregion
    }
}