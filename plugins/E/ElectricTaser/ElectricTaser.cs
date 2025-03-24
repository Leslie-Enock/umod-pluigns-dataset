using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Rust;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Electric Taser", "ZockiRR", "2.0.5")]
    [Description("Gives players the ability to spawn a taser")]
    class ElectricTaser : CovalencePlugin
    {

        #region Variables
        private const string PERMISSION_GIVETASER = "electrictaser.givetaser";
        private const string PERMISSION_REMOVEALLTASERS = "electrictaser.removealltasers";
        private const string PERMISSION_TASENPC = "electrictaser.tasenpc";
        private const string PERMISSION_USETASER = "electrictaser.usetaser";

        private const string I18N_NO_PLAYER_FOR_NAME = "NoPlayerForName";
        private const string I18N_MULTIPLE_PLAYERS_FOR_NAME = "MultiplePlayersForName";
        private const string I18N_GAVE_TASER_TO = "GaveTaserTo";
        private const string I18N_GAVE_TASER_TO_YOU = "GaveTaserToYou";
        private const string I18N_COULD_NOT_SPAWN = "CouldNotSpawn";
        private const string I18N_TASER = "Taser";
        private const string I18N_PLAYERS_ONLY = "PlayersOnly";
        private const string I18N_CANNOT_MOVE_ITEM = "CannotMoveItem";
        private const string I18N_NOT_ALLOWED_TO_USE = "NotAllowedToUse";
        private const string I18N_REMOVED_ALL_TASERS = "RemovedAllTasers";

        #endregion Variables

        #region Data
        private class DataContainer
        {
            // Set Nailgun.net.ID
            public HashSet<ulong> NailgunIDs = new HashSet<ulong>();
        }
        #endregion Data

        #region Configuration
        private Configuration config;

        private class Configuration
        {
            [JsonProperty("TaserCooldown")]
            public float TaserCooldown = 5f;

            [JsonProperty("TaserDistance")]
            public float TaserDistance = 8f;

            [JsonProperty("TaserShockDuration")]
            public float TaserShockDuration = 20f;

            [JsonProperty("TaserDamage")]
            public float TaserDamage = 0f;

            [JsonProperty("NoUsePermissionDamage")]
            public float NoUsePermissionDamage = 20f;

            [JsonProperty("InstantKillsNPCs")]
            public bool InstantKillsNPCs = false;

            [JsonProperty("NPCBeltLocked")]
            public bool NPCBeltLocked = true;

            [JsonProperty("NPCWearLocked")]
            public bool NPCWearLocked = true;

            [JsonProperty("ItemNailgun")]
            public string ItemNailgun = "pistol.nailgun";

            [JsonProperty("PrefabScream")]
            public string PrefabScream = "assets/bundled/prefabs/fx/player/gutshot_scream.prefab";

            [JsonProperty("PrefabShock")]
            public string PrefabShock = "assets/prefabs/locks/keypad/effects/lock.code.shock.prefab";

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }

                if (!config.ToDictionary().Keys.SequenceEqual(Config.ToDictionary(x => x.Key, x => x.Value).Keys))
                {
                    PrintWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch
            {
                PrintWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            PrintWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        #endregion Configuration

        #region localization
        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [I18N_NO_PLAYER_FOR_NAME] = "No players found with name or ID '{0}'",
                [I18N_MULTIPLE_PLAYERS_FOR_NAME] = "Multiple players were found, please specify: {0}",
                [I18N_GAVE_TASER_TO] = "Gave taser to {0}",
                [I18N_GAVE_TASER_TO_YOU] = "Gave taser to you",
                [I18N_COULD_NOT_SPAWN] = "Could not spawn a taser",
                [I18N_TASER] = "Taser",
                [I18N_PLAYERS_ONLY] = "Command '{0}' can only be used by a player",
                [I18N_CANNOT_MOVE_ITEM] = "Cannot move item!",
                [I18N_NOT_ALLOWED_TO_USE] = "You can't use the taser!",
                [I18N_REMOVED_ALL_TASERS] = "Removed all tasers from the server"
            }, this);
        }
        #endregion localization

        #region commands
        [Command("givetaser", "givetazer"), Permission(PERMISSION_GIVETASER)]
        private void GiveTaser(IPlayer aPlayer, string aCommand, string[] someArgs)
        {
            IPlayer thePlayer = someArgs.Length > 0 ? FindPlayer(someArgs[0], aPlayer) : aPlayer;
            if (thePlayer == null)
            {
                return;
            }
            if (thePlayer.IsServer)
            {
                Message(aPlayer, I18N_PLAYERS_ONLY, aCommand);
                return;
            }
            BasePlayer thePlayerEntity = thePlayer.Object as BasePlayer;
            if (!thePlayerEntity)
            {
                return;
            }
            Item theItem = GiveItemToPlayer(thePlayerEntity, config.ItemNailgun);
            if (theItem == null)
            {
                Message(aPlayer, I18N_COULD_NOT_SPAWN);
                return;
            }
            EnableTaserBehaviour(theItem.GetHeldEntity().GetComponent<BaseProjectile>());
            if (thePlayer == aPlayer)
            {
                Message(aPlayer, I18N_GAVE_TASER_TO_YOU);
            }
            else
            {
                Message(aPlayer, I18N_GAVE_TASER_TO, thePlayer.Name);
            }
        }

        [Command("removealltasers", "removealltazers"), Permission(PERMISSION_REMOVEALLTASERS)]
        private void RemoveAllTasers(IPlayer aPlayer, string aCommand, string[] someArgs)
        {
            foreach (DroppedItem eachItem in BaseNetworkable.serverEntities.OfType<DroppedItem>())
            {
                Item theItem = eachItem.GetItem();
                if (theItem.GetHeldEntity()?.GetComponent<TaserController>())
                {
                    theItem.Remove();
                }
            }

            foreach (BaseProjectile eachProjectile in BaseNetworkable.serverEntities.OfType<BaseProjectile>())
            {
                if (eachProjectile.GetComponent<TaserController>())
                {
                    eachProjectile.GetItem()?.Remove();
                    if (!eachProjectile.IsDestroyed)
                    {
                        eachProjectile.Kill();
                    }
                }
            }

            Message(aPlayer, I18N_REMOVED_ALL_TASERS);
        }
        #endregion commands

        #region hooks
        private void Unload()
        {
            OnServerSave();

            foreach (BaseProjectile eachProjectile in BaseNetworkable.serverEntities.OfType<BaseProjectile>())
            {
                if (eachProjectile.GetComponent<TaserController>())
                {
                    DisableTaserBehaviour(eachProjectile);
                }
            }

            foreach (BasePlayer eachPlayer in BaseNetworkable.serverEntities.OfType<BasePlayer>())
            {
                ShockedController theController = eachPlayer.GetComponent<ShockedController>();
                if (theController)
                {
                    UnityEngine.Object.Destroy(theController);
                }
            }
        }

        private void OnServerSave()
        {
            DataContainer thePersistentData = new DataContainer();
            foreach (BaseProjectile eachTaser in BaseNetworkable.serverEntities.OfType<BaseProjectile>())
            {
                TaserController theController = eachTaser.GetComponent<TaserController>();
                if (theController)
                {
                    thePersistentData.NailgunIDs.Add(eachTaser.net.ID.Value);
                }
            }
            Interface.Oxide.DataFileSystem.WriteObject(Name, thePersistentData);
        }

        private void OnServerInitialized(bool anInitialFlag)
        {
            permission.RegisterPermission(PERMISSION_TASENPC, this);
            permission.RegisterPermission(PERMISSION_USETASER, this);

            // Readd Behaviour
            DataContainer thePersistentData = Interface.Oxide.DataFileSystem.ReadObject<DataContainer>(Name);
            foreach (ulong eachNailgunID in thePersistentData.NailgunIDs)
            {
                BaseProjectile theTaser = BaseNetworkable.serverEntities.Find(new NetworkableId(eachNailgunID))?.GetComponent<BaseProjectile>();
                if (theTaser)
                {
                    EnableTaserBehaviour(theTaser);
                }
            }
        }

        private void OnWeaponFired(BaseProjectile aProjectile, BasePlayer aPlayer, ItemModProjectile aMod, ProtoBuf.ProjectileShoot aProjectileProtoBuf)
        {
            TaserController theController = aProjectile.GetComponent<TaserController>();
            if (theController)
            {
                theController.ResetTaser();
                if (!permission.UserHasPermission(aPlayer.UserIDString, PERMISSION_USETASER))
                {
                    Effect.server.Run(config.PrefabShock, aProjectile, StringPool.Get(aProjectile.MuzzleTransform.name), aProjectile.MuzzleTransform.localPosition, Vector3.zero);
                    aPlayer.OnAttacked(new HitInfo(aPlayer, aPlayer, DamageType.ElectricShock, config.NoUsePermissionDamage, aPlayer.transform.position + aPlayer.transform.forward * 1f));
                    Message(aPlayer, I18N_NOT_ALLOWED_TO_USE);
                }
            }
        }

        private object CanCreateWorldProjectile(HitInfo anInfo, ItemDefinition anItemDefinition)
        {
            if (anInfo.Weapon.GetComponent<TaserController>())
            {
                return false;
            }
            return null;
        }

        private object OnEntityTakeDamage(BaseCombatEntity anEntity, HitInfo aHitInfo)
        {
            if (!aHitInfo?.Weapon?.GetComponent<TaserController>())
            {
                return null;
            }

            aHitInfo.damageTypes.Clear();
            aHitInfo.DoHitEffects = false;
            aHitInfo.DoDecals = false;

            if (aHitInfo.InitiatorPlayer && !permission.UserHasPermission(aHitInfo.InitiatorPlayer.UserIDString, PERMISSION_USETASER))
            {
                return true;
            }

            float theDistance = !aHitInfo.IsProjectile() ? Vector3.Distance(aHitInfo.PointStart, aHitInfo.HitPositionWorld) : aHitInfo.ProjectileDistance;
            if (config.TaserDistance > 0f && theDistance > config.TaserDistance)
            {
                aHitInfo.DidHit = false;
                return true;
            }
            Effect.server.Run(config.PrefabShock, anEntity, aHitInfo.HitBone, aHitInfo.HitPositionLocal, aHitInfo.HitNormalLocal);
            aHitInfo.damageTypes.Add(DamageType.ElectricShock, config.TaserDamage);
            BasePlayer thePlayer = anEntity?.GetComponent<BasePlayer>();
            if (thePlayer)
            {
                if (thePlayer.IsNpc)
                {
                    if (aHitInfo.InitiatorPlayer && !permission.UserHasPermission(aHitInfo.InitiatorPlayer.UserIDString, PERMISSION_TASENPC))
                    {
                        return null;
                    }

                    if (config.InstantKillsNPCs)
                    {
                        thePlayer.Die(aHitInfo);
                        return null;
                    }

                    if (config.NPCBeltLocked)
                    {
                        thePlayer.inventory.containerBelt.SetLocked(true);
                    }
                    if (config.NPCWearLocked)
                    {
                        thePlayer.inventory.containerWear.SetLocked(true);
                    }
                }

                ShockedController theController = thePlayer.GetComponent<ShockedController>();
                if (!theController)
                {
                    theController = thePlayer.gameObject.AddComponent<ShockedController>();
                    theController.Config = config;
                }
                NextFrame(() => theController.Shock(aHitInfo));
            }
            return null;
        }

        private object CanMoveItem(Item anItem, PlayerInventory aPlayerLoot, uint aTargetContainer, int aTargetSlot, int anAmount)
        {
            if (anItem.GetRootContainer()?.IsLocked() ?? false && (anItem.GetOwnerPlayer()?.GetComponent<ShockedController>()?.IsShocked ?? false))
            {
                Message(aPlayerLoot.GetComponentInParent<BasePlayer>(), I18N_CANNOT_MOVE_ITEM);
                return ItemContainer.CanAcceptResult.CannotAccept;
            }
            return null;
        }

        private object OnPlayerRecover(BasePlayer aPlayer)
        {
            ShockedController theController = aPlayer.GetComponent<ShockedController>();
            if (theController)
            {
                if (aPlayer.IsNpc)
                {
                    if (aPlayer.inventory.containerBelt.IsLocked())
                    {
                        aPlayer.inventory.containerBelt.SetLocked(false);
                    }
                    if (aPlayer.inventory.containerWear.IsLocked())
                    {
                        aPlayer.inventory.containerWear.SetLocked(false);
                    }
                }
                theController.IsShocked = false;
            }
            return null;
        }

        private void OnItemDropped(Item anItem, BaseEntity aWorldEntity)
        {
            if (anItem.GetHeldEntity()?.GetComponent<TaserController>())
            {
                anItem.name = null;
            }
        }

        private object OnItemPickup(Item anItem, BasePlayer aPlayer)
        {
            if (anItem.GetHeldEntity()?.GetComponent<TaserController>())
            {
                anItem.name = GetText(I18N_TASER, aPlayer.UserIDString);
            }
            return null;
        }
        #endregion hooks

        #region methods
        private Item GiveItemToPlayer(BasePlayer aPlayer, string anItemName)
        {
            Item theItem = ItemManager.Create(ItemManager.FindItemDefinition(anItemName.ToLower()));
            if (theItem == null)
            {
                return null;
            }
            if (!aPlayer.inventory.GiveItem(theItem))
            {
                theItem.Remove();
                return null;
            }
            return theItem;
        }

        private void EnableTaserBehaviour(BaseProjectile aBaseProjectile)
        {
            Item theItem = aBaseProjectile.GetItem();
            if (theItem != null)
            {
                theItem.name = GetText(I18N_TASER, theItem.GetOwnerPlayer()?.UserIDString);
            }
            aBaseProjectile.canUnloadAmmo = false;
            aBaseProjectile.primaryMagazine.contents = 1;
            aBaseProjectile.primaryMagazine.capacity = 0;
            TaserController theController = aBaseProjectile.GetComponent<TaserController>();
            if (theController)
            {
                UnityEngine.Object.Destroy(theController);
            }
            aBaseProjectile.gameObject.AddComponent<TaserController>().Config = config;
            aBaseProjectile.SendNetworkUpdateImmediate();
        }

        private void DisableTaserBehaviour(BaseProjectile aBaseProjectile)
        {
            Item theItem = aBaseProjectile.GetItem();
            if (theItem != null)
            {
                theItem.name = null;
            }
            aBaseProjectile.canUnloadAmmo = true;
            aBaseProjectile.primaryMagazine.contents = 0;
            aBaseProjectile.primaryMagazine.capacity = 16;
            TaserController theController = aBaseProjectile.GetComponent<TaserController>();
            if (theController)
            {
                UnityEngine.Object.Destroy(theController);
            }
            aBaseProjectile.SendNetworkUpdateImmediate();
        }
        #endregion methods

        #region helpers
        private IPlayer FindPlayer(string aPlayerNameOrId, IPlayer aPlayer)
        {
            IPlayer[] theFoundPlayers = players.FindPlayers(aPlayerNameOrId).ToArray();
            if (theFoundPlayers.Length > 1)
            {
                Message(aPlayer, I18N_MULTIPLE_PLAYERS_FOR_NAME, string.Join(", ", theFoundPlayers.Select(p => p.Name).Take(10).ToArray()).Truncate(60));
                return null;
            }

            IPlayer theFoundPlayer = theFoundPlayers.Length == 1 ? theFoundPlayers[0] : null;
            if (theFoundPlayer == null)
            {
                Message(aPlayer, I18N_NO_PLAYER_FOR_NAME, aPlayerNameOrId);
                return null;
            }

            return theFoundPlayer;
        }

        private string GetText(string aKey, string aPlayerId = null, params object[] someArgs) => string.Format(lang.GetMessage(aKey, this, aPlayerId), someArgs);

        private void Message(IPlayer aPlayer, string anI18nKey, params object[] someArgs)
        {
            if (aPlayer.IsConnected)
            {
                string theText = GetText(anI18nKey, aPlayer.Id, someArgs);
                aPlayer.Reply(theText != anI18nKey ? theText : anI18nKey);
            }
        }

        private void Message(BasePlayer aPlayer, string anI18nKey, params object[] someArgs)
        {
            if (aPlayer.IsConnected)
            {
                string theText = GetText(anI18nKey, aPlayer.UserIDString, someArgs);
                aPlayer.ChatMessage(theText != anI18nKey ? theText : anI18nKey);
            }
        }
        #endregion helpers

        #region controllers
        private class TaserController : FacepunchBehaviour
        {
            public Configuration Config { get; set; }
            private BaseProjectile Taser
            {
                get
                {
                    if (taser == null)
                    {
                        taser = GetComponent<BaseProjectile>();
                    }
                    return taser;
                }
            }

            private BaseProjectile taser;

            public void ResetTaser()
            {
                Invoke(() =>
                {
                    Taser.primaryMagazine.contents = 1;
                    Taser.SendNetworkUpdateImmediate();
                }, Config.TaserCooldown);
            }
        }

        private class ShockedController : FacepunchBehaviour
        {
            public Configuration Config { get; set; }
            public bool IsShocked { get; set; }
            private BasePlayer Player
            {
                get
                {
                    if (player == null)
                    {
                        player = GetComponent<BasePlayer>();
                    }
                    return player;
                }
            }

            private BasePlayer player;

            public void Shock(HitInfo aHitInfo)
            {
                Effect.server.Run(Config.PrefabScream, Player.transform.position);
                if (!Player.IsSleeping())
                {
                    IsShocked = true;
                    float theHealth = Player.health;
                    Player.GoToIncapacitated(aHitInfo);
                    Player.health = theHealth;
                    Player.woundedDuration = Config.TaserShockDuration + 5f;
                    CancelInvoke(StopWounded);
                    Invoke(StopWounded, Config.TaserShockDuration);
                }
            }

            private void StopWounded()
            {
                if (Player.IsWounded())
                {
                    Player.StopWounded();
                }
            }
        }
        #endregion controllers
    }
}
