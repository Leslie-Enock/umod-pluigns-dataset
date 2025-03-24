//#define DEBUG

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Facepunch;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Enhanced Hammer", "misticos", "2.1.1")]
    [Description("Upgrade your buildings easily with a hammer")]
    public class EnhancedHammer : CovalencePlugin
    {
        #region Variables

        private static EnhancedHammer _ins;

        private HashSet<BasePlayer> _activePlayers = new HashSet<BasePlayer>();

        private readonly int _maskConstruction = LayerMask.GetMask("Construction");

        private const string PermissionUse = "enhancedhammer.use";
        private const string PermissionFree = "enhancedhammer.free";
        private const string PermissionDowngrade = "enhancedhammer.downgrade";
        private const string PermissionGradeHit = "enhancedhammer.grade.hit";
        private const string PermissionGradeClick = "enhancedhammer.grade.click";
        private const string PermissionGradeBuild = "enhancedhammer.grade.build";

        #endregion

        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] Commands =
            {
                "eh",
                "hammer",
                "up",
                "grade",
                "upgrade",
                "bgrade"
            };

            [JsonProperty("Allowed Grades", ItemConverterType = typeof(StringEnumConverter),
                NullValueHandling = NullValueHandling.Ignore)]
            public BuildingGrade.Enum[] OldGradesAllowed = null;

            [JsonProperty("Grades Enabled", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<GradeConfig> GradesEnabled = new List<GradeConfig>
            {
                new GradeConfig { GradeName = BuildingGrade.Enum.Wood.ToString() },
                new GradeConfig { GradeName = BuildingGrade.Enum.Stone.ToString() },
                new GradeConfig { GradeName = BuildingGrade.Enum.Metal.ToString() },
                new GradeConfig { GradeName = BuildingGrade.Enum.TopTier.ToString() }
            };

            [JsonProperty(PropertyName = "Distance From Entity")]
            public float Distance = 3.0f;

            [JsonProperty(PropertyName = "Allow Auto Grade")]
            public bool AutoGrade = true;

            [JsonProperty(PropertyName = "Send Downgrade Disabled Message")]
            public bool DowngradeMessage = true;

            [JsonProperty(PropertyName = "Cancel Default Hammer Hit Behavior")]
            public bool CancelHammerHitDefaultBehavior = true;

            [JsonProperty(PropertyName = "Default Preferences")]
            public PlayerPreferences Preferences = new PlayerPreferences();

            public class GradeConfig
            {
                [JsonProperty("Grade")]
                public string GradeName = BuildingGrade.Enum.Stone.ToString();

                [JsonIgnore]
                public BuildingGrade.Enum Grade = BuildingGrade.Enum.None;

                [JsonProperty("Skin")]
                public ulong? Skin = null;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }

            if (_config == null)
                return;

            if (_config.OldGradesAllowed != null)
            {
                _config.GradesEnabled.Clear();

                foreach (var grade in _config.OldGradesAllowed)
                {
                    _config.GradesEnabled.Add(new Configuration.GradeConfig { Grade = grade });
                }

                _config.OldGradesAllowed = null;
                SaveConfig();
            }

            foreach (var grade in _config.GradesEnabled)
            {
                if (!Enum.TryParse(grade.GradeName, true, out grade.Grade) ||
                    !Enum.IsDefined(typeof(BuildingGrade.Enum), grade.Grade))
                {
                    grade.Grade = BuildingGrade.Enum.None;
                    PrintWarning($"Grade '{grade.GradeName}' was not found");
                }
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion

        #region Work with Data

        private void DeleteOldData() => Interface.Oxide.DataFileSystem.DeleteDataFile(Name);

        private void ConvertOldData()
        {
            var oldData = LoadOldData();
            if (oldData == null)
                return;

            foreach (var kvp in oldData.Preferences)
            {
                var preference = PlayerPreferences.GetOrCreate(kvp.Key);

                preference.AutoGrade = kvp.Value.AutoGrade;
                preference.DisableIn = kvp.Value.DisableIn;

                PlayerPreferences.Save(kvp.Key);
            }

            PrintWarning($"Converted {oldData.Preferences.Count} player preferences into new format");

            DeleteOldData();
        }

        private PluginData LoadOldData()
        {
            try
            {
                return Interface.Oxide.DataFileSystem.ReadObject<PluginData>(Name);
            }
            catch (Exception e)
            {
                PrintError(e.ToString());
            }

            return null;
        }

        private class PluginData
        {
            [JsonProperty(PropertyName = "Player Preferences")]
            public Dictionary<string, OldPreference> Preferences = new Dictionary<string, OldPreference>();

            [Serializable]
            public class OldPreference
            {
                public float DisableIn = 30f;

                public bool AutoGrade = true;
            }
        }

        private abstract class SplitDatafile<T> where T : SplitDatafile<T>, new()
        {
            public static Dictionary<string, T> LoadedData = new Dictionary<string, T>();

            protected static string[] GetFiles(string baseFolder)
            {
                try
                {
                    var json = ".json".Length;
                    var paths = Interface.Oxide.DataFileSystem.GetFiles(baseFolder);
                    for (var i = 0; i < paths.Length; i++)
                    {
                        var path = paths[i];
                        var separatorIndex = path.LastIndexOf(Path.DirectorySeparatorChar);

                        // We have to do this since GetFiles returns paths instead of filenames
                        // And other methods require filenames
                        paths[i] = path.Substring(separatorIndex + 1, path.Length - separatorIndex - 1 - json);
                    }

                    return paths;
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }

            protected static T Save(string baseFolder, string filename)
            {
                T data;
                if (!LoadedData.TryGetValue(filename, out data))
                    return null;

                Interface.Oxide.DataFileSystem.WriteObject(baseFolder + filename, data);
                return data;
            }

            protected static T Get(string baseFolder, string filename)
            {
                T data;
                if (LoadedData.TryGetValue(filename, out data))
                    return data;

                return null;
            }

            protected static T GetOrLoad(string baseFolder, string filename)
            {
                T data;
                if (LoadedData.TryGetValue(filename, out data))
                    return data;

                try
                {
                    data = Interface.Oxide.DataFileSystem.ReadObject<T>(baseFolder + filename);
                }
                catch (Exception e)
                {
                    Interface.Oxide.LogError(e.ToString());
                }

                return LoadedData[filename] = data;
            }

            protected static T GetOrCreate(string baseFolder, string path)
            {
                return GetOrLoad(baseFolder, path) ?? (LoadedData[path] = new T().SetDefaults());
            }

            protected virtual T SetDefaults()
            {
                throw new NotImplementedException();
            }
        }

        private class PlayerPreferences : SplitDatafile<PlayerPreferences>
        {
            // State

            [JsonIgnore]
            public BuildingGrade.Enum Grade = BuildingGrade.Enum.None;

            [JsonIgnore]
            public ulong Skin = 0ul;

            [JsonIgnore]
            public DateTime? LastUsed = null;

            [JsonIgnore]
            public bool Enabled => LastUsed != null;

            // Preferences

            public float? DisableIn = 30f;

            public bool AutoGrade = true;

            public void ResetState()
            {
                Grade = BuildingGrade.Enum.None;
                Skin = 0ul;

                LastUsed = null;
            }

            public static readonly string BaseFolder =
                nameof(EnhancedHammer) + Path.DirectorySeparatorChar + "Preferences" + Path.DirectorySeparatorChar;

            public static string[] GetFiles() => GetFiles(BaseFolder);
            public static PlayerPreferences Save(string filename) => Save(BaseFolder, filename);
            public static PlayerPreferences Get(string filename) => Get(BaseFolder, filename);
            public static PlayerPreferences GetOrLoad(string filename) => GetOrLoad(BaseFolder, filename);
            public static PlayerPreferences GetOrCreate(string filename) => GetOrCreate(BaseFolder, filename);

            protected override PlayerPreferences SetDefaults()
            {
                var defaults = _ins._config.Preferences;

                DisableIn = defaults.DisableIn;
                AutoGrade = defaults.AutoGrade;

                return this;
            }
        }

        #endregion

        #region Hooks

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Command: No Permission", $"You do not have enough permissions for that ({PermissionUse})" },
                { "Command: Player Only", "You must be an in-game player to use this command" },
                { "Command: Invalid Grade", "You have entered an invalid grade" },
                { "Command: Timeout", "Current timeout: {timeout}" },
                { "Command: Auto Grade: Enabled", "You have enabled automatic toggle for grading" },
                { "Command: Auto Grade: Disabled", "You have disabled automatic toggle for grading" },
                { "Command: Auto Grade: Force Disabled", "Auto Grade is disabled on this server" },
                {
                    "Command: Syntax", "Grade command syntax:\n" +
                                       "(grade) - Set current grade to a specific value\n" +
                                       "timeout (Time in seconds) - Disable grading in X seconds\n" +
                                       "autograde (True/False) - Toggle automatic grading toggle"
                },
                { "Grade: Changed", "Current grade: {grade}" },
                { "Grade: No Downgrade", "Downgrading is not allowed on this server" },
                { "Grade: Building Blocked", "You cannot build there" },
                { "Grade: Upgrade Blocked", "You cannot upgrade this" },
                { "Grade: Insufficient Resources", "You cannot afford this upgrade" },
                { "Grade: Recently Damaged", "This entity was recently damaged" }
            }, this);
        }

        private void Init()
        {
            _ins = this;

            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionFree, this);
            permission.RegisterPermission(PermissionDowngrade, this);
            permission.RegisterPermission(PermissionGradeHit, this);
            permission.RegisterPermission(PermissionGradeClick, this);
            permission.RegisterPermission(PermissionGradeBuild, this);

            if (!_config.AutoGrade)
                Unsubscribe(nameof(OnStructureUpgrade));

            ConvertOldData();

            AddCovalenceCommand(_config.Commands, nameof(CommandGrade));
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerDisconnected(player);

            PlayerPreferences.LoadedData.Clear();

            _ins = null;
        }

        private void OnPlayerDisconnected(BasePlayer basePlayer)
        {
            var preferences = PlayerPreferences.Save(basePlayer.UserIDString);

            preferences?.ResetState();

            CancelTimeoutUpdate(basePlayer.UserIDString);
        }

        private object OnHammerHit(BasePlayer basePlayer, HitInfo info)
        {
            var preferences = PlayerPreferences.GetOrCreate(basePlayer.UserIDString);
            if (!preferences.Enabled)
                return null;

            var block = info.HitEntity as BuildingBlock;
            if (block == null)
                return null;

            if (!CanUse(basePlayer.IPlayer, GradingType.Hit))
                return null;

            Upgrade(basePlayer, preferences, block, false);

            return _config.CancelHammerHitDefaultBehavior ? false : (object)null;
        }

        private void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            var player = planner.GetOwnerPlayer();
            if (player == null)
                return;

            var preferences = PlayerPreferences.GetOrCreate(player.UserIDString);
            if (!preferences.Enabled)
            {
#if DEBUG
                Puts($"{nameof(OnEntityBuilt)} ({player.UserIDString}) > Ignored: Disabled");
#endif
                return;
            }

            if (!CanUse(player.IPlayer, GradingType.Build))
            {
#if DEBUG
                Puts($"{nameof(OnEntityBuilt)} ({player.UserIDString}) > Ignored: Cannot use");
#endif
                return;
            }

            var block = gameObject.ToBaseEntity() as BuildingBlock;
            if (block == null)
            {
#if DEBUG
                Puts($"{nameof(OnEntityBuilt)} ({player.UserIDString}) > Ignored: No building block");
#endif
                return;
            }

            Upgrade(player, preferences, block, false);
        }

        private void OnStructureUpgrade(BuildingBlock block, BasePlayer basePlayer, BuildingGrade.Enum grade,
            ulong skin)
        {
            var preferences = PlayerPreferences.GetOrCreate(basePlayer.UserIDString);
            if (!preferences.AutoGrade)
                return;

            // Ignore if upgrading the same tier and enabled
            // (To enable if not)
            if (preferences.Enabled && preferences.Grade == grade && preferences.Skin == skin)
                return;

            if (!CanUse(basePlayer.IPlayer, GradingType.Use))
                return;

            SetActiveGrade(basePlayer, preferences, grade, skin, false);
        }

        private void OnPlayerInput(BasePlayer basePlayer, InputState input)
        {
            if (!input.IsDown(BUTTON.FIRE_PRIMARY))
                return;

            if (!(basePlayer.GetHeldEntity() is Hammer))
                return;

            var preferences = PlayerPreferences.GetOrCreate(basePlayer.UserIDString);
            if (!preferences.Enabled)
                return;

            if (!CanUse(basePlayer.IPlayer, GradingType.Click))
                return;

            RaycastHit hit;
            if (!Physics.Raycast(basePlayer.eyes.HeadRay(), out hit, _ins._config.Distance, _ins._maskConstruction))
                return;

            var block = hit.GetEntity() as BuildingBlock;
            if (block == null)
                return;

            Upgrade(basePlayer, preferences, block, true);
        }

        #endregion

        #region Commands

        private void CommandGrade(IPlayer player, string command, string[] args)
        {
            if (!CanUse(player, GradingType.Use))
            {
                player.Reply(GetMsg("Command: No Permission", player.Id));
                return;
            }

            var basePlayer = player.Object as BasePlayer;
            if (basePlayer == null)
            {
                player.Reply(GetMsg("Command: Player Only", player.Id));
                return;
            }

            if (args.Length == 0)
                goto syntax;

            switch (args[0].ToLower())
            {
                default:
                {
                    BuildingGrade.Enum grade;
                    if (!Enum.TryParse(args[0], true, out grade) || grade > BuildingGrade.Enum.TopTier ||
                        grade < BuildingGrade.Enum.None)
                    {
                        player.Reply(GetMsg("Command: Invalid Grade", player.Id));
                        return;
                    }

                    var preferences = PlayerPreferences.GetOrCreate(player.Id);

                    SetActiveGrade(basePlayer, preferences, grade, 0ul, false);
                    return;
                }

                case "t":
                case "timeout":
                {
                    if (args.Length < 2)
                        goto syntax;

                    float? timeout = null;
                    float parsedTimeout;
                    if (float.TryParse(args[1], out parsedTimeout))
                        timeout = parsedTimeout;

                    PlayerPreferences.GetOrCreate(player.Id).DisableIn = timeout;

                    player.Reply(GetMsg("Command: Timeout", player.Id)
                        .Replace("{timeout}", parsedTimeout.ToString("0.##")));

                    return;
                }

                case "ag":
                case "autograde":
                {
                    if (args.Length < 2)
                        goto syntax;

                    if (!_config.AutoGrade)
                    {
                        player.Reply(GetMsg("Command: Auto Grade: Force Disabled", player.Id));

                        return;
                    }

                    bool autograde;
                    if (!bool.TryParse(args[1], out autograde))
                        goto syntax;

                    PlayerPreferences.GetOrCreate(player.Id).AutoGrade = autograde;

                    player.Reply(GetMsg("Command: Auto Grade: " + (autograde ? "Enabled" : "Disabled"),
                        player.Id));

                    return;
                }
            }

            syntax:
            player.Reply(GetMsg("Command: Syntax", player.Id));
        }

        #endregion

        #region Availability Timers

        private Dictionary<string, Timer> _availabilityTimers = new Dictionary<string, Timer>();

        private void UpdateTimeout(BasePlayer basePlayer, PlayerPreferences preferences)
        {
            if (!preferences.Enabled || !preferences.DisableIn.HasValue)
                return;

            // ReSharper disable once PossibleInvalidOperationException
            var timePassed = (float)(DateTime.UtcNow - preferences.LastUsed.Value).TotalSeconds;
            var timeLeft = preferences.DisableIn.Value - timePassed;

#if DEBUG
            Puts($"{nameof(UpdateTimeout)} ({basePlayer.UserIDString}) > {timePassed:F1}s passed, {timeLeft:F1}s left");
#endif

            if (timeLeft < 0)
            {
                SetActive(basePlayer, preferences, false);
                return;
            }

            CancelTimeoutUpdate(basePlayer.UserIDString);

            _availabilityTimers[basePlayer.UserIDString] =
                timer.In(timeLeft, () => UpdateTimeout(basePlayer, preferences));
        }

        private void CancelTimeoutUpdate(string id)
        {
            Timer t;
            if (_availabilityTimers.TryGetValue(id, out t))
                timer.Destroy(ref t);
        }

        #endregion

        private void SetActiveGrade(BasePlayer basePlayer, PlayerPreferences preferences, BuildingGrade.Enum grade,
            ulong skin, bool suppressMessages)
        {
            if (preferences.Grade == grade && preferences.Skin == skin)
                return;

            if (grade > BuildingGrade.Enum.TopTier || grade < BuildingGrade.Enum.None)
                return;

            if (grade != BuildingGrade.Enum.None && !IsGradeEnabled(grade, skin))
                return;

            preferences.Grade = grade;
            preferences.Skin = skin;

            if (grade != BuildingGrade.Enum.None)
                SetActive(basePlayer, preferences, true);

            if (!suppressMessages)
            {
                var player = basePlayer.IPlayer;
                player.Message(GetMsg("Grade: Changed", player.Id)
                    .Replace("{grade}", grade.ToString()));
            }
        }

        private void SetActive(BasePlayer basePlayer, PlayerPreferences preferences, bool isActive)
        {
#if DEBUG
            Puts($"{nameof(SetActive)} ({basePlayer.UserIDString}) > {preferences.Enabled} -> {isActive}");
#endif

            if (preferences.Enabled == isActive)
            {
                if (!isActive)
                    return;

                // Update timeout if already active
                preferences.LastUsed = DateTime.UtcNow;
                UpdateTimeout(basePlayer, preferences);
                return;
            }

            if (isActive)
            {
                preferences.LastUsed = DateTime.UtcNow;

                _activePlayers.Add(basePlayer);
            }
            else
            {
                SetActiveGrade(basePlayer, preferences, BuildingGrade.Enum.None, 0ul, false);

                preferences.LastUsed = null;

                _activePlayers.Remove(basePlayer);
            }

            UpdateTimeout(basePlayer, preferences);

            if (_activePlayers.Count == 0) // -> 0
                Unsubscribe(nameof(OnPlayerInput));
            else if (isActive && _activePlayers.Count == 1) // 0 -> 1
                Subscribe(nameof(OnPlayerInput));
        }

        private void Upgrade(BasePlayer basePlayer, PlayerPreferences preferences, BuildingBlock block,
            bool suppressMessages)
        {
            // Ensure it's active and update the timeout
            SetActive(basePlayer, preferences, true);

            // Ignore if there is no need to upgrade
            if (preferences.Grade == block.grade && preferences.Skin == block.skinID)
            {
#if DEBUG
                Puts($"{nameof(Upgrade)} ({basePlayer.UserIDString}) > Ignored: Same grade");
#endif
                return;
            }

            var player = basePlayer.IPlayer;

            if (preferences.Grade < block.grade && !player.HasPermission(PermissionDowngrade))
            {
                if (!suppressMessages && _config.DowngradeMessage)
                {
                    player.Message(GetMsg("Grade: No Downgrade", player.Id));
                }

#if DEBUG
                Puts($"{nameof(Upgrade)} ({basePlayer.UserIDString}) > Ignored: No downgrade");
#endif
                return;
            }

            if (basePlayer.IsBuildingBlocked(block.transform.position, block.transform.rotation, block.bounds))
            {
                if (!suppressMessages)
                {
                    player.Message(GetMsg("Grade: Building Blocked", player.Id));
                }

#if DEBUG
                Puts($"{nameof(Upgrade)} ({basePlayer.UserIDString}) > Ignored: Building blocked");
#endif
                return;
            }

            if (block.IsUpgradeBlocked())
            {
                if (!suppressMessages)
                {
                    player.Message(GetMsg("Grade: Upgrade Blocked", player.Id));
                }

#if DEBUG
                Puts($"{nameof(Upgrade)} ({basePlayer.UserIDString}) > Ignored: Upgrade blocked");
#endif
                return;
            }

            if (block.SecondsSinceAttacked <= 30f)
            {
                if (!suppressMessages)
                {
                    player.Message(GetMsg("Grade: Recently Damaged", player.Id));
                }

#if DEBUG
                Puts($"{nameof(Upgrade)} ({basePlayer.UserIDString}) > Ignored: Recently damaged");
#endif
                return;
            }

            var grade = block.blockDefinition.GetGrade(preferences.Grade, preferences.Skin);
            if (grade == null)
            {
                PrintWarning($"'{nameof(grade)}' == null! Contact the plugin developer as this should never happen");
                return;
            }

            var hookResult =
                Interface.CallHook("CanChangeGrade", basePlayer, block, preferences.Grade, preferences.Skin);

            if (hookResult is bool && !(bool)hookResult)
            {
#if DEBUG
                Puts($"{nameof(Upgrade)} ({basePlayer.UserIDString}) > Ignored: CanChangeGrade returned false");
#endif
                return;
            }

            if (!player.HasPermission(PermissionFree))
            {
                var costToBuild = grade.CostToBuild(block.grade);
                foreach (var item in costToBuild)
                {
                    if (basePlayer.inventory.GetAmount(item.itemid) >= item.amount)
                        continue;

                    if (!suppressMessages)
                    {
                        player.Message(GetMsg("Grade: Insufficient Resources", player.Id));
                    }

#if DEBUG
                    Puts($"{nameof(Upgrade)} ({basePlayer.UserIDString}) > Ignored: Insufficient resources");
#endif
                    return;
                }

                var items = Pool.GetList<Item>();
                foreach (var item in costToBuild)
                {
                    basePlayer.inventory.Take(items, item.itemid, (int)item.amount);
                    basePlayer.Command("note.inv", item.itemid, item.amount * -1f);
                }

                foreach (var item in items)
                {
                    item.Remove();
                }

                ItemManager.DoRemoves();
                Pool.FreeList(ref items);
            }

#if DEBUG
            Puts(
                $"{nameof(Upgrade)} ({basePlayer.UserIDString}) > Upgraded to {preferences.Grade} ({preferences.Skin})");
#endif

            block.ClientRPC(null, "DoUpgradeEffect", (int)preferences.Grade, preferences.Skin);
            block.skinID = preferences.Skin;
            block.ChangeGrade(preferences.Grade, true);
        }

        #region Helpers

        private enum GradingType : byte
        {
            Use,
            Hit,
            Click,
            Build
        }

        private bool IsGradeEnabled(BuildingGrade.Enum grade, ulong skin)
        {
            foreach (var gradeEnabled in _config.GradesEnabled)
            {
                if (gradeEnabled.Grade != grade)
                {
                    continue;
                }

                if (gradeEnabled.Skin == null)
                {
                    return true;
                }

                if (gradeEnabled.Skin == skin)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool CanUse(IPlayer player, GradingType type)
        {
            if (_ins == null)
                return false;

            if (!player.HasPermission(PermissionUse))
                return false;

            switch (type)
            {
                case GradingType.Hit:
                {
                    return player.HasPermission(PermissionGradeHit);
                }

                case GradingType.Click:
                {
                    return player.HasPermission(PermissionGradeClick);
                }

                case GradingType.Build:
                {
                    return player.HasPermission(PermissionGradeBuild);
                }

                default:
                {
                    return true;
                }
            }
        }

        private static string GetMsg(string key, string userId = null) => _ins.lang.GetMessage(key, _ins, userId);

        #endregion
    }
}