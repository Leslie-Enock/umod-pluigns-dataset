using Oxide.Core.Plugins;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
#pragma warning disable 0649
namespace Oxide.Plugins
{
    [Info("Player Loot Logs", "hizentv + Irish", "1.5.2")]
    [Description("Detailed logging for player looting events. Optionally log to Console, File, and/or Discord.")]
    class PlayerLootLogs : RustPlugin
    {
        #region Plugin Reference
        [PluginReference] private Plugin DiscordLogger;
        #endregion
        #region Configuration
        class PlayerLootLogConfig
        {
            public enum LootTarget
            {
                Player,
                PlayerTeam,
                PlayerCorpse
            }

            public Dictionary<LootTarget, bool> Tracking { get; set; }
            public LogSource Console { get; set; } = new LogSource();
            public FileLogSource File { get; set; } = new FileLogSource();
            public DiscordLogSource Discord { get; set; } = new DiscordLogSource();

            public PlayerLootLogConfig InitDefaults()
            {
                if (Tracking == null)
                {
                    Tracking = new Dictionary<LootTarget, bool>();
                }
                foreach (LootTarget target in Enum.GetValues(typeof(LootTarget)))
                {
                    if (!Tracking.ContainsKey(target))
                    {
                        Tracking[target] = true;
                    }
                }
                Console = Console ?? new LogSource();
                File = File ?? new FileLogSource();
                Discord = Discord ?? new DiscordLogSource();
                return this;
            }

            public bool IsTracking(BasePlayer player, BaseEntity target)
            {
                if (target is NPCPlayer || target is NPCPlayerCorpse)
                {
                    return false;
                }
                if (target is BasePlayer)
                {
                    return Tracking[LootTarget.Player] && IsTrackingTeam(player, target);
                }
                else if (target is PlayerCorpse)
                {
                    return Tracking[LootTarget.PlayerCorpse] && IsTrackingTeam(player, target);
                }
                return false;
            }

            private bool IsTrackingTeam(BasePlayer player, BaseEntity target) => Tracking[LootTarget.PlayerTeam] ||
                !(player.Team?.members?.Contains(new PlayerInfo(target).SteamID) ?? false);

            public class LogSource
            {
                const string defaultPrefix = "[{0:yyyy/MM/dd HH:mm:ss}] ";
                public bool Enabled { get; set; }
                public string FormatLootGive { get; set; } = defaultPrefix + "{8} gave {3} ({4}) to {1}";
                public string FormatLootTake { get; set; } = defaultPrefix + "{8} looted {3} ({4}) from {1}";
                public string FormatLootDropSelf { get; set; } =
                    defaultPrefix + "{8} dropped {3} ({4}) while looting {1}";
                public string FormatLootDropTarget { get; set; } = defaultPrefix + "{8} dropped {3} ({4}) from {1}";

                public enum LootAction
                {
                    Add,
                    Take,
                    DropSelf,
                    DropTarget
                }

                private string GetFormat(LootAction type)
                {
                    switch (type)
                    {
                        case LootAction.Add: return FormatLootGive;
                        case LootAction.Take: return FormatLootTake;
                        case LootAction.DropSelf: return FormatLootDropSelf;
                        case LootAction.DropTarget: return FormatLootDropTarget;
                        default: throw new ArgumentException($"Type '{type}' not supported.", nameof(type));
                    }
                }

                public string FormatMessage(LootAction lootType, LootChange change, LootItem item,
                    params PlayerInfo[] looters) => FormatMessage(GetFormat(lootType), change, item, looters);

                private string FormatMessage(string format, LootChange change, LootItem item,
                    params PlayerInfo[] looters) => string.Format(format,
                    change.Timestamp,
                    change.Target.DisplayName,
                    change.Target.SteamID,
                    item.Name,
                    Math.Abs(item.Amount),
                    item.Condition,
                    item.ConditionMax,
                    item.ConditionPercent,
                    string.Join(", ", looters.Select(looter => looter.DisplayName)),
                    string.Join(", ", looters.Select(looter => looter.SteamIDString)),
                    string.Join(", ", looters.Select(looter => $"{looter.DisplayName}{looter.SteamID}]")),
                    change.Target.Location.x.ToString("0"),
                    change.Target.Location.y.ToString("0"),
                    change.Target.Location.z.ToString("0")
                );
            }

