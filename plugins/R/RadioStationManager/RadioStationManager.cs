
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using System.Collections.Generic;
using System.Linq;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("Radio Station Manager", "Whispers88", "1.0.4")]
    [Description("Allows you to easily add and remove radio stations")]

    public class RadioStationManager : RustPlugin
    {
        #region Config
        private Configuration config;
        public class Configuration
        {
            [JsonProperty("Radio Stations (station name, station url)", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, string> RadioStations = new Dictionary<string, string>();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Config

        #region Init
        private const string permallow = "radiostationmanager.allow";

        private List<string> commands = new List<string> { nameof(StationAddCMD), nameof(StationRemoveCMD), nameof(StationListCMD), nameof(StationClearCMD) };

        private void OnServerInitialized()
        {
            //register permissions
            permission.RegisterPermission(permallow, this);
            //register commands
            commands.ForEach(command => AddLocalizedCommand(command));

            //Add preexisting stations to config
            if (!BoomBox.ServerUrlList.IsNullOrEmpty() && config.RadioStations.Count == 0)
            {
                string[] strArrays = BoomBox.ServerUrlList.Split(new char[] { ',' });
                if ((int)strArrays.Length % 2 != 0)
                {
                    Puts("Invalid server URL list, skipping recovery, please add new stations via cmd or config");
                    BoomBox.ServerUrlList = string.Empty;

                    UpdateStns();
                    return;
                }
                for (int i = 0; i < (int)strArrays.Length; i += 2)
                {
                    if (!config.RadioStations.ContainsKey(strArrays[i]))
                    {
                        config.RadioStations.Add(strArrays[i], strArrays[i + 1].Replace(" ", string.Empty));
                    }
                }
            }
            UpdateStns();
        }

        #endregion Init

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["ArgsReq1"] = "Invalid args use: /stnremove stnNAME",
                ["ArgsReq2"] = "Invalid args use: /stnadd stnNAME,stnURL",
                ["NoPerms"] = "You don't have permission to use this command.",
                ["StnNA"] = "Station not found",
                ["StnAdded"] = "Radio Station {0} was added url:{1}",
                ["StnRemoved"] = "Radio Station {0} was removed",
                ["StnsCleared"] = "All custom stations have been cleared",
                ["StnsList"] = "Custom Stations: {0}",
                //Commands
                ["StationAddCMD"] = "stnadd",
                ["StationRemoveCMD"] = "stnremove",
                ["StationListCMD"] = "stnlist",
                ["StationClearCMD"] = "stnclear"
            }, this);
        }

        #endregion Localization

        #region Commands
        private void StationAddCMD(IPlayer iplayer, string command, string[] args)
        {
            if (!HasPerm(iplayer.Id, permallow))
            {
                Message(iplayer, "NoPerms");
                return;
            }
            string[] arg = args.Aggregate((a, b) => a + ' ' + b).Split(',');
            if (arg.Length < 2)
            {
                Message(iplayer, "ArgsReq2");
                return;
            }

            config.RadioStations[arg[0]] = arg[1].Replace(" ", string.Empty);

            Message(iplayer, "StnAdded", arg[0], arg[1]);
            UpdateStns();
        }

        private void StationRemoveCMD(IPlayer iplayer, string command, string[] args)
        {
            if (!HasPerm(iplayer.Id, permallow))
            {
                Message(iplayer, "NoPerms");
                return;
            }
            if (args.Length < 1)
            {
                Message(iplayer, "ArgsReq1");
                return;
            }
            string stnname = args.Aggregate((a, b) => a + ' ' + b);
            if (!config.RadioStations.ContainsKey(stnname))
            {
                Message(iplayer, "StnNA");
                return;
            }
            config.RadioStations.Remove(stnname);

            Message(iplayer, "StnRemoved", stnname);
            UpdateStns();
        }
        private void StationListCMD(IPlayer iplayer, string command, string[] args)
        {
            if (!HasPerm(iplayer.Id, permallow))
            {
                Message(iplayer, "NoPerms");
                return;
            }
            string stn = string.Empty;
            foreach (var station in config.RadioStations)
            {
                stn += station.Key + ", ";
            }
            Message(iplayer, "StnsList", stn);
        }

        private void StationClearCMD(IPlayer iplayer, string command, string[] args)
        {
            if (!HasPerm(iplayer.Id, permallow))
            {
                Message(iplayer, "NoPerms");
                return;
            }
            config.RadioStations.Clear();
            BoomBox.ServerUrlList = string.Empty;
            Message(iplayer, "StnsCleared");
            UpdateStns();
        }
        #endregion Commands

        #region Methods
        private void UpdateStns()
        {
            List<string> liststr = new List<string>();
            if (!BoomBox.ServerValidStations.IsNullOrEmpty())
                BoomBox.ServerValidStations.Clear();

            if (!config.RadioStations.IsEmpty())
            {
                foreach (var station in config.RadioStations)
                {
                    string stn = station.Key + "," + station.Value + ",";
                    if (!liststr.Contains(stn))
                        liststr.Add(stn);
                }

                string urllist = string.Concat(liststr);
                Server.Command($"BoomBox.ServerUrlList \"{urllist.Remove(urllist.Length - 1, 1)}\"");
            }

            SaveConfig();
        }
        #endregion Methods

        #region Helpers
        private string GetLang(string langKey, string playerId = null, params object[] args)
        {
            return string.Format(lang.GetMessage(langKey, this, playerId), args);
        }
        private void Message(IPlayer player, string langKey, params object[] args)
        {
            if (player.IsConnected) player.Message(GetLang(langKey, player.Id, args));
        }

        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);

        private void AddLocalizedCommand(string command)
        {
            foreach (string language in lang.GetLanguages(this))
            {
                Dictionary<string, string> messages = lang.GetMessages(language, this);
                foreach (KeyValuePair<string, string> message in messages)
                {
                    if (!message.Key.Equals(command)) continue;

                    if (string.IsNullOrEmpty(message.Value)) continue;

                    AddCovalenceCommand(message.Value, command);
                }
            }
        }
        #endregion Helpers
    }
}