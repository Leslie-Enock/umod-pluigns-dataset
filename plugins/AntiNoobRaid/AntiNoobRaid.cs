using Oxide.Core;
using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Rust;
using UnityEngine;
using Newtonsoft.Json;
using System.Linq;

/*========================================================*
*                                                         *
*   ***************************************************   *
*   *Original author :   Slydelix on versions <1.8.5  *   *
*   *Maintainer(s)   :   RustySpoon342 from v1.8.6    *   *
*   ***************************************************   *
*                                                         *
*    Patch Submissions : Nivex on versions 1.8.9, 1.9.0   *
*                                                         *
*=========================================================*/

namespace Oxide.Plugins
{
    [Info("AntiNoobRaid", "MasterSplinter", "2.1.1", ResourceId = 2697)]
    class AntiNoobRaid : RustPlugin
    {
        [PluginReference] private Plugin PlaytimeTracker, WipeProtection, Clans, StartProtection;

        //Pre-release Version Number
        private bool beta = false;
        private string betaversion = "0.0.6";

        private List<BasePlayer> cooldown = new List<BasePlayer>();
        private List<BasePlayer> MessageCooldown = new List<BasePlayer>();
        private List<ulong> LeaderLower = new List<ulong>();
        private List<BuildingBlock> _blocks = new List<BuildingBlock>();
        private Timer CheckingPT;
        private Timer CheckingC;

        //private Dictionary<ulong, Timer> RaidTimerDictionary = new Dictionary<ulong, Timer>();
        private Dictionary<string, string> raidtools = new Dictionary<string, string>
        {
            {"ammo.rocket.fire", "rocket_fire" },
            {"ammo.rocket.hv", "rocket_hv" },
            {"ammo.rocket.basic", "rocket_basic" },
            {"ammo.rocket.smoke", "rocket_smoke" },
            {"explosive.timed", "explosive.timed.deployed" },
            {"surveycharge", "survey_charge.deployed" },
            {"explosive.satchel", "explosive.satchel.deployed" },
            {"grenade.beancan", "grenade.beancan.deployed" },
            {"grenade.f1", "grenade.f1.deployed" },
            {"ammo.grenadelauncher.he", "40mm_grenade_he" },
            {"ammo.rifle", "riflebullet" },
            {"ammo.rifle.explosive", "riflebullet_explosive" },
            {"ammo.rifle.incendiary", "riflebullet_fire" },
            {"ammo.pistol", "pistolbullet" },
            {"ammo.pistol.fire", "pistolbullet_fire" },
            {"ammo.shotgun", "shotgunbullet" },
            {"ammo.shotgun.fire", "shotgunbullet_fire" },
            {"ammo.shotgun.slug", "shotgunslug" },
            {"ammo.rocket.mlrs", "rocket_mlrs" },
            {"arrow.wooden", "arrow_wooden" },
            {"arrow.hv", "arrow_hv" },
            {"arrow.fire", "arrow_fire" },
            {"arrow.bone", "arrow_bone" }
        };
        private Dictionary<string, string> RaidToolsCheck = new Dictionary<string, string>
        {
            {"ammo.rocket.fire", "rocket_fire" },
            {"ammo.rocket.hv", "rocket_hv" },
            {"ammo.rocket.basic", "rocket_basic" },
            {"ammo.rocket.smoke", "rocket_smoke" },
            {"explosive.timed", "explosive.timed.deployed" },
            {"surveycharge", "survey_charge.deployed" },
            {"explosive.satchel", "explosive.satchel.deployed" },
            {"grenade.beancan", "grenade.beancan.deployed" },
            {"grenade.f1", "grenade.f1.deployed" },
            {"ammo.grenadelauncher.he", "40mm_grenade_he" },
            {"ammo.grenadelauncher.buckshot", "shotgunbullet" },
            {"ammo.rifle", "riflebullet" },
            {"ammo.rifle.hv", "riflebullet" },
            {"ammo.pistol", "pistolbullet" },
            {"ammo.pistol.hv", "pistolbullet" },
            {"ammo.shotgun", "shotgunbullet" },
            {"ammo.shotgun.slug", "shotgunslug" },
            {"ammo.handmade.shell", "handmade_shell.projectile" },
            {"ammo.rocket.mlrs", "rocket_mlrs" },
            {"ammo.nailgun.nails", "nailgun.entity" },
            {"flamethrower", "flamethrower.entity" },
            {"arrow.wooden", "arrow_wooden" },
            {"arrow.hv", "arrow_hv" },
            {"arrow.bone", "arrow_bone" },
            {"spear.stone","spear_stone.entity" },
            {"speargun.spear","speargun_spear" },
            {"spear.wooden", "spear_wooden.entity" },
            {"knife.bone", "knife_bone.entity" },
            {"bone.club", "bone_club.entity" },
            {"knife.butcher", "butcherknife.entity" },
            {"machete", "machete.weapon" },
            {"knife.combat", "knife.combat.entity" },
            {"longsword", "longsword.entity" },
            {"mace", "mace.entity" },
            {"paddle", "paddle.entity" },
            {"pitchfork", "pitchfork.entity" },
            {"salvaged.cleaver", "salvaged_cleaver.entity" },
            {"salvaged.sword", "salvaged_sword.entity" },
            {"hatchet", "hatchet.entity" },
            {"pickaxe", "pickaxe.entity" },
            {"axe.salvaged", "axe_salvaged.entity" },
            {"hammer.salvaged", "hammer_salvaged.entity" },
            {"icepick.salvaged", "icepick_salvaged.entity" },
            {"stonehatchet", "stonehatchet.entity" },
            {"stone.pickaxe", "stone_pickaxe.entity" },
            {"rock", "rock.entity" },
            {"skull", "skull.entity" },
            {"jackhammer", "jackhammer.entity" }
        };
        private Dictionary<string, string> NotSupportedRaidTools = new Dictionary<string, string>
        {
            {"ammo.rifle.explosive", "riflebullet_explosive" },
            {"ammo.rifle.incendiary", "riflebullet_fire" },
            {"ammo.pistol.fire", "pistolbullet_fire" },
            {"ammo.shotgun.fire", "shotgunbullet_fire" },
            {"flamethrower", "flamethrower.entity" },
            {"snowball", "snowballgunbullet" },
            {"arrow.fire", "arrow_fire" }
        };


        private int layers = LayerMask.GetMask("Construction", "Deployed");
        private readonly string AdminPerm = "antinoobraid.admin";
        private readonly string NoobPerm = "antinoobraid.noob";

        #region Config

        private class ConfigFile
        {
            [JsonProperty(PropertyName = "Main Settings")]
            public MainSettings Main = new MainSettings();
            [JsonProperty(PropertyName = "Other Settings")]
            public OtherSettings Other = new OtherSettings();
            [JsonProperty(PropertyName = "Team & Clan Settings")]
            public RelationshipSettings Relationship = new RelationshipSettings();
            [JsonProperty(PropertyName = "Refund Settings")]
            public RefundSettings Refund = new RefundSettings();
            [JsonProperty(PropertyName = "Manage Messages")]
            public ManageMessages Messages = new ManageMessages();
            [JsonProperty(PropertyName = "Entity Settings")]
            public EntitySettings Entity = new EntitySettings();
            [JsonProperty(PropertyName = "Weapon Settings")]
            public WeaponSettings RaidTools = new WeaponSettings();
            [JsonProperty(PropertyName = "Advance Settings")]
            public AdvanceSettings Advance = new AdvanceSettings();

            public static readonly ConfigFile DefaultConfigFile = new ConfigFile();

        }

        public class MainSettings
        {
            [JsonProperty("Time (seconds) after which noob will lose protection (in-game time)")]
            public int ProtectionTime = 43200;
            [JsonProperty("Days of inactivity after which player will be raidable")]
            public double InactivityRemove = 3;
            [JsonProperty("Remove noob status of a raider on raid attempt")]
            public bool UnNoobNew = true;
            [JsonProperty("Remove noob status of a raider who is manually marked as a noob on raid attempt")]
            public bool UnNoobManual = true;
        }

        public class OtherSettings
        {
            [JsonProperty("Allow Patrol Helicopter to damage noob structures (This will allow players to raid noobs with Patrol Helicopter)")]
            public bool PatrolHeliDamage = true;
            [JsonProperty("Ignore twig when calculating base ownership (prevents exploiting)")]
            public bool IgnoreTwig = true;
            [JsonProperty("Check full ownership of the base instead of only one block")]
            public bool CheckFullOwnership = true;
            [JsonProperty("Kill fireballs when someone tries to raid protected player with fire (prevents lag)")]
            public bool KillFire = true;
            [JsonProperty("Clear Player Data on Map-Wipe")]
            public bool MapWipe = false;
        }

        public class RelationshipSettings
        {
            [JsonProperty("Enable 'Clan' Support (Allow clan members to destroy each others entities & Remove protection from clan members when a member tries to raid)")]
            public bool CheckClan = true;
            [JsonProperty("Enable 'Team' Support (Allow team members to destroy each others entities & Remove protection from team members when a member tries to raid)")]
            public bool CheckTeam = true;
            [JsonProperty("Enable 'Team' playtime sync (Sync's all player's within a team to the highest playtime)")]
            public bool SyncTeamPlaytime = true;
        }

        public class RefundSettings
        {
            [JsonProperty("Refund explosives")]
            public bool RefundItem = true;
            [JsonProperty("Refunds before player starts losing explosives")]
            public int RefundTimes = 4;
        }

        public class ManageMessages
        {
            [JsonProperty("Notify player on first connection with protection time")]
            public bool MessageOnFirstConnection = true;
            [JsonProperty("Use game tips to send most messages to players")]
            public bool UseGT = false;
            [JsonProperty("Show message for not being able to raid")]
            public bool ShowMessage = true;
            [JsonProperty("Show time until raidable")]
            public bool ShowTime = false;
            [JsonProperty("Show time until raidable only to owners")]
            public bool ShowOwnerTime = true;
            [JsonProperty("Show message on first Twig placement that Twig is not protected")]
            public bool ShowMessageTwig = true;
        }

        public class EntitySettings
        {
            [JsonProperty("List of entities that can be destroyed even if owner is noob")]
            public Dictionary<string, string> AllowedEntities = AllowedEntitiesDictionary;
            [JsonProperty("List of entities that can be destroyed without losing noob protection")]
            public Dictionary<string, string> AllowedEntitiesNoob = AllowedEntitiesNoobDictionary;

            public static Dictionary<string, string> AllowedEntitiesDictionary = new Dictionary<string, string>
            {
                {"ShortPrefabName", "Common Name"}, {"beartrap", "Snap Trap"}, {"landmine", "Landmine"},
            };
            public static Dictionary<string, string> AllowedEntitiesNoobDictionary = new Dictionary<string, string>
            {
                {"ShortPrefabName", "Common Name"}, {"campfire", "Camp Fire"},
            };
        }

        public class WeaponSettings
        {
            [JsonProperty("List of Weapons/Tools that won't trigger player to lose noob protection")]
            public Dictionary<string, string> AllowedRaidTools = AllowedRaidToolsDictionary;

