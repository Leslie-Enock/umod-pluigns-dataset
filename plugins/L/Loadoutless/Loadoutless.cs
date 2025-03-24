using Oxide.Core;
using System;
using System.Collections;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/*
 * This is a partial rewrite 2.0.0
 * Now Supports Skins + Ammo Types + Reset Command
 *
 * Updated API hook
 *
 * This update 2.0.2 fixes issues with commands
 * Updated reset lang msg
 *
 * This update 2.0.3
 * Added ability for multiple loadouts per player assuming they have the permission loadoutless.save
 * Added Select command
 * Added Remove command
 * Added Clear command
 * Updated Save command
 * updated lang files
 * Added Item limits defaults to server stacksize limits
 * Added Auto Limit adjuster based on StackSizeChanges on the server.
 *
 * Warning Delete Lang file!
 * This update 2.0.4
 * Updated lang files
 * Fixed command issues
 * Updated permissions to be cleaner
 *
 * Updated Lang
 * Added overwrite command
 *
 * Update 2.0.7
 * Updated Commands / Code Cleanup
 * Imported WhiteThunders Memory leak patch
 *
 * Update 2.0.8
 * Fixed Item Limits set to 0/enabled not allowing admins to set default loadout.
 *
 * Update 2.0.9
 * Fixed error after creating a ban file and attempting to reset players data files
 *
 * Update 2.1.0
 * Fixed Clearing Data Files
 * Fixed Duplication issues
 * Fixed performance hits on high pop servers now runs coroutines on reset/clear
 *
 * Update 2.1.1
 * Fixes extended mag issue ( now you don't have to re-load to get the 38 limit )
 * Fixes 3 msg responses not being sent properly.
 * Fixed Syntax msg missing /overwrite name command option.
 * Delete lang file
 */

namespace Oxide.Plugins
{
    [Info("Loadoutless", "Khan", "2.1.2")]
    [Description("Players can respawn with a loadout")]
    public class Loadoutless : RustPlugin
    {
        #region Variables

        private const string Default = "loadoutless.spawn";
        private const string Use = "loadoutless.use";
        private const string Admin = "loadoutless.admin";

        private const string FileBanlist = "Loadoutless_itembanlist";
        private const string FileMain = "Loadoutless_folder/";

        private static Loadoutless _instance;

        private Dictionary<string, PlayerLoadout> _playerLoadouts = new Dictionary<string, PlayerLoadout>();

        private PluginConfig _config;

        #endregion

