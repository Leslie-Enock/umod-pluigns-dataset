#if DEBUG
using System.Diagnostics;
using Debug = UnityEngine.Debug;
#endif

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Rust;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins;

[Info("Power Spawn", "misticos", "1.4.0")]
[Description("Powerful position generation tool with API")]
internal class PowerSpawn : CovalencePlugin
{
    #region Variables

    private static PowerSpawn _ins;

    private readonly Random _random = new();

    private int _halfWorldSize;

    private const string PermissionLocation = "powerspawn.location";

    #endregion

    #region Configuration

    private Configuration _config;

    private class Configuration
    {
        [JsonProperty("Profiles", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public Dictionary<string, Profile> Profiles { get; set; } = new()
        {
            { string.Empty, new Profile() },
            {
                "No Winter", new Profile
                {
                    BiomesBlocked = new List<TerrainBiome.Enum>
                    {
                        TerrainBiome.Enum.Arctic
                    },
                    SplatBlocked = new List<TerrainSplat.Enum>
                    {
                        TerrainSplat.Enum.Snow
                    }
                }
            },
            {
                "Beach Only", new Profile
                {
                    SplatBlocked = new List<TerrainSplat.Enum>
                    {
                        TerrainSplat.Enum.Snow
                    },
                    TopologiesAllowed = new List<TerrainTopology.Enum>
                    {
                        TerrainTopology.Enum.Beach, TerrainTopology.Enum.Beachside
                    }
                }
            },
            {
                "Snowy Forest", new Profile
                {
                    SplatThresholdMinimum = new Dictionary<TerrainSplat.Enum, float>
                    {
                        { TerrainSplat.Enum.Snow, 0.8f }
                    },
                    TopologiesAllowed = new List<TerrainTopology.Enum>
                    {
                        TerrainTopology.Enum.Forest, TerrainTopology.Enum.Forestside
                    }
                }
            }
        };

        [JsonProperty("Respawn Configurations", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public List<RespawnConfiguration> RespawnConfigurations { get; set; } = new()
        {
            new RespawnConfiguration()
        };

        [JsonProperty("Respawn Profile", NullValueHandling = NullValueHandling.Ignore)]
        public string RespawnProfileName { get; set; } = null;

        [JsonProperty("Respawn Locations Group", NullValueHandling = NullValueHandling.Ignore)]
        public int? RespawnGroup { get; set; } = null;

        [JsonProperty("Enable Respawn Locations Group", NullValueHandling = NullValueHandling.Ignore)]
        public bool? EnableRespawnGroup { get; set; } = null;

        [JsonProperty("Enable Respawn Management")]
        public bool EnableRespawn { get; set; } = true;

        [JsonProperty("Location Management Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
        public string[] LocationCommand { get; set; } = { "loc", "location", "ps" };

        public class RespawnConfiguration
        {
            [JsonProperty("Permission")]
            public string Permission { get; set; } = string.Empty;

            [JsonProperty("Profile Name")]
            public string ProfileName { get; set; } = string.Empty;

            [JsonProperty("Locations Group")]
            public int? Group { get; set; } = null;

            [JsonIgnore]
            public Profile Profile = null;
        }

        public class Profile
        {
            [JsonProperty("Minimal Distance To Building")]
            public int DistanceBuilding { get; set; } = 16;

            [JsonProperty("Minimal Distance To Collider")]
            public int DistanceCollider { get; set; } = 8;

            [JsonProperty("Raycast Distance Above")]
            public float DistanceRaycast { get; set; } = 50f;

            [JsonProperty("Number Of Attempts To Find A Position Per Frame")]
            public int AttemptsPerFrame { get; set; } = 160;

            [JsonProperty("Number Of Positions Per Frame")]
            public int PositionsPerFrame { get; set; } = 16;

            [JsonProperty("Number Of Attempts To Find A Pregenerated Position")]
            public int AttemptsPregenerated { get; set; } = 400;

            [JsonProperty("Pregenerated Positions Amount")]
            public int PregeneratedAmount { get; set; } = 4000;

            [JsonProperty("Pregenerated Amount Check Frequency (Seconds)")]
            public float PregeneratedCheck { get; set; } = 90f;

            [JsonProperty("Biomes Threshold", NullValueHandling = NullValueHandling.Ignore)]
            public Dictionary<TerrainBiome.Enum, float> BiomesThresholdOld { get; set; } = null;

            [JsonProperty("Biomes Minimum Threshold", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<TerrainBiome.Enum, float> BiomesThresholdMinimum { get; set; } = new();

            [JsonProperty("Biomes Maximum Threshold",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<TerrainBiome.Enum, float> BiomesThresholdMaximum { get; set; } = new();

            [JsonProperty("Biomes Allowed",
                ObjectCreationHandling = ObjectCreationHandling.Replace,
                ItemConverterType = typeof(StringEnumConverter))]
            public List<TerrainBiome.Enum> BiomesAllowed { get; set; } = new();

            [JsonProperty("Biomes Blocked", ObjectCreationHandling = ObjectCreationHandling.Replace,
                ItemConverterType = typeof(StringEnumConverter))]
            public List<TerrainBiome.Enum> BiomesBlocked { get; set; } = new();

            [JsonProperty("Splat Minimum Threshold", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<TerrainSplat.Enum, float> SplatThresholdMinimum { get; set; } = new();

            [JsonProperty("Splat Maximum Threshold", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<TerrainSplat.Enum, float> SplatThresholdMaximum { get; set; } = new();

            [JsonProperty("Splat Allowed", ObjectCreationHandling = ObjectCreationHandling.Replace,
                ItemConverterType = typeof(StringEnumConverter))]
            public List<TerrainSplat.Enum> SplatAllowed { get; set; } = new();

            [JsonProperty("Splat Blocked", ObjectCreationHandling = ObjectCreationHandling.Replace,
                ItemConverterType = typeof(StringEnumConverter))]
            public List<TerrainSplat.Enum> SplatBlocked { get; set; } = new();

            [JsonProperty("Topologies Allowed", ObjectCreationHandling = ObjectCreationHandling.Replace,
                ItemConverterType = typeof(StringEnumConverter))]
            public List<TerrainTopology.Enum> TopologiesAllowed { get; set; } = new();

            [JsonProperty("Topologies Blocked", ObjectCreationHandling = ObjectCreationHandling.Replace,
                ItemConverterType = typeof(StringEnumConverter))]
            public List<TerrainTopology.Enum> TopologiesBlocked { get; set; } = new();

            [JsonIgnore]
            public Coroutine Coroutine = null;

#if DEBUG
            [JsonIgnore]
            public uint Calls = 0;

            [JsonIgnore]
            public double CallsTook = 0d;

            [JsonIgnore]
            public uint SkippedGenerations = 0;
#endif

            [JsonIgnore]
            public List<Vector3> Positions = new();

            public bool IsValidPosition(Vector3 position)
            {
                return IsValidColliders(position) && IsValidBuilding(position) && IsValidAbove(position) &&
                       IsValidTopology(position) && IsValidBiome(position) && IsValidSplat(position);
            }

            private const int LayerMaskAbove = ~(Layers.Terrain | Layers.Server.Players);

            public bool IsValidAbove(Vector3 position)
            {
                // Casting from above. If done from below, you could be inside a collider which would not be hit
                position += new Vector3(0f, DistanceRaycast + Mathf.Epsilon, 0f);

                return !Physics.Raycast(position, Vector3.down, DistanceRaycast, LayerMaskAbove);
            }

            private const int LayerMaskBuilding = Layers.Construction;

            public bool IsValidBuilding(Vector3 position)
            {
                return !Physics.CheckSphere(position, DistanceBuilding, LayerMaskBuilding);
            }

            // TODO: Separate option for triggers, perhaps per-mask even
            private const int LayerMaskColliders = ~(Layers.Terrain | Layers.Server.Players | Layers.Construction);

            public bool IsValidColliders(Vector3 position)
            {
                return !Physics.CheckSphere(position, DistanceCollider, LayerMaskColliders);
            }

            public bool IsValidTopology(Vector3 position)
            {
                // TODO: Instead of going through all topologies, make two masks and compare with bitwise operators
                if (TopologiesAllowed.Count > 0)
                {
                    foreach (var topology in TopologiesAllowed)
                        if (TerrainMeta.TopologyMap.GetTopology(position, (int)topology))
                            return true;

                    return false;
                }

                if (TopologiesBlocked.Count > 0)
                {
                    foreach (var topology in TopologiesBlocked)
                        if (TerrainMeta.TopologyMap.GetTopology(position, (int)topology))
                            return false;

                    return true;
                }

                return true;
            }

            public bool IsValidBiome(Vector3 position)
            {
                if (BiomesAllowed.Count > 0 || BiomesBlocked.Count > 0)
                {
                    var biomeHighest = (TerrainBiome.Enum)TerrainMeta.BiomeMap.GetBiomeMaxType(position);

                    if (BiomesAllowed.Count > 0 && !BiomesAllowed.Contains(biomeHighest))
                        return false;

                    if (BiomesBlocked.Count > 0 && BiomesBlocked.Contains(biomeHighest))
                        return false;
                }

                if (BiomesThresholdMinimum.Count > 0)
                {
                    foreach (var threshold in BiomesThresholdMinimum)
                        if (TerrainMeta.BiomeMap.GetBiome(position, (int)threshold.Key) < threshold.Value)
                            return false;
                }

                if (BiomesThresholdMaximum.Count > 0)
                {
                    foreach (var threshold in BiomesThresholdMaximum)
                        if (TerrainMeta.BiomeMap.GetBiome(position, (int)threshold.Key) > threshold.Value)
                            return false;
                }

                return true;
            }

            public bool IsValidSplat(Vector3 position)
            {
                if (SplatAllowed.Count > 0 || SplatBlocked.Count > 0)
                {
                    var splatHighest = (TerrainSplat.Enum)TerrainMeta.SplatMap.GetSplatMaxType(position);

                    if (SplatAllowed.Count > 0 && !SplatAllowed.Contains(splatHighest))
                        return false;

                    if (SplatBlocked.Count > 0 && SplatBlocked.Contains(splatHighest))
                        return false;
                }

                if (SplatThresholdMinimum.Count > 0)
                {
                    foreach (var threshold in SplatThresholdMinimum)
                        if (TerrainMeta.SplatMap.GetSplat(position, (int)threshold.Key) < threshold.Value)
                            return false;
                }

                if (SplatThresholdMaximum.Count > 0)
                {
                    foreach (var threshold in SplatThresholdMaximum)
                        if (TerrainMeta.SplatMap.GetSplat(position, (int)threshold.Key) > threshold.Value)
                            return false;
                }

                return true;
            }
        }
    }

    protected override void LoadConfig()
    {
        base.LoadConfig();
        try
        {
            _config = Config.ReadObject<Configuration>();
            if (_config == null) throw new Exception();

            if (_config.EnableRespawnGroup != null)
            {
                var respawnConfiguration =
                    _config.RespawnConfigurations.FirstOrDefault(x => string.IsNullOrEmpty(x.Permission));
                if (respawnConfiguration == null)
                    _config.RespawnConfigurations.Add(respawnConfiguration =
                        new Configuration.RespawnConfiguration());

                respawnConfiguration.ProfileName = _config.RespawnProfileName;
                respawnConfiguration.Group = _config.EnableRespawnGroup.Value ? _config.RespawnGroup : null;

                _config.RespawnProfileName = null;
                _config.EnableRespawnGroup = null;
                _config.RespawnGroup = null;
            }

            foreach (var profile in _config.Profiles.Values)
            {
                if (profile.BiomesThresholdOld != null)
                {
                    profile.BiomesThresholdMinimum = profile.BiomesThresholdOld;
                    profile.BiomesThresholdOld = null;
                }
            }

            SaveConfig();
        }
        catch (Exception e)
        {
            PrintError("Your configuration file contains an error. Using default configuration values.\n" + e);
            LoadDefaultConfig();
        }

        if (_config == null)
            return;

        foreach (var configuration in _config.RespawnConfigurations)
        {
            configuration.Profile = _config.Profiles.GetValueOrDefault(configuration.ProfileName);

            if (configuration.Profile is null)
                throw new Exception(
                    $"Your configuration file contains an error. Profile '{configuration.ProfileName}' does not exist.");
        }
    }

    protected override void LoadDefaultConfig() => _config = new Configuration();

    protected override void SaveConfig() => Config.WriteObject(_config);

    #endregion

    #region Work with Data

    private PluginData _data = new();

    private class PluginData
    {
        public List<Location> Locations = new();

        [JsonIgnore]
        public Dictionary<int, List<Location>> LocationsByGroup = null;

        // ReSharper disable once MemberCanBePrivate.Local
        public int LastID = 0;

        public void AddLocation(Location location)
        {
            Locations.Add(location);

            if (!LocationsByGroup.TryGetValue(location.Group, out var groupLocations))
                LocationsByGroup[location.Group] = groupLocations = new List<Location>();

            groupLocations.Add(location);
        }

        public void RemoveLocation(int index)
        {
            if (Locations.Count <= index)
                return;

            var location = Locations[index];
            var groupId = location.Group;
            if (LocationsByGroup.TryGetValue(groupId, out var groupLocations))
                groupLocations.Remove(location);

            Locations.RemoveAt(index);
        }

        public class Location
        {
            public string Name;
            public int ID = _ins._data.LastID++;
            public int Group = -1;
            public Vector3 Position;

            public string Format(string player)
            {
                var text = new StringBuilder(GetMsg("Location: Format", player));
                text.Replace("{name}", Name);
                text.Replace("{id}", ID.ToString());
                text.Replace("{group}", Group.ToString());
                text.Replace("{position}", Position.ToString());

                return text.ToString();
            }

            public static int? FindIndexById(int id)
            {
                // TODO: this sucks
                for (var i = 0; i < _ins._data.Locations.Count; i++)
                {
                    if (_ins._data.Locations[i].ID == id)
                        return i;
                }

                return null;
            }

            public static IReadOnlyList<Location> FindByGroup(int group)
            {
                // TODO: static sucks
                return _ins._data.LocationsByGroup.TryGetValue(group, out var locations)
                    ? locations
                    : Array.Empty<Location>();
            }
        }
    }

    private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);

    private void LoadData()
    {
        try
        {
            _data = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
        }
        catch (Exception e)
        {
            PrintError(e.ToString());
        }

        _data ??= new PluginData();

        _data.LocationsByGroup = _data.Locations
            .GroupBy(l => l.Group)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    #endregion

    #region Hooks

    protected override void LoadDefaultMessages()
    {
        lang.RegisterMessages(new Dictionary<string, string>
        {
            { "No Permission", "nope" },
            {
                "Location: Syntax", "Location Syntax:\n" +
                                    "new (Name) - Create a new location with a specified name\n" +
                                    "delete (ID) - Delete a location with the specified ID\n" +
                                    "edit (ID) <Parameter 1> <Value> <...> - Edit a location with the specified ID\n" +
                                    "update - Apply datafile changes\n" +
                                    "list - Get a list of locations\n" +
                                    "validate location (Profile Name) (ID) - Validate preconfigured location\n" +
                                    "validate generated (Profile Name) (ID) - Validate generated position (from draw)\n" +
                                    "validate position (Profile Name) - Validate your current position\n" +
                                    "debug <Profile Name> - Print minor debug information\n" +
                                    "show <Profile Name> <Radius> - Show all positions"
            },
            {
                "Location: Edit Syntax", "Location Edit Parameters:\n" +
                                         "move (x;y;z / here) - Move a location to the specified position\n" +
                                         "group (ID / reset) - Set group of a location or reset the group"
            },
            { "Location: Debug", "Currently available pre-generated positions: {amount}" },
            { "Location: Shown", "Showing generated positions on screen.." },
            { "Location: Unable To Parse Position", "Unable to parse the position" },
            { "Location: Unable To Parse Group", "Unable to parse the entered group" },
            { "Location: Format", "Location ID: {id}; Group: {group}; Position: {position}; Name: {name}" },
            { "Location: Not Found", "Location was not found." },
            { "Location: Generated Not Found", "Generated position was not found." },
            { "Location: Profile Not Found", "Profile was not found." },
            { "Location: Edit Finished", "Edit was finished." },
            { "Location: Removed", "Location was removed from our database." },
            { "Location: Updated", "Datafile changes were applied." },
            {
                "Location: Validation Format",
                "Validation results:\n" +
                "Buildings: {buildings}\n" +
                "Colliders: {colliders}\n" +
                "Raycast above: {raycast}\n" +
                "Biomes: {biome}\n" +
                "Splat: {splat}\n" +
                "Topologies: {topology}"
            },
            { "Location: Player Only", "This is available only to in-game players." }
        }, this);
    }

    private void Init()
    {
        _ins = this;
        LoadData();

        permission.RegisterPermission(PermissionLocation, this);
        AddCovalenceCommand(_config.LocationCommand, nameof(CommandLocation));

        if (!_config.EnableRespawn)
            Unsubscribe(nameof(OnPlayerRespawn));
        else
        {
            foreach (var configuration in _config.RespawnConfigurations)
            {
                if (!string.IsNullOrEmpty(configuration.Permission))
                    permission.RegisterPermission(configuration.Permission, this);
            }
        }
    }

    private void OnServerInitialized()
    {
        _halfWorldSize = ConVar.Server.worldsize / 2;

        foreach (var kvp in _config.Profiles)
        {
            kvp.Value.Coroutine = InvokeHandler.Instance.StartCoroutine(PositionGeneration(kvp.Value, kvp.Key));
        }
    }

    private void Unload()
    {
        foreach (var kvp in _config.Profiles)
        {
            InvokeHandler.Instance.StopCoroutine(kvp.Value.Coroutine);
        }
    }

    private object OnPlayerRespawn(BasePlayer player)
    {
        Configuration.RespawnConfiguration respawnConfiguration = null;
        foreach (var configuration in _config.RespawnConfigurations)
        {
            if (!string.IsNullOrEmpty(configuration.Permission) &&
                !permission.UserHasPermission(player.UserIDString, configuration.Permission))
                continue;

            respawnConfiguration = configuration;
            break;
        }

        if (respawnConfiguration == null)
            return null;

        Vector3? position = null;
        if (respawnConfiguration.Group is { } groupId)
        {
            var positions = PluginData.Location.FindByGroup(groupId);

            position = positions[_random.Next(0, positions.Count)].Position;
        }
        else if (respawnConfiguration.Profile is { } profile)
        {
            position = FindPregeneratedPosition(profile);
        }

        if (!position.HasValue)
        {
#if DEBUG
            Debug.Log($"{nameof(OnPlayerRespawn)} > Unable to find a position for {player.UserIDString}.");
#endif
            return null;
        }

#if DEBUG
        Debug.Log($"{nameof(OnPlayerRespawn)} > Found position for {player.UserIDString}: {position}.");
#endif

        return new BasePlayer.SpawnPoint
        {
            pos = position.Value
        };
    }

    #endregion

    #region Commands

    private void CommandLocation(IPlayer player, string command, string[] args)
    {
        if (!player.HasPermission(PermissionLocation))
        {
            player.Reply(GetMsg("No Permission", player.Id));
            return;
        }

        if (args.Length == 0)
        {
            goto syntax;
        }

        switch (args[0])
        {
            case "new":
            case "n":
            {
                if (args.Length != 2)
                {
                    goto syntax;
                }

                var location = new PluginData.Location
                {
                    Name = args[1]
                };

                player.Position(out location.Position.x, out location.Position.y, out location.Position.z);
                _data.AddLocation(location);

                player.Reply(location.Format(player.Id));
                goto saveData;
            }

            case "delete":
            case "remove":
            case "d":
            case "r":
            {
                if (args.Length != 2 || !int.TryParse(args[1], out var id))
                {
                    goto syntax;
                }

                var locationIndex = PluginData.Location.FindIndexById(id);
                if (!locationIndex.HasValue)
                {
                    player.Reply(GetMsg("Location: Not Found", player.Id));
                    return;
                }

                _data.RemoveLocation(locationIndex.Value);
                player.Reply(GetMsg("Location: Removed", player.Id));
                goto saveData;
            }

            case "edit":
            case "e":
            {
                if (args.Length < 4 || !int.TryParse(args[1], out var id))
                {
                    player.Reply(GetMsg("Location: Edit Syntax", player.Id));
                    return;
                }

                var locationIndex = PluginData.Location.FindIndexById(id);
                if (!locationIndex.HasValue)
                {
                    player.Reply(GetMsg("Location: Not Found", player.Id));
                    return;
                }

                var locationCd = new CommandLocationData
                {
                    Player = player,
                    Location = _data.Locations[locationIndex.Value]
                };

                locationCd.Apply(args);
                player.Reply(GetMsg("Location: Edit Finished", player.Id));
                goto saveData;
            }

            case "update":
            case "u":
            {
                LoadData();
                player.Reply(GetMsg("Location: Updated", player.Id));
                return;
            }

            case "list":
            case "l":
            {
                var table = new TextTable();
                table.AddColumns("ID", "Name", "Group", "Position");

                foreach (var location in _data.Locations)
                {
                    table.AddRow(location.ID.ToString(), location.Name, location.Group.ToString(),
                        location.Position.ToString());
                }

                player.Reply(table.ToString());
                return;
            }

            case "valid":
            case "validate":
            case "v":
            {
                if (args.Length < 2)
                    goto syntax;

                var profile = _config.Profiles.GetValueOrDefault(args.Length > 2 ? args[2] : string.Empty);
                if (profile == null)
                {
                    player.Reply(GetMsg("Location: Profile Not Found", player.Id));
                    return;
                }

                Vector3 position;

                switch (args[1])
                {
                    case "location":
                    case "l":
                        if (args.Length < 4 || !int.TryParse(args[3], out var id))
                        {
                            goto syntax;
                        }

                        var locationIndex = PluginData.Location.FindIndexById(id);
                        if (!locationIndex.HasValue)
                        {
                            player.Reply(GetMsg("Location: Not Found", player.Id));
                            return;
                        }

                        position = _data.Locations[locationIndex.Value].Position;
                        break;

                    case "generated":
                    case "g":
                        if (args.Length < 4 || !int.TryParse(args[3], out var index))
                        {
                            goto syntax;
                        }

                        if (profile.Positions.Count <= index)
                        {
                            player.Reply(GetMsg("Location: Generated Not Found", player.Id));
                            return;
                        }

                        position = profile.Positions[index];
                        break;

                    case "position":
                    case "p":
                        if (player.Object is not BasePlayer basePlayer)
                        {
                            player.Reply(GetMsg("Location: Player Only", player.Id));
                            return;
                        }

                        position = basePlayer.ServerPosition;
                        break;

                    default:
                        goto syntax;
                }

                player.Reply(GetMsg("Location: Validation Format", player.Id)
                    .Replace("{buildings}", profile.IsValidBuilding(position).ToString())
                    .Replace("{colliders}", profile.IsValidColliders(position).ToString())
                    .Replace("{raycast}", profile.IsValidAbove(position).ToString())
                    .Replace("{biome}", profile.IsValidBiome(position).ToString())
                    .Replace("{topology}", profile.IsValidTopology(position).ToString())
                    .Replace("{splat}", profile.IsValidSplat(position).ToString()));

                return;
            }

            case "debug":
            {
                var profile =
                    _config.Profiles.GetValueOrDefault(args.Length > 1 ? string.Join(" ", args.Skip(1)) : string.Empty);

                if (profile == null)
                {
                    player.Reply(GetMsg("Location: Profile Not Found", player.Id));
                    return;
                }

                player.Reply(GetMsg("Location: Debug", player.Id)
                    .Replace("{amount}", profile.Positions.Count.ToString()));
                return;
            }

            case "show":
            case "draw":
            {
                if (player.Object is not BasePlayer basePlayer)
                {
                    player.Reply(GetMsg("Location: Player Only", player.Id));
                    return;
                }

                var profile = _config.Profiles.GetValueOrDefault(args.Length > 1 ? args[1] : string.Empty);

                if (profile == null)
                {
                    player.Reply(GetMsg("Location: Profile Not Found", player.Id));
                    return;
                }

                if (args.Length <= 2 || !float.TryParse(args[2], out var distance) || distance < 0)
                    distance = float.NaN;
                else
                    distance *= distance;

                var playerPosition = basePlayer.transform.position;
                for (var i = 0; i < profile.Positions.Count; i++)
                {
                    var position = profile.Positions[i];
                    if (!float.IsNaN(distance) &&
                        (playerPosition - position).SqrMagnitude2D() >= distance)
                        continue;

                    DDraw.Text(basePlayer, 10f, from: position, text: $"#{i}");
                }

                player.Reply(GetMsg("Location: Shown", player.Id));
                return;
            }

            default:
            {
                goto syntax;
            }
        }

        syntax:
        player.Reply(GetMsg("Location: Syntax", player.Id));
        return;

        saveData:
        SaveData();
    }

    private class CommandLocationData
    {
        public IPlayer Player;

        public PluginData.Location Location;

        private const int FirstArgumentIndex = 2;

        public void Apply(string[] args)
        {
            for (var i = FirstArgumentIndex; i + 1 < args.Length; i += 2)
            {
                switch (args[i])
                {
                    case "move":
                    {
                        var position = ParseVector(args[i + 1].ToLower());
                        if (!position.HasValue)
                        {
                            Player.Reply(GetMsg("Location: Unable To Parse Position", Player.Id));
                            break;
                        }

                        Location.Position = position.Value;
                        break;
                    }

                    case "group":
                    {
                        var group = -1;
                        if (args[i + 1] != "reset" && !int.TryParse(args[i + 1], out group))
                        {
                            Player.Reply(GetMsg("Location: Unable To Parse Group", Player.Id));
                            break;
                        }

                        Location.Group = group;
                        break;
                    }
                }
            }
        }

        private Vector3? ParseVector(string argument)
        {
            var vector = new Vector3();

            if (argument == "here")
            {
                Player.Position(out vector.x, out vector.y, out vector.z);
            }
            else
            {
                var coordinates = argument.Split(';');
                if (coordinates.Length != 3 || !float.TryParse(coordinates[0], out vector.x) ||
                    !float.TryParse(coordinates[1], out vector.y) || !float.TryParse(coordinates[2], out vector.z))
                {
                    return null;
                }
            }

            return vector;
        }
    }

    #endregion

    #region API

    private Vector3? GetLocation(int id)
    {
        var locationIndex = PluginData.Location.FindIndexById(id);
        if (!locationIndex.HasValue)
            return null;

        return _data.Locations[locationIndex.Value].Position;
    }

    private JObject GetGroupLocations(int group)
    {
        var locations = PluginData.Location.FindByGroup(group);
        return JObject.FromObject(locations);
    }

    private Vector3? GetPregeneratedLocation(string profileName = null)
    {
        if (profileName == null || !_config.Profiles.TryGetValue(profileName, out var profile))
        {
            PrintWarning($"Unknown profile has been retrieved.\n{StackTraceUtility.ExtractStackTrace()}");
            return null;
        }

        return FindPregeneratedPosition(profile);
    }

    #endregion

    #region Helpers

    private IEnumerator PositionGeneration(Configuration.Profile profile, string name)
    {
#if DEBUG
        var watch = Stopwatch.StartNew();
#endif
        while (true)
        {
            if (profile.Positions.Count >= profile.PregeneratedAmount)
            {
#if DEBUG
                Debug.Log(
                    $"{nameof(PositionGeneration)} > {profile.Calls} frames took {profile.CallsTook}ms (AVG: {profile.CallsTook / profile.Calls}ms). Generated (Profile: \"{name}\"): {profile.Positions.Count}+{profile.SkippedGenerations}.");

                profile.Calls = 0;
                profile.CallsTook = 0d;
                profile.SkippedGenerations = 0;
#endif
                yield return new WaitForSeconds(profile.PregeneratedCheck);
                continue;
            }

#if DEBUG
            watch.Start();
#endif

            var attempts = 0;
            var found = 0;
            while (attempts++ < profile.AttemptsPerFrame && found < profile.PositionsPerFrame &&
                   profile.Positions.Count < profile.PregeneratedAmount)
            {
                var position = TryFindPosition(profile);
                if (!position.HasValue)
                {
#if DEBUG
                    profile.SkippedGenerations++;
#endif
                    continue;
                }

                profile.Positions.Add(position.Value);
                found++;
            }

#if DEBUG
            profile.Calls++;
            profile.CallsTook += watch.Elapsed.TotalMilliseconds;

            watch.Reset();
#endif

            yield return null;
        }

        // ReSharper disable once IteratorNeverReturns
    }

    private Vector3? FindPregeneratedPosition(Configuration.Profile profile)
    {
        Vector3? position = null;
        for (var i = 0; i < profile.AttemptsPregenerated; i++)
        {
            if (profile.Positions.Count <= 0)
            {
#if DEBUG
                Debug.Log($"{nameof(FindPregeneratedPosition)} > There are no pregenerated positions.");
#endif
                return null;
            }

            // index. noice, performance for RemoveAt!
            var index = _random.Next(0, profile.Positions.Count);
            position = profile.Positions[index];

            // If it is a good position, break to return it.
            if (profile.IsValidPosition(position.Value))
                break;

            // Remove invalid position
            profile.Positions.RemoveAt(index);

            // Reset position value
            position = null;
        }

        return position;
    }

    private Vector3? TryFindPosition(Configuration.Profile profile)
    {
        var position = new Vector3(GetRandomPosition(), 0, GetRandomPosition());
        var waterInfo = WaterLevel.GetWaterInfo(position, false, false);

        position.y = waterInfo.terrainHeight;

        // Invalid if under the water
        if (waterInfo.isValid)
            return null;

        // Invalid if has buildings or colliders in the configured range
        if (!profile.IsValidPosition(position))
            return null;

        return position;
    }

    private int GetRandomPosition() => _random.Next(-_halfWorldSize, _halfWorldSize);

    private static string GetMsg(string key, string userId = null) => _ins.lang.GetMessage(key, _ins, userId);

    internal static class DDraw
    {
        public static void Line(BasePlayer player, float duration = 0.5f, Color? color = null,
            Vector3? from = null, Vector3? to = null)
        {
            player.SendConsoleCommand("ddraw.line", duration, Format(color), Format(from), Format(to));
        }

        public static void Arrow(BasePlayer player, float duration = 0.5f, Color? color = null,
            Vector3? from = null, Vector3? to = null, float headSize = 0f)
        {
            player.SendConsoleCommand("ddraw.arrow", duration, Format(color), Format(from), Format(to), headSize);
        }

        public static void Sphere(BasePlayer player, float duration = 0.5f, Color? color = null,
            Vector3? from = null, string text = "")
        {
            player.SendConsoleCommand("ddraw.sphere", duration, Format(color), Format(from), text);
        }

        public static void Text(BasePlayer player, float duration = 0.5f, Color? color = null,
            Vector3? from = null, string text = "")
        {
            player.SendConsoleCommand("ddraw.text", duration, Format(color), Format(from), text);
        }

        public static void Box(BasePlayer player, float duration = 0.5f, Color? color = null,
            Vector3? from = null, float size = 0.1f)
        {
            player.SendConsoleCommand("ddraw.box", duration, Format(color), Format(from), size);
        }

        private static string Format(Color? color) => ReferenceEquals(color, null)
            ? string.Empty
            : $"{color.Value.r},{color.Value.g},{color.Value.b},{color.Value.a}";

        private static string Format(Vector3? pos) => ReferenceEquals(pos, null)
            ? string.Empty
            : $"{pos.Value.x} {pos.Value.y} {pos.Value.z}";
    }

    #endregion
}