            public class FileLogSource : LogSource
            {
                public string FileNameFormat { get; set; } = "{0:yyyyMMdd}";
                public int DelaySeconds { get; set; } = 60;
            }

            public class DiscordLogSource : LogSource
            {
                public string WebHookUrl { get; set; } = string.Empty;
            }
        }

        PlayerLootLogConfig config;

        protected override void LoadDefaultConfig() =>
            Config.WriteObject(new PlayerLootLogConfig().InitDefaults(), true);
        #endregion
        #region System Hooks

        void Init()
        {
            config = Config.ReadObject<PlayerLootLogConfig>().InitDefaults();
            if (!config.File.Enabled && !config.Discord.Enabled && !config.Console.Enabled)
            {
                Puts("No logging configuration is enabled.");
                return;
            }
            if (config.File.Enabled)
            {
                if (string.IsNullOrWhiteSpace(config.File.FileNameFormat))
                {
                    Puts(
                        $"Setting '{nameof(PlayerLootLogConfig.File)}:{nameof(PlayerLootLogConfig.FileLogSource.FileNameFormat)}' is empty, disabling file logging.");
                    config.File.Enabled = false;
                }
                else if (config.File.DelaySeconds <= 0)
                {
                    Puts(
                        $"Setting '{nameof(PlayerLootLogConfig.File)}:{nameof(PlayerLootLogConfig.FileLogSource.DelaySeconds)}' must be greater than 0, disabling file logging.");
                }
                else
                {
                    timer.Every(config.File.DelaySeconds, FlushLogQueue);
                }
            }
            if (config.Discord.Enabled)
            {
                if (string.IsNullOrWhiteSpace(config.Discord.WebHookUrl))
                {
                    Puts(
                        $"Setting '{nameof(PlayerLootLogConfig.Discord)}:{nameof(PlayerLootLogConfig.DiscordLogSource.WebHookUrl)}' is empty, disabling discord logging.");
                    config.Discord.Enabled = false;
                }
            }
            Config.WriteObject(config, true);
        }

        void Unload() => FlushLogQueue();
        #endregion
        #region Logging

        private void LogChange(LootChange change)
        {
            foreach (var looter in change.Looters)
            {
                if (change.ItemsModified[looter.SteamID].Any())
                {
                    foreach (var item in change.ItemsModified[looter.SteamID])
                    {
                        if (change.ItemsModified[change.Target.SteamID].Any(tItem =>
                                tItem.Id == item.Id && tItem.Amount == item.Amount * -1))
                        {
                            if (item.Amount > 0)
                            {
                                SendLog(PlayerLootLogConfig.LogSource.LootAction.Take, change, item, change.Looters);
                            }
                            else
                            {
                                SendLog(PlayerLootLogConfig.LogSource.LootAction.Add, change, item, change.Looters);
                            }
                        }
                        else
                        {
                            SendLog(PlayerLootLogConfig.LogSource.LootAction.DropSelf, change, item, change.Looters);
                        }
                    }
                }
            }
            foreach (var item in change.ItemsModified[change.Target.SteamID])
            {
                if (!change.ItemsModified.Where(im => im.Key != change.Target.SteamID).Any(im =>
                        im.Value.Any(li => li.Id == item.Id && li.Amount == item.Amount * -1)))
                {
                    SendLog(PlayerLootLogConfig.LogSource.LootAction.DropTarget, change, item, change.Looters);
                }
            }
        }

        public ConcurrentQueue<string> FileLogQueue { get; set; } = new ConcurrentQueue<string>();

        private void SendLog(PlayerLootLogConfig.LogSource.LootAction lootType, LootChange change, LootItem item,
            params PlayerInfo[] looters)
        {
            if (config.Console.Enabled)
            {
                Puts(config.Console.FormatMessage(lootType, change, item, looters));
            }
            if (config.File.Enabled)
            {
                FileLogQueue.Enqueue(config.File.FormatMessage(lootType, change, item, looters));
            }
            if (config.Discord.Enabled)
            {
                if (DiscordLogger == null || !DiscordLogger.IsLoaded)
                {
                    Puts($"Plugin {nameof(DiscordLogger)} must be installed in order to enable discord logging.");
                    config.Discord.Enabled = false;
                }
                else if (string.IsNullOrWhiteSpace(config.Discord.WebHookUrl))
                {
                    Puts(
                        $"Setting {nameof(PlayerLootLogConfig.Discord)}:{nameof(PlayerLootLogConfig.DiscordLogSource.WebHookUrl)} must be set to enable discord logging.");
                    config.Discord.Enabled = false;
                }
                else
                {
                    DiscordLogger.Call("DiscordSendMessage",
                        config.Discord.FormatMessage(lootType, change, item, looters), config.Discord.WebHookUrl);
                }
            }
        }

