using System;
using System.Globalization;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

/*1.2.1
 * Added: "Show Throwables" to config which shows hud for throwable weapons (grenades, c4, etc.)
 * Added: "Show Individual Ammo Icons" to config which shows the ammo image instead of a generic icon
 * Added: "Show Text Outline" to config
 */

namespace Oxide.Plugins
{
    [Info("Ammo HUD", "beee", "1.2.1")]
    [Description("Allows players to show an Ammo HUD.")]
    public class AmmoHUD : RustPlugin
    {
        public static AmmoHUD _plugin;
        private static PluginConfig _config;
        
        [PluginReference]
        private Plugin ImageLibrary;

        public PlayersData pData;

        public const string PREFIX_SHORT = "ahud.";
        public const string PREFIX_LONG = "ammohud.";

        public const string PERM_ADMIN = PREFIX_LONG + "admin";
        public const string PERM_USE = PREFIX_LONG + "use";
        
        #region Config

        private class PluginConfig
        {
            public Oxide.Core.VersionNumber Version;

            [JsonProperty("General Settings")]
            public GeneralConfigSettings GeneralSettings { get; set; }

            [JsonProperty("Position Settings")]
            public PositionConfigSettings PositionSettings { get; set; }

            #region GeneralConfigSettings
            internal class GeneralConfigSettings
            {
                [JsonProperty("Show Text Outline")]
                public bool ShowOutline { get; set; }

                [JsonProperty("Show Individual Ammo Icons")]
                public bool ShowAmmoIcons { get; set; }

                [JsonProperty("Show Throwables")]
                public bool ShowThrowables { get; set; }
            }
            #endregion

            #region PositionConfigSettings
            internal class PositionConfigSettings
            {
                [JsonProperty("Default State (true = on, false = off)")]
                public bool DefaultState { get; set; }

                [JsonProperty(
                    "Position (Top, TopLeft, TopRight, Left, Right, Bottom, BottomLeft, BottomRight)"
                )]
                public string Position { get; set; }

                [JsonProperty("Custom Position")]
                public CustomPositionConfigSettings CustomPositionSettings { get; set; }

                #region CustomPositionConfigSettings
                internal class CustomPositionConfigSettings
                {
                    [JsonProperty("Enabled")]
                    public bool Enabled { get; set; }

                    [JsonProperty("Custom Position")]
                    public PositionPreset CustomPosition { get; set; }
                }
                #endregion
            }
            #endregion
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        private PluginConfig GetDefaultConfig()
        {
            PluginConfig result = new PluginConfig
            {
                Version = Version,
                GeneralSettings = new PluginConfig.GeneralConfigSettings()
                {
                    ShowOutline = true,
                    ShowAmmoIcons = false,
                    ShowThrowables = false
                },
                PositionSettings = new PluginConfig.PositionConfigSettings()
                {
                    DefaultState = true,
                    Position = "Right",
                    CustomPositionSettings =
                        new PluginConfig.PositionConfigSettings.CustomPositionConfigSettings()
                        {
                            Enabled = false,
                            CustomPosition = new PositionPreset()
                            {
                                Position = PositionEnum.Right,
                                ParentPosition = new PositionParams()
                                {
                                    Enabled = true,
                                    AnchorMin = "1 0.5",
                                    AnchorMax = "1 0.5",
                                    OffsetMin = "-155 -32",
                                    OffsetMax = "-15 33"
                                },
                                WeaponAmmoFontSize = 36,
                                WeaponAmmoTextAlignment = TextAnchor.LowerRight,
                                WeaponAmmoPosition = new PositionParams()
                                {
                                    Enabled = true,
                                    AnchorMin = "0 0",
                                    AnchorMax = "0.79 0.70",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 0"
                                },
                                TotalAmmoFontSize = 22,
                                TotalAmmoTextAlignment = TextAnchor.LowerRight,
                                TotalAmmoPosition = new PositionParams()
                                {
                                    Enabled = true,
                                    AnchorMin = "0 0.55",
                                    AnchorMax = "1 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = "0 0"
                                },
                                IconPosition = new PositionParams()
                                {
                                    Enabled = true,
                                    AnchorMin = "1 0.13",
                                    AnchorMax = "1 0.13",
                                    OffsetMin = "-30 0",
                                    OffsetMax = "0 30"
                                }
                            }
                        }
                }
            };

            return result;
        }

