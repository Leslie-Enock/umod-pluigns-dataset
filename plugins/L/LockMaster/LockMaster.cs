using UnityEngine;
using System;
using System.Collections.Generic;
using System.Globalization;
using Oxide.Core;
using Newtonsoft.Json;


namespace Oxide.Plugins
{
    [Info("Lock Master", "FastBurst", "1.1.8")]
    [Description("Lock all your storages and deployables")]
    class LockMaster : RustPlugin
    {
        #region Vars
        private static bool isPlayer(ulong id) => id > 76560000000000000L;
        private const string COMPOSTER_PREFAB = "assets/prefabs/deployable/composter/composter.prefab";
        private const string DROPBOX_PREFAB = "assets/prefabs/deployable/dropbox/dropbox.deployed.prefab";
        private const string VENDING_PREFAB = "assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab";
        private const string FURNACE_PREFAB = "assets/prefabs/deployable/furnace/furnace.prefab";
        private const string LARGE_FURNACE_PREFAB = "assets/prefabs/deployable/furnace.large/furnace.large.prefab";
        private const string REFINERY_PREFAB = "assets/prefabs/deployable/oil refinery/refinery_small_deployed.prefab";
        private const string BBQ_PREFAB = "assets/prefabs/deployable/bbq/bbq.deployed.prefab";
        private const string SMALL_PLANTER_PREFAB = "assets/prefabs/deployable/planters/planter.small.deployed.prefab";
        private const string LARGE_PLANTER_PREFAB = "assets/prefabs/deployable/planters/planter.large.deployed.prefab";
        private const string STATIC_REFINERY_PREFAB = "assets/bundled/prefabs/static/small_refinery_static.prefab";
        private const string STATIC_BBQ_PREFAB = "assets/bundled/prefabs/static/bbq.static.prefab";
        private const string HITCH_PREFAB = "assets/prefabs/deployable/hitch & trough/hitchtrough.deployed.prefab";
        private const string MIXINGTABLE_PREFAB = "assets/prefabs/deployable/mixingtable/mixingtable.deployed.prefab";
        public static LockMaster Instance;
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            Instance = this;

            permission.RegisterPermission(configData.GeneralSettings.permissionName, this);
            cmd.AddChatCommand(configData.GeneralSettings.commandOption, this, "CmdRefresh");
        }

