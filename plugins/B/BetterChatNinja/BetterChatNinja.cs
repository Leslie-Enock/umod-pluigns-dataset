//#define DEBUG

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("Better Chat Ninja", "misticos", "1.0.2")]
    [Description("Hide your ranks from other players and vanish like a ninja in the chat")]
    class BetterChatNinja : CovalencePlugin
    {
        #region Configuration

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty("Commands", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] Commands = { "ninja", "toggle" };

            [JsonProperty("Save Preferences")]
            public bool Save = true;
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
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion

        #region Work with Data

        private PluginData _data = new PluginData();

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

            if (_data == null) _data = new PluginData();
        }

        private class PluginData
        {
            [JsonProperty("Hidden")]
            public HashSet<string> Hidden = new HashSet<string>();
        }

        #endregion

        [PluginReference("BetterChat")]
        private Plugin _chat = null;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Command: Toggle: Hidden", "You have <color=#5cb85c>hidden</color> your rank in the chat" },
                { "Command: Toggle: Shown", "Your rank will be <color=#d9534f>shown</color> in the chat" }
            }, this);
        }

        private void Init()
        {
            AddCovalenceCommand(_config.Commands, nameof(CommandToggle));

            // Unsubscribe from the save hook in case saving is disabled in configuration

            if (!_config.Save)
            {
                Unsubscribe(nameof(OnServerSave));
            }
        }

        private void Loaded()
        {
            // Only load data from the datafile when it is enabled in configuration

            if (_config.Save)
            {
                LoadData();
            }

            PullDefaultGroupProperties();
        }

        private void PullDefaultGroupProperties()
        {
            // Retry in case the plugin is not loaded

            if (_chat == null || !_chat.IsLoaded)
            {
                PrintWarning("Better Chat was not found. Please, install the plugin! Will retry in 30 seconds");
                timer.Once(30f, PullDefaultGroupProperties);
                return;
            }

            // Grab properties for the default group

            _defaultGroupProperties = _chat.Call<Dictionary<string, object>>("API_GetGroupFields",
                Interface.Oxide.Config.Options.DefaultGroups.Players);

            var title = (string)_defaultGroupProperties["Title"];
            var titleSize = (int)_defaultGroupProperties["TitleSize"];
            var titleColor = (string)_defaultGroupProperties["TitleColor"];

            titleColor = titleColor.StartsWith("#") ? titleColor.Substring(1) : titleColor;

            _defaultTitleFormatted = $"[#{(titleColor)}][+{titleSize}]{title}[/+][/#]";
            _defaultTitleHidden = (bool)_defaultGroupProperties["TitleHidden"];

            _defaultUsernameColor = _defaultGroupProperties["UsernameColor"];
            _defaultUsernameSize = _defaultGroupProperties["UsernameSize"];

            _defaultMessageColor = _defaultGroupProperties["MessageColor"];
            _defaultMessageSize = _defaultGroupProperties["MessageSize"];
            
            _defaultChatFormat = _defaultGroupProperties["ChatFormat"];
            _defaultConsoleFormat = _defaultGroupProperties["ConsoleFormat"];

#if DEBUG
            Puts($"Obtained default group properties: {JsonConvert.SerializeObject(_defaultGroupProperties)}");
#endif
        }

        private void OnServerSave()
        {
            // Save the data file
            // Hook will not be called if disabled in the configuration

            SaveData();
        }

        private void CommandToggle(IPlayer player, string command, string[] args)
        {
            if (_data.Hidden.Add(player.Id))
            {
                player.Reply(GetMsg("Command: Toggle: Hidden", player.Id));
            }
            else
            {
                _data.Hidden.Remove(player.Id);
                player.Reply(GetMsg("Command: Toggle: Shown", player.Id));
            }
        }

        private Dictionary<string, object> _defaultGroupProperties = null;
        private string _defaultTitleFormatted = null;
        private bool _defaultTitleHidden = false;
        private object _defaultUsernameColor;
        private object _defaultUsernameSize;
        private object _defaultMessageColor;
        private object _defaultMessageSize;
        private object _defaultChatFormat;
        private object _defaultConsoleFormat;

        private void OnBetterChat(Dictionary<string, object> data)
        {
            // Ignore if the default group properties have not been grabbed yet

            if (_defaultGroupProperties == null)
            {
                PrintWarning("No default group properties have been obtained, make sure Better Chat has such group configured");
                return;
            }

            // Do nothing if there is no player or if the player is not "hidden"

            var target = (IPlayer)data["Player"];
            if (target == null)
            {
                PrintWarning("Player was null, contact the developer");
                return;
            }

            if (!_data.Hidden.Contains(target.Id))
            {
#if DEBUG
                Puts("Player is NOT hidden, ignoring");
#endif
                return;
            }

#if DEBUG
            Puts($"Data prior to modification: {JsonConvert.SerializeObject(data)}");
#endif

            // Only leave the default group in titles

            var titles = (List<string>)data["Titles"];

            titles.Clear();

            if (!_defaultTitleHidden)
            {
                titles.Add(_defaultTitleFormatted);
            }

            // Change primary group to the default group

            data["PrimaryGroup"] = Interface.Oxide.Config.Options.DefaultGroups.Players;

            // Update username settings to the default group's settings

            var usernameSettings = (Dictionary<string, object>)data["UsernameSettings"];

            usernameSettings["Color"] = _defaultUsernameColor;
            usernameSettings["Size"] = _defaultUsernameSize;

            // Update message settings to the default group's settings

            var messageSettings = (Dictionary<string, object>)data["MessageSettings"];

            messageSettings["Color"] = _defaultMessageColor;
            messageSettings["Size"] = _defaultMessageSize;

            // Update format settings to the default group's settings

            var formatSettings = (Dictionary<string, object>)data["FormatSettings"];

            formatSettings["Chat"] = _defaultChatFormat;
            formatSettings["Console"] = _defaultConsoleFormat;

            // for other developers:

            // i know i could replace the dictionary for the optimization purposes
            // but if any other plugin updates it
            // it will change the group settings for all other messages
            // so I cannot use my own dictionary here
            // and would rather modify an existing one

#if DEBUG
            Puts($"Data after modification: {JsonConvert.SerializeObject(data)}");
#endif
        }

        private string GetMsg(string key, string id) => lang.GetMessage(key, this, id);
    }
}