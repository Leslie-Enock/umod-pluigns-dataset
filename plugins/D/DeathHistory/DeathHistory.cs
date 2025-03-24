using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Death History", "MadKingCraig", "1.2.1")]
    [Description("Get the locations of previous deaths.")]
    class DeathHistory : RustPlugin
	{
        #region Fields

        private const string CanUsePermission = "deathhistory.use";
        private const string AdminPermission = "deathhistory.admin";

        private PluginConfiguration _configuration;
        private DynamicConfigFile _data;

        DeathHistoryData dhData;

        private Dictionary<string, List<List<float>>> _deaths = new Dictionary<string, List<List<float>>>();
        private float _worldSize = (ConVar.Server.worldsize);
        private bool _newSaveDetected;

        #endregion

        #region Commands

        [Command("deaths")]
        private void CommandDeaths(IPlayer user, string command, string[] args)
        {
            if (args.Length > 1 || user.IsServer)
                return;

            var player = user.Object as BasePlayer;

            if (!permission.UserHasPermission(player.UserIDString, CanUsePermission))
            {
                user.Reply(lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                if (_deaths.ContainsKey(player.UserIDString))
                {
                    List<string> deathLocations = SendCorpseLocations(player);
                    if (deathLocations.Count > _configuration.MaxNumberOfDeaths)
                        deathLocations.RemoveRange(0, (deathLocations.Count - _configuration.MaxNumberOfDeaths));
                    string order = "Oldest Death";
                    if (!_configuration.OldestFirst)
                    {
                        deathLocations.Reverse();
                        order = "Most Recent Death";
                    }
                    string message = string.Join("\n", deathLocations);
                    user.Reply(string.Format(lang.GetMessage("DeathLocation", this, player.UserIDString), player.displayName, order, message));
                }
                else
                    user.Reply(string.Format(lang.GetMessage("UnknownLocation", this, player.UserIDString), player.displayName));
                return;
            }

            if (args.Length >= 1 && !permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                user.Reply(lang.GetMessage("NoPermission", this, player.UserIDString));
                return;
            }

            if (args[0] == "wipe_data")
            {
                NewData();
                user.Reply(string.Format(lang.GetMessage("DataWiped", this, player.UserIDString)));
                return;
            }

            var target = BasePlayer.FindAwakeOrSleeping(args[0]);
            if (target == null)
            {
                user.Reply(string.Format(lang.GetMessage("PlayerNotFound", this, player.UserIDString), args[0]));
                return;
            }

            if (_deaths.ContainsKey(target.UserIDString))
            {
                List<string> deathLocations = SendCorpseLocations(target);
                if (deathLocations.Count > _configuration.MaxNumberOfDeaths)
                    deathLocations.RemoveRange(0, (deathLocations.Count - _configuration.MaxNumberOfDeaths));
                string order = "Oldest Death";
                if (!_configuration.OldestFirst)
                {
                    deathLocations.Reverse();
                    order = "Most Recent Death";
                }
                string message = string.Join("\n", deathLocations);
                user.Reply(string.Format(lang.GetMessage("DeathLocation", this, player.UserIDString), target.displayName, order, message));
            }
            else
                user.Reply(string.Format(lang.GetMessage("UnknownLocation", this, player.UserIDString), target.displayName));
        }

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(CanUsePermission, this);
            permission.RegisterPermission(AdminPermission, this);
        }

        private void Loaded()
        {
            _data = Interface.Oxide.DataFileSystem.GetFile("death_history_data");
        }

        private void OnServerInitialized()
        {
            try
            {
                _configuration = Config.ReadObject<PluginConfiguration>();
                Config.WriteObject(_configuration);
            }
            catch
            {
                Puts("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
            

            AddCovalenceCommand("deaths", nameof(CommandDeaths));

            LoadData();
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["DeathLocation"] = "{0}'s Death Locations ({1} First)\n{2}",
                ["UnknownLocation"] = "{0}'s last death location is unknown.",
                ["NoPermission"] = "You do not have permission to use this command.",
                ["PlayerNotFound"] = "{0} not found.",
                ["DataWiped"] = "Data has been wiped."
            }, this);
        }

        private void OnNewSave(string filename)
        {
            _newSaveDetected = true;
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;

            if (entity.gameObject == null) return;

            var player = entity as BasePlayer;

            if (player == null || entity.IsNpc || !player.userID.IsSteamId()) return;

            string userID = player.UserIDString;
            Vector3 deathPosition = entity.transform.position;
            List<float> shortDeathPosition = new List<float> { deathPosition.x, deathPosition.y, deathPosition.z };

            if (!_deaths.ContainsKey(userID))
                _deaths.Add(userID, new List<List<float>>());

            if (_deaths[userID].Count >= _configuration.MaxNumberOfDeaths)
                _deaths[userID].RemoveRange(0, (_deaths[userID].Count - _configuration.MaxNumberOfDeaths) + 1);

            _deaths[userID].Add(shortDeathPosition);

            SaveData();
        }

        #endregion

        #region Configuration

        protected override void LoadDefaultConfig() => _configuration = new PluginConfiguration();

        private sealed class PluginConfiguration
        {
            [JsonProperty(PropertyName = "Number of Deaths to Keep")]
            public int MaxNumberOfDeaths = 5;
            [JsonProperty(PropertyName = "Oldest Death First (false = Most Recent Death First)")]
            public bool OldestFirst = true;
            [JsonProperty(PropertyName = "Display Grid (false = Coordinates)")]
            public bool DisplayGrid = true;
        }

        #endregion

        #region Functions

        private string CalculateGridPosition(Vector3 position)
        {
            int maxGridSize = Mathf.FloorToInt(World.Size / 146.3f) - 1;
            int xGrid = Mathf.Clamp(Mathf.FloorToInt((position.x + (World.Size / 2f)) / 146.3f), 0, maxGridSize);
            string extraA = string.Empty;
            if (xGrid > 26) extraA = $"{(char)('A' + (xGrid / 26 - 1))}";
            return $"{extraA}{(char)('A' + xGrid % 26)}{Mathf.Clamp(maxGridSize - Mathf.FloorToInt((position.z + (World.Size / 2f)) / 146.3f), 0, maxGridSize).ToString()}";
        }

        private List<string> GetGrids(List<Vector3> deathLocations)
        {
            List<string> deathGrids = new List<string>();
            
            foreach (var location in deathLocations)
            {
                deathGrids.Add(CalculateGridPosition(location));
            }
            
            return deathGrids;
        }

        private List<string> GetCoordinates(List<Vector3> deathLocations)
        {
            List<string> deathGrids = new List<string>();

            foreach (var location in deathLocations)
            {
                deathGrids.Add($"({Mathf.Round(location[0])}, {Mathf.Round(location[1])}, {Mathf.Round(location[2])})");
            }

            return deathGrids;
        }

        private List<string> SendCorpseLocations(BasePlayer player)
        {
            List<List<float>> shortDeathLocations = _deaths[player.UserIDString];
            List<Vector3> allDeathLocations = new List<Vector3>();
            foreach (var location in shortDeathLocations)
            {
                allDeathLocations.Add(new Vector3(location[0], location[1], location[2]));
            }

            return _configuration.DisplayGrid ? GetGrids(allDeathLocations) : GetCoordinates(allDeathLocations);
        }

        #endregion

        #region Data Management

        private void SaveData()
        {
            dhData.Deaths = _deaths;
            _data.WriteObject(dhData);
        }

        private void LoadData()
        {
            try
            {
                if (_newSaveDetected)
                {
                    dhData = new DeathHistoryData();
                    _deaths = new Dictionary<string, List<List<float>>>();
                }
                else
                {
                    dhData = _data.ReadObject<DeathHistoryData>();
                    _deaths = dhData.Deaths;
                }
            }
            catch
            {
                dhData = new DeathHistoryData();
            }
        }

        private void NewData()
        {
            dhData = new DeathHistoryData();
            _deaths = new Dictionary<string, List<List<float>>>();
        }

        public class DeathHistoryData
        {
            public Dictionary<string, List<List<float>>> Deaths = new Dictionary<string, List<List<float>>>();
        }

        #endregion
    }
}
