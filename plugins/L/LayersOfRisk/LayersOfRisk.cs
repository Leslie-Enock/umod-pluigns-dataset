using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Random = System.Random;

namespace Oxide.Plugins
{
    [Info("Layers of Risk", "Mike Danielsson", 1.24)]
    [Description("Play in this new game changing world where more focus is on how you choose your Risks.")]
    class LayersOfRisk : RustPlugin
    {
        #region Variables
        //Default variables
        bool hasLateInitializedBeenRun = false;
        float TimeLookPlayerUiIsDrawn = 2.4f;
        Vector3 middlePosition = new Vector3(0, 0, 0);
        Vector3 downDirection = new Vector3(0, -1, 0);
        Vector3 vectormultiplier = new Vector3(0, 2, 0);
        int hackableCrateDefaultHackTime = 900;
        //Changable
        bool enableZoneIndicators = false;
        int zoneIndicatorStrength = 12;
        int outerWall = 723;
        int middleWall = 437;
        int InnerWall = 182;
        int distanceToBuildFromWalls = 25;
        int hackAbleWarZoneExtraTime = 20;
        int maxHackableCrateSpawns = 25;
        int hackableCratesSpawedAtPluginStart = 8;
        int timeBetweenHackableSpawnTries = 300;
        int hackableCrateZone1Time = 780;
        int hackableCrateZone2Time = 660;
        int hackableCrateZone3Time = 480;
        int hackableCrateZone4Time = 180;

        int hackableCrateZone1ArmourStart = 0;
        int hackableCrateZone1ArmourEnd = 4;
        int hackableCrateZone2ArmourStart = 2;
        int hackableCrateZone2ArmourEnd = 10;
        int hackableCrateZone3ArmourStart = 9;
        int hackableCrateZone3ArmourEnd = 16;
        int hackableCrateZone4ArmourStart = 15;
        int hackableCrateZone4ArmourEnd = 22;

        int hackableCrateZone1WeaponStart = 1;
        int hackableCrateZone1WeaponEnd = 21;
        int hackableCrateZone2WeaponStart = 17;
        int hackableCrateZone2WeaponEnd = 30;
        int hackableCrateZone3WeaponStart = 25;
        int hackableCrateZone3WeaponEnd = 35;
        int hackableCrateZone4WeaponStart = 32;
        int hackableCrateZone4WeaponEnd = 44;

        //War zone variables
        int totalWarZones = 0;
        int warZoneTimeBetweenIntervalls = 1;
        float playerWarZoneStartDecreser = 0.001f;
        //Changable
        int warZoneSize = 150;
        int warZoneRaidAliveTimer = 600;
        int playerWarZone1Delay = 3600;
        int playerWarZone2Delay = 2100;
        int playerWarZone3Delay = 1500;
        int playerWarZone4Delay = 900;
        int timeBeforePlayerTakeDamageWhenEnterWarZone = 6;
        int startWarZoneAfterDamageToStructure = 25;
        int warZoneEnterDamage = 20;
        bool canDieWhenEnteringWarZone = false;

        //Timer variables
        float updateTierRefreshRate = 0.2f;
        float initializeTimerDelay = 1f;
        float tierPointChangeDelay = 0;
        //Changable
        int updateTierTime = 20;

        //Prefabs
        string hackableCreate = "assets/prefabs/deployable/chinooklockedcrate/codelockedhackablecrate.prefab";
        string warZoneSphere = "assets/prefabs/visualization/sphere.prefab";

        //VFX prefabs
        string vomit = "assets/bundled/prefabs/fx/gestures/drink_vomit.prefab";

        //GUI elements
        string uiTierBox = "ui_tier_box";
        string uiTierRangeBox = "ui_tier_range_box";
        string uiZoneBox = "ui_zone_box";
        string uiMeterZoneBox = "ui_meter_zone_box";
        string uiTierLoading = "ui_tier_loading ";
        string uiTierPoints = "ui_tier_points";
        string uiInfoText = "ui_info_text";
        string uiGetPlayerTierOnLook = "ui_get_player_tier_on_look";
        string uiTierPointsChange = "ui_tier_points_change";

        //Tier box
        float tierBoxPosX = 0.3445f;
        float tierBoxPosY = 0.113f;
        float tierBoxSizeX = 0.0457f;
        float tierBoxSizeY = 0.041f;

        //Tier progress bar box
        float tierProgressPosX = 0.394f;
        float tierProgressPosY = 0.113f;
        float tierProgressSizeX = 0.2464f;
        float tierProgressSizeY = 0.041f;

        //Tier range box
        float tierRangeBoxPosX = 0.112f;
        float tierRangeBoxPosY = 0.025f;
        float tierRangeBoxSizeX = 0.1494f;
        float tierRangeBoxSizeY = 0.041f;

        //Zone box
        float zoneBoxPosX = 0.112f;
        float zoneBoxPosY = 0.0725f;
        float zoneBoxSizeX = 0.045f;
        float zoneBoxSizeY = 0.041f;

        //Meters to next zone box
        float meterZoneBoxPosX = 0.1602f;
        float meterZoneBoxPosY = 0.0725f;
        float meterZoneBoxSizeX = 0.03f;
        float meterZoneBoxSizeY = 0.041f;

        //Get tier on look text box
        float getTierOnLookPosX = 0.39f;
        float getTierOnLookPosY = 0.85f;
        float getTierOnLookSizeX = 0.22f;
        float getTierOnLookSizeY = 0.041f;

        //Get tier on look text box
        float getTierPointsChangePosX = 0.443f;
        float getTierPointsChangePosY = 0.14f;
        float getTierPointsChangeSizeX = 0.15f;
        float getTierPointsChangeSizeY = 0.1f;

        //Data files
        DynamicConfigFile weaponsDataFile;
        DynamicConfigFile armoursDataFile;
        DynamicConfigFile tiersDataFile;
        DynamicConfigFile hackableCreatPositionsDataFile;

        class playerDataClass
        {
            public bool isUpdateTierTimerRunning { get; set; }
            public bool isPlayerInitialized { get; set; }
            public int currentTier {get; set; }
            public int nextTier {get; set; }
            public int oldTier { get; set; }
            public int currentZone { get; set; }
            public float distanceFromMiddle { get; set; }
            public float cantStartRaidZoneTimer { get; set; }
            public float startRaidZoneAfterDamageDealtToStructure { get; set; }
            public float currentUpdateCycle { get; set; }
            public int tierId { get; set; }
            public float lookPlayerDelay { get; set; }
        }

        class tierDataClass
        {
            public string tierLetter { get; set; }
            public int min { get; set; }
            public int max { get; set; }
        }

        class itemDataClass
        {
            public string shortName { get; set; }
            public int value { get; set; }
            public int followItem { get; set; }
        }

        class warZoneDataClass
        {
            public int zone { get; set; }
            public float size { get; set; }
            public float currentUpTime { get; set; }
            public float aliveTime { get; set; }
            public Vector3 position { get; set; }
        }

        class hackableCrateDataClass
        {
            public bool inUse { get; set; }
        }

        //Player data dictionary
        Dictionary<ulong, playerDataClass> playerDataDic = new Dictionary<ulong, playerDataClass>();

        //Tier data dictionary
        Dictionary<int, tierDataClass> tierDataDic = new Dictionary<int, tierDataClass>();

        //Weapons data dictionary
        Dictionary<int, itemDataClass> weaponDataDic = new Dictionary<int, itemDataClass>();

        //Armours data dictionary
        Dictionary<int, itemDataClass> armourDataDic = new Dictionary<int, itemDataClass>();

        //War zone data dictionary
        Dictionary<int, warZoneDataClass> warZoneDataDic = new Dictionary<int, warZoneDataClass>();

        //War zone timer dictionary
        Dictionary<int, Timer> timerHolderDic = new Dictionary<int, Timer>();

        //Hackable crate data dictionary
        Dictionary<Vector3, hackableCrateDataClass> hackableCrateDataDic = new Dictionary<Vector3, hackableCrateDataClass>();
        #endregion

        #region Initialization
        void Init()
        {
            updateSettingsFromConfigFile();

            //Check if weaponConfigFile exits and name the json file.
            weaponsDataFile = createConfigFileIfDontExist("LayersOfRiskWeapons");

            //Check if armourConfigFile exits and name the json file.
            armoursDataFile = createConfigFileIfDontExist("LayersOfRiskArmours");

            //Check if layersOfRiskTierData exits and name the json file.
            tiersDataFile = createConfigFileIfDontExist("LayersOfRiskTierData");

            hackableCreatPositionsDataFile = createConfigFileIfDontExist("LayersOfRiskHackableCratePositions");

            //Get tier data from data files
            dataInitialization();
        }

        protected override void LoadDefaultConfig()
        {
            Config["world", "outerWall"] = outerWall;
            Config["world", "middleWall"] = middleWall;
            Config["world", "InnerWall"] = InnerWall;
            Config["world", "distanceToBuildFromWalls"] = distanceToBuildFromWalls;
            Config["world", "enableZoneIndicators"] = enableZoneIndicators;
            Config["world", "zoneIndicatorStrength"] = zoneIndicatorStrength;

            Config["player tiers", "updateTierTime"] = updateTierTime;

            Config["hackable crates", "maxHackableCrateSpawns"] = maxHackableCrateSpawns;
            Config["hackable crates", "hackAbleWarZoneExtraTime"] = hackAbleWarZoneExtraTime;
            Config["hackable crates", "hackableCratesSpawedAtPluginStart"] = hackableCratesSpawedAtPluginStart;
            Config["hackable crates", "timeBetweenHackableSpawnTries"] = timeBetweenHackableSpawnTries;
            Config["hackable crates", "hackableCrateZone1Time"] = hackableCrateZone1Time;
            Config["hackable crates", "hackableCrateZone2Time"] = hackableCrateZone2Time;
            Config["hackable crates", "hackableCrateZone3Time"] = hackableCrateZone3Time;
            Config["hackable crates", "hackableCrateZone4Time"] = hackableCrateZone4Time;

            Config["hackable crates", "content", "hackableCrateZone1ArmourStart"] = hackableCrateZone1ArmourStart;
            Config["hackable crates", "content", "hackableCrateZone1ArmourEnd"] = hackableCrateZone1ArmourEnd;
            Config["hackable crates", "content", "hackableCrateZone2ArmourStart"] = hackableCrateZone2ArmourStart;
            Config["hackable crates", "content", "hackableCrateZone2ArmourEnd"] = hackableCrateZone2ArmourEnd;
            Config["hackable crates", "content", "hackableCrateZone3ArmourStart"] = hackableCrateZone3ArmourStart;
            Config["hackable crates", "content", "hackableCrateZone3ArmourEnd"] = hackableCrateZone3ArmourEnd;
            Config["hackable crates", "content", "hackableCrateZone4ArmourStart"] = hackableCrateZone4ArmourStart;
            Config["hackable crates", "content", "hackableCrateZone4ArmourEnd"] = hackableCrateZone4ArmourEnd;
            Config["hackable crates", "content", "hackableCrateZone1WeaponStart"] = hackableCrateZone1WeaponStart;
            Config["hackable crates", "content", "hackableCrateZone1WeaponEnd"] = hackableCrateZone1WeaponEnd;
            Config["hackable crates", "content", "hackableCrateZone2WeaponStart"] = hackableCrateZone2WeaponStart;
            Config["hackable crates", "content", "hackableCrateZone2WeaponEnd"] = hackableCrateZone2WeaponEnd;
            Config["hackable crates", "content", "hackableCrateZone3WeaponStart"] = hackableCrateZone3WeaponStart;
            Config["hackable crates", "content", "hackableCrateZone3WeaponEnd"] = hackableCrateZone3WeaponEnd;
            Config["hackable crates", "content", "hackableCrateZone4WeaponStart"] = hackableCrateZone4WeaponStart;
            Config["hackable crates", "content", "hackableCrateZone4WeaponEnd"] = hackableCrateZone4WeaponEnd;

            Config["war zones", "warZoneSize"] = warZoneSize;
            Config["war zones", "warZoneRaidAliveTimer"] = warZoneRaidAliveTimer;
            Config["war zones", "playerWarZone1Delay"] = playerWarZone1Delay;
            Config["war zones", "playerWarZone2Delay"] = playerWarZone2Delay;
            Config["war zones", "playerWarZone3Delay"] = playerWarZone3Delay;
            Config["war zones", "playerWarZone4Delay"] = playerWarZone4Delay;
            Config["war zones", "timeBeforePlayerTakeDamageWhenEnterWarZone"] = timeBeforePlayerTakeDamageWhenEnterWarZone;
            Config["war zones", "startWarZoneAfterDamageToStructure"] = startWarZoneAfterDamageToStructure;
            Config["war zones", "warZoneEnterDamage"] = warZoneEnterDamage;
            Config["war zones", "canDieWhenEnteringWarZone"] = canDieWhenEnteringWarZone;
        }

