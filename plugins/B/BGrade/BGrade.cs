using System;
using System.Linq;
using System.Collections.Generic;

using Rust;
using UnityEngine;

using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Plugins.BGradeExt;

namespace Oxide.Plugins
{
    [Info("BGrade", "Ryan / Rustoria.co", "1.1.6")]
    [Description("Auto update building blocks when placed")]
    public class BGrade : RustPlugin
    {
        #region Declaration

        public static BGrade Instance;
        private ListHashSet<string> _registeredPermissions = new ListHashSet<string>();
        private Dictionary<Vector3, int> _lastAttacked = new Dictionary<Vector3, int>();

        #endregion

        #region Config

        private bool ConfigChanged;

        // Timer settings
        private bool AllowTimer;
        private int MaxTimer;
        private int DefaultTimer;

        // Last attack settings
        private bool CheckLastAttack;
        private int UpgradeCooldown;

        // Command settings
        private List<string> ChatCommands;
        private List<string> ConsoleCommands;

        // Refund settings
        private bool RefundOnBlock;

        // Player Component settings
        private bool DestroyOnDisconnect;

        protected override void LoadDefaultConfig() => PrintWarning("Generating default configuration file...");

        private void InitConfig()
        {
            AllowTimer = GetConfig(true, "Timer Settings", "Enabled");
            DefaultTimer = GetConfig(30, "Timer Settings", "Default Timer");
            MaxTimer = GetConfig(180, "Timer Settings", "Max Timer");
            ChatCommands = GetConfig(new List<string>
            {
                "bgrade",
                "grade"
            }, "Command Settings", "Chat Commands");
            ConsoleCommands = GetConfig(new List<string>
            {
                "bgrade.up"
            }, "Command Settings", "Console Commands");
            CheckLastAttack = GetConfig(true, "Building Attack Settings", "Enabled");
            UpgradeCooldown = GetConfig(30, "Building Attack Settings", "Cooldown Time");
            RefundOnBlock = GetConfig(true, "Refund Settings", "Refund on Block");
            DestroyOnDisconnect = GetConfig(false, "Destroy Data on Player Disconnect (for high pop servers)");

            if (ConfigChanged)
            {
                PrintWarning("Updated configuration file with new/changed values.");
                SaveConfig();
            }
        }

        private T GetConfig<T>(T defaultVal, params string[] path)
        {
            var data = Config.Get(path);
            if (data != null)
            {
                return Config.ConvertValue<T>(data);
            }

            Config.Set(path.Concat(new object[] { defaultVal }).ToArray());
            ConfigChanged = true;
            return defaultVal;
        }

        #endregion

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Permission"] = "You don't have permission to use that command",

                ["Error.InvalidArgs"] = "Invalid arguments, please use /{0} help",
                ["Error.Resources"] = "You don't have enough resources to upgrade.",
                ["Error.InvalidTime"] = "Please enter a valid time. '<color=orange>{0}</color>' is not recognised as a number.",
                ["Error.TimerTooLong"] = "Please enter a time that is below the value of <color=orange>{0}</color>.",

                ["Notice.SetGrade"] = "Automatic upgrading is now set to grade <color=orange>{0}</color>.",
                ["Notice.SetTime"] = "The disable timer is now set to <color=orange>{0}</color>.",
                ["Notice.Disabled"] = "Automatic upgrading is now disabled.",
                ["Notice.Disabled.Auto"] = "Automatic upgrading has been automatically disabled.",
                ["Notice.Time"] = "It'll automatically disable in <color=orange>{0}</color> seconds.",

                ["Command.Help"] = "<color=orange><size=16>BGrade Command Usages</size></color>",
                ["Command.Help.0"] = "/{0} 0 - Disables BGrade",
                ["Command.Help.1"] = "/{0} 1 - Upgrades to Wood upon placement",
                ["Command.Help.2"] = "/{0} 2 - Upgrades to Stone upon placement",
                ["Command.Help.3"] = "/{0} 3 - Upgrades to Metal upon placement",
                ["Command.Help.4"] = "/{0} 4 - Upgrades to Armoured upon placement",
                ["Command.Help.T"] = "/{0} t <seconds> - Time until BGrade is disabled",

                ["Command.Settings"] = "<color=orange><size=16>Your current settings</size></color>",
                ["Command.Settings.Timer"] = "Timer: <color=orange>{0}</color> seconds",
                ["Command.Settings.Grade"] = "Grade: <color=orange>{0}</color>",

