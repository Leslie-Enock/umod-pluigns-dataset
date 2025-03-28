using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust;
using Time = Oxide.Core.Libraries.Time;

namespace Oxide.Plugins;

[Info("Statistics DB", "misticos", "1.2.7")]
[Description("Statistics database for developers")]
internal class StatisticsDB : CovalencePlugin
{
    #region Variables

    [PluginReference]
    // ReSharper disable InconsistentNaming
    private Plugin ConnectionDB = null, PlayerDatabase = null;
    // ReSharper restore InconsistentNaming

    private static PluginData _data = new();

    private static StatisticsDB _ins;

    private static readonly Time Time = GetLibrary<Time>();

    #endregion

    #region Configuration

    private static Configuration _config = new();

    private class Configuration
    {
        [JsonProperty(PropertyName = "Debug")]
        public bool Debug { get; set; } = false;

        [JsonProperty(PropertyName = "Inactive Entry Lifetime")]
        public uint Lifetime { get; set; } = 259200;

        [JsonProperty(PropertyName = "Collect Joins")]
        public bool CollectJoins { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Leaves")]
        public bool CollectLeaves { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Kills")]
        public bool CollectKills { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Deaths")]
        public bool CollectDeaths { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Suicides")]
        public bool CollectSuicides { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Shots")]
        public bool CollectShots { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Headshots")]
        public bool CollectHeadshots { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Experiments")]
        public bool CollectExperiments { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Recoveries")]
        public bool CollectRecoveries { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Voice Bytes")]
        public bool CollectVoiceBytes { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Wounded Times")]
        public bool CollectWoundedTimes { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Crafted Items")]
        public bool CollectCraftedItems { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Repaired Items")]
        public bool CollectRepairedItems { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Lift Usages")]
        public bool CollectLiftUsages { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Wheel Spins")]
        public bool CollectWheelSpins { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Hammer Hits")]
        public bool CollectHammerHits { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Explosives Thrown")]
        public bool CollectExplosivesThrown { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Weapon Reloads")]
        public bool CollectWeaponReloads { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Rockets Launched")]
        public bool CollectRocketsLaunched { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Collectible Pickups")]
        public bool CollectCollectiblePickups { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Plant Pickups")]
        public bool CollectPlantPickups { get; set; } = true;

        [JsonProperty(PropertyName = "Collect Gathered")]
        public bool CollectGathered { get; set; } = true;
    }

    protected override void LoadConfig()
    {
        base.LoadConfig();
        try
        {
            _config = Config.ReadObject<Configuration>();
            if (_config == null) throw new Exception();
        }
        catch
        {
            PrintError("Your configuration file contains an error. Using default configuration values.");
            LoadDefaultConfig();
        }
    }

    protected override void LoadDefaultConfig() => _config = new Configuration();

    protected override void SaveConfig() => Config.WriteObject(_config);

    #endregion

    #region Work with Data

    private void SaveData()
    {
        foreach (var kvp in _data.Statistics)
        {
            SaveData(kvp.Key);
        }
    }

    private void SaveData(ulong id)
    {
        PlayerDatabase.Call("SetPlayerData", id.ToString(), Name, _data.Statistics[id], true);
    }

    private void LoadData(ulong id)
    {
        var success = PlayerDatabase.Call("GetPlayerDataRaw", id.ToString(), Name);
        if (success is string result)
        {
            _data.Statistics[id] = JsonConvert.DeserializeObject<PlayerStats>(result);
        }
    }

    private class PluginData
    {
        public Dictionary<ulong, PlayerStats> Statistics = new();
    }

    private class PlayerStats
    {
        public uint LastUpdate;

        public uint Joins;

        public uint Leaves;

        public uint Kills;

        public uint Deaths;

        public uint Suicides;

        public uint Shots;

        public uint Headshots;

        public uint Experiments;

        public uint Recoveries;

        public uint VoiceBytes;

        public uint WoundedTimes;

        public uint CraftedItems;

        public uint RepairedItems;

        public uint LiftUsages;

        public uint WheelSpins;

        public uint HammerHits;

        public uint ExplosivesThrown;

        public uint WeaponReloads;

        public uint RocketsLaunched;

        public uint SecondsPlayed;

        public List<string> Names;

        // ReSharper disable once InconsistentNaming
        public List<string> IPs;

        public List<uint> TimeStamps;

        public Dictionary<string, uint> CollectiblePickups = new();

        public Dictionary<string, uint> PlantPickups = new();

        public Dictionary<string, uint> Gathered = new();

        public PlayerStats()
        {
        }

        internal PlayerStats(ulong id)
        {
            if (!id.IsSteamId())
                return;

            Update();
            _data.Statistics.Add(id, this);
        }

        public void Update() => LastUpdate = Time.GetUnixTimestamp();

        public static PlayerStats Find(ulong id)
        {
            if (_data.Statistics.TryGetValue(id, out var statistics))
            {
                return statistics;
            }

            _ins.LoadData(id);

            return _data.Statistics.TryGetValue(id, out statistics) ? statistics : null;
        }
    }

    #endregion

    #region Hooks

    private void Init()
    {
        _ins = this;

        if (!_config.CollectJoins)
            Unsubscribe(nameof(OnPlayerConnected));

        if (!_config.CollectLeaves)
            Unsubscribe(nameof(OnPlayerDisconnected));

        if (!_config.CollectExperiments)
            Unsubscribe(nameof(CanExperiment));

        if (!_config.CollectHeadshots)
            Unsubscribe(nameof(OnEntityTakeDamage));

        if (!_config.CollectWoundedTimes)
            Unsubscribe(nameof(OnPlayerWound));

        if (!_config.CollectRecoveries)
            Unsubscribe(nameof(OnPlayerRecover));

        if (!_config.CollectVoiceBytes)
            Unsubscribe(nameof(OnPlayerVoice));

        if (!_config.CollectCraftedItems)
            Unsubscribe(nameof(OnItemCraftFinished));

        if (!_config.CollectRepairedItems)
            Unsubscribe(nameof(OnItemRepair));

        if (!_config.CollectLiftUsages)
            Unsubscribe(nameof(OnLiftUse));

        if (!_config.CollectWheelSpins)
            Unsubscribe(nameof(OnSpinWheel));

        if (!_config.CollectHammerHits)
            Unsubscribe(nameof(OnHammerHit));

        if (!_config.CollectExplosivesThrown)
            Unsubscribe(nameof(OnExplosiveThrown));

        if (!_config.CollectWeaponReloads)
            Unsubscribe(nameof(OnReloadWeapon));

        if (!_config.CollectRocketsLaunched)
            Unsubscribe(nameof(OnRocketLaunched));

        if (!_config.CollectShots)
            Unsubscribe(nameof(OnWeaponFired));

        if (!_config.CollectCollectiblePickups)
            Unsubscribe(nameof(OnCollectiblePickup));

        if (!_config.CollectPlantPickups)
            Unsubscribe(nameof(OnGrowableGather));

        if (!_config.CollectGathered)
            Unsubscribe(nameof(OnDispenserGather));
    }

    private void OnServerInitialized()
    {
        var playersCount = BasePlayer.activePlayerList.Count;
        for (var i = 0; i < playersCount; i++)
        {
            OnPlayerConnected(BasePlayer.activePlayerList[i]);
        }
    }

    private void OnServerSave()
    {
        var current = Time.GetUnixTimestamp();
        var data = _data.Statistics.ToArray();
        var removed = 0;
        for (var i = _data.Statistics.Count - 1; i >= 0; i--)
        {
            var entry = data[i].Value;
            if (entry.LastUpdate + _config.Lifetime > current) continue;

            _data.Statistics.Remove(data[i].Key);
            removed++;
        }

        PrintDebug($"Removed old data entries: {removed}");
        SaveData();
    }

    private void Unload() => SaveData();

    private void OnPlayerConnected(BasePlayer player)
    {
        InitPlayer(player);
        var stats = PlayerStats.Find(player.userID);
        stats.Joins++;
        stats.Update();
    }

    [Command("statistics.migrate")]
    private void MigrateCommand(IPlayer player, string command, string[] args)
    {
        if (!player.IsAdmin) return;
        try
        {
            _data = JsonConvert.DeserializeObject<PluginData>(ConnectionDB?.Call<string>("API_GetValueRaw", Name),
                new JsonSerializerSettings
                {
                    ObjectCreationHandling = ObjectCreationHandling.Replace,
                    NullValueHandling = NullValueHandling.Ignore
                });
        }
        catch (Exception e)
        {
            PrintError($"Error: {e.Message}\n" +
                       $"Description: {e.StackTrace}");
            return;
        }

        if (_data == null) return;

        SaveData();
        player.Reply("Migration successful!");
    }

    [Command("statistics.output")]
    private void OutputCommand(IPlayer player, string command, string[] args)
    {
        if (!player.IsAdmin || args.Length == 0)
            return;

        var target = players.FindPlayer(string.Join(" ", args));
        if (target == null) // TODO: Message
            return;

        Puts(API_GetAllPlayerData(ulong.Parse(target.Id)).ToString());
    }

    private void OnPlayerDisconnected(BasePlayer player) => PlayerStats.Find(player.userID).Leaves++;

    private void CanExperiment(BasePlayer player, Workbench workbench) =>
        PlayerStats.Find(player.userID).Experiments++;

    private void OnPlayerDeath(BasePlayer player, HitInfo info)
    {
        if (info == null || player == null || player.IsNpc)
            return;

        var stats = PlayerStats.Find(player.userID);
        if (_config.CollectSuicides && info.damageTypes.GetMajorityDamageType() == DamageType.Suicide)
            stats.Suicides++;
        else
        {
            if (_config.CollectDeaths)
                stats.Deaths++;

            if (!_config.CollectKills) return;

            var attacker = info.InitiatorPlayer;
            if (attacker == null || attacker.IsNpc)
                return;

            stats = PlayerStats.Find(attacker.userID);
            stats.Kills++;
        }
    }

    private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
    {
        if (info?.InitiatorPlayer == null || !info.isHeadshot)
            return;

        PlayerStats.Find(info.InitiatorPlayer.userID).Headshots++;
    }

    private void OnPlayerWound(BasePlayer player) => PlayerStats.Find(player.userID).WoundedTimes++;

    private void OnPlayerRecover(BasePlayer player) => PlayerStats.Find(player.userID).Recoveries++;

    private void OnPlayerVoice(BasePlayer player, byte[] data) =>
        PlayerStats.Find(player.userID).VoiceBytes += (uint)data.Length;

    private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter) =>
        PlayerStats.Find(crafter.owner.userID).CraftedItems++;

    private void OnItemRepair(BasePlayer player, Item item) => PlayerStats.Find(player.userID).RepairedItems++;

    private void OnLiftUse(Lift lift, BasePlayer player) => PlayerStats.Find(player.userID).LiftUsages++;

    private void OnLiftUse(ProceduralLift lift, BasePlayer player) => PlayerStats.Find(player.userID).LiftUsages++;

    private void OnSpinWheel(BasePlayer player, SpinnerWheel wheel) => PlayerStats.Find(player.userID).WheelSpins++;

    private void OnHammerHit(BasePlayer player, HitInfo info) => PlayerStats.Find(player.userID).HammerHits++;

    private void OnExplosiveThrown(BasePlayer player, BaseEntity entity) =>
        PlayerStats.Find(player.userID).ExplosivesThrown++;

    private void OnReloadWeapon(BasePlayer player, BaseProjectile projectile) =>
        PlayerStats.Find(player.userID).WeaponReloads++;

    private static Time _time = GetLibrary<Time>();

    private void InitPlayer(BasePlayer player, bool isDisconnect = false)
    {
        var id = player.userID;
        var name = player.displayName;
        var ip = player.net.connection.ipaddress;
        var time = _time.GetUnixTimestamp();

        ip = ip.Substring(0, ip.LastIndexOf(':'));

        var stats = PlayerStats.Find(player.userID);
        if (stats == null)
        {
            var info = new PlayerStats
            {
                Names = new List<string> { name },
                IPs = new List<string> { ip },
                TimeStamps = new List<uint> { time },
                SecondsPlayed = 1
            };

            PrintDebug($"Added new user {name} ({id})");
            _data.Statistics.Add(id, info);
            SaveData(player.userID);
            return;
        }

        if (!stats.Names.Contains(name))
            stats.Names.Add(name);

        if (!stats.IPs.Contains(ip))
            stats.IPs.Add(ip);

        if (isDisconnect)
            stats.SecondsPlayed += time - stats.TimeStamps.Last();
        stats.TimeStamps.Add(time);

        PrintDebug($"Updated user {name} ({id})");
    }

    private void OnRocketLaunched(BasePlayer player, BaseEntity entity) =>
        PlayerStats.Find(player.userID).RocketsLaunched++;

    private void OnWeaponFired(BaseProjectile projectile, BasePlayer player, ItemModProjectile mod,
        ProtoBuf.ProjectileShoot projectiles) => PlayerStats.Find(player.userID).Shots++;

    private void OnCollectiblePickup(Item item, BasePlayer player)
    {
        var dict = PlayerStats.Find(player.userID).CollectiblePickups;
        if (dict.TryGetValue(item.info.shortname, out var count))
            count += (uint)item.amount;
        else
        {
            count = (uint)item.amount;
            dict.Add(item.info.shortname, count);
        }

        dict[item.info.shortname] = count;
    }

    private void OnGrowableGather(GrowableEntity plant, Item item, BasePlayer player)
    {
        var dict = PlayerStats.Find(player.userID).PlantPickups;
        if (dict.TryGetValue(item.info.shortname, out var count))
            count += (uint)item.amount;
        else
        {
            count = (uint)item.amount;
            dict.Add(item.info.shortname, count);
        }

        dict[item.info.shortname] = count;
    }

    private void OnDispenserGather(ResourceDispenser dispenser, BaseEntity entity, Item item)
    {
        var dict = PlayerStats.Find(((BasePlayer)entity).userID).Gathered;
        if (dict.ContainsKey(item.info.shortname))
            dict[item.info.shortname] += (uint)item.amount;
        else
        {
            dict.Add(item.info.shortname, (uint)item.amount);
        }
    }

    #endregion

    #region API

    // General API

    private JObject API_GetAllData() => JObject.FromObject(_data.Statistics);
    private JObject API_GetAllPlayerData(ulong id) => JObject.FromObject(PlayerStats.Find(id));
    private bool API_ContainsPlayer(ulong id) => PlayerStats.Find(id) != null;

    private uint? API_GetJoins(ulong id) => PlayerStats.Find(id)?.Joins;
    private uint? API_GetLeaves(ulong id) => PlayerStats.Find(id)?.Leaves;
    private uint? API_GetKills(ulong id) => PlayerStats.Find(id)?.Kills;
    private uint? API_GetDeaths(ulong id) => PlayerStats.Find(id)?.Deaths;
    private uint? API_GetSuicides(ulong id) => PlayerStats.Find(id)?.Suicides;
    private uint? API_GetShots(ulong id) => PlayerStats.Find(id)?.Shots;
    private uint? API_GetHeadshots(ulong id) => PlayerStats.Find(id)?.Headshots;
    private uint? API_GetExperiments(ulong id) => PlayerStats.Find(id)?.Experiments;
    private uint? API_GetRecoveries(ulong id) => PlayerStats.Find(id)?.Recoveries;
    private uint? API_GetVoiceBytes(ulong id) => PlayerStats.Find(id)?.VoiceBytes;
    private uint? API_GetWoundedTimes(ulong id) => PlayerStats.Find(id)?.WoundedTimes;
    private uint? API_GetCraftedItems(ulong id) => PlayerStats.Find(id)?.CraftedItems;
    private uint? API_GetRepairedItems(ulong id) => PlayerStats.Find(id)?.RepairedItems;
    private uint? API_GetLiftUsages(ulong id) => PlayerStats.Find(id)?.LiftUsages;
    private uint? API_GetWheelSpins(ulong id) => PlayerStats.Find(id)?.WheelSpins;
    private uint? API_GetHammerHits(ulong id) => PlayerStats.Find(id)?.HammerHits;
    private uint? API_GetExplosivesThrown(ulong id) => PlayerStats.Find(id)?.ExplosivesThrown;
    private uint? API_GetWeaponReloads(ulong id) => PlayerStats.Find(id)?.WeaponReloads;
    private uint? API_GetRocketsLaunched(ulong id) => PlayerStats.Find(id)?.RocketsLaunched;
    private uint? API_GetSecondsPlayed(ulong id) => PlayerStats.Find(id)?.SecondsPlayed;
    private List<string> API_GetNames(ulong id) => PlayerStats.Find(id)?.Names;
    private List<string> API_GetIPs(ulong id) => PlayerStats.Find(id)?.IPs;
    private List<uint> API_GetTimeStamps(ulong id) => PlayerStats.Find(id)?.TimeStamps;

    // Gather API

    private Dictionary<string, uint> API_GetCollectiblePickups(ulong id) =>
        PlayerStats.Find(id)?.CollectiblePickups;

    private Dictionary<string, uint> API_GetPlantPickups(ulong id) =>
        PlayerStats.Find(id)?.PlantPickups;

    private Dictionary<string, uint> API_GetGathered(ulong id) =>
        PlayerStats.Find(id)?.Gathered;

    private uint? API_GetCollectiblePickups(ulong id, string shortname)
    {
        var data = API_GetCollectiblePickups(id);
        if (data?.TryGetValue(shortname, out var amount) == true)
            return amount;

        return null;
    }

    private uint? API_GetPlantPickups(ulong id, string shortname)
    {
        var data = API_GetPlantPickups(id);
        if (data?.TryGetValue(shortname, out var amount) == true)
            return amount;

        return null;
    }

    private uint? API_GetGathered(ulong id, string shortname)
    {
        var data = API_GetGathered(id);
        if (data?.TryGetValue(shortname, out var amount) == true)
            return amount;

        return null;
    }

    #endregion

    #region Helpers

    [Conditional("DEBUG")]
    private static void PrintDebug(string message)
    {
        _ins.Puts(message);
    }

    #endregion
}