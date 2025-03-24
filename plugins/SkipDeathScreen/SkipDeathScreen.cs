using System;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using ProtoBuf;
using UnityEngine;

namespace Oxide.Plugins;

[Info("Skip Death Screen", "Notchu", "1.0.5")]
[Description("Skip death screen if there is no available Sleeping bags")]
public class SkipDeathScreen : CovalencePlugin
{
    private class Configuration
    {
        [JsonProperty("Ignore sleeping bags cooldown")]
        public bool IgnoreTimers = false;

        [JsonProperty("Allow users to change IgnoreTimers value for themself")]
        public bool AllowChangeSettings = true;
    }

    private Configuration _config;

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

    #region Work with Data

    private abstract class SplitDatafile<T> where T : SplitDatafile<T>, new()
    {
        public static Dictionary<string, T> LoadedData = new();

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

        protected static bool Delete(string baseFolder, string filename)
        {
            if (!LoadedData.Remove(filename)) return false;

            Interface.Oxide.DataFileSystem.DeleteDataFile(baseFolder + filename);

            return true;
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

        protected static T Create(string baseFolder, string filename)
        {
            return LoadedData[filename] = new T();
        }
    }

    private class SkipDeathScreenData : SplitDatafile<SkipDeathScreenData>
    {
        public bool IgnoreTimers { get; set; }

        public static readonly string BaseFolder = "SkipDeath" + Path.DirectorySeparatorChar +
                                                   "IgnoreTimers" + Path.DirectorySeparatorChar;

        public static string[] GetFiles()
        {
            return GetFiles(BaseFolder);
        }

        public static bool Delete(string filename)
        {
            return Delete(BaseFolder, filename);
        }

        public static SkipDeathScreenData Save(string filename)
        {
            return Save(BaseFolder, filename);
        }

        public static SkipDeathScreenData Get(string filename)
        {
            return Get(BaseFolder, filename);
        }

        public static SkipDeathScreenData GetOrLoad(string filename)
        {
            return GetOrLoad(BaseFolder, filename);
        }

        public static SkipDeathScreenData Create(string filename)
        {
            return Create(BaseFolder, filename);
        }
    }

    #endregion

    protected override void SaveConfig()
    {
        Config.WriteObject(_config);
    }

    protected override void LoadDefaultConfig()
    {
        _config = new Configuration();
    }

    protected override void LoadDefaultMessages()
    {
        lang.RegisterMessages(new Dictionary<string, string>
        {
            { "Command: Timers: Enabled", "Now cool down will not be ignored for skipping death screen" },
            { "Command: Timers: Disabled", "Now cool down will be ignored for skipping death screen" },
            { "Command: not allowed by server", "This command is not allowed on this server."},
            { "Command: no permission", "You dont have permission to use this command"}
        }, this);
    }

    private void Init()
    {
        permission.RegisterPermission("skipDeathScreen.use", this);
    }

    private void Unload()
    {
        SkipDeathScreenData.LoadedData.Clear();
    }

    private void OnEntityDeath(BasePlayer player, HitInfo info)
    {
        if (player.IsNpc) return;

        var hasPermission = permission.UserHasPermission(player.UserIDString, "skipDeathScreen.use");

        if (!hasPermission) return;

        var localIgnoreTimers = _config.IgnoreTimers;
        if (_config.AllowChangeSettings && SkipDeathScreenData.GetOrLoad(player.UserIDString) is { } data)
            localIgnoreTimers = data.IgnoreTimers;

        var bags = SleepingBag.FindForPlayer(player.userID, localIgnoreTimers);

        if (bags.Length == 0)
            NextTick(() =>
            {
                if (player.IsDead() && player.IsConnected) player.Respawn();
            });
    }

    [Command("ToggleTimers")]
    private async void ChangeTimerSettings(IPlayer player, string command, string[] args)
    {
        if (!_config.AllowChangeSettings)
        {
            player.Message(lang.GetMessage("Command: not allowed by server", this, player.Id));
            return;
        }

        var hasPermission = permission.UserHasPermission(player.Id, "skipDeathScreen.use");

        if (!hasPermission)
        {
            player.Message(lang.GetMessage("Command: no permission", this, player.Id));
            return;
        }

        var data = SkipDeathScreenData.Get(player.Id);
        if (data == null)
        {
            data = SkipDeathScreenData.Create(player.Id);
            data.IgnoreTimers = _config.IgnoreTimers;
        }

        data.IgnoreTimers = !data.IgnoreTimers;

        SkipDeathScreenData.Save(player.Id);

        player.Message(lang.GetMessage(data.IgnoreTimers ? "Command: Timers: Enabled" : "Command: Timers: Disabled",
            this, player.Id));
    }
}