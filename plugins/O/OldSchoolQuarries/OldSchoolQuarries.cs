using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("OldSchoolQuarries", "S0N_0F_BISCUIT", "1.0.8")]
    [Description("Makes resource output from quarries better")]
    class OldSchoolQuarries : RustPlugin
    {
        #region Variables
        private enum OreType { None, Sulfur, Metal, HighQuality };

        class CustomItem
        {
            public string shortname;
            public int amount;
            public double chance;
            public bool valid = true;
        }

        class ConfigData
        {
            [JsonProperty(PropertyName = "Display Detailed Analysis")]
            public bool Detailed_Analysis = false;
            [JsonProperty(PropertyName = "Custom Items")]
            public List<CustomItem> Custom_Items = new List<CustomItem>();
        }

        static class StandardOutput
        {
            public static DepositEntry stone = new DepositEntry()
            {
                type = OreType.None,
                amount = 100000,
                workNeeded = .3f
            };
            public static DepositEntry hqm = new DepositEntry()
            {
                type = OreType.HighQuality,
                amount = 100000,
                workNeeded = 35f
            };
            public static DepositEntry metal = new DepositEntry()
            {
                type = OreType.Metal,
                amount = 100000,
                workNeeded = 2f
            };
            public static DepositEntry sulfur = new DepositEntry()
            {
                type = OreType.Sulfur,
                amount = 100000,
                workNeeded = 3.25f
            };
        }

        class DepositEntry
        {
            public OreType type = OreType.None;
            public int amount;
            public float workNeeded;
        }

        class Deposit
        {
            public Origin origin = new Origin();
            public List<DepositEntry> entries = new List<DepositEntry>();
        }

        class Origin
        {
            public float x = 0;
            public float y = 0;
            public float z = 0;

            public Origin()
            {
                x = y = z = 0;
            }
            public Origin(Vector3 vector)
            {
                x = vector.x;
                y = vector.y;
                z = vector.z;
            }
        }

        class StoredData
        {
            public List<Deposit> changedDeposits = new List<Deposit>();
        }

        private static readonly System.Random rng = new System.Random();
        private ConfigData config = new ConfigData();
        private StoredData data = new StoredData();
        #endregion

        #region Localization
        private new void LoadDefaultMessages()
        {
            // English
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["NoCommandPermission"] = "You do not have permission to use this command!",
                ["NoResources"] = "No resources found.",
                ["AnalysisHeader"] = "Mineral Analysis:",
                ["Analysis"] = "Item: {0}, Amount: {1} pM",
                ["AnalysisDetailed"] = "Item: {0}, Amount: {1}, Work Needed: {2}, Return: {3} pM",
                ["AnalysisFooter"] = "----------------------------------",
                ["ClearData"] = "Plugin data cleared."
            }, this);
        }
        #endregion

        #region Permissions
        private static class Permissions
        {
            public static string probe = $"oldschoolquarries.probe";
            public static string customloot = $"oldschoolquarries.customloot";
            public static string standardoutput = $"oldschoolquarries.standardoutput";
        }
        #endregion

        #region Initialization
        //
        // Mod initialization
        //
        private void Init()
        {
            // Permissions
            permission.RegisterPermission(Permissions.probe, this);
            permission.RegisterPermission(Permissions.customloot, this);
            permission.RegisterPermission(Permissions.standardoutput, this);
            // Configuration
            try
            {
                LoadConfigData();
            }
            catch
            {
                LoadDefaultConfig();
                LoadConfigData();
            }
            // Data
            LoadData();
        }
        //
        // Edit the stored resource deposits
        //
        void OnServerInitialized()
        {
            ValidateConfig();

            ItemDefinition stones = ItemManager.itemList.Find(x => x.shortname == "stones");
            ItemDefinition sulfur = ItemManager.itemList.Find(x => x.shortname == "sulfur.ore");
            ItemDefinition metal = ItemManager.itemList.Find(x => x.shortname == "metal.ore");
            ItemDefinition hqm = ItemManager.itemList.Find(x => x.shortname == "hq.metal.ore");

            foreach (Deposit deposit in data.changedDeposits)
            {
                ResourceDepositManager.ResourceDeposit rd = ResourceDepositManager.GetOrCreate(GetVector3(deposit.origin));

                if (deposit.entries.Count == 4)
                {
                    rd._resources.Clear();
                    rd.Add(stones, 1, StandardOutput.stone.amount, StandardOutput.stone.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                    rd.Add(sulfur, 1, StandardOutput.sulfur.amount, StandardOutput.sulfur.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                    rd.Add(metal, 1, StandardOutput.metal.amount, StandardOutput.metal.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                    rd.Add(hqm, 1, StandardOutput.hqm.amount, StandardOutput.hqm.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                }
                else
                {
                    foreach (DepositEntry entry in deposit.entries)
                    {
                        switch (entry.type)
                        {
                            case OreType.Metal:
                                if (!rd._resources.Exists(r => r.type.shortname == "metal.ore"))
                                    rd.Add(metal, 1, entry.amount, entry.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                                else
                                {
                                    ResourceDepositManager.ResourceDeposit.ResourceDepositEntry resource = rd._resources.Find(r => r.type.shortname == "metal.ore");
                                    resource.amount = entry.amount;
                                    resource.workNeeded = entry.workNeeded;
                                }
                                break;
                            case OreType.Sulfur:
                                if (!rd._resources.Exists(r => r.type.shortname == "sulfur.ore"))
                                    rd.Add(sulfur, 1, entry.amount, entry.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                                else
                                {
                                    ResourceDepositManager.ResourceDeposit.ResourceDepositEntry resource = rd._resources.Find(r => r.type.shortname == "sulfur.ore");
                                    resource.amount = entry.amount;
                                    resource.workNeeded = entry.workNeeded;
                                }
                                break;
                            default:
                                break;
                        }
                    }
                }
            }
        }
        //
        // Clear the data on a new map
        //
        private void OnNewSave(string filename)
        {
            ClearData();
        }
        #endregion

        #region Config Handling
        //
        // Load config file
        //
        protected override void LoadDefaultConfig()
        {
            Config.Clear();
            var config = new ConfigData();
            // Create example item
            config.Custom_Items.Add(new CustomItem() { shortname = "candycane", amount = 1, chance = 0 });
            Config.WriteObject(config, true);
        }
        //
        // Load the config values to the config class
        //
        private void LoadConfigData()
        {
            config = Config.ReadObject<ConfigData>();
        }
        //
        // Validate the config file
        //
        private void ValidateConfig()
        {
            bool issuesFound = false;
            List<ItemDefinition> itemDefinitions = ItemManager.itemList;
            foreach (CustomItem item in config.Custom_Items)
            {
                if (!itemDefinitions.Exists(x => x.shortname == item.shortname))
                {
                    Puts($"The shortname \"{item.shortname}\" is invalid!");
                    item.valid = false;
                    issuesFound = true;
                }
                else
                {
                    item.valid = true;
                }

                if (item.chance > 100)
                {
                    Puts($"Invalid chance for shortname: \"{item.shortname}\"");
                    item.chance = 100;
                    issuesFound = true;
                }
                else if (item.chance < 0)
                {
                    Puts($"Invalid chance for shortname: \"{item.shortname}\"");
                    item.chance = 0;
                    issuesFound = true;
                }

                if (item.amount < 0)
                {
                    Puts($"Invalid amount for shortname: \"{item.shortname}\"");
                    item.amount = 0;
                    issuesFound = true;
                }
            }
            if (issuesFound)
            {
                Puts("Issues found in configuration file!");
            }
            Config.WriteObject(config, true);
        }
        #endregion

        #region Data Handling
        //
        // Load plugin data
        //
        private void LoadData()
        {
            try
            {
                data = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Title);
            }
            catch
            {
                data = new StoredData();
                SaveData();
            }
        }
        //
        // Save PlayerData
        //
        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Title, data);
        }
        //
        // Clear StoredData
        //
        private void ClearData()
        {
            data = new StoredData();
            SaveData();
        }
        #endregion

        #region Functionality
        //
        // Update salt map when resource deposit is tapped for the first time
        //
        void OnEntitySpawned(BaseNetworkable entity)
        {
            if (entity is SurveyCharge)
            {
                OreType ore = OreType.None;

                ItemDefinition stones = ItemManager.itemList.Find(x => x.shortname == "stones");
                ItemDefinition sulfur = ItemManager.itemList.Find(x => x.shortname == "sulfur.ore");
                ItemDefinition metal = ItemManager.itemList.Find(x => x.shortname == "metal.ore");
                ItemDefinition hqm = ItemManager.itemList.Find(x => x.shortname == "hq.metal.ore");

                ResourceDepositManager.ResourceDeposit rd = ResourceDepositManager.GetOrCreate(entity.transform.position);
                Deposit deposit = new Deposit { origin = GetOrigin(rd.origin) };
                BasePlayer player = (entity as SurveyCharge).creatorEntity as BasePlayer;

                if (permission.UserHasPermission(player.UserIDString, Permissions.standardoutput))
                {
                    bool createDeposit = true;
                    // If deposit has been changed make sure it is correct
                    if (data.changedDeposits.Exists(d => GetVector3(d.origin) == rd.origin))
                    {
                        bool depositValid = true;
                        foreach (ResourceDepositManager.ResourceDeposit.ResourceDepositEntry resource in rd._resources)
                        {
                            switch (resource.type.shortname)
                            {
                                case "stones":
                                    if (!(StandardOutput.stone.amount == resource.amount && StandardOutput.stone.workNeeded == resource.workNeeded))
                                        depositValid = false;
                                    break;
                                case "sulfur.ore":
                                    if (!(StandardOutput.sulfur.amount == resource.amount && StandardOutput.sulfur.workNeeded == resource.workNeeded))
                                        depositValid = false;
                                    break;
                                case "metal.ore":
                                    if (!(StandardOutput.metal.amount == resource.amount && StandardOutput.metal.workNeeded == resource.workNeeded))
                                        depositValid = false;
                                    break;
                                case "hq.metal.ore":
                                    if (!(StandardOutput.hqm.amount == resource.amount && StandardOutput.hqm.workNeeded == resource.workNeeded))
                                        depositValid = false;
                                    break;
                                default:
                                    break;
                            }
                        }

                        if (depositValid)
                            createDeposit = false;
                        else
                            deposit = data.changedDeposits.Find(d => GetVector3(d.origin) == rd.origin);

                    }

                    if (createDeposit)
                    {
                        rd._resources.Clear();
                        deposit.entries.Clear();

                        rd.Add(stones, 1, StandardOutput.stone.amount, StandardOutput.stone.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                        deposit.entries.Add(new DepositEntry()
                        {
                            type = StandardOutput.stone.type,
                            amount = StandardOutput.stone.amount,
                            workNeeded = StandardOutput.stone.workNeeded
                        });
                        rd.Add(sulfur, 1, StandardOutput.sulfur.amount, StandardOutput.sulfur.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                        deposit.entries.Add(new DepositEntry()
                        {
                            type = StandardOutput.sulfur.type,
                            amount = StandardOutput.sulfur.amount,
                            workNeeded = StandardOutput.sulfur.workNeeded
                        });
                        rd.Add(metal, 1, StandardOutput.metal.amount, StandardOutput.metal.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                        deposit.entries.Add(new DepositEntry()
                        {
                            type = StandardOutput.metal.type,
                            amount = StandardOutput.metal.amount,
                            workNeeded = StandardOutput.metal.workNeeded
                        });
                        rd.Add(hqm, 1, StandardOutput.hqm.amount, StandardOutput.hqm.workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                        deposit.entries.Add(new DepositEntry()
                        {
                            type = StandardOutput.hqm.type,
                            amount = StandardOutput.hqm.amount,
                            workNeeded = StandardOutput.hqm.workNeeded
                        });

                        data.changedDeposits.Add(deposit);
                        SaveData();
                    }
                }

                if (data.changedDeposits.Exists(d => GetVector3(d.origin) == rd.origin))
                    return;

                ResourceDepositManager.ResourceDeposit.ResourceDepositEntry originalResource = null;

                int oreCount = 0;
                foreach (ResourceDepositManager.ResourceDeposit.ResourceDepositEntry resource in rd._resources)
                {
                    switch (resource.type.shortname)
                    {
                        case "sulfur.ore":
                            ore = OreType.Sulfur;
                            originalResource = resource;
                            oreCount++;
                            break;
                        case "metal.ore":
                            ore = OreType.Metal;
                            originalResource = resource;
                            oreCount++;
                            break;
                        case "hq.metal.ore":
                            ore = OreType.HighQuality;
                            originalResource = resource;
                            oreCount++;
                            break;
                        default:
                            break;
                    }
                }

                if (oreCount > 1)
                    return;



                if (originalResource == null && rd._resources.Count != 0)
                    originalResource = rd._resources.ToArray()[0];

                System.Random rng = new System.Random();
                float workNeeded = (float)(rng.Next(0, 2) + rng.NextDouble());
                int choice = rng.Next(1, 100);
                int amount = 0;
                switch (ore)
                {
                    case OreType.Sulfur:  // Give a chance at some amount of metal ore
                        if (workNeeded > 1f)
                        {
                            amount = rng.Next(10000, 100000);
                            rd.Add(metal, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                            deposit.entries.Add(new DepositEntry() { type = OreType.Metal, amount = amount, workNeeded = workNeeded });
                        }
                        break;
                    case OreType.Metal: // Give a chance at some amount of sulfur ore
                        if (workNeeded > 1.75f)
                        {
                            amount = rng.Next(10000, 100000);
                            rd.Add(sulfur, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                            deposit.entries.Add(new DepositEntry() { type = OreType.Sulfur, amount = amount, workNeeded = workNeeded });
                        }
                        break;
                    case OreType.HighQuality: // Give a chance at some amount of either metal, sulfur, or both ores
                        if (choice < 40) // Just sulfur
                        {
                            amount = rng.Next(10000, 100000);
                            workNeeded = (float)(rng.Next(3, 4) + rng.NextDouble());
                            rd.Add(sulfur, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                            deposit.entries.Add(new DepositEntry() { type = OreType.Sulfur, amount = amount, workNeeded = workNeeded });
                        }
                        else if (choice < 80) // Just metal
                        {
                            if (workNeeded < 1.75f)
                                workNeeded += (1.75f - workNeeded);
                            amount = rng.Next(10000, 100000);
                            rd.Add(metal, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                            deposit.entries.Add(new DepositEntry() { type = OreType.Metal, amount = amount, workNeeded = workNeeded });
                        }
                        else // Both sulfur and metal
                        {
                            if (workNeeded < 1.75f)
                                workNeeded += (1.75f - workNeeded);
                            amount = rng.Next(10000, 100000);
                            rd.Add(metal, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                            deposit.entries.Add(new DepositEntry() { type = OreType.Metal, amount = amount, workNeeded = workNeeded });
                            amount = rng.Next(10000, 100000);
                            workNeeded = (float)(rng.Next(3, 4) + rng.NextDouble());
                            rd.Add(sulfur, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                            deposit.entries.Add(new DepositEntry() { type = OreType.Sulfur, amount = amount, workNeeded = workNeeded });
                        }
                        break;
                    default: // Give a chance at some amount of either metal, sulfur, or both ores
                        if (oreCount == 1)
                            return;
                        if (choice < 40) // Just sulfur
                        {
                            if (workNeeded > 1.75f)
                                return;
                            amount = rng.Next(10000, 100000);
                            workNeeded = (float)(rng.Next(3, 4) + rng.NextDouble());
                            rd.Add(sulfur, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                            deposit.entries.Add(new DepositEntry() { type = OreType.Sulfur, amount = amount, workNeeded = workNeeded });
                        }
                        else if (choice < 80) // Just metal
                        {
                            if (workNeeded < 1.75f)
                                workNeeded += (1.75f - workNeeded);
                            amount = rng.Next(10000, 100000);
                            rd.Add(metal, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                            deposit.entries.Add(new DepositEntry() { type = OreType.Metal, amount = amount, workNeeded = workNeeded });
                        }
                        else // Both sulfur and metal
                        {
                            if (workNeeded < 1.75f)
                                workNeeded += (1.75f - workNeeded);
                            amount = rng.Next(10000, 100000);
                            rd.Add(metal, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                            deposit.entries.Add(new DepositEntry() { type = OreType.Metal, amount = amount, workNeeded = workNeeded });
                            amount = rng.Next(10000, 100000);
                            workNeeded = (float)(rng.Next(3, 4) + rng.NextDouble());
                            rd.Add(sulfur, 1, amount, workNeeded, ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM);
                            deposit.entries.Add(new DepositEntry() { type = OreType.Sulfur, amount = amount, workNeeded = workNeeded });
                        }
                        break;
                }

                data.changedDeposits.Add(deposit);
                SaveData();
            }
        }
        //
        // Add custom item to quarries on fuel consumed
        //
        void OnItemUse(Item item, int amountToUse)
        {
            try
            {
                if (BaseNetworkable.serverEntities.Find(item.parent.entityOwner.parentEntity.uid) is MiningQuarry)
                {
                    MiningQuarry quarry = BaseNetworkable.serverEntities.Find(item.parent.entityOwner.parentEntity.uid) as MiningQuarry;

                    if (quarry.canExtractLiquid)
                        return;

                    if (!permission.UserHasPermission(quarry.OwnerID.ToString(), Permissions.customloot))
                        return;

                    ItemContainer hopper = (quarry.hopperPrefab.instance as StorageContainer).inventory;

                    double value = (rng.Next(0, 100) + rng.NextDouble());
                    if (value > 100d)
                        value = 100d;
                    if (config.Custom_Items == null)
                        return;
                    foreach (CustomItem cItem in config.Custom_Items)
                    {
                        if (!cItem.valid || cItem.chance == 0 || cItem.amount == 0)
                            continue;
                        if (value <= cItem.chance)
                        {
                            try
                            {
                                hopper.AddItem(ItemManager.itemList.Find(x => x.shortname == cItem.shortname), cItem.amount);
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }
        #endregion

        #region Commands
        //
        // Perform a mineral analysis at players position
        //
        [ChatCommand("getdeposit")]
        void GetDeposit(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Permissions.probe))
            {
                PrintToChat(player, Lang("NoCommandPermission", player.UserIDString));
                return;
            }

            ResourceDepositManager.ResourceDeposit rd = ResourceDepositManager.GetOrCreate(player.transform.position);
            if (rd == null)
            {
                PrintToChat(player, Lang("NoResources", player.UserIDString));
                return;
            }

            PrintToChat(player, Lang("AnalysisHeader", player.UserIDString));
            float num1 = 10f;
            float num2 = 7.5f;
            List<int> fixIndex = new List<int>();
            int index = 0;
            foreach (ResourceDepositManager.ResourceDeposit.ResourceDepositEntry resource in rd._resources)
            {
                float num3 = (float)(60.0 / num1 * (num2 / (double)resource.workNeeded));
                if (float.IsInfinity(num3))
                    fixIndex.Add(index);
                if (config.Detailed_Analysis)
                    PrintToChat(player, Lang("AnalysisDetailed", player.UserIDString, resource.type.displayName.translated, resource.amount, resource.workNeeded, Math.Round(num3, 1)));
                else
                    PrintToChat(player, Lang("Analysis", player.UserIDString, resource.type.displayName.translated, Math.Round(num3, 1)));
                index++;
            }

            if (fixIndex.Count != 0)
            {
                foreach (int pos in fixIndex)
                    rd._resources.RemoveAt(pos);
            }

            PrintToChat(player, Lang("AnalysisFooter", player.UserIDString));
        }

        [ConsoleCommand("getdeposit")]
        void GetDepositConsole(ConsoleSystem.Arg arg)
        {
            GetDeposit(arg.Player(), "getdeposit", null);
        }
        //
        // Reload plugin config from within the game
        //
        [ChatCommand("osq.reloadconfig")]
        void ReloadConfig(BasePlayer player, string command, string[] args)
        {
            if (!player.IsAdmin)
                return;

            LoadConfigData();
            ValidateConfig();
        }
        //
        // Clear plugin data
        //
        [ConsoleCommand("osq.cleardata")]
        void ClearData(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
                return;
            ClearData();
            Puts(Lang("ClearData"));
        }
        #endregion

        #region Helpers
        //
        // Get formatted string from the lang file
        //
        private string Lang(string key, string userId = null, params object[] args) => string.Format(lang.GetMessage(key, this, userId), args);
        //
        // Get Vector3 from Origin
        //
        private Vector3 GetVector3(Origin origin) => new Vector3(origin.x, origin.y, origin.z);
        //
        // Get Origin from Vector3
        //
        private Origin GetOrigin(Vector3 vector) => new Origin(vector);
        #endregion
    }
}