        bool isFlushingLogQueue = false;
        object flushLock = new object();

        private void FlushLogQueue()
        {
            var sb = new StringBuilder();
            string line;
            try
            {
                lock (flushLock)
                {
                    if (isFlushingLogQueue) return;
                    isFlushingLogQueue = true;
                }
                while (FileLogQueue.TryDequeue(out line))
                {
                    sb.AppendLine(line);
                }
                if (sb.Length > 0)
                {
                    LogToFile(string.Format(config.File.FileNameFormat, DateTimeOffset.Now), sb.ToString().Trim(),
                        this, false);
                }
            }
            finally
            {
                isFlushingLogQueue = false;
            }
        }
        #endregion
        #region Player Hooks

        void OnLootEntity(BasePlayer player, BaseEntity entity) => EnterLootEvent(player, entity);

        void OnLootNetworkUpdate(PlayerLoot loot)
        {
            if (loot != null
                && loot.baseEntity != null
                && loot.entitySource != null)
            {
                EnterLootEvent(loot.baseEntity, loot.entitySource);
            }
        }

        void OnInventoryNetworkUpdate(PlayerInventory inventory, ItemContainer container,
            ProtoBuf.UpdateItemContainer updateItemContainer, PlayerInventory.Type type, bool broadcast)
        {
            if (inventory == null)
            {
                return;
            }
            var player = inventory.baseEntity;
            if (player != null && LinkToLootEvent.ContainsKey(player.userID) &&
                TargetLootEvents.ContainsKey(LinkToLootEvent[player.userID]))
            {
                EnterLootEvent(player, TargetLootEvents[LinkToLootEvent[player.userID]].Target);
            }
        }

        void OnPlayerLootEnd(PlayerLoot inventory) => ExitLootEvent(inventory.baseEntity, inventory.entitySource);

        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity) => ExitLootEvent(player, entity);
        #endregion
        #region Loot Events Methods

        private Dictionary<ulong, ulong> LinkToLootEvent { get; set; } = new Dictionary<ulong, ulong>();
        private Dictionary<ulong, LootEvent> TargetLootEvents { get; set; } = new Dictionary<ulong, LootEvent>();

        private void EnterLootEvent(BasePlayer player, BaseEntity target)
        {
            if (!config.IsTracking(player, target))
            {
                return;
            }
            if (target is BasePlayer || target is PlayerCorpse)
            {
                var targetPlayer = new PlayerInfo(target);
                if (targetPlayer.SteamID == player.userID)
                {
                    return;
                }
                if (!config.Tracking[PlayerLootLogConfig.LootTarget.PlayerTeam] && player?.Team?.members != null &&
                    player.Team.members.Contains(targetPlayer.SteamID))
                {
                    return;
                }
                LinkToLootEvent[player.userID] = targetPlayer.SteamID;
                if (TargetLootEvents.ContainsKey(targetPlayer.SteamID))
                {
                    LootChange change;
                    if (TargetLootEvents[targetPlayer.SteamID].RecordChange(player, target, out change))
                    {
                        LogChange(change);
                    }
                }
                else
                {
                    TargetLootEvents[targetPlayer.SteamID] = new LootEvent(player, target);
                }
            }
        }

