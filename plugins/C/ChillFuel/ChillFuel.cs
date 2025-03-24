using System;
using System.Collections.Generic;

using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System.Globalization;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Chill Fuel", "Thisha", "0.3.2")]
    [Description("Simple visualisation of vehicle fuel amount")]
    public class ChillFuel : RustPlugin
    {
        #region variables
        private const string minicopterShortName = "minicopter.entity";
        private const string rowboatShortName = "rowboat";
        private const string RHIBShortName = "rhib";
        private const string fuelpermissionName = "chillfuel.use";
        private const string fuelmodpermissionName = "chillfuel.modify";

        #endregion variables

        #region localization
        private const string inviteNoticeMsg = "assets/bundled/prefabs/fx/invite_notice.prefab";
        private const string dplnMinicopter = "Minicopter";
        private const string dplnScrapTransport = "Scrap heli";
        private const string dplnMotorboat = "Motorboat";
        private const string dplnRHIB = "RHIB";
        private const string dplnCar = "Car";
        private const string dplnShowValue = "Show fuel";
        private const string lblClose = "Close";
        private const string noModPerm = "You are not allowed to modify fuel settings";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [dplnMinicopter] = dplnMinicopter,
                [dplnScrapTransport] = dplnScrapTransport,
                [dplnMotorboat] = dplnMotorboat,
                [dplnRHIB] = dplnRHIB,
                [dplnCar] = dplnCar,
                [dplnShowValue] = dplnShowValue,
                [lblClose] = lblClose,
                [noModPerm] = noModPerm
            }, this);
        }
        #endregion localization

        #region config
        private ConfigData config;

        class ConfigData
        {
            [JsonProperty(PropertyName = "Postition")]
            public AnchorPosition Position = new AnchorPosition
            {
                XAxis = 0.285f,
                YAxis = 0.010f
            };

            [JsonProperty(PropertyName = "Width")]
            public float Width = 0.045f;

            [JsonProperty(PropertyName = "Minicopter alert")]
            public int MiniAlert = 50;

            [JsonProperty(PropertyName = "Scrap heli alert")]
            public int ScrapAlert = 0;

            [JsonProperty(PropertyName = "Motorboat alert")]
            public int BoatAlert = 0;

            [JsonProperty(PropertyName = "RHIB alert")]
            public int RHIBAlert = 0;

            [JsonProperty(PropertyName = "Car alert")]
            public int CarAlert = 0;
        }

        private class AnchorPosition
        {
            [JsonProperty(PropertyName = "X-axis")]
            public float XAxis = 0;

            [JsonProperty(PropertyName = "Y-axis")]
            public float YAxis = 0;
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null)
                    throw new Exception();

                SaveConfig();
            }
            catch
            {
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion config

        #region data
        private Dictionary<ulong, PlayerData> playerData = new Dictionary<ulong, PlayerData>();

        class PlayerData
        {
            public bool Enabled;
            public int MiniAlert = 50;
            public int ScrapAlert = 0;
            public int BoatAlert = 0;
            public int RHIBAlert = 0;
            public int CarAlert = 0;
        }

        void InitPlayer(ulong userID, bool reset)
        {
            if (reset)
                playerData.Remove(userID);

            PlayerData data = new PlayerData();
            data.Enabled = true;
            data.MiniAlert = config.MiniAlert;
            data.ScrapAlert = config.ScrapAlert;
            data.BoatAlert = config.BoatAlert;
            data.RHIBAlert = config.RHIBAlert;
            data.CarAlert = config.CarAlert;
            
            playerData[userID] = data;
            SaveData();
        }

        void LoadData()
        {
            try
            {
                playerData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, PlayerData>>(Name);
            }
            catch
            {
                playerData = new Dictionary<ulong, PlayerData>();
            }
        }

        void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, playerData);
        #endregion data

        #region commands
        [ChatCommand("Fuel")]
        void HandleChatcommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, fuelmodpermissionName))
            {
                player.ChatMessage(Lang(noModPerm, player.UserIDString));
                return;
            }

            ShowPlayerPanel(player);
        }

        [ConsoleCommand("fuel.add")]
        private void DeltaAlarm(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null || arg.Connection.player == null)
                return;

            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            switch (arg.Args[0])
            {
                case "mini":
                    {
                        int newValue = playerData[player.userID].MiniAlert + Convert.ToInt32(arg.Args[1]);
                        if ((newValue < 0) || (newValue > 200))
                            return;

                        playerData[player.userID].MiniAlert = newValue;
                        SaveData();
                        break;
                    }

                case "scrap":
                    {
                        int newValue = playerData[player.userID].ScrapAlert + Convert.ToInt32(arg.Args[1]);
                        if ((newValue < 0) || (newValue > 200))
                            return;

                        playerData[player.userID].ScrapAlert = newValue;
                        SaveData();
                        break;
                    }
                    
                case "boat":
                    {
                        int newValue = playerData[player.userID].BoatAlert + Convert.ToInt32(arg.Args[1]);
                        if ((newValue < 0) || (newValue > 200))
                            return;

                        playerData[player.userID].BoatAlert = newValue;
                        SaveData();
                        break;
                    }
                    
                case "rhib":
                    {
                        int newValue = playerData[player.userID].RHIBAlert + Convert.ToInt32(arg.Args[1]);
                        if ((newValue < 0) || (newValue > 200))
                            return;

                        playerData[player.userID].RHIBAlert = newValue;
                        SaveData();
                        break;
                    }
                    
                case "car":
                    {
                        int newValue = playerData[player.userID].CarAlert + Convert.ToInt32(arg.Args[1]);
                        if ((newValue < 0) || (newValue > 200))
                            return;

                        playerData[player.userID].CarAlert = newValue;
                        SaveData();
                        break;
                    }
            }

            CuiHelper.DestroyUi(player, playerDataPanel);
            ShowValuePanel(player);
        }

        [ConsoleCommand("playerdatafuelclose")]
        private void CloseInfo(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null || arg.Connection.player == null)
                return;

            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            CuiHelper.DestroyUi(player, playerLabelPanel);
            CuiHelper.DestroyUi(player, playerDataPanel);
            CuiHelper.DestroyUi(player, playerButtonPanel);
        }

        [ConsoleCommand("playerdatatoggleshowfuel")]
        private void ToggleShowFuel(ConsoleSystem.Arg arg)
        {
            if (arg.Connection == null || arg.Connection.player == null)
                return;

            var player = arg.Connection.player as BasePlayer;
            if (player == null)
                return;

            playerData[player.userID].Enabled = !playerData[player.userID].Enabled;
            SaveData();

            CuiHelper.DestroyUi(player, playerDataPanel);
            ShowValuePanel(player);

            DestroyUI(player, true);
            if (playerData[player.userID].Enabled)
                CheckAction(player, true);
        }
        #endregion commands

        #region Hooks
        private void Init()
        {
            permission.RegisterPermission(fuelpermissionName, this);
            permission.RegisterPermission(fuelmodpermissionName, this);

            LoadData();
        }

        private void Unload()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (!player.IsAlive())
                    continue;

                if (!permission.UserHasPermission(player.UserIDString, fuelpermissionName))
                    continue;

                CuiHelper.DestroyUi(player, playerLabelPanel);
                CuiHelper.DestroyUi(player, playerDataPanel);
                CuiHelper.DestroyUi(player, playerButtonPanel);

                if (!PlayerSignedUp(player))
                    continue;

                DestroyUI(player, true);
            }
        }

        void OnPlayerDeath(BasePlayer player, ref HitInfo info)
        {
            if (!permission.UserHasPermission(player.UserIDString, fuelpermissionName))
                return;

            if (!PlayerSignedUp(player))
                return;

            DestroyUI(player, true);
        }

        void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, fuelpermissionName))
                return;
            
            if (!PlayerSignedUp(player))
                return;

            BaseEntity be = entity.GetParentEntity();
            if (be == null)
                return;

            EntityFuelSystem fuelSystem;

            ScrapTransportHelicopter scrap = be.GetComponentInParent<ScrapTransportHelicopter>();
            if (scrap != null)
            {
                fuelSystem = scrap.GetFuelSystem();
                if (fuelSystem != null)
                {
                    if (fuelSystem.fuelStorageInstance.IsValid(true))
                    {
                        UpdatePanels(player, fuelSystem.GetFuelAmount(), true);
                        DoPlayerTime(player, false);
                    }
                }
            }
            else
            {
                if (be.ShortPrefabName.Equals(minicopterShortName))
                {
                    Minicopter copter = be.GetComponentInParent<Minicopter>();
                    if (copter != null)
                    {
                        fuelSystem = copter.GetFuelSystem();
                        if (fuelSystem != null)
                        {
                            if (fuelSystem.fuelStorageInstance.IsValid(true))
                            {
                                UpdatePanels(player, fuelSystem.GetFuelAmount(), true);
                                DoPlayerTime(player, false);
                            }
                        }
                    }
                }
                else
                {
                    if (be.ShortPrefabName.Equals(rowboatShortName))
                    {
                        MotorRowboat boat = be.GetComponentInChildren<MotorRowboat>();
                        if (boat != null)
                        {
                            fuelSystem = boat.GetFuelSystem();
                            if (fuelSystem != null)
                            {
                                if (fuelSystem.fuelStorageInstance.IsValid(true))
                                {
                                    UpdatePanels(player, fuelSystem.GetFuelAmount(), true);
                                    DoPlayerTime(player, false);
                                }
                            }
                        }
                    }
                    else
                    {
                        if (be.ShortPrefabName.Equals(RHIBShortName))
                        {
                            RHIB rhib = be.GetComponentInChildren<RHIB>();
                            if (rhib != null)
                            {
                                fuelSystem = rhib.GetFuelSystem();
                                if (fuelSystem != null)
                                {
                                    if (fuelSystem.fuelStorageInstance.IsValid(true))
                                    {
                                        UpdatePanels(player, fuelSystem.GetFuelAmount(), true);
                                        DoPlayerTime(player, false);
                                    }
                                }
                            }
                        }
                        else
                        {
                            ModularCar car = be.GetComponentInChildren<ModularCar>();
                            if (car != null)
                            {
                                car.GetFuelSystem().HasFuel(false);
                                UpdatePanels(player, car.GetFuelSystem().GetFuelAmount(), true);
                                DoPlayerTime(player, false);
                            }
                        }
                    }
                }
            }
        }

        void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, fuelpermissionName))
                return;

            if (!PlayerSignedUp(player))
                return;

            DestroyUI(player, true);
        }
        #endregion Hooks

        #region Functions
        void DoPlayerTime(BasePlayer player, bool updatePicture)
        {
            if (player == null)
                return;
            
            if (player.isMounted)
            {
                timer.Once(5f, () =>
                {
                    CheckAction(player, updatePicture);
                });
            } 
            else
            {
                DestroyUI(player,true);
            }
        }

        void CheckAction(BasePlayer player, bool updatePicture)
        {
            if (player == null)
                return;

            if (!PlayerSignedUp(player))
            {
                DestroyUI(player, true);
                return;
            }

            EntityFuelSystem fuelSystem;

            BaseVehicle veh = player.GetMountedVehicle();
            if (veh != null)
            {
                ScrapTransportHelicopter scrap = veh.GetComponentInParent<ScrapTransportHelicopter>();
                if (scrap != null)
                {
                    fuelSystem = scrap.GetFuelSystem();
                    if (fuelSystem != null)
                    {
                        if (fuelSystem.fuelStorageInstance.IsValid(true))
                        {
                            if (playerData[player.userID].ScrapAlert > 0)
                            {
                                if ((fuelSystem.GetFuelAmount() >= playerData[player.userID].ScrapAlert - 2) && (fuelSystem.GetFuelAmount() <= playerData[player.userID].ScrapAlert))
                                    Effect.server.Run(inviteNoticeMsg, scrap.transform.position);
                            }

                            UpdatePanels(player, fuelSystem.GetFuelAmount(), updatePicture);
                            DoPlayerTime(player, false);
                        }
                    }
                }
                else
                {
                    Minicopter copter = veh.GetComponentInParent<Minicopter>();
                    if (copter != null)
                    {
                        fuelSystem = copter.GetFuelSystem();
                        if (fuelSystem != null)
                        {
                            if (fuelSystem.fuelStorageInstance.IsValid(true))
                            {
                                if (playerData[player.userID].MiniAlert > 0)
                                { 
                                    if ((fuelSystem.GetFuelAmount() >= playerData[player.userID].MiniAlert - 2) && (fuelSystem.GetFuelAmount() <= playerData[player.userID].MiniAlert))
                                        Effect.server.Run(inviteNoticeMsg, copter.transform.position);
                                }

                                UpdatePanels(player, fuelSystem.GetFuelAmount(), updatePicture);
                                DoPlayerTime(player, false);
                            }
                        }
                    }
                    else
                    {
                        RHIB rhib = veh.GetComponentInParent<RHIB>();
                        if (rhib != null)
                        {
                            fuelSystem = rhib.GetFuelSystem();
                            if (fuelSystem != null)
                            {
                                if (fuelSystem.fuelStorageInstance.IsValid(true))
                                {
                                    if (playerData[player.userID].RHIBAlert > 0)
                                    {
                                        if ((fuelSystem.GetFuelAmount() >= playerData[player.userID].RHIBAlert - 2) && (fuelSystem.GetFuelAmount() <= playerData[player.userID].RHIBAlert))
                                            Effect.server.Run(inviteNoticeMsg, rhib.transform.position);
                                    }

                                    UpdatePanels(player, fuelSystem.GetFuelAmount(), updatePicture);
                                    DoPlayerTime(player, false);
                                }
                            }
                        }
                        else
                        {
                            MotorRowboat motorBoat = veh.GetComponentInParent<MotorRowboat>();
                            if (motorBoat != null)
                            {
                                fuelSystem = motorBoat.GetFuelSystem();
                                if (fuelSystem != null)
                                {
                                    if (fuelSystem.fuelStorageInstance.IsValid(true))
                                    {
                                        if (playerData[player.userID].BoatAlert > 0)
                                        {
                                            if ((fuelSystem.GetFuelAmount() >= playerData[player.userID].BoatAlert - 2) && (fuelSystem.GetFuelAmount() <= playerData[player.userID].BoatAlert))
                                                Effect.server.Run(inviteNoticeMsg, motorBoat.transform.position);
                                        }

                                        UpdatePanels(player, fuelSystem.GetFuelAmount(), updatePicture);
                                        DoPlayerTime(player, false);
                                    }
                                }
                            }
                            else
                            {
                                ModularCar car = veh.GetComponentInParent<ModularCar>();
                                if (car != null)
                                {
                                    car.GetFuelSystem().HasFuel(true);

                                    if (playerData[player.userID].CarAlert > 0)
                                    {
                                        if ((car.GetFuelSystem().GetFuelAmount() >= playerData[player.userID].CarAlert - 2) && (car.GetFuelSystem().GetFuelAmount() <= playerData[player.userID].CarAlert))
                                            Effect.server.Run(inviteNoticeMsg, car.transform.position);
                                    }

                                    UpdatePanels(player, car.GetFuelSystem().GetFuelAmount(), updatePicture);
                                    DoPlayerTime(player, false);
                                }
                                else
                                    DestroyUI(player, true);
                            }
                        }
                    }
                }
            }
            else
            {
                DestroyUI(player, true);
            }
        }
        
        void UpdateState(BasePlayer player, bool newState)
        {
            bool doSave = false;

            if (!playerData.ContainsKey(player.userID))
            {
                InitPlayer(player.userID,false);
                playerData[player.userID].Enabled = newState;
                doSave = true;
            }
            else
            {
                if (playerData[player.userID].Enabled != newState)
                {
                    playerData[player.userID].Enabled = newState;
                    doSave = true;
                }
            }

            if (doSave)
            {
                SaveData();
                DestroyUI(player,true);
                if (newState == true)
                    CheckAction(player, true);
            }
        }
        #endregion Functions    

        #region ui
        void UpdatePanels(BasePlayer player, float condition, bool doPicture)
        {
            if (player == null)
                return;

            string color = "1 1 1 255";
            string valueText;

            if (condition < 0)
                condition = 0;

            valueText = ((int)Math.Round(condition, 0)).ToString();

            DestroyUI(player, doPicture);
            DrawUI(player, color, valueText, doPicture);
        }

        void DestroyUI(BasePlayer player, bool updatePicture)
        {
            CuiHelper.DestroyUi(player, "fuelmeterpanel");
            if (updatePicture)
                CuiHelper.DestroyUi(player, "fuelmeterpicture");
        }

        void DrawUI(BasePlayer player, string color, string valueText, bool updatePicture)
        {
            if (player == null)
                return;

            CuiElementContainer menu = Generate_Menu(player, color, valueText, updatePicture);
            CuiHelper.AddUi(player, menu);
        }

        CuiElementContainer Generate_Menu(BasePlayer player, string color, string valueText, bool updatePicture)
        {
            var elements = new CuiElementContainer();
            var info01 = elements.Add(new CuiLabel
            {
                Text =
                {
                    Text = valueText,
                    Color = "1 1 1 255",
                    FontSize = 13,
                    Align = TextAnchor.MiddleLeft
                },

                RectTransform = {
                    AnchorMin = (config.Position.XAxis + 0.015f).ToString() + " " + config.Position.YAxis.ToString(),      
                    AnchorMax = (config.Position.XAxis + 0.040f).ToString() + " " + (config.Position.YAxis + 0.020f).ToString() 
                },
            }, "Hud", "fuelmeterpanel"); ;

            if (updatePicture)
            {
                var elements2 = new CuiElementContainer();
                elements2.Add(new CuiElement
                {
                    Name = "fuelmeterpicture",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Color = "1 1 1 1",
                            Url = "https://i.imgur.com/t0d3aza.png"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = config.Position.XAxis.ToString() + " " + config.Position.YAxis.ToString(),
                            AnchorMax = (config.Position.XAxis + 0.010f).ToString() + " " + (config.Position.YAxis + 0.020f).ToString()
                        }
                    }
                }); 

                CuiHelper.AddUi(player, elements2);
            }

            return elements;
        }
        #endregion ui

        #region playerpanel
        private const string playerLabelPanel = "playerfuellabels";
        private const string playerDataPanel = "playerfueldata";
        private const string playerButtonPanel = "playerfuelbuttons";

        private void ShowPlayerPanel(BasePlayer player)
        {
            var elements = new CuiElementContainer();

            var panel = elements.Add(new CuiPanel
            {
                Image = {
                    Color = ColorExtensions.ColorFromHex("#859c5aE4",255)  
                },

                RectTransform = {
                    AnchorMin = "0.40 0.35",
                    AnchorMax = "0.50 0.7"
                },

                CursorEnabled = false
            }, "Hud", playerLabelPanel);

            elements.Add(AddLabel(Lang(dplnMinicopter, player.UserIDString), "0.08 0.80", "0.9 0.90"), playerLabelPanel);
            elements.Add(AddLabel(Lang(dplnScrapTransport, player.UserIDString), "0.08 0.65", "0.9 0.75"), playerLabelPanel);
            elements.Add(AddLabel(Lang(dplnMotorboat, player.UserIDString), "0.08 0.50", "0.9 0.60"), playerLabelPanel);
            elements.Add(AddLabel(Lang(dplnRHIB, player.UserIDString), "0.08 0.35", "0.9 0.45"), playerLabelPanel); 
            elements.Add(AddLabel(Lang(dplnCar, player.UserIDString), "0.08 0.20", "0.9 0.30"), playerLabelPanel);

            elements.Add(AddLabel(Lang(dplnShowValue, player.UserIDString), "0.08 0.05", "0.9 0.15"), playerLabelPanel);


            CuiHelper.AddUi(player, elements);

            ShowValuePanel(player);
            ShowButtonPanel(player);
        }

        private void ShowValuePanel(BasePlayer player)
        {
            var elements = new CuiElementContainer();

            var panel = elements.Add(new CuiPanel
            {
                Image = {
                    Color = ColorExtensions.ColorFromHex("#859c5aE4",255)
                },

                RectTransform = {
                    AnchorMin = "0.50 0.35",
                    AnchorMax = "0.55 0.7"
                },

                CursorEnabled = true
            }, "Hud", playerDataPanel);

            elements.Add(AddValueBackground("0.35 0.80", "0.65 0.90"), playerDataPanel);
            elements.Add(AddValueBackground("0.35 0.65", "0.65 0.75"), playerDataPanel);
            elements.Add(AddValueBackground("0.35 0.50", "0.65 0.60"), playerDataPanel);
            elements.Add(AddValueBackground("0.35 0.35", "0.65 0.45"), playerDataPanel);
            elements.Add(AddValueBackground("0.35 0.20", "0.65 0.30"), playerDataPanel);
            elements.Add(AddValueBackground("0.35 0.05", "0.65 0.15"), playerDataPanel);

            elements.Add(AddValueLabel(GetStringValue(playerData[player.userID].MiniAlert), "0.35 0.80", "0.65 0.90"), playerDataPanel);
            elements.Add(AddValueLabel(GetStringValue(playerData[player.userID].ScrapAlert), "0.35 0.65", "0.65 0.75"), playerDataPanel);
            elements.Add(AddValueLabel(GetStringValue(playerData[player.userID].BoatAlert), "0.35 0.50", "0.65 0.60"), playerDataPanel);
            elements.Add(AddValueLabel(GetStringValue(playerData[player.userID].RHIBAlert), "0.35 0.35", "0.65 0.45"), playerDataPanel);
            elements.Add(AddValueLabel(GetStringValue(playerData[player.userID].CarAlert), "0.35 0.20", "0.65 0.30"), playerDataPanel);

            if (playerData[player.userID].Enabled)
                elements.Add(AddValueLabel("V", "0.35 0.05", "0.65 0.15"), playerDataPanel);
            else
                elements.Add(AddValueLabel("X", "0.35 0.05", "0.65 0.15"), playerDataPanel);

            CuiHelper.AddUi(player, elements);
        }

        private void ShowButtonPanel(BasePlayer player)
        {
            var elements = new CuiElementContainer();

            var panel = elements.Add(new CuiPanel
            {
                Image = {
                    Color = ColorExtensions.ColorFromHex("#859c5aE4",255)
                },

                RectTransform = {
                    AnchorMin = "0.55 0.35",
                    AnchorMax = "0.72 0.7"
                },

                CursorEnabled = true
            }, "Hud", playerButtonPanel);

            elements.Add(CreateUpDownButton(playerButtonPanel, "mini", false, "0.25 0.80", "0.40 0.90"), playerButtonPanel);
            elements.Add(CreateUpDownButton(playerButtonPanel, "scrap", false, "0.25 0.65", "0.40 0.75"), playerButtonPanel);
            elements.Add(CreateUpDownButton(playerButtonPanel, "boat", false, "0.25 0.50", "0.40 0.60"), playerButtonPanel);
            elements.Add(CreateUpDownButton(playerButtonPanel, "rhib", false, "0.25 0.35", "0.40 0.45"), playerButtonPanel);
            elements.Add(CreateUpDownButton(playerButtonPanel, "car", false, "0.25 0.20", "0.40 0.30"), playerButtonPanel);
            
            elements.Add(CreateUpDownButton(playerButtonPanel, "mini", true, "0.45 0.80", "0.60 0.90"), playerButtonPanel);
            elements.Add(CreateUpDownButton(playerButtonPanel, "scrap", true, "0.45 0.65", "0.60 0.75"), playerButtonPanel);
            elements.Add(CreateUpDownButton(playerButtonPanel, "boat", true, "0.45 0.50", "0.60 0.60"), playerButtonPanel);
            elements.Add(CreateUpDownButton(playerButtonPanel, "rhib", true, "0.45 0.35", "0.60 0.45"), playerButtonPanel);
            elements.Add(CreateUpDownButton(playerButtonPanel, "car", true, "0.45 0.20", "0.60 0.30"), playerButtonPanel);

            elements.Add(CreateToggleButton(playerButtonPanel, !playerData[player.userID].Enabled, "0.35 0.05", "0.50 0.15"), playerButtonPanel);

            elements.Add(CreateCloseButton(playerButtonPanel, Lang(lblClose, player.UserIDString)), playerButtonPanel);
            
            CuiHelper.AddUi(player, elements);
        }

        private static CuiButton CreateToggleButton(string mainPanelName, bool toggleOn, string anchorMin, string anchorMax)
        {
            return new CuiButton
            {
                Button =
                {
                    Command = "playerdatatoggleshowfuel",
                    Color = ColorExtensions.ColorFromHex("#060804E4",255),
                },
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                },
                Text =
                {
                    Text = "<>",
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter
                },
                
            };
        }

        private static CuiButton CreateUpDownButton(string mainPanelName, string vehicle, bool up, string anchorMin, string anchorMax)
        {
            int value;
            string lblText = string.Empty;

            if (up)
            {
                value = 10;
                lblText = "+";
            }
            else
            {
                value = -10;
                lblText = "-";
            }

            string command = "fuel.add " + vehicle + " " + value.ToString();

            return new CuiButton
            {
                Button =
                {
                    Command = command,
                    Color = ColorExtensions.ColorFromHex("#060804E4",255)
                },
                RectTransform =
                {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                },
                Text =
                {
                    Text = lblText,
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter
                }
            };
        }

        private static CuiButton CreateCloseButton(string mainPanelName, string lblText)
        {
            return new CuiButton
            {
                Button =
                {
                    Command = string.Format("playerdatafuelclose", mainPanelName),
                    Color = ColorExtensions.ColorFromHex("#060804E4",255)
                },
                RectTransform =
                {
                    AnchorMin = "0.68 0.04",
                    AnchorMax = "0.94 0.16"
                },
                Text =
                {
                    Text = lblText,
                    FontSize = 14,
                    Align = TextAnchor.MiddleCenter
                }
            };
        }

        private static CuiLabel AddLabel(string labelText, string anchorMin, string anchorMax)
        {
            return new CuiLabel
            {
                Text =
                {
                Text = labelText,
                Color = ColorExtensions.ColorFromHex("#FFFFFFFF", 255),
                FontSize = 14,
                Align = TextAnchor.MiddleLeft,
                },

                RectTransform = {
                AnchorMin = anchorMin,
                AnchorMax = anchorMax
                },
            };
        }

        private static CuiLabel AddValueLabel(string labelText, string anchorMin, string anchorMax)
        {
            return new CuiLabel
            {
                Text =
                {
                Text = labelText,
                Color = ColorExtensions.ColorFromHex("#FFFFFFFF", 255),
                FontSize = 14,
                Align = TextAnchor.MiddleCenter,
                },

                RectTransform = {
                AnchorMin = anchorMin,
                AnchorMax = anchorMax
                },
            };
        }

        private static CuiPanel AddValueBackground(string anchorMin, string anchorMax)
        {
            return new CuiPanel
            {
                Image = {
                    Color = ColorExtensions.ColorFromHex("##060804E4",255)
                },

                RectTransform = {
                    AnchorMin = anchorMin,
                    AnchorMax = anchorMax
                },

                CursorEnabled = true
            };

        }
        #endregion playerpanel

        #region helpers
        private bool PlayerSignedUp(BasePlayer player)
        {
            if (playerData.ContainsKey(player.userID))
            {
                return playerData[player.userID].Enabled;
            }
            else
            {
                InitPlayer(player.userID, false);
                return true;
            }
        }

        private string GetStringValue(int value)
        {
            if (value != 0)
                return value.ToString();
            else
                return "0";
        }

        private string Lang(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);

        public static class ColorExtensions
        {
            public static string ToRustFormatString(Color color)
            {
                return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
            }

            //
            // UnityEngine 5.1 Color extensions which were removed in 5.2
            //

            public static string ToHexStringRGB(Color col)
            {
                Color32 color = col;
                return string.Format("{0}{1}{2}", color.r, color.g, color.b);
            }

            public static string ToHexStringRGBA(Color col)
            {
                Color32 color = col;
                return string.Format("{0}{1}{2}{3}", color.r, color.g, color.b, color.a);
            }

            public static bool TryParseHexString(string hexString, out Color color)
            {
                try
                {
                    color = FromHexString(hexString);
                    return true;
                }
                catch
                {
                    color = Color.white;
                    return false;
                }
            }

            private static Color FromHexString(string hexString)
            {
                if (string.IsNullOrEmpty(hexString))
                {
                    throw new InvalidOperationException("Cannot convert an empty/null string.");
                }
                var trimChars = new[] { '#' };
                var str = hexString.Trim(trimChars);
                switch (str.Length)
                {
                    case 3:
                        {
                            var chArray2 = new[] { str[0], str[0], str[1], str[1], str[2], str[2], 'F', 'F' };
                            str = new string(chArray2);
                            break;
                        }
                    case 4:
                        {
                            var chArray3 = new[] { str[0], str[0], str[1], str[1], str[2], str[2], str[3], str[3] };
                            str = new string(chArray3);
                            break;
                        }
                    default:
                        if (str.Length < 6)
                        {
                            str = str.PadRight(6, '0');
                        }
                        if (str.Length < 8)
                        {
                            str = str.PadRight(8, 'F');
                        }
                        break;
                }
                var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);
                var a = byte.Parse(str.Substring(6, 2), NumberStyles.HexNumber);

                return new Color32(r, g, b, a);
            }

            public static string ColorFromHex(string hexColor, int alpha)
            {
                hexColor = hexColor.TrimStart('#');
                if (hexColor.Length != 6 && hexColor.Length != 8)
                {
                    hexColor = "000000";
                }
                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);

                if (hexColor.Length == 8)
                {
                    alpha = int.Parse(hexColor.Substring(6, 2), NumberStyles.AllowHexSpecifier);
                }

                return $"{red / 255.0} {green / 255.0} {blue / 255.0} {alpha / 255.0}";
            }
        }
        #endregion helpers
    }
}