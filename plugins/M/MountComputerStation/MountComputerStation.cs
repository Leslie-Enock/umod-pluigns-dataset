using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Mount Computer Station", "YaMang -w-", "1.1.2")]
    [Description("View computer stations with commands!")]
	public class MountComputerStation : RustPlugin
	{
		Dictionary<BasePlayer, Vector3> SetLocation = new Dictionary<BasePlayer, Vector3>();
        Dictionary<BasePlayer, BaseEntity> _station = new Dictionary<BasePlayer, BaseEntity>();
        private string UsePermission = "MountComputerStation.use";
        #region OxideHook
        private void OnServerInitialized()
        {
            permission.RegisterPermission(UsePermission, this);

            for (int i = 0; i < _config.commands.Count; i++)
            {
                cmd.AddChatCommand(_config.commands[i], this, nameof(ChatStation));
                cmd.AddConsoleCommand(_config.commands[i], this, nameof(ConsoleStation));
            }
        }
        private void Unload()
        {
            foreach (var item in _station)
            {
                item.Value.Kill();
            }

            foreach(var baseplayer in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(baseplayer, "ExitBtn");
            }    
            
        }
        #endregion

        #region Command
        private void ChatStation(BasePlayer player, string command, string[] args) => SetComputerStation(player);

        private void ConsoleStation(ConsoleSystem.Arg arg)
        {
			if(arg.IsRcon)
            {
                if(arg.Args.Length == 0)
                {
                    arg.Reply = Lang("SyntaxError");
                    return;
                }


				BasePlayer findplayer = BasePlayer.FindByID(Convert.ToUInt64(arg.Args[0])) ?? null;
                if(findplayer == null)
                {
                    arg.Reply = Lang("NotFound");
                    return;
                }

				SetComputerStation(findplayer);
			}

            var player = arg.Connection.player as BasePlayer ?? null;
            SetComputerStation(player);

        }
        #endregion

        #region Funtions
        private void SetComputerStation(BasePlayer player)
        {
            if(!permission.UserHasPermission(player.UserIDString, UsePermission))
            {
                Messages(player, Lang("NotAccess", player.UserIDString));
                return;
            }

            
			var station = GameManager.server.CreateEntity("assets/prefabs/deployable/computerstation/computerstation.deployed.prefab", _config.locationStation.PlayerLocation ? player.transform.position : _config.locationStation.location) as ComputerStation;
            if(_config.addcctvs.Count != 0)
            {
                NextFrame(() =>
                {
                    foreach (var item in _config.addcctvs)
                    {
                        if (station.controlBookmarks.Contains(item)) continue;
                        station.controlBookmarks.Add(item);
                    }
                    station.SendNetworkUpdateImmediate();
                });
            }
            station.Spawn();
			station._name += "__ym__";

			station.MountPlayer(player);
            NextTick(() =>
            {
                station.SendControlBookmarks(player);
            });
           
            if(player.isMounted)
            {
                
                if (!SetLocation.ContainsKey(player))
                    SetLocation.Add(player, player.transform.position);

                ExitUI(player);
                _station.Add(player, station);
            }
            else
                station.Kill();
            
		}
        [ConsoleCommand("scsdismount")]
        private void ScsDismount(ConsoleSystem.Arg arg)
        {
            if (arg.IsRcon) return;

            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;
            
            var entity = player.GetMounted();
            player.DismountObject();
            ScsDestory(entity, player);

            CuiHelper.DestroyUi(player, "ExitBtn");
        }
		private void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            if (entity == null || player == null) return;

            if (entity._name.Contains("__ym__"))
                ScsDestory(entity, player);
        }

        private void ScsDestory(BaseMountable entity, BasePlayer player)
        {
            if (SetLocation.ContainsKey(player))
            {
                timer.Once(0.01f, () =>
                {
                    player.Teleport(SetLocation[player]);
                    SetLocation.Remove(player);
                });
            }

            if (entity._name.Contains("__ym__"))
            {
                _station.Remove(player);
                entity.Kill();
                CuiHelper.DestroyUi(player, "ExitBtn");
            }
        }
        private void Messages(BasePlayer player, string text)
        {
            player.SendConsoleCommand("chat.add", 2, _config.SteamID, $"{_config.Prefix} {text}");
        }
        #endregion

        #region Config        
        private ConfigData _config;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Prefix")] public string Prefix { get; set; }
            [JsonProperty(PropertyName = "SteamID")]  public string SteamID { get; set; }
            [JsonProperty(PropertyName = "Commands")] public List<string> commands { get; set; }
            [JsonProperty(PropertyName = "Location Station")] public LocationStation locationStation { get; set; }
            [JsonProperty(PropertyName = "Auto Add Station CCTVs")] public List<string> addcctvs { get; set; }
            [JsonProperty(PropertyName = "UI Settings")] public UISettings uiSettings { get; set; }
            public Oxide.Core.VersionNumber Version { get; set; }
        }
        public class UISettings
        {
            public string oMin { get; set; }
            public string oMax { get; set; }
            public string Color { get; set; }
        }
        public class LocationStation
        {
            [JsonProperty(PropertyName = "Summoned from player position (If false, use location)")]  public bool PlayerLocation;
            public Vector3 location;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<ConfigData>();

            if (_config.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(_config, true);
        }

        protected override void LoadDefaultConfig() => _config = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                Prefix = "<color=#00ffff>[ Mount Computer Station ] - </color>\n",
                commands = new List<string>
                {
                    "scs"
                },
                locationStation = new LocationStation
                {
                    PlayerLocation = true,
                    location = new Vector3(0f, 100f, 0f)
                },
                uiSettings = new UISettings
                { 
                    oMin = "381.104 239.333",
                    oMax = "542.896 296.667",
                    Color = "0.176 0.72 0.72 1"
                },
                addcctvs = new List<string>
                {
                    
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");
            _config.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {

                { "NotAccess", "<color=red>You cannot use this command.</color>" },
                { "SyntaxError", "Please enter your SteamID" },
                { "NotFound", "Player Not Found" },
                { "Exit", "[ Exit ]" }

            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "NotAccess", "<color=red>당신은 이 명령어를 사용할수 없습니다.</color>" },
                { "SyntaxError", "고유번호를 입력해주세요." },
                { "NotFound", "플레이어를 찾을 수 없습니다." },
                { "Exit", "[ 닫기 ]" }
            }, this, "ko");
        }

        private string Lang(string key, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this), args);
        }

        #endregion


        #region UI
        private void ExitUI(BasePlayer player)
        {
            var container = new CuiElementContainer();
            container.Add(new CuiButton
            {
                Button = { Color = _config.uiSettings.Color, Command = "scsdismount" },
                Text = { Text = Lang("Exit"), Font = "robotocondensed-regular.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter, Color = "0 0 0 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = _config.uiSettings.oMin, OffsetMax = _config.uiSettings.oMax }
            }, "Overlay", "ExitBtn");

            CuiHelper.DestroyUi(player, "ExitBtn");
            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}
