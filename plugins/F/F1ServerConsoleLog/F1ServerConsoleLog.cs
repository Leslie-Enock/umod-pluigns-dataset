using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("F1 Server Console Log", "NooBlet", "0.1.7")]
    [Description("Logs Server Console to F1 Console")]
    public class F1ServerConsoleLog : CovalencePlugin
    {
        List<BasePlayer> playerWithPerm = new List<BasePlayer>();
        string perm = "F1ServerConsoleLog.log";

        #region Hooks
        private void OnServerInitialized()
        {
            permission.RegisterPermission(perm, this);           
            LoadConfiguration();           
        }

        private void Loaded()
        {
            UnityEngine.Application.logMessageReceived += ConsoleLog;
            playerWithPerm.Clear();
        }
        private void Unload()
        { 
            UnityEngine.Application.logMessageReceived -= ConsoleLog;
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (!playerWithPerm.Contains(player))
            {
                if (permission.UserHasPermission(player.UserIDString, perm))
                {
                    playerWithPerm.Add(player);
                }
            }
           
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (playerWithPerm.Contains(player))
            {
                playerWithPerm.Remove(player);
            }
        }
        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!playerWithPerm.Contains(player))
            {
                if (permission.UserHasPermission(player.UserIDString, perm))
                {
                    playerWithPerm.Add(player);
                }
            }
        }


        private void ConsoleLog(string condition, string stackTrace, LogType type)
        {
            if (!string.IsNullOrEmpty(condition))
            {
                if (exclude(condition)) { return; }
                sendtoF1(condition);
            }
        }
        #endregion

        #region Cached Variables

        private string TimeString = "HH:mm:ss";
        private List<object> excludestrings = new List<object> 
        {
            "Kinematic",
            "NullReferenceException: Object reference not set to an instance of an object",
            "Invalid NavAgent Position",
            "Invalid Position",
        };

        #endregion

        #region Config

        protected override void LoadDefaultConfig()
        {
            LogWarning("Creating a new configuration file");
        }
        private void LoadConfiguration()
        {
           
            CheckCfg<string>("DateTime string format output", ref TimeString);
            CheckCfg<List<object>>("Strings to exclude from console log", ref excludestrings);

            SaveConfig();
        }

        private void CheckCfg<T>(string Key, ref T var)
        {
            if (Config[Key] is T)
                var = (T)Config[Key];
            else
                Config[Key] = var;
        }



        #endregion config       

        #region Methods

        private string GetLang(string key, string id) => lang.GetMessage(key, this, id);

        void sendtoF1(string log)
        {
            foreach (var p in playerWithPerm)
            {
               if(p == null) { continue; }
                p.ConsoleMessage($"({DateTime.Now.ToString(TimeString)}) <size=16>{log}</size>");
            }
        }

        bool exclude(string msg)
        {
            foreach(var s in excludestrings)
            {
                if (msg.Contains(s.ToString()))
                {
                    return true;
                }
            }
            return false;
        }
        #endregion

        #region Commands

        [Command("console")]
        private void consoleCommand(IPlayer iplayer, string command, string[] args)
        {
            if (!permission.UserHasPermission(iplayer.Id,perm)) { iplayer.Reply("You dont have permission for this command!"); return; }
            BasePlayer player = iplayer.Object as BasePlayer;
            if(args.Length < 0) { player.ChatMessage(string.Format(GetLang("OnOROff", player.UserIDString)));return; }
            if (args.Length > 1) { player.ChatMessage(string.Format(GetLang("ToManyArgs", player.UserIDString))); return; }
            if(args[0] == "on")
            {
                if (!playerWithPerm.Contains(player))
                {
                    playerWithPerm.Add(player);
                    player.ChatMessage(string.Format(GetLang("ConsoleOn", player.UserIDString)));
                }
            }
            if(args[0] == "off")
            {
                if (playerWithPerm.Remove(player)) 
                {
                    player.ChatMessage(string.Format(GetLang("ConsoleOff", player.UserIDString)));
                }
            }

            if (args[0] == "test")
            {
                if (playerWithPerm.Contains(player))
                {
                    player.ChatMessage("Player in console recieve list");
                }
                else
                {
                    player.ChatMessage("Player not in console recieve list");
                }
            }
        }

        #endregion Commands

        #region Lang API

        private void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["OnOROff"] = "Please input Console State (on or off)",
                ["ToManyArgs"] = "To much Info Added , Please use /console on   or    /console off",
                ["ConsoleOn"] ="Console turned on!",
                ["ConsoleOff"] = "Console turned off!",

            }, this, "en");
        }

        #endregion Lang API
    }
}
