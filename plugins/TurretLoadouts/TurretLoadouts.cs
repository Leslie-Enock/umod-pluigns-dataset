using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Oxide.Core.Plugins;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Turret Loadouts", "WhiteThunder", "1.2.0")]
    [Description("Automatically fills turrets with weapons, attachments and ammo, using configurable loadouts.")]
    internal class TurretLoadouts : CovalencePlugin
    {
        #region Fields

        private const int LoadoutNameMaxLength = 20;

        private const string Permission_AutoToggle = "turretloadouts.autotoggle";
        private const string Permission_AutoToggleSamSite = "turretloadouts.autotoggle.samsite";
        private const string Permission_Manage = "turretloadouts.manage";
        private const string Permission_ManageCustom = "turretloadouts.manage.custom";

        private const string Permission_RulesetPrefix = "turretloadouts.ruleset";
        private const string Permission_DefaultLoadoutPrefix = "turretloadouts.default";
        private const string Permission_DefaultFlameTurretLoadoutPrefix = "turretloadouts.flameturret.default";
        private const string Permission_DefaultShotgunTrapLoadoutPrefix = "turretloadouts.shotguntrap.default";
        private const string Permission_DefaultSamSiteLoadoutPrefix = "turretloadouts.samsite.default";

        private readonly object False = false;

        private readonly Dictionary<string, PlayerData> _playerDataCache = new();

        private Configuration _config;

        #endregion

        #region Hooks

        private void Init()
        {
            permission.RegisterPermission(Permission_AutoToggle, this);
            permission.RegisterPermission(Permission_AutoToggleSamSite, this);
            permission.RegisterPermission(Permission_Manage, this);
            permission.RegisterPermission(Permission_ManageCustom, this);

            _config.Init(this);

            if (!_config.LockAutoFilledTurrets)
            {
                Unsubscribe(nameof(OnTurretToggle));
                Unsubscribe(nameof(CanMoveItem));
                Unsubscribe(nameof(OnDropContainerEntity));
            }
        }

        private void OnServerInitialized()
        {
            // Update locked turrets so they can be picked up and don't drop loot.
            // This is done even if the config option for locked turrets is off
            // because there could be locked turrets lingering from a previous configuration.
            // TBD if there are other plugins locking inventories which could conflict.
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                if (entity is AutoTurret or SamSite)
                {
                    var container = entity as ContainerIOEntity;
                    if (IsLocked(container))
                    {
                        SetupLockedContainer(container);
                    }
                }
                else if (entity is FlameTurret or GunTrap)
                {
                    var container = entity as StorageContainer;
                    if (IsLocked(container))
                    {
                        SetupLockedContainer(container);
                    }
                }
            }
        }

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            var ownerPlayer = plan?.GetOwnerPlayer();
            if (ownerPlayer == null)
                return;

            var entity = go?.ToBaseEntity();
            if (entity == null)
                return;

            FillTurretLikeEntity(ownerPlayer, entity);
        }

        private void OnEntityPickedUp(StorageContainer container)
        {
            if (container is GunTrap or FlameTurret && IsLocked(container))
            {
                container.inventory.Clear();
            }
        }

        private void OnTurretToggle(AutoTurret turret)
        {
            // Remove items if powering down while locked and out of ammo
            // Otherwise, the turret would be unusable other than picking it up
            if (turret != null && turret.IsOnline() && IsLocked(turret) && GetTotalAmmo(turret) == 0)
            {
                turret.inventory.Clear();
                turret.inventory.SetLocked(false);
            }
        }

        private object CanMoveItem(Item item)
        {
            // Fix issue where right-clicking an item in a locked turret inventory allows moving it.
            var containerEntity = item.parent?.entityOwner;
            if (containerEntity is AutoTurret or SamSite && item.parent.IsLocked())
                return False;

            return null;
        }

        // Compatibility with plugin: Remover Tool (RemoverTool)
        private object OnDropContainerEntity(ContainerIOEntity container)
        {
            // Prevent Remover Tool from explicitly dropping the inventory
            if (container is AutoTurret or SamSite && IsLocked(container))
                return False;

            return null;
        }

        // Compatibility with plugin: Remover Tool (RemoverTool)
        private object OnDropContainerEntity(StorageContainer container)
        {
            // Prevent Remover Tool from explicitly dropping the inventory
            if (container is FlameTurret or GunTrap && IsLocked(container))
                return False;

            return null;
        }

        #endregion

        #region API

        [HookMethod(nameof(API_FillTurret))]
        public void API_FillTurret(BasePlayer player, BaseEntity turret)
        {
            FillTurretLikeEntity(player, turret);
        }

        #endregion

        #region Exposed Hooks

        private static class ExposedHooks
        {
            public static object OnTurretLoadoutFill(BasePlayer player, BaseEntity turret)
            {
                return Interface.CallHook("OnTurretLoadoutFill", player, turret);
            }
        }

        #endregion

        #region Commands

        [Command("tl")]
        private void MainCommand(IPlayer player, string cmd, string[] args)
        {
            if (player.IsServer)
                return;

            if (args.Length == 0)
            {
                SubCommandDefault(player);
                return;
            }

            switch (args[0].ToLower())
            {
                case "help":
                    SubCommandHelp(player);
                    return;

                case "list":
                    SubCommandList(player);
                    return;

                case "save":
                    SubCommandSave(player, args.Skip(1).ToArray());
                    return;

                case "update":
                    SubCommandUpdate(player, args.Skip(1).ToArray());
                    return;

                case "rename":
                    SubCommandRename(player, args.Skip(1).ToArray());
                    return;

                case "delete":
                    SubCommandDelete(player, args.Skip(1).ToArray());
                    return;

                default:
                    SubCommandActivate(player, args);
                    return;
            }
        }

        private void SubCommandDefault(IPlayer player)
        {
            if (!VerifyPermissionAny(player, Permission_Manage, Permission_ManageCustom))
                return;

            var sb = new StringBuilder();

            var loadout = GetPlayerActiveLoadout(player.Id);
            if (loadout == null)
            {
                sb.AppendLine(GetMessage(player, "Command.Default.NoActive"));
            }
            else
            {
                sb.AppendLine(GetMessage(player, "Command.Default.Active", GetLoadoutDisplayName(loadout, player.Id)));
                sb.Append(PrintLoadoutDetails(player, loadout));
                sb.AppendLine();
            }

            sb.AppendLine(GetMessage(player, "Command.Default.HelpHint"));
            player.Reply(sb.ToString());
        }

        private void SubCommandHelp(IPlayer player)
        {
            if (!VerifyPermissionAny(player, Permission_Manage, Permission_ManageCustom))
                return;

            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, "Generic.Header"));
            sb.AppendLine(GetMessage(player, "Command.Help.Details"));
            sb.AppendLine(GetMessage(player, "Command.Help.List"));
            sb.AppendLine(GetMessage(player, "Command.Help.Activate"));

            if (player.HasPermission(Permission_ManageCustom))
            {
                sb.AppendLine(GetMessage(player, "Command.Help.Save"));
                sb.AppendLine(GetMessage(player, "Command.Help.Update"));
                sb.AppendLine(GetMessage(player, "Command.Help.Rename"));
                sb.AppendLine(GetMessage(player, "Command.Help.Delete"));
            }

            player.Reply(sb.ToString());
        }

        private void SubCommandList(IPlayer player)
        {
            if (!VerifyPermissionAny(player, Permission_Manage, Permission_ManageCustom))
                return;

            var playerData = GetPlayerData(player);

            // Prune loadouts that are no longer valid
            // For example, if the player no longer has permission to the weapon type
            playerData.RestrictAndPruneLoadouts(GetPlayerLoadoutRuleset(player));

            var defaultLoadout = GetPlayerLastAllowedProfile(_config.DefaultLoadouts, player.Id);
            if (playerData.Loadouts.Count == 0 && defaultLoadout == null)
            {
                ReplyToPlayer(player, "Command.List.NoLoadouts");
                return;
            }

            var loadouts = playerData.Loadouts.ToArray();
            Array.Sort(loadouts, SortLoadoutNames);

            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, "Generic.Header"));

            if (defaultLoadout != null)
            {
                AddListItem(sb, player, defaultLoadout, playerData.ActiveLoadout);
            }

            foreach (var loadout in loadouts)
            {
                AddListItem(sb, player, loadout, playerData.ActiveLoadout);
            }

            sb.AppendLine();
            sb.AppendLine(GetMessage(player, "Command.List.ToggleHint"));

            player.Reply(sb.ToString());
        }

        private void AddListItem(StringBuilder sb, IPlayer player, TurretLoadout loadout, string activeLoadout)
        {
            var weaponDefinition = ItemManager.FindItemDefinition(loadout.Weapon);
            var activeString = loadout.IsDefault && activeLoadout == null || activeLoadout == loadout.Name ? GetMessage(player, "Command.List.Item.Active") : string.Empty;

            var attachmentAbbreviations = AbbreviateAttachments(player, loadout);
            var attachmentsString = attachmentAbbreviations == null
                ? string.Empty
                : $" ({string.Join(", ", attachmentAbbreviations)})";

            sb.AppendLine(GetMessage(player, "Command.List.Item", activeString, GetLoadoutDisplayName(loadout, player.Id), weaponDefinition.displayName.translated, attachmentsString));
        }

        private IEnumerable<string> AbbreviateAttachments(IPlayer player, TurretLoadout loadout)
        {
            if (loadout.Attachments == null || loadout.Attachments.Count == 0)
                return null;

            return loadout.Attachments.Select(attachmentName =>
            {
                var langKey = $"Abbreviation.{attachmentName}";
                var abbreviated = GetMessage(player, langKey);
                return abbreviated == langKey ? attachmentName : abbreviated;
            });
        }

        private void SubCommandSave(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, Permission_ManageCustom))
                return;

            if (args.Length == 0)
            {
                ReplyToPlayer(player, "Command.Save.Error.Syntax");
                return;
            }

            var loadoutName = args[0];
            if (!VerifyLoadoutNameLength(player, loadoutName))
                return;

            if (MatchesDefaultLoadoutName(player, loadoutName))
            {
                ReplyToPlayer(player, "Generic.Error.DefaultLoadout");
                return;
            }

            var playerData = GetPlayerData(player);
            var loadoutRuleset = GetPlayerLoadoutRuleset(player);
            playerData.RestrictAndPruneLoadouts(loadoutRuleset);

            if (playerData.HasLoadout(loadoutName))
            {
                ReplyToPlayer(player, "Command.Save.Error.LoadoutExists", loadoutName);
                return;
            }

            if (playerData.Loadouts.Count >= _config.MaxLoadoutsPerPlayer)
            {
                ReplyToPlayer(player, "Command.Save.Error.TooManyLoadouts", _config.MaxLoadoutsPerPlayer);
                return;
            }

            var basePlayer = player.Object as BasePlayer;

            if (!VerifyTurretFound(basePlayer, out var turret) ||
                !VerifyTurretLoadoutValid(player, turret, out var loadout))
                return;

            var disallowedItems = new Dictionary<string, int>();
            if (playerData.ValidateAndPossiblyReduceLoadout(loadout, loadoutRuleset, disallowedItems) == LoadoutManager.ValidationResult.DisallowedWeapon)
            {
                var itemDefinition = ItemManager.FindItemDefinition(loadout.Weapon);
                ReplyToPlayer(player, "Generic.Error.WeaponNotAllowed", itemDefinition.displayName.translated);
                return;
            }

            loadout.Name = loadoutName;
            playerData.SaveLoadout(loadout);

            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, "Command.Save.Success", loadoutName));
            sb.Append(PrintLoadoutDetails(player, loadout));

            if (!disallowedItems.IsEmpty())
            {
                sb.Append(PrintDisallowedItems(player, disallowedItems));
            }

            player.Reply(sb.ToString());
        }

        private void SubCommandUpdate(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, Permission_ManageCustom))
                return;

            if (args.Length < 1)
            {
                ReplyToPlayer(player, "Command.Update.Error.Syntax");
                return;
            }

            var loadoutName = args[0];
            if (MatchesDefaultLoadoutName(player, loadoutName))
            {
                ReplyToPlayer(player, "Generic.Error.DefaultLoadout");
                return;
            }

            var basePlayer = player.Object as BasePlayer;

            if (!VerifyTurretFound(basePlayer, out var turret) ||
                !VerifyTurretLoadoutValid(player, turret, out var newLoadout))
                return;

            var playerData = GetPlayerData(player);
            var loadoutPermission = GetPlayerLoadoutRuleset(player);
            playerData.RestrictAndPruneLoadouts(loadoutPermission);

            if (!VerifyHasLoadout(player, loadoutName, out _))
                return;

            var disallowedItems = new Dictionary<string, int>();
            if (playerData.ValidateAndPossiblyReduceLoadout(newLoadout, loadoutPermission, disallowedItems) == LoadoutManager.ValidationResult.DisallowedWeapon)
            {
                var itemDefinition = ItemManager.FindItemDefinition(newLoadout.Weapon);
                ReplyToPlayer(player, "Generic.Error.WeaponNotAllowed", itemDefinition.displayName.translated);
                return;
            }

            newLoadout.Name = loadoutName;
            GetPlayerData(player).TryUpdateLoadout(newLoadout);

            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, "Command.Update.Success", loadoutName));
            sb.Append(PrintLoadoutDetails(player, newLoadout));

            if (!disallowedItems.IsEmpty())
            {
                sb.Append(PrintDisallowedItems(player, disallowedItems));
            }

            player.Reply(sb.ToString());
        }

        private string PrintDisallowedItems(IPlayer player, Dictionary<string, int> disallowedItems)
        {
            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, "Generic.RestrictedItems"));
            foreach (var entry in disallowedItems)
            {
                sb.AppendLine($"  {entry.Value} {GetItemDisplayName(entry.Key)}");
            }

            return sb.ToString();
        }

        private string PrintLoadoutDetails(IPlayer player, TurretLoadout loadout)
        {
            var sb = new StringBuilder();
            sb.AppendLine(GetMessage(player, loadout.Peacekeeper ? "Command.Default.Mode.Peacekeeper" : "Command.Default.Mode.AttackAll"));

            var ammoString = loadout.Ammo != null && loadout.Ammo.Amount > 0
                ? $" ({loadout.Ammo.Amount} {GetItemDisplayName(loadout.Ammo.Name)})"
                : string.Empty;

            sb.AppendLine(GetMessage(player, "Command.Default.Weapon", GetItemDisplayName(loadout.Weapon), ammoString));

            if (loadout.Attachments != null && loadout.Attachments.Count > 0)
            {
                sb.AppendLine(GetMessage(player, "Command.Default.Attachments"));
                foreach (var attachmentName in loadout.Attachments)
                {
                    sb.AppendLine($"  {GetItemDisplayName(attachmentName)}");
                }
            }

            if (loadout.ReserveAmmo != null && !loadout.ReserveAmmo.IsEmpty())
            {
                sb.AppendLine(GetMessage(player, "Command.Default.ReserveAmmo"));
                foreach (var ammo in loadout.ReserveAmmo)
                {
                    if (ammo.Amount > 0)
                    {
                        sb.AppendLine($"  {ammo.Amount} {GetItemDisplayName(ammo.Name)}");
                    }
                }
            }

            return sb.ToString();
        }

        private void SubCommandRename(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, Permission_ManageCustom))
                return;

            if (args.Length < 2)
            {
                ReplyToPlayer(player, "Command.Rename.Error.Syntax");
                return;
            }

            var oldName = args[0];
            var newName = args[1];

            if (MatchesDefaultLoadoutName(player, oldName) || MatchesDefaultLoadoutName(player, newName))
            {
                ReplyToPlayer(player, "Generic.Error.DefaultLoadout");
                return;
            }

            if (!VerifyHasLoadout(player, oldName, out var loadout) ||
                !VerifyLoadoutNameLength(player, newName))
                return;

            var playerData = GetPlayerData(player);
            var existingLoadoutWithNewName = playerData.FindByName(newName);

            // Allow renaming if just changing case
            if (existingLoadoutWithNewName != null && loadout != existingLoadoutWithNewName)
            {
                ReplyToPlayer(player, "Command.Rename.Error.LoadoutNameTaken", existingLoadoutWithNewName.Name);
                return;
            }

            var actualOldLoadoutName = loadout.Name;
            playerData.RenameLoadout(loadout, newName);

            if (playerData.ActiveLoadout == actualOldLoadoutName)
            {
                playerData.ActiveLoadout = newName;
            }

            ReplyToPlayer(player, "Command.Rename.Success", actualOldLoadoutName, newName);
        }

        private void SubCommandDelete(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, Permission_ManageCustom))
                return;

            if (args.Length < 1)
            {
                ReplyToPlayer(player, "Command.Delete.Error.Syntax");
                return;
            }

            var loadoutName = args[0];

            if (!VerifyHasLoadout(player, loadoutName, out var loadout))
                return;

            if (loadout.IsDefault)
            {
                ReplyToPlayer(player, "Generic.Error.DefaultLoadout");
                return;
            }

            GetPlayerData(player).DeleteLoadout(loadout);
            ReplyToPlayer(player, "Command.Delete.Success", loadout.Name);
        }

        private void SubCommandActivate(IPlayer player, string[] args)
        {
            if (!VerifyPermissionAny(player, Permission_Manage, Permission_ManageCustom))
                return;

            if (args.Length < 1)
            {
                ReplyToPlayer(player, "Command.Activate.Error.Syntax");
                return;
            }

            var loadoutName = args[0];

            if (!VerifyHasLoadout(player, loadoutName, out var loadout, matchPartial: true))
                return;

            var playerData = GetPlayerData(player);

            if (loadout.IsDefault)
            {
                playerData.ActiveLoadout = playerData.ActiveLoadout == null ? string.Empty : null;
            }
            else
            {
                playerData.ActiveLoadout = playerData.ActiveLoadout == loadout.Name ? string.Empty : loadout.Name;
            }

            playerData.SaveData();

            if (playerData.ActiveLoadout == string.Empty)
            {
                ReplyToPlayer(player, "Command.Activate.Success.Deactivated", GetLoadoutDisplayName(loadout, player.Id));
            }
            else
            {
                var sb = new StringBuilder();
                sb.AppendLine(GetMessage(player, "Command.Default.Active", GetLoadoutDisplayName(loadout, player.Id)));
                sb.Append(PrintLoadoutDetails(player, loadout));
                ReplyToPlayer(player, sb.ToString());
            }
        }

        #endregion

        #region Helper Methods - Command Checks

        private bool VerifyPermissionAny(IPlayer player, params string[] permissionNames)
        {
            if (HasPermissionAny(player, permissionNames))
                return true;

            ReplyToPlayer(player, "Generic.Error.NoPermission");
            return false;
        }

        private bool VerifyTurretFound(BasePlayer player, out AutoTurret turret)
        {
            turret = GetLookEntity(player, 3) as AutoTurret;
            if (turret != null)
                return true;

            ReplyToPlayer(player.IPlayer, "Command.Save.Error.NoTurretFound");
            return false;
        }

        private bool VerifyTurretLoadoutValid(IPlayer player, AutoTurret turret, out TurretLoadout loadout)
        {
            loadout = CreateLoadout(turret);
            if (loadout != null)
                return true;

            ReplyToPlayer(player, "Generic.Error.NoTurretWeapon");
            return false;
        }

        private bool VerifyHasLoadout(IPlayer player, string loadoutName, out TurretLoadout loadout, bool matchPartial = false)
        {
            loadout = MatchesDefaultLoadoutName(player, loadoutName, matchPartial)
                ? GetPlayerLastAllowedProfile(_config.DefaultLoadouts, player.Id)
                : GetPlayerData(player).FindByName(loadoutName, matchPartial);

            if (loadout != null)
                return true;

            ReplyToPlayer(player, "Generic.Error.LoadoutNotFound", loadoutName);
            return false;
        }

        private bool VerifyLoadoutNameLength(IPlayer player, string loadoutName)
        {
            if (loadoutName.Length <= LoadoutNameMaxLength)
                return true;

            ReplyToPlayer(player, "Generic.Error.LoadoutNameLength", LoadoutNameMaxLength);
            return false;
        }

        #endregion

        #region Helper Methods - Turrets

        private static void SetupLockedContainer(ContainerIOEntity container)
        {
            container.dropsLoot = false;
            container.pickup.requireEmptyInv = false;
        }

        private static void SetupLockedContainer(StorageContainer container)
        {
            container.dropsLoot = false;
            container.pickup.requireEmptyInv = false;
        }

        private static bool IsLocked(IItemContainerEntity containerEntity)
        {
            return containerEntity.inventory?.IsLocked() ?? false;
        }

        private static int GetTotalAmmo(AutoTurret turret)
        {
            if (turret == null || turret.inventory == null)
                return 0;

            var weapon = turret.GetAttachedWeapon();
            if (weapon == null)
                return 0;

            // AutoTurret.GetTotalAmmo() only includes the reserve ammo, not the loaded ammo
            return weapon.primaryMagazine.contents + turret.GetTotalAmmo();
        }

        private static TurretLoadout CreateLoadout(AutoTurret turret)
        {
            var heldEntity = turret.AttachedWeapon;
            if (heldEntity == null)
                return null;

            var weaponItem = turret.inventory.GetSlot(0);
            if (weaponItem == null)
                return null;

            var loadout = new TurretLoadout
            {
                Weapon = weaponItem.info.shortname,
                Skin = weaponItem.skin,
                Peacekeeper = turret.PeacekeeperMode()
            };

            if (weaponItem.contents != null)
            {
                var attachments = new List<string>();
                for (var slot = 0; slot < weaponItem.contents.capacity; slot++)
                {
                    var attachmentItem = weaponItem.contents.GetSlot(slot);
                    if (attachmentItem != null)
                    {
                        attachments.Add(attachmentItem.info.shortname);
                    }
                }

                if (attachments.Count > 0)
                {
                    loadout.Attachments = attachments;
                }
            }

            var weapon = heldEntity as BaseProjectile;
            if (weapon != null && weapon.primaryMagazine.contents > 0)
            {
                loadout.Ammo = new AmmoAmount
                {
                    Name = weapon.primaryMagazine.ammoType.shortname,
                    Amount = weapon.primaryMagazine.contents,
                };
            }

            var reserveAmmo = new List<AmmoAmount>();
            for (var slot = 1; slot <= 6; slot++)
            {
                var ammoItem = turret.inventory.GetSlot(slot);
                if (ammoItem is not { amount: > 0 })
                    continue;

                reserveAmmo.Add(new AmmoAmount
                {
                    Name = ammoItem.info.shortname,
                    Amount = ammoItem.amount
                });
            }

            if (reserveAmmo.Count > 0)
            {
                loadout.ReserveAmmo = reserveAmmo;
            }

            return loadout;
        }

        private string GetLoadoutDisplayName(TurretLoadout loadout, string userIdString)
        {
            return loadout.IsDefault ? GetDefaultLoadoutName(userIdString) : loadout.Name;
        }

        private Item AddHeldEntity(AutoTurret turret, BasePlayer ownerPlayer, TurretLoadout loadout)
        {
            var heldItem = ItemManager.CreateByName(loadout.Weapon, 1, loadout.Skin);
            if (heldItem == null)
            {
                LogError($"Weapon '{loadout.Weapon}' is not a valid item. Unable to add weapon to turret for player {ownerPlayer.userID}.");
                return null;
            }

            if (loadout.Attachments != null)
            {
                foreach (var attachmentName in loadout.Attachments)
                {
                    var attachmentItem = ItemManager.CreateByName(attachmentName);
                    if (attachmentItem == null)
                    {
                        LogError($"Attachment '{attachmentName}' is not a valid item. Unable to add to turret weapon for player {ownerPlayer.userID}.");
                    }
                    else if (!attachmentItem.MoveToContainer(heldItem.contents))
                    {
                        attachmentItem.Remove();
                    }
                }
            }

            if (!heldItem.MoveToContainer(turret.inventory, 0))
            {
                heldItem.Remove();
                return null;
            }

            var weapon = heldItem.GetHeldEntity() as BaseProjectile;
            if (weapon != null)
            {
                if (loadout.Attachments != null)
                {
                    weapon.DelayedModsChanged();
                    weapon.CancelInvoke(weapon.DelayedModsChanged);
                }

                // Must unload the weapon first or the turret will unload it and the ammo will temporarily take up inventory space
                weapon.primaryMagazine.contents = 0;
                turret.UpdateAttachedWeapon();
                turret.CancelInvoke(turret.UpdateAttachedWeapon);

                if (loadout.Ammo != null)
                {
                    var loadedAmmoItemDefinition = ItemManager.FindItemDefinition(loadout.Ammo.Name);
                    if (loadedAmmoItemDefinition == null)
                    {
                        LogError($"Ammo type '{loadout.Ammo.Name}' is not a valid item. Unable to add ammo to turret for player {ownerPlayer.userID}.");
                        return heldItem;
                    }

                    weapon.primaryMagazine.ammoType = loadedAmmoItemDefinition;
                    weapon.primaryMagazine.contents = Math.Min(weapon.primaryMagazine.capacity, loadout.Ammo.Amount);
                }
            }

            return heldItem;
        }

        private void AddReserveAmmo(ItemContainer container, BaseLoadout loadout, BasePlayer ownerPlayer, int firstSlot = 0)
        {
            if (loadout.ReserveAmmo == null)
                return;

            var slot = firstSlot;
            var maxSlot = container.capacity - 1;

            foreach (var ammo in loadout.ReserveAmmo)
            {
                if (slot > maxSlot)
                    break;

                if (ammo.Amount <= 0)
                    continue;

                var itemDefinition = ItemManager.FindItemDefinition(ammo.Name);
                if (itemDefinition == null)
                {
                    LogError($"Ammo type '{ammo.Name}' is not a valid item. Unable to add ammo to turret for player {ownerPlayer.userID}.");
                    continue;
                }

                // Allow default loadouts to bypass max stack size
                var amountToAdd = loadout.IsDefault ? ammo.Amount : Math.Min(ammo.Amount, itemDefinition.stackable);
                var ammoItem = ItemManager.Create(itemDefinition, amountToAdd);
                if (!ammoItem.MoveToContainer(container, slot))
                {
                    ammoItem.Remove();
                }

                if (ammoItem.parent != container)
                {
                    // The item was split due to max stack size.
                    if (loadout.IsDefault)
                    {
                        var destinationItem = container.GetSlot(slot);
                        if (destinationItem != null)
                        {
                            destinationItem.amount = amountToAdd;
                            destinationItem.MarkDirty();
                        }
                    }

                    ammoItem.Remove();
                }

                slot++;
            }
        }

        private void FillAutoTurret(BasePlayer player, AutoTurret turret)
        {
            var loadout = GetPlayerActiveLoadout(player.UserIDString);
            if (loadout == null)
                return;

            if (loadout.Peacekeeper)
            {
                turret.SetPeacekeepermode(true);
            }

            var heldItem = AddHeldEntity(turret, player, loadout);
            if (heldItem == null)
                return;

            AddReserveAmmo(turret.inventory, loadout, player, firstSlot: 1);
            turret.UpdateTotalAmmo();
            turret.EnsureReloaded();

            var isInstrument = (heldItem.GetHeldEntity() as HeldEntity)?.IsInstrument() ?? false;
            if ((isInstrument || GetTotalAmmo(turret) > 0) && HasPermissionAny(player, Permission_AutoToggle))
            {
                turret.InitiateStartup();
                var turretSwitch = turret.GetComponentInChildren<ElectricSwitch>();
                if (turretSwitch != null)
                {
                    turretSwitch.SetSwitch(true);
                }
            }

            if (_config.LockAutoFilledTurrets)
            {
                heldItem.contents.SetLocked(true);
                turret.inventory.SetLocked(true);
                SetupLockedContainer(turret);
            }

            if (HasPermissionAny(player, Permission_Manage, Permission_ManageCustom))
            {
                ChatMessage(player, "Generic.FilledFromLoadout", GetLoadoutDisplayName(loadout, player.UserIDString));
            }

            turret.SendNetworkUpdate();
        }

        private void FillSamSite(BasePlayer player, SamSite samSite)
        {
            var loadout = GetPlayerLastAllowedProfile(_config.DefaultSamSiteLoadouts, player.UserIDString);
            if (loadout == null)
                return;

            AddReserveAmmo(samSite.inventory, loadout, player);

            if (HasPermissionAny(player, Permission_AutoToggleSamSite))
            {
                samSite.SetFlag(IOEntity.Flag_HasPower, true);
            }

            if (_config.LockAutoFilledTurrets)
            {
                samSite.inventory.SetLocked(true);
                SetupLockedContainer(samSite);
            }
        }

        private void FillFlameTurret(BasePlayer player, FlameTurret flameTurret)
        {
            var loadout = GetPlayerLastAllowedProfile(_config.DefaultFlameTurretLoadouts, player.UserIDString);
            if (loadout == null)
                return;

            AddReserveAmmo(flameTurret.inventory, loadout, player);
            if (_config.LockAutoFilledTurrets)
            {
                flameTurret.inventory.SetLocked(true);
                SetupLockedContainer(flameTurret);
            }
        }

        private void FillGunTrap(BasePlayer player, GunTrap gunTrap)
        {
            var loadout = GetPlayerLastAllowedProfile(_config.DefaultShotgunTrapLoadouts, player.UserIDString);
            if (loadout == null)
                return;

            AddReserveAmmo(gunTrap.inventory, loadout, player);
            if (_config.LockAutoFilledTurrets)
            {
                gunTrap.inventory.SetLocked(true);
                SetupLockedContainer(gunTrap);
            }
        }

        private void FillTurretLikeEntity(BasePlayer player, BaseEntity entity)
        {
            if (ExposedHooks.OnTurretLoadoutFill(player, entity) is false)
                return;

            switch (entity)
            {
                case AutoTurret turret:
                    FillAutoTurret(player, turret);
                    return;
                case SamSite samSite:
                    FillSamSite(player, samSite);
                    return;
                case FlameTurret flameTurret:
                    FillFlameTurret(player, flameTurret);
                    return;
                case GunTrap gunTrap:
                    FillGunTrap(player, gunTrap);
                    return;
            }
        }

        #endregion

        #region Helper Methods - Misc

        public static void LogWarning(string message) => Interface.Oxide.LogWarning($"[Turret Loadouts] {message}");
        public static void LogError(string message) => Interface.Oxide.LogError($"[Turret Loadouts] {message}");

        private bool HasPermissionAny(BasePlayer basePlayer, params string[] permissionNames)
        {
            return HasPermissionAny(basePlayer.UserIDString, permissionNames);
        }

        private bool HasPermissionAny(IPlayer player, params string[] permissionNames)
        {
            return HasPermissionAny(player.Id, permissionNames);
        }

        private bool HasPermissionAny(string userId, params string[] permissionNames)
        {
            foreach (var perm in permissionNames)
            {
                if (permission.UserHasPermission(userId, perm))
                    return true;
            }

            return false;
        }

        private bool MatchesDefaultLoadoutName(IPlayer player, string loadoutName, bool matchPartial = false)
        {
            var defaultLoadoutName = GetDefaultLoadoutName(player.Id);

            return matchPartial
                ? defaultLoadoutName.IndexOf(loadoutName, StringComparison.CurrentCultureIgnoreCase) >= 0
                : defaultLoadoutName.Equals(loadoutName, StringComparison.CurrentCultureIgnoreCase);
        }

        private string GetDefaultLoadoutName(string userIdString)
        {
            return GetMessage(userIdString, "Generic.DefaultLoadoutName");
        }

        private static int SortLoadoutNames(TurretLoadout a, TurretLoadout b)
        {
            return a.Name.ToLower().CompareTo(b.Name.ToLower());
        }

        private static string GetItemDisplayName(string shortname)
        {
            var itemDefinition = ItemManager.FindItemDefinition(shortname);
            return itemDefinition == null ? shortname : itemDefinition.displayName.translated;
        }

        private static BaseEntity GetLookEntity(BasePlayer basePlayer, int maxDistance)
        {
            return !Physics.Raycast(basePlayer.eyes.HeadRay(), out var hit, maxDistance) ? null : hit.GetEntity();
        }

        private static void AddToDictKey(Dictionary<string, int> dict, string key, int amount)
        {
            if (dict.ContainsKey(key))
            {
                dict[key] += amount;
            }
            else
            {
                dict[key] = amount;
            }
        }

        #endregion

        #region Data Management

        private PlayerData GetPlayerData(IPlayer player)
        {
            return GetPlayerData(player.Id);
        }

        private PlayerData GetPlayerData(string userIdString)
        {
            if (_playerDataCache.TryGetValue(userIdString, out var data))
                return data;

            data = PlayerData.Get(userIdString);
            _playerDataCache[userIdString] = data;
            return data;
        }

        private class PlayerData : LoadoutManager
        {
            public static PlayerData Get(string ownerId)
            {
                var filepath = GetFilepath(ownerId);

                var data = Interface.Oxide.DataFileSystem.ExistsDatafile(filepath) ?
                    Interface.Oxide.DataFileSystem.ReadObject<PlayerData>(filepath) :
                    new PlayerData(ownerId);

                return data;
            }

            private static string GetFilepath(string ownerId)
            {
                return $"{nameof(TurretLoadouts)}/{ownerId}";
            }

            [JsonProperty("OwnerId")]
            public string OwnerId { get; }

            [JsonProperty("ActiveLoadout", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string ActiveLoadout;

            public PlayerData(string ownerId)
            {
                OwnerId = ownerId;
            }

            public override void SaveData()
            {
                Interface.Oxide.DataFileSystem.WriteObject(GetFilepath(OwnerId), this);
            }

            // Remove loadouts where the player no longer has permission to the weapon type
            // Update other loadouts to remove disallowed items
            public void RestrictAndPruneLoadouts(LoadoutRuleset ruleset)
            {
                var changed = false;

                for (var i = 0; i < Loadouts.Count; i++)
                {
                    var loadout = Loadouts[i];
                    var validationResult = ValidateAndPossiblyReduceLoadout(loadout, ruleset);

                    if (validationResult == ValidationResult.InvalidWeapon)
                    {
                        LogWarning($"Removed turret loadout '{loadout.Name}' for player '{OwnerId}' because weapon '{loadout.Weapon}' is not a valid item.");
                    }
                    else if (validationResult == ValidationResult.DisallowedWeapon)
                    {
                        LogWarning($"Removed turret loadout '{loadout.Name}' for player '{OwnerId}' because they are no longer allowed to use loadouts with weapon '{loadout.Weapon}'.");
                    }

                    if (validationResult == ValidationResult.InvalidWeapon || validationResult == ValidationResult.DisallowedWeapon)
                    {
                        Loadouts.RemoveAt(i);
                        i--;
                    }

                    if (validationResult != ValidationResult.Valid)
                    {
                        changed = true;
                    }
                }

                if (changed)
                {
                    SaveData();
                }
            }
        }

        private abstract class LoadoutManager
        {
            public enum ValidationResult { Valid, Changed, InvalidWeapon, DisallowedWeapon }

            private static bool MatchesLoadout(TurretLoadout loadout, string name, bool matchPartial = false)
            {
                return matchPartial
                    ? loadout.Name.IndexOf(name, StringComparison.CurrentCultureIgnoreCase) >= 0
                    : loadout.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase);
            }

            [JsonProperty("Loadouts")]
            public List<TurretLoadout> Loadouts = new();

            public TurretLoadout FindByName(string loadoutName, bool matchPartial = false)
            {
                if (Loadouts == null)
                    return null;

                foreach (var currentLoadout in Loadouts)
                {
                    if (MatchesLoadout(currentLoadout, loadoutName))
                        return currentLoadout;
                }

                if (matchPartial)
                {
                    // Perform partial matching in a second pass so the first pass can get a closer match
                    foreach (var currentLoadout in Loadouts)
                    {
                        if (MatchesLoadout(currentLoadout, loadoutName, matchPartial: true))
                            return currentLoadout;
                    }
                }

                return null;
            }

            public bool HasLoadout(string loadoutName)
            {
                return FindByName(loadoutName) != null;
            }

            public void SaveLoadout(TurretLoadout loadout)
            {
                Loadouts.Add(loadout);
                SaveData();
            }

            public bool TryUpdateLoadout(TurretLoadout newLoadout)
            {
                var existingLoadout = FindByName(newLoadout.Name);
                if (existingLoadout == null)
                    return false;

                Loadouts[Loadouts.IndexOf(existingLoadout)] = newLoadout;
                SaveData();
                return true;
            }

            public void RenameLoadout(TurretLoadout loadout, string newName)
            {
                loadout.Name = newName;
                SaveData();
            }

            public void DeleteLoadout(TurretLoadout loadout)
            {
                Loadouts.Remove(loadout);
                SaveData();
            }

            // Removes items from the loadout not currently allowed by the ruleset
            // The loadout may no longer be valid due to containing an invalid or disallowed weapon
            // This can be determined by checking the validation result
            public ValidationResult ValidateAndPossiblyReduceLoadout(TurretLoadout loadout, LoadoutRuleset loadoutRuleset, Dictionary<string, int> disallowedItems = null)
            {
                disallowedItems ??= new Dictionary<string, int>();

                var weaponDefinition = ItemManager.FindItemDefinition(loadout.Weapon);
                if (weaponDefinition == null)
                    return ValidationResult.InvalidWeapon;

                if (!loadoutRuleset.IsWeaponAllowed(loadout.Weapon))
                {
                    disallowedItems[loadout.Weapon] = 1;
                    return ValidationResult.DisallowedWeapon;
                }

                if (loadout.Attachments != null)
                {
                    for (var i = 0; i < loadout.Attachments.Count; i++)
                    {
                        var attachmentName = loadout.Attachments[i];
                        if (!loadoutRuleset.IsAttachmentAllowed(attachmentName))
                        {
                            disallowedItems[attachmentName] = 1;
                            loadout.Attachments.RemoveAt(i);
                            i--;
                        }
                    }
                }

                var allowedAmmo = loadoutRuleset.AllowedAmmo;
                var countedAmmo = new Dictionary<string, int>();

                // Don't impose ammo limits if allowed ammo is null
                if (loadout.Ammo != null && allowedAmmo != null)
                {
                    var ammo = loadout.Ammo;

                    // Make sure ammo name exists
                    if (ItemManager.FindItemDefinition(ammo.Name) == null)
                    {
                        LogWarning($"Ammo type '{ammo.Name}' is not a valid item. Removing from loadout.");
                        ammo.Amount = 0;
                    }
                    else if (allowedAmmo.ContainsKey(ammo.Name))
                    {
                        var allowedAmount = allowedAmmo[ammo.Name];

                        // Don't impose a limit if the allowed amount is negative
                        if (allowedAmount >= 0 && ammo.Amount > allowedAmount)
                        {
                            // Reduce ammo to the allowed amount
                            AddToDictKey(disallowedItems, ammo.Name, ammo.Amount - allowedAmount);
                            ammo.Amount = allowedAmount;
                        }
                    }
                    else
                    {
                        // Ammo not allowed
                        AddToDictKey(disallowedItems, ammo.Name, ammo.Amount);
                        ammo.Amount = 0;
                    }

                    if (ammo.Amount <= 0)
                    {
                        loadout.Ammo = null;
                    }
                    else
                    {
                        AddToDictKey(countedAmmo, ammo.Name, ammo.Amount);
                    }
                }

                // Don't impose ammo limits if allowed ammo is null
                if (loadout.ReserveAmmo != null && allowedAmmo != null)
                {
                    for (var i = 0; i < loadout.ReserveAmmo.Count; i++)
                    {
                        var ammo = loadout.ReserveAmmo[i];

                        // Make sure ammo name exists
                        if (ItemManager.FindItemDefinition(ammo.Name) == null)
                        {
                            LogWarning($"Ammo type '{ammo.Name}' is not a valid item. Removing from loadout.");
                            ammo.Amount = 0;
                        }
                        else if (allowedAmmo.ContainsKey(ammo.Name))
                        {
                            // Don't impose a limit if the allowed amount is negative
                            if (allowedAmmo[ammo.Name] >= 0)
                            {
                                var countedAmount = countedAmmo.ContainsKey(ammo.Name) ? countedAmmo[ammo.Name] : 0;
                                var remainingAllowedAmount = allowedAmmo[ammo.Name] - countedAmount;

                                if (ammo.Amount > remainingAllowedAmount)
                                {
                                    // Reduce ammo to the allowed amount
                                    AddToDictKey(disallowedItems, ammo.Name, ammo.Amount - remainingAllowedAmount);
                                    ammo.Amount = remainingAllowedAmount;
                                }
                            }
                        }
                        else
                        {
                            // Ammo not allowed
                            AddToDictKey(disallowedItems, ammo.Name, ammo.Amount);
                            ammo.Amount = 0;
                        }

                        if (ammo.Amount <= 0)
                        {
                            loadout.ReserveAmmo.RemoveAt(i);
                            i--;
                        }
                        else
                        {
                            AddToDictKey(countedAmmo, ammo.Name, ammo.Amount);
                        }
                    }
                }

                return disallowedItems.IsEmpty() ? ValidationResult.Valid : ValidationResult.Changed;
            }

            public abstract void SaveData();
        }

        #endregion

        #region Configuration

        private TurretLoadout GetPlayerActiveLoadout(string userIdString)
        {
            if (HasPermissionAny(userIdString, Permission_Manage, Permission_ManageCustom))
            {
                var playerData = GetPlayerData(userIdString);
                if (playerData.ActiveLoadout == string.Empty)
                {
                    // Player has explicitly set no active loadout.
                    return null;
                }

                if (playerData.ActiveLoadout != null)
                {
                    var loadout = playerData.FindByName(playerData.ActiveLoadout);
                    if (loadout == null)
                        return null;

                    var validationResult = playerData.ValidateAndPossiblyReduceLoadout(loadout, GetPlayerLoadoutRuleset(userIdString));
                    if (validationResult == LoadoutManager.ValidationResult.InvalidWeapon ||
                        validationResult == LoadoutManager.ValidationResult.DisallowedWeapon)
                        return null;

                    return loadout;
                }
            }

            // Player doesn't have permission to use custom loadouts, or they have not set an active one.
            return GetPlayerLastAllowedProfile(_config.DefaultLoadouts, userIdString);
        }

        private T GetPlayerLastAllowedProfile<T>(T[] profileList, string userIdString, T defaultProfile = null) where T : BaseProfile
        {
            if (profileList == null || profileList.Length == 0)
                return defaultProfile;

            for (var i = profileList.Length - 1; i >= 0; i--)
            {
                var profile = profileList[i];
                if (permission.UserHasPermission(userIdString, profile.Permission))
                    return profile;
            }

            return defaultProfile;
        }

        private LoadoutRuleset GetPlayerLoadoutRuleset(string userIdString)
        {
            return GetPlayerLastAllowedProfile(_config.LoadoutRulesets, userIdString, _config.EmptyLoadoutRuleset);
        }

        private LoadoutRuleset GetPlayerLoadoutRuleset(IPlayer player)
        {
            return GetPlayerLoadoutRuleset(player.Id);
        }

        private class Configuration : BaseConfiguration
        {
            [JsonIgnore]
            public readonly LoadoutRuleset EmptyLoadoutRuleset = new()
            {
                // Nothing allowed.
                AllowedWeapons = Array.Empty<string>(),
                AllowedAttachments = Array.Empty<string>(),
                AllowedAmmo = new Dictionary<string, int>(),
            };

            [JsonProperty("LockAutoFilledTurrets")]
            public bool LockAutoFilledTurrets = false;

            [JsonProperty("MaxLoadoutsPerPlayer")]
            public int MaxLoadoutsPerPlayer = 10;

            [JsonProperty("DefaultLoadouts")]
            public DefaultTurretLoadout[] DefaultLoadouts =
            {
                new()
                {
                    Name = "ak47",
                    Weapon = "rifle.ak",
                    Skin = 885146172,
                    Ammo = new AmmoAmount { Name = "ammo.rifle", Amount = 30 },
                    ReserveAmmo = new List<AmmoAmount>
                    {
                        new() { Name = "ammo.rifle", Amount = 128 },
                        new() { Name = "ammo.rifle", Amount = 128 },
                    },
                },
                new()
                {
                    Name = "m249",
                    Weapon = "lmg.m249",
                    Skin = 1831294069,
                    Attachments = new List<string>
                    {
                        "weapon.mod.lasersight",
                        "weapon.mod.silencer",
                    },
                    Ammo = new AmmoAmount { Name = "ammo.rifle.explosive", Amount = 100 },
                    ReserveAmmo = new List<AmmoAmount>
                    {
                        new() { Name = "ammo.rifle.incendiary", Amount = 128 },
                        new() { Name = "ammo.rifle.hv", Amount = 128 },
                    },
                },
            };

            [JsonProperty("LoadoutRulesets")]
            public LoadoutRuleset[] LoadoutRulesets =
            {
                new()
                {
                    Name = "onlypistols",
                    AllowedWeapons = new[]
                    {
                        "pistol.eoka",
                        "pistol.m92",
                        "pistol.nailgun",
                        "pistol.python",
                        "pistol.revolver",
                        "pistol.semiauto",
                        "pistol.water",
                    },
                    AllowedAmmo = new Dictionary<string, int>
                    {
                        ["ammo.pistol"] = 600,
                        ["ammo.pistol.hv"] = 400,
                        ["ammo.pistol.fire"] = 200,
                    },
                },
                new()
                {
                    Name = "norifles",
                    DisallowedWeapons = new[]
                    {
                        "rifle.ak",
                        "rifle.bolt",
                        "rifle.l96",
                        "rifle.lr300",
                        "rifle.m39",
                        "rifle.semiauto",
                        "lmg.m249",
                    },
                },
                new()
                {
                    Name = "unlimited",
                },
            };

            [JsonProperty("DefaultSamSiteLoadouts")]
            public DefaultBaseLoadout[] DefaultSamSiteLoadouts =
            {
                new()
                {
                    Name = "fullammo",
                    ReserveAmmo = new List<AmmoAmount>
                    {
                        new() { Name = "ammo.rocket.sam", Amount = 128 },
                        new() { Name = "ammo.rocket.sam", Amount = 128 },
                        new() { Name = "ammo.rocket.sam", Amount = 128 },
                        new() { Name = "ammo.rocket.sam", Amount = 128 },
                        new() { Name = "ammo.rocket.sam", Amount = 128 },
                        new() { Name = "ammo.rocket.sam", Amount = 128 },
                    },
                },
            };

            [JsonProperty("DefaultFlameTurretLoadouts")]
            public DefaultBaseLoadout[] DefaultFlameTurretLoadouts =
            {
                new()
                {
                    Name = "fullammo",
                    ReserveAmmo = new List<AmmoAmount>
                    {
                        new() { Name = "lowgradefuel", Amount = 500 }
                    },
                },
            };

            [JsonProperty("DefaultShotgunTrapLoadouts")]
            public DefaultBaseLoadout[] DefaultShotgunTrapLoadouts =
            {
                new()
                {
                    Name = "fullammo",
                    ReserveAmmo = new List<AmmoAmount>
                    {
                        new() { Name = "ammo.handmade.shell", Amount = 128 },
                        new() { Name = "ammo.handmade.shell", Amount = 128 },
                        new() { Name = "ammo.handmade.shell", Amount = 128 },
                        new() { Name = "ammo.handmade.shell", Amount = 128 },
                        new() { Name = "ammo.handmade.shell", Amount = 128 },
                        new() { Name = "ammo.handmade.shell", Amount = 128 },
                    },
                },
            };

            public void Init(TurretLoadouts pluginInstance)
            {
                foreach (var loadout in DefaultLoadouts)
                {
                    loadout.Init(pluginInstance, Permission_DefaultLoadoutPrefix);
                }

                foreach (var loadout in DefaultSamSiteLoadouts)
                {
                    loadout.Init(pluginInstance, Permission_DefaultSamSiteLoadoutPrefix);
                }

                foreach (var loadout in DefaultFlameTurretLoadouts)
                {
                    loadout.Init(pluginInstance, Permission_DefaultFlameTurretLoadoutPrefix);
                }

                foreach (var loadout in DefaultShotgunTrapLoadouts)
                {
                    loadout.Init(pluginInstance, Permission_DefaultShotgunTrapLoadoutPrefix);
                }

                foreach (var ruleset in LoadoutRulesets)
                {
                    ruleset.Init(pluginInstance, Permission_RulesetPrefix);
                }
            }
        }

        private abstract class BaseProfile
        {
            [JsonProperty("Name")]
            public string Name;

            [JsonIgnore]
            public string Permission;

            public void Init(TurretLoadouts pluginInstance, string permissionPrefix)
            {
                if (string.IsNullOrWhiteSpace(Name))
                    return;

                Permission = $"{permissionPrefix}.{Name}";
                pluginInstance.permission.RegisterPermission(Permission, pluginInstance);
            }
        }

        private class LoadoutRuleset : BaseProfile
        {
            [JsonProperty("AllowedWeapons", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string[] AllowedWeapons;

            [JsonProperty("DisallowedWeapons", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string[] DisallowedWeapons;

            [JsonProperty("AllowedAttachments", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string[] AllowedAttachments;

            [JsonProperty("DisallowedAttachments", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string[] DisallowedAttachments;

            [JsonProperty("AllowedAmmo", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public Dictionary<string, int> AllowedAmmo;

            public bool IsWeaponAllowed(string weaponName)
            {
                if (AllowedWeapons != null)
                    return AllowedWeapons.Contains(weaponName);

                if (DisallowedWeapons != null)
                    return !DisallowedWeapons.Contains(weaponName);

                return true;
            }

            public bool IsAttachmentAllowed(string attachmentName)
            {
                if (AllowedAttachments != null)
                    return AllowedAttachments.Contains(attachmentName);

                if (DisallowedAttachments != null)
                    return !DisallowedAttachments.Contains(attachmentName);

                return true;
            }
        }

        private class BaseLoadout : BaseProfile
        {
            [JsonProperty("ReserveAmmo", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public List<AmmoAmount> ReserveAmmo;

            [JsonIgnore]
            public virtual bool IsDefault => false;
        }

        private class DefaultBaseLoadout : BaseLoadout
        {
            public override bool IsDefault => true;
        }

        private class TurretLoadout : BaseLoadout
        {
            [JsonProperty("Weapon")]
            public string Weapon;

            [JsonProperty("Skin", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public ulong Skin;

            [JsonProperty("Peacekeeper", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool Peacekeeper;

            [JsonProperty("Attachments", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public List<string> Attachments;

            [JsonProperty("Ammo", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public AmmoAmount Ammo;
        }

        private class DefaultTurretLoadout : TurretLoadout
        {
            public override bool IsDefault => true;
        }

        private class AmmoAmount
        {
            [JsonProperty("Name")]
            public string Name;

            [JsonProperty("Amount")]
            public int Amount;
        }

        private Configuration GetDefaultConfig() => new();

        #region Configuration Helpers

        private class BaseConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(BaseConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            var changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                if (currentRaw.TryGetValue(key, out var currentRawValue))
                {
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (currentWithDefaults[key] is Dictionary<string, object> defaultDictValue)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                        {
                            changed = true;
                        }
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _config = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_config))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_config, true);
        }

        #endregion

        #endregion

        #region Localization

        private string GetMessage(string userIdString, string messageName, params object[] args)
        {
            var message = lang.GetMessage(messageName, this, userIdString);
            return args.Length > 0 ? string.Format(message, args) : message;
        }

        private string GetMessage(BasePlayer basePlayer, string messageName, params object[] args) =>
            GetMessage(basePlayer.UserIDString, messageName, args);

        private string GetMessage(IPlayer player, string messageName, params object[] args) =>
            GetMessage(player.Id, messageName, args);

        private void ReplyToPlayer(IPlayer player, string messageName, params object[] args) =>
            player.Reply(string.Format(GetMessage(player, messageName), args));

        private void ChatMessage(BasePlayer basePlayer, string messageName, params object[] args) =>
            basePlayer.ChatMessage(string.Format(GetMessage(basePlayer, messageName), args));

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["Generic.RestrictedItems"] = "<color=#f44>Restricted items not saved:</color>",
                ["Generic.DefaultLoadoutName"] = "Default",
                ["Generic.Error.NoPermission"] = "You don't have permission to use this command.",
                ["Generic.Error.NoTurretWeapon"] = "Error: That auto turret has no weapon.",
                ["Generic.Error.WeaponNotAllowed"] = "Error: Weapon not allowed: <color=#f44>{0}</color>.",
                ["Generic.Error.LoadoutNotFound"] = "Error: Loadout <color=#fe4>{0}</color> not found.",
                ["Generic.Error.LoadoutNameLength"] = "Error: Loadout name may not be longer than <color=#fe4>{0}</color> characters.",
                ["Generic.Error.DefaultLoadout"] = "Error: You cannot edit the default loadout.",
                ["Generic.Header"] = "<size=16><color=#fa5>Turret Loadouts</color></size>",
                ["Generic.FilledFromLoadout"] = "Filled turret with loadout: <color=#fe4>{0}</color>. Type <color=#fe4>/tl help</color> for more options.",

                ["Command.Activate.Error.Syntax"] = "Syntax: <color=#fe4>tl <loadout name></color>",
                ["Command.Activate.Success.Deactivated"] = "Deactivated <color=#fe4>{0}</color> loadout.",

                ["Command.Default.HelpHint"] = "Use <color=#fe4>tl help</color> for more options.",
                ["Command.Default.NoActive"] = "No active turret loadout.",
                ["Command.Default.Active"] = "<size=16><color=#fa5>Active Turret Loadout</color>: {0}</size>",
                ["Command.Default.Mode.AttackAll"] = "<color=#fe4>Mode</color>: <color=#f44>Attack All</color>",
                ["Command.Default.Mode.Peacekeeper"] = "<color=#fe4>Mode</color>: <color=#6e6>Peacekeeper</color>",
                ["Command.Default.Weapon"] = "<color=#fe4>Weapon</color>: {0}{1}",
                ["Command.Default.Attachments"] = "<color=#fe4>Attachments</color>:",
                ["Command.Default.ReserveAmmo"] = "<color=#fe4>Reserve ammo</color>:",

                ["Command.List.NoLoadouts"] = "You don't have any turret loadouts.",
                ["Command.List.ToggleHint"] = "Use <color=#fe4>tl <loadout name></color> to activate or deactivate a loadout.",
                ["Command.List.Item"] = "<color=#fe4>{1}</color>{0} - {2}{3}",
                ["Command.List.Item.Active"] = " <color=#5bf>[ACTIVE]</color>",

                ["Command.Save.Error.Syntax"] = "Syntax: <color=#fe4>tl save <name></color>",
                ["Command.Save.Error.NoTurretFound"] = "Error: No auto turret found.",
                ["Command.Save.Error.LoadoutExists"] = "Error: Loadout <color=#fe4>{0}</color> already exists. Use <color=#fe4>tl update {0}</color> to update it.",
                ["Command.Save.Error.TooManyLoadouts"] = "Error: You may not have more than <color=#fe4>{0}</color> loadouts. You may delete another loadout and try again.",
                ["Command.Save.Success"] = "Turret loadout saved as <color=#fe4>{0}</color>. Activate it with <color=#fe4>tl {0}</color>.",

                ["Command.Update.Error.Syntax"] = "Syntax: <color=#fe4>tl update <name></color>",
                ["Command.Update.Success"] = "Updated <color=#fe4>{0}</color> loadout.",

                ["Command.Rename.Error.Syntax"] = "Syntax: <color=#fe4>tl rename <name> <new name></color>",
                ["Command.Rename.Error.LoadoutNameTaken"] = "Error: Loadout name <color=#fe4>{0}</color> is already taken.",
                ["Command.Rename.Success"] = "Renamed <color=#fe4>{0}</color> loadout to <color=#fe4>{1}</color>.",

                ["Command.Delete.Error.Syntax"] = "Syntax: <color=#fe4>tl delete <name></color>",
                ["Command.Delete.Success"] = "Deleted <color=#fe4>{0}</color> loadout.",

                ["Command.Help.Details"] = "<color=#fe4>tl</color> - Show your active loadout details",
                ["Command.Help.List"] = "<color=#fe4>tl list</color> - List turret loadouts",
                ["Command.Help.Activate"] = "<color=#fe4>tl <loadout name></color> - Toggle whether a loadout is active",
                ["Command.Help.Save"] = "<color=#fe4>tl save <name></color> - Save a loadout with the turret you are aiming at",
                ["Command.Help.Update"] = "<color=#fe4>tl update <name></color> - Overwrite an existing loadout with the turret you are aiming at",
                ["Command.Help.Rename"] = "<color=#fe4>tl rename <name> <new name></color> - Rename a loadout",
                ["Command.Help.Delete"] = "<color=#fe4>tl delete <name></color> - Delete a loadout",

                ["Abbreviation.weapon.mod.8x.scope"] = "16x",
                ["Abbreviation.weapon.mod.flashlight"] = "FL",
                ["Abbreviation.weapon.mod.holosight"] = "HS",
                ["Abbreviation.weapon.mod.lasersight"] = "LS",
                ["Abbreviation.weapon.mod.muzzleboost"] = "MBS",
                ["Abbreviation.weapon.mod.muzzlebrake"] = "MBR",
                ["Abbreviation.weapon.mod.silencer"] = "SL",
                ["Abbreviation.weapon.mod.simplesight"] = "SS",
                ["Abbreviation.weapon.mod.small.scope"] = "8x",
            }, this, "en");
        }

        #endregion
    }
}
