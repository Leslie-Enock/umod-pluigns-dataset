using Network;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Corpse Location", "shinnova", "2.3.8")]
    [Description("Allows users to locate their latest corpse")]
    internal class CorpseLocation : RustPlugin
    {
        [PluginReference] Plugin ZoneManager, AbandonedBases, RaidableBases;

        public enum AmountType { Double, Float, Int }
        public enum PlayerType { BasePlayer, String, ULong }
        private const string UsePerm = "corpselocation.use";
        private const string TPPerm = "corpselocation.tp";
        private const string VIPPerm = "corpselocation.vip";
        private const string AdminPerm = "corpselocation.admin";
        private const string NoCostPerm = "corpselocation.nocost";
        private Dictionary<string, Timer> ActiveTimers = new();
        private Dictionary<string, Vector3> ReturnLocations = new();

        #region Data
        public class StoredData
        {
            public Dictionary<string, string> deaths = new();
            public Dictionary<string, int> teleportsRemaining = new();

            public StoredData() { }
        }

        private StoredData storedData = new();

        private void NewData()
        {
            storedData = new();
            SaveData();
        }

        private void LoadData()
        {
            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch { }

            if (storedData == null || storedData.deaths == null)
            {
                Puts("Corrupted data -- generating new data file");
                NewData();
            }
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, storedData);
        }
        #endregion Data

        #region Hooks
        private void OnNewSave(string filename)
        {
            NewData();
        }

        private void OnServerSave()
        {
            timer.Once(15f, SaveData);
        }

        private void Unload()
        {
            SaveData();
        }

        private void OnServerInitialized()
        {
            permission.RegisterPermission(UsePerm, this);
            permission.RegisterPermission(TPPerm, this);
            permission.RegisterPermission(VIPPerm, this);
            permission.RegisterPermission(AdminPerm, this);
            LoadData();
            StartResetTimer();
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, UsePerm))
            {
                return;
            }
            if (storedData.deaths.TryGetValue(player.UserIDString, out var location))
            {
                SendCorpseLocation(player, location.ToVector3());
            }
        }

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (player == null || player.IsDestroyed || !player.userID.IsSteamId())
            {
                return;
            }
            storedData.deaths[player.UserIDString] = player.transform.position.ToString();
            Puts($"{player.displayName} ({player.UserIDString}) died at {player.transform.position}");
        }

        private void OnEntitySpawned(PlayerCorpse corpse)
        {
            if (corpse == null)
            {
                return;
            }

            string userid = corpse.playerSteamID.ToString();

            if (!userid.IsSteamId())
            {
                return;
            }

            if (ActiveTimers.Remove(userid, out Timer t))
            {
                t.Destroy();
            }

            ActiveTimers[userid] = timer.Repeat(1, config.trackTime, () =>
            {
                if (corpse != null && !corpse.IsDestroyed)
                {
                    storedData.deaths[userid] = corpse.transform.position.ToString();
                }
            });
        }

        private void OnUserPermissionGranted(string id, string permName)
        {
            if (permName != VIPPerm)
            {
                return;
            }

            storedData.teleportsRemaining[id] = storedData.teleportsRemaining.TryGetValue(id, out var n) ? n + config.viptpAmount - config.tpAmount : config.viptpAmount;
        }

        private void OnUserGroupAdded(string id, string groupName)
        {
            foreach (var permName in permission.GetGroupPermissions(groupName))
            {
                if (permName == VIPPerm)
                {
                    OnUserPermissionGranted(id, permName);
                }
            }
        }

        #endregion Hooks

        #region Helpers

        private void StartResetTimer()
        {
            if (!TimeSpan.TryParse(config.resetTime, out var resetTime))
            {
                Puts("Invalid resetTime format. Using midnight as default.");
                resetTime = TimeSpan.Zero;
            }

            DateTime now = DateTime.Now;
            DateTime nextReset = now.Date + resetTime;

            if (nextReset <= now)
                nextReset = nextReset.AddDays(1);

            float time = (float)(nextReset - now).TotalSeconds;

            timer.Once(time, () =>
            {
                foreach (var playerId in storedData.teleportsRemaining.Keys.ToList())
                {
                    bool isVIP = permission.UserHasPermission(playerId, VIPPerm);
                    storedData.teleportsRemaining[playerId] = isVIP ? config.viptpAmount : config.tpAmount;
                }

                SaveData();
                Puts("Daily teleports were reset.");

                foreach (var player in BasePlayer.activePlayerList)
                {
                    Message(player, "DailyReset");
                }

                StartResetTimer();
            });
        }

        public bool CanPlayerTeleport(BasePlayer player, Vector3 to)
        {
            if (config.blockToZM && ZoneManager != null && Convert.ToBoolean(ZoneManager?.Call("HasPlayerFlag", player, "notp")))
            {
                Message(player, "TeleportBlockedCorpse");
                return false;
            }

            if (config.blockFromBuildBlocked && player.IsBuildingBlocked())
            {
                Message(player, "TeleportBlockedFrom");
                return false;
            }

            if (config.blockToBuildBlocked && player.IsBuildingBlocked(to, player.transform.rotation, player.bounds))
            {
                Message(player, "TeleportBlockedTo");
                return false;
            }

            if (config.ignoreRB && RaidableBases != null && Convert.ToBoolean(RaidableBases?.Call("EventTerritory", to)))
            {
                return true;
            }

            if (config.ignoreAB && AbandonedBases != null && Convert.ToBoolean(AbandonedBases?.Call("EventTerritory", to)))
            {
                return true;
            }

            var ret = Interface.CallHook("CanTeleport", player, to);

            if (ret is string str)
            {
                Player.Message(player, str);
                return false;
            }

            return true;
        }

        private object OnBlockRaidableBasesTeleport(BasePlayer player, Vector3 to) => config.ignoreRB ? true : (object)null;

        private object OnBlockAbandonedBasesTeleport(BasePlayer player, Vector3 to) => config.ignoreAB ? true : (object)null;

        private static string PositionToGrid(Vector3 position) => MapHelper.PositionToString(position);

        private void SendCorpseLocation(BasePlayer player, Vector3 location)
        {
            int DistanceToCorpse = Mathf.FloorToInt(Vector3.Distance(player.transform.position, location));
            if (config.showGrid)
            {
                Message(player, "YouDiedGrid", DistanceToCorpse, PositionToGrid(location));
            }
            else Message(player, "YouDied", DistanceToCorpse);
        }

        private List<BasePlayer> GetPlayers(string NameOrID)
        {
            return BasePlayer.allPlayerList.Where(target =>
            {
                if (target == null)
                {
                    return false;
                }
                return target.UserIDString == NameOrID || target.displayName.Contains(NameOrID, StringComparison.OrdinalIgnoreCase);
            }).ToList();
        }

        #endregion Helpers

        #region Commands
        [ChatCommand("where")]
        private void whereCommand(BasePlayer player, string command, string[] args)
        {
            string PlayerID = player.UserIDString;
            if (args.Contains("tp") && permission.UserHasPermission(PlayerID, TPPerm))
            {
                int TPAllowed = config.tpAmount;
                if (permission.UserHasPermission(PlayerID, VIPPerm))
                {
                    TPAllowed = config.viptpAmount;
                }
                if (!storedData.teleportsRemaining.TryGetValue(PlayerID, out int remainingTeleports) || remainingTeleports > TPAllowed)
                {
                    remainingTeleports = TPAllowed;
                    storedData.teleportsRemaining[PlayerID] = remainingTeleports;
                    SaveData();
                }
                if (!storedData.deaths.TryGetValue(PlayerID, out string location))
                {
                    Message(player, "UnknownLocation");
                    return;
                }
                if (config.blockFromZM && Convert.ToBoolean(ZoneManager?.CallHook("HasPlayerFlag", player, "notp")))
                {
                    Message(player, "TeleportBlockedPlayer");
                    return;
                }
                Vector3 destination = location.ToVector3();
                if (!CanPlayerTeleport(player, destination))
                {
                    return;
                }
                if (TPAllowed > 0 && remainingTeleports <= 0)
                {
                    Message(player, "OutOfTeleports");
                }
                else
                {
                    float tpCd = config.tpCountdown;
                    if (tpCd > 0)
                    {
                        Message(player, "TeleportingIn", tpCd);
                    }
                    timer.Once(tpCd, () =>
                    {
                        if (!CanPlayerTeleport(player, destination))
                        {
                            return;
                        }
                        if (!Teleport(player, destination, IsFree(player, args))) return;
                        Vector3 originalpos = player.transform.position;
                        player.Invoke(() =>
                        {
                            if (config.blockToZM && Convert.ToBoolean(ZoneManager?.Call("HasPlayerFlag", player, "notp")))
                            {
                                player.Teleport(originalpos);
                                Message(player, "TeleportBlockedCorpse");
                                return;
                            }
                            Message(player, "ArrivedAtYourCorpse");
                            if (config.allowReturn)
                            {
                                ReturnLocations[PlayerID] = originalpos;
                                Message(player, "ReturnAvailable");
                            }
                            if (TPAllowed > 0)
                            {
                                storedData.teleportsRemaining[PlayerID] = --remainingTeleports;
                                SaveData();
                                Message(player, "TeleportsRemaining", remainingTeleports);
                            }
                        }, 0.1f);
                    });
                }
                return;
            }
            if (permission.UserHasPermission(PlayerID, UsePerm))
            {
                if (storedData.deaths.TryGetValue(player.UserIDString, out var location))
                {
                    SendCorpseLocation(player, location.ToVector3());
                }
                else Message(player, "UnknownLocation");
                if (storedData.teleportsRemaining.ContainsKey(player.UserIDString))
                {
                    Message(player, "TeleportsRemaining", storedData.teleportsRemaining[PlayerID]);
                }
                else
                {
                    int TPAllowed = config.tpAmount;
                    if (permission.UserHasPermission(PlayerID, VIPPerm))
                    {
                        TPAllowed = config.viptpAmount;
                    }
                    Message(player, "TeleportsRemaining", TPAllowed);
                }
            }
            else Message(player, "NotAllowed");
        }

        private bool TryPay(BasePlayer player, PaymentMethod m) => m switch
        {
            { IsEnabled: false } => true,

            _ when player == null => false,

            _ => TryPay(player, m.PlayerType, m.AmountType, m.Amount, m.PluginName, m.BalanceHook, m.WithdrawHook, m.CostFormat)
        };

        private bool TryPay(BasePlayer player, PlayerType playerType, AmountType amountType, double amount, string pluginName, string balanceHook, string withdrawHook, string formattedCost)
        {
            Plugin plugin = plugins.Find(pluginName);

            if (plugin == null || !plugin.IsLoaded)
            {
                return true;
            }

            object amountObj = amountType switch
            {
                AmountType.Double => (object)(double)amount,
                AmountType.Float => (object)(float)amount,
                AmountType.Int or _ => (object)(int)amount
            };

            object userObj = playerType switch
            {
                PlayerType.BasePlayer => player,
                PlayerType.String => player.UserIDString,
                PlayerType.ULong or _ => (ulong)player.userID,
            };

            double balance = Convert.ToDouble(plugin.Call(balanceHook, userObj));

            if (string.IsNullOrEmpty(formattedCost))
            {
                formattedCost = amountObj.ToString();
            }
            else
            {
                formattedCost = formattedCost.Replace("{cost}", amountObj.ToString());
            }

            switch (balance >= amount)
            {
                case true:
                    plugin.Call(withdrawHook, userObj, amountObj);
                    Message(player, "Withdrawn", formattedCost);
                    return true;

                case false:
                    Message(player, "NotWithdrawn", formattedCost);
                    return false;
            }
        }

        [HookMethod("Teleport")]
        public bool Teleport(BasePlayer player, Vector3 to, bool free = false)
        {
            if (!free && !config.Payments.All(m => TryPay(player, m)))
            {
                return false;
            }

            Vector3 from = player.transform.position;

            try
            {
                player.UpdateActiveItem(default);
                player.EnsureDismounted();
                player.Server_CancelGesture();

                if (player.HasParent())
                {
                    player.SetParent(null, true, true);
                }

                if (player.IsConnected)
                {
                    player.EndLooting();
                    StartSleeping(player);
                }

                player.RemoveFromTriggers();
                player.Teleport(to);

                if (player.IsConnected && !Net.sv.visibility.IsInside(player.net.group, to))
                {
                    player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
                    player.ClientRPC(RpcTarget.Player("StartLoading", player));
                    player.SendEntityUpdate();

                    if (!player.limitNetworking)
                    {
                        player.UpdateNetworkGroup();
                        player.SendNetworkUpdateImmediate(false);
                    }
                }
            }
            finally
            {
                if (!player.limitNetworking)
                {
                    player.ForceUpdateTriggers();
                }
            }

            Interface.CallHook("OnPlayerTeleported", player, from, to);

            return true;
        }

        public void StartSleeping(BasePlayer player)
        {
            if (!player.IsSleeping())
            {
                Interface.CallHook("OnPlayerSleep", player);
                player.SetPlayerFlag(BasePlayer.PlayerFlags.Sleeping, true);
                player.sleepStartTime = Time.time;
                BasePlayer.sleepingPlayerList.Add(player);
                player.CancelInvoke("InventoryUpdate");
                player.CancelInvoke("TeamUpdate");
            }
        }

        [ChatCommand("return")]
        private void returnCommand(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, TPPerm))
            {
                if (!ReturnLocations.TryGetValue(player.UserIDString, out var destination))
                {
                    Message(player, "ReturnUnavailable");
                    return;
                }
                if (!CanPlayerTeleport(player, destination))
                {
                    return;
                }
                if (!Teleport(player, destination, IsFree(player, args))) return;
                Message(player, "ReturnUsed");
                ReturnLocations.Remove(player.UserIDString);
            }
            else Message(player, "NotAllowed");
        }

        [ChatCommand("tpcorpse")]
        private void tpCommand(BasePlayer player, string command, string[] args)
        {
            if (permission.UserHasPermission(player.UserIDString, AdminPerm))
            {
                if (args.Length == 0)
                {
                    Message(player, "NeedTarget");
                    return;
                }
                var NameOrID = args[0] == "nocost" && args.Length > 1 ? args[1] : args[0];
                var FoundPlayers = GetPlayers(NameOrID);
                if (FoundPlayers.Count == 0)
                {
                    Message(player, "InvalidPlayer", NameOrID);
                    return;
                }
                var target = FoundPlayers[0];
                if (storedData.deaths.TryGetValue(target.UserIDString, out string value))
                {
                    Vector3 destination = value.ToVector3();
                    if (!Teleport(player, destination, IsFree(player, args))) return;
                    Message(player, "ArrivedAtTheCorpse", target.displayName);
                }
                else Message(player, "UnknownLocationTarget", target.displayName);
            }
            else Message(player, "NotAllowed");
        }

        private bool IsFree(BasePlayer player, string[] args) => permission.UserHasPermission(player.UserIDString, NoCostPerm) || player.IsAdmin && config.nocost && args.Contains("nocost");

        #endregion Commands

        #region Config

        protected override void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                ["YouDied"] = "Your corpse was last seen {0} meters from here.",
                ["YouDiedGrid"] = "Your corpse was last seen {0} meters from here, in {1}.",
                ["TeleportingIn"] = "Teleporting to your corpse in {0} second(s).",
                ["TeleportBlockedCorpse"] = "Your corpse is in a restricted area, preventing teleportation.",
                ["TeleportBlockedPlayer"] = "You are not allowed to teleport from here.",
                ["TeleportBlockedTo"] = "You cannot teleport into a building blocked area.",
                ["TeleportBlockedFrom"] = "You cannot teleport from a building blocked area.",
                ["ArrivedAtYourCorpse"] = "You have arrived at your corpse.",
                ["ArrivedAtTheCorpse"] = "You have arrived at the corpse of {0}.",
                ["ReturnAvailable"] = "You can use <color=#ffa500ff>/return</color> to return to your initial location.",
                ["ReturnUnavailable"] = "You don't have a location set to return to.",
                ["ReturnUsed"] = "You have successfully returned to your initial location.",
                ["OutOfTeleports"] = "You have no more teleports left today.",
                ["TeleportsRemaining"] = "You have {0} teleports remaining today.",
                ["UnknownLocation"] = "Your last death location is unknown.",
                ["UnknownLocationTarget"] = "{0}'s last death location is unknown.",
                ["NeedTarget"] = "You need to specify a player to teleport to the corpse of, using either their name or steam id.",
                ["InvalidPlayer"] = "{0} is not part of a known player's name/id.",
                ["NotAllowed"] = "You do not have permission to use that command.",
                ["NotWithdrawn"] = "You do not have <color=#FFFF00>{0}</color> to pay for this corpse teleport!",
                ["Withdrawn"] = "You have paid <color=#FFFF00>{0}</color> for this corpse teleport!",
                ["DailyReset"] = "Daily teleports were reset."
            };

            lang.RegisterMessages(messages, this);
        }

        private void Message(BasePlayer player, string key, params object[] args)
        {
            if (!player.IsValid())
            {
                return;
            }

            string message = lang.GetMessage(key, this, player.UserIDString);

            if (string.IsNullOrEmpty(message))
            {
                return;
            }

            Player.Message(player, args.Length == 0 ? message : string.Format(message, args), config.steamId);
        }

        private Configuration config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Teleport payment methods", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<PaymentMethod> Payments;

            [JsonProperty(PropertyName = "Show grid location")]
            public bool showGrid = true;

            [JsonProperty(PropertyName = "Track a corpse's location for x seconds")]
            public int trackTime = 30;

            [JsonProperty(PropertyName = "Allow teleporting to own corpse x times per day (0 for unlimited)")]
            public int tpAmount = 5;

            [JsonProperty(PropertyName = "Allow teleporting to own corpse x times per day (0 for unlimited), for VIPs")]
            public int viptpAmount = 10;

            [JsonProperty(PropertyName = "Allow returning to original location after teleporting")]
            public bool allowReturn = false;

            [JsonProperty(PropertyName = "Countdown until teleporting to own corpse (0 for instant tp)")]
            public float tpCountdown = 5f;

            [JsonProperty(PropertyName = "Block teleports into Zone Manager's tp blocked zones")]
            public bool blockToZM = true;

            [JsonProperty(PropertyName = "Block teleports from Zone Manager's tp blocked zones")]
            public bool blockFromZM = true;

            [JsonProperty(PropertyName = "Block teleports into building blocked areas")]
            public bool blockToBuildBlocked = false;

            [JsonProperty(PropertyName = "Block teleports from building blocked areas")]
            public bool blockFromBuildBlocked = false;

            [JsonProperty(PropertyName = "Ignore Abandoned Bases")]
            public bool ignoreAB;

            [JsonProperty(PropertyName = "Ignore Raidable Bases")]
            public bool ignoreRB;

            [JsonProperty(PropertyName = "Reset players' remaining teleports at this time (HH:mm:ss format)")]
            public string resetTime = "00:00:00";

            [JsonProperty(PropertyName = "Chat steam id")]
            public ulong steamId;

            [JsonProperty(PropertyName = "Allow admins to specify 'nocost' in commands")]
            public bool nocost;

            [JsonProperty(PropertyName = "ServerRewards Cost", NullValueHandling = NullValueHandling.Ignore)]
            public int? SRC { get; set; } = null;

            [JsonProperty(PropertyName = "Economics Cost", NullValueHandling = NullValueHandling.Ignore)]
            public double? EC { get; set; } = null;
        }

        public class PaymentMethod
        {
            [JsonProperty(PropertyName = "Player Type (0 = BasePlayer, 1 = String, 2 = ULong)")]
            public PlayerType PlayerType;
            [JsonProperty(PropertyName = "Amount Type (0 = Double, 1 = Float, 2 = Int)")]
            public AmountType AmountType;
            public bool Enabled;
            [JsonProperty(PropertyName = "Cost")]
            public double Amount;
            public string PluginName;
            public string BalanceHook;
            public string WithdrawHook;
            public string CostFormat;
            public PaymentMethod(bool enabled, PlayerType playerType, AmountType amountType, double amount, string pluginName, string balanceHook, string withdrawHook, string costFormat)
            {
                Enabled = enabled;
                PlayerType = playerType;
                AmountType = amountType;
                Amount = amount;
                PluginName = pluginName;
                BalanceHook = balanceHook;
                WithdrawHook = withdrawHook;
                CostFormat = costFormat;
            }
            internal bool IsEnabled => Enabled && Amount > 0 && !string.IsNullOrEmpty(PluginName) && !string.IsNullOrEmpty(BalanceHook) && !string.IsNullOrEmpty(WithdrawHook);
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            canSaveConfig = false;
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                canSaveConfig = true;
            }
            catch (Exception ex)
            {
                Puts(ex.ToString());
                LoadDefaultConfig();
            }
            if (config.Payments == null)
            {
                double economics = config.EC.HasValue ? config.EC.Value : 0;
                int rewards = config.SRC.HasValue ? config.SRC.Value : 0;
                config.Payments = new()
                {
                    new(economics != 0, PlayerType.ULong, AmountType.Double, economics, "Economics", "Balance", "Withdraw", "${cost}"),
                    new(economics != 0, PlayerType.ULong, AmountType.Int, economics, "BankSystem", "Balance", "Withdraw", "${cost}"),
                    new(economics != 0, PlayerType.ULong, AmountType.Int, economics, "IQEconomic", "API_GET_BALANCE", "API_REMOVE_BALANCE", "${cost}"),
                    new(rewards != 0, PlayerType.ULong, AmountType.Int, rewards, "ServerRewards", "CheckPoints", "TakePoints", "{cost} RP"),
                };
                config.EC = null;
                config.SRC = null;
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new();
        }

        private bool canSaveConfig = true;

        protected override void SaveConfig()
        {
            if (canSaveConfig)
            {
                Config.WriteObject(config);
            }
        }
        #endregion
    }
}