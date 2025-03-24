using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Oxide.Core;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Wake Info", "Mevent", "1.2.0")]
    [Description("Gives a note with information about the server after connecting")]
    public class WakeInfo : RustPlugin
    {
        #region Config

        private static Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Notes", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<NoteConf> Notes = new List<NoteConf>
            {
                new NoteConf
                {
                    Title = "New Player Info",
                    Enabled = true,
                    Permission = string.Empty,
                    Text = new List<string>
                    {
                        "Hello {name}!",
                        "Now online: {online}"
                    }
                },
                new NoteConf
                {
                    Title = "New Player Info 2",
                    Enabled = true,
                    Permission = string.Empty,
                    Text = new List<string>
                    {
                        "Last Wipe: {lastwipe}!",
                        "Max online: {maxonline}"
                    }
                }
            };

            [JsonProperty(PropertyName = "Only Introduce?")]
            public bool Introduce = true;

            [JsonProperty(PropertyName = "Note Item")]
            public ItemConf Item = new ItemConf
            {
                ShortName = "note",
                Skin = 0
            };
        }

        private class NoteConf
        {
            [JsonProperty(PropertyName = "Title")] public string Title;

            [JsonProperty(PropertyName = "Enabled?")]
            public bool Enabled;

            [JsonProperty(PropertyName = "Permission (ex: wakeinfo.vip)")]
            public string Permission;

            [JsonProperty(PropertyName = "Text", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<string> Text = new List<string>();

            public void Get(BasePlayer player)
            {
                var item = _config.Item.ToItem(Title);
                if (item == null) return;

                var convertText = ConvertText(player);
                if (!string.IsNullOrEmpty(convertText))
                    item.text = convertText;

                item.MarkDirty();
                GiveItem(player, item);
            }

            private void GiveItem(BasePlayer player, Item item)
            {
                if (!item.MoveToContainer(player.inventory.containerBelt))
                    player.GiveItem(item, BaseEntity.GiveItemReason.PickedUp);
            }

            private string ConvertText(BasePlayer player)
            {
                return string.Join("\n", Text)
                    .Replace("{lastwipe}", SaveRestore.SaveCreatedTime.ToUniversalTime().ToShortDateString())
                    .Replace("{name}", player.displayName)
                    .Replace("{steamid}", player.UserIDString)
                    .Replace("{maxonline}", ConVar.Server.maxplayers.ToString())
                    .Replace("{online}", BasePlayer.activePlayerList.Count.ToString());
            }
        }

        private class ItemConf
        {
            [JsonProperty(PropertyName = "Short Name")]
            public string ShortName;

            [JsonProperty(PropertyName = "Skin")] public ulong Skin;

            public Item ToItem(string title)
            {
                var item = ItemManager.CreateByName(ShortName, 1, Skin);
                if (item == null)
                {
                    Debug.LogError($"Error creating item with shortName '{ShortName}'");
                    return null;
                }

                if (!string.IsNullOrEmpty(title))
                    item.name = title;

                return item;
            }
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

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        #endregion

        #region Data

        private PluginData _data;

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, _data);
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
            [JsonProperty(PropertyName = "Players", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ulong> Players = new List<ulong>();
        }

        #endregion

        #region Hooks

        private void Init()
        {
            LoadData();
        }

        private void Unload()
        {
            SaveData();

            _config = null;
        }

        private void OnNewSave()
        {
            _data.Players.Clear();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || _config.Introduce && _data.Players.Contains(player.userID)) return;

            _config.Notes
                .FindAll(note => note.Enabled && (string.IsNullOrEmpty(note.Permission) ||
                                                  permission.UserHasPermission(player.UserIDString, note.Permission)))
                .ForEach(note => note.Get(player));

            if (!_data.Players.Contains(player.userID))
                _data.Players.Add(player.userID);
        }

        #endregion
    }
}