            public static Dictionary<string, string> AllowedRaidToolsDictionary = new Dictionary<string, string>
            {
                {"ShortPrefabName", "Common Name"}, {"rocket_smoke", "Smoke Rocket WIP!!!!"},
            };
        }

        public class AdvanceSettings
        {
            [JsonProperty("User data refresh interval (seconds)")]
            public int Frequency = 30;
            [JsonProperty("Save interval (seconds)")]
            public int SaveFrequency = 60;
            [JsonProperty("Save data on Server Save")]
            public bool OnServerSave = true;
            [JsonProperty("Show structure has no owner in console")]
            public bool ShowNoOwnerBase = false;
            [JsonProperty("Enable Logs (Logs can be found in /oxide/logs/antinoobraid)")]
            public bool EnableLogging = true;
        }

        private ConfigFile config;

        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            PrintWarning("Creating new config file");
            NextTick(() => Config.WriteObject(config));
            SaveConfig();
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>()
            {
                {"NoPlayerFound", "Couldn't find '{0}'"},
                {"NotNumber",  "{0} is not a number" },

                {"antinoobcmd_syntax", "Wrong syntax! /antinoob <addnoob|removenoob|wipe>" },
                {"antinoobcmd_addnoob_syntax", "Wrong syntax! /antinoob addnoob <name/steamid>" },
                {"antinoobcmd_alreadynoob", "{0} is already marked as a noob" },
                {"antinoobcmd_marked", "Marked {0} as a noob" },
                {"antinoobcmd_removenoob_syntax", "Wrong syntax! /antinoob removenoob <name/steamid>" },
                {"antinoobcmd_removednoob", "{0} does not have a noob status anymore" },
                {"antinoobcmd_missingnoob", "{0} is not a noob" },
                {"antinoobcmd_wipe_syntax", "Missing argument! <all|playerdata|attempts>" },
                {"dataFileWiped", "Data file successfully wiped"},
                {"dataFileWiped_attempts", "Data file successfully wiped (raid attempts)"},
                {"dataFileWiped_playerdata", "Data file successfully wiped (player data)"},

                {"struct_noowner","Structure at {0} has no owner! OwnerID = {1} buildingID = {2}" },
                {"clan_lostnoob" , "Clan '{0}' lost their noob status because they tried to raid" },
                {"lost_teamsprotection" , "You lost your team noob protection for damaging a structure" },
                {"lost_clansprotection" , "You lost your clan noob protection for damaging a structure" },
                {"new_user_lostprotection" , "You lost your noob protection for damaging a structure" },
                {"console_lostnoobstatus", "{0} hasn't connected for {1} days so he lost his noob status (can be raided)"},
                {"console_notenough", "{0} doesn't have enough hours in game to be marked as a non-noob"},

                {"firstconnectionmessage", "You are a new player so your buildings are protected for first {0} hours of your time on server"},

                {"pt_notInstalled", "Playtime Tracker is not installed, will check again in 15 seconds!"},
                {"pt_detected", "Playtime Tracker detected"},
                {"C_notInstalled", "Clans is not installed, will check again in 15 seconds!"},
                {"C_detected", "Clans detected"},

                {"userinfo_nofound", "Failed to get playtime info for {0}! trying again in 300 seconds!"},
                {"userinfo_nofound_2nd_attempt", "Failed to get playtime info for {0}! Has been marked as non-noob!"},
                {"userinfo_found", "Successfully got playtime info for {0}!"},

                {"twig_can_attack", "This structure is Twig & is not raid protected. Please upgrade to Wood or higer for protection!"},
                {"can_attack", "This structure is not raid protected"},
                {"NotLooking", "You are not looking at a building/deployable"},

                {"refunditem_help", "Wrong Syntax! /refunditem add <you have to hold the item you want to add>\n/refunditem remove <you have to hold the item you want to remove>\n/refunditem list\n/refunditem clear\n/refunditem all <sets all raid tools as refundable>"},
                {"refunditem_needholditem", "You need to hold the item you want to add/remove from refund list"},
                {"refunditem_notexplosive", "This item is not an explosive"},
                {"refunditem_added", "Added '{0}' to list of items to refund"},
                {"refunditem_alreadyonlist", "This item is already on the list"},
                {"refunditem_notonlist", "This item is not on the list"},
                {"refunditem_removed", "Removed '{0}' from the list of items to refund"},
                {"refunditem_addedall", "Added all raid tools to refund list"},
                {"refunditem_cleared", "Cleared list of items to refund"},
                {"refunditem_empty", "There are no item set up yet"},
                {"refunditem_list", "List of items which will get refunded: \n{0}"},

                {"refund_free", "Your '{0}' was refunded."},
                {"refund_last", "Your '{0}' was refunded but will not be next time."},
                {"refund_1time", "Your '{0}' was refunded. After 1 more attempt it wont be refunded."},
                {"refund_nTimes", "Your '{0}' was refunded. After {1} more attempts it wont be refunded"},

                {"cannot_attack_no_time", "This entity cannot be destroyed because it was built by a new player"},
                {"cannot_attack_time", "This entity cannot be destroyed because it was built by a new player ({0})"},
                {"looking_item", "The entity name is {0}"},
                {"notlooking_item", "You need to look at a deployed item to get the name!"},
                {"holding_item", "The items name is {0}"},
                {"notholding_item", "You need to hold a Raiding Tool to get the name!"},
                {"Not_Supported", "This Raiding Tool is not supported for \"Weapon Settings\"!"},
                {"Increase_Refresh", "Increase \"User data refresh interval\" in the Config and reload! Can't be less or equal than 10 seconds." },

                {"secs", " seconds"},
                {"mins", " minutes"},
                {"min", " minute"},
                {"hours", " hours"},
                {"hour", " hour"},
                {"day", " day"},
                {"days", " days"}
            }, this);
        }

        #endregion

        #region DataFile/Classes

        private class BuildingInfo
        {
            public static HashSet<BuildingInfo> buildCache = new HashSet<BuildingInfo>();

            public BuildingInfo() { }

            public uint buildingID;

            public ulong OwnerID;

            public DateTime lastUpdate;

            public static BuildingInfo GetByBuildingID(uint bID)
            {
                foreach (var entry in buildCache)
                    if (entry.buildingID == bID) return entry;

                return null;
            }

            public double GetCacheAge()
            {
                if (lastUpdate == null) return -1;
                return DateTime.UtcNow.Subtract(lastUpdate).TotalSeconds;
            }
        }

        private class ClanInfo
        {
            public static HashSet<ClanInfo> clanCache = new HashSet<ClanInfo>();

            public ClanInfo() { }

            public static ClanInfo FindClanByName(string clanName)
            {
                foreach (var clan in ClanInfo.clanCache)
                    if (clan.clanName == clanName) return clan;

                return null;
            }

            public static ClanInfo GetClanOf(ulong playerID)
            {
                foreach (var clan in ClanInfo.clanCache)
                    if (clan.members.Contains(playerID)) return clan;

                return null;
            }

            public string clanName { get; set; }

            public List<ulong> members = new List<ulong>();
        }

        private class StoredData
        {
            public Dictionary<ulong, double> players = new Dictionary<ulong, double>();
            public Dictionary<ulong, int> AttackAttempts = new Dictionary<ulong, int>();
            public List<ulong> playersWithNoData = new List<ulong>();
            public List<ulong> FirstMessaged = new List<ulong>();
            public List<ulong> InTeam = new List<ulong>();
            public List<ulong> ShowTwigsNotProtected = new List<ulong>();
            public Dictionary<ulong, string> lastConnection = new Dictionary<ulong, string>();
            public List<ulong> IgnoredPlayers = new List<ulong>();

            public StoredData()
            {
            }
        }

        private class StoredDataItemList
        {
            public Dictionary<string, string> ItemList = new Dictionary<string, string>();

            public StoredDataItemList()
            {
            }
        }

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject($"{this.Name}\\{this.Name}_Data", storedData);
        private void SaveDataItemList() => Interface.Oxide.DataFileSystem.WriteObject($"{this.Name}\\{this.Name}_RefundItemList", storedDataItemList);


        StoredData storedData;
        StoredDataItemList storedDataItemList;

        #endregion

        #region Setup/Saving
        private void Init()
        {
            permission.RegisterPermission(AdminPerm, this);
            permission.RegisterPermission(NoobPerm, this);

            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>($"{this.Name}\\{this.Name}_Data");
            storedDataItemList = Interface.Oxide.DataFileSystem.ReadObject<StoredDataItemList>($"{this.Name}\\{this.Name}_RefundItemList");
            config = Config.ReadObject<ConfigFile>();
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnEntityKill));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnEntityTakeDamage));
            //if (config.AutoMinutes <= 0 && config.AutoAddProtection) PrintError("Time (minutes) after which the raided person gains raid protection is set to 0!!! Change this!!");
        }

        private void RegisterCommands()
        {
            foreach (var command in AdminCommands)
                AddCovalenceCommand(command.Key, command.Value, AdminPerm);
        }

        private void OnServerInitialized(bool isStartup) // Reminder use this hook instead of Loaded(). Thanks Nivex
        {
            foreach (var block in BaseEntity.saveList.OfType<BuildingBlock>()) _blocks.Add(block);

            Subscribe(nameof(OnEntityDeath));
            Subscribe(nameof(OnEntityKill));
            Subscribe(nameof(OnEntitySpawned));
            Subscribe(nameof(OnEntityTakeDamage));

            RegisterCommands();

            foreach (var entry in storedData.players.Where(x => !storedData.lastConnection.ContainsKey(x.Key)))
                storedData.lastConnection.Add(entry.Key, string.Empty);

            StartChecking();
            CheckPlayersWithNoInfo();

            if (!config.Advance.OnServerSave)
            {
                float args = config.Advance.SaveFrequency;
                int offset = (int) args;

                timer.Every(offset, SaveData);
            }

            if (beta) PrintWarning($"Debug Version: {betaversion}");

            if (config.Advance.Frequency <= 10)
            {
                timer.Every(15f, () =>
                {
                    PrintWarning(lang.GetMessage("Increase_Refresh", this, null));
                });
            }

            if (PlaytimeTracker == null)
            {
                PrintWarning(lang.GetMessage("pt_notInstalled", this, null));
                CheckingPT = timer.Every(15f, () =>
                {
                    if (PlaytimeTracker == null)
                    {
                        PrintWarning(lang.GetMessage("pt_notInstalled", this, null));
                    }
                    else
                    {
                        PrintWarning(lang.GetMessage("pt_detected", this, null));
                        timer.Destroy(ref CheckingPT);
                    }

                });
            }
            if (config.Relationship.CheckClan && Clans != null)
            {
                timer.Every(60f, RefreshClanCache);
            }
            if (config.Relationship.CheckClan && Clans == null)
            {
                PrintWarning(lang.GetMessage("C_notInstalled", this, null));
                CheckingC = timer.Every(15f, () =>
                {
                    if (Clans == null)
                    {
                        PrintWarning(lang.GetMessage("C_notInstalled", this, null));
                    }
                    else
                    {
                        PrintWarning(lang.GetMessage("C_detected", this, null));
                        timer.Destroy(ref CheckingC);
                    }

                });
            }
        }

        private void Unload()
        {
            LeaderLower.Clear();
            SaveData();
            SaveDataItemList();
        }

        private void OnServerSave()
        {
            if (config.Advance.OnServerSave) timer.Once(UnityEngine.Random.Range(5, 15), SaveData);
        }

        private void OnNewSave()
        {
            if (config.Other.MapWipe == true)
            {
                storedData.players.Clear();
                storedData.IgnoredPlayers.Clear();
                storedData.lastConnection.Clear();
                storedData.ShowTwigsNotProtected.Clear();
                storedData.InTeam.Clear();
                storedData.FirstMessaged.Clear();
                storedData.playersWithNoData.Clear();
                storedData.AttackAttempts.Clear();
                SaveData();
                PrintWarning("New savefile detected. All Player Data was cleared!");
            }
        }

        #endregion

        #region Commands
        private Dictionary<string, string> AdminCommands = new Dictionary<string, string>
        {
            { "antinoob", "AntiNoobCommand" },
            { "checkname", "CheckNameCmd" },
            { "refunditem", "RefundCmd" },
        };

        private void AntiNoobCommand(IPlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                player.Reply(lang.GetMessage("antinoobcmd_syntax", this, player.Id));
                return;
            }

            switch (args[0].ToLower())
            {
                case "addnoob":
                    {
                        if (args.Length < 2)
                        {
                            player.Reply(lang.GetMessage("antinoobcmd_addnoob_syntax", this, player.Id));
                            return;
                        }

                        var p = covalence.Players.FindPlayer(args[1]);
                        if (p == null)
                        {
                            player.Reply(lang.GetMessage("NoPlayerFound", this, player.Id), null, args[1]);
                            return;
                        }

                        foreach (var entry in storedData.players)
                        {
                            if (entry.Key == ulong.Parse(p.Id))
                            {
                                if (storedData.players[entry.Key] == -25d)
                                {
                                    player.Reply(lang.GetMessage("antinoobcmd_alreadynoob", this, player.Id), null, p.Name);
                                    return;
                                }

                                storedData.players[entry.Key] = -25d;
                                player.Reply(lang.GetMessage("antinoobcmd_marked", this, player.Id), null, p.Name);
                                return;
                            }
                        }
                        player.Reply(lang.GetMessage("NoPlayerFound", this, player.Id), null, args[1]);
                        return;
                    }

                case "removenoob":
                    {
                        if (args.Length < 2)
                        {
                            player.Reply(lang.GetMessage("antinoobcmd_removenoob_syntax", this, player.Id));
                            return;
                        }

                        var p = covalence.Players.FindPlayer(args[1]);
                        if (p == null)
                        {
                            player.Reply(lang.GetMessage("NoPlayerFound", this, player.Id), null, args[1]);
                        }

                        foreach (var entry in storedData.players)
                        {
                            if (entry.Key == ulong.Parse(p.Id))
                            {
                                if (storedData.players[entry.Key] == -50d)
                                {

                                    player.Reply(lang.GetMessage("antinoobcmd_missingnoob", this, player.Id), null, p.Name);
                                    return;
                                }

                                storedData.players[entry.Key] = -50d;
                                player.Reply(lang.GetMessage("antinoobcmd_removednoob", this, player.Id), null, p.Name);
                                return;
                            }
                        }

                        player.Reply(lang.GetMessage("NoPlayerFound", this, player.Id), null, args[1]);
                        return;
                    }

                case "wipe":
                    {
                        if (args.Length < 2)
                        {
                            player.Reply(lang.GetMessage("antinoobcmd_wipe_syntax", this, player.Id));
                            return;
                        }

                        switch (args[1].ToLower())
                        {
                            case "all":
                                {
                                    storedData.lastConnection.Clear();
                                    storedData.playersWithNoData.Clear();
                                    storedData.FirstMessaged.Clear();
                                    storedData.AttackAttempts.Clear();
                                    storedDataItemList.ItemList.Clear();
                                    storedData.players.Clear();
                                    player.Reply(lang.GetMessage("dataFileWiped", this, player.Id));
                                    return;
                                }

                            case "playerdata":
                                {
                                    storedData.players.Clear();
                                    player.Reply(lang.GetMessage("dataFileWiped_playerdata", this, player.Id));
                                    return;
                                }

                            case "attempts":
                                {
                                    storedData.AttackAttempts.Clear();
                                    player.Reply(lang.GetMessage("dataFileWiped_attempts", this, player.Id));
                                    return;
                                }

                            default:
                                {
                                    player.Reply(lang.GetMessage("antinoobcmd_wipe_syntax", this, player.Id));
                                    return;
                                }
                        }
                    }

                default:
                    {
                        player.Reply(lang.GetMessage("antinoobcmd_syntax", this, player.Id));
                        return;
                    }
            }
        }

        [ChatCommand("checknew")]
        private void CheckNewCmd(BasePlayer player, string command, string[] args)
        {
            BaseEntity hitEnt = GetLookAtEntity(player);
            if (hitEnt == null)
            {
                SendReply(player, lang.GetMessage("NotLooking", this, player.UserIDString));
                return;
            }

            if (config.Messages.ShowMessage && config.Other.IgnoreTwig && (hitEnt as BuildingBlock)?.grade == BuildingGrade.Enum.Twigs)
            {
                SendReply(player, lang.GetMessage("twig_can_attack", this, player.UserIDString));
                return;
            }

            ulong owner = config.Other.CheckFullOwnership ? FullOwner(hitEnt) : hitEnt.OwnerID;

            if (owner == 0u || !storedData.players.ContainsKey(owner)) return;
            MessagePlayer(player, owner);
        }

        [ChatCommand("entdebug")]
        private void EntDebugCmd(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin) return;

            BaseEntity ent = GetLookAtEntity(player);

            if (ent == null)
            {
                SendReply(player, "No entity");
                return;
            }

            var ownerid = (FullOwner(ent) != 0) ? FullOwner(ent) : ent.OwnerID;

            if (args.Length < 1)
            {
                SendReply(player, "OwnerID: " + ent.OwnerID);
                SendReply(player, "FullOwner: " + FullOwner(ent));

                if (storedData.players.ContainsKey(ownerid))
                {
                    var tiem = (int)storedData.players[ownerid];
                    switch (tiem)
                    {
                        case -25:
                            {
                                SendReply(player, "Time: -25d (manually set noob)");
                                break;
                            }

                        case -50:
                            {
                                SendReply(player, "Time: -50d (damaged structure, clan lost protection, inactive)");
                                break;
                            }

                        default:
                            {
                                SendReply(player, "Time: " + tiem + " ('natural' time)");
                                if (tiem >= config.Main.ProtectionTime) SendReply(player, "Should be raidable");
                                else SendReply(player, "Shouldn't be raidable");
                                break;
                            }
                    }
                }

                else SendReply(player, "StoredData does not contain info for " + ownerid);
                return;
            }

            var t = ulong.Parse(args[0]);
            ent.OwnerID = t;
            ent.SendNetworkUpdate();
            SendReply(player, "Set OwnerID: " + ent.OwnerID);
            return;
        }

        private void CheckNameCmd(IPlayer p, string command, string[] args)
        {
            BasePlayer player = p.Object as BasePlayer;
            BaseEntity hitEnt = GetLookAtEntity(player);
            Item helditem = player.GetActiveItem();

            if (player == null) return;

            if (args.Length < 1)
            {
                return;
            }

            switch (args[0].ToLower())
            {
                case "looking":
                    {
                        if (hitEnt == null)
                        {
                            p.Reply(lang.GetMessage("notlooking_item", this, p.Id));
                            return;
                        }

                        if (hitEnt != null)
                        {
                            p.Reply(lang.GetMessage("looking_item", this, player.UserIDString), null, hitEnt);
                            return;
                        }

                        return;
                    }

                case "holding":
                    {
                        if (helditem == null)
                        {
                            p.Reply(lang.GetMessage("notholding_item", this, p.Id));
                            return;
                        }

                        if (NotSupportedRaidTools.ContainsKey(helditem.info.shortname))
                        {
                            p.Reply(lang.GetMessage("Not_Supported", this, p.Id));
                            return;
                        }

                        if (RaidToolsCheck.ContainsKey(helditem.info.shortname))
                        {
                            p.Reply(lang.GetMessage("holding_item", this, player.UserIDString), null, RaidToolsCheck[helditem.info.shortname]);
                            return;
                        }
                        else

                        {
                            p.Reply(lang.GetMessage("notholding_item", this, p.Id));
                            return;
                        }
                    }
            }
        }

        private void RefundCmd(IPlayer p, string command, string[] args)
        {
            BasePlayer player = p.Object as BasePlayer;
            if (player == null) return;

            if (args.Length < 1)
            {
                p.Reply(lang.GetMessage("refunditem_help", this, p.Id));
                return;
            }

            Item helditem = player.GetActiveItem();

            switch (args[0].ToLower())
            {
                case "add":
                    {
                        if (player.GetActiveItem() == null)
                        {
                            p.Reply(lang.GetMessage("refunditem_needholditem", this, player.UserIDString));
                            return;
                        }

                        if (raidtools.ContainsKey(helditem.info.shortname))
                        {
                            if (!storedDataItemList.ItemList.ContainsKey(helditem.info.shortname))
                            {
                                storedDataItemList.ItemList.Add(helditem.info.shortname, raidtools[helditem.info.shortname]);
                                p.Reply(lang.GetMessage("refunditem_added", this, player.UserIDString), null, helditem.info.displayName.english);
                                return;
                            }

                            p.Reply(lang.GetMessage("refunditem_alreadyonlist", this, player.UserIDString));
                            return;
                        }

                        p.Reply(lang.GetMessage("refunditem_notexplosive", this, player.UserIDString));

                        SaveDataItemList();

                        return;
                    }

                case "remove":
                    {
                        if (player.GetActiveItem() == null)
                        {
                            p.Reply(lang.GetMessage("refunditem_needholditem", this, player.UserIDString));
                            return;
                        }

                        if (storedDataItemList.ItemList.ContainsKey(helditem.info.shortname))
                        {
                            storedDataItemList.ItemList.Remove(helditem.info.shortname);
                            p.Reply(lang.GetMessage("refunditem_removed", this, player.UserIDString), null, helditem.info.displayName.english);
                            return;
                        }

                        p.Reply(lang.GetMessage("refunditem_notonlist", this, player.UserIDString));

                        SaveDataItemList();

                        return;
                    }

                case "all":
                    {
                        foreach (var t in raidtools)

                            if (!storedDataItemList.ItemList.ContainsKey(t.Key)) storedDataItemList.ItemList.Add(t.Key, t.Value);

                        p.Reply(lang.GetMessage("refunditem_addedall", this, player.UserIDString));

                        SaveDataItemList();

                        return;
                    }

                case "clear":
                    {
                        storedDataItemList.ItemList.Clear();

                        p.Reply(lang.GetMessage("refunditem_cleared", this, player.UserIDString));

                        SaveDataItemList();

                        return;
                    }

                case "list":
                    {
                        if (storedDataItemList.ItemList.Count < 1)
                        {
                            p.Reply(lang.GetMessage("refunditem_empty", this, player.UserIDString));
                            return;
                        }

                        List<string> T2 = new List<string>();

                        foreach (var entry in storedDataItemList.ItemList)
                        {
                            Item item = ItemManager.CreateByName(entry.Key, 1);

                            if (item.info.displayName.english == null)
                                LogToFile("other", "Failed to find display name for " + entry.Key, this, true);
                            T2.Add(item?.info?.displayName?.english);
                        }

                        string final = string.Join("\n", T2.ToArray());
                        p.Reply(lang.GetMessage("refunditem_list", this, player.UserIDString), null, final);
                        return;
                    }

                default:
                    {
                        p.Reply(lang.GetMessage("refunditem_help", this, player.UserIDString));
                        return;
                    }
            }
        }

        #endregion

        #region Rust/uMod Hooks
        private object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo hitinfo)
        {
            var owner = config.Other.CheckFullOwnership ? FullOwner(entity) : entity.OwnerID;
            BasePlayer attacker = hitinfo.InitiatorPlayer;
            var dmgType = hitinfo?.damageTypes?.GetMajorityDamageType() ?? DamageType.Generic;
            string name = CheckForRaidingTools(hitinfo);

            if (hitinfo == null || entity == null || attacker == null || owner == 0) return null; //null checks

            if (!entity.OwnerID.IsSteamId() || !attacker.userID.IsSteamId()) return null;

            if (entity?.OwnerID == attacker?.userID) return null;

            if (dmgType == DamageType.Decay || dmgType == DamageType.Generic || dmgType == DamageType.Bite || dmgType == DamageType.Fun_Water) return null;

            if (entity.OwnerID == 0u || CheckForBuildingOrDeployable(entity, hitinfo) == false) return null;

            if (HasProtection(attacker) == true) return null;

            if (config.Other.IgnoreTwig && (entity as BuildingBlock)?.grade == BuildingGrade.Enum.Twigs) return null;

            if (CheckForHelicopterOrMLRS(entity, hitinfo) == true) return null;

            if (NullWeaponCheck(entity, hitinfo) == true) return null;

            if (config.RaidTools.AllowedRaidTools.ContainsKey(name))
            {
                if (PlayerIsNew(owner))
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                    hitinfo.HitMaterial = 0;
                    hitinfo.damageTypes.ScaleAll(0f);
                    NextTick(() => {
                        MessagePlayer(attacker, owner);
                        Refund(attacker, name, entity);

                    });
                    return true;
                }
                else
                {
                    return null;
                }
            }

            if (config.Entity.AllowedEntities.ContainsKey(entity.ShortPrefabName))
            {
                return null;
            }

            if (config.Entity.AllowedEntitiesNoob.ContainsKey(entity.ShortPrefabName))
            {
                if (PlayerIsNew(owner))
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                    hitinfo.HitMaterial = 0;
                    hitinfo.damageTypes.ScaleAll(0f);
                    NextTick(() =>
                    {
                        MessagePlayer(attacker, owner);
                        Refund(attacker, name, entity);
                    });
                    return true;
                }
                else
                {
                    return null;
                }
            }

            if (attacker == null || owner == 0u) return null;

            if (storedData.IgnoredPlayers.Contains(attacker.userID)) return null;

            if (owner == attacker?.userID) return null;

            bool wipe = false;
            try
            {
                if (WipeProtection != null) wipe = WipeProtection.Call<bool>("WipeProtected");
            }

            catch (Exception e)
            {
                PrintError("Caught an exception while trying to get data from WipeProtection: " + e.Message);
            }

            if (wipe) return null;

            if (config.Relationship.CheckTeam && CheckTeam(entity, hitinfo) == true)
            {
                return null;
            }
            if (config.Relationship.CheckTeam && CheckTeam(entity, hitinfo) == false)
            {
                if (!string.IsNullOrEmpty(name) && config.Main.UnNoobNew)
                {
                    RemoveTeamProtection(attacker, hitinfo);
                }
            }

            if (config.Relationship.CheckClan && CheckClan(entity, hitinfo) == true)
            {
                return null;
            }
            if (config.Relationship.CheckClan && CheckClan(entity, hitinfo) == false)
            {
                if (!string.IsNullOrEmpty(name) && config.Main.UnNoobNew)
                {
                    RemoveClanProtection(attacker, attacker?.userID ?? 0u);
                }
            }

            if (cooldown.Contains(attacker))
            {
                if (PlayerIsNew(owner))
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                    hitinfo.HitMaterial = 0;
                    hitinfo.damageTypes.ScaleAll(0f);
                    return true;
                }
                return null;
            }

            cooldown.Add(attacker);
            RemoveCD(cooldown, attacker);
            LogPlayer(attacker);

            RemoveProtection(attacker, hitinfo);

            if (PlayerIsNew(owner))
            {
                //keep in mind, antinoobraid.noob perm doesn't get removed
                hitinfo.damageTypes = new DamageTypeList();
                hitinfo.DoHitEffects = false;
                hitinfo.HitMaterial = 0;
                hitinfo.damageTypes.ScaleAll(0f);
                NextTick(() => {
                    //if player was *manually* set to noob we don't remove his protection on raid attempt
                    MessagePlayer(attacker, owner);
                    Refund(attacker, name, entity);

                });
                return true;
            }

            return null;
        }

        private void OnEntityBuilt(Planner plan, GameObject gameObject)
        {
            BaseEntity entity = gameObject.ToBaseEntity();
            var player = plan.GetOwnerPlayer();

            if ((entity as BuildingBlock)?.grade == BuildingGrade.Enum.Twigs)
            {
                if (config.Messages.ShowMessageTwig && config.Other.IgnoreTwig && !storedData.ShowTwigsNotProtected.Contains(player.userID))
                {
                    string msg = string.Format(lang.GetMessage("twig_can_attack", this, player.UserIDString));

                    storedData.ShowTwigsNotProtected.Add(player.userID);

                    if (config.Messages.UseGT)
                    {
                        player.SendConsoleCommand("gametip.showgametip", msg);
                        timer.Once(10f, () => player.SendConsoleCommand("gametip.hidegametip"));
                    }
                    else
                    {
                        SendReply(player, msg);
                    }
                }
            }
        }

        private void OnFireBallDamage(FireBall fireball, BaseCombatEntity entity, HitInfo hitinfo)
        {
            if (hitinfo == null || fireball == null || entity == null || !config.Other.KillFire) return;

            if (!(entity is BuildingBlock || entity is Door || entity.OwnerID != 0u || entity.PrefabName.Contains("deployable")) || fireball.IsDestroyed) return;

            if (config.Other.IgnoreTwig && (entity as BuildingBlock)?.grade == BuildingGrade.Enum.Twigs) return;

            BasePlayer attacker = hitinfo?.InitiatorPlayer;

            if (config.Entity.AllowedEntities.ContainsKey(entity.ShortPrefabName))
            {
                return;
            }

            if (entity?.OwnerID == attacker?.userID) return;

            if (attacker?.userID ==  null) return;

            if (PlayerIsNew(entity.OwnerID))
            {
                fireball.Kill();
                var player = fireball.creatorEntity as BasePlayer;
                if (player != null)
                {
                    if (!MessageCooldown.Contains(player))
                    {
                        MessagePlayer(player, entity.OwnerID);
                        MessageCooldown.Add(player);
                        RemoveCD(MessageCooldown, player);
                    }
                }

                return;
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason) => LastConnect(player.userID);

        private void OnEntitySpawned(BuildingBlock block) => _blocks.Add(block);

        private void OnEntityDeath(BuildingBlock block, HitInfo hitInfo) => _blocks.Remove(block);

        private void OnEntityKill(BuildingBlock block) => _blocks.Remove(block);

        private void OnUserConnected(IPlayer player)
        {
            BasePlayer bp = player.Object as BasePlayer;
            LastConnect(bp.userID);

            if (storedData.players.ContainsKey(bp.userID)) return;
            storedData.players.Add(bp.userID, 0d);
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player is BasePlayer && !player.IsNpc)
            {

                if (!player.userID.IsSteamId()) return;

                if (!config.Messages.MessageOnFirstConnection || storedData.FirstMessaged.Contains(player.userID)) return;
                storedData.FirstMessaged.Add(player.userID);

                var val = 0d;
                if (storedData.players.TryGetValue(player.userID, out val) && (val > 100d || val == -50d || val == -25d)) return;

                string msg = string.Format(lang.GetMessage("firstconnectionmessage", this, player.UserIDString), (config.Main.ProtectionTime / 3600d));
                if (config.Messages.UseGT)
                {
                    player.SendConsoleCommand("gametip.showgametip", msg);
                    timer.Once(10f, () => player.SendConsoleCommand("gametip.hidegametip"));
                }
                else
                {
                    SendReply(player, msg);
                }
            }
        }

        private void OnTeamCreated(BasePlayer player, RelationshipManager.PlayerTeam team)
        {
            if (config.Relationship.SyncTeamPlaytime)
            {
                if (storedData.InTeam.Contains(player.userID)) return;
                storedData.InTeam.Add(player.userID);
            }
        }

        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (config.Relationship.SyncTeamPlaytime)
            {
                timer.Once(2f, () =>
                {
                    storedData.InTeam.Add(player.userID);
                    SyncTeam(team, player.userID);
                });
            }
        }

        private void OnTeamKick(RelationshipManager.PlayerTeam team, ulong target)
        {
            if (config.Relationship.SyncTeamPlaytime)
            {
                if (storedData.InTeam.Contains(target)) storedData.InTeam.Remove(target);
            }
        }

        private void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (config.Relationship.SyncTeamPlaytime)
            {
                if (storedData.InTeam.Contains(player.userID)) storedData.InTeam.Remove(player.userID);
            }
        }

        private void OnTeamDisbanded(RelationshipManager.PlayerTeam team)
        {
            if (config.Relationship.SyncTeamPlaytime)
            {
                foreach (ulong member in team.members)
                {
                    if (storedData.InTeam.Contains(member)) storedData.InTeam.Remove(member);
                }
            }
        }

        #endregion

        #region API

        bool IgnorePlayer(object o)
        {
            ulong id = 0u;

            if (o is BasePlayer)
            {
                id = (o as BasePlayer).userID;
            }

            else if (ulong.TryParse(o.ToString(), out id))
            {
                //Do nothing I guess?
            }

            if (id == 0u) return false;

            if (!storedData.IgnoredPlayers.Contains(id))
            {
                storedData.IgnoredPlayers.Add(id);
                SaveData();
                return true;
            }

            return false;
        }

        bool UnIgnorePlayer(object o)
        {
            ulong id = 0u;

            if (o is BasePlayer)
            {
                id = (o as BasePlayer).userID;
            }

            else if (ulong.TryParse(o.ToString(), out id))
            {
                //Do nothing I guess?
            }

            if (id == 0u) return false;

            if (storedData.IgnoredPlayers.Contains(id))
            {
                storedData.IgnoredPlayers.Remove(id);
                SaveData();
                return true;
            }

            return false;
        }

        #endregion

        #region Clans
        private bool AllowOwnerCheckTimeClans(BasePlayer attacker, ulong ID)
        {
            var player = Convert.ToString(attacker.UserIDString);
            var owner = Convert.ToString(ID);
            var val = 0d;

            if (config.Messages.ShowOwnerTime && config.Relationship.CheckClan)
            {
                var ownerclanName = Clans.Call<string>("GetClanOf", player);

                var playerclanName = Clans.Call<string>("GetClanOf", owner);

                if (!string.IsNullOrEmpty(playerclanName) || !string.IsNullOrEmpty(ownerclanName))
                {
                    if (playerclanName == ownerclanName)
                    {
                        if (storedData.players.TryGetValue(ID, out val) && (val == -50d)) return false;
                        return true;
                    }
                }
            }
            return false;
        }

        private bool CheckClan(BaseCombatEntity entity, HitInfo hitinfo)
        {
            var name = CheckForRaidingTools(hitinfo);
            var owner = config.Other.CheckFullOwnership ? FullOwner(entity) : entity.OwnerID;
            BasePlayer attacker = hitinfo.InitiatorPlayer;


            ulong userID = attacker?.userID ?? 0u;
            var clan = ClanInfo.GetClanOf(userID) ?? null;
            if (clan == null)
            {
                var clanName = Clans?.Call<string>("GetClanOf", userID) ?? string.Empty;
                if (!string.IsNullOrEmpty(clanName))
                {
                    var members = GetClanMembers(clanName);
                    var claninfo = new ClanInfo { clanName = clanName, members = members };
                    ClanInfo.clanCache.Add(claninfo);

                    if (claninfo.members.Contains(owner)) return true;
                }
            }

            else if (clan.members.Contains(owner)) return true;

            return false;
        }

        private List<ulong> GetClanMembers(string clanName)
        {
            if (string.IsNullOrEmpty(clanName)) return new List<ulong>();

            var claninfo = ClanInfo.FindClanByName(clanName);
            if (claninfo != null) return claninfo.members;

            if (Clans == null) return new List<ulong>();

            List<ulong> IDlist = new List<ulong>();

            foreach (var p in covalence.Players.All)
            {
                string clan = Clans?.Call<string>("GetClanOf", p) ?? string.Empty;

                if (clan == clanName)
                    IDlist.Add(ulong.Parse(p.Id));
            }

            RefreshClanCache();
            return IDlist;
        }

        private void RefreshClanCache()
        {
            if (config.Relationship.CheckClan && Clans == null)
            {
                Puts(lang.GetMessage("C_notInstalled", this, null));
                return;
            }

            foreach (var player in covalence.Players.All.Where(x => !x.IsBanned))
            {
                ulong playerID = ulong.Parse(player.Id);
                var clanName = Clans.Call<string>("GetClanOf", playerID);
                if (!string.IsNullOrEmpty(clanName))
                {
                    if (!ClanInfo.clanCache.Select(x => x.clanName).Contains(clanName))
                    {
                        var clan = new ClanInfo { clanName = clanName, members = new List<ulong> { playerID } };
                        ClanInfo.clanCache.Add(clan);
                    }

                    else
                    {
                        foreach (var clan in ClanInfo.clanCache)
                        {
                            if (clanName == clan.clanName && !clan.members.Contains(playerID))
                                clan.members.Add(playerID);
                        }
                    }
                }
            }
        }

        private void RemoveClanProtection(BasePlayer attacker, ulong playerID)
        {
            if (playerID == 0u) return;

            var clanName = Clans.Call<string>("GetClanOf", playerID);
            if (string.IsNullOrEmpty(clanName)) return;

            var ClanMembers = ClanInfo.FindClanByName(clanName)?.members ?? new List<ulong>();
            if (ClanMembers.Count < 1)
            {
                return;
            }

            var list = storedData.players.Where(x => x.Value != -50d && ClanMembers.Contains(x.Key));
            if (list.Count() < 1) return;

            foreach (var member in list.ToList()) storedData.players[member.Key] = -50d;
            Puts(lang.GetMessage("clan_lostnoob", this, null), clanName);

            string msg = string.Format(lang.GetMessage("lost_clansprotection", this, attacker.UserIDString));
            if (config.Messages.UseGT)
            {
                attacker.SendConsoleCommand("gametip.showgametip", msg);
                timer.Once(10f, () => attacker.SendConsoleCommand("gametip.hidegametip"));
            }
            else
            {
                SendReply(attacker, msg);
            }

            if (config.Advance.EnableLogging) LogToFile("clanlostnoob", $"[{DateTime.Now}] - Clan '{clanName}' lost their noob status because {playerID} tried to raid", this, false);
        }

        #endregion

        #region Teams

        private bool AllowOwnerCheckTimeTeams(BasePlayer attacker, ulong ID)
        {
            var val = 0d;

            if (config.Messages.ShowOwnerTime && config.Relationship.CheckTeam)
            {
                var Instance = RelationshipManager.ServerInstance;

                BasePlayer ownerPlayer;
                if (Instance == null) PrintWarning("RelationshipManager instance is null! how is this even possible?");
                if (Instance.cachedPlayers.TryGetValue(ID, out ownerPlayer))
                {
                    if (ownerPlayer.currentTeam == attacker.currentTeam && ownerPlayer.currentTeam != 0)
                    {
                        if (storedData.players.TryGetValue(ID, out val) && (val == -50d)) return false;
                        return true;
                    }
                }
            }

            return false;
        }

        private bool CheckTeam(BaseCombatEntity entity, HitInfo hitinfo)
        {
            var owner = config.Other.CheckFullOwnership ? FullOwner(entity) : entity.OwnerID;
            BasePlayer attacker = hitinfo.InitiatorPlayer;
            var Instance = RelationshipManager.ServerInstance;

            //Check Team For Owner
            BasePlayer ownerPlayer;
            if (Instance == null) PrintWarning("RelationshipManager instance is null! how is this even possible?");
            else
            {
                if (Instance.cachedPlayers.TryGetValue(owner, out ownerPlayer))
                {
                    if (ownerPlayer.currentTeam == attacker.currentTeam && ownerPlayer.currentTeam != 0) return true;
                }
            }

            return false;
        }

        private void RemoveTeamProtection(BaseCombatEntity entity, HitInfo hitinfo)
        {
            var name = CheckForRaidingTools(hitinfo);
            var owner = config.Other.CheckFullOwnership ? FullOwner(entity) : entity.OwnerID;
            BasePlayer attacker = hitinfo.InitiatorPlayer;

            //Remove protection from whole team
            if (config.Main.UnNoobNew)
            {
                if (attacker.currentTeam != 0)
                {
                    if (storedData.players[attacker.userID] >= 0)
                    {
                        string msg = string.Format(lang.GetMessage("lost_teamsprotection", this, attacker.UserIDString));

                        if (config.Messages.UseGT)
                        {
                            attacker.SendConsoleCommand("gametip.showgametip", msg);
                            timer.Once(10f, () => attacker.SendConsoleCommand("gametip.hidegametip"));
                        }
                        else
                        {
                            SendReply(attacker, msg);
                        }

                        MessagePlayer(attacker, owner);
                        if (config.Advance.EnableLogging) LogToFile($"TeamLostNoob", $"[{DateTime.Now}] - {attacker} lost their team noob status.", this, false);
                    }
                    var team = RelationshipManager.ServerInstance?.FindPlayersTeam(attacker.userID);
                    foreach (var member in team.members) storedData.players[member] = -50d;
                }
            }
        }

        private void SyncTeam(RelationshipManager.PlayerTeam team, ulong ID)
        {
            foreach (ulong member in team.members)
            {
                var LeaderPlayTime = storedData.players[ID];
                var MemberPlaytime = storedData.players[member];

                if (LeaderPlayTime == MemberPlaytime)
                {
                    continue;
                }
                if (LeaderPlayTime > MemberPlaytime)
                {
                    if (MemberPlaytime == -50d)
                    {
                        WriteTeamData(ID, -50d);
                        SyncTeam(team, ID);
                        break;
                    }
                    WriteTeamData(member, LeaderPlayTime);
                    continue;
                }
                if (LeaderPlayTime < MemberPlaytime)
                {
                    if (LeaderPlayTime == -50d)
                    {
                        WriteTeamData(member, -50d);
                        SyncTeam(team, ID);
                        break;
                    }
                    WriteTeamData(ID, MemberPlaytime);
                    SyncTeam(team, ID);
                    break;
                }
            }
        }

        // For when Plugins create Teams without calling any Rust Hooks
        private void FailSafe()
        {
            var TeamManager = RelationshipManager.ServerInstance;

            foreach (var team in TeamManager.teams)
            {
                foreach (var member in team.Value.members)
                {

                    if (storedData.players.ContainsKey(member))
                    {
                        if (storedData.InTeam.Contains(member)) continue;
                        else storedData.InTeam.Add(member);
                    }
                    else
                    {
                        storedData.InTeam.Add(member);
                        storedData.players.Add(member, 0d);
                    }
                }
            }

            foreach (BasePlayer bp in BasePlayer.activePlayerList)
            {
                if (bp.currentTeam == 0)
                {
                    if (storedData.InTeam.Contains(bp.userID)) 
                    {
                        storedData.InTeam.Remove(bp.userID);
                    }
                }
            }

            foreach (BasePlayer bp in BasePlayer.sleepingPlayerList)
            {
                if (bp.currentTeam == 0)
                {
                    if (storedData.InTeam.Contains(bp.userID))
                    {
                        storedData.InTeam.Remove(bp.userID);
                    }
                }
            }
        }

        #endregion

        #region Start Protection

        bool HasProtection(BasePlayer player)
        {
            if (player == null) return false;

            if (StartProtection != null && StartProtection.IsLoaded)
            {
                if (StartProtection.Call<bool>("HasProtection", player))
                {
                    return true;
                }
            }
            return false;
        }

        #endregion

        #region Playtime Checks

        private void APICall(ulong ID)
        {
            if (!ID.IsSteamId()) return;
            if (PlaytimeTracker == null)
            {
                Puts(lang.GetMessage("pt_notInstalled", this, null));
                return;
            }

            double apitime = -1;

            try
            {
                apitime = PlaytimeTracker?.Call<double>("GetPlayTime", ID.ToString()) ?? -1d;
            }

            catch (Exception)
            {
                Puts(lang.GetMessage("userinfo_nofound", this, null), ID);
                if (config.Advance.EnableLogging) LogToFile("playtimecollection", $"[{DateTime.Now}] - Failed to get playtime info for {ID}", this, false);
                storedData.playersWithNoData.Add(ID);
                timer.Once(300f, () => APICall_SecondAttempt(ID));
            }

            if (apitime != -1d)
            {
                var val = 0d;
                if (storedData.players.ContainsKey(ID))
                {
                    if (storedData.players.TryGetValue(ID, out val) && (val >= apitime)) return;
                    storedData.players[ID] = apitime;
                }
                else
                {
                    storedData.players.Add(ID, apitime);
                    Puts(lang.GetMessage("userinfo_found", this, null), ID);
                    if (config.Advance.EnableLogging) LogToFile("playtimecollection", $"[{DateTime.Now}] - Successfully got playtime info for {ID}", this, false);
                }
            }
        }

        private void APICall_SecondAttempt(ulong ID)
        {
            double apitime = -1;

            try
            {
                apitime = PlaytimeTracker?.Call<double>("GetPlayTime", ID.ToString()) ?? -1d;
            }

            catch (Exception)
            {
                Puts(lang.GetMessage("userinfo_nofound_2nd_attempt", this, null), ID);
                if (config.Advance.EnableLogging) LogToFile("playtimecollection", $"[{DateTime.Now}] - Failed to get playtime info for {ID}. Has been marked as non-noob", this, false);

                if (storedData.players.ContainsKey(ID))
                {
                    storedData.players[ID] = -50d;
                    if (storedData.playersWithNoData.Contains(ID)) storedData.playersWithNoData.Remove(ID);
                    if (storedData.FirstMessaged.Contains(ID) == false) storedData.FirstMessaged.Add(ID);
                    if (storedData.ShowTwigsNotProtected.Contains(ID) == false) storedData.ShowTwigsNotProtected.Add(ID);

                    return;
                }
                else
                {
                    storedData.players.Add(ID, -50d);
                    if (storedData.playersWithNoData.Contains(ID)) storedData.playersWithNoData.Remove(ID);
                    if (storedData.FirstMessaged.Contains(ID) == false) storedData.FirstMessaged.Add(ID);
                    if (storedData.ShowTwigsNotProtected.Contains(ID) == false) storedData.ShowTwigsNotProtected.Add(ID);
                }
            }

            if (apitime != -1d)
            {
                if (storedData.players.ContainsKey(ID)) storedData.players[ID] = apitime;
                else
                {
                    storedData.players.Add(ID, apitime);
                    Puts(lang.GetMessage("userinfo_found", this, null), ID);
                    if (config.Advance.EnableLogging) LogToFile("playtimecollection", $"[{DateTime.Now}] - Successfully got playtime info for {ID}", this, false);
                }
            }
        }

        private void APICall_TeamSync(ulong ID)
        {
            if (!ID.IsSteamId()) return;
            if (PlaytimeTracker == null)
            {
                Puts(lang.GetMessage("pt_notInstalled", this, null));
                return;
            }

            var team = RelationshipManager.ServerInstance?.FindPlayersTeam(ID);

            foreach (ulong member in team.members)
            {
                double apitime = PlaytimeTracker?.Call<double>("GetPlayTime", ID.ToString()) ?? -1d;
                double apitime_member = PlaytimeTracker?.Call<double>("GetPlayTime", member.ToString()) ?? -1d;

                APICall_TeamSync_Compare(ID, member, apitime, apitime_member);
            }

            APICall_TeamSync_Final(ID);
        }

        private void APICall_TeamSync_Compare(ulong ID, ulong member, double apitime, double apitime_member)
        {
            if (ID == member)
            {
                WriteTeamData(ID, apitime);
                return;
            }
            if (apitime == apitime_member)
            {
                WriteTeamData(ID, apitime);
                WriteTeamData(member, apitime);
                return;
            }
            if (apitime > apitime_member)
            {
                WriteTeamData(ID, apitime);
                WriteTeamData(member, apitime);
                return;
            }
            if (apitime < apitime_member)
            {
                WriteTeamData(ID, apitime_member);
                WriteTeamData(member, apitime_member);

                if (LeaderLower.Contains(ID))
                {
                    return;
                }

                LeaderLower.Add(ID);
                return;
            }
        }

        private void APICall_TeamSync_Final(ulong ID)
        {
            var team = RelationshipManager.ServerInstance?.FindPlayersTeam(ID);

            if (LeaderLower.Contains(ID))
            {
                LeaderLower.Remove(ID);
                SyncTeam(team, ID);

            }
            SyncTeam(team, ID);
            return;
        }

        private void WriteTeamData(ulong ID, double apitime)
        {
            if (ID == 0) return;
            storedData.players[ID] = apitime;
        }

        private void Check()
        {
            if (BasePlayer.activePlayerList.Count == 0) return;

            foreach (BasePlayer bp in BasePlayer.activePlayerList)
            {
                if (!bp.IsConnected || bp == null) continue;
                if (!bp.userID.IsSteamId()) continue;
                if (storedData.playersWithNoData.Contains(bp.userID)) continue;

                var val = 0d;

                if (storedData.players.TryGetValue(bp.userID, out val) && (val == -50d || val == -25d)) continue;
                if (config.Relationship.SyncTeamPlaytime && storedData.InTeam.Contains(bp.userID))
                {
                    APICall_TeamSync(bp.userID);
                    continue;
                }
                APICall(bp.userID);
            }

            foreach (BasePlayer bp in BasePlayer.sleepingPlayerList)
            {
                if (bp == null) continue;
                if (!bp.userID.IsSteamId()) continue;
                if (storedData.playersWithNoData.Contains(bp.userID)) continue;

                var val = 0d;

                if (storedData.players.TryGetValue(bp.userID, out val) && (val == -50d || val == -25d)) continue;
                if (config.Relationship.SyncTeamPlaytime && storedData.InTeam.Contains(bp.userID))
                {
                    APICall_TeamSync(bp.userID);
                    continue;
                }

                APICall(bp.userID);
            }
        }

        private void Check(ulong ID)
        {
            if (storedData.playersWithNoData.Contains(ID)) return;
            if (storedData.players[ID] == -50d || storedData.players[ID] == -25d) return;
            if (config.Relationship.SyncTeamPlaytime && storedData.InTeam.Contains(ID))
            {
                APICall_TeamSync(ID);
                return;
            }
            APICall(ID);
        }

        #endregion

        #region Weapon Checks

        private string CheckForRaidingTools(HitInfo hitinfo)
        {
            string name = string.Empty;

            if (hitinfo?.WeaponPrefab?.ShortPrefabName == "rocket_fire"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "rocket_heli_napalm"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "rocket_heli"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "rocket_mlrs"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "rocket_hv"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "rocket_basic"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "rocket_smoke"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "explosive.timed.deployed"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "survey_charge.deployed"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "explosive.satchel.deployed"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "grenade.beancan.deployed"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "grenade.f1.deployed"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "40mm_grenade_he"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "flamethrower.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "spear_stone.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "spear_wooden.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "knife_bone.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "bone_club.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "butcherknife.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "machete.weapon"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "knife.combat.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "longsword.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "mace.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "paddle.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "pitchfork.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "salvaged_cleaver.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "salvaged_sword.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "hatchet.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "pickaxe.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "axe_salvaged.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "hammer_salvaged.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "icepick_salvaged.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "stonehatchet.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "stone_pickaxe.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "rock.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "skull.entity"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "snowballgunbullet"
            || hitinfo?.WeaponPrefab?.ShortPrefabName == "jackhammer.entity")
            {
                name = hitinfo?.WeaponPrefab?.ShortPrefabName;
            }
            else
            {
                name = hitinfo?.ProjectilePrefab?.name.ToString();
            }

            return name;
        }

        private bool NullWeaponCheck(BaseCombatEntity entity, HitInfo hitinfo)
        {
            var name = CheckForRaidingTools(hitinfo);
            var owner = config.Other.CheckFullOwnership ? FullOwner(entity) : entity.OwnerID;
            BasePlayer attacker = hitinfo.InitiatorPlayer;

            if (config.Entity.AllowedEntities.ContainsKey(entity.ShortPrefabName))
            {
                if (name == null && owner != 0u && attacker != null)
                {
                    if (config.Relationship.CheckTeam && CheckTeam(entity, hitinfo) == false)
                    {
                        if (PlayerIsNew(owner))
                        {
                            hitinfo.damageTypes = new DamageTypeList();
                            hitinfo.DoHitEffects = false;
                            hitinfo.HitMaterial = 0;
                            hitinfo.damageTypes.ScaleAll(0f);
                            NextTick(() =>
                            {
                                RemoveProtection(entity, hitinfo);
                                MessagePlayer(attacker, owner);
                                Refund(attacker, name, entity);
                            });
                            return true;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else if (config.Relationship.CheckTeam && CheckTeam(entity, hitinfo) == true)
                    {
                        return true;
                    }
                    else if (config.Relationship.CheckClan && CheckClan(entity, hitinfo) == false)
                    {
                        if (PlayerIsNew(owner))
                        {
                            hitinfo.damageTypes = new DamageTypeList();
                            hitinfo.DoHitEffects = false;
                            hitinfo.HitMaterial = 0;
                            hitinfo.damageTypes.ScaleAll(0f);
                            NextTick(() =>
                            {
                                RemoveProtection(entity, hitinfo);
                                MessagePlayer(attacker, owner);
                                Refund(attacker, name, entity);
                            });
                            return true;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else if (config.Relationship.CheckClan && CheckClan(entity, hitinfo) == true)
                    {
                        return true;
                    }
                    else if (PlayerIsNew(owner))
                    {
                        hitinfo.damageTypes = new DamageTypeList();
                        hitinfo.DoHitEffects = false;
                        hitinfo.HitMaterial = 0;
                        hitinfo.damageTypes.ScaleAll(0f);
                        return true;
                    }
                    else
                    {
                        return true;
                    }
                }

                if (name == null && owner != 0u && attacker == null)
                {
                    if (PlayerIsNew(owner))
                    {
                        hitinfo.damageTypes = new DamageTypeList();
                        hitinfo.DoHitEffects = false;
                        hitinfo.HitMaterial = 0;
                        hitinfo.damageTypes.ScaleAll(0f);
                        return true;
                    }
                    else
                    {
                        return true;
                    }
                }

                return false;
            }

            if (config.Entity.AllowedEntitiesNoob.ContainsKey(entity.ShortPrefabName))
            {
                if (name == null && owner != 0u && attacker != null)
                {
                    if (config.Relationship.CheckTeam && CheckTeam(entity, hitinfo) == false)
                    {
                        if (PlayerIsNew(owner))
                        {
                            hitinfo.damageTypes = new DamageTypeList();
                            hitinfo.DoHitEffects = false;
                            hitinfo.HitMaterial = 0;
                            hitinfo.damageTypes.ScaleAll(0f);
                            NextTick(() =>
                            {
                                RemoveProtection(entity, hitinfo);
                                MessagePlayer(attacker, owner);
                                Refund(attacker, name, entity);
                            });
                            return true;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else if (config.Relationship.CheckTeam && CheckTeam(entity, hitinfo) == true)
                    {
                        return true;
                    }
                    else if (config.Relationship.CheckClan && CheckClan(entity, hitinfo) == false)
                    {
                        if (PlayerIsNew(owner))
                        {
                            hitinfo.damageTypes = new DamageTypeList();
                            hitinfo.DoHitEffects = false;
                            hitinfo.HitMaterial = 0;
                            hitinfo.damageTypes.ScaleAll(0f);
                            NextTick(() =>
                            {
                                RemoveProtection(entity, hitinfo);
                                MessagePlayer(attacker, owner);
                                Refund(attacker, name, entity);
                            });
                            return true;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else if (config.Relationship.CheckClan && CheckClan(entity, hitinfo) == true)
                    {
                        return true;
                    }
                    else if (PlayerIsNew(owner))
                    {
                        hitinfo.damageTypes = new DamageTypeList();
                        hitinfo.DoHitEffects = false;
                        hitinfo.HitMaterial = 0;
                        hitinfo.damageTypes.ScaleAll(0f);
                        return true;
                    }
                    else
                    {
                        return true;
                    }
                }

                if (name == null && owner != 0u && attacker == null)
                {
                    if (PlayerIsNew(owner))
                    {
                        hitinfo.damageTypes = new DamageTypeList();
                        hitinfo.DoHitEffects = false;
                        hitinfo.HitMaterial = 0;
                        hitinfo.damageTypes.ScaleAll(0f);
                        return true;
                    }
                    else
                    {
                        return true;
                    }
                }

                return false;
            }

            if (name == null && owner != 0u && attacker != null)
            {
                if (config.Relationship.CheckTeam && CheckTeam(entity, hitinfo) == false)
                {
                    if (PlayerIsNew(owner))
                    {
                        hitinfo.damageTypes = new DamageTypeList();
                        hitinfo.DoHitEffects = false;
                        hitinfo.HitMaterial = 0;
                        hitinfo.damageTypes.ScaleAll(0f);
                        NextTick(() =>
                        {
                            RemoveProtection(entity, hitinfo);
                            MessagePlayer(attacker, owner);
                            Refund(attacker, name, entity);
                        });
                        return true;
                    }
                    else
                    {
                        return true;
                    }
                }
                else if (config.Relationship.CheckTeam && CheckTeam(entity, hitinfo) == true)
                {
                    return true;
                }
                else if (config.Relationship.CheckClan && CheckClan(entity, hitinfo) == false)
                {
                    if (PlayerIsNew(owner))
                    {
                        hitinfo.damageTypes = new DamageTypeList();
                        hitinfo.DoHitEffects = false;
                        hitinfo.HitMaterial = 0;
                        hitinfo.damageTypes.ScaleAll(0f);
                        NextTick(() =>
                        {
                            RemoveProtection(entity, hitinfo);
                            MessagePlayer(attacker, owner);
                            Refund(attacker, name, entity);
                        });
                        return true;
                    }
                    else
                    {
                        return true;
                    }
                }
                else if (config.Relationship.CheckClan && CheckClan(entity, hitinfo) == true)
                {
                    return true;
                }
                else if (PlayerIsNew(owner))
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                    hitinfo.HitMaterial = 0;
                    hitinfo.damageTypes.ScaleAll(0f);
                    return true;
                }
                else
                {
                    return true;
                }
            }

            if (name == null && owner != 0u && attacker == null)
            {
                if (PlayerIsNew(owner))
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                    hitinfo.HitMaterial = 0;
                    hitinfo.damageTypes.ScaleAll(0f);
                    return true;
                }
                else
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Helper Methods

        private bool AllowOwnerCheckTime(BasePlayer attacker, ulong ID)
        {
            var player = Convert.ToString(attacker.UserIDString);
            var owner = Convert.ToString(ID);
            var val = 0d;

            if (config.Messages.ShowOwnerTime)
            {
                if (player == owner)
                {
                    if (storedData.players.TryGetValue(ID, out val) && (val == -50d)) return false;
                    return true;
                }
            }

            return false;
        }

        private bool CheckForBuildingOrDeployable(BaseCombatEntity entity, HitInfo hitinfo)
        {
            var dmgType = hitinfo?.damageTypes?.GetMajorityDamageType() ?? DamageType.Generic;
            var owner = config.Other.CheckFullOwnership ? FullOwner(entity) : entity.OwnerID;
            BasePlayer attacker = hitinfo.InitiatorPlayer;

            if (dmgType == DamageType.Decay || dmgType == DamageType.Generic || dmgType == DamageType.Bite) return false;

            if (owner == 0u) return false;

            if (entity is BuildingBlock || entity is Door) return true;
            if (entity.PrefabName.Contains("deployable")) return true;
            if (entity.PrefabName.Contains("window")) return true;
            if (entity.PrefabName.Contains("grill")) return true;
            if (entity.PrefabName.Contains("turret")) return true;
            if (entity.PrefabName.Contains("wall.external")) return true;
            if (entity.PrefabName.Contains("solarpanel")) return true;
            if (entity.PrefabName.Contains("electrical")) return true;
            if (entity.PrefabName.Contains("furnace")) return true;
            if (entity.PrefabName.Contains("box")) return true;
            if (entity.PrefabName.Contains("frame.cell")) return true;
            if (entity.PrefabName.Contains("shutter")) return true;
            if (entity.PrefabName.Contains("ladder")) return true;
            if (entity.PrefabName.Contains("water_catcher")) return true;
            if (entity.PrefabName.Contains("sign")) return true;
            if (entity.PrefabName.Contains("switch")) return true;
            if (entity.PrefabName.Contains("smart")) return true;
            if (entity.PrefabName.Contains("netting")) return true;
            if (entity.PrefabName.Contains("fence")) return true;
            if (entity.PrefabName.Contains("generator")) return true;
            if (entity.PrefabName.Contains("watchtower")) return true;
            if (entity.PrefabName.Contains("landmine")) return true;

            return false;
        }

        private bool CheckForHelicopterOrMLRS(BaseCombatEntity entity, HitInfo hitinfo)
        {
            var name = CheckForRaidingTools(hitinfo);
            var owner = config.Other.CheckFullOwnership ? FullOwner(entity) : entity.OwnerID;
            BasePlayer attacker = hitinfo.InitiatorPlayer;
            var dmgType = hitinfo?.damageTypes?.GetMajorityDamageType() ?? DamageType.Generic;

            if (name != null && owner != 0u && attacker != null && dmgType != DamageType.Bullet) return false;

            // Patrol Helicopter
            if (name == "rocket_heli" && owner != 0u && attacker == null && dmgType == DamageType.Explosion)
                {
                    if (config.Other.PatrolHeliDamage)
                    {
                        return true;
                    }
                    else if (!config.Other.PatrolHeliDamage)
                    {
                        if (PlayerIsNew(owner))
                        {
                            hitinfo.damageTypes = new DamageTypeList();
                            hitinfo.DoHitEffects = false;
                            hitinfo.HitMaterial = 0;
                            hitinfo.damageTypes.ScaleAll(0f);
                            return true;
                        }
                    }

                    return false;
                }

            if (name == "rocket_heli_napalm" && owner != 0u && attacker == null && dmgType == DamageType.Heat)
                {
                    if (config.Other.PatrolHeliDamage)
                    {
                        return true;
                    }
                    else if (!config.Other.PatrolHeliDamage)
                    {
                        if (PlayerIsNew(owner))
                        {
                            hitinfo.damageTypes = new DamageTypeList();
                            hitinfo.DoHitEffects = false;
                            hitinfo.HitMaterial = 0;
                            hitinfo.damageTypes.ScaleAll(0f);
                            return true;
                        }
                    }

                    return false;
                }

            if (name == null && owner != 0u && attacker == null && dmgType == DamageType.Bullet)
                {
                    if (config.Other.PatrolHeliDamage)
                    {
                        return true;
                    }
                    else if (!config.Other.PatrolHeliDamage)
                    {
                        if (PlayerIsNew(owner))
                        {
                            hitinfo.damageTypes = new DamageTypeList();
                            hitinfo.DoHitEffects = false;
                            hitinfo.HitMaterial = 0;
                            hitinfo.damageTypes.ScaleAll(0f);
                            return true;
                        }
                    }

                    return false;
                }

            // MLRS Damage Fix to Noob Structures
            if (attacker == null && name == "rocket_mlrs")
            {
                if (PlayerIsNew(owner))
                {
                    hitinfo.damageTypes = new DamageTypeList();
                    hitinfo.DoHitEffects = false;
                    hitinfo.HitMaterial = 0;
                    hitinfo.damageTypes.ScaleAll(0f);
                    return true;
                }

                return false;
            }
            return false;
        }

        private string CheckLeft(int intsecs)
        {
            var time = DateTime.Now.AddSeconds(intsecs - 1);
            var timespan = time.Subtract(DateTime.Now);

            string t = string.Empty;
            if (timespan.Days != 0) t += timespan.Days + "d ";
            if (timespan.Hours != 0) t += timespan.Hours + "h ";
            if (timespan.Minutes != 0) t += timespan.Minutes + "m ";
            if (timespan.Seconds != 0) t += timespan.Seconds + "s";
            if (t.Last() == ' ')
            {
                string nw = t.Remove(t.Length - 1);
                return nw;
            }

            return t;
        }

        private void CheckPlayersWithNoInfo()
        {
            int rate = (config.Advance.Frequency <= 10) ? 10 : config.Advance.Frequency - 10;

            timer.Every(rate, () =>
            {
                if (storedData.playersWithNoData.Count < 1) return;

                foreach (ulong ID in storedData.playersWithNoData.ToList())
                {
                    double time = -1d;
                    try
                    {
                        time = PlaytimeTracker?.Call<double>("GetPlayTime", ID.ToString()) ?? -1d;
                    }

                    catch (Exception)
                    {
                        continue;
                    }

                    if (time == -1d) continue;

                    if (storedData.players.ContainsKey(ID))
                    {
                        string date = "[" + DateTime.Now.ToString() + "] ";
                        Puts(lang.GetMessage("userinfo_found", this, null), ID);
                        if (config.Advance.EnableLogging) LogToFile("playtimecollection", $"[{DateTime.Now}] - Successfully got playtime info for {ID}", this, false);
                        storedData.players[ID] = time;
                        storedData.playersWithNoData.Remove(ID);
                        continue;
                    }

                    storedData.players.Add(ID, time);
                    Puts(lang.GetMessage("userinfo_found", this, null), ID);
                    if (config.Advance.EnableLogging) LogToFile("playtimecollection", $"[{DateTime.Now}] - Successfully got playtime info for {ID}", this, false);
                    storedData.playersWithNoData.Remove(ID);
                    continue;
                }
            });
        }

        private ulong FullOwner(BaseEntity ent, BasePlayer p = null)
        {
            if (ent == null) return 0u;

            var block = ent as BuildingBlock;
            if (block == null) return ent.OwnerID;

            var cached = BuildingInfo.GetByBuildingID(block.buildingID);

            if (cached != null)
            {
                if (cached.GetCacheAge() < 180 && cached.GetCacheAge() != -1) return cached.OwnerID;
            }

            var ownership = new Dictionary<ulong, int>();

            _blocks.RemoveAll(x => x == null || x.IsDestroyed || x.OwnerID == 0u);

            foreach (var x in _blocks) // no more expensive LINQ
            {
                if (x.buildingID != block.buildingID || config.Other.IgnoreTwig && x.grade == BuildingGrade.Enum.Twigs)
                {
                    continue;
                }

                if (!ownership.ContainsKey(x.OwnerID)) ownership[x.OwnerID] = 1;
                else ownership[x.OwnerID]++;
            }

            if (ownership.Count == 0)
            {
                //Should this even happen?
                if (config.Advance.ShowNoOwnerBase) PrintWarning(lang.GetMessage("struct_noowner", this, null), ent.transform.position, ent.OwnerID, block.buildingID);
                if (config.Advance.EnableLogging) LogToFile("other", $"[{DateTime.Now}] - Structure at {ent.transform.position} has no owner", this, false);

                if (cached != null)
                {
                    cached.OwnerID = 0;
                    cached.lastUpdate = DateTime.UtcNow;
                }
                else
                {
                    BuildingInfo.buildCache.Add(new BuildingInfo { buildingID = block.buildingID, lastUpdate = DateTime.UtcNow, OwnerID = 0 });
                }

                return ent.OwnerID;
            }

            var owner = ownership.Max(x => x.Key);

            if (cached != null)
            {
                cached.OwnerID = owner;
                cached.lastUpdate = DateTime.UtcNow;
            }

            else BuildingInfo.buildCache.Add(new BuildingInfo { buildingID = block.buildingID, lastUpdate = DateTime.UtcNow, OwnerID = owner });

            return owner;
        }

        private BaseEntity GetLookAtEntity(BasePlayer player, float maxDist = 10f)
        {
            if (player == null || player.IsDead()) return null;
            RaycastHit hit;
            var currentRot = Quaternion.Euler(player?.serverInput?.current?.aimAngles ?? Vector3.zero) * Vector3.forward;
            var ray = new Ray((player?.eyes?.position ?? Vector3.zero), currentRot);
            if (Physics.Raycast(ray, out hit, maxDist, layers))
            {
                var ent = hit.GetEntity() ?? null;
                if (ent != null && !(ent?.IsDestroyed ?? true)) return ent;
            }

            return null;
        }

        private void LastConnect(ulong ID) => storedData.lastConnection[ID] = DateTime.Now.ToString();

        private void LogPlayer(BasePlayer attacker)
        {
            if (attacker == null) return;
            var val = 0;
            if (!storedData.AttackAttempts.TryGetValue(attacker.userID, out val)) storedData.AttackAttempts[attacker.userID] = 1;
            else storedData.AttackAttempts[attacker.userID]++;
        }

        private void MessagePlayer(BasePlayer attacker, ulong ID)
        {
            if (!storedData.players.ContainsKey(ID)) return;
            double time2 = storedData.players[ID];
            int left = (int)(config.Main.ProtectionTime - time2);

            if (config.Messages.ShowMessage)
            {
                if (config.Messages.ShowOwnerTime && AllowOwnerCheckTime(attacker, ID))
                {
                    if (PlayerIsNew(ID))
                    {
                        SendReply(attacker, lang.GetMessage("cannot_attack_time", this, attacker.UserIDString), CheckLeft(left));
                        return;
                    }
                }

                else if (config.Messages.ShowOwnerTime && AllowOwnerCheckTimeClans(attacker, ID))
                {
                    if (PlayerIsNew(ID))
                    {
                        SendReply(attacker, lang.GetMessage("cannot_attack_time", this, attacker.UserIDString), CheckLeft(left));
                        return;
                    }
                }

                else if (config.Messages.ShowOwnerTime && AllowOwnerCheckTimeTeams(attacker, ID))
                {
                    if (PlayerIsNew(ID))
                    {
                        SendReply(attacker, lang.GetMessage("cannot_attack_time", this, attacker.UserIDString), CheckLeft(left));
                        return;
                    }
                }

                else if (PlayerIsNew(ID))
{
                    if (config.Messages.ShowTime)
                    {
                        SendReply(attacker, lang.GetMessage("cannot_attack_time", this, attacker.UserIDString), CheckLeft(left));
                        return;
                    }

                    SendReply(attacker, lang.GetMessage("cannot_attack_no_time", this, attacker.UserIDString));
                    return;
                }
                
                else
                SendReply(attacker, lang.GetMessage("can_attack", this, attacker.UserIDString));
            }
        }

        private bool PlayerIsNew(ulong ID)
        {
            if (permission.UserHasPermission(ID.ToString(), NoobPerm)) return true;
            var outDouble = 0d;
            if (!storedData.players.TryGetValue(ID, out outDouble) || outDouble == -50d) return false;
            if (outDouble < config.Main.ProtectionTime || outDouble == -25d) return true;
            return false;
        }

        private void RemoveCD(List<BasePlayer> List, BasePlayer player)
        {
            if (player == null) return;
            timer.Once(10, () =>
            {
                if (List.Contains(player)) List.Remove(player);
            });
        }

        private void RemoveProtection(BaseCombatEntity entity, HitInfo hitinfo)
        {
            BasePlayer attacker = hitinfo.InitiatorPlayer;

            if (config.Main.UnNoobNew)
            {
                if (storedData.players[attacker.userID] >= 0)
                {
                    storedData.players[attacker.userID] = -50d;

                    string msg = string.Format(lang.GetMessage("new_user_lostprotection", this, attacker.UserIDString));

                    if (config.Messages.UseGT)
                    {
                        attacker.SendConsoleCommand("gametip.showgametip", msg);
                        timer.Once(10f, () => attacker.SendConsoleCommand("gametip.hidegametip"));
                    }
                    else
                    {
                        SendReply(attacker, msg);
                    }

                    if (config.Advance.EnableLogging) LogToFile("damagedstructure", $"[{DateTime.Now}] - {attacker.userID} lost there noob protection for damaging {entity.OwnerID} structure", this, false);
                }
            }

            if (config.Main.UnNoobManual)
            {
                if (storedData.players[attacker.userID] == -25d)
                {
                    storedData.players[attacker.userID] = -50d;

                    string msg = string.Format(lang.GetMessage("new_user_lostprotection", this, attacker.UserIDString));

                    if (config.Messages.UseGT)
                    {
                        attacker.SendConsoleCommand("gametip.showgametip", msg);
                        timer.Once(10f, () => attacker.SendConsoleCommand("gametip.hidegametip"));
                    }
                    else
                    {
                        SendReply(attacker, msg);
                    }

                    if (config.Advance.EnableLogging) LogToFile("damagedstructure", $"[{DateTime.Now}] - {attacker.userID} lost there noob protection for damaging {entity.OwnerID} structure", this, false);
                }
            }
        }

        private void RemoveInactive()
        {
            foreach (var entry in storedData.lastConnection)
            {
                var val = 0d;
                if (string.IsNullOrEmpty(entry.Value) || !storedData.players.TryGetValue(entry.Key, out val)) continue;
                if (val == -50d) continue;
                var tp = DateTime.Now.Subtract(Convert.ToDateTime(entry.Value));

                if (tp.TotalDays > config.Main.InactivityRemove)
                {
                    Puts(lang.GetMessage("console_lostnoobstatus", this, null), entry.Key, config.Main.InactivityRemove);
                    if (config.Advance.EnableLogging) LogToFile("inactive", $"[{DateTime.Now}] - {entry.Key} hasn't connected for {config.Main.InactivityRemove} days so he lost his noob status", this, false);
                    storedData.players[entry.Key] = -50d;
                    if (config.Relationship.SyncTeamPlaytime && storedData.InTeam.Contains(entry.Key))
                    {
                        ulong ID = entry.Key;
                        var team = RelationshipManager.ServerInstance?.FindPlayersTeam(ID);
                        if (team == null) continue;
                        SyncTeam(team, ID);
                    }
                }
            }
        }

        private void Refund(BasePlayer attacker, string name, BaseEntity ent)
        {
            if (!config.Refund.RefundItem || storedDataItemList.ItemList.Count < 1) return;
            
            foreach (var entry in storedDataItemList.ItemList)
            {
                if (name == entry.Value)
                {
                    if (config.Refund.RefundTimes == 0)
                    {
                        Item item = ItemManager.CreateByName(entry.Key, 1);
                        attacker.GiveItem(item);
                        SendReply(attacker, lang.GetMessage("refund_free", this, attacker.UserIDString), item.info.displayName.english);
                        if (config.Advance.EnableLogging) LogToFile("refund", $"[{DateTime.Now}] - {attacker.UserIDString} was refunded {item.info.displayName.english}", this, false);
                        return;
                    }

                    if (storedData.AttackAttempts[attacker.userID] <= config.Refund.RefundTimes)
                    {
                        int a = config.Refund.RefundTimes - (storedData.AttackAttempts[attacker.userID]);
                        Item item = ItemManager.CreateByName(entry.Key, 1);
                        attacker.GiveItem(item);

                        switch (a)
                        {
                            case 0:
                                {
                                    SendReply(attacker, lang.GetMessage("refund_last", this, attacker.UserIDString), item.info.displayName.english);
                                    if (config.Advance.EnableLogging) LogToFile("refund", $"[{DateTime.Now}] - {attacker.UserIDString} was refunded {item.info.displayName.english} but will not be next time", this, false);

                                    return;
                                }

                            case 1:
                                {
                                    SendReply(attacker, lang.GetMessage("refund_1time", this, attacker.UserIDString), item.info.displayName.english);
                                    if (config.Advance.EnableLogging) LogToFile("refund", $"[{DateTime.Now}] - {attacker.UserIDString} was refunded {item.info.displayName.english} but after 1 more attempt it wont be refunded", this, false);
                                    return;
                                }

                            default:
                                {
                                    SendReply(attacker, lang.GetMessage("refund_nTimes", this, attacker.UserIDString), item.info.displayName.english, a);
                                    if (config.Advance.EnableLogging) LogToFile("refund", $"[{DateTime.Now}] - {attacker.UserIDString} was refunded {item.info.displayName.english} but after {a} more attempts it wont be refunded", this, false);

                                    return;
                                }

                        }
                    }
                }
            }
        }

        private void StartChecking()
        {
            if (config.Relationship.SyncTeamPlaytime)
            {
                timer.Every(config.Advance.Frequency - 5, () =>
                {
                    FailSafe();
                });
            }
            timer.Every(config.Advance.Frequency - 5, () =>
            {
                RemoveInactive();
            });
            timer.Every(config.Advance.Frequency, () =>
            {
                Check();
            });
        }

        #endregion
    }
}


//  Copyright (C) <2021>  <MasterSplinter>
//
//  This program is free software: you can redistribute it and/or modify
//  it under the terms of the GNU General Public License as published by
//  the Free Software Foundation, either version 3 of the License, or
//  (at your option) any later version.
//
//  This program is distributed in the hope that it will be useful,
//  but WITHOUT ANY WARRANTY; without even the implied warranty of
//  MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//  GNU General Public License for more details.
//
//  You should have received a copy of the GNU General Public License
//  along with this program.  If not, see <https://www.gnu.org/licenses></https:>.
