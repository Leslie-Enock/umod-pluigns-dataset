using System;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Collections.Generic;


namespace Oxide.Plugins
{
    [Info("Tc Options", "Sapnu Puas", "1.0.4")]
    [Description("Add a code lock and loot to tc when placed")]
    public class TcOptions : RustPlugin
    {
        private const string PermissionUse = "tcoptions.use";

        #region[Load/Unload]
        private void Init()
        {
            permission.RegisterPermission(PermissionUse, this);
        }
        private void OnServerInitialized()
        {
            LoadLockData();
        }
       
        void OnNewSave(string filename)
        {
            LoadConfig();
            LocknetID = new List<NetworkableId>();
            Puts("Wiping Data Files!!");
        }

        private void Unload()
        {
            SaveLockData();
        }

        #endregion[Load/Unload]

        #region[Assets]
        private const string codelockprefab = "assets/prefabs/locks/keypad/lock.code.prefab";
        private const string Prefab_CodeLock_UnlockEffect = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";
        private const string Prefab_CodeLock_LockEffect = "assets/prefabs/locks/keypad/effects/lock.code.lock.prefab";
        #endregion[Assets]

        #region [Localisation]
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Blocked"] = "you cannot remove this code lock !!"

            }, this);
        }
        #endregion [Localisation]

        #region[Hooks]
        void OnEntitySpawned(BaseEntity ent)
        {
            var output = ent.GetComponent<StorageContainer>();
            ulong player = ent.OwnerID;
            if (ent == null || ent.OwnerID == 0 || ent.IsDestroyed) return;
            if (!permission.UserHasPermission(ent?.OwnerID.ToString(), PermissionUse)) return;
           
            if (config.Tc.ContainsKey(ent.ShortPrefabName) && ent.OwnerID != 0)
            {
                if (config.Tcsettings.AddLock)
                {
                    AddLock(ent, player);
                }
                if (config.Tcsettings.AddLoot)
                {
                    SpawnTcLoot(output);
                }
            }
        }
        void OnEntityKill(BaseLock baseLock)
        {
            if (LocknetID.Contains(baseLock.net.ID))
            {
                LocknetID.Remove(baseLock.net.ID);
                SaveLockData();
            }
        }
      
        object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (baseLock == null) return false;
            if (baseLock.OwnerID != player.userID)
            {
                return null;
            }
            if (baseLock.OwnerID == player.userID)
            {
                return true;
            }
            return null;
        }
        object CanUnlock(BasePlayer player, BaseLock baseLock)
        {
            if (baseLock == null) return false;
            if (baseLock.OwnerID != player.userID)
            {
                return null;
            }
            if (baseLock.OwnerID == player.userID)
            {
                Unlock(baseLock);
                return true;
            }

            return null;
        }
        object CanPickupLock(BasePlayer player, BaseLock baseLock)
        {
            if (LocknetID.Contains(baseLock.net.ID))
            {
                player.SendConsoleCommand("showtoast", 1, lang.GetMessage("Blocked", this, player.UserIDString));

                return true;
            }
            return null;

        }
        #endregion[Hooks]

        #region [Config]

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();

                if (config == null) LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            config = Configuration.DefaultConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Settings")]
            public _tcsettings Tcsettings = new _tcsettings();

            [JsonProperty("Loot for")]
            public Dictionary<string, List<Loot>> Tc;

            public class _tcsettings
            {
                [JsonProperty("Add Codelock ?")]
                public bool AddLock = true;

                [JsonProperty("Add loot to toolcupboard ?")]
                public bool AddLoot = true;

            }
            
            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    Tc = new Dictionary<string, List<Loot>>
                    {
                        ["cupboard.tool.deployed"] = new List<Loot>//tc
                        {
                            new Loot
                            {
                                ShortName = "wood",
                                Amount = 2000,
                            },
                            new Loot
                            {
                                ShortName = "stones",
                                Amount = 3000,
                            },
                            new Loot
                            {
                                ShortName = "metal.fragments",
                                Amount = 1000,
                            },
                            new Loot
                            {
                                ShortName = "metal.refined",
                                Amount = 100,
                            },
                        },
                    },
                };
            }
        }
        public class Loot
        {
            [JsonProperty("ShortName")]
            public string ShortName;

            [JsonProperty("Amount")]
            public int Amount;
        }

        #endregion[Config]

        #region[Methods]
        void AddLock(BaseEntity ent, ulong playerid)
        {
            CodeLock codeLock = GameManager.server.CreateEntity(codelockprefab) as CodeLock;
            codeLock.Spawn();
            codeLock.OwnerID = ent.OwnerID;
            codeLock.code = UnityEngine.Random.Range(1111, 9999).ToString();
            codeLock.SetParent(ent, ent.GetSlotAnchorName(BaseEntity.Slot.Lock));
            ent.SetSlot(BaseEntity.Slot.Lock, codeLock);
            codeLock.whitelistPlayers.Add(ent.OwnerID);
            codeLock.SetFlag(BaseEntity.Flags.Locked, true);
            codeLock.enableSaving = false;
            codeLock.SendNetworkUpdateImmediate(true);
            Effect.server.Run(Prefab_CodeLock_LockEffect, codeLock, 0, Vector3.zero, Vector3.forward);
            LocknetID.Add(codeLock.net.ID);
            SaveLockData();
        }
        private void Unlock(BaseLock baseLock)
        {
            baseLock.SetFlag(BaseEntity.Flags.Locked, false);
            Effect.server.Run(Prefab_CodeLock_UnlockEffect, baseLock, 0, Vector3.zero, Vector3.forward);
        }
        private void SpawnTcLoot(StorageContainer container)
        {
            List<Loot> items;
            if (!config.Tc.TryGetValue(container.ShortPrefabName, out items))
            {
                return;
            }
            ItemContainer itemcontainer = container.GetComponent<StorageContainer>().inventory;
            foreach (var value in items)
            {
                var amount = value.Amount;
                var shortName = value.ShortName;
                var item = ItemManager.CreateByName(shortName, amount);
                if (itemcontainer == null || item == null) continue;
                item.MoveToContainer(itemcontainer, -1, true, true);
                item.parent = itemcontainer;
                item.MarkDirty();
            }
        }
        #endregion[Methods]

        #region[Data]
        private void SaveLockData()
        {
            if (LocknetID != null)
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/LockIds", LocknetID);
          
        }
        private void LoadLockData()
        {
            if (Interface.Oxide.DataFileSystem.ExistsDatafile($"{Name}/LockIds"))
            {
                LocknetID = Interface.Oxide.DataFileSystem.ReadObject<List<NetworkableId>>($"{Name}/LockIds");
            }
            else
            {
                LocknetID = new List<NetworkableId>();
                Puts("creating a new Lock data file");
                SaveLockData();
            }
        }
        [JsonProperty("lock ids")]
        private List<NetworkableId> LocknetID = new List<NetworkableId>();

        #endregion[Data]

    }
}