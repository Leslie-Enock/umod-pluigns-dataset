using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Random = UnityEngine.Random;

/*
 * Changelog
 *
 * - Partial rewrite - Wrecks
 *
 * Version 2.0.0
 * - Rewrote Config.
 * - Removed usage of findobjectsoftype.
 * - Cache Trees on Load and any new ones after.
 * - Added a Timer to config.
 * - If you used the old version, Delete config and Load this version.
 * - Added Lang with Delivery Message.
 * - Added Ability to Give Custom Gifts to Override Vanilla Gifts.
 */

namespace Oxide.Plugins
{
    [Info("Christmas Tree Presents", "redBDGR / Wrecks", "2.0.0")]
    [Description("Spawns Christmas presents under Christmas trees")]
    public class ChristmasTreePresents : RustPlugin
    {
        #region Variables

        private const string Gift = "assets/prefabs/misc/xmas/giftbox/giftbox_loot.prefab";
        private readonly HashSet<ChristmasTree> _christmasTrees = new();
        private Timer _spawnTimer;

        #endregion

        #region Config

        public class Settings
        {
            [JsonProperty("How Often to Spawn Presents in Minutes")] public int SpawnTimer;
            [JsonProperty("Minimum number of presents per tree")] public int MinNumOfPresents;
            [JsonProperty("Maximum number of presents per tree")] public int MaxNumOfPresents;
            [JsonProperty("Tree needs all ornaments?")] public bool TreeNeedsAllOrnaments;
            [JsonProperty("Tree needs to be on foundation?")] public bool TreeNeedsToBeOnFoundation;
            [JsonProperty("Send Custom Gifts?(Clears Vanilla and Adds Your Own)")] public bool SendCustomGifts;
            [JsonProperty("Minimum number of Custom Gifts per Present")] public int MinNumOfCustomGifts;
            [JsonProperty("Maximum number of Custom Gifts per Present")] public int MaxNumOfCustomGifts;
        }

        public class Items
        {
            [JsonProperty("Shortname")] public string Shortname { get; set; }
            [JsonProperty("SkinID")] public ulong SkinId { get; set; }
            [JsonProperty("Probability (0-1)")] public float Probability { get; set; }
            [JsonProperty("Custom Name")] public string CustomName { get; set; }
            [JsonProperty("Minimum Amount")] public int MinimumAmount { get; set; }
            [JsonProperty("Maximum Amount")] public int MaximumAmount { get; set; }
        }

        private static Configuration _config;

