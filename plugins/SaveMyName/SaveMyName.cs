using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Save my Name", "MikeLitoris", "0.0.5")]
    [Description("Allows saving of your name for online and offline protection")]
    class SaveMyName : RustPlugin
    {
        private ConfigData configData;
        private const string savemyname = "SaveMyName.use";
        private const string savemynameadmin = "SaveMyName.admin";

        class ConfigData
        {
            [JsonProperty(PropertyName = "Admins can bypass the name-restrictions")]
            public bool IgnoreAdmins { get; set; }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["blockedname"] = "The name youre trying to connect with has been blocked, please switch and reconnect - Or visit the Discord if you feel this is unjust",
                ["clearedname"] = " has been removed from the saved names",
                ["nofoundmame"] = " No saved name found, use /savemyname to save current name",
                ["savedname"] = " Has been saved!",


            }, this);
        }

        private bool LoadConfigVariables()
        {
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                return false;
            }
            SaveConfig(configData);
            return true;
        }
        void Init()
        {

            permission.RegisterPermission(savemyname, this);
            permission.RegisterPermission(savemynameadmin, this);
            if (!LoadConfigVariables())
            {
                Puts("Config File issue detected");
                LoadDefaultConfig();
                return;
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new config");
            configData = new ConfigData { IgnoreAdmins = true };
            SaveConfig(configData);
        }

        void SaveConfig(ConfigData config)
        {
            Config.WriteObject(config, true);
        }

        class SavedNames
        {
            public ulong SteamID { get; set; }
            public string Name { get; set; }
            public DateTime Timestamp { get; set; }
        }

        StoredData storedData;

        class StoredData
        {
          public List<SavedNames> savedNames  = new List<SavedNames>();
        }
        
        void Loaded()
        {
            storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>("SaveMyName");
            Interface.Oxide.DataFileSystem.WriteObject("SaveMyName", storedData);
        }

        void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("SaveMyName", storedData);
        }

        void OnPlayerConnected(BasePlayer player)
        {
           if(player != null)
            { 
                foreach (var storedname in storedData.savedNames)
                {
                    if (configData.IgnoreAdmins)
                    {
                        if (player.displayName == storedname.Name && player.userID != storedname.SteamID && permission.UserHasPermission(storedname.SteamID.ToString(), savemyname) == true && player.IsAdmin == false)
                        {
                            player.Kick(lang.GetMessage("blockedname", this));
                        }
                    }
                    else
                    {
                        if (player.displayName == storedname.Name && player.userID != storedname.SteamID && permission.UserHasPermission(storedname.SteamID.ToString(), savemyname) == true)
                        {
                            player.Kick(lang.GetMessage("blockedname", this));
                        }
                    }
                }
            }
        }

        void OnUserNameUpdated(string id, string oldName, string newName)
        {
            BasePlayer player = BasePlayer.FindAwakeOrSleeping(id);
            if(player != null)
            {                           
                foreach (var storedname in storedData.savedNames)
                {
                    if (configData.IgnoreAdmins)
                    { 
                        if (newName == storedname.Name && ulong.Parse(id) != storedname.SteamID && permission.UserHasPermission(storedname.SteamID.ToString(), savemyname) == true && player.IsAdmin == false)// && player.IsAdmin == false)
                        {                        
                            player.Kick(lang.GetMessage("blockedname", this));
                        }
                    }
                    else
                    {
                        if (newName == storedname.Name && ulong.Parse(id) != storedname.SteamID && permission.UserHasPermission(storedname.SteamID.ToString(), savemyname) == true)
                        {
                            player.Kick(lang.GetMessage("blockedname", this));
                        }
                    }
                }
            }
        }

        [ChatCommand("clearmyname")]
        void ClearName(BasePlayer player, string command, string[] args)
        {
            if(player != null)
            {           
                if (permission.UserHasPermission(player.UserIDString, savemyname))
                {
                    try
                    {
                        SavedNames savedName = storedData.savedNames.Find(x => x.SteamID == player.userID);
                        storedData.savedNames.Remove(savedName);
                        SendReply(player, savedName.Name + lang.GetMessage("clearedname", this));
                        Puts(savedName.Name + lang.GetMessage("clearedname", this));
                        SaveData();
                    }
                    catch (System.Exception)
                    {
                        SendReply(player, lang.GetMessage("nofoundname", this));
                    }
                }
            }
        }

        [ChatCommand("savemyname")]
        void SaveName(BasePlayer player, string command, string[] args)
        {
            if (player != null)
            {               
                if (permission.UserHasPermission(player.UserIDString, savemyname))
                {
                    if (storedData.savedNames.Find(x => x.SteamID == player.userID) != null)
                    {
                        SavedNames savedName = storedData.savedNames.Find(x => x.SteamID == player.userID);
                        savedName.Name = player.displayName;
                        savedName.Timestamp = DateTime.Now;
                        SaveData();
                        SendReply(player, player.displayName + lang.GetMessage("savedname", this));
                        Puts(player.displayName + lang.GetMessage("savedname", this));
                    }
                    else
                    {
                        SavedNames saved = new SavedNames { SteamID = player.userID, Name = player.displayName, Timestamp = DateTime.Now };
                        storedData.savedNames.Add(saved);
                        SaveData();
                        Puts(player.displayName + lang.GetMessage("savedname", this));
                        SendReply(player, player.displayName + lang.GetMessage("savedname", this));
                    }
                }
            }
            
        }
        [ConsoleCommand("savednames")]
        void SavedNamesListCon()
        {
            string line = "\n";
            foreach (var sn in storedData.savedNames)
            {
                line += $"[{sn.SteamID}] {sn.Name}\n";
            }
            Puts(line);
        }

        [ChatCommand("savednames")]
        void SavedNamesList(BasePlayer player, string command, string[] args)
        {
            if (player != null)
            {
                if (permission.UserHasPermission(player.UserIDString, savemynameadmin))
                {
                    string line = "";
                    foreach (var sn in storedData.savedNames)
                    {
                        line += $"[{sn.SteamID}] {sn.Name}\n";
                    }
                    SendReply(player, line);

                }
            }
        }
    }
}