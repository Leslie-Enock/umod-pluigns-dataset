using Facepunch;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("AntiItems", "Author redBDGR, Maintainer nivex", "1.0.15")]
    [Description("Remove the need for certain items in crafting and repairing")]
    class AntiItems : RustPlugin
    {
        private Dictionary<string, int> componentList = new();

        private string permissionName = "antiitems.use";

        private Configuration config;

        private void OnPlayerConnected(BasePlayer player) => timer.Once(5f, () => DoItems(player));

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (!IsInvalid(player) && permission.UserHasPermission(player.UserIDString, permissionName))
            {
                RemoveItems(player);
            }
        }

        private void OnPlayerRespawned(BasePlayer player) => NextTick(() => DoItems(player));

        private void Init()
        {
            permission.RegisterPermission(permissionName, this);
            if (config.settings.useActiveRefreshing && config.settings.refreshTime > 0)
            {
                timer.Repeat(config.settings.refreshTime, 0, () =>
                {
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        RefreshItems(player);
                    }
                });
            }
            VerifyShortnames();
        }

        private void OnGroupPermissionRevoked(string group, string perm)
        {
            if (perm != permissionName) return;
            var users = permission.GetUsersInGroup(group);
            foreach (var user in users)
            {
                var userid = user.Split('(')[0].Trim();
                var player = BasePlayer.FindAwakeOrSleeping(userid);
                if (IsInvalid(player)) continue;
                RemoveItems(player);
            }
        }

        private void OnUserPermissionRevoked(string userid, string perm)
        {
            if (perm != permissionName) return;
            var player = BasePlayer.FindAwakeOrSleeping(userid);
            if (IsInvalid(player)) return;
            RemoveItems(player);
        }

        private void OnEntityDeath(BasePlayer player, HitInfo info)
        {
            if (!IsInvalid(player) && permission.UserHasPermission(player.UserIDString, permissionName))
            {
                RemoveItems(player);
            }
        }

        private void OnItemCraftCancelled(ItemCraftTask task)
        {
            foreach (var entry in task.takenItems)
            {
                if (componentList.ContainsKey(entry.info.shortname))
                {
                    timer.Once(0.01f, () =>
                    {
                        if (entry != null)
                        {
                            entry.RemoveFromContainer();
                            entry.Remove();
                        }
                    });
                }
            }
        }

        private void RefreshItems(BasePlayer player)
        {
            if (IsInvalid(player) || !permission.UserHasPermission(player.UserIDString, permissionName) || player.IsDead()) return;
            for (var i = 0; i < componentList.Count; i++)
            {
                Item item = player.inventory.containerMain.GetSlot(24 + i);
                if (item == null) continue;
                item.RemoveFromContainer();
                item.Remove();
            }
            DoItems(player);
        }

        private void DoItems(BasePlayer player)
        {
            if (IsInvalid(player) || !permission.UserHasPermission(player.UserIDString, permissionName) || player.IsDead()) return;
            player.inventory.containerMain.capacity = 24 + componentList.Count;
            var compList = componentList.Select(key => key.Key).ToList();
            for (var i = 0; i < componentList.Count; i++)
            {
                var item = ItemManager.CreateByName(compList[i], componentList[compList[i]]);
                if (item == null)
                {
                    Puts($"{compList[i]} was not able to be created properly. Perhaps the name of it is wrong");
                    continue;
                }
                if (!item.MoveToContainer(player.inventory.containerMain, 24 + i, true, config.settings.ignoreStackLimit))
                {
                    item.Remove();
                }
            }
        }

        private void RemoveItems(BasePlayer player)
        {
            List<Item> foundComponents = Pool.GetList<Item>();
            foreach (var item in player.inventory.containerMain.itemList)
            {
                if (item != null && componentList.ContainsKey(item.info.shortname))
                {
                    foundComponents.Add(item);
                }
            }
            foreach (var key in foundComponents)
            {
                key.RemoveFromContainer();
                key.Remove(0.1f);
            }
            Pool.FreeList(ref foundComponents);
            ItemManager.DoRemoves();
        }

        private void VerifyShortnames()
        {
            foreach (var component in config.settings.componentList)
            {
                if (ItemManager.FindItemDefinition(component.Key) == null) Puts($"Error: '{component.Key}' is not a valid shortname");
                else componentList[component.Key] = component.Value;
            }
        }

        private bool IsInvalid(BasePlayer player) => !player || !player.userID.IsSteamId() || player.IsDestroyed || player.inventory == null || player.inventory.containerMain == null;

        #region Configuration

        public class Settings
        {
            [JsonProperty("Components", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<string, int> componentList = new()
            {
                { "propanetank", 1000 }, 
                { "gears", 1000 }, 
                { "metalpipe", 1000 },
                { "metalspring", 1000 }, 
                { "riflebody", 1000 }, 
                { "roadsigns", 1000 },
                { "rope", 1000 }, 
                { "semibody", 1000 }, 
                { "sewingkit", 1000 },
                { "smgbody", 1000 },
                { "tarp", 1000 }, 
                { "techparts", 1000 }, 
                { "sheetmetal", 1000 }
            };
            
            [JsonProperty("Use Active Item Refreshing")]
            public bool useActiveRefreshing = true;

            [JsonProperty("Refresh Time")]
            public float refreshTime = 600f;

            [JsonProperty("Ignore Stack Limit")]
            public bool ignoreStackLimit;
        }

        private class Configuration
        {
            [JsonProperty("Settings")]
            public Settings settings = new();

            private string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    Puts("Configuration appears to be outdated; updating and saving.");
                    SaveConfig();
                }
            }
            catch
            {
                Puts($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }

        }

        protected override void SaveConfig()
        {
            Puts($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

    }
}