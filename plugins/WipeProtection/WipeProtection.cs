using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Text;
using Rust;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("WipeProtection", "FastBurst", "2.1.6")]
    [Description("Blocks raiding after wipe for so many hours")]
    class WipeProtection : RustPlugin
    {
        [PluginReference] Plugin HeliSupport;
        #region Vars
        private List<BasePlayer> cooldown = new List<BasePlayer>();
        private float wipeprotecctime;
        private bool refund, broadcastend, msgadmin;
        private const string permUse = "wipeprotection.use";
        private static WipeProtection Instance { get; set; }
        private StoredData storedData;

        private Dictionary<string, string> raidtools = new Dictionary<string, string>
        {
            {"ammo.rocket.fire", "rocket_fire" },
            {"ammo.rocket.hv", "rocket_hv" },
            {"ammo.rocket.basic", "rocket_basic" },
            {"explosive.timed", "explosive.timed.deployed" },
            {"surveycharge", "survey_charge.deployed" },
            {"explosive.satchel", "explosive.satchel.deployed" },
            {"grenade.beancan", "grenade.beancan.deployed" },
            {"grenade.f1", "grenade.f1.deployed" },
            {"ammo.grenadelauncher.he", "40mm_grenade_he"},
            {"ammo.rifle", "riflebullet" },
            {"ammo.rifle.explosive", "riflebullet_explosive" },
            {"ammo.rifle.incendiary", "riflebullet_fire" },
            {"ammo.pistol", "pistolbullet" },
            {"ammo.pistol.fire", "pistolbullet_fire" },
            {"ammo.shotgun", "shotgunbullet" },
            {"ammo.shotgun.fire", "shotgunbullet_fire" },
            {"ammo.shotgun.slug", "shotgunslug" },
            {"arrow.fire", "arrow_fire" }
        };
        #endregion

        #region Oxide Hooks
        private void Unload() => SaveFile();

        private void Init()
        {
            permission.RegisterPermission(permUse, this);
            lang.RegisterMessages(Messages, this);
            Instance = this;
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(this.Name);
            CheckTime();
        }

        private void CheckTime()
        {
            timer.Every(30f, () => {
                if (!storedData.wipeprotection)
                    return;

                if (DateTime.Now >= Convert.ToDateTime(storedData.RaidStartTime))
                {
                    if (configData.Settings.broadcastend)
                        SendChatMessage("raidprotection_ended");

                    storedData.wipeprotection = false;
                    SaveFile();
                    return;
                }
            });
        }

        private void OnNewSave(string filename)
        {
            DateTime now = DateTime.Now;
            DateTime rs = now.AddHours(configData.Settings.wipeprotecctime);
            storedData.wipeprotection = true;
            storedData.lastwipe = SaveRestore.SaveCreatedTime.ToString();
            storedData.RaidStartTime = rs.ToString();
            SaveFile();
            PrintWarning(msg("console_auto"), now, rs);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (!player.IsAdmin || !configData.Settings.msgadmin || !storedData.wipeprotection)
                return;

            string remaining = Convert.ToDateTime(storedData.RaidStartTime).Subtract(DateTime.Now).ToShortString();
            SendReply(player, msg("adminmsg"), "<color=orange>[WipeProtection]</color>", storedData.RaidStartTime, remaining);
        }

        private object CanHelicopterTarget(PatrolHelicopterAI heli, BaseEntity entity)
        {
            object successAFD = Instance.HeliSupport?.Call("IsHeliSupport", heli as PatrolHelicopterAI);
            if (successAFD is bool && (bool)successAFD)
            {
                return true;
            }
            return true;
        }

        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (hitinfo == null || entity == null || entity.OwnerID == hitinfo?.InitiatorPlayer?.userID || entity?.OwnerID == 0 || hitinfo?.WeaponPrefab?.ShortPrefabName == null)
                return null;

            if (!(entity is BuildingBlock || entity is Door || entity.PrefabName.Contains("deployable")))
                return null;

            BasePlayer attacker = hitinfo.InitiatorPlayer;

            string name = null;

            if (hitinfo?.WeaponPrefab?.ShortPrefabName == "rocket_fire"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "rocket_hv"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "rocket_basic"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "explosive.timed.deployed"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "survey_charge.deployed"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "explosive.satchel.deployed"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "grenade.beancan.deployed"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "grenade.f1.deployed"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "40mm_grenade_he"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "ammo.rifle"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "ammo.rifle.explosive"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "ammo.rifle.incendiary"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "arrow.fire"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "ammo.pistol"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "ammo.pistol.fire"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "ammo.rifle.explosive"
            )
            {
                name = hitinfo?.WeaponPrefab?.ShortPrefabName;
            }
            else
            {
                name = hitinfo?.ProjectilePrefab?.name.ToString();
            }

            if (cooldown.Contains(attacker))
            {
                RemoveCD(attacker);
                if (WipeProtected())
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                    hitinfo.HitMaterial = 0;
                    return true;
                }
                return null;
            }

            cooldown.Add(attacker);
            RemoveCD(attacker);

            if (WipeProtected())
            {
                //Puts(name.ToString());
                hitinfo.damageTypes = new DamageTypeList();
                hitinfo.DoHitEffects = false;
                hitinfo.HitMaterial = 0;
                msgPlayer(attacker, entity);
                Refund(attacker, name, entity);
                return true;
            }

            return null;
        }
        #endregion

        #region Functions
        private void RemoveCD(BasePlayer player)
        {
            if (player == null)
                return;

            timer.In(0.1f, () => {
                if (cooldown.Contains(player)) cooldown.Remove(player);
            });
        }

        bool WipeProtected()
        {
            if (!storedData.wipeprotection)
                return false;

            if (DateTime.Now < (Convert.ToDateTime(storedData.RaidStartTime)))
                return true;

            return false;
        }

        private void msgPlayer(BasePlayer player, BaseEntity entity)
        {
            if (WipeProtected())
            {
                SendReply(player, msg("wipe_blocked"));
                return;
            }
        }

        private void Refund(BasePlayer player, string name, BaseEntity ent)
        {
            if (configData.Settings.Refunds.RefundAmmo)
            {
                foreach (var entry in raidtools)
                {
                    if (name == entry.Value)
                    {
                        Item item = ItemManager.CreateByName(entry.Key, 1);
                        player.GiveItem(item);
                        if (configData.Settings.Refunds.NotifyRefund)
                            SendReply(player, msg("refunded"), item.info.displayName.english);
                    }
                }
            }
            else
            {
                if (configData.Settings.Refunds.NotifyRefundNo)
                    SendReply(player, msg("refundedNot"));
            }
        }

        private void SaveFile() => Interface.Oxide.DataFileSystem.WriteObject(this.Name, storedData);
        #endregion

        #region Commands
        [ConsoleCommand("wp")]
        private void wipeCmd(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                return;

            var player = arg.Player();

            if (arg.Args == null || arg.Args.Length == 0)
            {
                Puts("Wipe Protection by:: FastBurst");
                Puts("wp start - Manually start wipe protection");
                Puts("wp stop - Manually stop wipe protection");
                return;
            }

            if (arg.IsAdmin == true)
            {
                switch (arg.Args[0].ToLower())
                {
                    case "start":
                        DateTime now = DateTime.Now;
                        DateTime rs = now.AddHours(configData.Settings.wipeprotecctime);
                        storedData.wipeprotection = true;
                        storedData.lastwipe = SaveRestore.SaveCreatedTime.ToString();
                        storedData.RaidStartTime = rs.ToString();
                        SaveFile();

                        Puts(msg("console_manual"), now, rs);
                        return;
                    case "stop":
                        storedData.wipeprotection = false;
                        SaveFile();
                        Puts(msg("console_stopped"));
                        return;
                    default:
                        break;
                }
            }
            else
            {
                if (permission.UserHasPermission(player.UserIDString, permUse) == false)
                {
                    SendReply(arg, msg("permission"));
                    return;
                }
            }
        }

        [ChatCommand("wp")]
        private void wipeCmd2(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, permUse) == false)
            {
                SendReply(player, msg("permission"));
                return;
            }

            if (args.Length == 0)
            {
                var helpmsg = new StringBuilder();
                helpmsg.Append("<size=22><color=green>Wipe Protection</color></size> by: FastBurst\n");
                helpmsg.Append("<color=orange>/wp start</color> - Manually start wipe protection\n");
                helpmsg.Append("<color=orange>/wp stop</color> - Manually stop wipe protection\n");
                SendReply(player, helpmsg.ToString().TrimEnd());
                return;
            }

            switch (args[0].ToLower())
            {
                case "start":
                    DateTime now = DateTime.Now;
                    DateTime rs = now.AddHours(configData.Settings.wipeprotecctime);
                    storedData.wipeprotection = true;
                    storedData.lastwipe = SaveRestore.SaveCreatedTime.ToString();
                    storedData.RaidStartTime = rs.ToString();
                    SaveFile();

                    SendReply(player, msg("console_manual"), now, rs);
                    return;
                case "stop":
                    storedData.wipeprotection = false;
                    SaveFile();
                    SendReply(player, msg("console_stopped"));
                    return;
                default:
                    player.ChatMessage("Invalid syntax!");
                    break;
            }
        }
        #endregion

        #region Config		
        private static ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "Settings")]
            public SettingOptions Settings { get; set; }

            public class SettingOptions
            {
                [JsonProperty(PropertyName = "Broadcast to chat when raid block has ended")]
                public bool broadcastend { get; set; }
                [JsonProperty(PropertyName = "Message admins on connection with info on when the raid block is ending")]
                public bool msgadmin { get; set; }
                [JsonProperty(PropertyName = "Wipe protection time (hours)")]
                public float wipeprotecctime { get; set; }
                [JsonProperty(PropertyName = "Refunding Options")]
                public RefundSettings Refunds { get; set; }

                public class RefundSettings
                {
                    [JsonProperty(PropertyName = "Allow Refunding of Explosives & Rockets")]
                    public bool RefundAmmo { get; set; }
                    [JsonProperty(PropertyName = "Enable notifican on refunding of ammo types")]
                    public bool NotifyRefund { get; set; }
                    [JsonProperty(PropertyName = "Enable notifican on denial of refunding of ammo types")]
                    public bool NotifyRefundNo { get; set; }
                }
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
                Settings = new ConfigData.SettingOptions
                {
                    broadcastend = true,
                    msgadmin = true,
                    wipeprotecctime = 24f,
                    Refunds = new ConfigData.SettingOptions.RefundSettings
                    {
                        RefundAmmo = true,
                        NotifyRefund = true,
                        NotifyRefundNo = true
                    }
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new Core.VersionNumber(2, 0, 0))
                configData = baseConfig;

            if (configData.Version < new Core.VersionNumber(2, 0, 3))
                configData.Settings.Refunds = baseConfig.Settings.Refunds;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        private T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
        #endregion

        #region DataFile
        private class StoredData
        {
            public bool wipeprotection;
            public string lastwipe;
            public string RaidStartTime;

            public StoredData()
            {

            }
        }
        #endregion

        #region Lang
        private static void SendChatMessage(string key, params object[] args)
        {
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                BasePlayer player = BasePlayer.activePlayerList[i];
                player.ChatMessage(args != null ? string.Format(msg(key, player.UserIDString), args) : msg(key, player.UserIDString));
            }
        }

        private static string msg(string key, string playerId = null) => Instance.lang.GetMessage(key, Instance, playerId);

        private Dictionary<string, string> Messages = new Dictionary<string, string>
        {
            ["adminmsg"] = "Wipe protection ending at <color=orange>{0} ({1})</color>",
            ["console_manual"] = "Manually setting {0} as wipe time and {1} as time after which raiding is possible",
            ["console_auto"] = "Detected wipe, setting {0} as wipe time and {1} as time after which raiding is possible",
            ["console_stopped"] = "Everything is now raidable",
            ["raidprotection_ended"] = "<size=20>Wipe protection is now over.</size>",
            ["dataFileWiped"] = "Data file successfully wiped",
            ["permission"] = "<color=#ff0000>You don't have permission to use that!</color>",
            ["refunded"] = "Your '{0}' was refunded.",
            ["refundedNot"] = "Your Ammo or Explosives are <color=red>not being refunded</color> and has been lost due to trying to raid before the time allowed.",
            ["wipe_blocked"] = "This entity cannot be destroyed because all raiding is currently blocked."
        };
        #endregion		
    }
}