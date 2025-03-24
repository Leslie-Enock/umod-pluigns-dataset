using Facepunch;
using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Oxide.Plugins
{
    [Info("Oil Crate", "Default", "0.7.5")]
    [Description("Brings the pump jack back into the game")]
    class OilCrate : RustPlugin
    {
        private const string oilCrateAllow = "oilcrate.allow";
        private readonly List<Timer> timers = new List<Timer>();
        private readonly List<string> duplicate = new List<string>();
        private bool quarryLiquid;
        private bool pumpjackSolid;
        private bool effectStaticPumpjacks;
        private bool missingPermission;
        private float oilCrateChance;
        private float oilCrateDespawn;
        private bool dryOnly;
        private float oilGatherWorkNeededMin;
        private float oilGatherWorkNeededMax;
        private bool dropFreePumpjack;
        private bool craftViaPickaxe;
        private Dictionary<string, object> pumpJackResources = new Dictionary<string, object>();

        private Dictionary<string, object> pumpJackResourcesDefault()
        {
            return new Dictionary<string, object>()
            {
                {"gears", 15},
                {"metal.fragments", 1750},
                {"wood", 10000},
            };
        }

        private void LoadVariables()
        {
            quarryLiquid = GetConfig("1. Settings", "1. Quarry can gather oil", false);
            pumpjackSolid = GetConfig("1. Settings", "2. Pump jack can gather ores", false);
            effectStaticPumpjacks = GetConfig("1. Settings", "3. Change gather rate of static pump jacks (at monuments)", true);
            missingPermission = GetConfig("1. Settings", "4. Show 'missing permission' message", true);
            oilCrateChance = GetConfig("2. Oil crate", "1. Chance in % (0.0 - 100.0)", 5f);
            oilCrateDespawn = GetConfig("2. Oil crate", "2. Despawn timer in seconds", 300f);
            dryOnly = GetConfig("2. Oil crate", "3. Only on dry biome", true);
            oilGatherWorkNeededMin = 10 / GetConfig("3. Crude oil gather rate", "1. Minimum crude oil per low grade fuel", 0.525f);
            oilGatherWorkNeededMax = 10 / GetConfig("3. Crude oil gather rate", "2. Maximum crude oil per low grade fuel", 0.725f);
            dropFreePumpjack = GetConfig("4. Get pump jack", "1. Drop a free pump jack out of a oil crate", false);
            craftViaPickaxe = GetConfig("4. Get pump jack", "2. Craft via hit with a pickaxe", true);
            pumpJackResources = GetConfig("4. Get pump jack", "3. Resources to craft a pump jack", pumpJackResourcesDefault());
            SaveConfig();
        }
        protected override void LoadDefaultConfig()
        {
            Puts("Creating a new config file");
            LoadVariables();
        }
        private void Init()
        {
            permission.RegisterPermission(oilCrateAllow, this);
            LoadVariables();
            if (oilCrateChance < 0)
            {
                oilCrateChance = 0;
            }

            if (oilCrateChance > 100)
            {
                oilCrateChance = 100;
            }
        }
        /*private void Unload()
        {
            foreach (Timer time in timers)
            {
                time.Destroy();
            }
        }*/
        private void OnResourceDepositCreated(ResourceDepositManager.ResourceDeposit resourceDeposit)
        {
            if (Random.Range(0f, 100f) >= oilCrateChance)
            {
                return;
            }

            if (!pumpjackSolid)
            {
                resourceDeposit._resources.Clear();
            }



            resourceDeposit.Add(ItemManager.FindItemDefinition("crude.oil"), 1f, 50000, Random.Range(oilGatherWorkNeededMin, oilGatherWorkNeededMax), ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, false);
        }
        private void OnSurveyGather(SurveyCharge survey, Item item)
        {
            if (item.info.name != "crude_oil.item")
            {
                return;
            }

            Vector3 pos = survey.transform.position;
            string posID = $"{pos.x}{pos.y}{pos.z}";
            if (duplicate.Contains(posID))
            {
                timer.In(1f, () =>
                {
                    duplicate.Remove(posID);
                });
                return;
            }
            if (!IsAllowed((BasePlayer)survey.creatorEntity, true))
            {
                item.Remove();
                DeSpawn("survey_crater", pos, 0f);
                if (missingPermission)
                {
                    SendReply((BasePlayer)survey.creatorEntity, lang.GetMessage("NoPriv", this, survey.creatorEntity.ToString()));
                }

                return;
            }
            if (dryOnly && TerrainMeta.BiomeMap.GetBiome(pos, TerrainBiome.ARID) < 0.5f)
            {
                item.Remove();
                DeSpawn("survey_crater", pos, 0f);
                if (missingPermission)
                {
                    SendReply((BasePlayer)survey.creatorEntity, lang.GetMessage("DryOnly", this, survey.creatorEntity.ToString()));
                }

                return;
            }
            if (!quarryLiquid)
            {
                DeSpawn("survey_crater.prefab", pos, 0f);
            }

            Spawn("assets/prefabs/tools/surveycharge/survey_crater_oil.prefab", pos);
            DeSpawn("survey_crater", pos, oilCrateDespawn);
            duplicate.Add(posID);
            if (!dropFreePumpjack)
            {
                return;
            }

            Item pumpJack = ItemManager.CreateByName("mining.pumpjack", 1);
            pumpJack.Drop(pos + new Vector3(.5f, .5f), Vector3.zero, Quaternion.AngleAxis(0, Vector3.left));
        }
        private void OnEntitySpawned(BaseNetworkable entity)
        {
            MiningQuarry quarry = entity as MiningQuarry;
            if (quarry == null)
            {
                return;
            }

            if (!quarry.ShortPrefabName.Contains("pumpjack.static"))
            {
                quarry.canExtractSolid = true;
            }

            if (entity.ShortPrefabName.Contains("mining.quarry"))
            {
                quarry.canExtractLiquid = quarryLiquid;
            }

            if (quarry.ShortPrefabName.Contains("pumpjack.static") && effectStaticPumpjacks)
            {
                MiningQuarry[] quarries = UnityEngine.Object.FindObjectsOfType<MiningQuarry>();
                quarry._linkedDeposit._resources.Clear();
                quarry._linkedDeposit.Add(ItemManager.FindItemDefinition("crude.oil"), 1f, 50000, Random.Range(oilGatherWorkNeededMin, oilGatherWorkNeededMax), ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, true);
                quarry.SendNetworkUpdateImmediate();
            }
        }
        private void OnPlayerAttack(BasePlayer attacker, HitInfo info)
        {
            if (!craftViaPickaxe)
            {
                return;
            }

            SurveyCrater oilCrate = info?.HitEntity as SurveyCrater;
            if (oilCrate == null || oilCrate.ShortPrefabName != "survey_crater_oil")
            {
                return;
            }

            Item item = info.Weapon?.GetItem();
            if (item == null || !item.info.name.Contains("pickaxe"))
            {
                return;
            }

            Dictionary<int, int> toTake = new Dictionary<int, int>();
            foreach (KeyValuePair<string, object> resourcePair in pumpJackResources)
            {
                ItemDefinition resource = ItemManager.FindItemDefinition(resourcePair.Key);
                int existingAmount = attacker.inventory.GetAmount(resource.itemid);
                if (existingAmount < (int)resourcePair.Value)
                {
                    SendReply(attacker, lang.GetMessage("NotEnough", this, attacker.UserIDString), resource.displayName.english.ToLower());
                    return;
                }
                toTake.Add(resource.itemid, (int)resourcePair.Value);
            }
            foreach (KeyValuePair<int, int> take in toTake)
            {
                attacker.inventory.Take(null, take.Key, take.Value);
            }

            Item pumpJack = ItemManager.CreateByName("mining.pumpjack", 1);
            pumpJack.Drop(oilCrate.transform.position + new Vector3(.5f, .5f), Vector3.zero, Quaternion.AngleAxis(0, Vector3.left));
        }

        [ConsoleCommand("oilcrate")]
        private void ccmdOilCrate(ConsoleSystem.Arg arg)
        {
            if (arg.Connection != null && arg.Player() == null)
            {
                return;
            }

            cmdOilCrate(arg.Player(), string.Empty, arg.Args);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Help"] = "You can use the following commands:\n- regenerate | to <color=yellow>regenerate</color> each crude oil source\n- refill | to <color=yellow>refill</color> each pump jack\n- clear | to <color=red>remove</color> crude oil out of every source. This cannot be undone\n- destroy | similar use to clear, but only removes out of empty ones.",
                ["QuarryUpdate"] = "The gather rate for crude oil in each pump jack and quarry was successfully updated.",
                ["PumpUpdate"] = "The gather rate for crude oil in each pump jack was successfully updated.",
                ["QuarryRemove"] = "The crude oil of each pump jack and quarry was successfully removed.",
                ["PumpAdd"] = "The crude oil was successfully added to every pump jack.",
                ["NotAllowed"] = "You're not allowed to use this command.\nIf you believe this is an error, please contact the server admin.",
                ["QuarryDestroy"] = "The crude oil of each pump jack and quarry was successfully removed. And blanks were destroyed.",
                ["PumpDestroy"] = "The crude oil of each pump jack was successfully removed. And blanks were destroyed.",
                ["Error"] = "Syntax error: /oilcrate {0}",
                ["PumpRemove"] = "The crude oil of each pump jack was successfully removed.",
                ["NotEnough"] = "You can't gather the pump jack. You don't have enough {0}.",
                ["DryOnly"] = "You can only find oil in the southern desert",
                ["NoPriv"] = "You don't have the privileges to perform an oil drilling",
                //[""] = "",
            }, this);
        }


        [ChatCommand("oilcrate")]
        private void cmdOilCrate(BasePlayer player, string command, string[] args)
        {
            if (player != null && !IsAllowed(player))
            {
                SendReply(player, lang.GetMessage("NotAllowed", this, player.UserIDString));
                return;
            }
            if (args == null || args.Length <= 0 || args.Length > 2)
            {

                SendReply(player, lang.GetMessage("Help", this, player.UserIDString));
                return;
            }
            switch (args[0].ToLower())
            {
                case "regenerate":
                    RefreshQuarries("regenerate");
                    if (player == null)
                    {
                        if (quarryLiquid)
                        {
                            Puts("The gather rate crude oil in each pump jack and quarry was successfully updated.");
                        }
                        else
                        {
                            Puts("The gather rate crude oil in each pump jack was successfully updated.");
                        }
                    }
                    if (quarryLiquid)
                    {
                        SendReply(player, lang.GetMessage("QuarryUpdate", this, player.UserIDString));
                    }
                    else
                    {
                        SendReply(player, lang.GetMessage("PumpUpdate", this, player.UserIDString));
                    }

                    break;
                case "refill":
                    RefreshQuarries("refill");
                    if (player == null)
                    {
                        Puts("The crude oil was successfully added to each pump jack.");
                    }

                    SendReply(player, lang.GetMessage("PumpAdd", this, player.UserIDString));
                    break;
                case "clear":
                    RefreshQuarries("clear");
                    if (player == null)
                    {
                        if (quarryLiquid)
                        {
                            Puts("The crude oil of each pump jack and quarry was successfully removed.");
                        }
                        else
                        {
                            Puts("The crude oil of each pump jack was successfully removed.");
                        }
                    }
                    if (quarryLiquid)
                    {
                        SendReply(player, lang.GetMessage("QuarryRemove", this, player.UserIDString));
                    }
                    else
                    {
                        SendReply(player, lang.GetMessage("PumpRemove", this, player.UserIDString));
                    }

                    break;
                case "destroy":
                    RefreshQuarries("destroy");
                    if (player == null)
                    {
                        if (quarryLiquid)
                        {
                            Puts("The crude oil of each pump jack and quarry was successfully removed. And blanks were destroyed.");
                        }
                        else
                        {
                            Puts("The crude oil of each pump jack was successfully removed. And blanks were destroyed.");
                        }
                    }
                    if (quarryLiquid)
                    {
                        SendReply(player, lang.GetMessage("QuarryDestroy", this, player.UserIDString));
                    }
                    else
                    {
                        SendReply(player, lang.GetMessage("PumpDestroy", this, player.UserIDString));
                    }

                    break;
                default:
                    SendReply(player, lang.GetMessage("Error", this, player.UserIDString), args[0]);
                    break;
            }
        }

        private void RefreshQuarries(string todo)
        {
            MiningQuarry[] quarries = UnityEngine.Object.FindObjectsOfType<MiningQuarry>();
            foreach (MiningQuarry quarry in quarries)
            {
                ResourceDepositManager.ResourceDeposit depo = quarry._linkedDeposit;
                switch (todo)
                {
                    case "regenerate":
                        if (quarry.ShortPrefabName == "pumpjack-static")
                        {
                            depo._resources.Clear();
                            depo.Add(ItemManager.FindItemDefinition("crude.oil"), 1f, 50000, Random.Range(oilGatherWorkNeededMin, oilGatherWorkNeededMax), ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, true);
                            quarry.SendNetworkUpdateImmediate();
                            continue;
                        }
                        if (quarry.ShortPrefabName == "mining_quarry")
                        {
                            quarry.canExtractLiquid = quarryLiquid;
                        }

                        if (quarry.ShortPrefabName.Contains("mining_quarry") && !quarryLiquid)
                        {
                            continue;
                        }

                        foreach (ResourceDepositManager.ResourceDeposit.ResourceDepositEntry resource in depo._resources)
                        {
                            if (resource.type.transform.name != "crude_oil.item")
                            {
                                continue;
                            }

                            depo._resources.Remove(resource);
                            depo.Add(ItemManager.FindItemDefinition("crude.oil"), 1f, 50000, Random.Range(oilGatherWorkNeededMin, oilGatherWorkNeededMax), ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, true);
                            quarry.SendNetworkUpdateImmediate();
                            break;
                        }
                        continue;
                    case "refill":
                        if (quarry.ShortPrefabName == "mining_quarry")
                        {
                            continue;
                        }

                        depo._resources.Clear();
                        depo.Add(ItemManager.FindItemDefinition("crude.oil"), 1f, 50000, Random.Range(oilGatherWorkNeededMin, oilGatherWorkNeededMax), ResourceDepositManager.ResourceDeposit.surveySpawnType.ITEM, true);
                        quarry.SendNetworkUpdateImmediate();
                        continue;
                    case "clear":
                        if (quarry.ShortPrefabName == "mining_quarry")
                        {
                            quarry.canExtractLiquid = false;
                        }

                        foreach (ResourceDepositManager.ResourceDeposit.ResourceDepositEntry resource in depo._resources)
                        {
                            if (resource.type.transform.name != "crude_oil.item")
                            {
                                continue;
                            }

                            depo._resources.Remove(resource);
                            quarry.SendNetworkUpdateImmediate();
                            break;
                        }
                        continue;
                    case "destroy":
                        if (quarry.ShortPrefabName == "mining_quarry")
                        {
                            quarry.canExtractLiquid = false;
                        }

                        foreach (ResourceDepositManager.ResourceDeposit.ResourceDepositEntry resource in depo._resources)
                        {
                            if (resource.type.transform.name != "crude_oil.item")
                            {
                                continue;
                            }

                            depo._resources.Remove(resource);
                            quarry.SendNetworkUpdateImmediate();
                        }
                        if (depo._resources.Count <= 0)
                        {
                            quarry.KillMessage();
                        }

                        continue;
                }
            }
        }

        private void Spawn(string prefab, Vector3 position)
        {
            Quaternion rot;
            Vector3 pos;
            GetLocation(position, out pos, out rot);
            var createdPrefab = GameManager.server.CreatePrefab(prefab, pos, rot);
            if (createdPrefab == null) return;
            var entity = createdPrefab.GetComponent<BaseEntity>();
            entity.Spawn();
        }
        private void DeSpawn(string prefab, Vector3 position, float time)
        {
            timers.Add(timer.Once(time, () =>
            {
                List<SurveyCrater> nearby = Pool.GetList<SurveyCrater>();
                Vis.Entities(position, 1f, nearby);
                foreach (SurveyCrater ent in nearby)
                {
                    if (ent.PrefabName.Contains(prefab))
                    {
                        ent.KillMessage();
                    }
                }

                Pool.FreeList(ref nearby);
            }));
        }
        private void GetLocation(Vector3 startPos, out Vector3 pos, out Quaternion rot)
        {
            pos = startPos;
            pos.y = 0f;
            rot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            RaycastHit raycastHit;
            if (TerrainMeta.HeightMap)
            {
                var height = TerrainMeta.HeightMap.GetHeight(pos) - 0.2f;
                pos.y = Mathf.Max(pos.y, height);
            }
            if (TransformUtil.GetGroundInfo(pos, out raycastHit, 20f, -1063190527))
            {
                pos = raycastHit.point;
                rot = Quaternion.LookRotation(rot * Vector3.forward, raycastHit.normal);
            }
        }
        private T GetConfig<T>(string name, string name2, T defaultValue)
        {
            var data = Config[name] as Dictionary<string, object>;
            if (data == null)
            {
                data = new Dictionary<string, object>();
                Config[name] = data;
            }
            object value;
            if (!data.TryGetValue(name2, out value))
            {
                value = defaultValue;
                data[name2] = value;
            }
            return (T)Convert.ChangeType(Config[name, name2], typeof(T));
        }
        private bool IsAllowed(BasePlayer player, bool perm = false)
        {
            return player.net?.connection?.authLevel > 1 || perm && permission.UserHasPermission(player.UserIDString, oilCrateAllow);
        }
    }
}
