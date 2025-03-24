using System.Collections.Generic;
using System.Linq;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("Instant H2O", "Lincoln", "1.1.2")]
    [Description("Instantly fills water catchers upon placement")]
    public class InstantH2O : RustPlugin
    {
        private HashSet<ulong> playerList = new HashSet<ulong>(); // This will be cleared on reload
        private HashSet<ulong> persistentPlayerList = new HashSet<ulong>(); // This will be saved and loaded

        private Dictionary<WaterCatcher, Timer> timerDict = new Dictionary<WaterCatcher, Timer>();

        private const string permUse = "InstantH2O.use";
        private const string dataFileName = "InstantH2O_Data";

        private class PluginData
        {
            public HashSet<ulong> PersistentPlayerList { get; set; } = new HashSet<ulong>();
        }

        #region Localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Enabled"] = "<color=#ffc34d>InstantH2O</color>: Instantly fill water catchers <color=#b0fa66>Enabled</color>.",
                ["Disabled"] = "<color=#ffc34d>InstantH2O</color>: Instantly fill water catchers <color=#ff6666>Disabled</color>.",
                ["noPerm"] = "<color=#ffc34d>InstantH2O</color>: You do not have permissions to use this.",
                ["Reload"] = "<color=#ffc34d>InstantH2O</color>: Instantly fill water catchers <color=#ff6666>disabled</color> due to a plugin reload.",
            }, this);
        }
        #endregion

        private void OnServerInitialized()
        {
            permission.RegisterPermission(permUse, this);
            LoadData();
            RestoreWaterCatchers();
        }

        private bool HasPermission(BasePlayer player) => permission.UserHasPermission(player.UserIDString, permUse);

        [ChatCommand("h2o")]
        private void H2OCMD(BasePlayer player, string command, string[] args)
        {
            if (player == null || !HasPermission(player))
            {
                player?.ChatMessage(lang.GetMessage("noPerm", this, player.UserIDString));
                return;
            }

            if (playerList.Contains(player.userID))
            {
                playerList.Remove(player.userID);
                persistentPlayerList.Remove(player.userID); // Update the persistent list as well
                player.ChatMessage(lang.GetMessage("Disabled", this, player.UserIDString));
            }
            else
            {
                playerList.Add(player.userID);
                persistentPlayerList.Add(player.userID); // Update the persistent list as well
                player.ChatMessage(lang.GetMessage("Enabled", this, player.UserIDString));
            }

            SaveData();
            UpdateWaterCatchers(player.userID);
        }

        private void UpdateWaterCatchers(ulong userId)
        {
            foreach (var entity in BaseNetworkable.serverEntities.OfType<WaterCatcher>().ToArray())
            {
                if (entity == null || entity.OwnerID == 0) continue;

                if (persistentPlayerList.Contains(entity.OwnerID))
                {
                    StartWaterCatcherTimer(entity);
                }
                else
                {
                    StopWaterCatcherTimer(entity);
                }
            }
        }
        private void StartWaterCatcherTimer(WaterCatcher entity)
        {
            entity.maxOutputFlow = 1000;

            if (timerDict.ContainsKey(entity))
            {
                return;
            }

            Timer newTimer = timer.Every(1.0f, () =>
            {
                if (entity == null || entity.inventory == null)
                {
                    StopWaterCatcherTimer(entity);
                    return;
                }

                // Check if inventory exists and has items
                if (entity.inventory.itemList != null && entity.inventory.itemList.Count > 0)
                {
                    var waterItem = entity.inventory.itemList[0];
                    int max = entity.inventory.maxStackSize;
                    int current = waterItem.amount;

                    if (current < max)
                    {
                        int delta = max - current;
                        entity.AddResource(delta);
                        entity.maxOutputFlow = 10000;
                        entity.SendNetworkUpdateImmediate();
                    }
                }
                else
                {
                    // Initialize water collection when inventory is empty
                    entity.AddResource(1);
                    entity.SendNetworkUpdateImmediate();
                }
            });

            timerDict.Add(entity, newTimer);
        }
        private void StopWaterCatcherTimer(WaterCatcher entity)
        {
            Timer existingTimer;
            if (timerDict.TryGetValue(entity, out existingTimer))
            {
                existingTimer.Destroy();
                timerDict.Remove(entity);
            }
        }

        private void OnEntitySpawned(WaterCatcher entity)
        {
            if (entity == null || entity.OwnerID == 0) return;

            if (persistentPlayerList.Contains(entity.OwnerID))
            {
                StartWaterCatcherTimer(entity);
            }
        }

        private void Unload()
        {
            playerList.Clear(); // Clear the current state list
            SaveData();
        }

        private void LoadData()
        {
            var storedData = Interface.Oxide.DataFileSystem.ReadObject<PluginData>(dataFileName);
            persistentPlayerList = storedData.PersistentPlayerList ?? new HashSet<ulong>();
        }

        private void SaveData()
        {
            var storedData = new PluginData { PersistentPlayerList = persistentPlayerList };
            Interface.Oxide.DataFileSystem.WriteObject(dataFileName, storedData);
        }

        private void RestoreWaterCatchers()
        {
            foreach (var entity in BaseNetworkable.serverEntities.OfType<WaterCatcher>().ToArray())
            {
                if (entity == null || entity.OwnerID == 0) continue;

                if (persistentPlayerList.Contains(entity.OwnerID))
                {
                    StartWaterCatcherTimer(entity);
                }
            }
        }
    }
}