        void updateSettingsFromConfigFile()
        {
            LoadConfig();

            outerWall = (int)Config["world", "outerWall"];
            middleWall = (int)Config["world", "middleWall"];
            InnerWall = (int)Config["world", "InnerWall"];
            distanceToBuildFromWalls = (int)Config["world", "distanceToBuildFromWalls"];
            enableZoneIndicators = (bool)Config["world", "enableZoneIndicators"];
            zoneIndicatorStrength = (int)Config["world", "zoneIndicatorStrength"];

            updateTierTime = (int)Config["player tiers", "updateTierTime"];

            maxHackableCrateSpawns = (int)Config["hackable crates", "maxHackableCrateSpawns"];
            hackAbleWarZoneExtraTime = (int)Config["hackable crates", "hackAbleWarZoneExtraTime"];
            hackableCratesSpawedAtPluginStart = (int)Config["hackable crates", "hackableCratesSpawedAtPluginStart"];
            timeBetweenHackableSpawnTries = (int)Config["hackable crates", "timeBetweenHackableSpawnTries"];
            hackableCrateZone1Time = (int)Config["hackable crates", "hackableCrateZone1Time"];
            hackableCrateZone2Time = (int)Config["hackable crates", "hackableCrateZone2Time"];
            hackableCrateZone3Time = (int)Config["hackable crates", "hackableCrateZone3Time"];
            hackableCrateZone4Time = (int)Config["hackable crates", "hackableCrateZone4Time"];

            hackableCrateZone1ArmourStart = (int)Config["hackable crates", "content", "hackableCrateZone1ArmourStart"];
            hackableCrateZone1ArmourEnd = (int)Config["hackable crates", "content", "hackableCrateZone1ArmourEnd"];
            hackableCrateZone2ArmourStart = (int)Config["hackable crates", "content", "hackableCrateZone2ArmourStart"];
            hackableCrateZone2ArmourEnd = (int)Config["hackable crates", "content", "hackableCrateZone2ArmourEnd"];
            hackableCrateZone3ArmourStart = (int)Config["hackable crates", "content", "hackableCrateZone3ArmourStart"];
            hackableCrateZone3ArmourEnd = (int)Config["hackable crates", "content", "hackableCrateZone3ArmourEnd"];
            hackableCrateZone4ArmourStart = (int)Config["hackable crates", "content", "hackableCrateZone4ArmourStart"];
            hackableCrateZone4ArmourEnd = (int)Config["hackable crates", "content", "hackableCrateZone4ArmourEnd"];
            hackableCrateZone1WeaponStart = (int)Config["hackable crates", "content", "hackableCrateZone1WeaponStart"];
            hackableCrateZone1WeaponEnd = (int)Config["hackable crates", "content", "hackableCrateZone1WeaponEnd"];
            hackableCrateZone2WeaponStart = (int)Config["hackable crates", "content", "hackableCrateZone2WeaponStart"];
            hackableCrateZone2WeaponEnd = (int)Config["hackable crates", "content", "hackableCrateZone2WeaponEnd"];
            hackableCrateZone3WeaponStart = (int)Config["hackable crates", "content", "hackableCrateZone3WeaponStart"];
            hackableCrateZone3WeaponEnd = (int)Config["hackable crates", "content", "hackableCrateZone3WeaponEnd"];
            hackableCrateZone4WeaponStart = (int)Config["hackable crates", "content", "hackableCrateZone4WeaponStart"];
            hackableCrateZone4WeaponEnd = (int)Config["hackable crates", "content", "hackableCrateZone4WeaponEnd"];

            warZoneSize = (int)Config["war zones", "warZoneSize"];
            warZoneRaidAliveTimer = (int)Config["war zones", "warZoneRaidAliveTimer"];
            playerWarZone1Delay = (int)Config["war zones", "playerWarZone1Delay"];
            playerWarZone2Delay = (int)Config["war zones", "playerWarZone2Delay"];
            playerWarZone3Delay = (int)Config["war zones", "playerWarZone3Delay"];
            playerWarZone4Delay = (int)Config["war zones", "playerWarZone4Delay"];
            timeBeforePlayerTakeDamageWhenEnterWarZone = (int)Config["war zones", "timeBeforePlayerTakeDamageWhenEnterWarZone"];
            startWarZoneAfterDamageToStructure = (int)Config["war zones", "startWarZoneAfterDamageToStructure"];
            warZoneEnterDamage = (int)Config["war zones", "warZoneEnterDamage"];
            canDieWhenEnteringWarZone = (bool)Config["war zones", "canDieWhenEnteringWarZone"];

            if(zoneIndicatorStrength > 50)
            {
                zoneIndicatorStrength = 50;
            }

            if(hackableCratesSpawedAtPluginStart > 500)
            {
                hackableCratesSpawedAtPluginStart = 500;
            }

            if (timeBetweenHackableSpawnTries < 2)
            {
                hackableCratesSpawedAtPluginStart = 2;
            }
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CantDamagePlayer"] = "<color=#ec3636>[Warning]</color> Cant damage players in <color=#3683ec>tier {0}</color>!",
                ["CantDamageUpdatingTier"] = "<color=#ec3636>[Warning]</color> Cant deal damage while updating tier!",
                ["CantDamageInOtherZones"] = "<color=#ec3636>[Warning]</color> Cant damage players in other Zones!",
                ["CantDamageBuldings"] = "<color=#ec3636>[Warning]</color> Cant damage buldings for {0}!",
                ["StartWarZoneIfDamageDealt"] = "<color=#ec3636>[Warning]</color> Starting warzone zone if {0} more damage dealt!",
                ["OnlyWood"] = "<color=#ec3636>[Warning]</color> Can only use <color=#3683ec>Wood</color> in Zone 1!",
                ["OnlyWoodStone"] = "<color=#ec3636>[Warning]</color> Can only use <color=#3683ec>Wood</color> and <color=#3683ec>Stone</color> in Zone 2!",
                ["OnlyWoodStoneMetal"] = "<color=#ec3636>[Warning]</color> Can only use <color=#3683ec>Wood</color>, <color=#3683ec>Stone</color> and <color=#3683ec>Metal</color> in Zone 3!",
                ["Vomited"] = "<color=#ec3636>[Vomited]</color> Lost {0} health!",
                ["UpdatingTier"] = "updating",
                ["OnlyWoodenDoors"] = "<color=#ec3636>[Warning]</color> You can only build <color=#3683ec>wooden</color> doors in Zone 1!",
                ["OnlyWoodenMetalDoors"] = "<color=#ec3636>[Warning]</color> You can only build <color=#3683ec>wooden</color> and <color=#3683ec>metal</color> doors in Zone 2!",
                ["CantBuildCloseToEdge"] = "<color=#ec3636>[Warning]</color> Cant build so close to the edge of a zone!",
                ["NoDamageWhileUpdatingTier"] = "<color=#ec3636>[Warning]</color> While updating tier everyone can kill you, but you cant deal damage to players!",
                ["ChangingTier"] = "Changing",
                ["CrateSpawn"] = "Added crate spawn at position: {0}",
            }, this);
        }

        DynamicConfigFile createConfigFileIfDontExist(string file)
        {
            Debug.Log(Config["LeaveMessage"]);
            bool didExist = false;

            if (Interface.Oxide.DataFileSystem.ExistsDatafile(file))
            {
                Puts("File " + file + " allready exists. No need to create one.");

                didExist = true;
            }

            DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetDatafile(file);

            if (didExist == false)
            {
                if (file == "LayersOfRiskWeapons")
                {
                    dataFile["1", "id"] = 963906841; dataFile["1", "shortName"] = "rock"; dataFile["1", "value"] = 5; dataFile["1", "followItem"] = 0;
                    dataFile["2", "id"] = 1711033574; dataFile["2", "shortName"] = "bone.club"; dataFile["2", "value"] = 20; dataFile["2", "followItem"] = 1;
                    dataFile["3", "id"] = -1583967946; dataFile["3", "shortName"] = "stonehatchet"; dataFile["3", "value"] = 35; dataFile["3", "followItem"] = 1;
                    dataFile["4", "id"] = 171931394; dataFile["4", "shortName"] = "stone.pickaxe"; dataFile["4", "value"] = 35; dataFile["4", "followItem"] = 1;
                    dataFile["5", "id"] = 1814288539; dataFile["5", "shortName"] = "knife.bone"; dataFile["5", "value"] = 50; dataFile["5", "followItem"] = 1;
                    dataFile["6", "id"] = 1540934679; dataFile["6", "shortName"] = "spear.wooden"; dataFile["6", "value"] = 70; dataFile["6", "followItem"] = 1;
                    dataFile["7", "id"] = 1602646136; dataFile["7", "shortName"] = "spear.stone"; dataFile["7", "value"] = 100; dataFile["7", "followItem"] = 1;
                    dataFile["8", "id"] = -1252059217; dataFile["8", "shortName"] = "hatchet"; dataFile["8", "value"] = 100; dataFile["8", "followItem"] = 1;
                    dataFile["9", "id"] = -1302129395; dataFile["9", "shortName"] = "pickaxe"; dataFile["9", "value"] = 100; dataFile["9", "followItem"] = 1;

                    dataFile["10", "id"] = -1137865085; dataFile["10", "shortName"] = "machete"; dataFile["10", "value"] = 110; dataFile["10", "followItem"] = 1;
                    dataFile["11", "id"] = -1966748496; dataFile["11", "shortName"] = "mace"; dataFile["11", "value"] = 130; dataFile["11", "followItem"] = 1;
                    dataFile["12", "id"] = -1978999529; dataFile["12", "shortName"] = "salvaged.cleaver"; dataFile["12", "value"] = 140; dataFile["12", "followItem"] = 1;
                    dataFile["13", "id"] = -1506397857; dataFile["13", "shortName"] = "hammer.salvaged"; dataFile["13", "value"] = 140; dataFile["13", "followItem"] = 1;
                    dataFile["14", "id"] = -1780802565; dataFile["14", "shortName"] = "icepick.salvaged"; dataFile["14", "value"] = 160; dataFile["14", "followItem"] = 1;
                    dataFile["15", "id"] = -262590403; dataFile["15", "shortName"] = "axe.salvaged"; dataFile["15", "value"] = 160; dataFile["15", "followItem"] = 1;
                    dataFile["16", "id"] = -1469578201; dataFile["16", "shortName"] = "longsword"; dataFile["16", "value"] = 180; dataFile["16", "followItem"] = 1;
                    dataFile["17", "id"] = 1326180354; dataFile["17", "shortName"] = "salvaged.sword"; dataFile["17", "value"] = 180; dataFile["17", "followItem"] = 1;
                    dataFile["18", "id"] = -75944661; dataFile["18", "shortName"] = "pistol.eoka"; dataFile["18", "value"] = 195; dataFile["18", "followItem"] = 588596902;
                    dataFile["19", "id"] = 1443579727; dataFile["19", "shortName"] = "bow.hunting"; dataFile["19", "value"] = 210; dataFile["19", "followItem"] = -1234735557;

                    dataFile["20", "id"] = -1367281941; dataFile["20", "shortName"] = "shotgun.waterpipe"; dataFile["20", "value"] = 270; dataFile["20", "followItem"] = 588596902;
                    dataFile["21", "id"] = 1953903201; dataFile["21", "shortName"] = "pistol.nailgun"; dataFile["21", "value"] = 270; dataFile["21", "followItem"] = 588596902;
                    dataFile["22", "id"] = -1215753368; dataFile["22", "shortName"] = "flamethrower"; dataFile["22", "value"] = 310; dataFile["22", "followItem"] = 0;
                    dataFile["23", "id"] = 1104520648; dataFile["23", "shortName"] = "chainsaw"; dataFile["23", "value"] = 320; dataFile["23", "followItem"] = 0;
                    dataFile["24", "id"] = 1488979457; dataFile["24", "shortName"] = "jackhammer"; dataFile["24", "value"] = 320; dataFile["24", "followItem"] = 0;
                    dataFile["25", "id"] = -765183617; dataFile["25", "shortName"] = "shotgun.double"; dataFile["25", "value"] = 320; dataFile["25", "followItem"] = -727717969;
                    dataFile["26", "id"] = 1965232394; dataFile["26", "shortName"] = "crossbow"; dataFile["26", "value"] = 320; dataFile["26", "followItem"] = -1234735557;
                    dataFile["27", "id"] = 884424049; dataFile["27", "shortName"] = "bow.compound"; dataFile["27", "value"] = 340; dataFile["27", "followItem"] = -1234735557;
                    dataFile["28", "id"] = 649912614; dataFile["28", "shortName"] = "pistol.revolver"; dataFile["28", "value"] = 395; dataFile["28", "followItem"] = 785728077;
                    dataFile["29", "id"] = 818877484; dataFile["29", "shortName"] = "pistol.semiauto"; dataFile["29", "value"] = 405; dataFile["29", "followItem"] = 785728077;

                    dataFile["30", "id"] = 795371088; dataFile["30", "shortName"] = "shotgun.pump"; dataFile["30", "value"] = 440; dataFile["30", "followItem"] = -727717969;
                    dataFile["31", "id"] = -852563019; dataFile["31", "shortName"] = "pistol.m92"; dataFile["31", "value"] = 460; dataFile["31", "followItem"] = 785728077;
                    dataFile["32", "id"] = 1373971859; dataFile["32", "shortName"] = "pistol.python"; dataFile["32", "value"] = 500; dataFile["32", "followItem"] = 785728077;
                    dataFile["33", "id"] = 1796682209; dataFile["33", "shortName"] = "smg.2"; dataFile["33", "value"] = 610; dataFile["33", "followItem"] = 785728077;
                    dataFile["34", "id"] = -1758372725; dataFile["34", "shortName"] = "smg.thompson"; dataFile["34", "value"] = 620; dataFile["34", "followItem"] = 785728077;
                    dataFile["35", "id"] = -41440462; dataFile["35", "shortName"] = "shotgun.spas12"; dataFile["35", "value"] = 640; dataFile["35", "followItem"] = -727717969;
                    dataFile["36", "id"] = 1318558775; dataFile["36", "shortName"] = "smg.mp5"; dataFile["36", "value"] = 660; dataFile["36", "followItem"] = 785728077;
                    dataFile["37", "id"] = 442886268; dataFile["37", "shortName"] = "rocket.launcher"; dataFile["37", "value"] = 680; dataFile["37", "followItem"] = 1;
                    dataFile["38", "id"] = -904863145; dataFile["38", "shortName"] = "rifle.semiauto"; dataFile["38", "value"] = 680; dataFile["38", "followItem"] = -1211166256;
                    dataFile["39", "id"] = 1588298435; dataFile["39", "shortName"] = "rifle.bolt"; dataFile["39", "value"] = 680; dataFile["39", "followItem"] = -1211166256;

                    dataFile["40", "id"] = -1812555177; dataFile["40", "shortName"] = "rifle.lr300"; dataFile["40", "value"] = 795; dataFile["40", "followItem"] = -1211166256;
                    dataFile["41", "id"] = 1545779598; dataFile["41", "shortName"] = "rifle.ak"; dataFile["41", "value"] = 795; dataFile["41", "followItem"] = -1211166256;
                    dataFile["42", "id"] = 28201841; dataFile["42", "shortName"] = "rifle.m39"; dataFile["42", "value"] = 810; dataFile["42", "followItem"] = -1211166256;
                    dataFile["43", "id"] = -1123473824; dataFile["43", "shortName"] = "multiplegrenadelauncher"; dataFile["43", "value"] = 830; dataFile["43", "followItem"] = 0;
                    dataFile["44", "id"] = -778367295; dataFile["44", "shortName"] = "rifle.l96"; dataFile["44", "value"] = 850; dataFile["44", "followItem"] = -1211166256;
                    dataFile["45", "id"] = -2069578888; dataFile["45", "shortName"] = "lmg.m249"; dataFile["45", "value"] = 880; dataFile["45", "followItem"] = -1211166256;
                }
                else if (file == "LayersOfRiskArmours")
                {
                    dataFile["1", "id"] = 223891266; dataFile["1", "shortName"] = "tshirt"; dataFile["1", "value"] = 8; dataFile["1", "followItem"] = -2072273936;
                    dataFile["2", "id"] = 1608640313; dataFile["2", "shortName"] = "shirt.tanktop"; dataFile["2", "value"] = 8; dataFile["2", "followItem"] = -2072273936;
                    dataFile["3", "id"] = -1695367501; dataFile["3", "shortName"] = "pants.shorts"; dataFile["3", "value"] = 8; dataFile["3", "followItem"] = -2072273936;
                    dataFile["4", "id"] = -2025184684; dataFile["4", "shortName"] = "shirt.collared"; dataFile["4", "value"] = 8; dataFile["4", "followItem"] = -2072273936;
                    dataFile["5", "id"] = 237239288; dataFile["5", "shortName"] = "pants"; dataFile["5", "value"] = 8; dataFile["5", "followItem"] = -2072273936;
                    dataFile["6", "id"] = 935692442; dataFile["6", "shortName"] = "tshirt.long"; dataFile["6", "value"] = 8; dataFile["6", "followItem"] = -2072273936;
                    dataFile["7", "id"] = 1366282552; dataFile["7", "shortName"] = "burlap.gloves"; dataFile["7", "value"] = 8; dataFile["7", "followItem"] = -2072273936;
                    dataFile["8", "id"] = -1000573653; dataFile["8", "shortName"] = "boots.frog"; dataFile["8", "value"] = 8; dataFile["8", "followItem"] = -2072273936;
                    dataFile["9", "id"] = 1992974553; dataFile["9", "shortName"] = "burlap.trousers"; dataFile["9", "value"] = 8; dataFile["9", "followItem"] = -2072273936;

                    dataFile["10", "id"] = -761829530; dataFile["10", "shortName"] = "burlap.shoes"; dataFile["10", "value"] = 8; dataFile["10", "followItem"] = -2072273936;
                    dataFile["11", "id"] = 602741290; dataFile["11", "shortName"] = "burlap.shirt"; dataFile["11", "value"] = 8; dataFile["11", "followItem"] = -2072273936;
                    dataFile["12", "id"] = 1877339384; dataFile["12", "shortName"] = "burlap.headwrap"; dataFile["12", "value"] = 8; dataFile["12", "followItem"] = -2072273936;
                    dataFile["13", "id"] = 21402876; dataFile["13", "shortName"] = "burlap.gloves.new"; dataFile["13", "value"] = 8; dataFile["13", "followItem"] = -2072273936;
                    dataFile["14", "id"] = -1549739227; dataFile["14", "shortName"] = "shoes.boots"; dataFile["14", "value"] = 10; dataFile["14", "followItem"] = -2072273936;
                    dataFile["15", "id"] = -23994173; dataFile["15", "shortName"] = "hat.boonie"; dataFile["15", "value"] = 10; dataFile["15", "followItem"] = -2072273936;
                    dataFile["16", "id"] = 1675639563; dataFile["16", "shortName"] = "hat.beenie"; dataFile["16", "value"] = 10; dataFile["16", "followItem"] = -2072273936;
                    dataFile["17", "id"] = -1022661119; dataFile["17", "shortName"] = "hat.cap"; dataFile["17", "value"] = 10; dataFile["17", "followItem"] = -2072273936;
                    dataFile["18", "id"] = 196700171; dataFile["18", "shortName"] = "attire.hide.vest"; dataFile["18", "value"] = 14; dataFile["18", "followItem"] = -2072273936;
                    dataFile["19", "id"] = -1773144852; dataFile["19", "shortName"] = "attire.hide.skirt"; dataFile["19", "value"] = 14; dataFile["19", "followItem"] = -2072273936;

                    dataFile["20", "id"] = 1722154847; dataFile["20", "shortName"] = "attire.hide.pants"; dataFile["20", "value"] = 14; dataFile["20", "followItem"] = -2072273936;
                    dataFile["21", "id"] = 3222790; dataFile["21", "shortName"] = "attire.hide.helterneck"; dataFile["21", "value"] = 14; dataFile["21", "followItem"] = -2072273936;
                    dataFile["22", "id"] = 794356786; dataFile["22", "shortName"] = "attire.hide.boots"; dataFile["22", "value"] = 14; dataFile["22", "followItem"] = -2072273936;
                    dataFile["23", "id"] = -702051347; dataFile["23", "shortName"] = "mask.bandana"; dataFile["23", "value"] = 15; dataFile["23", "followItem"] = -2072273936;
                    dataFile["24", "id"] = 850280505; dataFile["24", "shortName"] = "bucket.helmet"; dataFile["24", "value"] = 20; dataFile["24", "followItem"] = -2072273936;
                    dataFile["25", "id"] = -2012470695; dataFile["25", "shortName"] = "mask.balaclava"; dataFile["25", "value"] = 20; dataFile["25", "followItem"] = -2072273936;
                    dataFile["26", "id"] = -699558439; dataFile["26", "shortName"] = "roadsign.gloves"; dataFile["26", "value"] = 20; dataFile["26", "followItem"] = -2072273936;
                    dataFile["27", "id"] = 1751045826; dataFile["27", "shortName"] = "hoodie"; dataFile["27", "value"] = 20; dataFile["27", "followItem"] = -2072273936;
                    dataFile["28", "id"] = -2094954543; dataFile["28", "shortName"] = "wood.armor.helmet"; dataFile["28", "value"] = 25; dataFile["28", "followItem"] = -2072273936;
                    dataFile["29", "id"] = -1163532624; dataFile["29", "shortName"] = "jacket"; dataFile["29", "value"] = 25; dataFile["29", "followItem"] = -2072273936;

                    dataFile["30", "id"] = -48090175; dataFile["30", "shortName"] = "jacket.snow"; dataFile["30", "value"] = 30; dataFile["30", "followItem"] = -2072273936;
                    dataFile["31", "id"] = 418081930; dataFile["31", "shortName"] = "wood.armor.jacket"; dataFile["31", "value"] = 30; dataFile["31", "followItem"] = -2072273936;
                    dataFile["32", "id"] = 832133926; dataFile["32", "shortName"] = "wood.armor.pants"; dataFile["32", "value"] = 30; dataFile["32", "followItem"] = -2072273936;
                    dataFile["33", "id"] = 980333378; dataFile["33", "shortName"] = "attire.hide.poncho"; dataFile["33", "value"] = 30; dataFile["33", "followItem"] = -2072273936;
                    dataFile["34", "id"] = -1903165497; dataFile["34", "shortName"] = "deer.skull.mask"; dataFile["34", "value"] = 30; dataFile["34", "followItem"] = -2072273936;
                    dataFile["35", "id"] = 968019378; dataFile["35", "shortName"] = "clatter.helmet"; dataFile["35", "value"] = 40; dataFile["35", "followItem"] = 1079279582;
                    dataFile["36", "id"] = -2002277461; dataFile["36", "shortName"] = "roadsign.jacket"; dataFile["36", "value"] = 40; dataFile["36", "followItem"] = 1079279582;
                    dataFile["37", "id"] = 1850456855; dataFile["37", "shortName"] = "roadsign.kilt"; dataFile["37", "value"] = 40; dataFile["37", "followItem"] = 1079279582;
                    dataFile["38", "id"] = 1746956556; dataFile["38", "shortName"] = "bone.armor.suit"; dataFile["38", "value"] = 40; dataFile["38", "followItem"] = -2072273936;
                    dataFile["39", "id"] = -1478212975; dataFile["39", "shortName"] = "hat.wolf"; dataFile["39", "value"] = 50; dataFile["39", "followItem"] = 1079279582;

                    dataFile["40", "id"] = -803263829; dataFile["40", "shortName"] = "coffeecan.helmet"; dataFile["40", "value"] = 55; dataFile["40", "followItem"] = 1079279582;
                    dataFile["41", "id"] = -1108136649; dataFile["41", "shortName"] = "tactical.gloves"; dataFile["41", "value"] = 100; dataFile["41", "followItem"] = 1079279582;
                    dataFile["42", "id"] = 671063303; dataFile["42", "shortName"] = "riot.helmet"; dataFile["42", "value"] = 100; dataFile["42", "followItem"] = 1079279582;
                    dataFile["43", "id"] = 1266491000; dataFile["43", "shortName"] = "hazmatsuit"; dataFile["43", "value"] = 150; dataFile["43", "followItem"] = 1079279582;
                    dataFile["44", "id"] = 1110385766; dataFile["44", "shortName"] = "metal.plate.torso"; dataFile["44", "value"] = 150; dataFile["44", "followItem"] = 254522515;
                    dataFile["45", "id"] = -194953424; dataFile["45", "shortName"] = "metal.facemask"; dataFile["45", "value"] = 175; dataFile["45", "followItem"] = 254522515;
                    dataFile["46", "id"] = -1102429027; dataFile["46", "shortName"] = "heavy.plate.jacket"; dataFile["46", "value"] = 200; dataFile["46", "followItem"] = 254522515;
                    dataFile["47", "id"] = 1181207482; dataFile["47", "shortName"] = "heavy.plate.helmet"; dataFile["47", "value"] = 225; dataFile["47", "followItem"] = 254522515;
                    dataFile["48", "id"] = -1778159885; dataFile["48", "shortName"] = "heavy.plate.pants"; dataFile["48", "value"] = 250; dataFile["48", "followItem"] = 254522515;
                }
                else if (file == "LayersOfRiskTierData")
                {
                    dataFile["1", "tier"] = "E";dataFile["1", "min"] = 0;dataFile["1", "max"] = 199;
                    dataFile["2", "tier"] = "D";dataFile["2", "min"] = 200;dataFile["2", "max"] = 399;
                    dataFile["3", "tier"] = "C";dataFile["3", "min"] = 400;dataFile["3", "max"] = 599;
                    dataFile["4", "tier"] = "B";dataFile["4", "min"] = 600;dataFile["4", "max"] = 799;
                    dataFile["5", "tier"] = "A";dataFile["5", "min"] = 800;dataFile["5", "max"] = 99999;
                }
                else if(file == "LayersOfRiskHackableCratePositions")
                {
                    dataFile["1", "posX"] = 0;
                    dataFile["1", "posY"] = 0;
                    dataFile["1", "posZ"] = 0;
                }

                dataFile.Save();

                Puts("Data file " + file + " did not exists and was created with default values!");
            }

            return dataFile;
        }

        void dataInitialization()
        {
            int count = 0;

            //Tier data dictionary initialization
            for (int i = 1; i <= tiersDataFile.Count();i++)
            {
                tierDataDic.Add(i,
                new tierDataClass
                {
                    tierLetter = tiersDataFile[i.ToString(), "tier"].ToString(),
                    min = Int32.Parse(tiersDataFile[i.ToString(), "min"].ToString()),
                    max = Int32.Parse(tiersDataFile[i.ToString(), "max"].ToString()),
                });
                count++;
            }

            Puts("Added " + count + " Keys with values in tierDataDic!");

            //Weapon data dictionary initialization
            count = 0;
            for (int i = 1; i <= weaponsDataFile.Count(); i++)
            {
                weaponDataDic.Add(Int32.Parse(weaponsDataFile[i.ToString(), "id"].ToString()),
                new itemDataClass
                {
                    shortName = weaponsDataFile[i.ToString(), "shortName"].ToString(),
                    value = Int32.Parse(weaponsDataFile[i.ToString(), "value"].ToString()),
                    followItem = Int32.Parse(weaponsDataFile[i.ToString(), "followItem"].ToString()),
                });
                count++;
            }

            Puts("Added " + count + " Keys with values in weaponDataDic!");

            //Armour data dictionary initialization
            count = 0;
            for (int i = 1; i <= armoursDataFile.Count(); i++)
            {
                armourDataDic.Add(Int32.Parse(armoursDataFile[i.ToString(), "id"].ToString()),
                new itemDataClass
                {
                    shortName = armoursDataFile[i.ToString(), "shortName"].ToString(),
                    value = Int32.Parse(armoursDataFile[i.ToString(), "value"].ToString()),
                    followItem = Int32.Parse(armoursDataFile[i.ToString(), "followItem"].ToString()),
                });
                count++;
            }

            Puts("Added " + count + " Keys with values in armourDataDic!");

            //Hackable crate data dictionary initialization
            count = 0;
            for (int i = 1; i <= hackableCreatPositionsDataFile.Count(); i++)
            {
                float posX = float.Parse(hackableCreatPositionsDataFile[i.ToString(), "posX"].ToString());
                float posY = float.Parse(hackableCreatPositionsDataFile[i.ToString(), "posY"].ToString());
                float posZ = float.Parse(hackableCreatPositionsDataFile[i.ToString(), "posZ"].ToString());

                hackableCrateDataDic.Add(new Vector3(posX, posY, posZ),
                new hackableCrateDataClass
                {
                    inUse = false,
                });
                count++;
            }

            Puts("Added " + count + " Keys with values in hackableCreateDataDic!");
        }
        void lateInitializing()
        {
            //Check if lateInitializing has been run after plugin start.
            if (hasLateInitializedBeenRun == false)
            {
                //Delete all spheres in the world
                rust.RunServerCommand("del " + warZoneSphere);

                //Delete all hackable crates in the world
                rust.RunServerCommand("del " + hackableCreate);

                //Spawn world spheres
                if(enableZoneIndicators)
                {
                    for (int i = 0; i < zoneIndicatorStrength; i++)
                    {
                        spawnSpherePreFab((outerWall * 2) + 6, middlePosition);
                        spawnSpherePreFab((middleWall * 2) + 6, middlePosition);
                        spawnSpherePreFab((InnerWall * 2) + 3, middlePosition);
                    }
                }

                //Start controller for hackable crates
                hackableCrateSpawnTimer();

                hasLateInitializedBeenRun = true;
            }
        }

        BaseEntity spawnSpherePreFab(float size, Vector3 spawnPos)
        {
            //Sphere prefab
            string SphereEnt = warZoneSphere;

            BaseEntity sphere = GameManager.server.CreateEntity(SphereEnt, spawnPos, new Quaternion(), true);
            SphereEntity ent = sphere.GetComponent<SphereEntity>();
            ent.currentRadius = size;
            ent.lerpSpeed = 0f;

            sphere.Spawn();

            return sphere;
        }

        void hackableCrateSpawnTimer()
        {
            //Spawn multipule hackable crates one time at plugin start up.
            for(int i = 0; i < hackableCratesSpawedAtPluginStart; i++)
            {
                hackableCrateSpawnController();
            }

            //Create timer
            Timer controllTimer;

            controllTimer = timer.Every(timeBetweenHackableSpawnTries, () =>
            {
                hackableCrateSpawnController();
            });
        }

        void hackableCrateSpawnController()
        {
            //Get random position
            Random randNum = new Random();
            int totalhackableSpawnPositions = hackableCrateDataDic.Count();

            //Get random position
            int getRandomSpawn = randNum.Next(0, totalhackableSpawnPositions );

            //If random position is not in use and not maximum amount of hackable crates has spawned.
            if (hackableCrateDataDic.Values.ElementAt(getRandomSpawn) != null && hackableCrateDataDic.Values.ElementAt(getRandomSpawn).inUse == false && getHowManyHackableCratesSpawned() < maxHackableCrateSpawns)
            {
                //Get spawn position
                Vector3 spawnPos = hackableCrateDataDic.Keys.ElementAt(getRandomSpawn);

                //Spawn hackable crate
                spawnHackableCreate(spawnPos);
            }
        }

        void spawnHackableCreate(Vector3 spawnPos)
        {
            if(spawnPos != Vector3.zero)
            {
                //Get base entity
                BaseEntity crate = GameManager.server.CreateEntity(hackableCreate, spawnPos, new Quaternion(), true);

                //Spawn crate
                crate.Spawn();
            }
        }

        int getHowManyHackableCratesSpawned()
        {

            //How many hackable crates is spawned varaible
            int hackableCratesSpawned = 0;

            //Go through all items in hackableCrateDataDic
            foreach (var item in hackableCrateDataDic)
            {
                //Set position and direction for ray cast
                Ray ray = new Ray(item.Key + vectormultiplier, downDirection);

                BaseEntity entity = getEntityFromRayCastHit(ray);

                //If ray cast hit an entity
                if (entity != null)
                {
                    //If ray cast did hit a hackable crate
                    if (entity.PrefabName == hackableCreate)
                    {
                        //Hackable crate exists on that position
                        item.Value.inUse = true;

                        //Add 1
                        hackableCratesSpawned++;
                    }
                }
                else
                {
                    //Hackable crate does not exists on that position
                    item.Value.inUse = false;
                }
            }

            return hackableCratesSpawned;
        }

        //Update content in container. Only runs when a player starts to hack a hackablecrate.
        void updateHackableCrateContainer(BaseEntity crate)
        {
            //Get crates storage container
            StorageContainer crateContainer = crate.GetComponent<StorageContainer>();

            //Create new container
            ItemContainer newContainer = new ItemContainer();
            newContainer.ServerInitialize(null, 6);
            newContainer.GiveUID();

            //Set crates container invetory to new containers inventory
            crateContainer.inventory = newContainer;

            Random randNum = new Random();
            int randWeaponDicElement = 0;
            int randArmourDicElement = 0;
            int randHackTime = 0;
            int randAmmoAmount;
            int randMedicAmount;

            //Add item
            Item weaponItem;
            Item weaponItemExtra = null;
            Item armourItem;
            Item armourItemExtra = null;

            //Zone 1
            if (getCurrentZone(getDistanceFromPosition(crate.transform.position, middlePosition, true)) == 1)
            {
                randWeaponDicElement = randNum.Next(hackableCrateZone1WeaponStart, hackableCrateZone1WeaponEnd);
                randArmourDicElement = randNum.Next(hackableCrateZone1ArmourStart, hackableCrateZone1ArmourEnd);
                randHackTime = hackableCrateZone1Time;
            }
            //Zone 2
            else if(getCurrentZone(getDistanceFromPosition(crate.transform.position, middlePosition, true)) == 2)
            {
                randWeaponDicElement = randNum.Next(hackableCrateZone2WeaponStart, hackableCrateZone2WeaponEnd);
                randArmourDicElement = randNum.Next(hackableCrateZone2ArmourStart, hackableCrateZone2ArmourEnd);
                randHackTime = hackableCrateZone2Time;
            }
            //Zone 3
            else if (getCurrentZone(getDistanceFromPosition(crate.transform.position, middlePosition, true)) == 3)
            {
                randWeaponDicElement = randNum.Next(hackableCrateZone3WeaponStart, hackableCrateZone3WeaponEnd);
                randArmourDicElement = randNum.Next(hackableCrateZone3ArmourStart, hackableCrateZone3ArmourEnd);
                randHackTime = hackableCrateZone3Time;
            }
            //Zone 4
            else if (getCurrentZone(getDistanceFromPosition(crate.transform.position, middlePosition, true)) == 4)
            {
                randWeaponDicElement = randNum.Next(hackableCrateZone4WeaponStart, hackableCrateZone4WeaponEnd);
                randArmourDicElement = randNum.Next(hackableCrateZone4ArmourStart, hackableCrateZone4ArmourEnd);
                randHackTime = hackableCrateZone4Time;
            }

            //Get weapon items from dictionary
            weaponItem = ItemManager.CreateByItemID(weaponDataDic.ElementAt(randWeaponDicElement).Key);
            if (weaponDataDic.ElementAt(randWeaponDicElement).Value.followItem != 1 && weaponDataDic.ElementAt(randWeaponDicElement).Value.followItem != 0)
            {
                randAmmoAmount = randNum.Next(15, 40);
                weaponItemExtra = ItemManager.CreateByItemID(weaponDataDic.ElementAt(randWeaponDicElement).Value.followItem, randAmmoAmount);
            }

            //Get armour items from dictionary
            armourItem = ItemManager.CreateByItemID(armourDataDic.ElementAt(randArmourDicElement).Key);
            if (armourDataDic.ElementAt(randArmourDicElement).Value.followItem != 1 && armourDataDic.ElementAt(randArmourDicElement).Value.followItem != 0)
            {
                randMedicAmount = randNum.Next(1, 3);
                armourItemExtra = ItemManager.CreateByItemID(armourDataDic.ElementAt(randArmourDicElement).Value.followItem, randMedicAmount);
            }

            //Add items to crate container
            weaponItem.MoveToContainer(newContainer);
            armourItem.MoveToContainer(newContainer);

            if(weaponItemExtra != null)
            {
                weaponItemExtra.MoveToContainer(newContainer);
            }

            if (armourItemExtra != null)
            {
                armourItemExtra.MoveToContainer(newContainer);
            }

            //Get HackableLockedCrate
            HackableLockedCrate ent = crate.GetComponent<HackableLockedCrate>();

            //Set how many seconds before crate will be unlocked (minus the default value on 15 min).
            ent.hackSeconds = randHackTime;

            //Set how long warzones will be up on hackable crates position.
            int getWarZoneUpTime;
            getWarZoneUpTime = (hackableCrateDefaultHackTime - randHackTime) + hackAbleWarZoneExtraTime;

            //Start war zone
            startWarZone(crate.transform.position, warZoneSize, getWarZoneUpTime);
        }
        #endregion

        #region Hooks
        void OnPlayerSleepEnded(BasePlayer player)
        {
            //Spawn world spheres if not spawned
            lateInitializing();

            //Set player data and start player initializing
            setPlayerData(player);

            //Create Tier GUI
            createInfoBox(player, uiTierBox, "Tier " + tierDataDic[playerDataDic[player.userID].tierId].tierLetter, tierBoxPosX, tierBoxPosY, tierBoxSizeX, tierBoxSizeY);
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            //Get BasePlayers
            BasePlayer victim = entity.ToPlayer();
            BasePlayer attacker = info.InitiatorPlayer;

            //Is both attacker and victim awake and is player
            if (isPlayerAwake(victim) && isPlayerAwake(attacker))
            {
                //Check if attacker in same zone
                if (playerDataDic[attacker.userID].currentZone == playerDataDic[victim.userID].currentZone)
                {
                    //Check if attacker is not in warzone
                    if (playerDataDic[attacker.userID].currentZone < 5)
                    {
                        //Check if attacker is updating tier
                        if (playerDataDic[attacker.userID].currentUpdateCycle == 0)
                        {
                            //Check if victim is in teir range
                            if (canVictimTakeDamage(victim, attacker) == true)
                            {
                                info.damageTypes.ScaleAll(1);
                            }
                            else
                            {
                                string CantDamagePlayer = lang.GetMessage("CantDamagePlayer", this, attacker.UserIDString);
                                PrintToChat(attacker, string.Format(CantDamagePlayer, tierDataDic[playerDataDic[victim.userID].tierId].tierLetter));
                                info.damageTypes.ScaleAll(0);
                            }
                        }
                        else
                        {
                            PrintToChat(attacker, lang.GetMessage("CantDamageUpdatingTier", this, attacker.UserIDString));
                            info.damageTypes.ScaleAll(0);
                        }
                    }
                    else
                    {
                        //Always deal damage in warzones
                        info.damageTypes.ScaleAll(1);
                    }
                }
                else
                {
                    PrintToChat(attacker, lang.GetMessage("CantDamageInOtherZones", this, attacker.UserIDString));
                    info.damageTypes.ScaleAll(0);
                }
            }
            else
            {
                //If attacked entity has a density of 0.5 or lower the attacker will always do full damage.
                bool ignoreAttackedEntity = entity.baseProtection.density <= 0.5f;

                //Is attacker shooting on player made buildings
                if(isPlayerAwake(attacker) && entity.OwnerID != 0 && ignoreAttackedEntity == false)
                {
                    //Attacker is not inside raid zone.
                    if (playerDataDic[attacker.userID].currentZone != 5)
                    {
                        //Attacker damaging buildings not owned by the attacker
                        if (victim == null && entity.OwnerID != attacker.userID)
                        {
                            //Attacker cant damage other players buildings
                            if(playerDataDic[attacker.userID].cantStartRaidZoneTimer > 0)
                            {
                                string CantDamageBuldings = lang.GetMessage("CantDamageBuldings", this, attacker.UserIDString);
                                PrintToChat(attacker, string.Format(CantDamageBuldings, timeFormating((int)playerDataDic[attacker.userID].cantStartRaidZoneTimer)));
                                info.damageTypes.ScaleAll(0);
                            }
                            else
                            {
                                //Update startRaidZone variable, if its over a specific value a raid zone will spawn.
                                playerDataDic[attacker.userID].startRaidZoneAfterDamageDealtToStructure -= info.damageTypes.Total();

                                string StartWarZoneIfDamageDealt = lang.GetMessage("StartWarZoneIfDamageDealt", this, attacker.UserIDString);
                                PrintToChat(attacker, string.Format(StartWarZoneIfDamageDealt, Math.Round(playerDataDic[attacker.userID].startRaidZoneAfterDamageDealtToStructure)));

                                info.damageTypes.ScaleAll(1);

                                //Spawn and start raid zone
                                if (playerDataDic[attacker.userID].startRaidZoneAfterDamageDealtToStructure < 0)
                                {
                                    //Set to default value
                                    playerDataDic[attacker.userID].startRaidZoneAfterDamageDealtToStructure = startWarZoneAfterDamageToStructure;

                                    //If in zone 1
                                    playerDataDic[attacker.userID].cantStartRaidZoneTimer = playerWarZone1Delay;

                                    //If in zone 2 to 4
                                    if (playerDataDic[attacker.userID].currentZone == 2)
                                    {
                                        playerDataDic[attacker.userID].cantStartRaidZoneTimer = playerWarZone2Delay;
                                    }
                                    else if(playerDataDic[attacker.userID].currentZone == 3)
                                    {
                                        playerDataDic[attacker.userID].cantStartRaidZoneTimer = playerWarZone3Delay;
                                    }
                                    else if(playerDataDic[attacker.userID].currentZone == 4)
                                    {
                                        playerDataDic[attacker.userID].cantStartRaidZoneTimer = playerWarZone4Delay;
                                    }

                                    startWarZone(entity.transform.position, warZoneSize, warZoneRaidAliveTimer);
                                }
                            }
                        }
                        //Destroying own buildings
                        else
                        {
                            info.damageTypes.ScaleAll(1);
                        }
                    }
                    else
                    {
                        info.damageTypes.ScaleAll(1);
                    }
                }
                else
                {
                    info.damageTypes.ScaleAll(1);
                }
            }
        }

        object OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            //Upgrade materials
            bool isWood = grade.ToString() == "Wood";
            bool isStone = grade.ToString() == "Stone";
            bool isMetal = grade.ToString() == "Metal";

            if (playerDataDic[player.userID].currentZone == 1)
            {
                if(isWood)
                {
                    return null;
                }
                else
                {
                    PrintToChat(player, lang.GetMessage("OnlyWood", this, player.UserIDString));
                    return 1;
                }
            }
            else if (playerDataDic[player.userID].currentZone == 2)
            {
                if (isWood || isStone)
                {
                    return null;
                }
                else
                {
                    PrintToChat(player, lang.GetMessage("OnlyWoodStone", this, player.UserIDString));
                    return 1;
                }
            }
            else if(playerDataDic[player.userID].currentZone == 3)
            {
                if (isWood || isStone || isMetal)
                {
                    return null;
                }
                else
                {
                    PrintToChat(player, lang.GetMessage("OnlyWoodStoneMetal", this, player.UserIDString));
                    return 1;
                }
            }
            else
            {
                return null;
            }
        }

        object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if(canPlayerBuild(planner, prefab) == true)
            {
                return null;
            }
            else
            {
                return 1;
            }
        }

        void OnCrateHack(HackableLockedCrate crate)
        {
            //Update crate container
            updateHackableCrateContainer(crate);
        }
        #endregion

        #region Player data
        void setPlayerData(BasePlayer player)
        {
            //Player steam id key
            bool doPlayerDataExist = playerDataDic.ContainsKey(player.userID);

            //Get tier points for player
            int getCurrentTier = getTierPointsForPlayer(player);

            if (doPlayerDataExist)
            {
                playerDataDic[player.userID].isUpdateTierTimerRunning = false;
                playerDataDic[player.userID].isPlayerInitialized = false;
                playerDataDic[player.userID].currentTier = getCurrentTier;
                playerDataDic[player.userID].nextTier = getCurrentTier;
                playerDataDic[player.userID].oldTier = getCurrentTier;
                playerDataDic[player.userID].distanceFromMiddle = getDistanceFromPosition(player.GetNetworkPosition(), middlePosition, true);
                //playerDataDic[player.userID].raidZoneTimer = 0;
                playerDataDic[player.userID].currentZone = getCurrentZone(getDistanceFromPosition(player.GetNetworkPosition(), middlePosition, true));
                //playerDataDic[player.userID].startRaidZoneAfterDamageDEalt = 0;
                playerDataDic[player.userID].currentUpdateCycle = 0;
                playerDataDic[player.userID].tierId = getTierId(getCurrentTier);
                playerDataDic[player.userID].lookPlayerDelay = 0;
            }
            else
            {
                //Set player data
                playerDataDic.Add(player.userID,
                new playerDataClass
                {
                    isUpdateTierTimerRunning = false,
                    isPlayerInitialized = false,
                    currentTier = getCurrentTier,
                    nextTier = getCurrentTier,
                    oldTier = getCurrentTier,
                    distanceFromMiddle = getDistanceFromPosition(player.GetNetworkPosition(), middlePosition, true),
                    cantStartRaidZoneTimer = 0,
                    currentZone = getCurrentZone(getDistanceFromPosition(player.GetNetworkPosition(), middlePosition, true)),
                    startRaidZoneAfterDamageDealtToStructure = startWarZoneAfterDamageToStructure,
                    currentUpdateCycle = 0,
                    tierId = getTierId(getCurrentTier),
                    lookPlayerDelay = 0
                });
            }

            //Start player initializing
            playerInitializeTimer(player);
        }

        //Return current player tier.
        int getTierPointsForPlayer(BasePlayer player)
        {
            int weaponPoints;
            int armourPoints;

            //Item conteiners
            ItemContainer containerBelt = player.inventory.containerBelt;
            ItemContainer containerWear = player.inventory.containerWear;
            ItemContainer containerMain = player.inventory.containerMain;

            //Loop through containerBelt and get highest valued weapon
            weaponPoints = getHighestWeaponValueFromContainer(containerBelt);

            //If containerMain has any item with higher value then containerBelt
            if(getHighestWeaponValueFromContainer(containerMain) > weaponPoints)
            {
                weaponPoints = getHighestWeaponValueFromContainer(containerMain);
            }

            //Loop through all containers and get the 5 highest valued items.
            armourPoints = getArmourItemPointsFromContainer(containerBelt, containerWear, containerMain, 5);

            return weaponPoints + armourPoints;
        }

        int getArmourItemPointsFromContainer(ItemContainer containerBelt, ItemContainer containerWear, ItemContainer containerMain, int maxItemsCounted)
        {
            //7 Best armour item values
            int countItemsInList = 0;
            int points = 0;

            ItemContainer[] containers = new ItemContainer[3];

            containers[0] = containerWear;
            containers[1] = containerMain;
            containers[2] = containerBelt;

            //Get items from highest to lowest
            foreach (var item in armourDataDic.OrderByDescending(key => key.Value.value))
            {
                //Loop through all 3 containers
                for (int i = 0; i < 3; i++)
                {
                    //Loop through one container
                    for (int y = 0; y < containers[i].itemList.Count; y++)
                    {
                        //Get item id
                        int getItemId = containers[i].itemList[y].info.itemid;
                        string getItemShortName = containers[i].itemList[y].info.shortname;

                        //Is item id same as item in armourDataDic.
                        if (getItemId == item.Key)
                        {
                            points += item.Value.value;
                            countItemsInList++;
                        }

                        //Exit after 7 items
                        if (countItemsInList >= maxItemsCounted)
                        {
                            return points;
                        }
                    }
                }
            }

            return points;
        }

        int getHighestWeaponValueFromContainer(ItemContainer container)
        {
            int points = 0;

            //Loop trough container
            for (int i = 0; i < container.itemList.Count; i++)
            {
                //Get item id
                int getItemId = container.itemList[i].info.itemid;

                //Check if item exists in weaponDataDic
                if (weaponDataDic.ContainsKey(getItemId))
                {
                    //Get weapon value
                    int getItemValue = weaponDataDic[getItemId].value;

                    if (getItemValue > points)
                    {
                        points = getItemValue;
                    }
                }
            }

            return points;
        }

        int getTierId(int value)
        {
            int tierId = 0;

            foreach (var item in tierDataDic)
            {
                //Check item point range
                if(value >= item.Value.min && value <= item.Value.max)
                {
                    tierId = item.Key;

                    break;
                }
            }

            return tierId;
        }

        //Used by playerTierTimer
        void updatePlayer(BasePlayer player, int nextTier, bool setTierData)
        {
            updatePlayerData(player, nextTier, setTierData);

            updatePlayerGui(player, nextTier, setTierData);
        }

        void updatePlayerData(BasePlayer player, int nextTier, bool setTierData)
        {
            if (setTierData == true)
            {
                //Update platyer old tier value
                playerDataDic[player.userID].oldTier = playerDataDic[player.userID].currentTier;

                //Updater player currentTier
                playerDataDic[player.userID].currentTier = nextTier;
                playerDataDic[player.userID].tierId = getTierId(nextTier);

                //Reset player update cycles
                playerDataDic[player.userID].currentUpdateCycle = 0;
            }

            //Update player distanceToMiddle
            playerDataDic[player.userID].distanceFromMiddle = getDistanceFromPosition(player.GetNetworkPosition(), middlePosition, true);

            ////////Update player zone (start)
            bool isInsideWarZone = false;
            foreach (var item in warZoneDataDic)
            {
                if(isPlayerInsideRaidZone(player, item.Value.position, item.Value.size / 2))
                {
                    //If raidZone has been alive more then x seconds, players health is over 25 and players old zone was not a raid zone.
                    if(item.Value.currentUpTime > timeBeforePlayerTakeDamageWhenEnterWarZone && (player.health > warZoneEnterDamage + 5 || canDieWhenEnteringWarZone == true) && playerDataDic[player.userID].currentZone != 5) 
                    {
                        player.Heal(-warZoneEnterDamage);

                        string vomited = lang.GetMessage("Vomited", this, player.UserIDString);
                        PrintToChat(player, string.Format(vomited, warZoneEnterDamage));

                        Effect.server.Run(vomit, player.transform.position);
                    }

                    playerDataDic[player.userID].currentZone = 5;
                    isInsideWarZone = true;
                    break;
                }
            }
            
            //If not in raid zone set regular zone.
            if(isInsideWarZone == false)
            {
                playerDataDic[player.userID].currentZone = getCurrentZone(playerDataDic[player.userID].distanceFromMiddle);
            }
            ////////Update player zone (end)

            //Update player cantStartRaidZoneTimer
            if (playerDataDic[player.userID].cantStartRaidZoneTimer > 0)
            {
                playerDataDic[player.userID].cantStartRaidZoneTimer -= updateTierRefreshRate;
            }

            //Update startRaidZoneAfterDamageDealtToStructure
            if (playerDataDic[player.userID].startRaidZoneAfterDamageDealtToStructure > 0 && playerDataDic[player.userID].startRaidZoneAfterDamageDealtToStructure <= startWarZoneAfterDamageToStructure)
            {
                playerDataDic[player.userID].startRaidZoneAfterDamageDealtToStructure += playerWarZoneStartDecreser;
            }
        }

        void updatePlayerGui(BasePlayer player, int nextTier, bool setTierData)
        {
            if (setTierData == true)
            {
                //Update tier box gui
                createInfoBox(player, uiTierBox, "Tier " + tierDataDic[playerDataDic[player.userID].tierId].tierLetter, tierBoxPosX, tierBoxPosY, tierBoxSizeX, tierBoxSizeY);

                //Update tier points changed ui
                if (playerDataDic[player.userID].currentTier != playerDataDic[player.userID].oldTier)
                {
                    //Get chnage in tier points
                    float getTierPointsChanged = playerDataDic[player.userID].currentTier - playerDataDic[player.userID].oldTier;

                    string positive = "";

                    if(getTierPointsChanged > 0)
                    {
                        positive = "+";
                    }

                    //Update UI
                    createInfoText(player, uiTierPointsChange, positive + getTierPointsChanged.ToString(), getTierPointsChangePosX, getTierPointsChangePosY, getTierPointsChangeSizeX, getTierPointsChangeSizeY);
                    
                    //Stop updating the ui.
                    playerDataDic[player.userID].oldTier = playerDataDic[player.userID].currentTier;

                    //Set delay to 0 becouse an update happened.
                    tierPointChangeDelay = 0;
                }
                else
                {
                    //Update delay. After a set amount this delay will destroy tier points changed.
                    tierPointChangeDelay += updateTierRefreshRate;

                    if (tierPointChangeDelay > 4)
                    {
                        //If ui element exists destroy it.
                        destroyGuiElement(player, uiTierPointsChange);
                    }
                }

                ////////Loading ui start
                //Destroy loading gui element
                destroyGuiElement(player, uiTierLoading);

                //Tier point text
                string tierPointsText = nextTier.ToString() + " / " + tierDataDic[playerDataDic[player.userID].tierId].max.ToString();

                //Tier point loading bar
                float lowValue = nextTier - tierDataDic[playerDataDic[player.userID].tierId].min;
                float highValue = tierDataDic[playerDataDic[player.userID].tierId].max - tierDataDic[playerDataDic[player.userID].tierId].min;
                float loadingBar = lowValue / highValue;

                //Create loading gui element
                //Set progress bar to max if player is A tier.
                if (playerDataDic[player.userID].tierId == tierDataDic.Count())
                {
                    createTierProgressBar(player, uiTierLoading, playerDataDic[player.userID].currentTier.ToString(), 1.0f, tierProgressPosX, tierProgressPosY, tierProgressSizeX, tierProgressSizeY);
                }
                else
                {
                    createTierProgressBar(player, uiTierLoading, tierPointsText, loadingBar, tierProgressPosX, tierProgressPosY, tierProgressSizeX, tierProgressSizeY);
                }
                ////////Loading ui end
            }

            ////////Update lookPlayer ui
            /////////////////////////////////////////////////
            BaseEntity entity = getEntityFromRayCastHit(player.eyes.HeadRay());

            //Entity is not null
            if (entity != null)
            {
                //Get player from entity
                BasePlayer lookPlayer = entity.ToPlayer();

                //Check if entity is player and if player is alive and mobile
                if (isPlayerAliveAndMobile(lookPlayer))
                {
                    playerDataDic[player.userID].lookPlayerDelay = 0;
                    displayTierFromLookPlayer(player, lookPlayer);
                }
            }

            playerDataDic[player.userID].lookPlayerDelay += updateTierRefreshRate;

            if (playerDataDic[player.userID].lookPlayerDelay > TimeLookPlayerUiIsDrawn)
            {
                destroyGuiElement(player, uiGetPlayerTierOnLook);
            }
            /////////////////////////////////////////////////

            //Update tier range box gui
            createTearRangeBox(player, uiTierRangeBox, checkTierRange(player, 5), checkTierRange(player, 4), checkTierRange(player, 3), checkTierRange(player, 2), checkTierRange(player, 1));

            //Update zone box gui
            createInfoBox(player, uiZoneBox, "Zone " + playerDataDic[player.userID].currentZone, zoneBoxPosX, zoneBoxPosY, zoneBoxSizeX, zoneBoxSizeY);

            //Update distance to next zone gui
            createInfoBox(player, uiMeterZoneBox, getDistanceToNextZone(playerDataDic[player.userID].distanceFromMiddle).ToString(), meterZoneBoxPosX, meterZoneBoxPosY, meterZoneBoxSizeX, meterZoneBoxSizeY);
        }

        bool isPlayerAwake(BasePlayer player)
        {
            if (player != null && player.IsConnected == true && player.IsDead() == false && player.IsSleeping() == false && player.IsSpectating() == false)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        bool isPlayerAliveAndMobile(BasePlayer player)
        {
            if (player != null && player.IsConnected == true && player.IsDead() == false && player.IsWounded() == false && player.IsSleeping() == false && player.IsSpectating() == false)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        int getCurrentZone(float distance)
        {
            int currentZone;

            if (distance > outerWall)
            {
                currentZone = 1;
            }
            else if(distance > middleWall)
            {
                currentZone = 2;
            }
            else if(distance > InnerWall)
            {
                currentZone = 3;
            }
            else
            {
                currentZone = 4;
            }

            return currentZone;
        }

        int getDistanceToNextZone(float distanceToMiddle)
        {
            int distanceToNextZone;

            if (distanceToMiddle > outerWall)
            {
                distanceToNextZone = Convert.ToInt32(distanceToMiddle) - outerWall;
            }
            else if (distanceToMiddle > middleWall)
            {
                distanceToNextZone = Convert.ToInt32(distanceToMiddle) - middleWall;
            }
            else if (distanceToMiddle > InnerWall)
            {
                distanceToNextZone = Convert.ToInt32(distanceToMiddle) - InnerWall;
            }
            else
            {
                distanceToNextZone = Convert.ToInt32(distanceToMiddle);
            }

            return distanceToNextZone;
        }

        float getDistanceFromPosition(Vector3 pos, Vector3 pos2, bool ignoreY)
        {
            float dist;

            if (ignoreY == true)
            {
                Vector3 posNoY = new Vector3(pos.x, 0, pos.z);
                Vector3 pos2NoY = new Vector3(pos2.x, 0, pos2.z);
                dist = Vector3.Distance(posNoY, pos2NoY);
            }
            else
            {
                dist = Vector3.Distance(pos, pos2);
            }

            return dist;
        }

        bool canVictimTakeDamage(BasePlayer victim, BasePlayer attacker)
        {
            //Victim tier id
            int getVictimTierId = playerDataDic[victim.userID].tierId;
            //Victim current zone (1,2,3,4)
            int getVictimZone = playerDataDic[victim.userID].currentZone;
            //Attacker tier id
            int getAttackerTierId = playerDataDic[attacker.userID].tierId;

            bool isVictimUpdatingTier = playerDataDic[victim.userID].currentUpdateCycle > 0;
            bool isAttackerUpdatingTier = playerDataDic[attacker.userID].currentUpdateCycle > 0;

            bool canTakeDamage = false;
            int tierRange;

            //Attacker cant deal damage while updating tier
            if(isAttackerUpdatingTier == false)
            {
                //Get tier range
                if (getVictimZone == 1 && isVictimUpdatingTier == false)
                {
                    tierRange = 0;
                }
                else if (getVictimZone == 2 && isVictimUpdatingTier == false)
                {
                    tierRange = 1;
                }
                else if (getVictimZone == 3 && isVictimUpdatingTier == false)
                {
                    tierRange = 2;
                }
                else if (getVictimZone == 4 && isVictimUpdatingTier == false)
                {
                    tierRange = 3;
                }
                else
                {
                    tierRange = 4;
                }

                bool range1 = getAttackerTierId <= getVictimTierId + tierRange;
                bool range2 = getAttackerTierId >= getVictimTierId - tierRange;

                //Check if victim is in zone range and can take damage
                if (range1 && range2)
                {
                    canTakeDamage = true;
                }
            }

            return canTakeDamage;
        }

        int checkTierRange(BasePlayer player, int tierId)
        {
            //Victim tier id
            int getPlayerTierId = playerDataDic[player.userID].tierId;
            //Victim current zone (1,2,3,4,5)
            int getPlayerZone = playerDataDic[player.userID].currentZone;

            bool isPlayerUpdatingTier = playerDataDic[player.userID].currentUpdateCycle > 0;

            int canAttackTier = 0;
            int tierRange;

            //Get tier range
            if (getPlayerZone == 1 && isPlayerUpdatingTier == false)
            {
                tierRange = 0;
            }
            else if (getPlayerZone == 2 && isPlayerUpdatingTier == false)
            {
                tierRange = 1;
            }
            else if (getPlayerZone == 3 && isPlayerUpdatingTier == false)
            {
                tierRange = 2;
            }
            else if (getPlayerZone == 4 && isPlayerUpdatingTier == false)
            {
                tierRange = 3;
            }
            else
            {
                tierRange = 4;
            }

            bool range1 = tierId <= getPlayerTierId + tierRange;
            bool range2 = tierId >= getPlayerTierId - tierRange;

            //Check if victim is in zone range and can take damage
            if (range1 && range2)
            {
                canAttackTier = 1;
            }

            return canAttackTier;
        }

        bool isPlayerInsideRaidZone(BasePlayer player, Vector3 position, float raidZoneSize)
        {
            if(getDistanceFromPosition(player.GetNetworkPosition(), position, false) < raidZoneSize)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        void startWarZone(Vector3 spawnPos, float size, int timeAlive)
        {
            //Update total war zones variable
            totalWarZones++;

            //War zone variables
            int counter = 0;
            int counterStop = 8;
            int warZoneSizeDecreser = 0;
            int warZoneId = totalWarZones;
            List<BaseEntity> sphereList = new List<BaseEntity>(24);

            //Add raid zone to dictionary
            warZoneDataDic.Add(warZoneId,
            new warZoneDataClass
            {
                zone = getCurrentZone(getDistanceFromPosition(spawnPos, middlePosition, true)),
                size = size,
                currentUpTime = 0,
                aliveTime = timeAlive,
                position = spawnPos,
            });

            //Timer
            Timer sphereWarZoneTimer;

            //Start timer
            sphereWarZoneTimer = timer.Repeat(0.2f, 3, () =>
            {
                //Spawn 8 spheres
                while (counter < counterStop)
                {
                    sphereList.Add(spawnSpherePreFab(warZoneDataDic[warZoneId].size - warZoneSizeDecreser, spawnPos));
                    counter++;
                }

                counterStop += 8;
                warZoneSizeDecreser += 1;
            });

            //Timer
            Timer warZoneUpdateTimer;

            //Timer start
            warZoneUpdateTimer = timer.Every(warZoneTimeBetweenIntervalls, () =>
            {
                bool shrinkSpheres = true;
                warZoneDataDic[warZoneId].currentUpTime += warZoneTimeBetweenIntervalls;

                if (warZoneDataDic[warZoneId].currentUpTime >= warZoneDataDic[warZoneId].aliveTime)
                {
                    for (int i = 0; i < sphereList.Count(); i++)
                    {
                        sphereList[i].Kill();
                    }

                    //Remove raid zone from dictionary
                    warZoneDataDic.Remove(warZoneId);

                    //Destroy timer
                    timerHolderDic[warZoneId].Destroy();

                    //Remove timer from dictionary
                    timerHolderDic.Remove(warZoneId);
                }
                else if (warZoneDataDic[warZoneId].currentUpTime >= warZoneDataDic[warZoneId].aliveTime - 3 && shrinkSpheres == true)
                {
                    //Timer
                    Timer shrinkSpheresTimer;
                    counter = 0;
                    counterStop = 8;
                    shrinkSpheres = false;

                    //Start timer
                    shrinkSpheresTimer = timer.Repeat(0.2f, 3, () =>
                    {
                        //Spawn 8 spheres
                        while (counter < counterStop)
                        {
                            SphereEntity ent = sphereList[counter].GetComponent<SphereEntity>();
                            ent.currentRadius = 1;
                            counter++;
                        }

                        counterStop += 8;
                    });
                }
            });

            //Set player data
            timerHolderDic.Add(warZoneId, warZoneUpdateTimer);
        }

        BaseEntity getEntityFromRayCastHit(Ray ray)
        {
            //var layers = LayerMask.GetMask("Construction", "Default", "Deployed", "Resource", "Terrain", "Water", "World");

            RaycastHit hit;

            if (UnityEngine.Physics.Raycast(ray, out hit, float.MaxValue))
            {
                BaseEntity entity = hit.GetEntity();

                return entity;
            }

            return null;
        }

        void displayTierFromLookPlayer(BasePlayer player, BasePlayer lookPlayer)
        {
            string color = "1.0 1.0 1.0 1.0";
            string text = lookPlayer.displayName + " - tier " + tierDataDic[playerDataDic[lookPlayer.userID].tierId].tierLetter;

            if(canVictimTakeDamage(lookPlayer, player))
            {
                color = "1.0 0.3 0.3 1.0";
            }

            if(playerDataDic[lookPlayer.userID].currentUpdateCycle > 0)
            {
                text = lookPlayer.displayName + " - " + lang.GetMessage("UpdatingTier", this, player.UserIDString) + " tier!";
            }

            getPlayerTierOnLookTextBox(player, uiGetPlayerTierOnLook, text, color, getTierOnLookPosX, getTierOnLookPosY, getTierOnLookSizeX, getTierOnLookSizeY);
        }

        bool canPlayerBuild(Planner planner, Construction prefab)
        {
            bool canBuild = true;

            //Get player
            BasePlayer player = planner.GetOwnerPlayer();

            //Prefab is a door
            bool ifDoor = prefab.fullName.Contains("door") == true && prefab.fullName.Contains("doorway") == false;

            //Prefab is wooden door
            bool isWoodenDoor = prefab.fullName.Contains("wood") == true;

            //Prefab is metal door
            bool isMetalDoor = prefab.fullName.Contains("metal") == true;

            //Is prefab door
            if (ifDoor)
            {
                if (playerDataDic[player.userID].currentZone == 1)
                {
                    if (isWoodenDoor == false)
                    {
                        PrintToChat(player, lang.GetMessage("OnlyWoodenDoors", this, player.UserIDString));
                        canBuild = false;
                    }
                }
                else if (playerDataDic[player.userID].currentZone == 2)
                {
                    if (isWoodenDoor == false && isMetalDoor == false)
                    {
                        PrintToChat(player, lang.GetMessage("OnlyWoodenMetalDoors", this, player.UserIDString));
                         canBuild = false;
                    }
                }
            }
            else
            {
                float playerDistanceToMid = getDistanceFromPosition(player.GetNetworkPosition(), middlePosition, true);

                bool toCloseToOuterWall = playerDistanceToMid > outerWall - distanceToBuildFromWalls && playerDistanceToMid < outerWall + distanceToBuildFromWalls;
                bool toCloseToMiddleWall = playerDistanceToMid > middleWall - distanceToBuildFromWalls && playerDistanceToMid < middleWall + distanceToBuildFromWalls;
                bool toCloseToInnerWall = playerDistanceToMid > InnerWall - distanceToBuildFromWalls && playerDistanceToMid < InnerWall + distanceToBuildFromWalls;

                if (toCloseToOuterWall || toCloseToMiddleWall || toCloseToInnerWall)
                {
                    PrintToChat(player, lang.GetMessage("CantBuildCloseToEdge", this, player.UserIDString));
                     canBuild = false;
                }
            }

            return canBuild;
        }

        string timeFormating(int time)
        {
            string timeFormatWithText;

            if(time > 60)
            {
                timeFormatWithText = (time / 60) + " min";
            }
            else
            {
                timeFormatWithText = time + " sec";
            }

            return timeFormatWithText;
        }

        Vector3 getRayCastHitPosition(Ray ray)
        {
            //var layers = LayerMask.GetMask("Construction", "Default", "Deployed", "Resource", "Terrain", "Water", "World");

            RaycastHit hit;

            if (UnityEngine.Physics.Raycast(ray, out hit, float.MaxValue))
            {
                return hit.point;
            }
            else
            {
                return ray.origin;
            }
        }
        #endregion

        #region Timers
        void playerInitializeTimer(BasePlayer player)
        {
            Timer initializeTimer;

            initializeTimer = timer.Once(initializeTimerDelay, () =>
            {
                //Check if playerTierTimer is running or not. If not running start it.
                if (playerDataDic[player.userID].isUpdateTierTimerRunning == false)
                {
                    playerTierTimer(player);

                    Puts(player.displayName + " is now initilized! Needed to start playerTierTimer.");
                }
                else
                {
                    Puts(player.displayName + " is now initilized! Did not start playerTierTimer.");
                }

                //Player has been initialized
                playerDataDic[player.userID].isPlayerInitialized = true;
            });
        }

        void playerTierTimer(BasePlayer player)
        {
            //Timer
            Timer tierTimer;

            ////////Timer start
            //////////////////////////////////////////////////////////////
            tierTimer = timer.Every(updateTierRefreshRate, () =>
            {
                //Check if player is online and not dead.
                if (isPlayerAliveAndMobile(player))
                {
                    //Check if player has gone throw the initialize process.
                    //This is needed if the player reconect. The timer seems to not destroy itself on player leave and will be reused when the player connects again insted of using 2 or more timers.
                    if (playerDataDic[player.userID].isPlayerInitialized == true)
                    {
                        int nextTier = getTierPointsForPlayer(player);
                        int currentTier = playerDataDic[player.userID].currentTier;

                        //Check if tier points has changed
                        if (nextTier != currentTier)
                        {
                            //Check if tier rank has changed
                            if (getTierId(nextTier) != getTierId(currentTier))
                            {
                                //If set amount of cycles are reached.
                                if (playerDataDic[player.userID].currentUpdateCycle >= (updateTierTime * 5))
                                {
                                    updatePlayer(player, nextTier, true);
                                }
                                else
                                {
                                    if(playerDataDic[player.userID].currentUpdateCycle == 0 && playerDataDic[player.userID].currentZone < 5)
                                    {
                                        PrintToChat(player, lang.GetMessage("NoDamageWhileUpdatingTier", this, player.UserIDString));
                                    }

                                    //Update player currentUpdateCycle
                                    playerDataDic[player.userID].currentUpdateCycle++;

                                    //Update player data but do not reset currentUpdateCycle
                                    updatePlayer(player, nextTier, false);

                                    //Update loading bar value
                                    float loadingTierValue = playerDataDic[player.userID].currentUpdateCycle / (updateTierTime * 5);

                                    //Update loading ui
                                    createTierProgressBar(player, uiTierLoading, lang.GetMessage("ChangingTier", this, player.UserIDString) + " tier...", loadingTierValue, tierProgressPosX, tierProgressPosY, tierProgressSizeX, tierProgressSizeY);
                                }
                            }
                            else
                            {
                                updatePlayer(player, nextTier, true);
                            }
                        }
                        else
                        {
                            updatePlayer(player, nextTier, true);
                        }
                    }
                    else
                    {
                        updatePlayer(player, playerDataDic[player.userID].currentTier, true);

                        //Check if the timer is running
                        playerDataDic[player.userID].isUpdateTierTimerRunning = true;
                    }
                }
                else
                {
                    destroyAllUiForPlayer(player);
                }
                //////////////////////////////////////////////////////////////
                ////////Timer end
            });
        }
        #endregion

        #region Player ui
        void destroyGuiElement(BasePlayer player, string element)
        {
            if (player != null && element != null)
            {
                CuiHelper.DestroyUi(player, element);
            }
        }

        void destroyAllUiForPlayer(BasePlayer player)
        {
            destroyGuiElement(player, uiZoneBox);
            destroyGuiElement(player, uiMeterZoneBox);
            destroyGuiElement(player, uiTierLoading);
            destroyGuiElement(player, uiTierPoints);
            destroyGuiElement(player, uiTierRangeBox);
            destroyGuiElement(player, uiInfoText);
            destroyGuiElement(player, uiTierBox);
            destroyGuiElement(player, uiGetPlayerTierOnLook);
            destroyGuiElement(player, uiTierPointsChange);
        }

        void createInfoBox(BasePlayer player, string uiElement, string text, float posX, float posY, float sizeX, float sizeY)
        {
            int fontSize = 16;
            if (text == "Zone 5")
            {
                text = "Warzone";
                fontSize = 16;
            }

            destroyGuiElement(player, uiElement);
            var Container = new CuiElementContainer();

            float heigth = posX + sizeX;
            float width = posY + sizeY;

            Container.Add(new CuiElement
            {
                Name = uiElement,
                Components =
                {
                    new CuiImageComponent 
                    {
                        Color = "0.2 0.2 0.2 0.7"
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = posX.ToString() + " " + posY.ToString(), AnchorMax = heigth.ToString() + " " + width.ToString(),
                    },
                }
            });
            Container.Add(new CuiElement
            {
                Parent = uiElement,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = text,
                        FontSize = fontSize,
                        Align = TextAnchor.MiddleCenter
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.0 0.0", AnchorMax = "1.0 0.9",
                    },
                }
            });

            CuiHelper.AddUi(player, Container);
        }

        void createInfoText(BasePlayer player, string uiElement, string text, float posX, float posY, float sizeX, float sizeY)
        {
            destroyGuiElement(player, uiElement);
            var Container = new CuiElementContainer();

            float heigth = posX + sizeX;
            float width = posY + sizeY;

            Container.Add(new CuiElement
            {
                Name = uiElement,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = text,
                        FontSize = 30,
                        Align = TextAnchor.MiddleCenter,
                        Color = "1.0 1.0 1.0 1.0"
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = posX.ToString() + " " + posY.ToString(), AnchorMax = heigth.ToString() + " " + width.ToString(),
                    },
                }
            });

            CuiHelper.AddUi(player, Container);
        }

        void createTierProgressBar(BasePlayer player, string uiElement, string text, float loadingValue, float posX, float posY, float sizeX, float sizeY)
        {
            destroyGuiElement(player, uiElement);
            var Container = new CuiElementContainer();

            float heigth = posX + sizeX;
            float width = posY + sizeY;

            Container.Add(new CuiElement
            {
                Name = uiElement,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.2 0.2 0.2 0.7"
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = posX.ToString() + " " + posY.ToString(), AnchorMax = heigth.ToString() + " " + width.ToString(),
                    },
                }
            });
            Container.Add(new CuiElement
            {
                Parent = uiElement,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.8 0.8 0.8 0.5"
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.0 0.0", AnchorMax = loadingValue.ToString() + " 1.0",
                    },
                }
            });
            Container.Add(new CuiElement
            {
                Parent = uiElement,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = text,
                        FontSize = 16,
                        Color = "0.0 0.0 0.0 0.8",
                        Align = TextAnchor.MiddleCenter
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.0 0.0", AnchorMax = "1.0 0.9",
                    },
                }
            });

            CuiHelper.AddUi(player, Container);
        }

        void createTearRangeBox(BasePlayer player, string uiElement, int a, int b, int c, int d, int e)
        {
            destroyGuiElement(player, uiElement);
            var Container = new CuiElementContainer();

            int[] isLetterWhite = new int[5];
            string[] letterArray = new string[5];
            float spaceMin = 0.05f;
            float spaceMax = 0.05f;
            float amountSpace = 0.18f;
            string textColor;

            isLetterWhite[0] = a; isLetterWhite[1] = b; isLetterWhite[2] = c; isLetterWhite[3] = d; isLetterWhite[4] = e;
            letterArray[0] = "A"; letterArray[1] = "B"; letterArray[2] = "C"; letterArray[3] = "D"; letterArray[4] = "E";

            float heigth = tierRangeBoxSizeY + tierRangeBoxSizeX;
            float width = tierRangeBoxPosY + tierRangeBoxSizeY;

            Container.Add(new CuiElement
            {
                Name = uiElement,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.2 0.2 0.2 0.7"
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = tierRangeBoxPosX.ToString() + " " + tierRangeBoxPosY.ToString(), AnchorMax = heigth.ToString() + " " + width.ToString(),
                    },
                }
            });
            for(int i = 0;i < isLetterWhite.Length;i++)
            {
                spaceMax = spaceMax + amountSpace;

                if(isLetterWhite[i] == 1)
                {
                    textColor = "1.0 0.6 0.2 1.0";
                }
                else
                {
                    textColor = "1.0 1.0 1.0 0.1";
                }

                Container.Add(new CuiElement
                {
                    Parent = uiElement,
                    Components =
                {
                    new CuiTextComponent
                    {
                        Text = letterArray[i],
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter,
                        Color = textColor
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = spaceMin + " 0.0", AnchorMax = spaceMax + " 0.9",
                    },
                }
                });

                spaceMin = spaceMin + amountSpace;
            }

            CuiHelper.AddUi(player, Container);
        }

        void getPlayerTierOnLookTextBox(BasePlayer player, string uiElement, string text, string color, float posX, float posY, float sizeX, float sizeY)
        {
            destroyGuiElement(player, uiElement);
            var Container = new CuiElementContainer();

            float heigth = posX + sizeX;
            float width = posY + sizeY;

            Container.Add(new CuiElement
            {
                Name = uiElement,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = text,
                        FontSize = 18,
                        Align = TextAnchor.MiddleCenter,
                        Color = color,
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = posX.ToString() + " " + posY.ToString(), AnchorMax = heigth.ToString() + " " + width.ToString(),
                    },
                }
            });

            Container.Add(new CuiElement
            {
                Parent = uiElement,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = "0.2 0.2 0.2 0.7",
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0.0 0.0", AnchorMax = "1.0 1.0",
                    },
                }
            });

            CuiHelper.AddUi(player, Container);
        }
        #endregion

        #region Commands

        [ChatCommand("cratepos")]
        void addHackableCreateSpawnPos(BasePlayer player)
        {
            if (isPlayerAwake(player) && player.IsAdmin)
            {
                //Variables
                string fileName = "LayersOfRiskHackableCratePositions";
                Vector3 playerEyesLookPos = getRayCastHitPosition(player.eyes.HeadRay());
                Timer timerOnce;

                BaseEntity crate = GameManager.server.CreateEntity(hackableCreate, getRayCastHitPosition(player.eyes.HeadRay()), new Quaternion(), true);

                crate.Spawn();

                StorageContainer crateContainer = crate.GetComponent<StorageContainer>();
                ItemContainer newContainer = new ItemContainer();
                newContainer.ServerInitialize(null, 6);
                newContainer.GiveUID();
                crateContainer.inventory = newContainer;

                timerOnce = timer.Once(2, () =>
                {
                    //Get datafile
                    DynamicConfigFile dataFile = Interface.Oxide.DataFileSystem.GetDatafile(fileName);
                    string item = (hackableCrateDataDic.Count() + 1).ToString();

                    if (hackableCrateDataDic.Keys.ElementAt(0).x == 0 && hackableCrateDataDic.Keys.ElementAt(0).y == 0 && hackableCrateDataDic.Keys.ElementAt(0).z == 0)
                    {
                        hackableCrateDataDic.Remove(Vector3.zero);
                        item = "1";
                    }

                    dataFile[item, "posX"] = crate.transform.position.x;
                    dataFile[item, "posY"] = crate.transform.position.y;
                    dataFile[item, "posZ"] = crate.transform.position.z;

                    dataFile.Save();

                    hackableCrateDataDic.Add(new Vector3(crate.transform.position.x, crate.transform.position.y, crate.transform.position.z),
                    new hackableCrateDataClass
                    {
                        inUse = false,
                    });

                    crate.Kill();

                    string CrateSpawn = lang.GetMessage("CrateSpawn", this, player.UserIDString);
                    PrintToChat(player, string.Format(CrateSpawn, playerEyesLookPos));
                });
            }
        }
        #endregion
    }

}