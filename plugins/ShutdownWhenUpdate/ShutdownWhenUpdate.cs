using Newtonsoft.Json;
using Oxide.Core.Libraries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Shutdown When Update", "Sorrow", "0.1.3")]
    [Description("Shutdown the server when uMod update is available")]
    public class ShutdownWhenUpdate : CovalencePlugin
    {
        #region Fields

        private const string Permission = "shutdownwhenupdate.use";
        private string _apiGitHub;
        private int _countdownToShutdown;
        private float _intervalToCheckUpdate;
        private string _tokenGithub;
        private bool _restartPlanned;

        #endregion

        #region uMod Hooks

        private void Init()
        {
            permission.RegisterPermission(Permission, this);

            _apiGitHub = Convert.ToString(Config["API GitHub"]);
            _tokenGithub = Convert.ToString(Config["Token GitHub (not required)"]);
            _countdownToShutdown = Convert.ToInt32(Config["Countdown before shutdown (seconds)"]);
            _intervalToCheckUpdate = GetIntervalToCheckUpdate();

            GetLatestVersion();
        }

        private void Unload()
        {
            _restartPlanned = false;
        }

        private void OnServerShutdown()
        {
            SaveConfig();
        }

        #endregion

        #region Functions

        /// <summary>
        /// Broadcasts the restart.
        /// </summary>
        /// <param name="countdownCount">The countdown count.</param>
        /// <param name="firstCall">if set to <c>true</c> [first call].</param>
        private void BroadcastRestart(int countdownCount, bool firstCall = true)
        {
            if (!players.Connected.Any() || countdownCount == 0)
            {
                Shutdown();
                return;
            }

            if (firstCall)
            {
                Puts(lang.GetMessage("pluginReadyToBeUpdated", this), GetFormattedTime(countdownCount));
                SendInfoMessage("pluginReadyToBeUpdated", new object[] {GetFormattedTime(countdownCount)});
            }
            else if (IsDisplayable(countdownCount))
            {
                Puts(lang.GetMessage("restartMessage", this), GetFormattedTime(countdownCount));
                SendInfoMessage("restartMessage", new object[] {GetFormattedTime(countdownCount)},
                    countdownCount >= 190);
            }

            countdownCount--;
            if (_restartPlanned) timer.Once(1f, () => BroadcastRestart(countdownCount, false));
        }

        /// <summary>
        /// Determines whether the specified countdown count is displayable.
        /// </summary>
        /// <param name="countdownCount">The countdown count.</param>
        /// <returns>
        ///   <c>true</c> if the specified countdown count is displayable; otherwise, <c>false</c>.
        /// </returns>
        private static bool IsDisplayable(int countdownCount)
        {
            return countdownCount % 3600 == 0 ||
                   countdownCount < 3600 && countdownCount % 600 == 0 ||
                   countdownCount < 600 && countdownCount % 60 == 0 ||
                   countdownCount < 180 && countdownCount % 1 == 0;
        }

        /// <summary>
        /// Gets the formatted time.
        /// </summary>
        /// <param name="countdownCount">The countdown count.</param>
        /// <returns></returns>
        private static string GetFormattedTime(int countdownCount)
        {
            var timeSpan = TimeSpan.FromSeconds(countdownCount);

            if (timeSpan.TotalSeconds < 1) return null;

            if (Math.Floor(timeSpan.TotalMinutes) >= 60)
            {
                return $"{timeSpan.Hours}h {timeSpan.Minutes}m";
            }
            else if (Math.Floor(timeSpan.TotalSeconds) >= 60)
            {
                return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
            }
            else
            {
                return $"{timeSpan.Seconds}s";
            }
        }

        /// <summary>
        /// Gets the latest version.
        /// </summary>
        /// <param name="manual">if set to <c>true</c> [manual].</param>
        private void GetLatestVersion(bool manual = false)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>();
            var plainTextBytes = Encoding.UTF8.GetBytes(_tokenGithub);
            headers.Add("Authorization", "Basic " + Convert.ToBase64String(plainTextBytes));
            headers.Add("User-Agent", "ShutdownWhenUpdate");
            try
            {
                webrequest.Enqueue(_apiGitHub, null, (code, response) =>
                {
                    if (response == null || code != 200)
                    {
                        Puts("Error: {0} - Could not contact GitHub server", code);
                    }
                    else
                    {
                        var json = JsonConvert.DeserializeObject<WebResponse>(response);
                        if (Convert.ToString(Config["Plugin - Current Version"]) == "0.0.0")
                            Config["Plugin - Current Version"] = json.Name;
                        Config["Plugin - Latest Version"] = json.Name;
                        SaveConfig();
                    }
                }, this, RequestMethod.GET, headers);
            }
            catch
            {
                Puts("Error: Could not contact GitHub server");
            }

            CompareVersionsAndShutdown();
            if (!manual) timer.Once(_intervalToCheckUpdate, () => { GetLatestVersion(); });
        }

        /// <summary>
        /// Compares the versions and update.
        /// </summary>
        private void CompareVersionsAndShutdown()
        {
            var pluginCurrentVersion =
                Convert.ToInt32(Config["Plugin - Current Version"].ToString().Replace(".", string.Empty));
            var pluginLatestVersion =
                Convert.ToInt32(Config["Plugin - Latest Version"].ToString().Replace(".", string.Empty));
            if (pluginCurrentVersion >= pluginLatestVersion || _restartPlanned) return;
            _restartPlanned = true;
            BroadcastRestart(_countdownToShutdown);
        }

        /// <summary>
        /// Shutdowns this instance.
        /// </summary>
        private void Shutdown()
        {
            Config["Plugin - Current Version"] = Config["Plugin - Latest Version"];
            SaveConfig();
            server.Command("quit");
        }

        /// <summary>
        /// Gets the interval to check update.
        /// </summary>
        /// <returns></returns>
        private float GetIntervalToCheckUpdate()
        {
            int interval = Convert.ToInt32(Config["Interval to check update (seconds)"]);
            if (_tokenGithub == "" && interval < 120)
            {
                return Convert.ToSingle(120);
            }
            else
            {
                return Convert.ToSingle(interval);
            }
        }

        #endregion

        #region Commands        

        [Command("swu")]
        private void SwuCommand(IPlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.Id, Permission)) return;
            if (args.Length < 1)
            {
                if (!_restartPlanned) return;
                SendInfoMessage("restartAbort");
                _restartPlanned = false;
            }
            else
            {
                switch (args[0].ToLower())
                {
                    case "start":
                        if (_restartPlanned) break;
                        _restartPlanned = true;
                        int countdownToShutdown;
                        if (args.Length >= 2 && int.TryParse(args[1], out countdownToShutdown))
                        {
                            BroadcastRestart(countdownToShutdown);
                        }
                        else
                        {
                            BroadcastRestart(_countdownToShutdown);
                        }

                        break;
                    case "stop":
                        if (!_restartPlanned) break;
                        _restartPlanned = false;
                        timer.In(1f, () => { SendInfoMessage("restartAbort"); });
                        break;
                    default:
                        SendInfoMessage("helpOptionNotFound");
                        break;
                }
            }
        }

        #endregion

        #region Localization

        /// <summary>
        /// MSG Information
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="player">The player.</param>
        /// <param name="args">The arguments.</param>
        /// <param name="hideMsg">if set to <c>true</c> [hide MSG].</param>
        private void InfoMsg(string key, IPlayer player, object[] args = null, bool hideMsg = true)
        {
            var message = lang.GetMessage(key, this, player.Id);
            if (args != null)
            {
                message = string.Format(message, args);
            }
#if RUST
            player?.Command("gametip.showgametip", message);
            if (hideMsg) timer.In(10f, () => { player?.Command("gametip.hidegametip"); });
#else
            player.Message(message);
#endif
        }

        /// <summary>
        /// Sends the information message.
        /// </summary>
        /// <param name="message">The message.</param>
        /// <param name="args">The arguments.</param>
        /// <param name="hideMsg"></param>
        private void SendInfoMessage(string message, object[] args = null, bool hideMsg = true)
        {
            foreach (var player in players.Connected)
            {
                InfoMsg(message, player, args, hideMsg);
            }
        }

        /// <summary>
        /// Loads the default messages.
        /// </summary>
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["pluginReadyToBeUpdated"] = "Plugin is ready to be updated, the server will be restart in {0}...",
                ["restartMessage"] = "Server will be restart in {0}...",
                ["restartAbort"] = "The restart has been suspended!",
                ["helpOptionNotFound"] = "This option doesn't exist.",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["pluginReadyToBeUpdated"] =
                    "Le plugin est prêt à recevoir une mise à jour, le serveur va redémarrer dans {0}...",
                ["restartMessage"] = "Le serveur va redémarrer dans {0}...",
                ["restartAbort"] = "Le redémarrage à été suspendu !",
                ["helpOptionNotFound"] = "Cette option n'existe pas.",
            }, this, "fr");
        }

        #endregion

        #region Config

        /// <summary>
        /// Loads the default configuration.
        /// </summary>
        protected new void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config["API GitHub"] = "https://api.github.com/repositories/94599577/releases/latest";
            Config["Countdown before shutdown (seconds)"] = 300;
            Config["Interval to check update (seconds)"] = 300;
            Config["Token GitHub (not required)"] = "";
            Config["Plugin - Current Version"] = "0.0.0";
            Config["Plugin - Latest Version"] = "0.0.0";
        }

        #endregion


        #region Class

        /// <summary>
        /// WebResponse Class
        /// </summary>
        private class WebResponse
        {
            public string Name { get; set; }
        }

        #endregion
    }
}