using System;
using Oxide.Core;
using UnityEngine;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Oxide.Plugins
{
    [Info("Rusty Rockets", "Strrobez", "1.1.0")]
    [Description("Allow players to use explosives during a certain time frame.")]
    public class RustyRockets : RustPlugin
    {
        private readonly int Date = (int)DateTime.UtcNow.DayOfWeek;
        private readonly int Hour = (int)DateTime.UtcNow.Hour;
        private readonly Dictionary<string, DateTime> Cooldown = new Dictionary<string, DateTime>();
        private readonly string UsePermission = "rustyrockets.use";

        #region Config

        private static Configuration _config;

        public class Configuration
        {
            [JsonProperty(PropertyName = "Rocket Cooldown")]
            public int RocketCooldown;
            
            [JsonProperty(PropertyName = "Day (To Allow Spawning)")]
            public string Day;

            [JsonProperty(PropertyName = "Hour (To Allow Spawning)")]
            public string Hour;

            [JsonProperty(PropertyName = "Items (To Give Players)")]
            public Item[] Items;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    RocketCooldown = 60,
                    Day = "Monday",
                    Hour = "12",
                    Items = new[] { new Item { ID = -742865266, Name = "Rockets", Amount = 20 } }
                };
            }
        }

        public class Item
        {
            [JsonProperty(PropertyName = "Item ID")]
            public int ID;

            [JsonProperty(PropertyName = "Item Name")]
            public string Name;
            
            [JsonProperty(PropertyName = "Item Amount")]
            public int Amount;

            public Item()
            {
            }

            public Item(int id, string name, int amount)
            {
                ID = id;
                Name = name;
                Amount = amount;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_config);
        #endregion

        #region Hooks

        private void Loaded()
        {
            if (!permission.PermissionExists(UsePermission))
                permission.RegisterPermission(UsePermission, this);
        }

        #endregion
        
        #region Command

        [ChatCommand("rustyrockets")]
        private void RustyRocketsCmd(BasePlayer player)
        {
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, UsePermission)) return;

            if (Date != (int)Enum.Parse(typeof(DayOfWeek), _config.Day) || Hour != int.Parse(_config.Hour))
            {
                player.ChatMessage(lang.GetMessage("Error", this, player.UserIDString));
                return;
            }
            
            if (Cooldown.ContainsKey(player.UserIDString))
            {
                player.ChatMessage(
                    string.Format(lang.GetMessage("Cooldown", this, player.UserIDString),
                    (Cooldown[player.UserIDString] - DateTime.UtcNow).Seconds)
                );
                return;
            }

            var items = _config.Items;

            foreach (Item item in items)
            {
                if (item.ID == 0 || item.Amount == 0) continue;

                var createdItem = ItemManager.CreateByItemID(item.ID, item.Amount);

                player.inventory.GiveItem(createdItem);

                player.ChatMessage(
                    string.Format(lang.GetMessage("Success", this, player.UserIDString), item.Amount,
                    item.Name)
                );
            }

            Cooldown.Add(player.UserIDString, DateTime.UtcNow.AddSeconds(_config.RocketCooldown));
            
            timer.Once(60, () => Cooldown.Remove(player.UserIDString));
        }
        
        #endregion
        
        #region Language
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Success"] = "You have been given {0} {1}.",
                ["Error"] = "You can't use this command right now.",
                ["Cooldown"] = "You can use this command again in {0} seconds."
            }, this);
        }
        #endregion
    }
}