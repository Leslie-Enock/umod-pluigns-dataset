using System;
using System.Linq;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Wipe Dates", "Freakyy", "1.0.3")]
    [Description("Shows the last and next wipe dates on command.")]
    class WipeDates : RustPlugin
    {
        #region variables
        public DateTime Last_Config_Call;

        public string Last_Wipe;
        public string Next_Wipe;

        public List<string> All_Wipe_Dates = new List<string>();

        private Settings _Settings;

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["WipeCommandMessage"] = "{Last_Wipe_Message} {Last_Wipe_Date} {Server_Timezone}\n{Next_Wipe_Message} {Next_Wipe_Date} {Server_Timezone}",
            }, this);
        }
        #endregion

        #region hooks

        void Init()
        {
            Last_Config_Call = DateTime.Now;
            save_config_settings();
            calculate_wipe_dates();
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            if(_Settings.Send_Message_On_User_Wake_Up)
                send_player_wipe_message(player);
        }

        #endregion

        #region commands
        [ChatCommand("wipe")]
        void wipe(BasePlayer player)
        {
            send_player_wipe_message(player);
        }
        #endregion

        #region functions
        private void send_player_wipe_message(BasePlayer player)
        {
            //prevents load Config spamming
            if (DateTime.Now > Last_Config_Call.AddMinutes(5))
            {
                save_config_settings();
                calculate_wipe_dates();
            }

            SendReply(player,
                lang.GetMessage("WipeCommandMessage", this, player.UserIDString)
                .Replace("{Last_Wipe_Date}", Last_Wipe)
                .Replace("{Next_Wipe_Date}", Next_Wipe)
                .Replace("{Last_Wipe_Message}", _Settings.Last_Wipe_Message)
                .Replace("{Next_Wipe_Message}", _Settings.Next_Wipe_Message)
                .Replace("{Server_Timezone}", _Settings.Server_Timezone)
            );
        }

        private void calculate_wipe_dates()
        {
            if(All_Wipe_Dates.Count == 0)
            {
                PrintError("Missing entries in config file. (All_Wipe_Dates)");
                return;
            }

            List<string> Past_Dates = All_Wipe_Dates.Where(x => Convert.ToDateTime(x) < DateTime.Now).OrderByDescending(x => x).ToList();

            List<string> Future_Dates = All_Wipe_Dates.Where(x => Convert.ToDateTime(x) > DateTime.Now).ToList();

            if(Past_Dates != null)
            {
                Last_Wipe = Convert.ToDateTime(Past_Dates.First()).ToString(_Settings.Date_Format);
            }

            if (Future_Dates != null)
            {
                Future_Dates.Sort();
                Next_Wipe = Convert.ToDateTime(Future_Dates.First()).ToString(_Settings.Date_Format);
            }
        }

        private void save_config_settings()
        {
            _Settings = Config.ReadObject<Settings>();

            Last_Config_Call = DateTime.Now;
            All_Wipe_Dates = _Settings.All_Wipe_Dates;
        }
        #endregion

        #region config
        private class Settings
        {
            public string Last_Wipe_Message { get; set; }
            public string Next_Wipe_Message { get; set; }
            public string Date_Format { get; set; }

            public List<string> All_Wipe_Dates;

            public bool Send_Message_On_User_Wake_Up { get; set; }

            public string Server_Timezone { get; set; }
        }

        private Settings GetDefaultSettings()
        {
            return new Settings
            {
                Last_Wipe_Message = "Last wipe was:",
                Next_Wipe_Message = "Next wipe will happen:",
                Date_Format = "MM/dd/yyyy HH:mm",
                All_Wipe_Dates = new List<string>() {
                    "02/09/2015 15:00:00",
                    "02/09/2099 15:00:00"
                },
                Send_Message_On_User_Wake_Up = false,
                Server_Timezone = "CET"
            };
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Attempting to create default config...");
            Config.WriteObject(GetDefaultSettings(), true);
            Config.Save();
        }
        #endregion
    }

}
