using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Monument Player Settings", "YaMang-w-", "1.0.3")]
    [Description("Block certain actions, commands, etc. within the monument. (to teleport)")]
    class MonumentPlayerSettings : RustPlugin
    {
        #region Fleids
        [PluginReference] private Plugin NoEscape;
        private const string OutPostPrefab = "assets/bundled/prefabs/autospawn/monument/medium/compound.prefab";
        private const string BanditTownPrefab = "assets/bundled/prefabs/autospawn/monument/medium/bandit_town.prefab";
        private const string MainUI = "MonumentContainer";
        private const string UsePermission = "monumentplayersettings.use";
        Dictionary<string, Timer> teleportTimer = new Dictionary<string, Timer>();
        Dictionary<string, Vector3> monumentLists = new Dictionary<string, Vector3>();

        #endregion

        #region Hook

        void OnServerInitialized()
        {
            permission.RegisterPermission(UsePermission, this);

            if (_config.tpMonumentSetting.useBandit || _config.tpMonumentSetting.useOutPost) cmd.AddChatCommand(_config.generalSettings.Commands, this, nameof(MonumentTPCMD));

            if (!_config.monumentSetting.blockInMSpray) Unsubscribe(nameof(OnSprayCreate));

            if (!_config.monumentSetting.blockInMPickup) Unsubscribe(nameof(CanPickupEntity));

            if (_config.monumentSetting.blockInMCommands.Count == 0) Unsubscribe(nameof(OnPlayerCommand));

            if (_config.monumentSetting.blockInMActiveItems.Count == 0) Unsubscribe(nameof(OnActiveItemChanged));

            var outpost = FindMonumentPosition(OutPostPrefab);
            if (outpost == new Vector3()) return;
            monumentLists.Add("outpost", outpost);
            
            var bandit = FindMonumentPosition(BanditTownPrefab);
            if (bandit == new Vector3()) return;
            monumentLists.Add("bandit", bandit);
        }

        void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, MainUI);
            }
        }

        void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (player == null) return;


            bool find = InMonumentPosition(player);
            if (find)
            {
                var held = player.GetHeldEntity();
                if (held == null) return;
                var item = held.GetItem() ?? null;

                if (!_config.monumentSetting.blockInMActiveItems.Contains(item.info.shortname)) return;
                held.SetHeld(false);
                held.SendNetworkUpdate();
                player.SendNetworkUpdate();
                Messages(player, Lang("ActiveItemsCannot", item.name ?? item.info.displayName.english));
            }
        }

        object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (!_config.monumentSetting.blockInMCommands.Contains(command)) return null;

            bool find = InMonumentPosition(player);
            if (find)
            {
                Messages(player, Lang("CommandsCannot", command));
                return true;
            }

            return null;
        }


        bool CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            bool find = InMonumentPosition(player);
            if (find)
            {
                Messages(player, Lang("PickupCannot"));
                return false;
            }

            return true;
        }

        private object OnSprayCreate(SprayCan spray, Vector3 position, Quaternion rotation)
        {
            BasePlayer player = spray.GetOwnerPlayer();

            bool find = InMonumentPosition(player);
            if (find)
            {
                Messages(player, Lang("SparayCannot"));
                return false;
            }

            return null;
        }
        #endregion

        #region Commands
        private void MonumentTPCMD(BasePlayer player, string command, string[] args)
        {
            if(!permission.UserHasPermission(player.UserIDString, UsePermission))
            {
                Messages(player, Lang("NoPerm"));
                return;
            }
            MonumentTeleportUI(player);    
        }

        [ConsoleCommand("teleport.monument")]
        private void MonumentTeleport(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (arg.Args[0] != "close" && arg.Args[0] != "cancel")
            {
                var err = SetFlags(player);
                if (err != null)
                {
                    Messages(player, $"{err}");
                    return;
                }
            }

            Vector3 pos = new Vector3();
            switch (arg.Args[0])
            {
                case "outpost":
                    if (teleportTimer.ContainsKey(player.UserIDString))
                    {
                        Messages(player, Lang("CancelTp"));
                        return;
                    }
                    pos = monumentLists["outpost"];
                    Messages(player, Lang("TryTp", Lang("Outpost"), _config.tpMonumentSetting.cooltimeOutPost));
                    var OutpostTimer = timer.Once(_config.tpMonumentSetting.cooltimeOutPost, () =>
                    {
                        TeleportPlayer(player, pos, Lang("Outpost"));
                    });
                    teleportTimer.Add(player.UserIDString, OutpostTimer);

                    CuiHelper.DestroyUi(player, MainUI);
                    break;

                case "bandit":
                    if (teleportTimer.ContainsKey(player.UserIDString))
                    {
                        Messages(player, Lang("CancelTp"));
                        return;
                    }
                    pos = monumentLists["bandit"];
                    Messages(player, Lang("TryTp", Lang("Bandit"), _config.tpMonumentSetting.cooltimeBandit));
                    var Bandittimer = timer.Once(_config.tpMonumentSetting.cooltimeBandit, () =>
                    {
                        TeleportPlayer(player, pos, Lang("Bandit"));
                    });
                    teleportTimer.Add(player.UserIDString, Bandittimer);

                    CuiHelper.DestroyUi(player, MainUI);
                    break;
                    
                case "close":
                    CuiHelper.DestroyUi(player, MainUI);
                    break;
                case "cancel":
                    if (teleportTimer.ContainsKey(player.UserIDString))
                    {
                        Messages(player, Lang("CancelTp"));
                        teleportTimer[player.UserIDString].Destroy();
                        teleportTimer.Remove(player.UserIDString);
                    }
                    else
                    {
                        Messages(player, "There is nothing to cancel.");
                        CuiHelper.DestroyUi(player, MainUI);
                    }
                    break;
            }
        }
        #endregion

        #region Config
        private ConfigData _config;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "General Settings")] public GeneralSettings generalSettings { get; set; }
            [JsonProperty(PropertyName = "Teleport Settings")] public TPMonumentSetting tpMonumentSetting { get; set; }
            [JsonProperty(PropertyName = "Monument Settings")] public MonumentSetting monumentSetting { get; set; }
            public Oxide.Core.VersionNumber Version { get; set; }
        }
        public class GeneralSettings
        {
            [JsonProperty(PropertyName = "Prefix", Order = 1)] public string Prefix { get; set; }
            [JsonProperty(PropertyName = "SteamID", Order = 2)] public ulong SteamID { get; set; }
            [JsonProperty(PropertyName = "Commands", Order = 3)] public string Commands { get; set; }
        }

        public class TPMonumentSetting
        {
            [JsonProperty(PropertyName = "Use teleport Outpost?")] public bool useOutPost { get; set; }
            [JsonProperty(PropertyName = "teleport cooltime Outpost")] public float cooltimeOutPost { get; set; }
            [JsonProperty(PropertyName = "Use teleport Bandit?")] public bool useBandit { get; set; }
            [JsonProperty(PropertyName = "teleport cooltime Bandit")] public float cooltimeBandit { get; set; }
        }

        public class MonumentSetting
        {
            [JsonProperty(PropertyName = "Block commands in monuments")] public List<string> blockInMCommands { get; set; }
            [JsonProperty(PropertyName = "Block Spary in monuments")] public bool blockInMSpray { get; set; }
            [JsonProperty(PropertyName = "Block Active Item in monuments")] public List<string> blockInMActiveItems { get; set; }
            [JsonProperty(PropertyName = "Block Pickup in monuments")] public bool blockInMPickup { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<ConfigData>();
                if (_config == null) throw new Exception();
                Puts($"{_config.Version} | {Version}");

                if (_config.Version < Version)
                    UpdateConfigValues();
            }
            catch
            {
                Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}_BackupError.json");
                PrintError("An error occurred in the config\nFind the CommandsItem_BackupError file.");
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig() => _config = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                generalSettings = new GeneralSettings
                {
                    Prefix = "<color=#5892bf>[Monument-Settings]</color>\n",
                    SteamID = 0,
                    Commands = "mtp",
                },
                tpMonumentSetting = new TPMonumentSetting
                {
                    useBandit = true,
                    cooltimeBandit = 15f,
                    useOutPost = true,
                    cooltimeOutPost = 15f
                },
                monumentSetting = new MonumentSetting
                {
                    blockInMSpray = true,
                    blockInMPickup = true,
                    blockInMActiveItems = new List<string>
                    {
                        "fun.trumpet"
                    },
                    blockInMCommands = new List<string>
                    {
                        "remove"
                    }
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");
            Config.WriteObject(_config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}_{_config.Version}.json");
            _config.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "SparayCannot", "<color=#red>Spray cannot be use here.</color>" },
                { "CommandsCannot", "<color=red>{0} cannot be use here.</color>" },
                { "PickupCannot", "<color=red>cannot be pickup here.</color>" },
                { "ActiveItemsCannot", "<color=red>{0} cannot be held here.</color>" },
                { "TryTp", "<color=#d0d0d0>Teleport to {0} after {1} seconds</color>" },
                { "CompleteTp", "<color=#d0d0d0>{0} Teleport!</color>" },
                { "PlayerInFlagsTp", "<color=#d0d0d0>{0} Teleporting Canceled | Reason: {1}!</color>" },
                { "CancelTp", "<color=yellow>Canceled Teleport</color>" },
                { "AlreadyTp", "<color=yellow>Already Teleporting / cancel press</color>" },
                { "Outpost", "Outpost"},
                { "Bandit", "Bandit Camp" },
                { "NoPerm", "<color=red>You have not permission</color>" }
            }, this);
        }

        private string Lang(string key, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this), args);
        }

        #endregion

        #region Helper
        private void Messages(BasePlayer player, string text) => player.SendConsoleCommand("chat.add", 2, _config.generalSettings.SteamID, $"{_config.generalSettings.Prefix} {text}");
        private string SetFlags(BasePlayer player)
        {
            if (player.isMounted)
            {
                return "mounted";
            }
            if (player.IsWounded())
            {
                return "Wounded";
            }
            if (player.IsSwimming())
            {
                return "Swimming";
            }
            if (Convert.ToBoolean(NoEscape?.Call("IsBlocked", player)))
            {
                return "Raid or Combat Blocked";
            }
            if (player.IsDead())
            {
                return "Dead";
            }
            if (player.IsHostile())
            {
                return "It's impossible because you're hostile";
            }
            if (player.IsBuildingBlocked())
            {
                return "Your in a building blocked";
            }
            return null;
        }
        private bool CheckFlags(BasePlayer player)
        {
            if (SetFlags(player) != null)
                return false;
            else
                return true;
        }

        private bool InMonumentPosition(BasePlayer player)
        {
            bool find = false;

            foreach (var item in TerrainMeta.Path.Monuments)
            {

                if (item.name.ToLower() == BanditTownPrefab)
                {
                    find = item.IsInBounds(player.transform.position);
                    if (find)
                        break;
                    else
                        continue;
                }

                if (item.name.ToLower() == OutPostPrefab)
                {
                    find = item.IsInBounds(player.transform.position);
                    if (find)
                        break;
                    else
                        continue;
                }


            }
            return find;
        }

        private Vector3 FindMonumentPosition(string monumentPrefab)
        {

            var monumentPosition = TerrainMeta.Path.Monuments
            .Where(m => m.name.ToLower() == monumentPrefab)
            .FirstOrDefault()?
            .transform
            .position;

            if (null == monumentPosition)
                Puts($"Failed to find a location for Monument: {monumentPrefab}");

            return monumentPosition ?? new Vector3();
        }
        private bool TeleportPlayer(BasePlayer player, Vector3 pos, string text)
        {
            var err = SetFlags(player);
            if (err != null)
            {
                if (teleportTimer.ContainsKey(player.UserIDString))
                {
                    Messages(player, Lang("PlayerInFlagsTp", text, err));
                    teleportTimer[player.UserIDString].Destroy();
                    teleportTimer.Remove(player.UserIDString);
                }
                return false;
            }

            RaycastHit hit;
            if (!Physics.Raycast(new Ray(pos + Vector3.up * 10, Vector3.down), out hit))
            {
                Messages(player, "[ERROR] - Monument not found");
                return false;
            }

            pos = hit.point;
            if (teleportTimer.ContainsKey(player.UserIDString))
            {
                player.Teleport(pos);
                
                teleportTimer[player.UserIDString].Destroy();
                teleportTimer.Remove(player.UserIDString);
            }

            return true;
        }
        private static string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            if (str.Length != 6) throw new Exception(HEX);
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100f}";
        }
        #endregion

        #region UI
        private void MonumentTeleportUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, MainUI);

            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                Image = { Color = "0 0 0 0.3529412", },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -69.992", OffsetMax = "150 70.008" }
            }, "Overlay", MainUI);

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.1607843 0.6705883 0.5294118 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 39.847", OffsetMax = "150 69.847" }
            }, MainUI, "TitlePanel");

            container.Add(new CuiButton
            {
                Button = { Color = "0.41 0.101 0.67 1", Command = "teleport.monument cancel" },
                Text = { Text = "<b>Cancel</b>", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "74.174 -15.154", OffsetMax = "121.038 15.153" }
            }, "TitlePanel", "CancelBtn");

            container.Add(new CuiButton
            {
                Button = { Color = "0 0 0 0.87", Command = "teleport.monument close" },
                Text = { Text = "<b>X</b>", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1" },
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "121.037 -15.154", OffsetMax = "150.004 15.153" }
            }, "TitlePanel", "CloseBtn");

            container.Add(new CuiElement
            {
                Name = "Title_Text",
                Parent = "TitlePanel",
                Components = {
                    new CuiTextComponent { Text = $"<b>Monument Teleport - [ {(CheckFlags(player) ? "<b><color=#00ff00>Available</color></b>" : "<b><color=red>Impossible</color></b>")} ]</b>", Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft, Color = "1 1 1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-144.735 -10.861", OffsetMax = "74.172 10.861" }
                }
            });
            if(_config.tpMonumentSetting.useBandit)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0.87", Command = "teleport.monument bandit" },
                    Text = { Text = $"<b>Bandit Camp\n\n\n[ {_config.tpMonumentSetting.cooltimeBandit}s ]</b>", Font = "robotocondensed-regular.ttf", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FadeIn = 2 },

                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -70.002", OffsetMax = "0 39.848" }
                }, MainUI, "BanditBtn");
            }
            else
            {
                if (_config.tpMonumentSetting.useOutPost)
                {
                    container.Add(new CuiButton
                    {
                        Button = { Color = "0 0 0 0.87", Command = "teleport.monument outpost" },
                        Text = { Text = $"<b>Outpost\n\n\n[ {_config.tpMonumentSetting.cooltimeBandit}s ]</b>", Font = "robotocondensed-regular.ttf", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FadeIn = 2 },

                        RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-150 -70.002", OffsetMax = "0 39.848" }
                    }, MainUI, "OutpostBtn");
                    return;
                }
            }
            

            if(_config.tpMonumentSetting.useOutPost)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = "0 0 0 0.87", Command = "teleport.monument outpost" },
                    Text = { Text = $"<b>Outpost\n\n\n[ {_config.tpMonumentSetting.cooltimeBandit}s ]</b>", Font = "robotocondensed-regular.ttf", FontSize = 17, Align = TextAnchor.MiddleCenter, Color = "1 1 1 1", FadeIn = 2 },

                    RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "0 -70.002", OffsetMax = "150 39.848" }
                }, MainUI, "OutpostBtn");
            }
            

            
            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}