        private void ExitLootEvent(BasePlayer player, BaseEntity target)
        {
            if (!config.IsTracking(player, target))
            {
                return;
            }
            if (target is BasePlayer || target is PlayerCorpse)
            {
                var targetPlayer = new PlayerInfo(target);
                if (TargetLootEvents.ContainsKey(targetPlayer.SteamID))
                {
                    TargetLootEvents[targetPlayer.SteamID].RemoveLooter(player);
                    if (!TargetLootEvents[targetPlayer.SteamID].Active)
                    {
                        TargetLootEvents.Remove(targetPlayer.SteamID);
                    }
                }
                if (LinkToLootEvent.ContainsKey(player.userID))
                {
                    LinkToLootEvent.Remove(player.userID);
                }
            }
            else
            {
                return;
            }
        }
        #endregion
        #region Data Models (PlayerInfo, LootChange, LootEvent, LootItem, LootTarget)
        private struct PlayerInfo
        {
            public ulong SteamID { get; set; }
            public string SteamIDString => SteamID.ToString();
            public string DisplayName { get; set; }
            public Vector3 Location { get; set; }
            public PlayerInfo(BaseEntity baseEntity)
            {
                if (baseEntity == null)
                {
                    throw new ArgumentNullException(nameof(baseEntity));
                }
                Location = baseEntity.ServerPosition;
                if (baseEntity is BasePlayer)
                {
                    var player = (BasePlayer)baseEntity;
                    SteamID = player.userID;
                    DisplayName = player.displayName;
                }
                else if (baseEntity is PlayerCorpse)
                {
                    var corpse = (PlayerCorpse)baseEntity;
                    SteamID = corpse.playerSteamID;
                    DisplayName = corpse.playerName;
                }
                else
                {
                    throw new ArgumentException(
                        $"Type '{baseEntity.GetType()}' not supported. Argument must be type {nameof(BasePlayer)} or {nameof(PlayerCorpse)}.",
                        nameof(baseEntity));
                }
            }
        }
        private struct LootChange
        {
            public PlayerInfo[] Looters { get; set; }
            public PlayerInfo Target { get; set; }
            public Vector3 Position { get; set; }
            public DateTimeOffset Timestamp { get; set; }
            public Dictionary<ulong, LootItem[]> ItemsModified { get; set; }
            public static LootChange Empty = new LootChange
            {
                Looters = Array.Empty<PlayerInfo>()
            };
            public bool IsEmpty() => Looters.Any();
            public static LootChange FromEvent(LootEvent lootEvent, BaseEntity baseTarget)
            {
                var target = new PlayerInfo(baseTarget);
                var existingTargetItems = lootEvent.Items[target.SteamID];
                var newTargetItems = LootItem.GetItems(baseTarget);
                var targetItemChanges = LootItem.GetChanges(existingTargetItems, newTargetItems);
                if (targetItemChanges.Any())
                {
                    var lootChange = new LootChange
                    {
                        Looters = lootEvent.Looters.Values.Select(player => new PlayerInfo(player)).ToArray(),
                        Target = target,
                        ItemsModified = new Dictionary<ulong, LootItem[]>
                        {
                            [target.SteamID] = targetItemChanges.ToArray()
                        },
                        Position = baseTarget.ServerPosition,
                        Timestamp = DateTimeOffset.Now,
                    };
                    foreach (var player in lootEvent.Looters)
                    {
                        var playerMod =
                            LootItem.GetChanges(lootEvent.Items[player.Key], LootItem.GetItems(player.Value))
                                ?.ToArray() ?? Array.Empty<LootItem>();
                        lootChange.ItemsModified[player.Key] = playerMod;
                    }
                    return lootChange;
                }
                return Empty;
            }
        }
        private class LootEvent
        {
            public Dictionary<ulong, BasePlayer> Looters { get; set; } = new Dictionary<ulong, BasePlayer>();
            public BaseEntity Target { get; set; }
            public Dictionary<ulong, List<LootItem>> Items { get; set; } = new Dictionary<ulong, List<LootItem>>();
            public bool Active => Looters.Count > 0;
            public LootEvent(BasePlayer player, BaseEntity baseTarget)
            {
                Looters = new Dictionary<ulong, BasePlayer>()
                {
                    [player.userID] = player
                };
                Target = baseTarget;
                UpdateItems();
            }
            public bool RecordChange(BasePlayer player, BaseEntity baseTarget, out LootChange lootChange)
            {
                if (!Looters.ContainsKey(player.userID) || !Items.ContainsKey(player.userID))
                {
                    Looters[player.userID] = player;
                    Items[player.userID] = LootItem.GetItems(player);
                    lootChange = LootChange.Empty;
                }
                else
                {
                    lootChange = LootChange.FromEvent(this, baseTarget);
                }
                UpdateItems();
                return lootChange.IsEmpty();
            }
            public void RemoveLooter(BasePlayer player)
            {
                if (Looters.ContainsKey(player.userID))
                {
                    Looters.Remove(player.userID);
                }
                if (Items.ContainsKey(player.userID))
                {
                    Items.Remove(player.userID);
                }
            }
            private void UpdateItems()
            {
                Items[new PlayerInfo(Target).SteamID] = LootItem.GetItems(Target);
                foreach (var looter in Looters)
                {
                    Items[looter.Key] = LootItem.GetItems(looter.Value);
                }
            }
        }

