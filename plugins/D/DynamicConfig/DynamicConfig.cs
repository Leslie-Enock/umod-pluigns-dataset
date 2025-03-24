using Oxide.Core;
using System.Collections.Generic;
using System;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;
using System.Text;
using Oxide.Core.Configuration;

namespace Oxide.Plugins
{
    [Info("Dynamic Config", "FastBurst", "1.0.5")]
    [Description("Dynamically changes configs of other plugins depending on time from last wipe")]
    class DynamicConfig : RustPlugin
    {
        private static DynamicConfig Instance;
        private ConfigLibrary configLibrary;
        private Timer updateTimer;
        private DynamicConfigFile configHandler;
        private DateTime WipeDate;

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["WipeDate"] = "Detected last wipe at: {0}",
                ["NoConfigs"] = "There is no configs found, please create them in {0}",
                ["FoundPlugin"] = "\"{0}\": {1}",
                ["TimePassed"] = "Time passed from last wipe: {0}",
                ["ErrorTimeSpan"] = "Invalid config name: \"{0}\", unable to parse time span",
                ["Update"] = "Updating \"{0}\"",
                ["InvalidPlugin"] = "Plugin \"{0}\" doesn't exist, check the filename \"{1}\"",
                ["Scan"] = "Scanning directory {0}",
                ["Total"] = "Added {0} configs",
                ["PluginNotUnloaded"] = "Plugin \"{0}\" was failed to unload",
                ["PluginNotLoaded"] = "Plugin \"{0}\" was failed to load",

            }, this);
        }

        private static string _(string message, params object[] args)
        {
            return string.Format(Instance.lang.GetMessages(Instance.lang.GetServerLanguage(), Instance)[message], args);
        }
        #endregion

        #region Hooks
        private void Init()
        {
            Instance = this;
            configHandler = new DynamicConfigFile(null); // We use it only to get read/write access to oxide/config directory via filename
        }

        private void OnServerInitialized()
        {
            var saveCreated = ConVar.Admin.ServerInfo().SaveCreatedTime;
            DateTime.TryParse(saveCreated, out this.WipeDate);
            WipeDate = this.WipeDate.ToLocalTime();
            Puts(_("WipeDate", this.WipeDate));
            Puts(_("TimePassed", TimeSpanToString(DateTime.Now.Subtract(this.WipeDate))));
            configLibrary = new ConfigLibrary();
            timer.Once(5, this.configLibrary.Update);
            updateTimer = timer.Every(60, this.configLibrary.Update);
        }

        private void Unload()
        {
            if (updateTimer != null && !this.updateTimer.Destroyed)
                updateTimer.Destroy();
            Instance = null;
        }
        #endregion

        #region Misc
        private static string DataPath
        {
            get
            {
                return $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{Instance.Name}{Path.DirectorySeparatorChar}";
            }
        }

        private static DataFileSystem DFS
        {
            get
            {
                return Interface.Oxide.DataFileSystem;
            }
        }
        #endregion

        #region Functions
        class ConfigLibrary
        {
            Dictionary<string, PluginInfo> pluginInfos = new();

            public ConfigLibrary()
            {
                int i = 1;
                try
                {
                    Instance.Puts(_("Scan", DataPath));
                    var files = DFS.GetFiles(DataPath);
                    foreach (var file in files)
                    {
                        var filename = file.Split(Path.DirectorySeparatorChar).Last();
                        filename = Utility.GetFileNameWithoutExtension(filename);
                        Match timespanRegex = new Regex(@"(\d+?[dhm])+", RegexOptions.IgnoreCase).Match(filename);
                        string timeSpanStr;
                        TimeSpan timeSpan = new TimeSpan();
                        var pluginName = filename;
                        if (timespanRegex.Success)
                        {
                            timeSpanStr = timespanRegex.Groups[0].ToString();
                            if (!TryParseTimeSpan(timeSpanStr, out timeSpan))
                            {
                                Instance.PrintError(_("ErrorTimeSpan", filename));
                                continue;
                            }

                            pluginName = filename.Replace(timeSpanStr, "");
                        }

                        if (!Instance.plugins.Exists(pluginName) && !Interface.Oxide.LoadPlugin(pluginName))
                        {
                            Instance.PrintWarning(_("InvalidPlugin", pluginName, filename));
                            continue;
                        }

                        var applyTime = Instance.WipeDate.Add(timeSpan);
                        Instance.Puts($"{i}. {_("FoundPlugin", pluginName, applyTime)}");
                        if (!pluginInfos.ContainsKey(pluginName))
                            pluginInfos[pluginName] = new PluginInfo(pluginName);

                        pluginInfos[pluginName].AddConfig(filename, applyTime);
                        i++;
                    }
                }
                catch (Exception ex)
                {
                    Instance.PrintWarning($"Error: {ex.Message}");
                }

                if (pluginInfos.Count == 0)
                {
                    Instance.PrintWarning(_("NoConfigs", DataPath));
                }
                else
                {
                    Instance.Puts(_("Total", i - 1));
                }
            }

            public void Update()
            {
                foreach (var info in pluginInfos)
                    info.Value.Update();
            }
        }

        class PluginInfo
        {
            private string pluginName;
            private List<ConfigInfo> configs = new List<ConfigInfo>();
            public PluginInfo(string pluginName)
            {
                this.pluginName = pluginName;
            }

            public void AddConfig(string filename, DateTime applyTime)
            {
                configs.Add(new ConfigInfo(filename, applyTime));
                configs.Sort((a, b) => a.applyTime.CompareTo(b.applyTime));
            }

            public void Update()
            {
                var pastConfigs = this.configs.Where(cfg => cfg.applyTime < DateTime.Now);
                if (pastConfigs.Count() == 0 || pastConfigs.Last() == null || pastConfigs.Last().applied)
                    return;
                pastConfigs.Last().Load(pluginName);
            }
        }

        class ConfigInfo
        {
            public string filename;
            public DateTime applyTime;
            public bool applied = false;

            public ConfigInfo(string filename, DateTime applyTime)
            {
                this.filename = filename;
                this.applyTime = applyTime;
            }

            public void Load(string pluginName)
            {
                Instance.Puts(_("Update", pluginName));
                if (!Interface.Oxide.UnloadPlugin(pluginName))
                    Instance.PrintWarning(_("PluginNotUnloaded", pluginName));
                var path = $"{DataPath}{filename}";
                var newConfig = DFS.ReadObject<object>(path);
                Instance.configHandler.WriteObject(newConfig, true, GetConfigPath(pluginName));
                if (!Interface.Oxide.LoadPlugin(pluginName))
                    Instance.PrintWarning(_("PluginNotLoaded", pluginName));
                this.applied = true;
            }
        }
        #endregion       

        #region Utilities
        private static bool TryParseTimeSpan(string source, out TimeSpan timeSpan)
        {
            int hours = 0;
            int days = 0;
            int minutes = 0;

            Match h = new Regex(@"(\d+?)h", RegexOptions.IgnoreCase).Match(source);
            Match d = new Regex(@"(\d+?)d", RegexOptions.IgnoreCase).Match(source);
            Match m = new Regex(@"(\d+?)m", RegexOptions.IgnoreCase).Match(source);

            if (h.Success)
                hours = Convert.ToInt32(h.Groups[1].ToString());

            if (d.Success)
                days = Convert.ToInt32(d.Groups[1].ToString());

            if (m.Success)
                minutes = Convert.ToInt32(m.Groups[1].ToString());

            source = source.Replace(hours + "h", string.Empty);
            source = source.Replace(days + "d", string.Empty);
            source = source.Replace(minutes + "m", string.Empty);

            if (!string.IsNullOrEmpty(source) || !(h.Success || d.Success || m.Success))
            {
                timeSpan = new TimeSpan();
                return false;
            }

            timeSpan = new TimeSpan(days, hours, minutes, 0);
            return true;
        }

        private static string TimeSpanToString(TimeSpan timeSpan)
        {
            var result = new StringBuilder();
            if (timeSpan.Days > 0)
                result.Append($"{timeSpan.Days}d");

            if (timeSpan.Hours > 0)
                result.Append($"{timeSpan.Hours}h");

            if (timeSpan.Minutes > 0)
                result.Append($"{timeSpan.Minutes}m");

            return result.ToString();
        }

        private static string GetConfigPath(string pluginName)
        {
            return $"{Interface.Oxide.ConfigDirectory}{Path.DirectorySeparatorChar}{pluginName}.json";
        }
        #endregion
    }
}