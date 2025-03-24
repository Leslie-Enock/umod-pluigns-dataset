/* --- Contributor information ---
 * Please follow the following set of guidelines when working on this plugin,
 * this to help others understand this file more easily.
 *
 * NOTE: On Authors, new entries go BELOW the existing entries. As with any other software header comment.
 *
 * -- Authors --
 * Thimo (ThibmoRozier) <thibmorozier@live.nl> 2021-04-19 +
 *
 * -- Naming --
 * Avoid using non-alphabetic characters, eg: _
 * Avoid using numbers in method and class names (Upgrade methods are allowed to have these, for readability)
 * Private constants -------------------- SHOULD start with a uppercase "C" (PascalCase)
 * Private readonly fields -------------- SHOULD start with a uppercase "C" (PascalCase)
 * Private fields ----------------------- SHOULD start with a uppercase "F" (PascalCase)
 * Arguments/Parameters ----------------- SHOULD start with a lowercase "a" (camelCase)
 * Classes ------------------------------ SHOULD start with a uppercase character (PascalCase)
 * Methods ------------------------------ SHOULD start with a uppercase character (PascalCase)
 * Public properties (constants/fields) - SHOULD start with a uppercase character (PascalCase)
 * Variables ---------------------------- SHOULD start with a lowercase character (camelCase)
 *
 * -- Style --
 * Max-line-width ------- 160
 * Single-line comments - // Single-line comment
 * Multi-line comments -- Just like this comment block!
 */