        private void CheckForConfigUpdates()
        {
            bool changes = false;

            if (
                _config == null
                || _config.PositionSettings == null
                || _config.PositionSettings.CustomPositionSettings == null
                || _config.PositionSettings.CustomPositionSettings.CustomPosition == null
            )
            {
                PluginConfig tmpDefaultConfig = GetDefaultConfig();
                _config = tmpDefaultConfig;
                changes = true;
            }

            //1.2.1 update
            if (_config.Version == null || _config.Version < new VersionNumber(1, 2, 1))
            {
                PluginConfig tmpDefaultConfig = GetDefaultConfig();
                _config.GeneralSettings = tmpDefaultConfig.GeneralSettings;
                changes = true;
            }

            if(_config.Version != Version)
            {
                changes = true;
            }

            if (changes)
            {
                _config.Version = Version;

                PrintWarning("Config updated");
                SaveConfig();
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();

            CheckForConfigUpdates();

            LoadColors();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion

        void OnServerInitialized()
        {
            if(!_config.GeneralSettings.ShowAmmoIcons)
            {
                if (!ImageLibrary)
                {
                    PrintError("The plugin is not installed on the server [ImageLibrary]");
                }
                else
                {
                    ImageLibrary.Call("AddImage", "https://www.dropbox.com/s/6y817vyw3je75ya/DvgPtiW.png?dl=1", "genammo");
                }
            }

            RegisterMessages();
            //permission.RegisterPermission(PERM_ADMIN, this);
            permission.RegisterPermission(PERM_USE, this);
        }

        void Loaded()
        {
            _plugin = this;

            LoadDefaultPresets();
            pData = PlayersData.Load();
        }

        void Unload()
        {
            SaveData();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer + "." + uisuffix);
            }
            _plugin = null;
            _config = null;
        }

        void RegisterMessages() => lang.RegisterMessages(messages, this);

        #region Data

        private void OnServerSave() => SaveData();

        private bool wiped { get; set; }

        private void OnNewSave(string filename) => wiped = true;

        private void SaveData()
        {
            if (pData != null)
                pData.Save();
        }

        public class PlayersData
        {
            public Dictionary<ulong, PlayerInfo> Players = new Dictionary<ulong, PlayerInfo>();

            public static PlayersData Load()
            {
                var data = Interface.Oxide.DataFileSystem.ReadObject<PlayersData>(nameof(AmmoHUD));
                if (data == null || _plugin.wiped)
                {
                    _plugin.PrintWarning("No player data found! Creating a new data file");
                    data = new PlayersData();
                    data.Save();
                }
                return data;
            }

            public void Save()
            {
                Interface.Oxide.DataFileSystem.WriteObject(nameof(AmmoHUD), this);
            }

            public bool ToggleHUDActive(ulong playerid)
            {
                PlayerInfo playerInfo;
                if (!Players.TryGetValue(playerid, out playerInfo))
                {
                    Players.Add(
                        playerid,
                        playerInfo = new PlayerInfo() { HUDActive = false, Position = "Default" }
                    );
                }
                else
                {
                    playerInfo.HUDActive = !playerInfo.HUDActive;
                }
                return playerInfo.HUDActive;
            }

            public bool IsActive(ulong playerid)
            {
                PlayerInfo playerInfo;
                if (!Players.TryGetValue(playerid, out playerInfo))
                {
                    return true;
                }
                else
                {
                    return playerInfo.HUDActive;
                }
            }

            public void SetWeaponActive(ulong playerid, bool active)
            {
                PlayerInfo playerInfo;
                if (!Players.TryGetValue(playerid, out playerInfo))
                {
                    Players.Add(
                        playerid,
                        playerInfo = new PlayerInfo()
                        {
                            HUDActive = _config.PositionSettings.DefaultState,
                            WeaponActive = active,
                            Position = "Default"
                        }
                    );
                }
                else
                {
                    playerInfo.WeaponActive = active;
                }
            }

            public bool IsWeaponActive(ulong playerid)
            {
                PlayerInfo playerInfo;
                if (!Players.TryGetValue(playerid, out playerInfo))
                {
                    return false;
                }
                else
                {
                    return playerInfo.WeaponActive;
                }
            }

            public PositionPreset GetPosition(ulong playerid)
            {
                PlayerInfo playerInfo;
                if (Players.TryGetValue(playerid, out playerInfo))
                {
                    var position = _plugin.GetPreset(playerInfo.Position);
                    if (position != null)
                        return position;
                }

                return _plugin.GetConfigPosition();
            }