                ["Words.Disabled"] = "disabled"
            }, this);
        }

        #endregion

        #region Methods

        private void RegisterPermissions()
        {
            _registeredPermissions = new ListHashSet<string>(  );

            for (var i = 1; i < 5; i++)
            {
                RegisterPermission( Name.ToLower() + "." + i );
            }

            RegisterPermission( Name.ToLower() + "." + "nores" );
            RegisterPermission( Name.ToLower() + "." + "all" );
        }

        private void RegisterPermission( string permissionName )
        {
            if ( !_registeredPermissions.Contains( permissionName ) )
            {
                _registeredPermissions.Add( permissionName );
            }

            permission.RegisterPermission( permissionName, this );
        }

        private void RegisterCommands()
        {
            foreach (var command in ChatCommands)
            {
                cmd.AddChatCommand(command, this, BGradeCommand);
            }

            foreach (var command in ConsoleCommands)
            {
                cmd.AddConsoleCommand(command, this, nameof(BGradeUpCommand));
            }
        }

        private void DestroyAll<T>() where T : MonoBehaviour
        {
            foreach (var type in UnityEngine.Object.FindObjectsOfType<T>())
            {
                UnityEngine.Object.Destroy(type);
            }
        }

        private void DealWithHookResult(BasePlayer player, BuildingBlock buildingBlock, int hookResult, GameObject gameObject)
        {
            if (hookResult <= 0)
            {
                return;
            }

            if (RefundOnBlock)
            {
                foreach (var itemToGive in buildingBlock.BuildCost())
                {
                    player.GiveItem(ItemManager.CreateByItemID(itemToGive.itemid, (int)itemToGive.amount));
                }
            }

            gameObject.GetComponent<BaseEntity>().Kill();
        }

        private string TakeResources(BasePlayer player, int playerGrade, BuildingBlock buildingBlock, out Dictionary<int, int> items)
        {
            var itemsToTake = new Dictionary<int, int>();

            List<ItemAmount> costToBuild = null;
            foreach (var grade in buildingBlock.blockDefinition.grades)
            {
                if (grade.gradeBase.type == (BuildingGrade.Enum) playerGrade)
                {
                    costToBuild = grade.CostToBuild();
                    break;
                }
            }

            if (costToBuild == null)
            {
                PrintError($"COULDN'T FIND COST TO BUILD WITH GRADE: {playerGrade} FOR {buildingBlock.PrefabName}");
                items = itemsToTake;
                return "Error.Resources".Lang(player.UserIDString);
            }

            foreach (var itemAmount in costToBuild)
            {
                if (!itemsToTake.ContainsKey(itemAmount.itemid))
                {
                    itemsToTake.Add(itemAmount.itemid, 0);
                }

                itemsToTake[itemAmount.itemid] += (int)itemAmount.amount;
            }

            var canAfford = true;
            foreach (var itemToTake in itemsToTake)
            {
                if (!player.HasItemAmount(itemToTake.Key, itemToTake.Value))
                {
                    canAfford = false;
                }
            }

            items = itemsToTake;
            return canAfford ? null : "Error.Resources".Lang(player.UserIDString);
        }

        private void CheckLastAttacked()
        {
            foreach (var lastAttackEntry in _lastAttacked.ToList())
            {
                if (!WasAttackedRecently(lastAttackEntry.Key))
                {
                    _lastAttacked.Remove(lastAttackEntry.Key);
                }
            }
        }

        private bool WasAttackedRecently(Vector3 position)
        {
            int time;
            if (!_lastAttacked.TryGetValue(position, out time))
            {
                return false;
            }

            if (time < Facepunch.Math.Epoch.Current)
            {
                return true;
            }

            return false;
        }

        #endregion

        #region BGrade Player

        private class BGradePlayer : FacepunchBehaviour
        {
            public static Dictionary<BasePlayer, BGradePlayer> Players = new Dictionary<BasePlayer, BGradePlayer>();

            private BasePlayer _player;
            private Timer _timer;
            private int _grade;
            private int _time;

            public void Awake()
            {
                var attachedPlayer = GetComponent<BasePlayer>();
                if ( attachedPlayer == null || !attachedPlayer.IsConnected )
                {
                    return;
                }

                _player = attachedPlayer;
                Players[_player] = this;

                _time = GetTime(false);
            }

            public int GetTime(bool updateTime = true)
            {
                if (!Instance.AllowTimer)
                {
                    return 0;
                }

                if (updateTime)
                {
                    UpdateTime();
                }

                return _time != 0 ? _time : Instance.DefaultTimer;
            }

            public void UpdateTime()
            {
                if (_time <= 0)
                {
                    return;
                }

                DestroyTimer();

                SetTimer(Instance.timer.Once(_time, () =>
                {
                    _grade = 0;
                    DestroyTimer();
                    _player.ChatMessage("Notice.Disabled.Auto".Lang(_player.UserIDString));
                }));
            }

            public int GetGrade() => _grade;

            public bool IsTimerValid
            {
                get
                {
                    return _timer != null && !_timer.Destroyed;
                }
            }

            private void SetTimer(Timer timer)
            {
                _timer = timer;
            }

            public void SetGrade(int newGrade)
            {
                _grade = newGrade;
            }

            public void SetTime(int newTime)
            {
                _time = newTime;
            }

            public void DestroyTimer()
            {
                _timer?.Destroy();
                _timer = null;
            }

            public void Destroy()
            {
                Destroy(this);
            }

            public void OnDestroy()
            {
                if ( Players.ContainsKey( _player ) )
                {
                    Players.Remove( _player );
                }
            }
        }

        #endregion

        #region Hooks

        private void Init()
        {
            Instance = this;

            InitConfig();
            RegisterCommands();
            RegisterPermissions();

            if (!CheckLastAttack)
            {
                Unsubscribe(nameof(OnEntityDeath));
                Unsubscribe(nameof(OnServerSave));
            }

            if (!DestroyOnDisconnect)
            {
                Unsubscribe(nameof(OnPlayerDisconnected));
            }
        }

        private void OnServerSave()
        {
            CheckLastAttacked();
        }

        private void Unload()
        {
            Instance = null;
            DestroyAll<BGradePlayer>();
            BGradePlayer.Players.Clear();
        }

        private void OnEntityBuilt(Planner plan, GameObject gameObject)
        {
            var player = plan?.GetOwnerPlayer();
            if (player == null)
            {
                return;
            }

            if ( plan.isTypeDeployable )
            {
                return;
            }

            var buildingBlock = gameObject.GetComponent<BuildingBlock>();
            if ( buildingBlock == null )
            {
                return;
            }

            if (!player.CanBuild())
            {
                return;
            }

            if ( !player.HasAnyPermission( _registeredPermissions ) )
            {
                return;
            }

            BGradePlayer bgradePlayer;
            if ( !BGradePlayer.Players.TryGetValue( player, out bgradePlayer ) )
            {
                return;
            }

            var playerGrade = bgradePlayer.GetGrade();
            if (playerGrade == 0)
            {
                return;
            }

            if (!player.HasPluginPerm("all") && !player.HasPluginPerm(playerGrade.ToString()))
            {
                return;
            }

            var hookCall = Interface.Call("CanBGrade", player, playerGrade, buildingBlock, plan);

            if (hookCall is int)
            {
                DealWithHookResult(player, buildingBlock, (int) hookCall, gameObject);
                return;
            }

            if (playerGrade < (int) buildingBlock.grade || buildingBlock.blockDefinition.grades[playerGrade] == null)
            {
                return;
            }

            if (CheckLastAttack && WasAttackedRecently(buildingBlock.transform.position))
            {
                return;
            }

            if (Interface.Call("OnStructureUpgrade", buildingBlock, player, (BuildingGrade.Enum) playerGrade) != null)
            {
                return;
            }

            if (!player.HasPluginPerm("nores"))
            {
                Dictionary<int, int> itemsToTake;
                var resourceResponse = TakeResources(player, playerGrade, buildingBlock, out itemsToTake);
                if (!string.IsNullOrEmpty(resourceResponse))
                {
                    player.ChatMessage(resourceResponse);
                    return;
                }

                foreach (var itemToTake in itemsToTake)
                {
                    player.TakeItem(itemToTake.Key, itemToTake.Value);
                }
            }

            if (AllowTimer)
            {
                bgradePlayer.UpdateTime();
            }

            buildingBlock.SetGrade((BuildingGrade.Enum)playerGrade);
            buildingBlock.SetHealthToMax();
            buildingBlock.StartBeingRotatable();
            buildingBlock.SendNetworkUpdate();
            buildingBlock.UpdateSkin();
            buildingBlock.ResetUpkeepTime();
            buildingBlock.GetBuilding()?.Dirty();
        }

        private object OnPayForPlacement( BasePlayer player, Planner planner, Construction component )
        {
            if ( planner.isTypeDeployable )
            {
                return null;
            }

            if ( !BGradePlayer.Players.ContainsKey( player ) )
            {
                return null;
            }

            if ( !player.HasPluginPerm( "nores" ) )
            {
                return null;
            }

            var bgradePlayer = BGradePlayer.Players[player];
            if ( bgradePlayer.GetGrade() == 0 )
            {
                return null;
            }

            return false;
        }

        private void OnEntityDeath(BuildingBlock buildingBlock, HitInfo info)
        {
            var attacker = info?.InitiatorPlayer;
            if (attacker == null)
            {
                return;
            }

            if (info.damageTypes.GetMajorityDamageType() == DamageType.Explosion)
            {
                _lastAttacked[buildingBlock.transform.position] = Facepunch.Math.Epoch.Current + UpgradeCooldown;
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            BGradePlayer bgradePlayer;
            if ( !BGradePlayer.Players.TryGetValue( player, out bgradePlayer ) )
            {
                return;
            }

            bgradePlayer.Destroy();
        }

        #endregion

        #region Commands

        private void BGradeCommand(BasePlayer player, string command, string[] args)
        {
            if (!player.HasAnyPermission(_registeredPermissions))
            {
                player.ChatMessage("Permission".Lang(player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                player.ChatMessage("Error.InvalidArgs".Lang(player.UserIDString, command));
                return;
            }

            var chatMsgs = new List<string>();

            switch (args[0].ToLower())
            {
                case "0":
                    {
                        player.ChatMessage("Notice.Disabled".Lang(player.UserIDString));
                        BGradePlayer bgradePlayer;
                        if ( BGradePlayer.Players.TryGetValue( player, out bgradePlayer ) )
                        {
                            bgradePlayer.DestroyTimer();
                            bgradePlayer.SetGrade( 0 );
                        }
                        return;
                    }

                case "1":
                case "2":
                case "3":
                case "4":
                    {
                        if (!player.HasPluginPerm("all") && !player.HasPluginPerm(args[0]))
                        {
                            player.ChatMessage("Permission".Lang(player.UserIDString));
                            return;
                        }

                        var grade = Convert.ToInt32(args[0]);

                        BGradePlayer bgradePlayer;
                        if ( !BGradePlayer.Players.TryGetValue( player, out bgradePlayer ) )
                        {
                            bgradePlayer = player.gameObject.AddComponent<BGradePlayer>();
                        }

                        bgradePlayer.SetGrade(grade);
                        var time = bgradePlayer.GetTime();
                        chatMsgs.Add("Notice.SetGrade".Lang(player.UserIDString, grade));

                        if (AllowTimer && time > 0)
                        {
                            chatMsgs.Add("Notice.Time".Lang(player.UserIDString, time));
                        }

                        player.ChatMessage(string.Join("\n", chatMsgs.ToArray()));
                        return;
                    }

                case "t":
                    {
                        if ( !AllowTimer )
                        {
                            return;
                        }

                        if ( args.Length == 1 )
                        {
                            goto default;
                        }

                        int time;
                        if (!int.TryParse(args[1], out time) || time <= 0)
                        {
                            player.ChatMessage("Error.InvalidTime".Lang(player.UserIDString, args[1]));
                            return;
                        }

                        if (time > MaxTimer)
                        {
                            player.ChatMessage("Error.TimerTooLong".Lang(player.UserIDString, MaxTimer));
                            return;
                        }

                        BGradePlayer bgradePlayer;
                        if ( !BGradePlayer.Players.TryGetValue( player, out bgradePlayer ) )
                        {
                            bgradePlayer = player.gameObject.AddComponent<BGradePlayer>();
                        }

                        player.ChatMessage("Notice.SetTime".Lang(player.UserIDString, time));
                        bgradePlayer.SetTime(time);
                        return;
                    }

                case "help":
                    {
                        chatMsgs.Add("Command.Help".Lang(player.UserIDString));
                        if (AllowTimer)
                        {
                            chatMsgs.Add("Command.Help.T".Lang(player.UserIDString, command));
                            chatMsgs.Add("Command.Help.0".Lang(player.UserIDString, command));
                        }

                        for (var i = 1; i < 5; i++)
                        {
                            if (player.HasPluginPerm(i.ToString()) || player.HasPluginPerm("all"))
                                chatMsgs.Add($"Command.Help.{i}".Lang(player.UserIDString, command));
                        }

                        if (chatMsgs.Count <= 3 && !player.HasPluginPerm("all"))
                        {
                            player.ChatMessage("Permission".Lang(player.UserIDString));
                            return;
                        }

                        BGradePlayer bgradePlayer;
                        if ( BGradePlayer.Players.TryGetValue( player, out bgradePlayer ) )
                        {
                            chatMsgs.Add( "Command.Settings".Lang( player.UserIDString ) );
                            if ( AllowTimer )
                            {
                                chatMsgs.Add( "Command.Settings.Timer".Lang( player.UserIDString, bgradePlayer.GetTime( false ) ) );
                            }

                            var fetchedGrade = bgradePlayer.GetGrade();
                            chatMsgs.Add( "Command.Settings.Grade".Lang( player.UserIDString, fetchedGrade == 0 ? "Words.Disabled".Lang( player.UserIDString ) : fetchedGrade.ToString() ) );
                        }

                        player.ChatMessage(string.Join("\n", chatMsgs.ToArray()));
                        return;
                    }

                default:
                    {
                        player.ChatMessage("Error.InvalidArgs".Lang(player.UserIDString, command));
                        return;
                    }
            }
        }

        private void BGradeUpCommand(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null)
            {
                return;
            }

            if (!player.HasAnyPermission(_registeredPermissions))
            {
                player.ChatMessage("Permission".Lang(player.UserIDString));
                return;
            }

            BGradePlayer bgradePlayer;
            if ( !BGradePlayer.Players.TryGetValue( player, out bgradePlayer ) )
            {
                bgradePlayer = player.gameObject.AddComponent<BGradePlayer>();
            }
            var grade = bgradePlayer.GetGrade() + 1;
            var count = 0;

            if (!player.HasPluginPerm("all"))
            {
                while (!player.HasPluginPerm(grade.ToString()))
                {
                    var newGrade = grade++;
                    if (newGrade > 4)
                    {
                        grade = 1;
                    }

                    if (count > bgradePlayer.GetGrade() + 4)
                    {
                        player.ChatMessage("Permission".Lang(player.UserIDString));
                        return;
                    }
                }
            }
            else if (grade > 4) grade = 1;

            var chatMsgs = new List<string>();
            bgradePlayer.SetGrade(grade);
            var time = bgradePlayer.GetTime();

            chatMsgs.Add("Notice.SetGrade".Lang(player.UserIDString, grade));
            if (AllowTimer && time > 0)
            {
                chatMsgs.Add("Notice.Time".Lang(player.UserIDString, time));
            }

            player.ChatMessage(string.Join("\n", chatMsgs.ToArray()));
        }

        #endregion
    }
}

