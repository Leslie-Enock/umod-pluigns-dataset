using UnityEngine;
using System.Collections.Generic;
using Oxide.Core;
using Convert = System.Convert;
using System.Linq;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("My CH47", "FastBurst", "1.0.2")]
    [Description("Spawn a CH47 Helicopter")]

    public class MyCH47 : RustPlugin
    {
        #region Vars
        private const string PREFAB_CH47 = "assets/prefabs/npc/ch47/ch47.entity.prefab";
        private const string CH47spawn = "mych47.spawn";
        private const string CH47cooldown = "mych47.cooldown";
        private bool normalch47kill;
        private float trigger = 60;
        private Timer clock;

        public Dictionary<BasePlayer, BaseVehicle> baseplayerch47 = new Dictionary<BasePlayer, BaseVehicle>(); //for FUTURE
        #endregion

        #region Oxide
        private void Init()
        {
            permission.RegisterPermission(CH47spawn, this);
            permission.RegisterPermission(CH47cooldown, this);
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
        }

        private void OnServerInitialized()
        {
            float cooldownsec = (configData.CoolSettings.cooldownmin * 60);
            if (cooldownsec <= 120)
            {
                PrintError("Please set a longer cooldown time. Minimum is 2 min.");
                return;
            }
            clock = timer.Repeat(trigger, 0, () =>
            {
                LetsClock();
            });
        }

        private void Unload()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            if (!configData.DebrisSettings.withoutdebris)
                return;
            if (normalch47kill == true)
            {
                if (configData.DebugSettings.debug == true)
                    Puts($"IGNORING DEBRIS REMOVAL - NORMAL CH47 KILLED");

                return;
            }
            if (entity == null)
                return;
            var prefabname = entity.ShortPrefabName;
            if (string.IsNullOrEmpty(prefabname))
                return;
            if (entity is HelicopterDebris && prefabname.Contains("ch47"))
            {
                var debris = entity.GetComponent<HelicopterDebris>();
                if (debris == null) return;
                entity.Kill();
                if (configData.DebugSettings.debug == true)
                    Puts($"REMOVED DEBRIS FROM myCH47 KILLED");
            }
        }

        private void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null)
                return;
            if (entity.net.ID == null)
                return;

            CH47Helicopter check = entity as CH47Helicopter;

            if (check == null)
                return;
            if (storedData.playerch47 == null)
                return;
            ulong todelete = 0uL;

            if (storedData.playerch47.ContainsValue(entity.net.ID.Value) == false)
            {
                if (configData.DebugSettings.debug == true)
                    Puts($"KILLED CH47 not from myCH47");

                normalch47kill = true;
                timer.Once(6f, () =>
                {
                    normalch47kill = false;
                });
            }
            foreach (var item in storedData.playerch47)
            {
                if (item.Value == entity.net.ID.Value)
                {
                    ChatPlayerOnline(item.Key, "killed");
                    foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
                        if (player.userID == item.Key)
                            baseplayerch47.Remove(player);

                    todelete = item.Key;
                }
            }

            if (todelete != 0)
                storedData.playerch47.Remove(todelete);
        }
        #endregion

        #region Functions
        private void ChatPlayerOnline(ulong ailldi, string message)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
                if (player.userID == ailldi)
                    if (message == "killed")
                        Player.Message(player, $"<color={configData.ChatSettings.ChatColor}>{lang.GetMessage("KilledMsg", this, player.UserIDString)}</color>", $"<color={configData.ChatSettings.PrefixColor}> {configData.ChatSettings.Prefix} </color>", configData.ChatSettings.SteamIDIcon);
        }

        private void LetsClock()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList.ToList())
            {
                float cooldownsec = (configData.CoolSettings.cooldownmin * 60);

                if (configData.DebugSettings.debug)
                    Puts($"cooldown in seconds, calculated from config : {cooldownsec}");

                if (storedData.playercounter.ContainsKey(player.userID) == true)
                {
                    if (configData.DebugSettings.debug)
                        Puts($"player cooldown counter increment");

                    float counting = new float();
                    storedData.playercounter.TryGetValue(player.userID, out counting);
                    storedData.playercounter.Remove(player.userID);
                    counting = counting + trigger;
                    storedData.playercounter.Add(player.userID, counting);

                    if (configData.DebugSettings.debug)
                        Puts($"player {player.userID} newtime {counting}");

                    if (counting >= cooldownsec)
                    {
                        if (configData.DebugSettings.debug)
                            Puts($"player reached cooldown. removing from dict.");

                        storedData.playercounter.Remove(player.userID);
                    }
                    else
                    {
                        if (configData.DebugSettings.debug)
                            Puts($"player new cooldown counter in minutes : {counting / 60} / {configData.CoolSettings.cooldownmin}");

                        storedData.playercounter.Remove(player.userID);
                        storedData.playercounter.Add(player.userID, counting);
                    }
                }
            }
        }
        #endregion

        #region Commands
        [ChatCommand("mych47")]
        private void SpawnMyCH47(BasePlayer player, string command, string[] args)
        {
            bool isspawner = permission.UserHasPermission(player.UserIDString, CH47spawn);
            if (isspawner == false)
            {
                Player.Message(player, $"<color={configData.ChatSettings.ChatColor}><i>{lang.GetMessage("NoPermMsg", this, player.UserIDString)}</i></color>", $"<color={configData.ChatSettings.PrefixColor}> {configData.ChatSettings.Prefix} </color>", configData.ChatSettings.SteamIDIcon);
                return;
            }
            if (storedData.playerch47.ContainsKey(player.userID) == true)
            {
                Player.Message(player, $"<color={configData.ChatSettings.ChatColor}>{lang.GetMessage("AlreadyMsg", this, player.UserIDString)}</color>", $"<color={configData.ChatSettings.PrefixColor}> {configData.ChatSettings.Prefix} </color>", configData.ChatSettings.SteamIDIcon);
                return;
            }

            bool hascooldown = permission.UserHasPermission(player.UserIDString, CH47cooldown);
            float minleft = 0;

            if (hascooldown == true)
            {
                if (storedData.playercounter.ContainsKey(player.userID) == false)
                    storedData.playercounter.Add(player.userID, 0);
                else
                {
                    float count = new float();
                    storedData.playercounter.TryGetValue(player.userID, out count);
                    minleft = configData.CoolSettings.cooldownmin - (count / 60);

                    if (configData.DebugSettings.debug == true)
                        Puts($"Player DID NOT reach cooldown return.");

                    Player.Message(player, $"<color={configData.ChatSettings.ChatColor}>{lang.GetMessage("CooldownMsg", this, player.UserIDString)} ({minleft} min)</color>", $"<color={configData.ChatSettings.PrefixColor}> {configData.ChatSettings.Prefix} </color>", configData.ChatSettings.SteamIDIcon);
                    return;
                }
            }
            else
            {
                if (storedData.playercounter.ContainsKey(player.userID))
                    storedData.playercounter.Remove(player.userID);
            }

            Vector3 position = player.transform.position + (player.transform.forward * 20);
            if (position == null)
                return;
            BaseVehicle vehicleCH47 = (BaseVehicle)GameManager.server.CreateEntity(PREFAB_CH47, position, new Quaternion());
            if (vehicleCH47 == null)
                return;

            BaseEntity CHentity = vehicleCH47 as BaseEntity;
            CHentity.OwnerID = player.userID;

            vehicleCH47.Spawn();

            Player.Message(player, $"<color={configData.ChatSettings.ChatColor}>{lang.GetMessage("SpawnedMsg", this, player.UserIDString)}</color>", $"<color={configData.ChatSettings.PrefixColor}> {configData.ChatSettings.Prefix} </color>", configData.ChatSettings.SteamIDIcon);

            ulong ch47uint = vehicleCH47.net.ID.Value;

            if (configData.DebugSettings.debug == true)
                Puts($"SPAWNED CH47 {ch47uint.ToString()} for player {player.displayName} OWNER {CHentity.OwnerID}");

            storedData.playerch47.Remove(player.userID);
            storedData.playerch47.Add(player.userID, ch47uint);
            baseplayerch47.Remove(player);
            baseplayerch47.Add(player, vehicleCH47);
        }

        [ChatCommand("noch47")]
        private void KillMyCH47(BasePlayer player, string command, string[] args)
        {
            bool isspawner = permission.UserHasPermission(player.UserIDString, CH47spawn);
            if (isspawner == false)
            {
                Player.Message(player, $"<color={configData.ChatSettings.ChatColor}><i>{lang.GetMessage("NoPermMsg", this, player.UserIDString)}</i></color>", $"<color={configData.ChatSettings.PrefixColor}> {configData.ChatSettings.Prefix} </color>", configData.ChatSettings.SteamIDIcon);
                return;
            }
            if (storedData.playerch47.ContainsKey(player.userID) == true)
            {
                ulong deluint;
                storedData.playerch47.TryGetValue(player.userID, out deluint);
                var tokill = BaseNetworkable.serverEntities.Find(new NetworkableId(deluint));
                if (tokill != null)
                    tokill.Kill();

                storedData.playerch47.Remove(player.userID);
                baseplayerch47.Remove(player);
            }
        }
        #endregion

        #region Config
        private static ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Chat Settings")]
            public ChatOptions ChatSettings { get; set; }
            [JsonProperty(PropertyName = "Cooldown (on permissions)")]
            public CooldownOptions CoolSettings { get; set; }
            [JsonProperty(PropertyName = "Debris Settings")]
            public DebrisOptions DebrisSettings { get; set; }
            [JsonProperty(PropertyName = "Debug Option")]
            public DebugOptions DebugSettings { get; set; }

            public class ChatOptions
            {
                [JsonProperty(PropertyName = "ChatColor")]
                public string ChatColor { get; set; }
                [JsonProperty(PropertyName = "Prefix")]
                public string Prefix { get; set; }
                [JsonProperty(PropertyName = "Prefix Color")]
                public string PrefixColor { get; set; }
                [JsonProperty(PropertyName = "Chat Icon")]
                public ulong SteamIDIcon { get; set; }
            }

            public class CooldownOptions
            {
                [JsonProperty(PropertyName = "Value in minutes")]
                public float cooldownmin { get; set; }
            }

            public class DebrisOptions
            {
                [JsonProperty(PropertyName = "Remove debris")]
                public bool withoutdebris { get; set; }
            }

            public class DebugOptions
            {
                [JsonProperty(PropertyName = "Enable debug Messages in console")]
                public bool debug { get; set; }
            }
            public Oxide.Core.VersionNumber Version { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                ChatSettings = new ConfigData.ChatOptions
                {
                    ChatColor = "#bbffb1",
                    Prefix = "[My CH47]",
                    PrefixColor = "#149800",
                    SteamIDIcon = 76561198332562475
                },
                CoolSettings = new ConfigData.CooldownOptions
                {
                    cooldownmin = 60
                },
                DebrisSettings = new ConfigData.DebrisOptions
                {
                    withoutdebris = false
                },
                DebugSettings = new ConfigData.DebugOptions
                {
                    debug = false
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new Core.VersionNumber(1, 0, 0))
                configData = baseConfig;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
        #endregion

        #region Localization      
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"AlreadyMsg", "You already have a CH47 helicopter.\nuse command '/noch47' to remove it."},
                {"SpawnedMsg", "Your CH47 has spawned !\nuse command '/noch47' to remove it."},
                {"KilledMsg", "Your CH47 has been removed/killed."},
                {"NoPermMsg", "You are not allowed to do this."},
                {"CooldownMsg", "You must wait before a new CH47"},

            }, this, "en");
        }
        #endregion

        #region Data
        class StoredData
        {
            public Dictionary<ulong, ulong> playerch47 = new Dictionary<ulong, ulong>();
            public Dictionary<ulong, float> playercounter = new Dictionary<ulong, float>();
            public StoredData()
            {
            }
        }

        private StoredData storedData;
        #endregion
    }
}