        private void CmdRefresh(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, configData.GeneralSettings.permissionName))
            {
                Message(player, "Usage");
                return;
            }
            NextTick(() =>
            {
                if (configData.ConfigSettings.lockOvens)
                    foreach (var entity in UnityEngine.Object.FindObjectsOfType<BaseOven>())
                    {
                        if (entity == null) continue;
                        if (isPlayer(entity.OwnerID))
                        {
                            if (entity is BaseOven && (!entity.PrefabName.Contains("bbq.static", CompareOptions.IgnoreCase) || !entity.PrefabName.Contains("small_refinery_static", CompareOptions.IgnoreCase)))
                            {
                                var oven = entity as BaseOven;
                                oven.isLockable = true;
                                oven.SendNetworkUpdate();
                                oven.SendNetworkUpdateImmediate(true);
                            }
                        }
                    }
                else
                    foreach (var entity in UnityEngine.Object.FindObjectsOfType<BaseOven>())
                    {
                        if (entity == null) continue;
                        if (isPlayer(entity.OwnerID))
                        {
                            if (entity is BaseOven && (!entity.PrefabName.Contains("bbq.static", CompareOptions.IgnoreCase) || !entity.PrefabName.Contains("small_refinery_static", CompareOptions.IgnoreCase)))
                            {
                                var oven = entity as BaseOven;
                                oven.isLockable = false;
                                oven.SendNetworkUpdate();
                                oven.SendNetworkUpdateImmediate(true);
                            }
                        }
                    }

                if (configData.ConfigSettings.lockHitch)
                    foreach (var entity in UnityEngine.Object.FindObjectsOfType<HitchTrough>())
                    {
                        if (entity == null) continue;
                        if (isPlayer(entity.OwnerID))
                        {
                            var troughs = entity as HitchTrough;
                            troughs.isLockable = true;
                            troughs.SendNetworkUpdate();
                            troughs.SendNetworkUpdateImmediate(true);
                        }
                    }
                else
                    foreach (var entity in UnityEngine.Object.FindObjectsOfType<HitchTrough>())
                    {
                        if (entity == null) continue;
                        if (isPlayer(entity.OwnerID))
                        {
                            var troughs = entity as HitchTrough;
                            troughs.isLockable = false;
                            troughs.SendNetworkUpdate();
                            troughs.SendNetworkUpdateImmediate(true);
                        }
                    }


                if (configData.ConfigSettings.lockComposter)
                    foreach (var entity in UnityEngine.Object.FindObjectsOfType<Composter>())
                    {
                        if (entity == null) continue;
                        if (isPlayer(entity.OwnerID))
                        {
                            var composters = entity as Composter;
                            composters.isLockable = true;
                            composters.SendNetworkUpdate();
                            composters.SendNetworkUpdateImmediate(true);
                        }
                    }
                else
                    foreach (var entity in UnityEngine.Object.FindObjectsOfType<Composter>())
                    {
                        if (entity == null) continue;
                        if (isPlayer(entity.OwnerID))
                        {
                            var composters = entity as Composter;
                            composters.isLockable = false;
                            composters.SendNetworkUpdate();
                            composters.SendNetworkUpdateImmediate(true);
                        }
                    }

                if (configData.ConfigSettings.lockDropBox)
                    foreach (var entity in UnityEngine.Object.FindObjectsOfType<DropBox>())
                    {
                        if (entity == null) continue;
                        if (isPlayer(entity.OwnerID))
                        {
                            var dropboxes = entity as DropBox;
                            dropboxes.isLockable = true;
                            dropboxes.SendNetworkUpdate();
                            dropboxes.SendNetworkUpdateImmediate(true);
                        }
                    }
                else
                    foreach (var entity in UnityEngine.Object.FindObjectsOfType<DropBox>())
                    {
                        if (entity == null) continue;
                        if (isPlayer(entity.OwnerID))
                        {
                            var dropboxes = entity as DropBox;
                            dropboxes.isLockable = false;
                            dropboxes.SendNetworkUpdate();
                            dropboxes.SendNetworkUpdateImmediate(true);
                        }
                    }

                if (configData.ConfigSettings.lockVending)
                    foreach (var entity in UnityEngine.Object.FindObjectsOfType<VendingMachine>())
                    {
                        if (entity == null && entity.ShortPrefabName.Contains("vendingmachine.deployed") == true) continue;
                        if (isPlayer(entity.OwnerID))
                        {
                            var vendingBoxes = entity as VendingMachine;
                            vendingBoxes.isLockable = true;
                            vendingBoxes.SendNetworkUpdate();
                            vendingBoxes.SendNetworkUpdateImmediate(true);
                        }
                    }
                else
                    foreach (var entity in UnityEngine.Object.FindObjectsOfType<VendingMachine>())
                    {
                        if (entity == null && entity.ShortPrefabName.Contains("vendingmachine.deployed") == true) continue;
                        if (isPlayer(entity.OwnerID))
                        {
                            var vendingBoxes = entity as VendingMachine;
                            vendingBoxes.isLockable = false;
                            vendingBoxes.SendNetworkUpdate();
                            vendingBoxes.SendNetworkUpdateImmediate(true);
                        }
                    }

                if (configData.ConfigSettings.lockPlanter)
                    foreach (var entity in UnityEngine.Object.FindObjectsOfType<PlanterBox>())
                    {
                        if (entity == null) continue;
                        if (isPlayer(entity.OwnerID))
                        {
                            var planters = entity as PlanterBox;
                            planters.isLockable = true;
                            planters.SendNetworkUpdate();
                            planters.SendNetworkUpdateImmediate(true);
                        }
                    }
                else
                    foreach (var entity in UnityEngine.Object.FindObjectsOfType<PlanterBox>())
                    {
                        if (entity == null) continue;
                        if (isPlayer(entity.OwnerID))
                        {
                            var planters = entity as PlanterBox;
                            planters.isLockable = false;
                            planters.SendNetworkUpdate();
                            planters.SendNetworkUpdateImmediate(true);
                        }
                    }

                if (configData.ConfigSettings.lockMixing)
                    foreach (var entity in UnityEngine.Object.FindObjectsOfType<MixingTable>())
                    {
                        if (entity == null) continue;
                        if (isPlayer(entity.OwnerID))
                        {
                            var mixingtables = entity as MixingTable;
                            mixingtables.isLockable = true;
                            mixingtables.SendNetworkUpdate();
                            mixingtables.SendNetworkUpdateImmediate(true);
                        }
                    }
                else
                    foreach (var entity in UnityEngine.Object.FindObjectsOfType<MixingTable>())
                    {
                        if (entity == null) continue;
                        if (isPlayer(entity.OwnerID))
                        {
                            var mixingtables = entity as MixingTable;
                            mixingtables.isLockable = false;
                            mixingtables.SendNetworkUpdate();
                            mixingtables.SendNetworkUpdateImmediate(true);
                        }
                    }
                Message(player, "Success");
            });
        }

        private void OnServerInitialized()
        {
            if (configData.ConfigSettings.lockHitch)
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<HitchTrough>())
                {
                    if (entity == null) continue;
                    if (isPlayer(entity.OwnerID))
                    {
                        var troughs = entity as HitchTrough;
                        troughs.isLockable = true;
                        troughs.SendNetworkUpdate();
                        troughs.SendNetworkUpdateImmediate(true);
                    }
                }
            else
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<HitchTrough>())
                {
                    if (entity == null) continue;
                    if (isPlayer(entity.OwnerID))
                    {
                        var troughs = entity as HitchTrough;
                        troughs.isLockable = false;
                        troughs.SendNetworkUpdate();
                        troughs.SendNetworkUpdateImmediate(true);
                    }
                }


            if (configData.ConfigSettings.lockComposter)
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<Composter>())
                {
                    if (entity == null) continue;
                    if (isPlayer(entity.OwnerID))
                    {
                        var composters = entity as Composter;
                        composters.isLockable = true;
                        composters.SendNetworkUpdate();
                        composters.SendNetworkUpdateImmediate(true);
                    }
                }
            else
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<Composter>())
                {
                    if (entity == null) continue;
                    if (isPlayer(entity.OwnerID))
                    {
                        var composters = entity as Composter;
                        composters.isLockable = false;
                        composters.SendNetworkUpdate();
                        composters.SendNetworkUpdateImmediate(true);
                    }
                }

            if (configData.ConfigSettings.lockDropBox)
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<DropBox>())
                {
                    if (entity == null) continue;
                    if (isPlayer(entity.OwnerID))
                    {
                        var dropboxes = entity as DropBox;
                        dropboxes.isLockable = true;
                        dropboxes.SendNetworkUpdate();
                        dropboxes.SendNetworkUpdateImmediate(true);
                    }
                }
            else
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<DropBox>())
                {
                    if (entity == null) continue;
                    if (isPlayer(entity.OwnerID))
                    {
                        var dropboxes = entity as DropBox;
                        dropboxes.isLockable = false;
                        dropboxes.SendNetworkUpdate();
                        dropboxes.SendNetworkUpdateImmediate(true);
                    }
                }

            if (configData.ConfigSettings.lockPlanter)
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<PlanterBox>())
                {
                    if (entity == null) continue;
                    if (isPlayer(entity.OwnerID))
                    {
                        var planters = entity as PlanterBox;
                        planters.isLockable = true;
                        planters.SendNetworkUpdate();
                        planters.SendNetworkUpdateImmediate(true);
                    }
                }
            else
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<PlanterBox>())
                {
                    if (entity == null) continue;
                    if (isPlayer(entity.OwnerID))
                    {
                        var planters = entity as PlanterBox;
                        planters.isLockable = false;
                        planters.SendNetworkUpdate();
                        planters.SendNetworkUpdateImmediate(true);
                    }
                }

            if (configData.ConfigSettings.lockMixing)
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<MixingTable>())
                {
                    if (entity == null) continue;
                    if (isPlayer(entity.OwnerID))
                    {
                        var mixingtables = entity as MixingTable;
                        mixingtables.isLockable = true;
                        mixingtables.SendNetworkUpdate();
                        mixingtables.SendNetworkUpdateImmediate(true);
                    }
                }
            else
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<MixingTable>())
                {
                    if (entity == null) continue;
                    if (isPlayer(entity.OwnerID))
                    {
                        var mixingtables = entity as MixingTable;
                        mixingtables.isLockable = false;
                        mixingtables.SendNetworkUpdate();
                        mixingtables.SendNetworkUpdateImmediate(true);
                    }
                }

            if (configData.ConfigSettings.lockVending)
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<VendingMachine>())
                {
                    if (entity == null && entity.ShortPrefabName.Contains("vendingmachine.deployed") == true) continue;
                    if (isPlayer(entity.OwnerID))
                    {
                        var vendingBoxes = entity as VendingMachine;
                        vendingBoxes.isLockable = true;
                        vendingBoxes.SendNetworkUpdate();
                        vendingBoxes.SendNetworkUpdateImmediate(true);
                    }
                }
            else
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<VendingMachine>())
                {
                    if (entity == null && entity.ShortPrefabName.Contains("vendingmachine.deployed") == true) continue;
                    if (isPlayer(entity.OwnerID))
                    {
                        var vendingBoxes = entity as VendingMachine;
                        vendingBoxes.isLockable = false;
                        vendingBoxes.SendNetworkUpdate();
                        vendingBoxes.SendNetworkUpdateImmediate(true);
                    }
                }

            if (configData.ConfigSettings.lockOvens)
            {
                foreach (var entity in UnityEngine.Object.FindObjectsOfType<BaseOven>())
                {
                    if (entity == null) continue;
                    if (isPlayer(entity.OwnerID))
                    {
                        if (entity is BaseOven && (!entity.ShortPrefabName.Contains("bbq.static", CompareOptions.IgnoreCase) || !entity.ShortPrefabName.Contains("small_refinery_static", CompareOptions.IgnoreCase)))
                        {
                            var oven = entity as BaseOven;
                            oven.isLockable = true;
                            oven.SendNetworkUpdate();
                            oven.SendNetworkUpdateImmediate(true);
                        }
                    }

                }
            }

            foreach (var entity in UnityEngine.Object.FindObjectsOfType<StorageContainer>())
            {
                OnEntitySpawned(entity);
            }
        }

        private void OnEntitySpawned(BaseNetworkable entity)
        {
            // Lock Hitch & Trough
            if (configData.ConfigSettings.lockHitch)
            {
                if (entity is HitchTrough)
                {
                    var hitchLock = entity as HitchTrough;
                    hitchLock.isLockable = true;
                    hitchLock.SendNetworkUpdateImmediate(true);
                    return;
                }
                var lockhitch = (entity as BaseLock)?.GetParentEntity() as HitchTrough;
                if (lockhitch != null)
                {
                    switch (lockhitch.PrefabName)
                    {
                        case HITCH_PREFAB:
                            entity.transform.localPosition = new Vector3(0f, 0.4f, 0.3f);
                            entity.transform.localRotation = Quaternion.Euler(0, 90, 0);
                            break;
                    }
                }
            }

            // Lock Composters
            if (configData.ConfigSettings.lockComposter)
            {
                if (entity is Composter)
                {
                    var composterLock = entity as Composter;
                    composterLock.isLockable = true;
                    composterLock.SendNetworkUpdateImmediate(true);
                    return;
                }
                var lockcompster = (entity as BaseLock)?.GetParentEntity() as Composter;
                if (lockcompster != null)
                {
                    switch (lockcompster.PrefabName)
                    {
                        case COMPOSTER_PREFAB:
                            entity.transform.localPosition = new Vector3(0f, 1.3f, 0.6f);
                            entity.transform.localRotation = Quaternion.Euler(0, 90, 0);
                            break;
                    }
                }
            }

            // Lock Planters
            if (configData.ConfigSettings.lockPlanter)
            {
                if (entity is PlanterBox)
                {
                    var planterLock = entity as PlanterBox;
                    planterLock.isLockable = true;
                    planterLock.SendNetworkUpdateImmediate(true);
                    return;
                }
                var lockplanter = (entity as BaseLock)?.GetParentEntity() as PlanterBox;
                if (lockplanter != null)
                {
                    switch (lockplanter.PrefabName)
                    {
                        case LARGE_PLANTER_PREFAB:
                            entity.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                            entity.transform.localRotation = Quaternion.Euler(0, 0, 90);
                            break;
                        case SMALL_PLANTER_PREFAB:
                            entity.transform.localPosition = new Vector3(0f, 0.45f, 0f);
                            entity.transform.localRotation = Quaternion.Euler(0, 0, 90);
                            break;
                    }
                }
            }

            // Lock DropbBoxes
            if (configData.ConfigSettings.lockDropBox)
            {
                if (entity is DropBox)
                {
                    var dropboxLock = entity as DropBox;
                    dropboxLock.isLockable = true;
                    dropboxLock.SendNetworkUpdateImmediate(true);
                    return;
                }
                var lockboxes = (entity as BaseLock)?.GetParentEntity() as DropBox;
                if (lockboxes != null)
                {
                    switch (lockboxes.PrefabName)
                    {
                        case DROPBOX_PREFAB:
                            entity.transform.localPosition = new Vector3(0, 0, 0);
                            entity.transform.localRotation = Quaternion.Euler(0, 0, 0);
                            break;
                    }
                }
            }

            // Lock VendingMachines
            if (configData.ConfigSettings.lockVending)
            {
                if (entity is VendingMachine && entity.ShortPrefabName.Contains("vendingmachine.deployed", CompareOptions.IgnoreCase) == true)
                {
                    var vendingLock = entity as VendingMachine;
                    vendingLock.isLockable = true;
                    vendingLock.SendNetworkUpdateImmediate(true);
                    return;
                }
                var vendingBoxes = (entity as BaseLock)?.GetParentEntity() as VendingMachine;
                if (vendingBoxes != null)
                {
                    switch (vendingBoxes.PrefabName)
                    {
                        case VENDING_PREFAB:
                            entity.transform.localPosition = new Vector3(0, 0, 0);
                            entity.transform.localRotation = Quaternion.Euler(0, 0, 0);
                            break;
                    }
                }
            }

            // Lock Mixing Tables
            if (configData.ConfigSettings.lockMixing)
            {
                if (entity is MixingTable)
                {
                    var mixingLock = entity as MixingTable;
                    mixingLock.isLockable = true;
                    mixingLock.SendNetworkUpdateImmediate(true);
                    return;
                }
                var lockmixtables = (entity as BaseLock)?.GetParentEntity() as MixingTable;
                if (lockmixtables != null)
                {
                    switch (lockmixtables.PrefabName)
                    {
                        case MIXINGTABLE_PREFAB:
                            entity.transform.localPosition = new Vector3(0, 0.8f, 0.375f);
                            entity.transform.localRotation = Quaternion.Euler(0, 90, 0);
                            break;
                    }
                }
            }


            // Lock All Ovens
            // If A Static Refinery or BBQ, Place lock and remove immediately, and make entity not lockable
            if (configData.ConfigSettings.lockOvens)
            {
                if (entity is BaseOven && (!entity.ShortPrefabName.Contains("bbq.static", CompareOptions.IgnoreCase) || !entity.ShortPrefabName.Contains("small_refinery_static", CompareOptions.IgnoreCase)))
                {
                    var ovenLock = entity as BaseOven;
                    ovenLock.isLockable = true;
                    ovenLock.SendNetworkUpdate();
                    ovenLock.SendNetworkUpdateImmediate(true);
                    return;
                }
                var lockovens = (entity as BaseLock)?.GetParentEntity() as BaseOven;
                if (lockovens != null)
                {
                    switch (lockovens.PrefabName)
                    {
                        case FURNACE_PREFAB:
                            entity.transform.localPosition = new Vector3(-0.02f, 0.3f, 0.5f);
                            entity.transform.localRotation = Quaternion.Euler(0, 90, 0);
                            break;
                        case LARGE_FURNACE_PREFAB:
                            entity.transform.localPosition = new Vector3(0.65f, 1.25f, -0.65f);
                            entity.transform.localRotation = Quaternion.Euler(0, 45, 0);
                            break;
                        case REFINERY_PREFAB:
                            entity.transform.localPosition = new Vector3(-0.01f, 1.25f, -0.6f);
                            entity.transform.localRotation = Quaternion.Euler(0, 90, 0);
                            break;
                        case BBQ_PREFAB:
                            entity.transform.localPosition = new Vector3(0.3f, 0.75f, 0f);
                            entity.transform.localRotation = Quaternion.Euler(0, 0, 0);
                            break;
                        case STATIC_BBQ_PREFAB:
                            if (lockovens is BaseOven && lockovens.prefabID == 28449714 || (lockovens.ShortPrefabName.Contains("bbq.static", CompareOptions.IgnoreCase)))
                            {
                                lockovens.isLockable = false;
                                lockovens.SendNetworkUpdate();
                                lockovens.SendNetworkUpdateImmediate(true);

                                var codelock = lockovens.GetComponentInChildren<CodeLock>();
                                var woodlock = lockovens.GetComponentInChildren<KeyLock>();
                                if (codelock != null)
                                {
                                    Effect.server.Run(codelock.effectUnlocked.resourcePath, codelock, 0, Vector3.zero, Vector3.forward, null, false);
                                    codelock.SetFlag(BaseEntity.Flags.Locked, false);

                                    timer.Once(0.05f, delegate () {
                                        codelock.Kill();
                                    });
                                }
                                if (woodlock != null)
                                {
                                    woodlock.SetFlag(BaseEntity.Flags.Locked, false);

                                    timer.Once(0.05f, delegate () {
                                        woodlock.Kill();
                                    });
                                }
                            }

                            entity.transform.localPosition = new Vector3(0.3f, 0.75f, 0f);
                            entity.transform.localRotation = Quaternion.Euler(0, 0, 0);
                            break;
                        case STATIC_REFINERY_PREFAB:
                            if (lockovens is BaseOven && lockovens.prefabID == 919097516 || (lockovens.ShortPrefabName.Contains("small_refinery_static", CompareOptions.IgnoreCase)))
                            {
                                lockovens.isLockable = false;
                                lockovens.SendNetworkUpdate();
                                lockovens.SendNetworkUpdateImmediate(true);

                                var codelock = lockovens.GetComponentInChildren<CodeLock>();
                                var woodlock = lockovens.GetComponentInChildren<KeyLock>();
                                if (codelock != null)
                                {
                                    Effect.server.Run(codelock.effectUnlocked.resourcePath, codelock, 0, Vector3.zero, Vector3.forward, null, false);
                                    codelock.SetFlag(BaseEntity.Flags.Locked, false);

                                    timer.Once(0.05f, delegate () {
                                        codelock.Kill();
                                    });
                                }
                                if (woodlock != null)
                                {
                                    woodlock.SetFlag(BaseEntity.Flags.Locked, false);

                                    timer.Once(0.05f, delegate () {
                                        woodlock.Kill();
                                    });
                                }
                            }
                            entity.transform.localPosition = new Vector3(-0.01f, 1.25f, -0.6f);
                            entity.transform.localRotation = Quaternion.Euler(0, 90, 0);
                            break;
                    }
                }

            }
        }
        #endregion

        #region Config
        private static ConfigData configData;
        private class ConfigData
        {
            [JsonProperty(PropertyName = "General Options")]
            public GeneralOptions GeneralSettings { get; set; }
            [JsonProperty(PropertyName = "Configuration")]
            public ConfigOptions ConfigSettings { get; set; }

            public class GeneralOptions
            {
                [JsonProperty(PropertyName = "Command")]
                public string commandOption { get; set; }
                [JsonProperty(PropertyName = "Permission Name")]
                public string permissionName { get; set; }
            }

            public class ConfigOptions
            {
                [JsonProperty(PropertyName = "Enable Locks on Composters")]
                public bool lockComposter { get; set; }
                [JsonProperty(PropertyName = "Enable Locks on DropBoxes")]
                public bool lockDropBox { get; set; }
                [JsonProperty(PropertyName = "Enable Locks on Vending Machines")]
                public bool lockVending { get; set; }
                [JsonProperty(PropertyName = "Enable Locks on Ovens (Furnaces, BBQ Grills, Small Oil Refineries)")]
                public bool lockOvens { get; set; }
                [JsonProperty(PropertyName = "Enable Locks on Small & Large Planters")]
                public bool lockPlanter { get; set; }
                [JsonProperty(PropertyName = "Enable Locks on Hitch & Trough")]
                public bool lockHitch { get; set; }
                [JsonProperty(PropertyName = "Enable Locks on Mixing Table")]
                public bool lockMixing { get; set; }
            }

            public Oxide.Core.VersionNumber Version { get; set; }
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
                GeneralSettings = new ConfigData.GeneralOptions
                {
                    commandOption = "refreshall",
                    permissionName = "lockmaster.admin"
                },
                ConfigSettings = new ConfigData.ConfigOptions
                {
                    lockComposter = true,
                    lockDropBox = true,
                    lockVending = true,
                    lockOvens = true,
                    lockPlanter = true,
                    lockHitch = true,
                    lockMixing = true
                },
                Version = Version
            };
        }

        protected override void SaveConfig() => Config.WriteObject(configData, true);

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            ConfigData baseConfig = GetBaseConfig();

            if (configData.Version < new Core.VersionNumber(1, 1, 5))
                configData = baseConfig;

            if (configData.Version < new Core.VersionNumber(1, 1, 6))
                configData.ConfigSettings.lockHitch = true;

            if (configData.Version < new Core.VersionNumber(1, 1, 8))
                configData.ConfigSettings.lockMixing = true;

            configData.Version = Version;
            PrintWarning("Config update completed!");
        }

        T GetConfig<T>(string name, T defaultValue)
        {
            if (Config[name] == null) return defaultValue;
            return (T)Convert.ChangeType(Config[name], typeof(T));
        }
        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"Usage", "You do not have permission to use this command"},
                {"Success", "Refreshing is done"}
            }, this);
        }

        private string Lang(string key, string id = null, params object[] args) => string.Format(lang.GetMessage(key, this, id), args);

        private void Message(BasePlayer player, string key, params object[] args)
        {
            SendReply(player, Lang(key, player.UserIDString, args));
        }
        #endregion
    }
}
