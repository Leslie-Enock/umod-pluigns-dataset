using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Wipe Info", "dFxPhoeniX", "1.2.8")]
    [Description("Adds the ablity to see wipe cycles")]
    public class WipeInfo : RustPlugin
    {
        private string LastWipe;
        private string NextWipe;

        Timer announceTimer;

        ////////////////////////////////////////////////////////////
        // Oxide Hooks
        ////////////////////////////////////////////////////////////

        private void Init()
        {
            InitConfig();
        }

        void OnServerInitialized()
        {
            LoadVariables();

            if (AnnounceOnTimer)
            {
                announceTimer = timer.Repeat((AnnounceTimer * 60) * 60, 0, ()=> BroadcastWipe()); 
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (AnnounceOnJoin)
            {
                cmdNextWipe(player, "", new string[0]);
            }
        }

        ////////////////////////////////////////////////////////////
        // General Methods
        ////////////////////////////////////////////////////////////

        private DateTime ParseTime(string time) => DateTime.ParseExact(time, DateFormat, CultureInfo.InvariantCulture);

        private string NextWipeDays(string WipeDate)
        {
            DateTime wipeDateTime;

            if (DateTime.TryParseExact(WipeDate, DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out wipeDateTime))
            {
                TimeSpan t = wipeDateTime.Subtract(DateTime.Today);
                if (wipeDateTime.Date == DateTime.Today)
                {
                    return "Today";
                }
                else
                {
                    return string.Format("{0:D2}D ({1:D2})", t.Days, WipeDate);
                }
            }

            return "NoDateFound";
        }

        private void LoadVariables()
        {
            DateTime dateTime = DateTime.Today;
            DateTime firstDayMonth = new DateTime(dateTime.Year, dateTime.Month, 1);
            DateTime firstDayNextMonth = firstDayMonth.AddMonths(1);
            DateTime lastDayMonth = firstDayMonth.AddMonths(1).AddDays(-1);
            DateTime lastDayNextMonth = firstDayMonth.AddMonths(2).AddDays(-1);

            List<DateTime> datesList = new List<DateTime>();

            for (DateTime day = firstDayMonth.Date; day.Date <= lastDayMonth.Date; day = day.AddDays(1))
            {
                datesList.Add(day);
            }

            for (DateTime day = firstDayNextMonth.Date; day.Date <= lastDayNextMonth.Date; day = day.AddDays(1))
            {
                datesList.Add(day);
            }

            var firstThursdays = datesList.Where(d => d.DayOfWeek == DayOfWeek.Thursday)
                                           .GroupBy(d => d.Month)
                                           .Select(e => e.First());

            DateTime? lastThursday = firstThursdays.FirstOrDefault();
            if (lastThursday != null)
            {
                LastWipe = lastThursday.Value.ToString(DateFormat);
            }
            else
            {
                LastWipe = "NoDateFound";
            }

            DateTime? nextThursday = firstThursdays.Skip(1).FirstOrDefault();
            if (nextThursday != null)
            {
                NextWipe = nextThursday.Value.ToString(DateFormat);
            }
            else
            {
                NextWipe = "NoDateFound";
            }
        }

        private void BroadcastWipe()
        {
            foreach (var p in BasePlayer.activePlayerList)
            {
                if (NextWipeDays(NextWipe) == "Today")
                {
                    SendReply(p, string.Format(msg("MapWipeToday", p.UserIDString), LastWipe, NextWipeDays(NextWipe)));
                }
                else
                {
                    SendReply(p, string.Format(msg("MapWipe", p.UserIDString), LastWipe, NextWipeDays(NextWipe)));
                }
            }                
        }

        private string msg(string key, string id = null, params object[] args)
        {
            string message = id == null ? RemoveFormatting(lang.GetMessage(key, this, id)) : lang.GetMessage(key, this, id);

            return args.Length > 0 ? string.Format(message, args) : message;
        }

        private string RemoveFormatting(string source)
        {
            return source.Contains(">") ? Regex.Replace(source, "<.*?>", string.Empty) : source;
        }

        ////////////////////////////////////////////////////////////
        // Commands
        ////////////////////////////////////////////////////////////

        [ChatCommand("wipe")]
        private void cmdNextWipe(BasePlayer player, string command, string[] args)
        {
            if (NextWipeDays(NextWipe) == "Today")
            {
                SendReply(player, string.Format(msg("MapWipeToday", player.UserIDString), LastWipe, NextWipeDays(NextWipe)));
            }
            else
            {
                SendReply(player, string.Format(msg("MapWipe", player.UserIDString), LastWipe, NextWipeDays(NextWipe)));
            }                
        }

        [ConsoleCommand("wipe")]
        private void cmdGetWipe(ConsoleSystem.Arg arg)
        {
            if (NextWipeDays(NextWipe) == "Today")
            {
                SendReply(arg, string.Format(msg("MapWipeToday"), LastWipe, NextWipeDays(NextWipe)));
            }
            else
            {
                SendReply(arg, string.Format(msg("MapWipe"), LastWipe, NextWipeDays(NextWipe)));
            }
        }

        ////////////////////////////////////////////////////////////
        // Configs
        ////////////////////////////////////////////////////////////

        private bool ConfigChanged;
        private string DateFormat;
        private bool AnnounceOnJoin;
        private bool AnnounceOnTimer;
        private int AnnounceTimer;

        protected override void LoadDefaultConfig() => PrintWarning("Generating default configuration file...");

        private void InitConfig()
        {
            DateFormat = GetConfig("MM/dd/yyyy", "Date format");
            AnnounceOnJoin = GetConfig(false, "Announce on join");
            AnnounceOnTimer = GetConfig(false, "Announce on timer");
            AnnounceTimer = GetConfig(3, "Announce timer");

            if (ConfigChanged)
            {
                PrintWarning("Updated configuration file with new/changed values.");
                SaveConfig();
            }
        }

        private T GetConfig<T>(T defaultVal, params string[] path)
        {
            var data = Config.Get(path);
            if (data != null)
            {
                return Config.ConvertValue<T>(data);
            }

            Config.Set(path.Concat(new object[] { defaultVal }).ToArray());
            ConfigChanged = true;
            return defaultVal;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {            
                {"MapWipe", "Last Map Wipe: <color=#ffae1a>{0}</color>\nTime Until Next Map Wipe: <color=#ffae1a>{1}</color>" },
                {"MapWipeToday", "Last Map Wipe: <color=#ffae1a>{0}</color>\nTime Until Next Map Wipe: <color=#ffae1a>today (19:00 UTC)</color>" }
            }, this);
            lang.RegisterMessages(new Dictionary<string, string>
            {            
                {"MapWipe", "Ultimul Wipe de Mapă: <color=#ffae1a>{0}</color>\nTimpul până la urmâtorul Wipe de Mapă: <color=#ffae1a>{1}</color>" },
                {"TMapWipeoday", "Ultimul Wipe de Mapă: <color=#ffae1a>{0}</color>\nTimpul până la urmâtorul Wipe de Mapă: <color=#ffae1a>astăzi (19:00 UTC)</color>" }
            }, this, "ro");
        }
    }
}