using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("LootLogs", "k1lly0u", "0.2.1"), Description("Log all items deposited and removed from storage, stash and oven containers")]
    class LootLogs : RustPlugin
    {
        #region Fields
        private readonly Hash<ItemId, LootData> m_TrackedItems = new Hash<ItemId, LootData>();
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnItemAddedToContainer));
            Unsubscribe(nameof(OnItemRemovedFromContainer));

            DeleteExpiredLogs();
        }

        private void OnServerInitialized()
        {
            Subscribe(nameof(OnEntityDeath));
            Subscribe(nameof(OnItemAddedToContainer));
            Subscribe(nameof(OnItemRemovedFromContainer));
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Error.NoEntity"] = "No valid entity found!",
                ["Message.Details"] = "The box you are looking at is of the type: {0} with the ID: {1}. You can find the log for this box in 'oxide/logs/LootLogs/{0}/{2}_{1}.txt'"
            }, this);
        }

        private void OnEntityDeath(StorageContainer entity, HitInfo hitInfo)
        {
            if (!entity || hitInfo == null)
                return;

            DeathLog(entity.GetType().Name, 
                entity.PrefabName, 
                entity.net.ID.Value.ToString(), 
                entity.transform.position.ToString(), 
                hitInfo.InitiatorPlayer ? hitInfo.InitiatorPlayer.displayName : string.Empty);            
        }

        private void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (item == null || !item.uid.IsValid)
                return;

            if (!container.entityOwner && !container.playerOwner)
                return;

            LootData lootData;
            if (!m_TrackedItems.TryGetValue(item.uid, out lootData))
                return;

            if (container.playerOwner && container.playerOwner.net != null)
                Log(container.playerOwner.displayName, $"{lootData.ItemAmount}x {lootData.ItemName}", lootData.Type, lootData.EntityID, lootData.EntityName, true);
            if (container.entityOwner && !(container.entityOwner is DroppedItemContainer) && container.entityOwner.net != null)
                Log(lootData.EntityName, $"{lootData.ItemAmount}x {lootData.ItemName}", container.entityOwner.GetType(), container.entityOwner.net.ID.Value, container.entityOwner.ShortPrefabName, false);

            m_TrackedItems.Remove(item.uid);
        }

        private void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (item == null || !item.uid.IsValid)
                return;

            if (!container.entityOwner && !container.playerOwner)
                return;
            
            if (!m_TrackedItems.ContainsKey(item.uid))
            {
                m_TrackedItems.Add(item.uid, new LootData
                {
                    EntityName = container.entityOwner ? container.entityOwner.ShortPrefabName : container.playerOwner.displayName,
                    EntityID = container.entityOwner? container.entityOwner.net.ID.Value : container.playerOwner.userID,
                    ItemAmount = item.amount,
                    ItemName = item.info.displayName.english,
                    Type = container.entityOwner ? container.entityOwner.GetType() : typeof(BasePlayer)
                });

                timer.Once(5, () =>
                {
                    if (m_TrackedItems.ContainsKey(item.uid))
                        m_TrackedItems.Remove(item.uid);
                });
            }
        }

        private struct LootData
        {
            public string EntityName;
            public ulong EntityID;
            public string ItemName;
            public int ItemAmount;
            public Type Type;            
        }
        #endregion

        #region Raycasts
        private readonly RaycastHit[] m_RaycastHits = new RaycastHit[128];
        
        private BaseEntity FindEntity(BasePlayer player)
        {
            int hits = Physics.RaycastNonAlloc(player.eyes.HeadRay(), m_RaycastHits, 5f);

            if (hits > 0)
            {
                for (int i = 0; i < hits; i++)
                {
                    RaycastHit raycastHit = m_RaycastHits[i];

                    BaseEntity baseEntity = raycastHit.GetEntity();
                    if (baseEntity is StorageContainer)
                        return baseEntity;
                }
            }
            
            return null;
        }
        #endregion
        
        #region Logging
        private static readonly object _logFileLock = new object();
        
        private static readonly string _directory = Path.Combine(Interface.Oxide.LogDirectory, "LootLogs");
        
        private readonly Hash<string, Hash<string, List<string>>> m_QueuedLogs = new Hash<string, Hash<string, List<string>>>();
        
        private const string DESTROYED_CONTAINERS = "DestroyedContainers";

        private void QueueLogEntry(string path, string fileName, string text)
        {
            if (!m_QueuedLogs.ContainsKey(path))
                m_QueuedLogs.Add(path, new Hash<string, List<string>>());

            if (!m_QueuedLogs[path].ContainsKey(fileName))
                m_QueuedLogs[path].Add(fileName, new List<string>());

            m_QueuedLogs[path][fileName].Add(text);
        }

        private void OnServerSave()
        {
            try
            {
                foreach (KeyValuePair<string, Hash<string, List<string>>> path in m_QueuedLogs)
                {
                    foreach (KeyValuePair<string, List<string>> fileName in path.Value)
                    {
                        foreach (string text in fileName.Value)
                            LogToFile(path.Key, fileName.Key, text);

                        fileName.Value.Clear();
                    }
                }
            }
            catch(Exception ex)
            {
                PrintError($"{ex.Message}\n{ex.StackTrace}");
            }
        }
        
        private void Log(string playername, string item, Type type, ulong id, string entityname, bool take)
        {
            string path = Path.Combine(type.Name, DateTime.Now.ToString("yyyy-MM-dd"));
            string fileName = $"{entityname}_{id}";

            QueueLogEntry(path, fileName, $"{playername} {(take ? "looted" : "deposited")} {item}");
        }

        private void DeathLog(string type, string entityname, string id, string position, string killer)
        {
            string path = Path.Combine(DESTROYED_CONTAINERS, DateTime.Now.ToString("yyyy-MM-dd"), type);
            string fileName = $"DeathLog_{DateTime.Now.ToString("yyyy-MM-dd")}";

            QueueLogEntry(path, fileName, $"Name:{entityname} | BoxID:{id} | Position:{position} | Killer: {killer} | LogFile: oxide/logs/LootLogs/{type}/{DateTime.Now.ToString("yyyy-MM-dd")}/{entityname}_{id}.txt");      
        }

        private void LogToFile(string path, string filename, string text)
        {
            path = Path.Combine(_directory, path);
            
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            
            filename = filename.ToLower() + ".txt";
            
            lock (_logFileLock)
            {
                using (FileStream fileStream = new FileStream(Path.Combine(path, Utility.CleanPath(filename)), FileMode.Append, FileAccess.Write, FileShare.Read))
                {
                    using (StreamWriter streamWriter = new StreamWriter(fileStream, Encoding.UTF8))
                        streamWriter.WriteLine(text);
                }
            }
        }

        private void DeleteExpiredLogs()
        {
            if (configData.DeleteAfterDays <= 0)
                return;
            
            if (!Directory.Exists(_directory))
                return;
            
            string[] directories = Directory.GetDirectories(_directory, "*", SearchOption.TopDirectoryOnly);

            foreach (string subDirectory in directories)
                DeleteExpiredLogFolders(subDirectory);
        }

        private void DeleteExpiredLogFolders(string subDirectory)
        {
            string[] dateDirectories = Directory.GetDirectories(subDirectory, "*", SearchOption.TopDirectoryOnly);

            for (int i = 0; i < dateDirectories.Length; i++)
            {
                string date = Path.GetFileNameWithoutExtension(dateDirectories[i]);

                try
                {
                    DateTime dt = DateTime.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                    if (DateTime.Now - dt > TimeSpan.FromDays(7))
                    {
                        Directory.Delete(dateDirectories[i], true);
                    }
                }
                catch(Exception ex){}
            }
        }

        [ConsoleCommand("testlog")]
        private void ConsoleCommandTestLog(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null)
                return;
            
            Log("string 1", "string 2", typeof(StorageContainer), 72223441141414, "string 5", false);
            DeathLog("strings", "name", "id", "position", "killer");
            OnServerSave();
        }
        #endregion

        #region Chat Commands
        [ChatCommand("findid")]
        private void cmdFindID(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            BaseEntity entity = FindEntity(player);
            if (entity is StorageContainer)
                FormatString(player, "Message.Details", entity.GetType(), entity.net.ID.Value, entity.ShortPrefabName);
            else TranslatedString(player, "Error.NoEntity");
        }
        #endregion

        #region Messaging
        private void TranslatedString(BasePlayer player, string key) => player.ChatMessage(lang.GetMessage(key, this, player.UserIDString));

        private void FormatString(BasePlayer player, string key, params object[] args) => player.ChatMessage(string.Format(lang.GetMessage(key, this, player.UserIDString), args));
        #endregion
        
        #region Config        
        private ConfigData configData;
        private class ConfigData
        {         
            [JsonProperty("Delete logs after X days (0 to disable)")]
            public int DeleteAfterDays { get; set; }
            
            public VersionNumber Version { get; set; }
        }        

        protected override void LoadConfig()
        {
            base.LoadConfig();
            configData = Config.ReadObject<ConfigData>();

            if (configData.Version < Version)
                UpdateConfigValues();

            Config.WriteObject(configData, true);
        }

        protected override void LoadDefaultConfig() => configData = GetBaseConfig();

        private ConfigData GetBaseConfig()
        {
            return new ConfigData
            {
                DeleteAfterDays = 0,
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            if (configData.Version < new VersionNumber(0, 2, 0))
                configData = GetBaseConfig();
            
            configData.Version = Version;
            PrintWarning("Config update completed!");
        }
       
        #endregion
    }
}