using System.ComponentModel;
using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Verification Gatekeeper", "ThibmoRozier", "1.2.1")]
    [Description("Prevents players from doing anything on the server until they are given the bypass permission")]
    public class VerificationGatekeeper : RustPlugin
    {
        #region Constants
        private const string CPermBypass = "verificationgatekeeper.bypass";
        private const string CPermBypassMount = "verificationgatekeeper.bypass.mount";
        private const string CPermBypassBedActions = "verificationgatekeeper.bypass.bedactions";
        private const string CPermBypassBuild = "verificationgatekeeper.bypass.build";
        private const string CPermBypassCardSwiping = "verificationgatekeeper.bypass.cardswiping";
        private const string CPermBypassChat = "verificationgatekeeper.bypass.chat";
        private const string CPermBypassCollectiblePickup = "verificationgatekeeper.bypass.collectiblepickup";
        private const string CPermBypassCommand = "verificationgatekeeper.bypass.command";
        private const string CPermBypassCounterActions = "verificationgatekeeper.bypass.counteractions";
        private const string CPermBypassCrafting = "verificationgatekeeper.bypass.crafting";
        private const string CPermBypassCrateHack = "verificationgatekeeper.bypass.cratehack";
        private const string CPermBypassCupboardActions = "verificationgatekeeper.bypass.cupboardactions";
        private const string CPermBypassCustomUI = "verificationgatekeeper.bypass.customui";
        private const string CPermBypassDemolish = "verificationgatekeeper.bypass.demolish";
        private const string CPermBypassDeployItem = "verificationgatekeeper.bypass.deployitem";
        private const string CPermBypassDoorActions = "verificationgatekeeper.bypass.dooractions";
        private const string CPermBypassElevatorActions = "verificationgatekeeper.bypass.elevatoractions";
        private const string CPermBypassEntityLooting = "verificationgatekeeper.bypass.entitylooting";
        private const string CPermBypassEntityPickup = "verificationgatekeeper.bypass.entitypickup";
        private const string CPermBypassExplosives = "verificationgatekeeper.bypass.explosives";
        private const string CPermBypassFlamers = "verificationgatekeeper.bypass.flamers";
        private const string CPermBypassFuelActions = "verificationgatekeeper.bypass.fuelactions";
        private const string CPermBypassGrowableGathering = "verificationgatekeeper.bypass.growablegathering";
        private const string CPermBypassHealingItemUsage = "verificationgatekeeper.bypass.healingitemusage";
        private const string CPermBypassHelicopterActions = "verificationgatekeeper.bypass.helicopteractions";
        private const string CPermBypassItemActions = "verificationgatekeeper.bypass.itemactions";
        private const string CPermBypassItemDropping = "verificationgatekeeper.bypass.itemdropping";
        private const string CPermBypassItemMoving = "verificationgatekeeper.bypass.itemmoving";
        private const string CPermBypassItemPickup = "verificationgatekeeper.bypass.itempickup";
        private const string CPermBypassItemSkinning = "verificationgatekeeper.bypass.itemskinning";
        private const string CPermBypassItemStacking = "verificationgatekeeper.bypass.itemstacking";
        private const string CPermBypassItemWearing = "verificationgatekeeper.bypass.itemwearing";
        private const string CPermBypassLiftActions = "verificationgatekeeper.bypass.liftactions";
        private const string CPermBypassLockActions = "verificationgatekeeper.bypass.lockactions";
        private const string CPermBypassMailboxActions = "verificationgatekeeper.bypass.mailboxactions";
        private const string CPermBypassMelee = "verificationgatekeeper.bypass.melee";
        private const string CPermBypassOvenActions = "verificationgatekeeper.bypass.ovenactions";
        private const string CPermBypassPhoneActions = "verificationgatekeeper.bypass.phoneactions";
        private const string CPermBypassPlayerAssist = "verificationgatekeeper.bypass.playerassist";
        private const string CPermBypassPlayerLooting = "verificationgatekeeper.bypass.playerlooting";
        private const string CPermBypassPush = "verificationgatekeeper.bypass.push";
        private const string CPermBypassRecyclerActions = "verificationgatekeeper.bypass.recycleractions";
        private const string CPermBypassReloading = "verificationgatekeeper.bypass.reloading";
        private const string CPermBypassRepair = "verificationgatekeeper.bypass.repair";
        private const string CPermBypassResearch = "verificationgatekeeper.bypass.research";
        private const string CPermBypassRockets = "verificationgatekeeper.bypass.rockets";
        private const string CPermBypassShopActions = "verificationgatekeeper.bypass.shopactions";
        private const string CPermBypassSignUpdate = "verificationgatekeeper.bypass.signupdate";
        private const string CPermBypassStashActions = "verificationgatekeeper.bypass.stashactions";
        private const string CPermBypassStructureRotate = "verificationgatekeeper.bypass.structurerotate";
        private const string CPermBypassSwitchActions = "verificationgatekeeper.bypass.switchactions";
        private const string CPermBypassTeamCreation = "verificationgatekeeper.bypass.teamcreation";
        private const string CPermBypassTrapActions = "verificationgatekeeper.bypass.trapactions";
        private const string CPermBypassTurretActions = "verificationgatekeeper.bypass.turretactions";
        private const string CPermBypassUpgrade = "verificationgatekeeper.bypass.upgrade";
        private const string CPermBypassVendingAdmin = "verificationgatekeeper.bypass.vendingadmin";
        private const string CPermBypassVendingUsage = "verificationgatekeeper.bypass.vendingusage";
        private const string CPermBypassWeaponFiring = "verificationgatekeeper.bypass.weaponfiring";
        private const string CPermBypassWiring = "verificationgatekeeper.bypass.wiring";
        private const string CPermBypassWoodCutting = "verificationgatekeeper.bypass.woodcutting";
        private const string CPermBypassWorldProjectiles = "verificationgatekeeper.bypass.worldprojectiles";
        private const string CPermBypassWounded = "verificationgatekeeper.bypass.wounded";
        #endregion Constants

        #region Variables
        private ConfigData FConfigData;
        #endregion Variables

        #region Config
        /// <summary>
        /// The config type class
        /// </summary>
        private class ConfigData
        {
            [JsonProperty("Admin Is Always Verified", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool AdminAlwaysVerified = true;
            [JsonProperty("Prevent (Dis-)Mount", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventMount = true;
            [JsonProperty("Prevent Bed Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventBedActions = true;
            [JsonProperty("Prevent Build", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventBuild = true;
            [JsonProperty("Prevent Card Swiping", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventCardSwiping = true;
            [JsonProperty("Prevent Chat", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventChat = true;
            [JsonProperty("Prevent Collectible Pickup", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventCollectiblePickup = true;
            [JsonProperty("Prevent Command", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventCommand = true;
            [JsonProperty("Prevent Counter Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventCounterActions = true;
            [JsonProperty("Prevent Crafting", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventCrafting = true;
            [JsonProperty("Prevent Crate Hack", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventCrateHack = true;
            [JsonProperty("Prevent Cupboard Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventCupboardActions = true;
            [JsonProperty("Prevent Custom UI", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(false)]
            public bool PreventCustomUI = false;
            [JsonProperty("Prevent Demolish", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventDemolish = true;
            [JsonProperty("Prevent Deploy Item", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventDeployItem = true;
            [JsonProperty("Prevent Door Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventDoorActions = true;
            [JsonProperty("Prevent Elevator Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventElevatorActions = true;
            [JsonProperty("Prevent Entity Looting", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventEntityLooting = true;
            [JsonProperty("Prevent Entity Pickup", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventEntityPickup = true;
            [JsonProperty("Prevent Explosives", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventExplosives = true;
            [JsonProperty("Prevent Flamers", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventFlamers = true;
            [JsonProperty("Prevent Fuel Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventFuelActions = true;
            [JsonProperty("Prevent Growable Gathering", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventGrowableGathering = true;
            [JsonProperty("Prevent Healing Item Usage", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventHealingItemUsage = true;
            [JsonProperty("Prevent Helicopter Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventHelicopterActions = true;
            [JsonProperty("Prevent Item Action", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventItemAction = true;
            [JsonProperty("Prevent Item Dropping", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventItemDropping = true;
            [JsonProperty("Prevent Item Moving", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventItemMoving = true;
            [JsonProperty("Prevent Item Pickup", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventItemPickup = true;
            [JsonProperty("Prevent Item Skinning", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventItemSkinning = true;
            [JsonProperty("Prevent Item Stacking", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventItemStacking = true;
            [JsonProperty("Prevent Item Wearing", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventItemWearing = true;
            [JsonProperty("Prevent Lift Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventLiftActions = true;
            [JsonProperty("Prevent Lock Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventLockActions = true;
            [JsonProperty("Prevent Mailbox Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventMailboxActions = true;
            [JsonProperty("Prevent Melee", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventMelee = true;
            [JsonProperty("Prevent Oven & Furnace Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventOvenActions = true;
            [JsonProperty("Prevent Phone Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventPhoneActions = true;
            [JsonProperty("Prevent Player Assist", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventPlayerAssist = true;
            [JsonProperty("Prevent Player Looting", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventPlayerLooting = true;
            [JsonProperty("Prevent Push", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventPush = true;
            [JsonProperty("Prevent Recycler Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventRecyclerActions = true;
            [JsonProperty("Prevent Reloading", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventReloading = true;
            [JsonProperty("Prevent Repair", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventRepair = true;
            [JsonProperty("Prevent Research", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventResearch = true;
            [JsonProperty("Prevent Rockets", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventRockets = true;
            [JsonProperty("Prevent Shop Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventShopActions = true;
            [JsonProperty("Prevent Sign Update", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventSignUpdate = true;
            [JsonProperty("Prevent Stash Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventStashActions = true;
            [JsonProperty("Prevent Structure Rotate", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventStructureRotate = true;
            [JsonProperty("Prevent Switch Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventSwitchActions = true;
            [JsonProperty("Prevent Team Creation", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventTeamCreation = true;
            [JsonProperty("Prevent Trap Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventTrapActions = true;
            [JsonProperty("Prevent Turret Actions", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventTurretActions = true;
            [JsonProperty("Prevent Upgrade", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventUpgrade = true;
            [JsonProperty("Prevent Vending Admin", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventVendingAdmin = true;
            [JsonProperty("Prevent Vending Usage", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventVendingUsage = true;
            [JsonProperty("Prevent Weapon Firing", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventWeaponFiring = true;
            [JsonProperty("Prevent Wiring", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventWiring = true;
            [JsonProperty("Prevent Wood Cutting", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventWoodCutting = true;
            [JsonProperty("Prevent World Projectiles", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventWorldProjectiles = true;
            [JsonProperty("Prevent Wounded", DefaultValueHandling = DefaultValueHandling.Populate), DefaultValue(true)]
            public bool PreventWounded = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            FConfigData = Config.ReadObject<ConfigData>();
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            FConfigData = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(FConfigData);
        #endregion Config

        #region Script Methods
        private bool? CheckAndReturnNullOrFalse(BasePlayer aPlayer, string aBypassPerm)
        {
            if (aPlayer == null || aPlayer.IsNpc || aPlayer.IPlayer.IsServer || (FConfigData.AdminAlwaysVerified && Player.IsAdmin(aPlayer)) ||
                permission.UserHasPermission(aPlayer.UserIDString, CPermBypass) || permission.UserHasPermission(aPlayer.UserIDString, aBypassPerm))
                return null;

            return false;
        }

        private bool? CheckAndReturnNullOrFalse(IPlayer aPlayer, string aBypassPerm)
        {
            if (aPlayer == null || aPlayer.IsServer || (FConfigData.AdminAlwaysVerified && aPlayer.IsAdmin) ||
                permission.UserHasPermission(aPlayer.Id, CPermBypass) || permission.UserHasPermission(aPlayer.Id, aBypassPerm))
                return null;

            return false;
        }
        #endregion Script Methods

        #region Hooks
        void Init()
        {
            permission.RegisterPermission(CPermBypass, this);

            /*
            // Just as a nice-to-have I'll leave this here
            if (!permission.GroupExists(FConfigData.VerifiedPlayerGroup))
                permission.CreateGroup(FConfigData.VerifiedPlayerGroup, "", 0);
            */

            if (!FConfigData.PreventMount)
            {
                Unsubscribe(nameof(CanDismountEntity));
                Unsubscribe(nameof(CanMountEntity));
                Unsubscribe(nameof(CanSwapToSeat));
                Unsubscribe(nameof(OnRidableAnimalClaim));
            }
            else
            {
                permission.RegisterPermission(CPermBypassMount, this);
            }

            if (!FConfigData.PreventBedActions)
            {
                Unsubscribe(nameof(CanAssignBed));
                Unsubscribe(nameof(CanRenameBed));
                Unsubscribe(nameof(CanSetBedPublic));
            }
            else
            {
                permission.RegisterPermission(CPermBypassBedActions, this);
            }

            if (!FConfigData.PreventBuild)
            {
                Unsubscribe(nameof(CanAffordToPlace));
                Unsubscribe(nameof(CanBuild));
                Unsubscribe(nameof(OnConstructionPlace));
            }
            else
            {
                permission.RegisterPermission(CPermBypassBuild, this);
            }

            if (!FConfigData.PreventCardSwiping)
            {
                Unsubscribe(nameof(OnCardSwipe));
            }
            else
            {
                permission.RegisterPermission(CPermBypassCardSwiping, this);
            }

            if (!FConfigData.PreventChat)
            {
                Unsubscribe(nameof(OnUserChat));
            }
            else
            {
                permission.RegisterPermission(CPermBypassChat, this);
            }

            if (!FConfigData.PreventCollectiblePickup)
            {
                Unsubscribe(nameof(OnCollectiblePickup));
            }
            else
            {
                permission.RegisterPermission(CPermBypassCollectiblePickup, this);
            }

            if (!FConfigData.PreventCommand)
            {
                Unsubscribe(nameof(OnUserCommand));
            }
            else
            {
                permission.RegisterPermission(CPermBypassCommand, this);
            }

            if (!FConfigData.PreventCounterActions)
            {
                Unsubscribe(nameof(OnCounterModeToggle));
                Unsubscribe(nameof(OnCounterTargetChange));
            }
            else
            {
                permission.RegisterPermission(CPermBypassCounterActions, this);
            }

            if (FConfigData.PreventCrafting)
                permission.RegisterPermission(CPermBypassCrafting, this);

            if (!FConfigData.PreventCrateHack)
            {
                Unsubscribe(nameof(CanHackCrate));
            }
            else
            {
                permission.RegisterPermission(CPermBypassCrateHack, this);
            }

            if (!FConfigData.PreventCupboardActions)
            {
                Unsubscribe(nameof(OnCupboardAuthorize));
                Unsubscribe(nameof(OnCupboardClearList));
                Unsubscribe(nameof(OnCupboardDeauthorize));
            }
            else
            {
                permission.RegisterPermission(CPermBypassCupboardActions, this);
            }

            if (!FConfigData.PreventCustomUI)
            {
                Unsubscribe(nameof(CanUseUI));
            }
            else
            {
                permission.RegisterPermission(CPermBypassCustomUI, this);
            }

            if (!FConfigData.PreventDemolish)
            {
                Unsubscribe(nameof(CanDemolish));
                Unsubscribe(nameof(OnStructureDemolish));
            }
            else
            {
                permission.RegisterPermission(CPermBypassDemolish, this);
            }

            if (!FConfigData.PreventDeployItem)
            {
                Unsubscribe(nameof(CanDeployItem));
            }
            else
            {
                permission.RegisterPermission(CPermBypassDeployItem, this);
            }

            if (!FConfigData.PreventDoorActions)
            {
                Unsubscribe(nameof(OnDoorClosed));
                Unsubscribe(nameof(OnDoorOpened));
            }
            else
            {
                permission.RegisterPermission(CPermBypassDoorActions, this);
            }

            if (!FConfigData.PreventElevatorActions)
            {
                Unsubscribe(nameof(OnElevatorButtonPress));
            }
            else
            {
                permission.RegisterPermission(CPermBypassElevatorActions, this);
            }

            if (!FConfigData.PreventEntityLooting)
            {
                Unsubscribe(nameof(CanLootEntity));
            }
            else
            {
                permission.RegisterPermission(CPermBypassEntityLooting, this);
            }

            if (!FConfigData.PreventEntityPickup)
            {
                Unsubscribe(nameof(CanPickupEntity));
            }
            else
            {
                permission.RegisterPermission(CPermBypassEntityPickup, this);
            }

            if (!FConfigData.PreventExplosives)
            {
                Unsubscribe(nameof(OnExplosiveDropped));
                Unsubscribe(nameof(OnExplosiveThrown));
            }
            else
            {
                permission.RegisterPermission(CPermBypassExplosives, this);
            }

            if (!FConfigData.PreventFlamers)
            {
                Unsubscribe(nameof(OnFlameThrowerBurn));
            }
            else
            {
                permission.RegisterPermission(CPermBypassFlamers, this);
            }

            if (!FConfigData.PreventFuelActions)
            {
                Unsubscribe(nameof(CanCheckFuel));
            }
            else
            {
                permission.RegisterPermission(CPermBypassFuelActions, this);
            }

            if (!FConfigData.PreventGrowableGathering)
            {
                Unsubscribe(nameof(OnGrowableGather));
            }
            else
            {
                permission.RegisterPermission(CPermBypassGrowableGathering, this);
            }

            if (!FConfigData.PreventHealingItemUsage)
            {
                Unsubscribe(nameof(OnHealingItemUse));
            }
            else
            {
                permission.RegisterPermission(CPermBypassHealingItemUsage, this);
            }

            if (!FConfigData.PreventHelicopterActions)
            {
                Unsubscribe(nameof(CanUseHelicopter));
            }
            else
            {
                permission.RegisterPermission(CPermBypassHelicopterActions, this);
            }

            if (!FConfigData.PreventItemAction)
            {
                Unsubscribe(nameof(OnItemAction));
            }
            else
            {
                permission.RegisterPermission(CPermBypassItemActions, this);
            }

            if (!FConfigData.PreventItemDropping)
            {
                Unsubscribe(nameof(CanDropActiveItem));
            }
            else
            {
                permission.RegisterPermission(CPermBypassItemDropping, this);
            }

            if (!FConfigData.PreventItemMoving)
            {
                Unsubscribe(nameof(CanAcceptItem));
                Unsubscribe(nameof(CanMoveItem));
            }
            else
            {
                permission.RegisterPermission(CPermBypassItemMoving, this);
            }

            if (!FConfigData.PreventItemPickup)
            {
                Unsubscribe(nameof(OnItemPickup));
            }
            else
            {
                permission.RegisterPermission(CPermBypassItemPickup, this);
            }

            if (!FConfigData.PreventItemSkinning)
            {
                Unsubscribe(nameof(OnItemSkinChange));
            }
            else
            {
                permission.RegisterPermission(CPermBypassItemSkinning, this);
            }

            if (!FConfigData.PreventItemStacking)
            {
                Unsubscribe(nameof(CanStackItem));
            }
            else
            {
                permission.RegisterPermission(CPermBypassItemStacking, this);
            }

            if (!FConfigData.PreventItemWearing)
            {
                Unsubscribe(nameof(CanWearItem));
            }
            else
            {
                permission.RegisterPermission(CPermBypassItemWearing, this);
            }

            if (!FConfigData.PreventLiftActions)
            {
                Unsubscribe(nameof(OnLiftUse));
            }
            else
            {
                permission.RegisterPermission(CPermBypassLiftActions, this);
            }

            if (!FConfigData.PreventLockActions)
            {
                Unsubscribe(nameof(CanChangeCode));
                Unsubscribe(nameof(CanLock));
                Unsubscribe(nameof(CanPickupLock));
                Unsubscribe(nameof(CanUnlock));
                Unsubscribe(nameof(CanUseLockedEntity));
                Unsubscribe(nameof(OnCodeEntered));
                Unsubscribe(nameof(OnItemLock));
                Unsubscribe(nameof(OnItemUnlock));
            }
            else
            {
                permission.RegisterPermission(CPermBypassLockActions, this);
            }

            if (!FConfigData.PreventMailboxActions)
            {
                Unsubscribe(nameof(CanUseMailbox));
            }
            else
            {
                permission.RegisterPermission(CPermBypassMailboxActions, this);
            }

            if (!FConfigData.PreventMelee)
            {
                Unsubscribe(nameof(OnMeleeAttack));
                Unsubscribe(nameof(OnMeleeThrown));
            }
            else
            {
                permission.RegisterPermission(CPermBypassMelee, this);
            }

            if (!FConfigData.PreventOvenActions)
            {
                Unsubscribe(nameof(OnOvenToggle));
            }
            else
            {
                permission.RegisterPermission(CPermBypassOvenActions, this);
            }

            if (!FConfigData.PreventPhoneActions)
            {
                Unsubscribe(nameof(OnPhoneDial));
                Unsubscribe(nameof(OnPhoneCallStart));
                Unsubscribe(nameof(OnPhoneNameUpdate));
            }
            else
            {
                permission.RegisterPermission(CPermBypassPhoneActions, this);
            }

            if (!FConfigData.PreventPlayerAssist)
            {
                Unsubscribe(nameof(OnPlayerAssist));
                Unsubscribe(nameof(OnPlayerRevive));
            }
            else
            {
                permission.RegisterPermission(CPermBypassPlayerAssist, this);
            }

            if (!FConfigData.PreventPlayerLooting)
            {
                Unsubscribe(nameof(CanLootPlayer));
            }
            else
            {
                permission.RegisterPermission(CPermBypassPlayerLooting, this);
            }

            if (!FConfigData.PreventPush)
            {
                Unsubscribe(nameof(CanPushBoat));
                Unsubscribe(nameof(OnVehiclePush));
            }
            else
            {
                permission.RegisterPermission(CPermBypassPush, this);
            }

            if (!FConfigData.PreventRecyclerActions)
            {
                Unsubscribe(nameof(OnRecyclerToggle));
            }
            else
            {
                permission.RegisterPermission(CPermBypassRecyclerActions, this);
            }

            if (!FConfigData.PreventReloading)
            {
                Unsubscribe(nameof(OnReloadMagazine));
                Unsubscribe(nameof(OnReloadWeapon));
                Unsubscribe(nameof(OnSwitchAmmo));
            }
            else
            {
                permission.RegisterPermission(CPermBypassReloading, this);
            }

            if (!FConfigData.PreventRepair)
            {
                Unsubscribe(nameof(OnHammerHit));
                Unsubscribe(nameof(OnStructureRepair));
            }
            else
            {
                permission.RegisterPermission(CPermBypassRepair, this);
            }

            if (!FConfigData.PreventResearch)
            {
                Unsubscribe(nameof(CanResearchItem));
                Unsubscribe(nameof(CanUnlockTechTreeNode));
                Unsubscribe(nameof(CanUnlockTechTreeNodePath));
            }
            else
            {
                permission.RegisterPermission(CPermBypassResearch, this);
            }

            if (!FConfigData.PreventRockets)
            {
                Unsubscribe(nameof(OnRocketLaunched));
            }
            else
            {
                permission.RegisterPermission(CPermBypassRockets, this);
            }

            if (!FConfigData.PreventShopActions)
            {
                Unsubscribe(nameof(OnShopCompleteTrade));
            }
            else
            {
                permission.RegisterPermission(CPermBypassShopActions, this);
            }

            if (!FConfigData.PreventSignUpdate)
            {
                Unsubscribe(nameof(CanUpdateSign));
            }
            else
            {
                permission.RegisterPermission(CPermBypassSignUpdate, this);
            }

            if (!FConfigData.PreventStashActions)
            {
                Unsubscribe(nameof(CanHideStash));
                Unsubscribe(nameof(CanSeeStash));
            }
            else
            {
                permission.RegisterPermission(CPermBypassStashActions, this);
            }

            if (!FConfigData.PreventStructureRotate)
            {
                Unsubscribe(nameof(OnStructureRotate));
            }
            else
            {
                permission.RegisterPermission(CPermBypassStructureRotate, this);
            }

            if (!FConfigData.PreventSwitchActions)
            {
                Unsubscribe(nameof(OnSwitchToggle));
            }
            else
            {
                permission.RegisterPermission(CPermBypassSwitchActions, this);
            }

            if (!FConfigData.PreventTeamCreation)
            {
                Unsubscribe(nameof(OnTeamCreate));
            }
            else
            {
                permission.RegisterPermission(CPermBypassTeamCreation, this);
            }

            if (!FConfigData.PreventTrapActions)
            {
                Unsubscribe(nameof(OnTrapArm));
                Unsubscribe(nameof(OnTrapDisarm));
            }
            else
            {
                permission.RegisterPermission(CPermBypassTrapActions, this);
            }

            if (!FConfigData.PreventTurretActions)
            {
                Unsubscribe(nameof(OnTurretAuthorize));
                Unsubscribe(nameof(OnTurretClearList));
                Unsubscribe(nameof(OnTurretRotate));
            }
            else
            {
                permission.RegisterPermission(CPermBypassTurretActions, this);
            }

            if (!FConfigData.PreventUpgrade)
            {
                Unsubscribe(nameof(CanAffordUpgrade));
                Unsubscribe(nameof(CanChangeGrade));
                Unsubscribe(nameof(OnStructureUpgrade));
            }
            else
            {
                permission.RegisterPermission(CPermBypassUpgrade, this);
            }

            if (!FConfigData.PreventVendingAdmin)
            {
                Unsubscribe(nameof(CanAdministerVending));
                Unsubscribe(nameof(OnRotateVendingMachine));
            }
            else
            {
                permission.RegisterPermission(CPermBypassVendingAdmin, this);
            }

            if (!FConfigData.PreventVendingUsage)
            {
                Unsubscribe(nameof(CanUseVending));
                Unsubscribe(nameof(OnBuyVendingItem));
                Unsubscribe(nameof(OnVendingTransaction));
            }
            else
            {
                permission.RegisterPermission(CPermBypassVendingUsage, this);
            }

            if (!FConfigData.PreventWeaponFiring)
            {
                Unsubscribe(nameof(OnWeaponFired));
            }
            else
            {
                permission.RegisterPermission(CPermBypassWeaponFiring, this);
            }

            if (!FConfigData.PreventWiring)
            {
                Unsubscribe(nameof(CanUseWires));
            }
            else
            {
                permission.RegisterPermission(CPermBypassWiring, this);
            }

            if (!FConfigData.PreventWoodCutting)
            {
                Unsubscribe(nameof(CanTakeCutting));
            }
            else
            {
                permission.RegisterPermission(CPermBypassWoodCutting, this);
            }

            if (!FConfigData.PreventWorldProjectiles)
            {
                Unsubscribe(nameof(CanCreateWorldProjectile));
                Unsubscribe(nameof(OnCreateWorldProjectile));
            }
            else
            {
                permission.RegisterPermission(CPermBypassWorldProjectiles, this);
            }

            if (!FConfigData.PreventWounded)
            {
                Unsubscribe(nameof(CanBeWounded));
            }
            else
            {
                permission.RegisterPermission(CPermBypassWounded, this);
            }

            if (!(FConfigData.PreventCrafting || FConfigData.PreventItemSkinning))
                Unsubscribe(nameof(CanCraft));
        }

        // PreventMount
        bool? CanDismountEntity(BasePlayer aPlayer, BaseMountable aEntity) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassMount);

        bool? CanMountEntity(BasePlayer aPlayer, BaseMountable aEntity) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassMount);

        bool? CanSwapToSeat(BasePlayer aPlayer, BaseMountable aMountable) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassMount);

        bool? OnRidableAnimalClaim(BaseRidableAnimal aAnimal, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassMount);

        // PreventBedActions
        bool? CanAssignBed(BasePlayer aPlayer, SleepingBag aBag, ulong aTargetPlayerId) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassBedActions);

        bool? CanRenameBed(BasePlayer aPlayer, SleepingBag aBed, string aBedName) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassBedActions);

        bool? CanSetBedPublic(BasePlayer aPlayer, SleepingBag aBed) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassBedActions);

        // PreventBuild
        bool? CanAffordToPlace(BasePlayer aPlayer, Planner aPlanner, Construction aConstruction) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassBuild);

        bool? CanBuild(Planner aPlanner, Construction aPrefab, Construction.Target aTarget) =>
            CheckAndReturnNullOrFalse(aPlanner.GetOwnerPlayer(), CPermBypassBuild);

        bool? OnConstructionPlace(BaseEntity aEntity, Construction aComponent, Construction.Target aConstructionTarget, BasePlayer aPlayer) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassBuild);

        // PreventCardSwiping
        bool? OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassCardSwiping);

        // PreventChat
        bool? OnUserChat(IPlayer aPlayer, string aMessage) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassChat);

        // PreventCollectiblePickup
        bool? OnCollectiblePickup(CollectibleEntity aEntity, BasePlayer aPlayer) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassCollectiblePickup);

        // PreventCommand
        bool? OnUserCommand(IPlayer aPlayer, string command, string[] args) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassCommand);

        // PreventCounterActions
        bool? OnCounterModeToggle(PowerCounter aCounter, BasePlayer aPlayer, bool aMode) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassCounterActions);

        bool? OnCounterTargetChange(PowerCounter aCounter, BasePlayer aPlayer, int aTargetNumber) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassCounterActions);

        // PreventCrafting
        bool? CanCraft(ItemCrafter aItemCrafter, ItemBlueprint aBp, int aAmount) =>
            FConfigData.PreventCrafting
                ? CheckAndReturnNullOrFalse(aItemCrafter.baseEntity, CPermBypassCrafting)
                : null;

        bool? CanCraft(PlayerBlueprints aPlayerBlueprints, ItemDefinition aItemDefinition, int aSkinItemId)
        {
            bool? result = null;

            if (FConfigData.PreventCrafting)
                result = CheckAndReturnNullOrFalse(aPlayerBlueprints.baseEntity, CPermBypassCrafting);

            if (result == null && FConfigData.PreventCrafting && aSkinItemId != 0)
                result = CheckAndReturnNullOrFalse(aPlayerBlueprints.baseEntity, CPermBypassItemSkinning);

            return result;
        }

        // PreventCrateHack
        bool? CanHackCrate(BasePlayer aPlayer, HackableLockedCrate aCrate) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassCrateHack);


        // PreventCupboardActions
        bool? OnCupboardAuthorize(BuildingPrivlidge aPrivilege, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassCupboardActions);

        bool? OnCupboardClearList(BuildingPrivlidge aPrivilege, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassCupboardActions);

        bool? OnCupboardDeauthorize(BuildingPrivlidge aPrivilege, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassCupboardActions);

        // PreventCustomUI
        bool? CanUseUI(BasePlayer aPlayer, string aJson) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassCustomUI);

        // PreventDemolish
        bool? CanDemolish(BasePlayer aPlayer, BuildingBlock aBlock, BuildingGrade.Enum aGrade) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassDemolish);

        bool? OnStructureDemolish(BaseCombatEntity aEntity, BasePlayer aPlayer, bool aImmediate) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassDemolish);

        // PreventDeployItem
        bool? CanDeployItem(BasePlayer aPlayer, Deployer aDeployer, uint aEntityId) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassDeployItem);

        // PreventDoorActions
        void OnDoorClosed(Door aDoor, BasePlayer aPlayer)
        {
            if (aPlayer == null || aPlayer.IsNpc || aPlayer.IPlayer.IsServer || (FConfigData.AdminAlwaysVerified && Player.IsAdmin(aPlayer)) ||
                permission.UserHasPermission(aPlayer.UserIDString, CPermBypass) || permission.UserHasPermission(aPlayer.UserIDString, CPermBypassDoorActions))
                return;

            aDoor.SetOpen(true, false);
        }

        void OnDoorOpened(Door aDoor, BasePlayer aPlayer)
        {
            if (aPlayer == null || aPlayer.IsNpc || aPlayer.IPlayer.IsServer || (FConfigData.AdminAlwaysVerified && Player.IsAdmin(aPlayer)) ||
                permission.UserHasPermission(aPlayer.UserIDString, CPermBypass) || permission.UserHasPermission(aPlayer.UserIDString, CPermBypassDoorActions))
                return;

            aDoor.SetOpen(false, false);
        }


        // PreventElevatorActions
        bool? OnElevatorButtonPress(ElevatorLift aLift, BasePlayer aPlayer, Elevator.Direction aDirection, bool aToTopOrBottom) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassElevatorActions);


        // PreventEntityLooting
        bool? CanLootEntity(BasePlayer aPlayer, BaseRidableAnimal aAnimal) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassEntityLooting);

        bool? CanLootEntity(BasePlayer aPlayer, DroppedItemContainer aContainer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassEntityLooting);

        bool? CanLootEntity(BasePlayer aPlayer, LootableCorpse aCorpse) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassEntityLooting);

        bool? CanLootEntity(BasePlayer aPlayer, ResourceContainer aContainer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassEntityLooting);

        bool? CanLootEntity(BasePlayer aPlayer, StorageContainer aContainer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassEntityLooting);


        // PreventEntityPickup
        bool? CanPickupEntity(BasePlayer aPlayer, BaseEntity aEntity) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassEntityPickup);


        // PreventExplosives
        void OnExplosiveDropped(BasePlayer aPlayer, BaseEntity aEntity, ThrownWeapon aItem)
        {
            if (CheckAndReturnNullOrFalse(aPlayer, CPermBypassExplosives) != null)
                aEntity.AdminKill();
        }

        void OnExplosiveThrown(BasePlayer aPlayer, BaseEntity aEntity, ThrownWeapon aItem)
        {
            if (CheckAndReturnNullOrFalse(aPlayer, CPermBypassExplosives) != null)
                aEntity.AdminKill();
        }


        // PreventFlamers
        void OnFlameThrowerBurn(FlameThrower aThrower, BaseEntity aFlame)
        {
            if (CheckAndReturnNullOrFalse(aThrower.GetOwnerPlayer(), CPermBypassFlamers) != null)
                aThrower.SetFlameState(false);
        }


        // PreventFuelActions
        bool? CanCheckFuel(EntityFuelSystem aFuelSystem, StorageContainer aFuelContainer, BasePlayer aPlayer) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassFuelActions);


        // PreventGrowableGathering
        bool? OnGrowableGather(GrowableEntity plant, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassGrowableGathering);


        // PreventHealingItemUsage
        bool? OnHealingItemUse(MedicalTool aTool, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassHealingItemUsage);


        // PreventHelicopterActions
        bool? CanUseHelicopter(BasePlayer aPlayer, CH47HelicopterAIController aHelicopter) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassHelicopterActions);


        // PreventItemAction
        bool? OnItemAction(Item aItem, string aAction, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassItemActions);


        // PreventItemDropping
        bool? CanDropActiveItem(BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassItemDropping);


        // PreventItemMoving
        ItemContainer.CanAcceptResult? CanAcceptItem(ItemContainer aContainer, Item aItem, int aTargetPos)
        {
            BasePlayer player = aItem.GetOwnerPlayer();

            if (player == null || player.IsNpc || player.IPlayer.IsServer || (FConfigData.AdminAlwaysVerified && Player.IsAdmin(player)) ||
                permission.UserHasPermission(player.UserIDString, CPermBypass) || permission.UserHasPermission(player.UserIDString, CPermBypassItemMoving))
                return null;

            return ItemContainer.CanAcceptResult.CannotAccept;
        }

        bool? CanMoveItem(Item aItem, PlayerInventory aPlayerLoot, uint aTargetContainer, int aTargetSlot, int aAmount)
        {
            bool? result = CheckAndReturnNullOrFalse(aItem.GetOwnerPlayer(), CPermBypassItemMoving);
            return result is bool
                ? result
                : CheckAndReturnNullOrFalse(aPlayerLoot.baseEntity, CPermBypassItemMoving);
        }


        // PreventItemPickup
        bool? OnItemPickup(Item aItem, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassItemPickup);


        // PreventItemSkinning
        bool? OnItemSkinChange(int aSkinItemId, Item aItem, StorageContainer aContainer, BasePlayer aPlayer) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassItemSkinning);


        // PreventItemStacking
        bool? CanStackItem(Item aItem, Item aTargetItem) => CheckAndReturnNullOrFalse(aItem.GetOwnerPlayer(), CPermBypassItemStacking);


        // PreventItemWearing
        bool? CanWearItem(PlayerInventory aInventory, Item aItem, int aTargetSlot) => CheckAndReturnNullOrFalse(aInventory.baseEntity, CPermBypassItemWearing);


        // PreventLiftActions
        bool? OnLiftUse(Lift aLift, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassLiftActions);

        bool? OnLiftUse(ProceduralLift aLift, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassLiftActions);


        // PreventLockActions
        bool? CanChangeCode(BasePlayer aPlayer, CodeLock aCodeLock, string aNewCode, bool aIsGuestCode) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassLockActions);

        bool? CanLock(BasePlayer aPlayer, BaseLock aLock) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassLockActions);

        bool? CanPickupLock(BasePlayer aPlayer, BaseLock aBaseLock) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassLockActions);

        bool? CanUnlock(BasePlayer aPlayer, BaseLock aBaseLock) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassLockActions);

        bool? CanUseLockedEntity(BasePlayer aPlayer, BaseLock aBaseLock) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassLockActions);

        bool? OnCodeEntered(CodeLock aCodeLock, BasePlayer aPlayer, string aCode) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassLockActions);

        bool? OnItemLock(Item aItem) => CheckAndReturnNullOrFalse(aItem.GetOwnerPlayer(), CPermBypassLockActions);

        bool? OnItemUnlock(Item aItem) => CheckAndReturnNullOrFalse(aItem.GetOwnerPlayer(), CPermBypassLockActions);


        // PreventMailboxActions
        bool? CanUseMailbox(BasePlayer aPlayer, Mailbox aMailbox) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassMailboxActions);


        // PreventMelee
        bool? OnMeleeAttack(BasePlayer aPlayer, HitInfo aInfo) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassMelee);

        void OnMeleeThrown(BasePlayer aPlayer, Item aItem)
        {
            if (CheckAndReturnNullOrFalse(aPlayer, CPermBypassMelee) != null)
                aItem.Remove();
        }


        // PreventOvenActions
        bool? OnOvenToggle(BaseOven aOven, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassOvenActions);


        // PreventPhoneActions
        bool? OnPhoneDial(PhoneController aCallerPhone, PhoneController aReceiverPhone, BasePlayer aPlayer) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassPhoneActions);

        bool? OnPhoneCallStart(PhoneController aPhone, PhoneController aOtherPhone, BasePlayer aPlayer) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassPhoneActions);

        bool? OnPhoneNameUpdate(PhoneController aPhoneController, string aName, BasePlayer aPlayer) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassPhoneActions);


        // PreventPlayerAssist
        bool? OnPlayerAssist(BasePlayer aTarget, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassPlayerAssist);

        bool? OnPlayerRevive(BasePlayer aReviver, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassPlayerAssist);


        // PreventPlayerLooting
        bool? CanLootPlayer(BasePlayer aTarget, BasePlayer aLooter) => CheckAndReturnNullOrFalse(aLooter, CPermBypassPlayerLooting);


        // PreventPush
        bool? CanPushBoat(BasePlayer aPlayer, MotorRowboat aBoat) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassPush);

        bool? OnVehiclePush(BaseVehicle aVehicle, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassPush);


        // PreventRecyclerActions
        bool? OnRecyclerToggle(Recycler aRecycler, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassRecyclerActions);


        // PreventReloading
        bool? OnReloadMagazine(BasePlayer aPlayer, BaseProjectile aProjectile, int aDesiredAmount) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassReloading);

        bool? OnReloadWeapon(BasePlayer aPlayer, BaseProjectile aProjectile) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassReloading);

        bool? OnSwitchAmmo(BasePlayer aPlayer, BaseProjectile aProjectile) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassReloading);


        // PreventRepair
        bool? OnHammerHit(BasePlayer aPlayer, HitInfo aInfo) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassRepair);

        bool? OnStructureRepair(BaseCombatEntity aEntity, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassRepair);


        // PreventResearch
        bool? CanResearchItem(BasePlayer aPlayer, Item aTargetItem) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassResearch);

        bool? CanUnlockTechTreeNode(BasePlayer aPlayer, TechTreeData.NodeInstance aNode, TechTreeData aTechTree) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassResearch);

        bool? CanUnlockTechTreeNodePath(BasePlayer aPlayer, TechTreeData.NodeInstance aNode, TechTreeData aTechTree) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassResearch);


        // PreventRockets
        void OnRocketLaunched(BasePlayer aPlayer, BaseEntity aEntity)
        {
            if (CheckAndReturnNullOrFalse(aPlayer, CPermBypassRockets) != null)
                aEntity.AdminKill();
        }


        // PreventShopActions
        bool? OnShopCompleteTrade(ShopFront aShop, BasePlayer aCustomer) => CheckAndReturnNullOrFalse(aCustomer, CPermBypassShopActions);


        // PreventSignUpdate
        bool? CanUpdateSign(BasePlayer aPlayer, PhotoFrame aPhotoFrame) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassSignUpdate);

        bool? CanUpdateSign(BasePlayer aPlayer, Signage aSign) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassSignUpdate);


        // PreventStashActions
        bool? CanHideStash(BasePlayer aPlayer, StashContainer aStash) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassStashActions);

        bool? CanSeeStash(BasePlayer aPlayer, StashContainer aStash) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassStashActions);


        // PreventStructureRotate
        bool? OnStructureRotate(BaseCombatEntity aEntity, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassStructureRotate);


        // PreventSwitchActions
        bool? OnSwitchToggle(IOEntity aEntity, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassSwitchActions);


        // PreventTeamCreation
        bool? OnTeamCreate(BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassTeamCreation);


        // PreventTrapActions
        bool? OnTrapArm(BearTrap aTrap, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassTrapActions);

        bool? OnTrapDisarm(Landmine aTrap, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassTrapActions);


        // PreventTurretActions
        bool? OnTurretAuthorize(AutoTurret aTurret, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassTurretActions);

        bool? OnTurretClearList(AutoTurret aTurret, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassTurretActions);

        bool? OnTurretRotate(AutoTurret aTurret, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassTurretActions);


        // PreventUpgrade
        bool? CanAffordUpgrade(BasePlayer aPlayer, BuildingBlock aBlock, BuildingGrade.Enum aGrade) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassUpgrade);

        bool? CanChangeGrade(BasePlayer aPlayer, BuildingBlock aBlock, BuildingGrade.Enum aGrade) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassUpgrade);

        bool? OnStructureUpgrade(BaseCombatEntity aEntity, BasePlayer aPlayer, BuildingGrade.Enum aGrade) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassUpgrade);


        // PreventVendingAdmin
        bool? CanAdministerVending(BasePlayer aPlayer, VendingMachine aMachine) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassVendingAdmin);

        bool? OnRotateVendingMachine(VendingMachine aMachine, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassVendingAdmin);


        // PreventVendingUsage
        bool? CanUseVending(VendingMachine aMachine, BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassVendingUsage);

        bool? OnBuyVendingItem(VendingMachine aMachine, BasePlayer aPlayer, int aSellOrderId, int aNumberOfTransactions) =>
            CheckAndReturnNullOrFalse(aPlayer, CPermBypassVendingUsage);

        bool? OnVendingTransaction(VendingMachine aMachine, BasePlayer aBuyer, int aSellOrderId, int aNumberOfTransactions) =>
            CheckAndReturnNullOrFalse(aBuyer, CPermBypassVendingUsage);


        // PreventWeaponFiring
        void OnWeaponFired(BaseProjectile aProjectile, BasePlayer aPlayer, ItemModProjectile aMod, ProtoBuf.ProjectileShoot aProjectiles)
        {
            if (CheckAndReturnNullOrFalse(aPlayer, CPermBypassWeaponFiring) != null)
                aProjectiles.projectiles.Clear();
        }


        // PreventWiring
        bool? CanUseWires(BasePlayer aPlayer) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassWiring);


        // PreventWoodCutting
        bool? CanTakeCutting(BasePlayer aPlayer, GrowableEntity aEntity) => CheckAndReturnNullOrFalse(aPlayer, CPermBypassWoodCutting);


        // PreventWorldProjectiles
        bool? CanCreateWorldProjectile(HitInfo aInfo, ItemDefinition aItemDef) =>
            CheckAndReturnNullOrFalse(aInfo.InitiatorPlayer, CPermBypassWorldProjectiles);

        bool? OnCreateWorldProjectile(HitInfo aInfo, Item aItem) =>
            CheckAndReturnNullOrFalse(aInfo.InitiatorPlayer, CPermBypassWorldProjectiles);


        // PreventWounded
        bool? CanBeWounded(BasePlayer aPlayer, HitInfo aInfo) =>
            CheckAndReturnNullOrFalse(aInfo.InitiatorPlayer, CPermBypassWounded);
        #endregion Hooks
    }
}