namespace Oxide.Plugins.BGradeExt
{
    public static class BGradeExtensions
    {
        private static readonly Permission permission = Interface.Oxide.GetLibrary<Permission>();
        private static readonly Lang lang = Interface.Oxide.GetLibrary<Lang>();

        public static bool HasAnyPermission(this BasePlayer player, ListHashSet<string> perms)
        {
            foreach (var perm in perms)
            {
                if (!player.HasPermission(perm))
                {
                    continue;
                }

                return true;
            }

            return false;
        }

        public static bool HasPermission(this BasePlayer player, string perm)
        {
            return permission.UserHasPermission(player.UserIDString, perm);
        }

        public static bool HasPluginPerm(this BasePlayer player, string perm)
        {
            return permission.UserHasPermission(player.UserIDString, BGrade.Instance.Name.ToLower() + "." + perm);
        }

        public static string Lang(this string key, string id = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, BGrade.Instance, id), args);
        }

        public static bool HasItemAmount(this BasePlayer player , int itemId , int itemAmount)
        {
            var count = 0;

            foreach (var item in player.inventory.containerMain.itemList)
            {
                if (item.info.itemid == itemId)
                {
                    count += item.amount;
                }
            }

            foreach (var item in player.inventory.containerBelt.itemList)
            {
                if (item.info.itemid == itemId)
                {
                    count += item.amount;
                }
            }

            foreach (var item in player.inventory.containerWear.itemList)
            {
                if (item.info.itemid == itemId)
                {
                    count += item.amount;
                }
            }

            return count >= itemAmount;
        }


        public static bool HasItemAmount(this BasePlayer player , int itemId , int itemAmount , out int amountGot)
        {
            var count = 0;

            foreach (var item in player.inventory.containerMain.itemList)
            {
                if (item.info.itemid == itemId)
                {
                    count += item.amount;
                }
            }

            foreach (var item in player.inventory.containerBelt.itemList)
            {
                if (item.info.itemid == itemId)
                {
                    count += item.amount;
                }
            }

            foreach (var item in player.inventory.containerWear.itemList)
            {
                if (item.info.itemid == itemId)
                {
                    count += item.amount;
                }
            }

            amountGot = count;
            return count >= itemAmount;
        }


        public static void TakeItem(this BasePlayer player, int itemId, int itemAmount)
        {
            if (player.inventory.Take(null, itemId, itemAmount) > 0)
            {
                player.SendConsoleCommand("note.inv", itemId, itemAmount * -1);
            }
        }
    }
}