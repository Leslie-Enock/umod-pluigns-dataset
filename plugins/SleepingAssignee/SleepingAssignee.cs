using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Sleeping Assignee", "Ryz0r", "1.3.0")]
    [Description("Allows you to check whom a sleeping bag or bed is assigned to, and who it was deployed by.")]
    internal class SleepingAssignee : RustPlugin
    {
        private const string UsePerm = "sleepingassignee.use";
        private Configuration config;
        private PluginData _data;
        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
        
        private void OnNewSave()
        {
            _data = new PluginData();
            SaveData();
        }

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
            [JsonProperty(PropertyName = "Name History")]
            public Dictionary<ulong, List<string>> NameHistory = new Dictionary<ulong, List<string>>();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoTarget"] = "There is no valid object that you are looking at!",
                ["AssignedTo"] = "This sleeping bag/bed has been assigned to {0}, and was deployed by {1}.",
                ["NoPerm"] = "You lack the required permissions to use this command.",
                ["NotBag"] = "This object is not a sleeping bag or a bed.",
                ["Naughty"] = "The name you attempted to give this bag is blocked. Your attempt has been reported to the admins."
            }, this);
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("A new configuration file is being generated.");
            config = new Configuration();
        }


        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private void Init()
        {
            permission.RegisterPermission(UsePerm, this);
            LoadData();
            foreach (var c in config.CommandsToUse)
            {
                cmd.AddChatCommand(c, this, nameof(CheckBag));
            }
        }

        private void CheckBag(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, UsePerm))
            {
                player.ChatMessage(lang.GetMessage("NoPerm", this, player.UserIDString));
                return;
            }

            RaycastHit raycastHit;
            if (Physics.Raycast(player.eyes.HeadRay(), out raycastHit))
            {
                var target = raycastHit.GetEntity();

                if (!target)
                {
                    player.ChatMessage(lang.GetMessage("NoTarget", this, player.UserIDString));
                    return;
                }

                if (target is SleepingBag)
                {
                    var sleepingBag = target as SleepingBag;
                    player.ChatMessage(string.Format(lang.GetMessage("AssignedTo", this, player.UserIDString),
                        GetPlayerName(sleepingBag.deployerUserID), GetPlayerName(sleepingBag.OwnerID)));

                    if (!_data.NameHistory.ContainsKey(sleepingBag.net.ID.Value)) return;

                    player.ChatMessage("Bag History\n" + string.Join("\n", _data.NameHistory[sleepingBag.net.ID.Value]));
                }
                else
                {
                    player.ChatMessage(lang.GetMessage("NotBag", this, player.UserIDString));
                }
            }
            else
            {
                player.ChatMessage(lang.GetMessage("NoTarget", this, player.UserIDString));
            }
        }
        
        private object CanRenameBed(BasePlayer player, SleepingBag bed, string bedName)
        {
            if (_data.NameHistory.ContainsKey(bed.net.ID.Value))
            {
                _data.NameHistory[bed.net.ID.Value].Add($"{player.UserIDString}: {bedName}");
            }
            else
            {
                _data.NameHistory.Add(bed.net.ID.Value, new List<string> {$"{player.UserIDString}: {bedName}"}); 
            }

            if (config.NaughtyWords.Contains(bedName))
            {
                if (config.LogToConsole)
                {
                    Puts($"{player} ({player.UserIDString}) tried to name a bag: {bedName}.");
                }
                
                player.ChatMessage(lang.GetMessage("Naughty", this, player.UserIDString));
                return true;
            }

            SaveData();
            return null;
        }

        private string GetPlayerName(ulong playerID)
        {
            var player = covalence.Players.FindPlayerById(playerID.ToString());
            if (player != null)
                return player.Name;
            return playerID + " (Unknown Player)";
        }

        private class Configuration
        {
            [JsonProperty(PropertyName = "Command To Check", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<string> CommandsToUse = new List<string> {"bag", "bed", "assign", "check"};

            [JsonProperty(PropertyName = "Prevent Naughty Named Bags")]
            public bool PreventBags = true;
            
            [JsonProperty(PropertyName = "Log Naughty Bags Attempts to Console")]
            public bool LogToConsole = true;
            
            [JsonProperty(PropertyName = "Naughty Word Filter", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public readonly List<string> NaughtyWords = new List<string> {"word1", "word2", "etc.."};
        }
    }
}