using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Loot Scanner", "Sorrow", "0.4.0")]
    [Description("Allows player to scan loot container with binoculars")]

    class LootScanner : RustPlugin
    {
        #region Fields
        [PluginReference]
        Plugin PopupNotifications;

        private const int BinocularsId = -1262185308;
        private const string SupplyDrop = "supply_drop";
        private const string CH47Crate = "codelockedhackablecrate";
        private static readonly Dictionary<string, string> PrefabList = new Dictionary<string, string>();

        private const string PermissionWorld = "lootscanner.world";
        private const string PermissionPlayerContainer = "lootscanner.player";
        private const string PermissionSupplyDrop = "lootscanner.supplydrop";
        private const string PermissionCh47Crate = "lootscanner.ch47crate";
        private const string PermissionPlayerCorpse = "lootscanner.playercorpse";
        private const string PermissionAlivePlayer = "lootscanner.aliveplayer";
        private const string PermissionBackpack = "lootscanner.backpack";

        internal static string SideOfGui;
        internal static float PositionY;
        internal static string ColorNone;
        internal static string ColorCommon;
        internal static string ColorUncommon;
        internal static string ColorRare;
        internal static string ColorVeryRare;
        internal static bool HideAirdrop;
        internal static bool HideCh47Crate;
        internal static bool HideCrashSiteCrate;
        internal static bool HideCommonLootableCrate;
        internal static bool HidePlayerName;
        internal static bool HideCorpseName;
        #endregion

        #region uMod Hooks
        private void OnServerInitialized()
        {
            InitPrefabList();

            permission.RegisterPermission(PermissionWorld, this);
            permission.RegisterPermission(PermissionPlayerContainer, this);
            permission.RegisterPermission(PermissionSupplyDrop, this);
            permission.RegisterPermission(PermissionCh47Crate, this);
            permission.RegisterPermission(PermissionPlayerCorpse, this);
            permission.RegisterPermission(PermissionAlivePlayer, this);
            permission.RegisterPermission(PermissionBackpack, this);

            SideOfGui = Convert.ToString(Config["Define the side of GUI (Left - Right)"]);
            PositionY = Convert.ToSingle(Config["Define the y position of GUI"]);
            ColorNone = Convert.ToString(Config["Color None"]);
            ColorCommon = Convert.ToString(Config["Color Common"]);
            ColorUncommon = Convert.ToString(Config["Color Uncommon"]);
            ColorRare = Convert.ToString(Config["Color Rare"]);
            ColorVeryRare = Convert.ToString(Config["Color Very Rare"]);
            HideAirdrop = Convert.ToBoolean(Config["Hide Airdrop's content"]);
            HideCh47Crate = Convert.ToBoolean(Config["Hide CH47 crate content"]);
            HideCrashSiteCrate = Convert.ToBoolean(Config["Hide heli and Bradley crate content"]);
            HideCommonLootableCrate = Convert.ToBoolean(Config["Hide common lootable container"]);
            HidePlayerName = Convert.ToBoolean(Config["Hide player name"]);
            HideCorpseName = Convert.ToBoolean(Config["Hide corpse name"]);
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!player.IsValid() || !input.IsDown(BUTTON.FIRE_SECONDARY) && player.GetActiveItem()?.info.itemid == BinocularsId)
            {
                CuiHelper.DestroyUi(player, player.UserIDString);
                return;
            }

            if (!input.WasJustPressed(BUTTON.USE) || player.GetActiveItem()?.info.itemid != BinocularsId)
                return;

            BaseEntity entity = GetEntityScanned(player, input);
            if (!entity.IsValid())
                return;           

            StorageContainer storageContainer = entity as StorageContainer;
            LootableCorpse playerCorpse = entity as LootableCorpse;
            BasePlayer targetPlayer = entity as BasePlayer;
            DroppedItemContainer backpack = entity as DroppedItemContainer;

            if (storageContainer != null)
                ProcessStorageEntity(player, storageContainer);

            if (playerCorpse != null)
                ProcessCorpseEntity(player, playerCorpse);

            if (targetPlayer != null && targetPlayer.userID.IsSteamId() == true)
                ProcessTargetPlayer(player, targetPlayer);

            if (backpack != null)
                ProcessBackpack(player, backpack);
        }

        private void ProcessBackpack(BasePlayer player, DroppedItemContainer backpack)
        {
            if (permission.UserHasPermission(player.UserIDString, PermissionBackpack))
            {
                var backpackName = HidePlayerName ? "Unknown" : backpack.playerName;
                var title = "<color=orange>[Loot Scanner]</color>\n> " + backpackName + " <";

                if (backpack.inventory.itemList.Count > 0)
                {
                    var scannerMessage = "";
                    foreach (var item in backpack.inventory.itemList)
                    {
                        scannerMessage = BuildScannerMessage(scannerMessage, item);
                    }

                    CreateUi(player, title, scannerMessage);
                }
                else
                {
                    var ui = UI.ConstructScanUi(player, title, "> " + string.Format(lang.GetMessage("EmptyStorage", this, player.UserIDString), backpackName));
                    CuiHelper.DestroyUi(player, player.UserIDString);
                    CuiHelper.AddUi(player, ui);
                }
            }
            else
            {
                SendPermissionMessage(player);
            }
        }

        private void ProcessTargetPlayer(BasePlayer player, BasePlayer targetPlayer)
        {
            if (permission.UserHasPermission(player.UserIDString, PermissionAlivePlayer))
            {
                var targetPlayerName = HidePlayerName ? "Unknown" : targetPlayer.displayName;
                var title = "<color=orange>[Loot Scanner]</color>\n> " + targetPlayerName + " <";

                if (targetPlayer.inventory.containerMain.itemList.Count > 0)
                {
                    var scannerMessage = "";
                    foreach (var item in targetPlayer.inventory.containerMain.itemList)
                    {
                        scannerMessage = BuildScannerMessage(scannerMessage, item);
                    }

                    if (targetPlayer.inventory.containerBelt.itemList.Count > 0)
                        foreach (var item in targetPlayer.inventory.containerBelt.itemList)
                        {
                            scannerMessage = BuildScannerMessage(scannerMessage, item);
                        }

                    CreateUi(player, title, scannerMessage);
                }
                else
                {
                    var ui = UI.ConstructScanUi(player, title, "> " + string.Format(lang.GetMessage("EmptyStorage", this, player.UserIDString), targetPlayerName));
                    CuiHelper.DestroyUi(player, player.UserIDString);
                    CuiHelper.AddUi(player, ui);
                }
            }
            else
            {
                SendPermissionMessage(player);
            }
        }

        private void ProcessCorpseEntity(BasePlayer player, LootableCorpse corpse)
        {
            if (permission.UserHasPermission(player.UserIDString, PermissionPlayerCorpse))
            {
                var corpseName = HideCorpseName ? "Unknown" : corpse.playerName;
                var title = "<color=orange>[Loot Scanner]</color>\n> " + corpseName + " <";

                foreach (ItemContainer container in corpse.containers)
                {
                    if (container.itemList.Count == 0)
                    {
                        var ui = UI.ConstructScanUi(player, title, "> " + string.Format(lang.GetMessage("EmptyStorage", this, player.UserIDString), corpseName));
                        CuiHelper.DestroyUi(player, player.UserIDString);
                        CuiHelper.AddUi(player, ui);
                    }
                    else
                    {
                        var scannerMessage = "";
                        foreach (var item in container.itemList)
                        {
                            scannerMessage = BuildScannerMessage(scannerMessage, item);
                        }

                        CreateUi(player, title, scannerMessage);
                    }
                }
            }
            else
            {
                SendPermissionMessage(player);
            }
        }

        private void ProcessStorageEntity(BasePlayer player, StorageContainer storage)
        {
            if (storage.OwnerID != 0 && permission.UserHasPermission(player.UserIDString, PermissionPlayerContainer) ||
                storage.ShortPrefabName == SupplyDrop &&
                permission.UserHasPermission(player.UserIDString, PermissionSupplyDrop) ||
                storage.ShortPrefabName == CH47Crate &&
                permission.UserHasPermission(player.UserIDString, PermissionCh47Crate) ||
                storage.OwnerID == 0 && permission.UserHasPermission(player.UserIDString, PermissionWorld))
            {
                var itemDefinition = ItemManager.FindItemDefinition(storage.ShortPrefabName);

                var storageName = itemDefinition != null ? itemDefinition.displayName.english : GetPrefabName(storage);

                var title = "<color=orange>[Loot Scanner]</color>\n> " + storageName + " <";

                if (storage.inventory.itemList.Count > 0)
                {
                    var scannerMessage = "";
                    if (storage.ShortPrefabName == SupplyDrop && HideAirdrop || storage.ShortPrefabName == CH47Crate && HideCh47Crate ||
                        LootContainer.spawnType.CRASHSITE.Equals(storage.GetComponent<LootContainer>()?.SpawnType) && HideCrashSiteCrate ||
                        (LootContainer.spawnType.TOWN.Equals(storage.GetComponent<LootContainer>()?.SpawnType) || LootContainer.spawnType.ROADSIDE.Equals(storage.GetComponent<LootContainer>()?.SpawnType))
                        && HideCommonLootableCrate)
                    {
                        scannerMessage = BuildRarityScannerMessage(scannerMessage, storage.inventory.itemList);
                    }
                    else
                    {
                        foreach (var item in storage.inventory.itemList)
                        {
                            scannerMessage = BuildScannerMessage(scannerMessage, item);
                        }
                    }

                    CreateUi(player, title, scannerMessage);
                }
                else
                {
                    var ui = UI.ConstructScanUi(player, title, "> " + string.Format(lang.GetMessage("EmptyStorage", this, player.UserIDString), storageName));
                    CuiHelper.DestroyUi(player, player.UserIDString);
                    CuiHelper.AddUi(player, ui);
                }
            }
            else
            {
                SendPermissionMessage(player);
            }
        }

        private void SendPermissionMessage(BasePlayer player)
        {
            var permissions = permission.GetUserPermissions(player.UserIDString).Where(p => p.Contains("lootscanner")).ToList();

            if (permissions.Count > 1)
            {
                SendInfoMessage(player, string.Format(lang.GetMessage("TwoPermissions", this, player.UserIDString), BeautifyPermissionName(permissions[0]), BeautifyPermissionName(permissions[1])));
            }
            else if (permissions.Count == 1)
            {
                SendInfoMessage(player, string.Format(lang.GetMessage("OnePermission", this, player.UserIDString), BeautifyPermissionName(permissions[0])));
            }
            else
            {
                SendInfoMessage(player, lang.GetMessage("NoPermissions", this, player.UserIDString));
            }
        }

        private void CreateUi(BasePlayer player, string headerMessage, string contentMessage)
        {
            CuiElementContainer ui = UI.ConstructScanUi(player, headerMessage, contentMessage);
            CuiHelper.DestroyUi(player, player.UserIDString);
            CuiHelper.AddUi(player, ui);
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (newItem?.info.itemid == BinocularsId &&
                (permission.UserHasPermission(player.UserIDString, PermissionWorld) ||
                permission.UserHasPermission(player.UserIDString, PermissionPlayerContainer) ||
                permission.UserHasPermission(player.UserIDString, PermissionSupplyDrop) ||
                permission.UserHasPermission(player.UserIDString, PermissionCh47Crate)))
            {
                SendInfoMessage(player, string.Format(lang.GetMessage("InfoMessage", this, player.UserIDString), BUTTON.USE));
            }

            if (oldItem?.info.itemid == BinocularsId)
            {
                CuiHelper.DestroyUi(player, player.UserIDString);
            }
        }

        #endregion

        #region Helpers
        private static void InitPrefabList()
        {
            foreach (var item in ItemManager.GetItemDefinitions())
            {
                var itemModDeployable = item?.GetComponent<ItemModDeployable>();
                if (itemModDeployable == null) continue;

                var resourcePath = itemModDeployable.entityPrefab.resourcePath;
                var name = SplitPrefabName(resourcePath);
                if (!PrefabList.ContainsKey(name)) PrefabList.Add(name, item.displayName.english);
            }
        }

        private static string BuildScannerMessage(string scannerMessage, Item item)
        {
            var sb = new StringBuilder();

            sb.Append(scannerMessage);
            sb.Append(" > ");
            sb.Append("<color=" + GetColorOfItem(item) + ">");
            sb.Append(item.info.displayName.english);
            sb.Append("</color>");
            if (item.amount > 1)
            {
                sb.Append(" ");
                sb.Append("x");
                sb.Append(item.amount);
            }
            sb.Append("\n");
            return sb.ToString();
        }

        private static string BuildRarityScannerMessage(string scannerMessage, List<Item> itemList)
        {
            var none = 0;
            var common = 0;
            var uncommon = 0;
            var rare = 0;
            var veryRare = 0;

            foreach (var item in itemList)
            {
                if (item?.info == null) continue;
                switch ((int)item?.info?.rarity)
                {
                    case 0:
                        none += 1;
                        break;
                    case 1:
                        common += 1;
                        break;
                    case 2:
                        uncommon += 1;
                        break;
                    case 3:
                        rare += 1;
                        break;
                    case 4:
                        veryRare += 1;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            var sb = new StringBuilder();
            sb.Append(scannerMessage);

            if (veryRare > 0)
            {
                var item = veryRare > 1 ? "Items" : "Item";
                sb.Append(" > ");
                sb.Append("<color=" + ColorVeryRare + ">");
                sb.Append("Very Rare " + item);
                sb.Append("</color>");
                sb.Append(" ");
                sb.Append("x");
                sb.Append(veryRare);
                sb.Append("\n");
            }

            if (rare > 0)
            {
                var item = rare > 1 ? "Items" : "Item";
                sb.Append(" > ");
                sb.Append("<color=" + ColorRare + ">");
                sb.Append("Rare " + item);
                sb.Append("</color>");
                sb.Append(" ");
                sb.Append("x");
                sb.Append(rare);
                sb.Append("\n");
            }

            if (uncommon > 0)
            {
                var item = uncommon > 1 ? "Items" : "Item";
                sb.Append(" > ");
                sb.Append("<color=" + ColorUncommon + ">");
                sb.Append("Uncommon " + item);
                sb.Append("</color>");
                sb.Append(" ");
                sb.Append("x");
                sb.Append(uncommon);
                sb.Append("\n");
            }

            if (common > 0)
            {
                var item = common > 1 ? "Items" : "Item";
                sb.Append(" > ");
                sb.Append("<color=" + ColorCommon + ">");
                sb.Append("Common " + item);
                sb.Append("</color>");
                sb.Append(" ");
                sb.Append("x");
                sb.Append(common);
                sb.Append("\n");
            }

            if (none > 0)
            {
                var item = none > 1 ? "Items" : "Item";
                sb.Append(" > ");
                sb.Append("<color=" + ColorNone + ">");
                sb.Append("None " + item);
                sb.Append("</color>");
                sb.Append(" ");
                sb.Append("x");
                sb.Append(none);
                sb.Append("\n");
            }


            return sb.ToString();
        }

        private void SendInfoMessage(BasePlayer player, string message)
        {
            player?.SendConsoleCommand("gametip.showgametip", message);
            timer.Once(3f, () => player?.SendConsoleCommand("gametip.hidegametip"));
        }

        private static string GetColorOfItem(Item item)
        {
            var color = ColorNone;
            if (item?.info == null) return color;
            switch ((int)item?.info?.rarity)
            {
                case 0:
                    color = ColorNone;
                    break;
                case 1:
                    color = ColorCommon;
                    break;
                case 2:
                    color = ColorUncommon;
                    break;
                case 3:
                    color = ColorRare;
                    break;
                case 4:
                    color = ColorVeryRare;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return color;
        }

        private static BaseEntity GetEntityScanned(BasePlayer player, InputState input) // Thanks to ignignokt84
        {
            // Get player position + 1.6y as eye-level
            var playerEyes = player.transform.position + new Vector3(0f, 1.6f, 0f);
            var direction = Quaternion.Euler(input.current.aimAngles) * Vector3.forward;
            // Raycast in the direction the player is looking
            var hits = Physics.RaycastAll(playerEyes, direction);
            // Maximum distance when player can use loot scanner
            var closest = 10000f;
            var target = Vector3.zero;
            Collider collider = null;
            // Find the closest hit
            foreach (var hit in hits)
            {
                var name = hit.collider.gameObject.name;
                if (hit.collider.gameObject.layer == 18 || hit.collider.gameObject.layer == 29) // Skip Triggers layer
                    continue;
                // Ignore zones, meshes, and landmark nobuild hits
                if (name.StartsWith("Zone Manager") ||
                    name == "prevent_building" ||
                    name == "preventBuilding" ||
                    name == "Mesh")
                    continue;

                if (!(hit.distance < closest)) continue;
                closest = hit.distance;
                target = hit.point;
                collider = hit.collider;
            }
            if (target == Vector3.zero) return null;
            var entity = collider?.gameObject.ToBaseEntity();
            return entity;
        }

        private static string GetPrefabName(BaseNetworkable entity)
        {
            var name = SplitPrefabName(entity.gameObject.name).Replace("static", "deployed");

            return PrefabList.ContainsKey(name) ? PrefabList[name] : BeautifyPrefabName(entity.ShortPrefabName);
        }

        private static string SplitPrefabName(string prefabName)
        {
            return prefabName.Split('/').Last();
        }

        private static string BeautifyPrefabName(string str)
        {
            str = Regex.Replace(str, "[0-9\\(\\)]", string.Empty).Replace('_', ' ').Replace('-', ' ').Replace('.', ' ').Replace("static", string.Empty);
            var textInfo = new CultureInfo("en-US").TextInfo;
            return textInfo.ToTitleCase(str).Trim();
        }

        private static string BeautifyPermissionName(string str)
        {
            return str.Split('.').Last();
        }

        #endregion

        #region UI
        private class UI
        {
            private static CuiElementContainer CreateElementContainer(string name, string color, string anchorMin, string anchorMax, string parent = "Overlay")
            {
                var elementContainer = new CuiElementContainer()
                {
                    new CuiElement()
                    {
                        Name = name,
                        Parent = parent,
                        Components =
                        {
                            new CuiImageComponent()
                            {
                                Color = color,
                            },
                            new CuiRectTransformComponent()
                            {
                                AnchorMin = anchorMin,
                                AnchorMax = anchorMax
                            }
                        }
                    },
                };
                return elementContainer;
            }

            private static void CreateLabel(string name, string parent, ref CuiElementContainer container, TextAnchor textAnchor, string text, string color, int fontSize, string anchorMin, string anchorMax)
            {
                container.Add(new CuiElement()
                {
                    Name = name,
                    Parent = parent,
                    Components =
                    {
                        new CuiTextComponent()
                        {
                            Text = text,
                            Align = textAnchor,
                            FontSize = fontSize,
                            Font = "droidsansmono.ttf",
                            Color = color

                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = anchorMin,
                            AnchorMax = anchorMax
                        }
                    }
                });
            }

            private static void CreateElement(string name, string parent, ref CuiElementContainer container, string anchorMin, string anchorMax, string color)
            {
                container.Add(new CuiElement()
                {
                    Name = name,
                    Parent = parent,
                    Components =
                    {
                        new CuiImageComponent()
                        {
                            Color = color
                        },
                        new CuiRectTransformComponent()
                        {
                            AnchorMin = anchorMin,
                            AnchorMax = anchorMax
                        }
                    }
                });
            }

            public static CuiElementContainer ConstructScanUi(BasePlayer player, string title, string message)
            {
                var height = 0.66f;
                var anchorX = "0.78" + " " + (PositionY - height);
                var anchorY = "0.990" + " " + PositionY;

                if (SideOfGui.Equals("Left", StringComparison.InvariantCultureIgnoreCase))
                {
                    anchorX = "0.01" + " " + (PositionY - height);
                    anchorY = "0.22" + " " + PositionY;
                }

                var container = CreateElementContainer(player.UserIDString, "1 1 1 0.0", anchorX, anchorY);
                CreateElement("uiLabel", player.UserIDString, ref container, "0.05 0.05", "0.95 0.95", "0.3 0.3 0.3 0.0");
                CreateElement("uiLabelPadded", "uiLabel", ref container, "0.05 0.05", "0.95 0.95", "0 0 0 0");
                CreateLabel("uiLabelText", "uiLabelPadded", ref container, TextAnchor.MiddleCenter, title, "0.98 0.996 0.98 1", 14, "0 0.80", "1 1");
                CreateLabel("uiLabelText", "uiLabelPadded", ref container, TextAnchor.UpperLeft, message, "0.98 0.996 0.98 1", 11, "0 0", "1 0.80");

                return container;
            }
        }
        #endregion

        #region Config
        private new void LoadDefaultConfig()
        {
            PrintWarning("Creating a new configuration file");
            Config.Clear();

            Config["Color None"] = "white";
            Config["Color Common"] = "#56c63f";
            Config["Color Uncommon"] = "#0097ff";
            Config["Color Rare"] = "#b675f3";
            Config["Color Very Rare"] = "#ffbf17";

            Config["Define the side of GUI (Left - Right)"] = "Right";
            Config["Define the y position of GUI"] = 0.98f;

            Config["Hide Airdrop's content"] = false;
            Config["Hide CH47 crate content"] = false;
            Config["Hide heli and Bradley crate content"] = false;
            Config["Hide common lootable container"] = false;
            Config["Hide player name"] = true;
            Config["Hide corpse name"] = false;

            SaveConfig();
        }
        #endregion

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"InfoMessage", "Press {0} while looking through the binoculars to scan a container."},
                {"TwoPermissions", "You're only allowed to use Loot Scanner on {0}'s and {1}'s loot containers."},
                {"OnePermission", "You're only allowed to use Loot Scanner on {0}'s loot containers."},
                {"NoPermissions", "You're not allowed to use Loot Scanner."},
                {"EmptyStorage", "{0} is empty."},
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                {"InfoMessage", "Appuyez sur {0} tout en regardant à travers les jumelles pour scanner un conteneur."},
                {"TwoPermissions", "Vous n'êtes autorisé à utiliser Loot Scanner que sur les conteneurs de butin de {0} et {1}."},
                {"OnePermission", "Vous n'êtes autorisé à utiliser Loot Scanner que sur les conteneurs de butin de {0}."},
                {"NoPermissions", "Vous n'êtes pas autorisé à utiliser Loot Scanner."},
                {"EmptyStorage", "{0} est vide."},
            }, this, "fr");
        }

        #endregion
    }
}