            public void SetPosition(ulong playerid, string position)
            {
                PlayerInfo playerInfo;
                if (!Players.TryGetValue(playerid, out playerInfo))
                {
                    Players.Add(
                        playerid,
                        playerInfo = new PlayerInfo() { HUDActive = true, Position = position }
                    );
                }
                else
                {
                    playerInfo.Position = position;
                }
            }
        }

        public class PlayerInfo
        {
            public bool HUDActive = true;
            public string Position = "Default";

            [JsonIgnore]
            public bool WeaponActive = false;
        }

        #endregion

        #region Commands

        [ChatCommand("ammohud")]
        void AmmoHUDChatCMD(BasePlayer player, string command, string[] args)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERM_USE))
                return;

            if (args.Length == 0)
            {
                BroadcastCommands(player);
                return;
            }

            string argument = args[0].ToLower().Trim();

            if (argument == "toggle")
            {
                bool isActive = pData.ToggleHUDActive(player.userID);
                UpdateUI(player, player.GetHeldEntity() as AttackEntity);

                if (isActive)
                    Reply(player, lang.GetMessage("toggle_enabled", this, player.UserIDString));
                else
                    Reply(player, lang.GetMessage("toggle_disabled", this, player.UserIDString));
            }
            else if (argument.ToLower() == "default")
            {
                pData.SetPosition(player.userID, argument);
                UpdateUI(player, player.GetHeldEntity() as AttackEntity);

                Reply(player, string.Format(lang.GetMessage("positionsetto", this, player.UserIDString), "Default"));
            }
            else
            {
                var preset = GetPreset(argument);

                if (preset != null)
                {
                    pData.SetPosition(player.userID, argument);
                    UpdateUI(player, player.GetHeldEntity() as AttackEntity);

                    Reply(player, string.Format(lang.GetMessage("positionsetto", this, player.UserIDString), preset.Position.ToString()));
                }
            }
        }

        public void BroadcastCommands(BasePlayer player)
        {
            string msg = string.Format(lang.GetMessage("commands", this, player.UserIDString), fontColor1);

            SendReply(player, msg);
        }

        #endregion

        #region Hooks

        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERM_USE))
                return;

            if (newItem == null || newItem.info.itemid == 550753330)
            {
                UpdateUI(player, null);
                return;
            }

            bool statechanged = false;
            if (newItem != null && (newItem.GetHeldEntity() is BaseProjectile || (newItem.GetHeldEntity() is ThrownWeapon && _config.GeneralSettings.ShowThrowables)))
            {
                if (!pData.IsWeaponActive(player.userID))
                {
                    statechanged = true;
                    pData.SetWeaponActive(player.userID, true);
                }
            }
            else
            {
                if (pData.IsWeaponActive(player.userID))
                {
                    statechanged = true;
                    pData.SetWeaponActive(player.userID, false);
                }
            }

            if (statechanged || oldItem != newItem)
                UpdateUI(player, newItem.GetHeldEntity() as AttackEntity);
        }

        void OnWeaponFired(
            BaseProjectile projectile,
            BasePlayer player,
            ItemModProjectile mod,
            ProtoBuf.ProjectileShoot projectiles
        )
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERM_USE))
                return;

            if (projectile != null && !projectile.ShortPrefabName.Contains("snowballgun"))
                UpdateUI(player, projectile, UpdateEnum.WeaponAmmo);
        }

        void OnRocketLaunched(BasePlayer player, BaseEntity entity)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERM_USE))
                return;

            var projectile = player.GetHeldEntity() as BaseProjectile;

            if (projectile != null)
            {
                UpdateUI(player, projectile, UpdateEnum.WeaponAmmo);
            }
        }

        object OnAmmoUnload(BaseProjectile weapon, Item item, BasePlayer player)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERM_USE))
                return null;

            NextTick(() =>
            {
                if (player == null)
                    return;

                var projectile = player.GetHeldEntity() as BaseProjectile;
                if (projectile != null)
                    UpdateUI(player, projectile, UpdateEnum.AllAmmo);
            });

            return null;
        }

        object OnMagazineReload(BaseProjectile weapon, IAmmoContainer ammoSource, BasePlayer player)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, PERM_USE))
                return null;

            NextTick(() =>
            {
                UpdateUI(player, weapon, UpdateEnum.All);
            });

            return null;
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if(container == null || item == null || item.info == null)
                return;
                
            var player = container?.playerOwner as BasePlayer;
            if (player == null || player.UserIDString == null || !permission.UserHasPermission(player.UserIDString, PERM_USE))
                return;

            var attackEntity = player.GetHeldEntity() as AttackEntity;

            if (attackEntity != null)
            {
                UpdateUI(player, attackEntity, UpdateEnum.AllAmmo);
            }
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item) => OnItemAddedToContainer(container, item);

        void OnExplosiveThrown(BasePlayer player, BaseEntity entity)
        {
            if (!_config.GeneralSettings.ShowThrowables || player == null || !permission.UserHasPermission(player.UserIDString, PERM_USE) || entity is SupplySignal)
                return;

            NextTick(() =>
            {
                var ThrownWeapon = player.GetHeldEntity() as ThrownWeapon;
                
                if (ThrownWeapon != null)
                {
                    UpdateUI(player, ThrownWeapon, UpdateEnum.WeaponAmmo);
                }
            });
        }

        void OnExplosiveDropped(BasePlayer player, BaseEntity entity) => OnExplosiveThrown(player, entity);

        #endregion

        #region UI

        string defaultAmmoColor,
            redAmmoColor,
            orangeAmmoColor = "";

        string defaultIconColor,
            blueIconColor,
            redIconColor,
            blackIconColor,
            greyIconColor,
            greenIconColor = "";

        public void LoadColors()
        {
            defaultAmmoColor = GetColorFromHex("#ffffff", 80);
            redAmmoColor = GetColorFromHex("#ff3737", 80);
            orangeAmmoColor = GetColorFromHex("#ffa749", 80);

            defaultIconColor = GetColorFromHex("#ba9a51", 50);
            blueIconColor = GetColorFromHex("#3564c3", 50);
            redIconColor = GetColorFromHex("#d43224", 50);
            blackIconColor = GetColorFromHex("#1e1e1e", 50);
            greyIconColor = GetColorFromHex("#a1a1a1", 50);
            greenIconColor = GetColorFromHex("#57a14f", 50);
        }

        private enum UpdateEnum
        {
            All,
            WeaponAmmo,
            AvailableAmmo,
            AllAmmo,
            Icon
        }

        public string Layer = "Under";
        public string uisuffix = "wui";

        private void UpdateUI(
            BasePlayer player,
            AttackEntity item,
            UpdateEnum uiToUpdate = UpdateEnum.All
        )
        {
            if (player == null)
                return;

            if (item == null
                || !pData.IsActive(player.userID)
                || !pData.IsWeaponActive(player.userID)
                || (item is ThrownWeapon && (!_config.GeneralSettings.ShowThrowables || item.ShortPrefabName == "supplysignal.weapon")))
            {
                CuiHelper.DestroyUi(player, Layer + "." + uisuffix);
                return;
            }

            PositionPreset position = pData.GetPosition(player.userID);

            if (!position.ParentPosition.Enabled)
            {
                CuiHelper.DestroyUi(player, Layer + "." + uisuffix);
                return;
            }

            if (uiToUpdate == UpdateEnum.All)
                CuiHelper.DestroyUi(player, Layer + "." + uisuffix);


            ///Calc Amounts

            int currAmmo = 0;
            int availableAmmo = 0;

            ulong skin = 0;
            
            BaseProjectile projectile = item as BaseProjectile;
            ItemDefinition ammoItemDefinition = null;

            if(projectile != null)
            {
                currAmmo = projectile.primaryMagazine.contents;
                availableAmmo = projectile.GetAvailableAmmo();

                ammoItemDefinition = projectile.primaryMagazine.ammoType;
            }
            else
            {
                ThrownWeapon thrownWeapon = item as ThrownWeapon;
                Item cachedItem = item.GetCachedItem();

                if(thrownWeapon == null || cachedItem == null)
                    return;

                currAmmo = cachedItem.amount;
                availableAmmo = player.inventory.GetAmount(cachedItem.info) - currAmmo;

                if(availableAmmo <= 0)
                    availableAmmo = -1;

                skin = cachedItem.skin;

                ammoItemDefinition = cachedItem.info;
            }

            ///

            var cont = new CuiElementContainer();

            bool onlyUpdateText = true;

            if (uiToUpdate == UpdateEnum.All)
            {
                onlyUpdateText = false;

                cont.Add(
                    new CuiPanel
                    {
                        CursorEnabled = false,
                        RectTransform =
                        {
                            AnchorMin = position.ParentPosition.AnchorMin,
                            AnchorMax = position.ParentPosition.AnchorMax,
                            OffsetMin = position.ParentPosition.OffsetMin,
                            OffsetMax = position.ParentPosition.OffsetMax
                        },
                        Image = { Color = "0 0 1 0" }
                    },
                    Layer,
                    Layer + "." + uisuffix
                );
            }

            float outlineOpacity = 0;

            if(_config.GeneralSettings.ShowOutline)
                outlineOpacity = 0.3f;

            if (
                (
                    uiToUpdate == UpdateEnum.All
                    || uiToUpdate == UpdateEnum.AvailableAmmo
                    || uiToUpdate == UpdateEnum.AllAmmo
                ) && position.TotalAmmoPosition.Enabled
            )
            {
                // if (uiToUpdate == UpdateEnum.AvailableAmmo || uiToUpdate == UpdateEnum.AllAmmo)
                //     CuiHelper.DestroyUi(player, "." + uisuffix + "totammo");

                string availableAmmoTxt = "";

                if(availableAmmo >= 0)
                {
                    availableAmmoTxt = string.Format("{0:n0}", availableAmmo);

                    cont.Add(
                        new CuiElement
                        {
                            Parent = Layer + "." + uisuffix,
                            Name = "." + uisuffix + "totammo",
                            Components =
                            {
                                new CuiTextComponent()
                                {
                                    Color = GetColorFromHex("#ffffff", 50),
                                    Text = availableAmmoTxt,
                                    FontSize = position.TotalAmmoFontSize,
                                    Align = position.TotalAmmoTextAlignment,
                                    Font = "robotocondensed-bold.ttf"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = position.TotalAmmoPosition.AnchorMin,
                                    AnchorMax = position.TotalAmmoPosition.AnchorMax,
                                    OffsetMin = position.TotalAmmoPosition.OffsetMin,
                                    OffsetMax = position.TotalAmmoPosition.OffsetMax
                                },
                                new CuiOutlineComponent { Color = $"0 0 0 {outlineOpacity}", Distance = "1 -1" }
                            }, 
                            Update = onlyUpdateText
                        }
                    );
                }
            }

            if (
                (
                    uiToUpdate == UpdateEnum.All
                    || uiToUpdate == UpdateEnum.WeaponAmmo
                    || uiToUpdate == UpdateEnum.AllAmmo
                ) && position.WeaponAmmoPosition.Enabled
            )
            {
                // if (uiToUpdate == UpdateEnum.WeaponAmmo || uiToUpdate == UpdateEnum.AllAmmo)
                //     CuiHelper.DestroyUi(player, "." + uisuffix + "currammo");

                string ammoColor = defaultAmmoColor;

                bool dangerzone = false;

                if(projectile != null)
                {
                    int magCapacity = projectile.primaryMagazine.capacity;
                    float ammoRatio = (float)currAmmo / (float)magCapacity;

                    if (ammoRatio < 0.6f)
                        ammoColor = orangeAmmoColor;
                    if (ammoRatio < 0.3f)
                        ammoColor = redAmmoColor;
                }

                cont.Add(
                    new CuiElement
                    {
                        Parent = Layer + "." + uisuffix,
                        Name = "." + uisuffix + "currammo",
                        Components =
                        {
                            new CuiTextComponent()
                            {
                                Color = ammoColor,
                                Text = $"{currAmmo}",
                                FontSize = position.WeaponAmmoFontSize,
                                Align = position.WeaponAmmoTextAlignment,
                                Font = "robotocondensed-bold.ttf"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = position.WeaponAmmoPosition.AnchorMin,
                                AnchorMax = position.WeaponAmmoPosition.AnchorMax,
                                OffsetMin = position.WeaponAmmoPosition.OffsetMin,
                                OffsetMax = position.WeaponAmmoPosition.OffsetMax
                            },
                            new CuiOutlineComponent { Color = $"0 0 0 {outlineOpacity}", Distance = "1 -1" }
                        }, 
                        Update = onlyUpdateText
                    }
                );
            }

            if (
                (uiToUpdate == UpdateEnum.All || uiToUpdate == UpdateEnum.Icon)
                && position.IconPosition.Enabled
            )
            {
                if (uiToUpdate == UpdateEnum.Icon)
                    CuiHelper.DestroyUi(player, "." + uisuffix + "icon");

                string iconColor = defaultIconColor;
                
                if(_config.GeneralSettings.ShowAmmoIcons)
                {
                    cont.Add(
                        new CuiPanel
                        {
                            CursorEnabled = false,
                            RectTransform =
                            {
                                AnchorMin = position.IconPosition.AnchorMin,
                                AnchorMax = position.IconPosition.AnchorMax,
                                OffsetMin = position.IconPosition.OffsetMin,
                                OffsetMax = position.IconPosition.OffsetMax
                            },
                            Image = { Color = GetColorFromHex("#ffffff", 20)}
                        },
                        Layer + "." + uisuffix,
                        "." + uisuffix + "icon"
                    );

                    cont.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = "0.1 0.1",
                            AnchorMax = "0.9 0.9"
                        },
                        Image = { ItemId = ammoItemDefinition.itemid, SkinId = skin }
                    },
                    "." + uisuffix + "icon",
                    "." + uisuffix + "iconimage");
                }
                else
                {
                    if (ammoItemDefinition.shortname.Contains(".hv") || ammoItemDefinition.shortname.Contains("shotgun.fire"))
                    {
                        iconColor = blueIconColor;
                    }
                    else if (
                        ammoItemDefinition.shortname.Contains("incen")
                        || ammoItemDefinition.shortname.Contains("fire")
                        || ammoItemDefinition.shortname == "ammo.shotgun"
                        || ammoItemDefinition.shortname.Contains("buckshot")
                    )
                    {
                        iconColor = redIconColor;
                    }
                    else if (ammoItemDefinition.shortname.Contains("explo") || ammoItemDefinition.shortname.Contains("grenadelauncher.he"))
                    {
                        iconColor = blackIconColor;
                    }
                    else if (ammoItemDefinition.shortname.Contains("smoke"))
                    {
                        iconColor = greyIconColor;
                    }
                    else if (ammoItemDefinition.shortname.Contains("slug"))
                    {
                        iconColor = greenIconColor;
                    }

                    cont.Add(
                        new CuiElement
                        {
                            Parent = Layer + "." + uisuffix,
                            Name = "." + uisuffix + "icon",
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Color = iconColor,
                                    Png = GetImg("genammo")
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = position.IconPosition.AnchorMin,
                                    AnchorMax = position.IconPosition.AnchorMax,
                                    OffsetMin = position.IconPosition.OffsetMin,
                                    OffsetMax = position.IconPosition.OffsetMax
                                }
                            }
                        }
                    );
                }
            }
            CuiHelper.AddUi(player, cont);
        }

        #endregion

        #region Helpers


        private string GetColorFromHex(string hex, double alpha)
        {
            if (string.IsNullOrEmpty(hex))
                hex = "#FFFFFF";

            var str = hex.Trim('#');
            if (str.Length != 6)
                throw new Exception(hex);
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {alpha / 100}";
        }

        private string GetImg(string name)
        {
            return (string)ImageLibrary?.Call("GetImage", name) ?? "";
        }

        public void Reply(BasePlayer player, string msg)
        {
            BroadcastToPlayer(player, msg);
        }

        static string fontColor1 = "<color=orange>";
        static string fontColor2 = "<color=white>";

        private void BroadcastToAll(string msg) =>
            PrintToChat(fontColor1 + "[AmmoHUD]" + " </color>" + fontColor2 + msg + "</color>");

        private void BroadcastToPlayer(BasePlayer player, string msg) =>
            SendReply(
                player,
                fontColor1 + "[AmmoHUD]" + " </color>" + fontColor2 + msg + "</color>"
            );

        #endregion

        #region Lang
        Dictionary<string, string> messages = new Dictionary<string, string>() 
        {
            {"commands", "{0}Ammo HUD Available Commands:</color>\n"
                + "/ammohud toggle\t\tToggles on/off\n"
                + "/ammohud [position]\t\tRepositions on screen\n\n"
                + "{0}Available Positions:</color>\nDefault, Top, Bottom, Right, Left,\n"
                + "BottomRight, BottomLeft, TopRight, TopLeft"},
            {"toggle_enabled", "HUD enabled."},
            {"toggle_disabled", "HUD disabled."},
            {"positionsetto", "HUD position set to {0}."}
        };
        #endregion

        #region PositionPresets

        public PositionPreset GetConfigPosition()
        {
            if (_config.PositionSettings.CustomPositionSettings.Enabled)
            {
                return _config.PositionSettings.CustomPositionSettings.CustomPosition;
            }
            else
            {
                return GetPreset(_config.PositionSettings.Position);
            }
        }

        public PositionPreset GetPreset(string position)
        {
            return PositionPresets.Find(s => s.Position.ToString().ToLower() == position.ToLower());
        }

        public void LoadDefaultPresets()
        {
            PositionPresets = new List<PositionPreset>()
            {
                new PositionPreset()
                {
                    Position = PositionEnum.Right,
                    ParentPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "1 0.5",
                        AnchorMax = "1 0.5",
                        OffsetMin = "-165 -35",
                        OffsetMax = "-15 35"
                    },
                    WeaponAmmoFontSize = 36,
                    WeaponAmmoTextAlignment = TextAnchor.LowerRight,
                    WeaponAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.70",
                        OffsetMin = "0 1",
                        OffsetMax = "-35 0"
                    },
                    TotalAmmoFontSize = 22,
                    TotalAmmoTextAlignment = TextAnchor.LowerRight,
                    TotalAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0.55",
                        AnchorMax = "1 1",
                        OffsetMin = "0 0",
                        OffsetMax = "0 0"
                    },
                    IconPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "1 0.13",
                        AnchorMax = "1 0.13",
                        OffsetMin = "-30 0",
                        OffsetMax = "0 30"
                    }
                },
                new PositionPreset()
                {
                    Position = PositionEnum.BottomRight,
                    ParentPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "1 0",
                        AnchorMax = "1 0",
                        OffsetMin = "-355 25",
                        OffsetMax = "-215 90"
                    },
                    WeaponAmmoFontSize = 36,
                    WeaponAmmoTextAlignment = TextAnchor.LowerRight,
                    WeaponAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.70",
                        OffsetMin = "0 1",
                        OffsetMax = "-35 0"
                    },
                    TotalAmmoFontSize = 22,
                    TotalAmmoTextAlignment = TextAnchor.LowerRight,
                    TotalAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0.55",
                        AnchorMax = "1 1",
                        OffsetMin = "0 0",
                        OffsetMax = "0 0"
                    },
                    IconPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "1 0.13",
                        AnchorMax = "1 0.13",
                        OffsetMin = "-30 0",
                        OffsetMax = "0 30"
                    }
                },
                new PositionPreset()
                {
                    Position = PositionEnum.Bottom,
                    ParentPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0.5 0",
                        AnchorMax = "0.5 0",
                        OffsetMin = "-120 80",
                        OffsetMax = "20 145"
                    },
                    WeaponAmmoFontSize = 36,
                    WeaponAmmoTextAlignment = TextAnchor.LowerRight,
                    WeaponAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.70",
                        OffsetMin = "0 1",
                        OffsetMax = "-35 0"
                    },
                    TotalAmmoFontSize = 22,
                    TotalAmmoTextAlignment = TextAnchor.LowerRight,
                    TotalAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0.55",
                        AnchorMax = "1 1",
                        OffsetMin = "0 0",
                        OffsetMax = "0 0"
                    },
                    IconPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "1 0.13",
                        AnchorMax = "1 0.13",
                        OffsetMin = "-30 0",
                        OffsetMax = "0 30"
                    }
                },
                new PositionPreset()
                {
                    Position = PositionEnum.BottomLeft,
                    ParentPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0",
                        AnchorMax = "0 0",
                        OffsetMin = "15 60",
                        OffsetMax = "155 125"
                    },
                    WeaponAmmoFontSize = 36,
                    WeaponAmmoTextAlignment = TextAnchor.LowerLeft,
                    WeaponAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.70",
                        OffsetMin = "35 0",
                        OffsetMax = "0 0"
                    },
                    TotalAmmoFontSize = 22,
                    TotalAmmoTextAlignment = TextAnchor.LowerLeft,
                    TotalAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0.55",
                        AnchorMax = "1 1",
                        OffsetMin = "0 0",
                        OffsetMax = "0 0"
                    },
                    IconPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0.12",
                        AnchorMax = "0 0.12",
                        OffsetMin = "0 0",
                        OffsetMax = "30 30"
                    }
                },
                new PositionPreset()
                {
                    Position = PositionEnum.Left,
                    ParentPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0.5",
                        AnchorMax = "0 0.5",
                        OffsetMin = "15 -32",
                        OffsetMax = "155 33"
                    },
                    WeaponAmmoFontSize = 36,
                    WeaponAmmoTextAlignment = TextAnchor.LowerLeft,
                    WeaponAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.70",
                        OffsetMin = "35 0",
                        OffsetMax = "0 0"
                    },
                    TotalAmmoFontSize = 22,
                    TotalAmmoTextAlignment = TextAnchor.LowerLeft,
                    TotalAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0.55",
                        AnchorMax = "1 1",
                        OffsetMin = "0 0",
                        OffsetMax = "0 0"
                    },
                    IconPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0.12",
                        AnchorMax = "0 0.12",
                        OffsetMin = "0 0",
                        OffsetMax = "30 30"
                    }
                },
                new PositionPreset()
                {
                    Position = PositionEnum.TopLeft,
                    ParentPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 1",
                        AnchorMax = "0 1",
                        OffsetMin = "15 -95",
                        OffsetMax = "155 -30"
                    },
                    WeaponAmmoFontSize = 36,
                    WeaponAmmoTextAlignment = TextAnchor.LowerLeft,
                    WeaponAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.70",
                        OffsetMin = "35 0",
                        OffsetMax = "0 0"
                    },
                    TotalAmmoFontSize = 22,
                    TotalAmmoTextAlignment = TextAnchor.LowerLeft,
                    TotalAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0.55",
                        AnchorMax = "1 1",
                        OffsetMin = "0 0",
                        OffsetMax = "0 0"
                    },
                    IconPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0.12",
                        AnchorMax = "0 0.12",
                        OffsetMin = "0 0",
                        OffsetMax = "30 30"
                    }
                },
                new PositionPreset()
                {
                    Position = PositionEnum.Top,
                    ParentPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0.5 1",
                        AnchorMax = "0.5 1",
                        OffsetMin = "-110 -95",
                        OffsetMax = "30 -30"
                    },
                    WeaponAmmoFontSize = 36,
                    WeaponAmmoTextAlignment = TextAnchor.LowerRight,
                    WeaponAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.70",
                        OffsetMin = "0 1",
                        OffsetMax = "-35 0"
                    },
                    TotalAmmoFontSize = 22,
                    TotalAmmoTextAlignment = TextAnchor.LowerRight,
                    TotalAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0.55",
                        AnchorMax = "1 1",
                        OffsetMin = "0 0",
                        OffsetMax = "0 0"
                    },
                    IconPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "1 0.13",
                        AnchorMax = "1 0.13",
                        OffsetMin = "-30 0",
                        OffsetMax = "0 30"
                    }
                },
                new PositionPreset()
                {
                    Position = PositionEnum.TopRight,
                    ParentPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "1 1",
                        AnchorMax = "1 1",
                        OffsetMin = "-155 -95",
                        OffsetMax = "-15 -30"
                    },
                    WeaponAmmoFontSize = 36,
                    WeaponAmmoTextAlignment = TextAnchor.LowerRight,
                    WeaponAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0",
                        AnchorMax = "1 0.70",
                        OffsetMin = "0 1",
                        OffsetMax = "-35 0"
                    },
                    TotalAmmoFontSize = 22,
                    TotalAmmoTextAlignment = TextAnchor.LowerRight,
                    TotalAmmoPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "0 0.55",
                        AnchorMax = "1 1",
                        OffsetMin = "0 0",
                        OffsetMax = "0 0"
                    },
                    IconPosition = new PositionParams()
                    {
                        Enabled = true,
                        AnchorMin = "1 0.13",
                        AnchorMax = "1 0.13",
                        OffsetMin = "-30 0",
                        OffsetMax = "0 30"
                    }
                }
            };
        }

        public List<PositionPreset> PositionPresets;

        public class PositionPreset
        {
            [JsonIgnore]
            public PositionEnum Position { get; set; }
            public PositionParams ParentPosition { get; set; }
            public PositionParams WeaponAmmoPosition { get; set; }
            public int WeaponAmmoFontSize { get; set; }
            public TextAnchor WeaponAmmoTextAlignment { get; set; }
            public PositionParams TotalAmmoPosition { get; set; }
            public int TotalAmmoFontSize { get; set; }
            public TextAnchor TotalAmmoTextAlignment { get; set; }
            public PositionParams IconPosition { get; set; }
        }

        public class PositionParams
        {
            public bool Enabled { get; set; }
            public string AnchorMin { get; set; }
            public string AnchorMax { get; set; }
            public string OffsetMin { get; set; }
            public string OffsetMax { get; set; }
        }

        public enum PositionEnum
        {
            Top,
            TopLeft,
            TopRight,
            Left,
            Right,
            Bottom,
            BottomLeft,
            BottomRight
        }

        #endregion
    }
}