        public class Configuration
        {
            [JsonProperty("Settings")] public Settings Settings;
            [JsonProperty("Items")] public List<Items> ItemsList;

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    Settings = new Settings()
                    {
                        SpawnTimer = 180,
                        MinNumOfPresents = 3,
                        MaxNumOfPresents = 4,
                        TreeNeedsAllOrnaments = true,
                        TreeNeedsToBeOnFoundation = true,
                        SendCustomGifts = false,
                        MinNumOfCustomGifts = 1,
                        MaxNumOfCustomGifts = 2
                    },
                    ItemsList = new List<Items>()
                    {
                        new()
                        {
                            Shortname = "snowball",
                            SkinId = 0,
                            Probability = 0.5f,
                            CustomName = "",
                            MinimumAmount = 1,
                            MaximumAmount = 1
                        },
                        new()
                        {
                            Shortname = "scrap",
                            SkinId = 0,
                            Probability = 0.5f,
                            CustomName = "",
                            MinimumAmount = 1,
                            MaximumAmount = 1
                        },
                        new()
                        {
                            Shortname = "blood",
                            SkinId = 0,
                            Probability = 0.5f,
                            CustomName = "",
                            MinimumAmount = 1,
                            MaximumAmount = 1
                        },
                        new()
                        {
                            Shortname = "bleach",
                            SkinId = 0,
                            Probability = 0.5f,
                            CustomName = "",
                            MinimumAmount = 1,
                            MaximumAmount = 1
                        },
                        new()
                        {
                            Shortname = "sticks",
                            SkinId = 0,
                            Probability = 0.5f,
                            CustomName = "",
                            MinimumAmount = 1,
                            MaximumAmount = 1
                        }
                    }
                };
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
            catch (Exception ex)
            {
                Debug.LogException(ex);
                PrintWarning("Creating new configuration file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => _config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(_config);

        #endregion

        #region Language

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages
            (new Dictionary<string, string>
            {
                ["Message"] = "Gifts are being Delivered, Be sure to Check under your <color=green>Christmas Tree</color>!"
            }, this);
        }

        #endregion

        #region Oxide Hooks

        private void OnServerInitialized()
        {
            CacheTrees();
            StartTimer();
        }

        private void Unload()
        {
            Clear();
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            if (entity == null)
            {
                return;
            }
            if (entity is ChristmasTree tree)
            {
                _christmasTrees.Add(tree);
            }
        }

        private void OnEntityKill(BaseEntity entity)
        {
            if (entity == null)
            {
                return;
            }
            if (entity is ChristmasTree tree)
            {
                _christmasTrees.Remove(tree);
            }
        }

        #endregion

        #region Helper Methods

        private void Clear()
        {
            _spawnTimer?.Destroy();
            _christmasTrees.Clear();
        }

        private void StartTimer()
        {
            _spawnTimer = timer.Every(_config.Settings.SpawnTimer * 60, TrySpawn);
        }

        private void CacheTrees()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is ChristmasTree tree)
                {
                    _christmasTrees.Add(tree);
                }
            }
        }

        private void TrySpawn()
        {
            SendMessage();
            foreach (var tree in _christmasTrees)
            {
                if (tree == null || tree.IsDestroyed)
                {
                    continue;
                }
                SpawnPresents(tree);
            }
        }

        private void SendMessage()
        {
            string message = lang.GetMessage("Message", this);
            Server.Broadcast(message);
        }

        private void SpawnPresents(ChristmasTree tree)
        {
            if (_config.Settings.TreeNeedsAllOrnaments)
            {
                if (!CheckOrnaments(tree))
                {
                    return;
                }
            }
            if (_config.Settings.TreeNeedsToBeOnFoundation)
            {
                if (!CheckBuilding(tree))
                {
                    return;
                }
            }
            for (int i = 0; i < Random.Range(_config.Settings.MinNumOfPresents, _config.Settings.MaxNumOfPresents + 1); i++)
            {
                CreatePresent(new Vector3(Random.Range(-1.2f, 1.2f), 0, Random.Range(-1.2f, 1.2f)) + tree.transform.localPosition, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
        }

        private void CreatePresent(Vector3 pos, Quaternion rot)
        {
            BaseEntity present = GameManager.server.CreateEntity(Gift, pos, rot);
            if (present == null)
            {
                return;
            }
            present.Spawn();
            if (!_config.Settings.SendCustomGifts) return;
            var inventory = present.GetComponent<StorageContainer>().inventory;
            AddCustomLoot(inventory);
        }

        private void AddCustomLoot(ItemContainer inventory)
        {
            inventory.Clear();
            var list = _config.ItemsList;
            if (list == null) return;
            var lootCount = Random.Range(_config.Settings.MinNumOfCustomGifts, _config.Settings.MaxNumOfCustomGifts + 1);
            var lootTable = list;
            for (var i = 0; i < lootCount;)
            {
                var entry = lootTable[Random.Range(0, lootTable.Count)];
                var chance = Random.Range(0f, 1f);
                if (chance > entry.Probability)
                {
                    continue;
                }
                var amount = Random.Range(entry.MinimumAmount, entry.MaximumAmount + 1);
                var item = ItemManager.CreateByName(entry.Shortname, amount, entry.SkinId);
                if (!string.IsNullOrEmpty(entry.CustomName))
                {
                    item.name = entry.CustomName;
                }
                inventory.GiveItem(item);
                i++;
            }
        }

        private static bool CheckOrnaments(ChristmasTree tree)
        {
            return tree.GetComponent<StorageContainer>().inventory.IsFull();
        }

        private static bool CheckBuilding(ChristmasTree tree)
        {
            DecayEntity decay = tree.GetComponent<DecayEntity>();
            if (decay == null)
            {
                return false;
            }
            return decay.GetBuilding() != null;
        }

        #endregion
    }
}