        private struct LootEntity
        {
            public PlayerInfo Player { get; set; }
            public List<LootItem> Items { get; set; }
            public LootEntity(BaseEntity baseEntity)
            {
                Player = new PlayerInfo(baseEntity);
                Items = LootItem.GetItems(baseEntity);
            }
        }

        private struct LootItem
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Amount { get; set; }
            public float Condition { get; set; }
            public float ConditionMax { get; set; }
            public float ConditionPercent => ConditionMax == 0 ? 100 : Condition / ConditionMax * 100;
            public bool CanStack { get; set; }
            public LootItem(Item baseItem)
            {
                Id = baseItem.info.itemid;
                Name = baseItem.info.name;
                Amount = baseItem.amount;
                Condition = baseItem.condition;
                ConditionMax = baseItem.info.condition.max;
                CanStack = baseItem.info.stackable > 1 || Condition == baseItem.maxCondition;
            }
            public static List<LootItem> GetItems(BaseEntity baseEntity)
            {
                if (baseEntity == null)
                {
                    throw new ArgumentNullException(nameof(baseEntity));
                }
                if (baseEntity is BasePlayer)
                {
                    var player = (BasePlayer)baseEntity;
                    var items = player.inventory.containerMain.itemList
                        .Concat(player.inventory.containerBelt.itemList)
                        .Concat(player.inventory.containerWear.itemList);
                    return Merge(items.Select(item => new LootItem(item)));
                }
                else if (baseEntity is PlayerCorpse)
                {
                    return FromContainers(((PlayerCorpse)baseEntity).containers);
                }
                else
                {
                    throw new ArgumentException($"Type '{baseEntity.GetType()}' not supported. Argument must be type {nameof(BasePlayer)} or {nameof(PlayerCorpse)}.", nameof(baseEntity));
                }
            }

            public static List<LootItem> GetChanges(IEnumerable<LootItem> existingItems, IEnumerable<LootItem> newItems)
            {
                var previous = Merge(existingItems);
                var current = Merge(newItems);
                var modified = new List<LootItem>();
                foreach (var item in current)
                {
                    var modItem = item;
                    var prevItem = previous.Where(pItem => pItem.Id == item.Id && pItem.Condition == item.Condition);
                    if (!prevItem.Any())
                    {
                        modified.Add(modItem);
                    }
                    else
                    {
                        modItem.Amount -= prevItem.First().Amount;
                        if (modItem.Amount != 0)
                        {
                            modified.Add(modItem);
                        }
                    }
                }
                foreach (var item in previous)
                {
                    if (!current.Any(cItem => cItem.Id == item.Id && cItem.Condition == item.Condition))
                    {
                        var modItem = item;
                        modItem.Amount *= -1;
                        modified.Add(modItem);
                    }
                }
                return modified;
            }

            public static List<LootItem> FromContainers(params ItemContainer[] containers)
            {
                if (containers == null)
                {
                    return new List<LootItem>();
                }
                else
                {
                    return Merge(containers.SelectMany(container =>
                        container.itemList.Select(item => new LootItem(item))));
                }
            }

            public static List<LootItem> Merge(IEnumerable<LootItem> items)
            {
                var stacks = new Dictionary<int, LootItem>();
                var final = new List<LootItem>();
                foreach (var item in items)
                {
                    if (item.CanStack)
                    {
                        if (stacks.ContainsKey(item.Id))
                        {
                            var currentStack = stacks[item.Id];
                            currentStack.Amount += item.Amount;
                            stacks[item.Id] = currentStack;
                        }
                        else
                        {
                            stacks[item.Id] = item;
                        }
                    }
                    else
                    {
                        final.Add(item);
                    }
                }
                final.AddRange(stacks.Values);
                return final;
            }
        }

        private struct LootIndex<T>
        {
            public Dictionary<T, string[]> Entries { get; set; }
            public void AddEntry(T key, string logId)
            {
                var entries = new List<string>(Entries.ContainsKey(key) ? Entries[key] : Array.Empty<string>());
                entries.Add(logId);
                Entries[key] = entries.Distinct().ToArray();
            }
        }
        #endregion
    }
}