        #region Localization
        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Error_Insuffarg"] = "Insufficient arguments! player options are \n <color=#32CD32>/loadout save name, overwrite name, remove name, select name</color>",
                ["Error_Insuffarg2"] = "Insufficient arguments! options are \n setdefault : sets server loadout, \n banitem, baninv, removebanitem, \n reset : resets players loadouts to server defaults \n clear : will delete all players saved loadouts",
                ["Error_Noitem"] = "No item selected!",
                ["Error_Itemnotfound"] = "Item not found!",
                ["Error_Filenotexist"] = "Playerfile doesn't exist.",
                ["Error_Creatingnewfile"] = "Creating a new playerfile...",
                ["Cmd_Removebanitem"] = "Item is removed from the banfile.",
                ["Cmd_Removebanitem_notinfile"] = "Item was not banned!",
                ["Cmd_Banitem_already"] = "Item is already banned!",
                ["Cmd_Banitem"] = "Item <color=#32CD32>sucessfully added</color> to the banfile!",
                ["Cmd_Setdefault"] = "Default Server loadout has <color=#32CD32>succesfully been set!</color>",
                ["Cmd_Processing"] = "coroutine has begun processing data files for update. will take a min or two.",
                ["Cmd_Wait"] = "Coroutine is still running please wait a min or two and try again.",
                ["Cmd_Noperm"] = "You don't have the permsission to do that!",
                ["Cmd_Saved"] = "Loadout <color=#32CD32>{0} was sucessfully saved!</color>",
                ["Cmd_inv_added"] = "All inventory Items are saved in the ban file",
                ["Cmd_Reset"] = "All players loadouts have been reset to default \n By user {0}",
                ["Cmd_MaxLoadOut"] = "Maximum number of loadouts is {0} please remove 1 to add another",
                ["Cmd_MaxLimit"] = "{0} stack amount is greater than item stack limit set {1}",
                ["Cmd_Select"] = "Available loadouts are <color=#32CD32> {0} </color>",
                ["Cmd_Remove"] = "Available loadouts to remove are <color=#32CD32> {0} </color>",
                ["Cmd_SelectMia"] = "Loadout {0} doesn't exist, your current loadouts are <color=#32CD32> {1} </color>",
                ["Cmd_Selected"] = "Loadout {0} is now set",
                ["Cmd_Cleared"] = "Player {0} has cleared everyone's loadouts, only default loadout remains",
                ["Cmd_Savedalready"] = "Loadout <color=#32CD32>{0}</color> already exists",
                ["Cmd_NotEnough"] = "You must save a loadout with a name! \n /loadout save primary",
                ["Cmd_NotEnough2"] = "Available loadouts to remove are <color=#32CD32> {0} </color>",
                ["Cmd_Overwritten"] = "Loadout <color=#32CD32>{0} was successfully overwritten!</color>",
                ["Cmd_Overwrite"] = "Available loadouts to overwite are <color=#32CD32> {0} </color>",

            }, this);
        }

        #endregion

        #region Config

        protected override void LoadDefaultConfig() => _config = new PluginConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                _config = Config.ReadObject<PluginConfig>();

                if (_config == null)
                {
                    throw new JsonException();
                }

                if (_config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys)) return;

                PrintWarning("Loaded updated config.");
                SaveConfig();
            }
            catch
            {
                PrintWarning("You have messed up your config please run it through Json Validation @ https://jsonlint.com/");
                //LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        private void CheckConfig()
        {
            foreach (ItemDefinition item in ItemManager.itemList)
            {
                if (!(item.isHoldable || item.isUsable || item.isWearable)) continue;
                if (!_config.Limits.ContainsKey(item.shortname))
                {
                    _config.Limits.Add(item.shortname, new Limit
                    {
                        DisplayName = item.displayName.english,
                        Amount = item.stackable
                    });
                }

                if (!_config.EnableAutoLimits) continue;
                _config.Limits[item.shortname].DisplayName = item.displayName.english;
                _config.Limits[item.shortname].Amount = item.stackable;
            }

            if (_config.DefaultLoadout == null)
            {
                _config.DefaultLoadout = new List<LoadoutItem>
                {
                    new LoadoutItem
                    {
                        Itemid = 20489901,
                        Amount = 1,
                        Slot = 0,
                        Container = "wear",
                        Bp = false,
                        Weapon = false
                    },
                    new LoadoutItem
                    {
                        Itemid = -1754948969,
                        Amount = 1,
                        Slot = 0,
                        Container = "main",
                        Bp = false,
                        Weapon = false
                    },
                    new LoadoutItem
                    {
                        Itemid = 963906841,
                        Amount = 1,
                        Slot = 0,
                        Container = "belt",
                        Bp = false,
                        Weapon = false
                    },
                    new LoadoutItem
                    {
                        Itemid = 795236088,
                        Amount = 1,
                        Slot = 1,
                        Container = "belt",
                        Bp = false,
                        Weapon = false
                    },
                    new LoadoutItem
                    {
                        Itemid = -1583967946,
                        Amount = 1,
                        Slot = 2,
                        Container = "belt",
                        Bp = false,
                        Weapon = false
                    },
                    new LoadoutItem
                    {
                        Itemid = 171931394,
                        Amount = 1,
                        Slot = 3,
                        Container = "belt",
                        Bp = false,
                        Weapon = false
                    },
                };
            }
            SaveConfig();
        }

        public class Limit
        {
            public string DisplayName;
            public double Amount;
        }

        private class PluginConfig
        {
            [JsonProperty("Set Max Player Loadouts", Order = 1)] public int MaxLoadouts = 3;

            [JsonProperty("Enable Item Limits", Order = 2)] public bool EnableLimits = true;

            [JsonProperty("Enable Auto Update Limits", Order = 3)] public bool EnableAutoLimits = false;

            [JsonProperty(Order = 4)] public List<LoadoutItem> DefaultLoadout;

            [JsonProperty("Item Loadout Limits Defaults to your Servers Stack Size", Order = 5)]

            public Dictionary<string, Limit> Limits = new Dictionary<string, Limit>();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(Use, this);
            permission.RegisterPermission(Default, this);
            permission.RegisterPermission(Admin, this);
            _instance = this;
        }

        private void OnServerInitialized()
        {
            CheckConfig();

            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                PlayerLoadout.TryLoad(player);
            }
        }

        private void Unload()
        {
            if (_coroutine != null)
                ServerMgr.Instance.StopCoroutine(_coroutine);

            _instance = null;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            PlayerLoadout.TryLoad(player);
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, Default))
            {
                GiveLoadout(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            _playerLoadouts.Remove(player.UserIDString);
        }

        #endregion

        #region Methodes

        private Coroutine _coroutine;

        private IEnumerator ResetPlayersLoadout()
        {
            string[] files = Interface.Oxide.DataFileSystem.GetFiles(FileMain);

            if (files == null)
            {
                _coroutine = null;
                yield return null;
            }

            int count = 0;
            foreach (string filePath in files)
            {
                if (filePath.Contains(FileBanlist)) continue;
                string fileName = RemoveFilePath(filePath).Replace(".json", "");
                if (_playerLoadouts.ContainsKey(fileName))
                {
                    _playerLoadouts[fileName].AvailableLoadouts["default"] = _config.DefaultLoadout;
                    _playerLoadouts[fileName].Loadout = "default";

                    Interface.Oxide.DataFileSystem.WriteObject($"{FileMain}/{fileName}", _playerLoadouts[fileName]);
                }
                else
                {
                    PlayerLoadout playerLoadout = Interface.Oxide.DataFileSystem.ReadObject<PlayerLoadout>($"{FileMain}/{fileName}");
                    playerLoadout.AvailableLoadouts["default"] = _config.DefaultLoadout;
                    playerLoadout.Loadout = "default";
                    playerLoadout.Save();
                }

                count++;
                Puts($"Setting Defaults Loadouts processing file {count} {files.Length - 1}");

                yield return CoroutineEx.waitForSeconds(0.5f);
            }

            _coroutine = null;
            yield return null;
        }

        private IEnumerator ClearPlayersLoadouts()
        {
            string[] files = Interface.Oxide.DataFileSystem.GetFiles(FileMain);

            if (files == null)
            {
                _coroutine = null;
                yield break;
            }

            int count = 0;
            foreach (string filePath in files)
            {
                if (filePath.Contains(FileBanlist)) continue;
                string fileName = RemoveFilePath(filePath).Replace(".json", "");
                if (_playerLoadouts.ContainsKey(fileName))
                {
                    _playerLoadouts[fileName].AvailableLoadouts.Clear();
                    _playerLoadouts[fileName].AvailableLoadouts["default"] = _config.DefaultLoadout;
                    _playerLoadouts[fileName].Loadout = "default";
                    Interface.Oxide.DataFileSystem.WriteObject($"{FileMain}/{fileName}", _playerLoadouts[fileName]);
                }
                else
                {
                    PlayerLoadout playerLoadout = Interface.Oxide.DataFileSystem.ReadObject<PlayerLoadout>($"{FileMain}/{fileName}");
                    playerLoadout.AvailableLoadouts.Clear();
                    playerLoadout.AvailableLoadouts["default"] = _config.DefaultLoadout;
                    playerLoadout.Loadout = "default";
                    playerLoadout.Save();
                }

                count++;
                Puts($"Clearing All Loadouts processing file {count} {files.Length - 1}");

                yield return CoroutineEx.waitForSeconds(0.5f);
            }

            _coroutine = null;
            yield return null;
        }

        public string RemoveFilePath(string value)
        {
            value = value.Substring(value.LastIndexOf('/') + 1);

            return value;
        }

        public void AddInvBan(Item[] invItems)
        {
            List<string> banFile = LoadBanFile();

            foreach (var item in invItems)
            {
                if (CheckBanFile(item.info.itemid.ToString()) == false)
                {
                    banFile.Add(item.info.itemid.ToString());
                }
                else
                {
                    PrintToConsole(item.info.shortname + "already in banfile");
                }

                UpdateBanFile(banFile);
            }
        }

        void UpdateBanFile(List<string> ban_list)
        {
            Interface.Oxide.DataFileSystem.WriteObject<string>(FileMain + FileBanlist, JsonConvert.SerializeObject(ban_list));
        }

        bool CheckBanFile(string itemid)
        {
            List<string> banFile = LoadBanFile();

            return banFile.Contains(itemid);
        }

        List<string> RemoveItemBanlist(List<string> banfile, string itemid)
        {
            List<string> newBanfile = new List<string>();
            foreach (string item in banfile)
            {
                if (item != itemid)
                {
                    newBanfile.Add(item);
                }
            }
            return newBanfile;
        }

        List<string> LoadBanFile()
        {
            List<string> banFile;
            //When ban file is needed checks if it exists if not, it will create one.
            if (!Interface.Oxide.DataFileSystem.ExistsDatafile(FileMain + FileBanlist))
            {
                banFile = new List<string>();
            }
            else
            {
                string rawBanfile = Interface.Oxide.DataFileSystem.ReadObject<string>(FileMain + FileBanlist);
                banFile = JsonConvert.DeserializeObject<List<string>>(rawBanfile);
            }

            return banFile;
        }

        private static List<LoadoutItem> GetPlayerLoadout(BasePlayer player, bool admin = false)
        {
            List<LoadoutItem> loadoutItems = new List<LoadoutItem>();

            foreach (Item item in player.inventory.containerWear.itemList)
            {
                if (item == null) continue;
                if (admin == false)
                {
                    if (_instance._config.EnableLimits && _instance._config.Limits.ContainsKey(item.info.shortname) && item.amount > _instance._config.Limits[item.info.shortname].Amount)
                    {
                        _instance.SendMessage(player,_instance.Lang("Cmd_MaxLimit", player.UserIDString, item.info.displayName.english, _instance._config.Limits[item.info.shortname].Amount));
                        return null;
                    }
                }
                LoadoutItem loadoutItem = ProcessItem(item, "wear");
                loadoutItems.Add(loadoutItem);
            }

            foreach (Item item in player.inventory.containerMain.itemList)
            {
                if (item == null) continue;
                if (admin == false)
                {
                    if (_instance._config.EnableLimits && _instance._config.Limits.ContainsKey(item.info.shortname) && item.amount > _instance._config.Limits[item.info.shortname].Amount)
                    {
                        _instance.SendMessage(player,_instance.Lang("Cmd_MaxLimit", player.UserIDString, item.info.displayName.english, _instance._config.Limits[item.info.shortname].Amount));
                        return null;
                    }
                }
                LoadoutItem loadoutItem = ProcessItem(item, "main");
                loadoutItems.Add(loadoutItem);
            }

            foreach (Item item in player.inventory.containerBelt.itemList)
            {
                if (item == null) continue;
                if (admin == false)
                {
                    if (_instance._config.EnableLimits && _instance._config.Limits.ContainsKey(item.info.shortname) && item.amount > _instance._config.Limits[item.info.shortname].Amount)
                    {
                        _instance.SendMessage(player,_instance.Lang("Cmd_MaxLimit", player.UserIDString, item.info.displayName.english, _instance._config.Limits[item.info.shortname].Amount));
                        return null;
                    }
                }
                LoadoutItem loadoutItem = ProcessItem(item, "belt");
                loadoutItems.Add(loadoutItem);
            }

            return loadoutItems;
        }

        private static LoadoutItem ProcessItem(Item item, string container)
        {
            LoadoutItem iItem = new LoadoutItem();
            iItem.Amount = item.amount;
            iItem.Mods = new List<int>();
            iItem.Container = container;
            iItem.Skinid = item.skin;
            iItem.Itemid = item.info.itemid;
            iItem.Weapon = false;
            iItem.Slot = item.position;
            iItem.Ammotype = 0;
            iItem.AmmoAmount = 0;

            if (item.info.category.ToString() != "Weapon")
            {
                return iItem;
            }

            BaseProjectile weapon = item.GetHeldEntity() as BaseProjectile;

            if (weapon == null || weapon.primaryMagazine == null)
            {
                return iItem;
            }

            iItem.Weapon = true;
            iItem.Ammotype = weapon.primaryMagazine.ammoType.itemid;
            iItem.AmmoAmount = weapon.primaryMagazine.contents;

            if (item.contents == null)
            {
                return iItem;
            }

            foreach (var mod in item.contents.itemList)
            {
                if (mod.info.itemid != 0)
                {
                    iItem.Mods.Add(mod.info.itemid);
                }
            }

            return iItem;
        }

        private static Item BuildWeapon(int id, ulong skin, int Ammotype, int ammoAmount, List<int> mods)
        {
            Item item = CreateByItemID(id, 1, skin);
            var weapon = item.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                weapon.primaryMagazine.contents = ammoAmount;
                weapon.primaryMagazine.ammoType = ItemManager.FindItemDefinition(Ammotype);
            }

            if (mods == null)
            {
                return item;
            }

            bool mag = false;
            foreach (var mod in mods)
            {
                item.contents.AddItem(ItemManager.FindItemDefinition(mod), 1);
                if (mod == 2005491391) //extended mag
                {
                    mag = true;
                }
            }

            if (mag)
            {
                int num = Mathf.CeilToInt(ProjectileWeaponMod.Mult(weapon, (x => x.magazineCapacity), (y => y.scalar), 1f) * weapon.primaryMagazine.definition.builtInSize);
                weapon.primaryMagazine.contents = ammoAmount;
                weapon.primaryMagazine.capacity = num;
            }

            return item;
        }

        private static Item BuildItem(int itemid, int amount, ulong skin)
        {
            if (amount < 1) amount = 1;
            Item item = CreateByItemID(itemid, amount, skin);
            return item;
        }

        private static Item CreateByItemID(int itemID, int amount = 1, ulong skin = 0)
        {
            return ItemManager.CreateByItemID(itemID, amount, skin);
        }

        private static bool GiveItem(PlayerInventory inv, Item item, ItemContainer container = null, int position = 0)
        {
            if (item == null) { return false; }
            return container != null && item.MoveToContainer(container, position, true) || item.MoveToContainer(inv.containerMain, position, true) || item.MoveToContainer(inv.containerBelt, position, true);
        }

        List<LoadoutItem> CheckBannedList(List<LoadoutItem> list)
        {
            List<LoadoutItem> newList = new List<LoadoutItem>();
            List<string> banFile = LoadBanFile();

            foreach (LoadoutItem item in list)
            {
                if (!banFile.Contains(item.Itemid.ToString()))
                {
                    newList.Add(item);
                }
            }

            return newList;
        }

        public object GiveLoadout(BasePlayer player)
        {
            return PlayerLoadout.Find(player)?.GiveLoadout(player);
        }

        public void SendMessage(BasePlayer player, string message)
        {
            PrintToChat(player, lang.GetMessage(message, this, player.UserIDString));
        }

        #endregion

        #region Classes

        public class PlayerLoadout
        {
            public ulong ID;
            public string Name;
            //public List<LoadoutItem> Items = new List<LoadoutItem>();
            public string Loadout;
            public Dictionary<string, List<LoadoutItem>> AvailableLoadouts = new Dictionary<string, List<LoadoutItem>>();

            internal static void TryLoad(BasePlayer player)
            {
                if (Find(player) != null)
                {
                    return;
                }

                PlayerLoadout data = Interface.Oxide.DataFileSystem.ReadObject<PlayerLoadout>($"{FileMain}/{player.userID}");

                if (data == null || data.ID == 0)
                {
                    data = new PlayerLoadout{ID = player.userID, Name = player.displayName, Loadout = "default", AvailableLoadouts = new Dictionary<string, List<LoadoutItem>>{{"default", _instance._config.DefaultLoadout}}};
                }
                else
                {
                    data.Update(player);
                }

                data.Save();

                if (!_instance._playerLoadouts.ContainsKey(player.UserIDString))
                {
                    _instance._playerLoadouts.Add(player.UserIDString, data);
                }
            }

            internal void Update(BasePlayer player)
            {
                ID = player.userID;
                Name = player.displayName;
                
                Save();
            }

            internal void Save()
            {
                Interface.Oxide.DataFileSystem.WriteObject($"{FileMain}/{ID}", this);
            }

            internal static PlayerLoadout Find(BasePlayer player)
            {
                PlayerLoadout playerLoadout;

                return _instance._playerLoadouts.TryGetValue(player.UserIDString, out playerLoadout) ? playerLoadout : null;
            }

            internal object GiveLoadout(BasePlayer player)
            {
                if (!AvailableLoadouts.ContainsKey(Loadout))
                {
                    return null;
                }
                player.inventory.Strip();

                foreach (var kitem in AvailableLoadouts[Loadout])
                {
                    GiveItem(player.inventory,
                        kitem.Weapon
                            ? BuildWeapon(kitem.Itemid, kitem.Skinid, kitem.Ammotype, kitem.AmmoAmount, kitem.Mods)
                            : BuildItem(kitem.Itemid, kitem.Amount, kitem.Skinid),
                        kitem.Container == "belt"
                            ? player.inventory.containerBelt
                            : kitem.Container == "wear"
                                ? player.inventory.containerWear
                                : player.inventory.containerMain, kitem.Slot);
                }

                return true;
            }
        }

        public class LoadoutItem
        {
            public int Itemid;
            public bool Bp;
            public ulong Skinid;
            public string Container;
            public int Slot;
            public int Amount;
            public bool Weapon;
            public int Ammotype;
            public int AmmoAmount;
            public List<int> Mods = new List<int>();
        }

        #endregion

        #region Commands

        [ChatCommand("loadout")]
        private void LoadoutLessCommand(BasePlayer player, string command, string[] args)
        {
            if (_coroutine != null)
            {
                SendMessage(player, "Cmd_Wait");
                return;
            }
            if (args.Length == 0)
            {
                if (permission.UserHasPermission(player.UserIDString, Admin))
                {
                    SendMessage(player,"Error_Insuffarg2");
                }

                if (permission.UserHasPermission(player.UserIDString, Use))
                {
                    SendMessage(player, "Error_Insuffarg");
                }
            }
            else
            {
                PlayerLoadout user = PlayerLoadout.Find(player);
                switch (args[0].ToLower())
                {
                    case "overwrite":
                        if (permission.UserHasPermission(player.UserIDString, Use))
                        {
                            if (args.Length < 2)
                            {
                                SendMessage(player, Lang("Cmd_NotEnough", player.UserIDString));
                                return;
                            }
                            if (!user.AvailableLoadouts.ContainsKey(args[1]))
                            {
                                SendMessage(player, Lang("Cmd_Overwrite", player.UserIDString, string.Join("\n", user.AvailableLoadouts.Keys.ToArray())));
                                return;
                            }

                            user.Loadout = args[1];
                            List<LoadoutItem> loadout = GetPlayerLoadout(player);
                            if (loadout == null) return;
                            user.AvailableLoadouts[args[1]] = CheckBannedList(loadout);
                            SendMessage(player, Lang("Cmd_Overwritten", player.UserIDString, user.Loadout));
                            user.Save();
                        }
                        else
                        {
                            SendMessage(player, "Cmd_Noperm");
                        }
                        break;
                    case "save":
                        if (permission.UserHasPermission(player.UserIDString, Use))
                        {
                            if (args.Length < 2)
                            {
                                SendMessage(player, Lang("Cmd_NotEnough", player.UserIDString));
                                return;
                            }
                            if (user.AvailableLoadouts.Count >= _config.MaxLoadouts)
                            {
                                SendMessage(player, Lang("Cmd_MaxLoadOut", player.UserIDString, _config.MaxLoadouts));
                                return;
                            }
                            if (user.AvailableLoadouts.ContainsKey(args[1]))
                            {
                                SendMessage(player, Lang("Cmd_Savedalready", player.UserIDString, args[1]));
                                return;
                            }
                            user.Loadout = args[1];
                            List<LoadoutItem> loadout = GetPlayerLoadout(player);
                            if (loadout == null) return;
                            user.AvailableLoadouts.Add(args[1], CheckBannedList(loadout));
                            SendMessage(player, Lang("Cmd_Saved", player.UserIDString, user.Loadout));
                            user.Save();
                        }
                        else
                        {
                            SendMessage(player, "Cmd_Noperm");
                        }
                        break;
                    case "remove":
                        if (permission.UserHasPermission(player.UserIDString, Use))
                        {
                            if (args.Length < 2)
                            {
                                SendMessage(player, _instance.Lang("Cmd_Remove", player.UserIDString, string.Join("\n", user.AvailableLoadouts.Keys.ToArray())));
                                return;
                            }
                            if (!user.AvailableLoadouts.ContainsKey(args[1]))
                            {
                                SendMessage(player, _instance.Lang("Cmd_Remove", player.UserIDString, string.Join("\n", user.AvailableLoadouts.Keys.ToArray())));
                                return;
                            }

                            user.AvailableLoadouts.Remove(args[1]);
                            user.Save();
                            SendMessage(player, $"Loadout {args[1]} was removed");
                        }
                        else
                        {
                            SendMessage(player, "Cmd_Noperm");
                        }
                        break;
                    case "select":
                        if (permission.UserHasPermission(player.UserIDString, Use))
                        {
                            if (args.Length == 1)
                            {
                                SendMessage(player, _instance.Lang("Cmd_Select", player.UserIDString, string.Join("\n", user.AvailableLoadouts.Keys.ToArray())));
                                return;
                            }
                            if (!user.AvailableLoadouts.ContainsKey(args[1]))
                            {
                                SendMessage(player, _instance.Lang("Cmd_SelectMia", player.UserIDString, args[1], string.Join("\n", user.AvailableLoadouts.Keys.ToArray())));
                                return;
                            }

                            user.Loadout = args[1];
                            user.Save();
                            SendMessage(player, _instance.Lang("Cmd_Selected", player.UserIDString, args[1]));
                        }
                        else
                        {
                            SendMessage(player, "Cmd_Noperm");
                        }
                        break;
                    case "setdefault":
                        if (permission.UserHasPermission(player.UserIDString, Admin))
                        {
                            List<LoadoutItem> loadout = GetPlayerLoadout(player, true);
                            if (loadout == null) return;
                            _config.DefaultLoadout = loadout;
                            SaveConfig();
                            _coroutine = ServerMgr.Instance.StartCoroutine(ResetPlayersLoadout());
                            SendMessage(player, "Cmd_Setdefault");
                            SendMessage(player, "Cmd_Processing");
                        }
                        else
                        {
                            SendMessage(player, "Cmd_Noperm");
                        }
                        break;
                    case "banitem":
                        if (permission.UserHasPermission(player.UserIDString, Admin))
                        {
                            try
                            {
                                List<string> ban_list = LoadBanFile();
                                string itemid = player.GetActiveItem().info.itemid.ToString();
                                if (CheckBanFile(itemid) == false)
                                {
                                    ban_list.Add(itemid);
                                    UpdateBanFile(ban_list);
                                    SendMessage(player, "Cmd_Banitem");
                                }
                                else
                                {
                                    SendMessage(player, "Cmd_Banitem_already");
                                }
                            }
                            catch (NullReferenceException)
                            {

                                SendMessage(player, "Error_Noitem");

                            }
                        }
                        else
                        {
                            SendMessage(player, "Cmd_Noperm");
                        }
                        break;
                    case "removebanitem":
                        if (permission.UserHasPermission(player.UserIDString, Admin))
                        {
                            try
                            {
                                string itemid = player.GetActiveItem().info.itemid.ToString();
                                if (CheckBanFile(itemid) == true)
                                {
                                    UpdateBanFile(RemoveItemBanlist(LoadBanFile(), itemid));
                                    SendMessage(player, "Cmd_Removebanitem");
                                }
                                else
                                {
                                    SendMessage(player, "Cmd_Removebanitem_notinfile");
                                }
                            }
                            catch (NullReferenceException)
                            {
                                SendMessage(player, "Cmd_Error_Noitem");
                            }
                        }
                        else
                        {
                            SendMessage(player, "Cmd_Noperm");
                        }
                        break;
                    case "baninv":
                        if (permission.UserHasPermission(player.UserIDString, Admin))
                        {
                            AddInvBan(player.inventory.AllItems());
                            SendMessage(player, "Cmd_inv_added");
                        }
                        else
                        {
                            SendMessage(player, "Cmd_Noperm");
                        }
                        break;
                    case "reset":
                        if (permission.UserHasPermission(player.UserIDString, Admin))
                        {
                            _coroutine = ServerMgr.Instance.StartCoroutine(ResetPlayersLoadout());
                            SendMessage(player, Lang("Cmd_Reset", player.UserIDString, player.displayName));
                            SendMessage(player, "Cmd_Processing");
                        }
                        else
                        {
                            SendMessage(player, "Cmd_Noperm");
                        }
                        break;
                    case "clear" :
                        if (permission.UserHasPermission(player.UserIDString, Admin))
                        {
                            _coroutine = ServerMgr.Instance.StartCoroutine(ClearPlayersLoadouts());
                            SendMessage(player, _instance.Lang("Cmd_Cleared", player.UserIDString, player.displayName));
                            SendMessage(player, "Cmd_Processing");
                        }
                        else
                        {
                            SendMessage(player, "Cmd_Noperm");
                        }
                        break;
                    default:
                        SendMessage(player, "Error_Insuffarg");
                        break;
                }
            }
        }

        #endregion
    }
}